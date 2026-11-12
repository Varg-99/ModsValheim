import numpy as np, struct, sys
from PIL import Image
from scipy import ndimage
def ler(f):
    d=open(f,'rb').read(); nv,ni,L=struct.unpack('<iif',d[4:16]); v=np.frombuffer(d,'<f4',nv*8,16).reshape(nv,8); i=np.frombuffer(d,'<u4',ni,16+nv*32).reshape(-1,3); return v,i,L
def render(malhas, alb, met, saida, flipv=True, ang=-42, R=512, OUT=128, lado=1, corBorda=(1,1,1)):
    A=np.array(Image.open(alb).convert('RGB')).astype(np.float32)/255
    M=np.array(Image.open(met).convert('RGBA')).astype(np.float32)/255
    th,tw=A.shape[:2]
    P=[];N=[];UV=[]
    rng=np.random.default_rng(0)
    for f in malhas:
        v,i,L=ler(f)
        a,b,c=v[i[:,0]],v[i[:,1]],v[i[:,2]]
        # area-proportional samples
        area=np.linalg.norm(np.cross(b[:,:3]-a[:,:3],c[:,:3]-a[:,:3]),axis=1)/2
        k=np.clip((area/ area.mean()*6).astype(int),3,60)
        idx=np.repeat(np.arange(len(i)),k)
        r1=rng.random(len(idx)); r2=rng.random(len(idx)); s=np.sqrt(r1)
        w0=1-s; w1=s*(1-r2); w2=s*r2
        def mix(col): return a[idx][:,col]*w0[:,None]+b[idx][:,col]*w1[:,None]+c[idx][:,col]*w2[:,None]
        P.append(mix(slice(0,3))); N.append(mix(slice(3,6))); UV.append(mix(slice(6,8)))
    P=np.concatenate(P); N=np.concatenate(N); UV=np.concatenate(UV)
    N/=np.linalg.norm(N,axis=1,keepdims=True)+1e-9
    # vista de lado: olhando de +X (lado=1) -> tela: (Z, Y); profundidade = X
    x2=P[:,2]*lado; y2=P[:,1]; dep=P[:,0]*lado
    t=np.radians(ang); cx=np.cos(t); sx=np.sin(t)
    u= x2*cx - y2*sx; w= x2*sx + y2*cx
    mn=np.array([u.min(),w.min()]); mx=np.array([u.max(),w.max()]); size=(mx-mn).max()*1.08; c0=(mn+mx)/2
    px=((u-c0[0])/size+0.5)*R; py=(0.5-(w-c0[1])/size)*R
    ix=np.clip(px.astype(int),0,R-1); iy=np.clip(py.astype(int),0,R-1)
    # z-buffer: mais perto do observador = maior dep
    ordem=np.argsort(dep)
    img=np.zeros((R,R,3),np.float32); alpha=np.zeros((R,R),bool)
    uu=UV[:,0]%1; vv=UV[:,1]%1
    if flipv: vv=1-vv
    tx=np.clip((uu*tw).astype(int),0,tw-1); ty=np.clip((vv*th).astype(int),0,th-1)
    col=A[ty,tx]; met_=M[ty,tx,0]; rug=M[ty,tx,3]
    nv=N.copy(); nv[:,0]*=lado; nv[:,2]*=lado
    # luz vinda de cima-esquerda-frente (em coordenadas de tela)
    nl= np.stack([nv[:,2]*cx-nv[:,1]*sx, nv[:,2]*sx+nv[:,1]*cx, nv[:,0]],1)  # (u, w, frente)
    L=np.array([-0.45,0.6,0.66]); L/=np.linalg.norm(L)
    d=np.abs(nl@L)
    # sombreamento em faixas (efeito 2D)
    faixa=np.where(d>0.75,1.0,np.where(d>0.4,0.82,0.62))
    brilho=met_*np.clip(rug,0.3,1)*(d>0.85)*0.35
    cor=col*faixa[:,None]*(1+met_[:,None]*0.6)+brilho[:,None]
    img[iy[ordem],ix[ordem]]=cor[ordem]; alpha[iy[ordem],ix[ordem]]=True
    # fecha buracos
    filled=ndimage.binary_closing(alpha,iterations=3); filled=ndimage.binary_fill_holes(filled)
    hole=filled&~alpha
    if hole.any():
        idxs=ndimage.distance_transform_edt(~alpha,return_distances=False,return_indices=True)
        img[hole]=img[idxs[0][hole],idxs[1][hole]]
    alpha=filled
    # contorno escuro grosso + linha clara por dentro (estilo 2D)
    esc=R//128
    d1=ndimage.binary_dilation(alpha,iterations=2*esc)
    borda=d1&~alpha
    fora=ndimage.binary_dilation(d1,iterations=4*esc)&~d1
    claro=alpha&~ndimage.binary_erosion(alpha,iterations=1*esc)
    out=np.zeros((R,R,4),np.float32)
    out[...,:3]=np.clip(img,0,1); out[...,3]=alpha
    out[claro,:3]=np.clip(out[claro,:3]*1.35+0.08,0,1)
    out[borda]=[0.04,0.03,0.03,1]
    out[fora]=[corBorda[0],corBorda[1],corBorda[2],1]
    im=Image.fromarray((out*255).astype(np.uint8),'RGBA').resize((OUT,OUT),Image.LANCZOS)
    im.save(saida)
if __name__=='__main__':
    pass
