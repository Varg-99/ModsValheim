# Converte os .raw novos (Blender, sem frestas) nas malhas finais do mod, repetindo o mesmo caminho de antes:
# canonico (raw2foic) -> afinar o cabo sem mexer na lamina -> endireitar o cabo no eixo Y.
# Flametal: separa as penas usando as malhas antigas como referencia. Obsidiana: alinha na malha antiga.
import sys, numpy as np, struct, json
sys.path.insert(0,'/home/claude/novas'); sys.path.insert(0,'/home/claude/costura')
from raw2foic import ler_raw
from afinar2 import transformador, eixo
from costurar import ler, gravar, soldar, bordas
from scipy.spatial import cKDTree
CANON=json.load(open('/home/claude/novas/canon.json'))
HEAD={'ferro':0.84,'Metal_negro':0.95,'flametal':0.86}
def canonico(raw,nome):
    v,idx=ler_raw(raw)
    b2g=lambda a: np.stack([a[:,0],a[:,2],-a[:,1]],1)
    P=b2g(v[:,0:3]); N=b2g(v[:,3:6]); UV=v[:,6:8]
    c=CANON[nome]; d=np.array(c['d']); butt=np.array(c['butt']); perp=np.array([-d[1],d[0]])*c['lado']
    ey=np.array([d[0],0,d[1]]); ez=np.array([perp[0],0,perp[1]]); ex=np.cross(ey,ez); R=np.stack([ex,ey,ez]); o=np.array([butt[0],0,butt[1]])
    Pc=(P-o)@R.T; Nc=N@R.T; Pc[:,1]-=Pc[:,1].min(); L=float(Pc[:,1].max())
    return np.concatenate([Pc,Nc,UV],1), idx.reshape(-1,3).astype(np.int64), L
def afinar(V,L,yh):
    aplica,info=transformador(V[:,:3],L,yh)
    Q,_=aplica(V[:,:3]); lam=V[:,2]>0.10; Q[lam]=V[lam,:3]; W=V.copy(); W[:,:3]=Q; return W,info
def endireitar(W,i,yH,extras=()):
    P=W[:,:3]; yc,cx,cz,r=eixo(P,yH); L=P[:,1].max()
    ok=(yc>0.06*L)&(yc<yH-0.08)
    ax=np.polyfit(yc[ok],cx[ok],1); az=np.polyfit(yc[ok],cz[ok],1)
    d=np.array([ax[0],1,az[0]]); d/=np.linalg.norm(d); o=np.array([ax[1],0,az[1]])
    y=np.array([0,1.,0]); v=np.cross(d,y); c=d@y; K=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]]); R=np.eye(3)+K+K@K/(1+c)
    def rig(V): V=V.copy(); V[:,:3]=(V[:,:3]-o)@R.T; V[:,3:6]=V[:,3:6]@R.T; return V
    W=rig(W); sh=W[:,1].min(); W[:,1]-=sh
    P=W[:,:3]; yc,cx,cz,r=eixo(P,yH); rmed=np.nanmedian(r[2:-2])
    def tira(V):
        Q=V[:,:3]; yy=Q[:,1]; x0=np.interp(yy,yc,cx); z0=np.interp(yy,yc,cz)
        rr=np.hypot(Q[:,0]-x0,Q[:,2]-z0); t=np.clip((yH-yy)/0.12,0,1)
        s=np.clip((rr-3*rmed)/(2*rmed),0,1); s=s*s*(3-2*s); k=t*(1-s); k[Q[:,2]>0.10]=0
        V=V.copy(); V[:,0]-=x0*k; V[:,2]-=z0*k; return V
    W=tira(W); return W, float(W[:,1].max())
def separar(V,i,velha_foice,velha_penas):
    a,_,_=ler(velha_foice); b,_,_=ler(velha_penas)
    ta=cKDTree(a[:,:3]); tb=cKDTree(b[:,:3])
    cen=V[i][:,:,:3].mean(1); da,_=ta.query(cen); db,_=tb.query(cen)
    pena=db<da
    def sub(mask):
        tri=i[mask]; usados=np.unique(tri); mapa=-np.ones(len(V),np.int64); mapa[usados]=np.arange(len(usados)); return V[usados],mapa[tri]
    return sub(~pena), sub(pena)
def icp(A,B,iters=40):
    # alinha A (pontos) em B: rotacao + translacao + escala
    ca=A.mean(0); cb=B.mean(0); sa=np.sqrt(((A-ca)**2).sum(1).mean()); sb=np.sqrt(((B-cb)**2).sum(1).mean())
    ua=np.linalg.svd(A-ca,full_matrices=False)[2]; ub=np.linalg.svd(B-cb,full_matrices=False)[2]
    tb=cKDTree(B); melhor=None
    for sx in [1,-1]:
        for sy in [1,-1]:
            D=np.diag([sx,sy,sx*sy*np.linalg.det(ua)*np.linalg.det(ub)])
            R=ub.T@D@ua; s=sb/sa; t=cb-s*ca@R.T
            for k in range(iters):
                X=s*A@R.T+t; d,j=tb.query(X); Y=B[j]
                mx=A.mean(0); my=Y.mean(0); H=(A-mx).T@(Y-my); U,S,Vt=np.linalg.svd(H); Dd=np.eye(3); Dd[2,2]=np.sign(np.linalg.det(Vt.T@U.T))
                R=Vt.T@Dd@U.T; s=(S*np.diag(Dd)).sum()/((A-mx)**2).sum(); t=my-s*mx@R.T
            err=np.median(tb.query(s*A@R.T+t)[0])
            if melhor is None or err<melhor[0]: melhor=(err,R,s,t)
    return melhor

def icp_rigido(A,B,R=None,s=1.0,t=None,iters=50,escala=False,trim=0.9):
    tb=cKDTree(B); R=np.eye(3) if R is None else R; t=np.zeros(3) if t is None else t
    for k in range(iters):
        X=s*A@R.T+t; d,j=tb.query(X); m=d<=np.quantile(d,trim); a=A[m]; Y=B[j[m]]
        mx=a.mean(0); my=Y.mean(0); H=(a-mx).T@(Y-my); U,S,Vt=np.linalg.svd(H); Dd=np.eye(3); Dd[2,2]=np.sign(np.linalg.det(Vt.T@U.T))
        R=Vt.T@Dd@U.T
        if escala: s=(S*np.diag(Dd)).sum()/((a-mx)**2).sum()
        t=my-s*mx@R.T
    d,_=tb.query(s*A@R.T+t); return R,s,t,np.median(d)
def alinhar_eixos(A,B):
    import itertools
    ea=A.max(0)-A.min(0); eb=B.max(0)-B.min(0); melhor=None
    for perm in itertools.permutations(range(3)):
        for sg in itertools.product([1,-1],repeat=3):
            M=np.zeros((3,3))
            for r,(c,sv) in enumerate(zip(perm,sg)): M[r,c]=sv
            if np.linalg.det(M)<0: continue
            Ar=A@M.T; s=(B.max(0)-B.min(0))[1]/(Ar.max(0)-Ar.min(0))[1]
            t=(B.max(0)+B.min(0))/2-s*(Ar.max(0)+Ar.min(0))/2
            R,s2,t2,err=icp_rigido(A,B,M,s,t,iters=25,escala=True)
            if melhor is None or err<melhor[3]: melhor=(R,s2,t2,err)
    return melhor

def transferir_uv(V,i,O,oi):
    # Cada triangulo novo pega o UV do triangulo antigo mais proximo (projetando os 3 cantos nele).
    # Os cantos ficam com UV proprio por triangulo; depois junta os que tem mesma posicao e mesmo UV.
    OP=O[:,:3]; ta=OP[oi[:,0]]; tb=OP[oi[:,1]]; tc=OP[oi[:,2]]
    ocen=(ta+tb+tc)/3; arv=cKDTree(ocen)
    P=V[:,:3]; cen=P[i].mean(1)
    _,cand=arv.query(cen,k=8)
    # escolhe, entre 8 candidatos, o triangulo antigo cujo plano/area fica mais perto do centro novo
    def dist_tri(p,a,b,c):
        ab=b-a; ac=c-a; n=np.cross(ab,ac); nn=np.linalg.norm(n,axis=-1,keepdims=True)+1e-20; n=n/nn
        q=p-((p-a)*n).sum(-1,keepdims=True)*n
        v0=ab; v1=ac; v2=q-a
        d00=(v0*v0).sum(-1); d01=(v0*v1).sum(-1); d11=(v1*v1).sum(-1); d20=(v2*v0).sum(-1); d21=(v2*v1).sum(-1)
        den=d00*d11-d01*d01+1e-30; bv=(d11*d20-d01*d21)/den; bw=(d00*d21-d01*d20)/den; bu=1-bv-bw
        fora=np.maximum(0,-np.minimum(np.minimum(bu,bv),bw))
        return np.abs(((p-a)*n).sum(-1))+fora*np.sqrt(nn[...,0])
    _,cand=arv.query(cen,k=24)
    pc=cen[:,None,:]; da=dist_tri(pc,ta[cand],tb[cand],tc[cand])
    # mesmo lado da peca: a normal do triangulo antigo tem que apontar para o mesmo lado (lamina fina tem frente e verso a milimetros)
    nn=np.cross(P[i[:,1]]-P[i[:,0]],P[i[:,2]]-P[i[:,0]]); nn/=np.linalg.norm(nn,axis=1,keepdims=True)+1e-20
    on=np.cross(tb-ta,tc-ta); on/=np.linalg.norm(on,axis=1,keepdims=True)+1e-20
    dot=(on[cand]*nn[:,None,:]).sum(-1)
    da=da+np.where(dot<0.3,1.0,0.0)+(1-dot)*0.002
    k=cand[np.arange(len(i)),da.argmin(1)]
    a=ta[k];b=tb[k];c=tc[k]; ua=O[oi[k,0],6:8]; ub=O[oi[k,1],6:8]; uc=O[oi[k,2],6:8]
    novosV=[]; 
    for col in range(3):
        p=P[i[:,col]]; ab=b-a; ac=c-a; v2=p-a
        d00=(ab*ab).sum(1); d01=(ab*ac).sum(1); d11=(ac*ac).sum(1); d20=(v2*ab).sum(1); d21=(v2*ac).sum(1)
        den=d00*d11-d01*d01+1e-30; bv=(d11*d20-d01*d21)/den; bw=(d00*d21-d01*d20)/den; bu=1-bv-bw
        uv=ua*bu[:,None]+ub*bv[:,None]+uc*bw[:,None]
        row=V[i[:,col]].copy(); row[:,6:8]=uv; novosV.append(row)
    C=np.stack(novosV,1).reshape(-1,8)
    chave=np.concatenate([np.round(C[:,:3]/1e-6),np.round(C[:,3:6]*100),np.round(C[:,6:8]/2e-5)],1).astype(np.int64)
    _,primeiro,inv=np.unique(chave,axis=0,return_index=True,return_inverse=True)
    return C[primeiro], inv.reshape(-1,3)
