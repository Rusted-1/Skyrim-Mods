import struct, sys, os, lz4.frame, zlib
def read_bsa(path, outdir, filt=None):
    b=open(path,'rb').read()
    magic,ver,off,aflags,fcount,filecount,tfnl,tfl,fileflags=struct.unpack_from('<4sIIIIIIIH',b,0)
    compressed_def=bool(aflags&4); embed=bool(aflags&0x100)
    p=off; folders=[]
    for i in range(fcount):
        h,cnt,_,foff=struct.unpack_from('<QIIQ',b,p) if ver==105 else struct.unpack_from('<QII',b,p)+(0,)
        p+=24 if ver==105 else 16; folders.append(cnt)
    # folder records blocks
    entries=[]
    for cnt in folders:
        l=b[p]; name=b[p+1:p+l].rstrip(b'\0').decode(); p+=1+l
        for j in range(cnt):
            h,sz,o=struct.unpack_from('<QII',b,p); p+=16; entries.append([name,sz,o])
    names=b[p:p+tfl].split(b'\0')
    out=[]
    for e,n in zip(entries,names):
        full=e[0]+'\\'+n.decode(); sz=e[1]; o=e[2]
        comp=compressed_def ^ bool(sz&0x40000000); sz&=0x3fffffff
        if filt and not filt(full): out.append(full); continue
        data=b[o:o+sz]
        if embed:
            l=data[0]; data=data[1+l:]
        if comp:
            orig=struct.unpack_from('<I',data,0)[0]; data=data[4:]
            data=lz4.frame.decompress(data) if ver==105 else zlib.decompress(data)
        dest=os.path.join(outdir,*full.split('\\')); os.makedirs(os.path.dirname(dest),exist_ok=True)
        open(dest,'wb').write(data); out.append(full)
    return out
if __name__=='__main__':
    fl=read_bsa(sys.argv[1],sys.argv[2],lambda f: f.endswith(('.pex','.psc','.seq')))
    print(len(fl)); [print(f) for f in fl if f.endswith(('.pex','.psc','.seq'))]
