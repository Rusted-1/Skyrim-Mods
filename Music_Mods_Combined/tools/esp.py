"""Minimal Skyrim SE plugin (.esp/.esm/.esl) reader/writer helpers.

Only what the music patch needs: read records (optionally just some top-level groups),
parse subrecords, and write a plain uncompressed plugin back out.
"""
import struct, zlib


class Sub:
    def __init__(s, t, d):
        s.type, s.data = t, d

    def __repr__(s):
        return f"{s.type}({len(s.data)})"


class Rec:
    def __init__(s, t, flags, fid, vc, ver, unk, subs):
        s.type, s.flags, s.fid, s.vc, s.ver, s.unk, s.subs = t, flags, fid, vc, ver, unk, subs

    def get(s, t):
        for x in s.subs:
            if x.type == t:
                return x.data

    def all(s, t):
        return [x.data for x in s.subs if x.type == t]

    @property
    def edid(s):
        d = s.get('EDID')
        return d.rstrip(b'\0').decode('cp1252') if d else ''


def parse_subs(data):
    subs, i, big = [], 0, None
    while i < len(data):
        t = data[i:i + 4].decode('ascii')
        sz = struct.unpack_from('<H', data, i + 4)[0]
        i += 6
        if t == 'XXXX':
            big = struct.unpack_from('<I', data, i)[0]
            i += sz
            continue
        if big is not None:
            sz, big = big, None
        subs.append(Sub(t, data[i:i + sz]))
        i += sz
    return subs


def read_plugin(path, groups=None):
    """Return (header record, master names, records).

    groups: optional set of top-level group labels (e.g. {'MUSC', 'MUST'}); other top-level
    groups are skipped without being parsed, which keeps reading Skyrim.esm fast.
    """
    b = open(path, 'rb').read()
    recs = []

    def walk(i, end, top):
        while i < end:
            t = b[i:i + 4].decode('ascii')
            sz = struct.unpack_from('<I', b, i + 4)[0]
            if t == 'GRUP':
                label = b[i + 8:i + 12]
                if not (top and groups is not None and label.decode('ascii', 'replace') not in groups):
                    walk(i + 24, i + sz, False)
                i += sz
                continue
            flags, fid, vc, ver, unk = struct.unpack_from('<IIIHH', b, i + 8)
            data = b[i + 24:i + 24 + sz]
            if flags & 0x40000:
                data = zlib.decompress(data[4:])
            recs.append(Rec(t, flags, fid, vc, ver, unk, parse_subs(data)))
            i += 24 + sz

    walk(0, len(b), True)
    hdr = recs[0]
    masters = [m.rstrip(b'\0').decode('cp1252') for m in hdr.all('MAST')]
    return hdr, masters, recs[1:]


def write_subs(subs):
    out = b''
    for s in subs:
        if len(s.data) > 0xFFFF:
            out += b'XXXX' + struct.pack('<HI', 4, len(s.data)) + s.type.encode() + struct.pack('<H', 0)
        else:
            out += s.type.encode() + struct.pack('<H', len(s.data))
        out += s.data
    return out


def write_rec(r):
    d = write_subs(r.subs)
    return r.type.encode() + struct.pack('<IIIIHH', len(d), r.flags & ~0x40000, r.fid, r.vc, r.ver, r.unk) + d


def write_grup(label, recs):
    body = b''.join(write_rec(r) for r in recs)
    return b'GRUP' + struct.pack('<I', 24 + len(body)) + label.encode() + struct.pack('<IIHH', 0, 0, 0, 0) + body


def zstr(s):
    return s.encode('cp1252') + b'\0'
