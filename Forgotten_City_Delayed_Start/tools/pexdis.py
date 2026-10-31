import struct, sys
OPS=[('nop',0),('iadd',3),('fadd',3),('isub',3),('fsub',3),('imul',3),('fmul',3),('idiv',3),('fdiv',3),('imod',3),('not',2),('ineg',2),('fneg',2),('assign',2),('cast',2),('cmp_eq',3),('cmp_lt',3),('cmp_le',3),('cmp_gt',3),('cmp_ge',3),('jmp',1),('jmpt',2),('jmpf',2),('callmethod',3),('callparent',2),('callstatic',3),('return',1),('strcat',3),('propget',3),('propset',3),('array_create',2),('array_length',2),('array_getelement',3),('array_setelement',3),('array_findelement',4),('array_rfindelement',4)]
class R:
    def __init__(s,b): s.b=b; s.p=0
    def u(s,f): v=struct.unpack_from('>'+f,s.b,s.p); s.p+=struct.calcsize('>'+f); return v[0]
    def ws(s): n=s.u('H'); v=s.b[s.p:s.p+n].decode('latin1'); s.p+=n; return v
def dis(path, out=print):
    r=R(open(path,'rb').read()); assert r.u('I')==0xFA57C0DE
    r.u('B');r.u('B');r.u('H');r.u('Q'); src=r.ws(); r.ws(); r.ws()
    S=[r.ws() for _ in range(r.u('H'))]
    if r.u('B'):
        r.u('Q')
        for _ in range(r.u('H')):
            r.u('H');r.u('H');r.u('H');r.u('B'); n=r.u('H'); r.p+=2*n
    for _ in range(r.u('H')): r.u('H'); r.u('B')
    def var():
        t=r.u('B')
        if t==0: return 'None'
        if t==1: return S[r.u('H')]
        if t==2: return repr(S[r.u('H')])
        if t==3: return str(r.u('i'))
        if t==4: return repr(r.u('f'))
        if t==5: return str(bool(r.u('B')))
    def func(name):
        rt=S[r.u('H')]; r.u('H'); r.u('I'); fl=r.u('B')
        ps=[(S[r.u('H')],S[r.u('H')]) for _ in range(r.u('H'))]
        ls=[(S[r.u('H')],S[r.u('H')]) for _ in range(r.u('H'))]
        out(f'  FUNCTION {rt} {name}({", ".join(t+" "+n for n,t in ps)}) flags={fl}')
        for n,t in ls: out(f'    local {t} {n}')
        for i in range(r.u('H')):
            op=r.u('B'); nm,na=OPS[op]; a=[var() for _ in range(na)]
            if nm in('callmethod','callparent','callstatic'):
                cnt=int(var()); a+= [var() for _ in range(cnt)]
            out(f'    {i:3d} {nm} '+' '.join(a))
    for _ in range(r.u('H')):
        name=S[r.u('H')]; r.u('I'); parent=S[r.u('H')]; r.u('H'); r.u('I'); auto=S[r.u('H')]
        out(f'SCRIPT {name} extends {parent}  (src {src})')
        for _ in range(r.u('H')):
            vn=S[r.u('H')]; vt=S[r.u('H')]; r.u('I'); out(f'  var {vt} {vn} = {var()}')
        for _ in range(r.u('H')):
            pn=S[r.u('H')]; pt=S[r.u('H')]; r.u('H'); r.u('I'); fl=r.u('B')
            if fl&4: out(f'  property {pt} {pn} auto -> {S[r.u("H")]}')
            else:
                out(f'  property {pt} {pn}')
                if fl&1: func(pn+'.get')
                if fl&2: func(pn+'.set')
        for _ in range(r.u('H')):
            st=S[r.u('H')]; out(f' STATE "{st}"')
            for _ in range(r.u('H')): func(S[r.u('H')])
if __name__=='__main__':
    for p in sys.argv[1:]: dis(p)
