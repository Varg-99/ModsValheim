# Textura "pixel" estilo Valheim: a cor e uniforme dentro de cubinhos (voxels) do proprio modelo,
# entao aparecem quadradinhos do mesmo tamanho em toda a foice, sem depender de como o UV foi cortado.
import numpy as np, struct
from PIL import Image
from scipy import ndimage
def ler(f):
    d=open(f,'rb').read(); nv,ni,L=struct.unpack('<iif',d[4:16]); v=np.frombuffer(d,'<f4',nv*8,16).reshape(nv,8); i=np.frombuffer(d,'<u4',ni,16+nv*32).reshape(-1,3); return v,i,L
def amostras(malhas, T, dens=3.0, seed=0):
    rng=np.random.default_rng(seed); P=[];UV=[]
    for f in malhas:
        v,i,L=ler(f); a,b,c=v[i[:,0]],v[i[:,1]],v[i[:,2]]
        e1=b[:,6:8]-a[:,6:8]; e2=c[:,6:8]-a[:,6:8]; ua=np.abs(e1[:,0]*e2[:,1]-e1[:,1]*e2[:,0])/2*T*T
        k=np.clip(np.ceil(ua*dens).astype(int),2,4000)
        idx=np.repeat(np.arange(len(i)),k); r1=rng.random(len(idx)); r2=rng.random(len(idx)); s=np.sqrt(r1)
        w0=1-s; w1=s*(1-r2); w2=s*r2
        m=lambda cs: a[idx][:,cs]*w0[:,None]+b[idx][:,cs]*w1[:,None]+c[idx][:,cs]*w2[:,None]
        P.append(m(slice(0,3))); UV.append(m(slice(6,8)))
    return np.concatenate(P),np.concatenate(UV),L
def assar(malhas, pastaIn, pastaOut, passo, niveis=24, flipv=True):
    A=np.array(Image.open(pastaIn+'/foice_albedo.png').convert('RGB')).astype(np.float32)
    N=np.array(Image.open(pastaIn+'/foice_normal.png').convert('RGB')).astype(np.float32)
    M=np.array(Image.open(pastaIn+'/foice_metal.png').convert('RGBA')).astype(np.float32)
    T=A.shape[0]
    P,UV,L=amostras(malhas,T)
    tx=np.clip(((UV[:,0]%1)*T).astype(int),0,T-1); vv=UV[:,1]%1
    ty=np.clip((((1-vv) if flipv else vv)*T).astype(int),0,T-1)
    vox=np.floor(P/passo).astype(np.int64); vox-=vox.min(0)
    key=(vox[:,0]*100000+vox[:,1])*100000+vox[:,2]
    uk,inv=np.unique(key,return_inverse=True)
    def media(img):
        ch=img.shape[2]; s=np.zeros((len(uk),ch)); cnt=np.bincount(inv,minlength=len(uk))[:,None]
        for c in range(ch): s[:,c]=np.bincount(inv,weights=img[ty,tx,c],minlength=len(uk))
        return s/np.maximum(cnt,1)
    jit=1+np.random.default_rng(7).uniform(-0.05,0.05,len(uk))
    out=[]
    for img in (A,N,M):
        mv=media(img)
        if img is A: mv=mv*jit[:,None]   # leve variacao por cubinho, como pintura a mao
        o=img.copy(); o[ty,tx]=mv[inv]; out.append(o)
    Ao,No,Mo=out
    q=(niveis-1)/255.0; Ao=np.round(Ao*q)/q
    # normal: so um pouco do relevo original, sobre a media do cubinho (faces chapadas)
    No=No*0.85+np.array([128,128,255])*0.15
    Image.fromarray(np.clip(Ao,0,255).astype(np.uint8)).save(pastaOut+'/foice_albedo_pixel.png')
    Image.fromarray(np.clip(No,0,255).astype(np.uint8)).save(pastaOut+'/foice_normal_pixel.png')
    Image.fromarray(np.clip(Mo,0,255).astype(np.uint8),'RGBA').save(pastaOut+'/foice_metal_pixel.png')
    return len(uk), L
