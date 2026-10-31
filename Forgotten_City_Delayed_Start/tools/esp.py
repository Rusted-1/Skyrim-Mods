import struct, zlib, sys
def subs(data):
    i=0; out=[]; big=None
    while i < len(data):
        t=data[i:i+4].decode('latin1'); sz=struct.unpack_from('<H',data,i+4)[0]; i+=6
        if t=='XXXX': big=struct.unpack_from('<I',data,i)[0]; i+=sz; continue
        if big is not None: sz=big; big=None
        out.append((t,data[i:i+sz])); i+=sz
    return out
def records(buf, start=0, end=None, path=()):
    i=start; end=len(buf) if end is None else end
    while i < end:
        t=buf[i:i+4].decode('latin1'); sz=struct.unpack_from('<I',buf,i+4)[0]
        if t=='GRUP':
            label=buf[i+8:i+12]; gt=struct.unpack_from('<I',buf,i+12)[0]
            yield from records(buf,i+24,i+sz,path+((label,gt),)); i+=sz
        else:
            flags,fid=struct.unpack_from('<II',buf,i+8)
            data=buf[i+24:i+24+sz]
            if flags & 0x40000:
                data=zlib.decompress(data[4:])
            yield dict(type=t,flags=flags,fid=fid,data=data,path=path,off=i)
            i+=24+sz
def header(buf):
    sz=struct.unpack_from('<I',buf,4)[0]; flags=struct.unpack_from('<I',buf,8)[0]
    s=subs(buf[24:24+sz]); masters=[d[:-1].decode() for t,d in s if t=='MAST']
    return flags, masters, 24+sz
def edid(r):
    for t,d in subs(r['data']):
        if t=='EDID': return d[:-1].decode('latin1')
    return ''
