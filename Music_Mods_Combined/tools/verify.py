"""Check a built Music_Mods_Combined.esp against the source plugins.

Usage (from inside tools/):
    python verify.py <plugin folder> <Music_Mods_Combined.esp> [--order load_order.txt]

Checks: ESL flag, masters exist and are in load order, every overridden music type exists in a
source plugin, every track reference points at a real track record (MUST) in its file, no null
entries, and no track a source plugin listed for that music type is missing from the merged list.
"""
import argparse, os, struct, sys
from esp import read_plugin
from build_combined import load_order, find, Plugin, tracks, load_excludes, resolve_excludes


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('folder'); ap.add_argument('built')
    ap.add_argument('--order', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'load_order.txt'))
    a = ap.parse_args()
    base_names, mod_names = load_order(a.order)
    plugins = {}
    for n in base_names + mod_names:
        f = find(a.folder, n)
        if f:
            plugins[n.lower()] = Plugin(f, n)
    have_base = all(n.lower() in plugins for n in base_names)
    excl = resolve_excludes(load_excludes(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'exclude_tracks.txt')), list(plugins.values()))
    musts = {p.key(r.fid) for p in plugins.values() for r in p.must}
    muscs = {p.key(r.fid) for p in plugins.values() for r in p.musc}

    hdr, masters, recs = read_plugin(a.built)
    errs, warn = [], []
    if not hdr.flags & 0x200:
        errs.append('ESL flag not set')
    order = [n.lower() for n in base_names + mod_names]
    pos = [order.index(m.lower()) if m.lower() in order else -1 for m in masters]
    if -1 in pos:
        errs.append(f'master not in load order: {[m for m, p in zip(masters, pos) if p < 0]}')
    if pos != sorted(pos):
        errs.append('masters are not in load order')
    if len(recs) != struct.unpack_from('<fII', hdr.get('HEDR'))[1]:
        errs.append('HEDR record count is wrong')

    key = lambda fid: (masters[fid >> 24].lower(), fid & 0xFFFFFF)
    total = 0
    for r in recs:
        k = key(r.fid)
        if k not in muscs:
            errs.append(f'{r.edid}: overrides a music type no source plugin has')
        d = r.get('TNAM') or b''
        lst = [key(x) for x in struct.unpack(f'<{len(d) // 4}I', d)]
        total += len(lst)
        for t in lst:
            if t in excl:
                errs.append(f'{r.edid}: excluded track still listed')
            if t[1] == 0:
                errs.append(f'{r.edid}: null track entry')
            elif t not in musts and (have_base or t[0] not in [b.lower() for b in base_names]):
                errs.append(f'{r.edid}: track {t[0]}:{t[1]:06X} does not exist')
        merged = set(lst)
        for p in plugins.values():
            for src in p.musc:
                if p.key(src.fid) == k:
                    lost = {t for t in tracks(p, src) if t[1] and t not in merged and t not in excl}
                    if lost:
                        errs.append(f'{r.edid}: {len(lost)} track(s) from {p.name} missing')
    print(f'{os.path.basename(a.built)}: {len(recs)} music types, {total} track entries, {len(masters)} masters')
    if not have_base:
        print('NOTE: game masters not supplied, so vanilla track references were not checked')
    for e in errs:
        print('ERROR:', e)
    print('OK' if not errs else f'{len(errs)} problem(s)')
    sys.exit(1 if errs else 0)


if __name__ == '__main__':
    main()
