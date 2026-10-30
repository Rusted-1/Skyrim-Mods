"""List the music files a replacer mod installs, for rules.json "replacers".

Usage (from inside tools/):
    python list_replacer.py <mod archive .zip|.7z|.rar or extracted folder> > lorkhan_files.txt

Writes one path per line, relative to Data and lower-case (e.g.
music/explore/reach/palette/day/a/mus_palette_reach_day_a_01.xwm). FOMOD option folders
("10. Atmospheres/...") are stripped. Only paths under music/ are listed. The list holds file
names only, never any audio.
"""
import os, re, sys


def names(src):
    if os.path.isdir(src):
        for root, _, files in os.walk(src):
            for f in files:
                yield os.path.relpath(os.path.join(root, f), src)
    elif src.lower().endswith('.zip'):
        import zipfile
        yield from zipfile.ZipFile(src).namelist()
    else:
        import libarchive          # pip install libarchive-c, for .7z / .rar
        with libarchive.file_reader(src) as a:
            for e in a:
                yield e.pathname


def main():
    out = set()
    for n in names(sys.argv[1]):
        p = n.replace('\\', '/').lower()
        i = p.find('music/')
        if i < 0 or p.endswith('/'):
            continue
        out.add(p[i:])
    for p in sorted(out):
        print(p)


if __name__ == '__main__':
    main()
