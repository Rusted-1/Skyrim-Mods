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
import argparse, collections, os, re, struct, sys
from esp import read_plugin, write_grup, write_subs, Rec, Sub, zstr
import weighting

FORMID_FUNCS = {149, 707, 359, 310, 543}   # condition functions whose first parameter is a FormID

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


def load_fixes(path):
    out = []
    if path and os.path.exists(path):
        for line in open(path, encoding='utf-8'):
            line = line.strip()
            if line and not line.startswith('#'):
                plugin, edid, action = [x.strip() for x in line.split('|')]
                out.append((plugin, edid, action))
    return out


def apply_fixes(fixes, plugins):
    """Apply fixes.txt to the in-memory track records (never to the files on disk)."""
    for plugin, edid, action in fixes:
        p = next((p for p in plugins if p.name.lower() == plugin.lower()), None)
        r = p and next((r for r in p.must if r.edid.lower() == edid.lower()), None)
        if not r:
            sys.exit(f'fixes.txt: {plugin}|{edid} not found')
        kind, i = action.split(':')
        conds = [s for s in r.subs if s.type == 'CTDA']
        if kind == 'or':
            c = bytearray(conds[int(i)].data); c[0] |= 1; conds[int(i)].data = bytes(c)
        else:
            sys.exit(f'fixes.txt: unknown action {action}')


def replacer_groups(rules, must, here):
    """Map track keys to a replacer group (e.g. LORKHAN) when a replacer mod swaps out their music.
    A track counts when its file (ignoring .wav/.xwm) is in the replacer's list; a palette counts when
    at least half of its parts do."""
    out = {}
    stem = lambda a: re.sub(r'\.(wav|xwm)$', '', re.sub(r'^/?data/', '', a.lower().replace('\\', '/')))
    for group, listfile in (rules.get('replacers') or {}).items():
        files = {stem(l.strip()) for l in open(os.path.join(here, listfile), encoding='utf-8') if l.strip()}
        for k, (p, r) in must.items():
            a = (r.get('ANAM') or b'').rstrip(b'\0').decode('cp1252')
            if a and stem(a) in files and 'SILENT' not in r.edid.upper():
                out[k] = group
        for k, (p, r) in must.items():
            sn = r.get('SNAM') or b''
            if sn and k not in out:
                kids = [p.key(x) for x in struct.unpack(f'<{len(sn) // 4}I', sn)]
                if kids and sum(1 for c in kids if out.get(c) == group) * 2 >= len(kids):
                    out[k] = group
    return out


def silence_seconds(r):
    f = r.get('FLTV')
    return struct.unpack('<f', f)[0] if f else 0.0


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
    ap.add_argument('--rules', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'rules.json'),
                    help='share rules (v0.4.0+); pass "none" for the plain v0.3.0 merge')
    ap.add_argument('--fixes', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixes.txt'))
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

    apply_fixes(load_fixes(a.fixes), base + mods)

    # Known track records (for validation and readable names)
    known_tracks, edids, must = set(), {}, {}
    for p in base + mods:
        for r in p.must:
            known_tracks.add(p.key(r.fid)); edids[p.key(r.fid)] = r.edid; must[p.key(r.fid)] = (p, r)
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

    rules = weighting.load_rules(a.rules) if a.rules and a.rules.lower() != 'none' else None
    repl = replacer_groups(rules, must, os.path.dirname(os.path.abspath(a.rules))) if rules else {}
    wreports = {}
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
        weighted = None
        if rules:
            uniq = list(dict.fromkeys(merged))
            info = {}
            for t in uniq:
                p_, r_ = must[t]
                info[t] = dict(group=repl.get(t, rules['groups'].get(t[0], t[0])), silence='SILENT' in r_.edid.upper(),
                               mask=weighting.mask(r_.all('CTDA')))
            pause = sum(1 for t in merged if info[t]['silence']) / len(merged)
            pr = rules.get('pause')
            if pr and any(info[t]['silence'] for t in uniq):
                # Every pause becomes one short silence that's allowed at all hours (rules.json "pause")
                pause = pr['share']
                pk = next(k for k, (p_, r_) in must.items() if r_.edid == pr['track'])
                uniq = [t for t in uniq if not info[t]['silence']] + [pk]
                info[pk] = dict(group='_pause', silence=True, mask=weighting.mask(must[pk][1].all('CTDA')))
                used_files.add(pk[0])
            weighted, wrep = weighting.weigh(winner.edid, uniq, info, pause, rules)
            wreports[winner.edid] = wrep
            merged = []
            for e, c in weighted:
                merged += [e] * c
        out_recs.append((k, winner, merged))
        used_files.add(k[0]); used_files.update(t[0] for t in merged if not isinstance(t[1], str))
        unknown = [t for t in merged if not isinstance(t[1], str) and t not in known_tracks and t[0] not in [m.lower() for m in missing_base]]
        report.append(dict(key=k, edid=winner.edid, editors=[p.name for p, _ in vs], opinions=opinions,
                           merged=merged, winner=winner_p.name, diffs=diffs, unknown=unknown))

    # Day/night copies (new track records of this patch)
    all_names = base_names + mod_names
    copies = {}                       # (key, period) -> new object id
    for _, _, merged in out_recs:
        for t in merged:
            if isinstance(t[1], str) and t not in copies:
                copies[t] = 0x800 + len(copies)
    if len(copies) > 0x800:
        sys.exit(f'{len(copies)} copies: more than an ESL-flagged plugin can hold')
    me = os.path.basename(a.out).lower()

    def copy_refs(src_key):
        p, r = must[src_key]
        refs = []
        sn = r.get('SNAM') or b''
        refs += [p.key(x) for x in struct.unpack(f'<{len(sn) // 4}I', sn)]
        for c in r.all('CTDA'):
            fn = struct.unpack_from('<H', c, 8)[0]
            run_on, ref = struct.unpack_from('<II', c, 20)
            if run_on == 2 and ref:
                refs.append(p.key(ref))
            if fn in FORMID_FUNCS:
                refs.append(p.key(struct.unpack_from('<I', c, 12)[0]))
            elif fn != 18:
                sys.exit(f'{r.edid}: condition function {fn} not supported for copies')
        return refs
    for (src, period) in copies:
        used_files.add(src[0]); used_files.update(x[0] for x in copy_refs(src))

    # Master list in load order, only files actually referenced
    order = [n for n in base_names if n.lower() in used_files] + [n for n in mod_names if n.lower() in used_files]
    idx = {n.lower(): i for i, n in enumerate(order)}
    mine = len(order)

    def fid(t):
        if isinstance(t[1], str):
            return (mine << 24) | copies[t]
        return (idx[t[0]] << 24) | t[1]

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

    must_recs = []
    for (src, period), oid in sorted(copies.items(), key=lambda kv: kv[1]):
        p, r = must[src]
        g = lambda x: fid(p.key(x))
        conds = [Sub('CTDA', c) for c in weighting.gate_ctda(period, rules)]
        for c in r.all('CTDA'):
            c = bytearray(c)
            fn = struct.unpack_from('<H', c, 8)[0]
            if fn in FORMID_FUNCS:
                struct.pack_into('<I', c, 12, g(struct.unpack_from('<I', c, 12)[0]))
            run_on, ref = struct.unpack_from('<II', c, 20)
            if run_on == 2 and ref:
                struct.pack_into('<I', c, 24, g(ref))
            conds.append(Sub('CTDA', bytes(c)))
        tag = all_names.index(names[src[0]])
        subs = [Sub('EDID', zstr(f'MMC_{period}_{tag:02d}_{src[1]:06X}'))]
        placed = False
        for s in r.subs:
            if s.type in ('EDID', 'CITC', 'CTDA'):
                continue
            if s.type == 'SNAM':
                if not placed:
                    subs += [Sub('CITC', struct.pack('<I', len(conds)))] + conds; placed = True
                sn = s.data
                subs.append(Sub('SNAM', struct.pack(f'<{len(sn) // 4}I', *[g(x) for x in struct.unpack(f'<{len(sn) // 4}I', sn)])))
                continue
            subs.append(s)
        if not placed:
            subs += [Sub('CITC', struct.pack('<I', len(conds)))] + conds
        must_recs.append(Rec('MUST', 0, (mine << 24) | oid, 0, 44, 0, subs))

    n_records = len(recs) + len(must_recs) + 1 + (1 if must_recs else 0)   # records + GRUPs
    hsubs = [Sub('HEDR', struct.pack('<fII', 1.71, n_records, 0x800 + len(copies))),
             Sub('CNAM', zstr('Rusted-1')),
             Sub('SNAM', zstr('Music Mods Combined: merges the track lists of all installed music mods so they play together with vanilla.'))]
    for n in order:
        hsubs += [Sub('MAST', zstr(n)), Sub('DATA', b'\0' * 8)]
    hdr = write_subs(hsubs)
    data = b'TES4' + struct.pack('<IIIIHH', len(hdr), 0x200, 0, 0, 44, 0) + hdr
    data += write_grup('MUSC', recs)
    if must_recs:
        data += write_grup('MUST', must_recs)
    with open(a.out, 'wb') as f:
        f.write(data)

    print(f'Wrote {a.out}: {len(recs)} music types, {len(must_recs)} day/night track copies, {len(order)} masters')
    if missing_base:
        print('WARNING: game masters not found, vanilla tracks only kept where a mod lists them:', ', '.join(missing_base))
    if a.report:
        write_report(a.report, report, order, missing_base, edids, names, nulls, wreports)


def weighting_rules_night(wreports):
    return next(iter(wreports.values()))['night_slots']


def nice(t, edids, names):
    return edids.get(t) or f'{names.get(t[0], t[0])}:{t[1]:06X}'


def write_report(path, report, order, missing_base, edids, names, nulls, wreports=None):
    L = ['# Merge report', '',
         f'Generated by `tools/build_combined.py`. Masters ({len(order)}): ' + ', '.join(f'`{n}`' for n in order) + '.', '']
    if missing_base:
        L += [f'**Built without the game masters** ({", ".join(missing_base)}): vanilla tracks are only kept where at least one mod lists them.', '']
    L += ['| Music type | Edited by | Tracks in merged list | Unique tracks | Header from |', '|---|---|---|---|---|']
    for r in report:
        L.append(f"| `{r['edid']}` | {len(r['editors'])} | {len(r['merged'])} | {len(set(r['merged']))} | {r['winner']} |")
    L += ['']
    if wreports:
        L += ['## Share rules (rules.json)', '',
              'Shares of all picks (music and pauses) achieved by the weighted list, averaged over the day (06:00-20:00) and the night. '
              '`err` is the average gap between achieved and target shares over the 24 h clock.', '',
              '| Music type | Rule class | Entries | err | Day shares | Night shares |', '|---|---|---|---|---|---|']
        def avg(sh, slots):
            acc = collections.Counter()
            for s in slots:
                for g, v in sh[s].items():
                    acc[g] += v / len(slots)
            return ', '.join(f"{'pause' if g == '_pause' else g} {v * 100:.0f}%" for g, v in acc.most_common())
        day = [s for s in range(weighting.SLOTS) if s not in weighting_rules_night(wreports)]
        for r in report:
            w = wreports.get(r['edid'])
            if w:
                ns = sorted(weighting_rules_night(wreports)); ds = [s for s in range(weighting.SLOTS) if s not in ns]
                L.append(f"| `{r['edid']}` | {w['cls']} | {w['total']} | {w['err'] * 100:.1f}% | {avg(w['shares'], ds)} | {avg(w['shares'], ns)} |")
        L += ['']
    for r in report:
        L += [f"## {r['edid']}", '']
        for label, c, lst in r['opinions']:
            L.append(f'- {label}: {len(lst)} entries, {len(c)} unique')
        L.append(f"- **Merged: {len(r['merged'])} entries, {len(set(r['merged']))} unique** (after share weighting, day/night copies counted separately)")
        if r['diffs']:
            L.append('- Header fields differ (merged plugin uses the last one): ' + '; '.join(
                f"{fld} " + ', '.join(f'{k}={v}' for k, v in vals.items()) for fld, vals in r['diffs'].items()))
        L.append('')
    open(path, 'w', encoding='utf-8').write('\n'.join(L))


if __name__ == '__main__':
    main()
