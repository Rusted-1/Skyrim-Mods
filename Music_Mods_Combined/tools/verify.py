"""Check a built Music_Mods_Combined.esp against the source plugins.

Usage (from inside tools/):
    python verify.py <plugin folder> <Music_Mods_Combined.esp> [--order load_order.txt] [--rules rules.json]

Structure checks: ESL flag; masters exist and are in load order; HEDR record count; every overridden
music type exists in a source plugin; every track reference points at a real track record (a source
MUST or one of this patch's day/night copies); no null entries; no excluded track listed.

Copy checks (v0.4.0+): each copy (EditorID MMC_<D|N>_<load order index>_<object id>) matches its
source track (with fixes.txt applied) exactly after FormID remapping (same music file, cues, sub-tracks and conditions)
with the day or night time check placed in front.

Coverage: every track a source plugin put in a music type is still in the merged list, directly or
through at least one copy.

Shares: re-reads the built plugin from scratch, recomputes each music type's share per group hour by
hour, and compares with the rules (independent of the weighting code's own report).
"""
import argparse, collections, os, struct, sys
from esp import read_plugin
from build_combined import (load_order, find, Plugin, tracks, load_excludes, resolve_excludes, FORMID_FUNCS,
                            load_fixes, apply_fixes, replacer_groups, silence_seconds)
import weighting

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('folder'); ap.add_argument('built')
    ap.add_argument('--order', default=os.path.join(HERE, 'load_order.txt'))
    ap.add_argument('--rules', default=os.path.join(HERE, 'rules.json'))
    a = ap.parse_args()
    base_names, mod_names = load_order(a.order)
    all_names = base_names + mod_names
    plugins = {}
    for n in all_names:
        f = find(a.folder, n)
        if f:
            plugins[n.lower()] = Plugin(f, n)
    have_base = all(n.lower() in plugins for n in base_names)
    apply_fixes(load_fixes(os.path.join(HERE, 'fixes.txt')), list(plugins.values()))
    excl = resolve_excludes(load_excludes(os.path.join(HERE, 'exclude_tracks.txt')), list(plugins.values()))
    musts = {p.key(r.fid): (p, r) for p in plugins.values() for r in p.must}
    muscs = {p.key(r.fid) for p in plugins.values() for r in p.musc}
    rules = weighting.load_rules(a.rules) if a.rules.lower() != 'none' and os.path.exists(a.rules) else None
    repl = replacer_groups(rules, musts, os.path.dirname(os.path.abspath(a.rules))) if rules else {}

    hdr, masters, recs = read_plugin(a.built)
    errs = []
    if not hdr.flags & 0x200:
        errs.append('ESL flag not set')
    order = [n.lower() for n in all_names]
    pos = [order.index(m.lower()) if m.lower() in order else -1 for m in masters]
    if -1 in pos:
        errs.append(f'master not in load order: {[m for m, p in zip(masters, pos) if p < 0]}')
    if pos != sorted(pos):
        errs.append('masters are not in load order')
    groups = 1 + (1 if any(r.type == 'MUST' for r in recs) else 0)
    if len(recs) + groups != struct.unpack_from('<fII', hdr.get('HEDR'))[1]:
        errs.append('HEDR record count is wrong')
    me = os.path.basename(a.built).lower()
    mi = len(masters)

    def key(fid):
        i = fid >> 24
        return (me, fid & 0xFFFFFF) if i == mi else (masters[i].lower(), fid & 0xFFFFFF)

    # --- copies
    copies = {}   # own key -> (source key, period, rec)
    for r in recs:
        if r.type != 'MUST':
            continue
        try:
            _, period, tag, oid = r.edid.split('_')
            src = (all_names[int(tag)].lower(), int(oid, 16))
        except Exception:
            errs.append(f'{r.edid}: not a recognised copy EditorID'); continue
        if src not in musts:
            errs.append(f'{r.edid}: source track not found'); continue
        copies[key(r.fid)] = (src, period, r)
        p, s = musts[src]
        g = lambda x: p.key(x)
        want_ctda = []
        for c in s.all('CTDA'):
            c = bytearray(c); fn = struct.unpack_from('<H', c, 8)[0]
            if fn in FORMID_FUNCS:
                struct.pack_into('<I', c, 12, 0)
            want_ctda.append(bytes(c))
        got = r.all('CTDA')
        if rules:
            if period not in rules['period_names'] or got[:2] != weighting.gate_ctda(period, rules):
                errs.append(f'{r.edid}: time check missing or wrong')
        rest = []
        for i, c in enumerate(got[2:]):
            c = bytearray(c); fn = struct.unpack_from('<H', c, 8)[0]
            if fn in FORMID_FUNCS:
                orig = s.all('CTDA')[i]
                if key(struct.unpack_from('<I', c, 12)[0]) != g(struct.unpack_from('<I', orig, 12)[0]):
                    errs.append(f'{r.edid}: condition {i} points at the wrong record')
                struct.pack_into('<I', c, 12, 0)
            rest.append(bytes(c))
        if rest != want_ctda:
            errs.append(f'{r.edid}: conditions differ from the source track')
        if struct.unpack('<I', r.get('CITC'))[0] != len(got):
            errs.append(f'{r.edid}: condition count (CITC) wrong')
        for t in ('CNAM', 'FLTV', 'DNAM', 'ANAM', 'BNAM', 'FNAM', 'LNAM'):
            if r.get(t) != s.get(t):
                errs.append(f'{r.edid}: {t} differs from the source track')
        sn1, sn2 = r.get('SNAM') or b'', s.get('SNAM') or b''
        if [key(x) for x in struct.unpack(f'<{len(sn1) // 4}I', sn1)] != [g(x) for x in struct.unpack(f'<{len(sn2) // 4}I', sn2)]:
            errs.append(f'{r.edid}: sub-tracks differ from the source track')

    # --- music types
    total, sharelog = 0, []
    for r in recs:
        if r.type != 'MUSC':
            continue
        k = key(r.fid)
        if k not in muscs:
            errs.append(f'{r.edid}: overrides a music type no source plugin has')
        d = r.get('TNAM') or b''
        lst = [key(x) for x in struct.unpack(f'<{len(d) // 4}I', d)]
        total += len(lst)
        covered = set()
        for t in lst:
            if t[0] == me:
                if t not in copies:
                    errs.append(f'{r.edid}: points at a missing copy {t[1]:06X}')
                else:
                    covered.add(copies[t][0])
                continue
            covered.add(t)
            if t in excl:
                errs.append(f'{r.edid}: excluded track still listed')
            if t[1] == 0:
                errs.append(f'{r.edid}: null track entry')
            elif t not in musts and (have_base or t[0] not in [b.lower() for b in base_names]):
                errs.append(f'{r.edid}: track {t[0]}:{t[1]:06X} does not exist')
        for p in plugins.values():
            for src in p.musc:
                if p.key(src.fid) == k:
                    lost = {t for t in tracks(p, src) if t[1] and t not in covered and t not in excl
                            and not (rules and rules.get('pause') and 'SILENT' in musts[t][1].edid.upper())}
                    if lost:
                        errs.append(f'{r.edid}: {len(lost)} track(s) from {p.name} missing')
        if rules:
            sharelog.append(check_shares(r.edid, lst, copies, musts, rules, repl))

    print(f'{os.path.basename(a.built)}: {sum(1 for r in recs if r.type == "MUSC")} music types, '
          f'{len(copies)} day/night copies, {total} track entries, {len(masters)} masters')
    if not have_base:
        print('NOTE: game masters not supplied, so vanilla track references were not checked')
    if sharelog:
        worst = sorted(sharelog, key=lambda x: -x[1])[:5]
        print('Shares (independent recount) - average gap from the rules over 24 h, worst five:')
        for name, gap in worst:
            print(f'   {name}: {gap * 100:.1f}%')
        bad = [n for n, gap in sharelog if gap > 0.12]
        if bad:
            errs.append(f'share gap above 12% in: {", ".join(bad)}')
    for e in errs:
        print('ERROR:', e)
    print('OK' if not errs else f'{len(errs)} problem(s)')
    sys.exit(1 if errs else 0)


def check_shares(edid, lst, copies, musts, rules, repl):
    """Recount shares per slot from the built list and compare with weighting.targets()."""
    cls = rules['type_class'].get(edid, 'default')
    mass = [collections.Counter() for _ in range(weighting.SLOTS)]
    pause_entries = 0
    for t in lst:
        if t in copies:
            src, period, rec = copies[t]
            m = weighting.mask(rec.all('CTDA'))
        else:
            src = t; m = weighting.mask(musts[t][1].all('CTDA'))
        silent = 'SILENT' in musts[src][1].edid.upper()
        g = '_pause' if silent else repl.get(src, rules['groups'].get(src[0], src[0]))
        for s in range(weighting.SLOTS):
            if m[s]:
                mass[s][g] += 1
    gaps, pgaps = [], []
    for s in range(weighting.SLOTS):
        z = sum(mass[s].values())
        if not z:
            continue
        music = {g: v for g, v in mass[s].items() if g != '_pause'}
        mz = sum(music.values())
        if not mz:
            continue
        tgt = weighting.targets(cls, set(music), s in rules['night_slots'], rules)
        gaps.append(sum(abs(music.get(g, 0) / mz - tgt.get(g, 0)) for g in set(music) | set(tgt)) / 2)
        if mass[s].get('_pause') and rules.get('pause'):
            pgaps.append(abs(mass[s]['_pause'] / z - rules['pause']['share']))
    g = sum(gaps) / len(gaps) if gaps else 0.0
    if pgaps:
        g = max(g, sum(pgaps) / len(pgaps))
    return edid, g


if __name__ == '__main__':
    main()
