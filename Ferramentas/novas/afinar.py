import numpy as np, struct, sys
def ler(f):
    d=open(f,'rb').read(); nv,ni,L=struct.unpack('<iif',d[4:16]); v=np.frombuffer(d,'<f4',nv*8,16).reshape(nv,8).copy(); i=np.frombuffer(d,'<u4',ni,16+nv*32).copy(); return v,i,L
def gravar(f,v,i,L):
    open(f,'wb').write(b'FOIC'+struct.pack('<iif',len(v),len(i),L)+v.astype('<f4').tobytes()+i.astype('<u4').tobytes())
ALVO=0.0150  # raio/comprimento da Obsidiana
def afinar(arq, extras=(), cabeca=None):
    v,i,L=ler(arq); P=v[:,:3]
    # inicio da cabeca: primeira altura (de baixo para cima) onde algo passa 0.1 para o lado da lamina
    ys=np.linspace(0.3*L,L,200); yHead=L
    for y in ys:
        m=(P[:,1]>y-0.005)&(P[:,1]<y+0.005)
        if m.sum()>5 and P[m,2].max()-np.median(P[m,2])>0.14: yHead=y; break
    yHead-=0.03
    if cabeca: yHead=cabeca
    bins=np.linspace(0,yHead,40); cx=[];cz=[];r=[]
    for a,b in zip(bins[:-1],bins[1:]):
        m=(P[:,1]>=a)&(P[:,1]<b)
        x=np.median(P[m,0]); z=np.median(P[m,2]); cx.append(x); cz.append(z)
        r.append(np.median(np.hypot(P[m,0]-x,P[m,2]-z)))
    yc=(bins[:-1]+bins[1:])/2; cx=np.convolve(cx,np.ones(3)/3,'same'); cz=np.convolve(cz,np.ones(3)/3,'same')
    rmed=np.median(r[2:-2]); f=min(1.0, ALVO*L/ (rmed*1.25))
    def aplica(Q):
        y=Q[:,1]; x0=np.interp(y,yc,cx); z0=np.interp(y,yc,cz)
        t=np.clip((yHead-y)/0.08,0,1); fe=1+(f-1)*t
        Q=Q.copy(); Q[:,0]=x0+(Q[:,0]-x0)*fe; Q[:,2]=z0+(Q[:,2]-z0)*fe; return Q
    v[:,:3]=aplica(P); gravar(arq,v,i,L)
    for e in extras:
        ve,ie,Le=ler(e); ve[:,:3]=aplica(ve[:,:3]); gravar(e,ve,ie,Le)
    print(arq,'L',round(L,3),'cabeca em',round(yHead,3),'raio',round(float(rmed),4),'fator',round(f,2))
if __name__=='__main__':
    afinar(sys.argv[1], sys.argv[2:])
