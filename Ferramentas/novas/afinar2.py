import numpy as np, sys
sys.path.insert(0,'/home/claude/novas'); from afinar import ler, gravar
ALVO=0.0150
def eixo(P,yHead,nb=40):
    bins=np.linspace(0,yHead,nb); yc=(bins[:-1]+bins[1:])/2
    cx=np.zeros(nb-1); cz=np.zeros(nb-1)
    for k,(a,b) in enumerate(zip(bins[:-1],bins[1:])):
        m=(P[:,1]>=a)&(P[:,1]<b)
        if m.sum()>10: cx[k]=np.median(P[m,0]); cz[k]=np.median(P[m,2])
        elif k: cx[k]=cx[k-1]; cz[k]=cz[k-1]
    for it in range(4):  # robusto: ignora pontos longe do eixo (lamina pendurada)
        r=[]
        for k,(a,b) in enumerate(zip(bins[:-1],bins[1:])):
            m=(P[:,1]>=a)&(P[:,1]<b); d=np.hypot(P[m,0]-cx[k],P[m,2]-cz[k])
            lim=max(0.03,2.0*np.median(d)) if it==0 else 0.06
            q=m.copy(); q[m]=d<lim
            if q.sum()>10: cx[k]=np.median(P[q,0]); cz[k]=np.median(P[q,2])
            r.append(np.median(np.hypot(P[q,0]-cx[k],P[q,2]-cz[k])) if q.sum()>10 else np.nan)
        cx2=np.convolve(np.pad(cx,1,'edge'),np.ones(3)/3,'valid'); cz2=np.convolve(np.pad(cz,1,'edge'),np.ones(3)/3,'valid'); cx,cz=cx2,cz2
    return yc,cx,cz,np.array(r)
def cabeca(P,L):
    for y in np.linspace(0.3*L,L,200):
        m=(P[:,1]>y-0.005)&(P[:,1]<y+0.005)
        if m.sum()>5 and P[m,2].max()-np.median(P[m,2])>0.14: return y-0.03
    return L
def transformador(P,L,yHead=None):
    yHead=yHead or cabeca(P,L)
    yc,cx,cz,r=eixo(P,yHead); rmed=float(np.nanmedian(r[2:-2]))
    f=min(1.0,ALVO*L/(rmed*1.25))
    r1,r2=2.2*rmed,3.2*rmed
    def aplica(Q):
        y=Q[:,1]; x0=np.interp(y,yc,cx); z0=np.interp(y,yc,cz)
        dx=Q[:,0]-x0; dz=Q[:,2]-z0; rr=np.hypot(dx,dz)
        t=np.clip((yHead-y)/0.08,0,1)                     # some perto da cabeca
        s=np.clip((rr-r1)/(r2-r1),0,1); s=s*s*(3-2*s)       # longe do eixo = lamina: intocada
        fe=1+(f-1)*t*(1-s)
        Q=Q.copy(); Q[:,0]=x0+dx*fe; Q[:,2]=z0+dz*fe; return Q, fe
    return aplica,dict(yHead=round(yHead,3),rmed=round(rmed,4),f=round(f,2))
