import sys; sys.path.insert(0,'/home/claude/novas')
from afinar import ler,gravar; from afinar2 import *
from scipy.spatial import cKDTree
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
U='/mnt/user-data/uploads/Documents/ModsValheim/CeifadoraObsidiana/'
HEAD={'ferro':0.84,'metalnegro':0.95,'flametal':0.86}
def restaurar(vn,vo):
    feat=lambda v: np.concatenate([v[:,6:8]*100, v[:,3:6]*1, v[:,:3]*5],1)
    d,j=cKDTree(feat(vo)).query(feat(vn)); out=vn.copy(); out[:,:3]=vo[j,:3]; out[:,3:6]=vo[j,3:6]; return out
fig,ax=plt.subplots(1,6,figsize=(30,11))
for k,n in enumerate(['ferro','metalnegro','flametal']):
    vo,_,Lo=ler('orig/%s.malha'%n); vn,i,L=ler(U+'modelo_%s/foice.malha'%n)
    if len(vo)==len(vn): base=vo.copy(); base[:,6:]=vn[:,6:]
    else: base=restaurar(vn,vo)
    aplica,info=transformador(vo[:,:3],L,HEAD[n]); print(n,info)
    Q,fe=aplica(base[:,:3]); lam0=base[:,2]>0.10; Q[lam0]=base[lam0,:3]; w=base.copy(); w[:,:3]=Q; gravar('saida/%s.malha'%n,w,i,L)
    lam=np.linalg.norm(Q-base[:,:3],axis=1); print('  vertices com z>0.12 movidos:',int(((base[:,2]>0.12)&(lam>1e-5)).sum()))
    a=ax[2*k]; a.scatter(base[:,2],base[:,1],s=0.05,c='k'); a.scatter(Q[:,2],Q[:,1],s=0.05,c='r'); a.set_aspect('equal'); a.set_title(n+' preto=orig vermelho=novo')
    a=ax[2*k+1]; a.scatter(Q[:,0],Q[:,1],s=0.05,c='r'); a.scatter(base[:,0],base[:,1],s=0.02,c='k',alpha=0.2); a.set_aspect('equal'); a.set_xlim(-0.1,0.1)
    if n=='flametal':
        vp,ip,Lp=ler(U+'modelo_flametal/penas.malha'); bp=restaurar(vp,vo); Qp,_=aplica(bp[:,:3]); bp[:,:3]=Qp; gravar('saida/penas.malha',bp,ip,Lp)
        ax[2*k].scatter(Qp[:,2],Qp[:,1],s=0.05,c='b')
plt.savefig('cmp2.png',dpi=60)

# ---- endireitar: eixo do cabo vira exatamente o eixo Y (onde o mod poe as maos) ----
def endireitar(n, extras=()):
    w,i,L=ler('saida/%s.malha'%n); P=w[:,:3]
    yH=HEAD[n]; yc,cx,cz,r=eixo(P,yH)
    ok=(yc>0.06*L)&(yc<yH-0.08)
    ax=np.polyfit(yc[ok],cx[ok],1); az=np.polyfit(yc[ok],cz[ok],1)
    d=np.array([ax[0],1,az[0]]); d/=np.linalg.norm(d); o=np.array([ax[1],0,az[1]])
    # rotacao que leva d para +Y
    y=np.array([0,1.,0]); v=np.cross(d,y); c=d@y; K=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
    R=np.eye(3)+K+K@K/(1+c)
    def rig(V):
        V=V.copy(); V[:,:3]=(V[:,:3]-o)@R.T; V[:,3:6]=V[:,3:6]@R.T; return V
    w=rig(w); sh=w[:,1].min(); w[:,1]-=sh
    # residuo (cabo torto): desloca so a parte do cabo, longe da lamina e da cabeca
    P=w[:,:3]; yc,cx,cz,r=eixo(P,yH); rmed=np.nanmedian(r[2:-2])
    def tira(V):
        Q=V[:,:3]; yy=Q[:,1]; x0=np.interp(yy,yc,cx); z0=np.interp(yy,yc,cz)
        rr=np.hypot(Q[:,0]-x0,Q[:,2]-z0); t=np.clip((yH-yy)/0.12,0,1)
        s=np.clip((rr-3*rmed)/(2*rmed),0,1); s=s*s*(3-2*s); k=t*(1-s); k[Q[:,2]>0.10]=0
        V=V.copy(); V[:,0]-=x0*k; V[:,2]-=z0*k; return V
    w=tira(w); L2=float(w[:,1].max()); gravar('saida/%s.malha'%n,w,i,L2)
    yc2,cx2,cz2,_=eixo(w[:,:3],yH)
    print(n,'inclinacao corrigida (graus)',round(float(np.degrees(np.arccos(c))),2),'deslocamento',o.round(4),'residuo max cm',round(float(np.max(np.hypot(cx2,cz2)[yc2<yH-0.1]))*100,2),'L',round(L2,3))
    for e in extras:
        ve,ie,Le=ler(e); ve=rig(ve); ve[:,1]-=sh; ve=tira(ve); gravar(e,ve,ie,L2)
for n in ['ferro','metalnegro','flametal']: endireitar(n, ['saida/penas.malha'] if n=='flametal' else ())
