import sys; sys.path.insert(0,'/home/claude/v2'); from processar_v2 import *
BK='/mnt/user-data/outputs/bak_malha/'; D='/mnt/user-data/uploads/Documents/ModsValheim/Ferramentas/novas/v2/'
rng=np.random.default_rng(2)
def pronto(n,pasta,penas=False):
    if pasta=='modelo': V,i,L=ler('out/modelo_foice.malha')
    else: V,i,L=canonico(D+n+'.raw',n); V,_=afinar(V,L,HEAD[n]); V,L=endireitar(V,i,HEAD[n])
    O,oi,Lo=ler(BK+pasta+'.malha')
    if penas: Pn,pi,_=ler(BK+'penas.malha'); oi=np.concatenate([oi,pi+len(O)]); O=np.concatenate([O,Pn])
    R,s,t,err=icp_rigido(V[rng.choice(len(V),20000,False),:3],O[:,:3],iters=40)
    V=V.copy(); V[:,:3]=V[:,:3]@R.T+t; V[:,3:6]=V[:,3:6]@R.T
    V2,i2=transferir_uv(V,i,O,oi); print(pasta,'vertices',len(V2),'tris',len(i2)); return V2,i2,Lo
for n,pasta in [('ferro','modelo_ferro'),('Metal_negro','modelo_metalnegro'),('obsidiana','modelo')]:
    V,i,L=pronto(n,pasta); gravar('out/final_%s.malha'%pasta,V,i,L)
V,i,L=pronto('flametal','modelo_flametal',True)
(fv,fi),(pv,pi)=separar(V,i,BK+'modelo_flametal.malha',BK+'penas.malha')
gravar('out/final_modelo_flametal.malha',fv,fi,L); gravar('out/final_penas.malha',pv,pi,L)
