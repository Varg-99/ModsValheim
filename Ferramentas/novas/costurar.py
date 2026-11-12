# Fecha as rachaduras da malha (frestas finas entre os pedacos da textura, criadas na reducao do Blender).
# Cada vertice da borda e puxado para o ponto mais proximo de uma borda vizinha (ate TOL de distancia).
import numpy as np, struct, sys
from scipy.spatial import cKDTree
def ler(f):
    d=open(f,'rb').read(); nv,ni,L=struct.unpack('<iif',d[4:16]); v=np.frombuffer(d,'<f4',nv*8,16).reshape(nv,8).copy(); i=np.frombuffer(d,'<u4',ni,16+nv*32).reshape(-1,3).copy(); return v,i,L
def gravar(f,v,i,L):
    open(f,'wb').write(b'FOIC'+struct.pack('<iif',len(v),i.size,L)+v.astype('<f4').tobytes()+i.astype('<u4').tobytes())
def soldar(P,tol=1e-6):
    q=np.round(P/tol).astype(np.int64); _,w,inv=np.unique(q,axis=0,return_index=True,return_inverse=True); return inv.ravel(), len(w)
def bordas(t):
    e=np.concatenate([t[:,[0,1]],t[:,[1,2]],t[:,[2,0]]]); s=np.sort(e,1); k=s[:,0]*(1<<32)+s[:,1]
    u,ix,c=np.unique(k,return_index=True,return_counts=True); return s[ix[c==1]]
def juntar_vertices(P,i,tol):
    w,n=soldar(P); Q=np.zeros((n,3)); Q[w]=P; bv=np.unique(bordas(w[i]))
    pares=cKDTree(Q[bv]).query_pairs(tol,output_type='ndarray')
    par=np.arange(len(bv))
    def f(x):
        r=x
        while par[r]!=r: r=par[r]
        while par[x]!=r: par[x],x=r,par[x]
        return r
    for a,b in pares:
        ra,rb=f(a),f(b)
        if ra!=rb: par[max(ra,rb)]=min(ra,rb)
    raiz=np.array([f(k) for k in range(len(bv))])
    soma=np.zeros((len(bv),3)); np.add.at(soma,raiz,Q[bv]); cnt=np.bincount(raiz,minlength=len(bv))
    cen=soma[raiz]/cnt[raiz][:,None]
    dist=np.linalg.norm(Q[bv]-cen,axis=1); maior=np.zeros(len(bv)); np.maximum.at(maior,raiz,dist)
    ok=maior[raiz]<=tol          # grupos espalhados demais (corrente de vertices) ficam como estavam
    Q[bv[ok]]=cen[ok]
    return Q[w]
def costurar(v,i,tol,iters=8):
    P=v[:,:3].astype(np.float64)
    P0=P.copy()
    P=juntar_vertices(P,i,tol)
    for it in range(iters):
        w,n=soldar(P); Q=np.zeros((n,3)); Q[w]=P   # posicao por vertice soldado
        t=w[i]; E=bordas(t)
        if len(E)==0: break
        bv=np.unique(E)
        A=Q[E[:,0]]; B=Q[E[:,1]]; M=(A+B)/2; half=np.linalg.norm(B-A,axis=1)/2
        tr=cKDTree(M); rmax=half.max()+tol
        novo=Q.copy(); mov=0
        for x in bv:
            p=Q[x]; cand=tr.query_ball_point(p,rmax)
            if not cand: continue
            cand=np.array(cand); cand=cand[(E[cand,0]!=x)&(E[cand,1]!=x)]
            if len(cand)==0: continue
            a=A[cand]; ab=B[cand]-a; tt=np.clip(((p-a)*ab).sum(1)/np.maximum((ab*ab).sum(1),1e-20),0,1)
            pr=a+ab*tt[:,None]; d=np.linalg.norm(pr-p,axis=1); j=d.argmin()
            if 1e-9<d[j]<=tol: novo[x]=pr[j]; mov+=1
        P=novo[w]
        lon=np.linalg.norm(P-P0,axis=1)>1.5*tol; P[lon]=P0[lon]   # nunca mexe mais que 1,5x a tolerancia
        w2,_=soldar(P); E2=bordas(w2[i])
        print('  passada',it+1,'movidos',mov,'bordas abertas',len(E2))
    v=v.copy(); v[:,:3]=P.astype(np.float32); return v
if __name__=='__main__':
    ent,sai,tolrel=sys.argv[1],sys.argv[2],float(sys.argv[3])
    v,i,L=ler(ent)
    w,_=soldar(v[:,:3]); print(ent,'bordas abertas antes',len(bordas(w[i])))
    v=costurar(v,i,L*tolrel)
    gravar(sai,v,i,L)
