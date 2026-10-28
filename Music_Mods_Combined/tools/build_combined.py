"""Build Music_Mods_Combined.esp: one patch that merges every music mod's track lists.

Usage (from inside tools/):
    python build_combined.py <plugin folder> [--order load_order.txt] [--out Music_Mods_Combined.esp]
                             [--report merge-report.md]

<plugin folder> holds the plugins named in load_order.txt (the music mods' .esp files and the
game's Skyrim.esm / Update.esm / Dawnguard.esm). Only their music records are read.

How each music type (MUSC) is merged
------------------------------------
* Every plugin that edits a music type gives an opinion: how many times each track appears in
  its list. Duplicates matter, because the game picks tracks at random and repeats act as weight
  (vanilla lists repeat the silence tracks many times to space music out).
* The merged list keeps each track as many times as the most generous opinion has it, so no mod
  loses a track and no mod's weighting is cut.
* The vanilla list from Skyrim.esm / Update.esm / Dawnguard.esm counts as an opinion too, so
  vanilla tracks that a mod removed come back (vanilla is meant to play alongside the mods).
* Compatibility patches win over the mods they patch: if plugin P has plugin Q as a master and
  both edit the same music type, Q's opinion on shared tracks is replaced by P's, because P's
  author already merged Q on purpose. Q's own new tracks are always kept, since a patch built
  for an older version of Q may simply not know about them.
* Empty (null) track entries are dropped, and so are the tracks named in exclude_tracks.txt
  (tracks that point at music files which no longer exist).
* Everything else on the music type (flags, priority, fade) comes from the last plugin in load
  order that edits it; any disagreement is listed in the report.
Track records (MUST) are never copied or edited, so each mod's own conditions (time of day,
eclipse, location, combat target) keep working exactly as their authors set them up.
"""
import argparse, collections, os, struct, sys
from esp import read_plugin, write_grup, write_subs, Rec, Sub, zstr

OUT_NAME = 'Music_Mods_Combined.esp'
HEADER_FIELDS = ('FNAM', 'PNAM', 'WNAM')


def load_order(path):
    base, mods = [], []
    for line in open(path, encoding='utf-8'):
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        if line.startswith('@base '):
            base.append(line[6:].strip())
        else:
            mods.append(line)
    return base, mods


def load_excludes(path):
    out = []
    if path and os.path.exists(path):
        for line in open(path, encoding='utf-8'):
            line = line.strip()
            if line and not line.startswith('#'):
                plugin, edid = line.split('|', 1)
                out.append((plugin.strip(), edid.strip()))
    return out


def resolve_excludes(excl, plugins):
    keys = set()
    for plugin, edid in excl:
        p = next((p for p in plugins if p.name.lower() == plugin.lower()), None)
        r = p and next((r for r in p.must if r.edid.lower() == edid.lower()), None)
        if not r:
            sys.exit(f'exclude_tracks.txt: {plugin}|{edid} not found')
        keys.add(p.key(r.fid))
    return keys


def find(folder, name):
    for f in os.listdir(folder):
        if f.lower() == name.lower():
            return os.path.join(folder, f)
    return None


class Plugin:
    def __init__(self, path, name):
        self.name = name
        hdr, self.masters, recs = read_plugin(path, groups={'MUSC', 'MUST'})
        self.esl = bool(hdr.flags & 0x200)
        self.musc = [r for r in recs if r.type == 'MUSC']
        self.must = [r for r in recs if r.type == 'MUST']

    def key(self, fid):
        """FormID -> (file name, object id) in global terms."""
        i = fid >> 24
        f = self.masters[i] if i < len(self.masters) else self.name
        return (f.lower(), fid & 0xFFFFFF)


def tracks(p, rec):
    d = rec.get('TNAM') or b''
    return [p.key(x) for x in struct.unpack(f'<{len(d) // 4}I', d)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('folder')
    ap.add_argument('--order', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'load_order.txt'))
    ap.add_argument('--exclude', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'exclude_tracks.txt'))
    ap.add_argument('--out', default=OUT_NAME)
    ap.add_argument('--report')
    a = ap.parse_args()

    base_names, mod_names = load_order(a.order)
    names = {}       # lower -> display name
    base, mods = [], []
    missing_base = []
    for n in base_names:
        f = find(a.folder, n)
        if f:
            base.append(Plugin(f, n))
        else:
            missing_base.append(n)
        names[n.lower()] = n
    for n in mod_names:
        f = find(a.folder, n)
        if not f:
            sys.exit(f'Missing plugin: {n} (expected in {a.folder})')
        mods.append(Plugin(f, n))
        names[n.lower()] = n

    # Known track records (for validation and readable names)
    known_tracks, edids = set(), {}
    for p in base + mods:
        for r in p.must:
            known_tracks.add(p.key(r.fid)); edids[p.key(r.fid)] = r.edid
        for r in p.musc:
            edids[p.key(r.fid)] = r.edid

    excluded = resolve_excludes(load_excludes(a.exclude), mods)

    vanilla = {}
    for p in base:
        for r in p.musc:
            vanilla[p.key(r.fid)] = (p, r)

    versions = collections.defaultdict(list)   # music type -> [(plugin, rec)] in load order
    for p in mods:
        for r in p.musc:
            k = p.key(r.fid)
            if k[0] == p.name.lower():
                continue      # a brand-new music type, nothing to merge
            versions[k].append((p, r))

    out_recs, report = [], []
    used_files = set()
    nulls = 0
    for k in sorted(versions, key=lambda k: (names.get(k[0], k[0]), k[1])):
        vs = versions[k]
        editors = [p.name.lower() for p, _ in vs]
        opinions = []   # (label, Counter, ordered list)
        for p, r in vs:
            t = tracks(p, r)
            later_patches = [q.name for q, _ in vs if p.name.lower() in [m.lower() for m in q.masters]]
            if later_patches:
                own = [x for x in t if x[0] == p.name.lower()]
                opinions.append((f'{p.name} (own tracks only; superseded by {", ".join(later_patches)})', collections.Counter(own), own))
            else:
                opinions.append((p.name, collections.Counter(t), t))
        if k in vanilla:
            vp, vr = vanilla[k]
            t = tracks(vp, vr)
            opinions.insert(0, (f'vanilla ({vp.name})', collections.Counter(t), t))

        want = collections.Counter()
        for _, c, _ in opinions:
            for t, n in c.items():
                if t[1] == 0 or t in excluded:
                    continue
                want[t] = max(want[t], n)
        nulls += sum(1 for _, c, _ in opinions if any(t[1] == 0 for t in c))

        # Order: walk the opinions (vanilla first, then load order) and emit until counts are met.
        merged, have = [], collections.Counter()
        for _, _, lst in opinions:
            for t in lst:
                if t[1] != 0 and t not in excluded and have[t] < want[t]:
                    merged.append(t); have[t] += 1

        winner_p, winner = vs[-1]
        if winner.all('CTDA'):
            sys.exit(f'{winner.edid}: music type has conditions; remapping them is not supported yet')
        diffs = {}
        for fld in HEADER_FIELDS:
            vals = {p.name: (r.get(fld) or b'').hex() for p, r in vs}
            if k in vanilla:
                vals = {f'vanilla': (vanilla[k][1].get(fld) or b'').hex(), **vals}
            if len(set(vals.values())) > 1:
                diffs[fld] = vals
        out_recs.append((k, winner, merged))
        used_files.add(k[0]); used_files.update(t[0] for t in merged)
        unknown = [t for t in merged if t not in known_tracks and t[0] not in [m.lower() for m in missing_base]]
        report.append(dict(key=k, edid=winner.edid, editors=[p.name for p, _ in vs], opinions=opinions,
                           merged=merged, winner=winner_p.name, diffs=diffs, unknown=unknown))

    # Master list in load order, only files actually referenced
    order = [n for n in base_names if n.lower() in used_files] + [n for n in mod_names if n.lower() in used_files]
    idx = {n.lower(): i for i, n in enumerate(order)}
    fid = lambda t: (idx[t[0]] << 24) | t[1]

    recs = []
    for k, winner, merged in out_recs:
        subs = []
        for s in winner.subs:
            if s.type == 'TNAM':
                subs.append(Sub('TNAM', struct.pack(f'<{len(merged)}I', *[fid(t) for t in merged])))
            else:
                subs.append(s)
        if not winner.get('TNAM') and merged:
            subs.append(Sub('TNAM', struct.pack(f'<{len(merged)}I', *[fid(t) for t in merged])))
        recs.append(Rec('MUSC', 0, fid(k), 0, 44, 0, subs))

    hsubs = [Sub('HEDR', struct.pack('<fII', 1.71, len(recs), 0x800)),
             Sub('CNAM', zstr('Rusted-1')),
             Sub('SNAM', zstr('Music Mods Combined: merges the track lists of all installed music mods so they play together with vanilla.'))]
    for n in order:
        hsubs += [Sub('MAST', zstr(n)), Sub('DATA', b'\0' * 8)]
    hdr = write_subs(hsubs)
    data = b'TES4' + struct.pack('<IIIIHH', len(hdr), 0x200, 0, 0, 44, 0) + hdr
    data += write_grup('MUSC', recs)
    with open(a.out, 'wb') as f:
        f.write(data)

    print(f'Wrote {a.out}: {len(recs)} music types, {len(order)} masters')
    if missing_base:
        print('WARNING: game masters not found, vanilla tracks only kept where a mod lists them:', ', '.join(missing_base))
    if a.report:
        write_report(a.report, report, order, missing_base, edids, names, nulls)


def nice(t, edids, names):
    return edids.get(t) or f'{names.get(t[0], t[0])}:{t[1]:06X}'


def write_report(path, report, order, missing_base, edids, names, nulls):
    L = ['# Merge report', '',
         f'Generated by `tools/build_combined.py`. Masters ({len(order)}): ' + ', '.join(f'`{n}`' for n in order) + '.', '']
    if missing_base:
        L += [f'**Built without the game masters** ({", ".join(missing_base)}): vanilla tracks are only kept where at least one mod lists them.', '']
    L += ['| Music type | Edited by | Tracks in merged list | Unique tracks | Header from |', '|---|---|---|---|---|']
    for r in report:
        L.append(f"| `{r['edid']}` | {len(r['editors'])} | {len(r['merged'])} | {len(set(r['merged']))} | {r['winner']} |")
    L += ['']
    for r in report:
        L += [f"## {r['edid']}", '']
        for label, c, lst in r['opinions']:
            L.append(f'- {label}: {len(lst)} entries, {len(c)} unique')
        L.append(f"- **Merged: {len(r['merged'])} entries, {len(set(r['merged']))} unique**")
        if r['diffs']:
            L.append('- Header fields differ (merged plugin uses the last one): ' + '; '.join(
                f"{fld} " + ', '.join(f'{k}={v}' for k, v in vals.items()) for fld, vals in r['diffs'].items()))
        L.append('')
    open(path, 'w', encoding='utf-8').write('\n'.join(L))


if __name__ == '__main__':
    main()
