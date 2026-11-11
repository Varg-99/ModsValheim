# Le o BVH (FK de verdade) e desenha quadros: vista de lado e de cima, com a foice.
import numpy as np, sys, re, math
from scipy.spatial.transform import Rotation as Rot
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt

def ler_bvh(arq):
    txt = open(arq).read().split("MOTION")
    hier, mot = txt[0], txt[1]
    toks = hier.split()
    nomes, pais, offs, chans = [], [], [], []
    pilha = []; i = 0; atual = None
    while i < len(toks):
        t = toks[i]
        if t in ("ROOT", "JOINT"):
            nomes.append(toks[i+1]); pais.append(pilha[-1] if pilha else -1); atual = len(nomes)-1; i += 2
        elif t == "End":
            nomes.append(None); pais.append(pilha[-1]); atual = len(nomes)-1; i += 2
        elif t == "{": pilha.append(atual); i += 1
        elif t == "}": pilha.pop(); i += 1
        elif t == "OFFSET": offs.append(np.array(list(map(float, toks[i+1:i+4])))); i += 4
        elif t == "CHANNELS":
            n = int(toks[i+1]); chans.append((len(nomes)-1, toks[i+2:i+2+n])); i += 2+n
        else: i += 1
    ch = {j: c for j, c in chans}
    linhas = [l for l in mot.strip().split("\n")[2:]]
    dados = np.array([list(map(float, l.split())) for l in linhas])
    return nomes, pais, offs, ch, dados

def fk(nomes, pais, offs, ch, linha):
    P = [None]*len(nomes); R = [None]*len(nomes); k = 0
    for j in range(len(nomes)):
        c = ch.get(j, [])
        pos = offs[j].copy(); rl = np.eye(3)
        if c:
            v = linha[k:k+len(c)]; k += len(c)
            if "Xposition" in c: pos = v[:3]; ang = v[3:]
            else: ang = v
            rl = Rot.from_euler('ZXY', ang, degrees=True).as_matrix()
        if pais[j] < 0: P[j] = pos; R[j] = rl
        else:
            P[j] = P[pais[j]] + R[pais[j]] @ pos; R[j] = R[pais[j]] @ rl
    return P

if __name__ == "__main__":
    pasta = sys.argv[1]
    nomes, pais, offs, ch, dados = ler_bvh(pasta + "/Ceifadora_Golpe3.bvh")
    foice = np.load(pasta + "/foice.npy")
    sel = list(range(0, len(dados), 3))
    cols = 6; rows = math.ceil(len(sel)/cols)
    for vista, eixos, nome in (("lado", (0, 2, 1), "lado"), ("cima", (0, 2, None), "cima"), ("frente", (0, 0, 1), "frente")):
        fig, axs = plt.subplots(rows, cols, figsize=(cols*2.6, rows*2.8))
        for a, f in zip(axs.flat, sel):
            P = fk(nomes, pais, offs, ch, dados[f])
            def proj(p):
                if nome == "lado": return (p[2], p[1])        # z para a direita, y para cima
                if nome == "frente": return (-p[0], p[1])     # vista de frente (esq do personagem a direita)
                return (-p[0], p[2])                            # de cima: x (direita do personagem) e z
            for j in range(len(nomes)):
                if pais[j] >= 0:
                    a.plot(*zip(proj(P[pais[j]]), proj(P[j])), color="k", lw=1.6)
            fb, ft, L = foice[f][:3], foice[f][3:6], foice[f][6:]
            a.plot(*zip(proj(fb), proj(ft)), color="saddlebrown", lw=2)
            a.plot(*zip(proj(ft), proj(ft + L*0.6 - (ft-fb)/np.linalg.norm(ft-fb)*0.25)), color="red", lw=2.5)
            a.set_title(f"{f/30:.2f}s", fontsize=8); a.set_aspect("equal")
            if nome == "cima": a.set_xlim(-1.8, 1.8); a.set_ylim(-1.5, 3.5)
            else: a.set_xlim(-1.6 if nome=="frente" else -1.2, 1.6 if nome=="frente" else 3.6); a.set_ylim(-0.1, 2.6)
            a.axhline(0, color="g", lw=0.5) if nome != "cima" else None
            a.tick_params(labelsize=5)
        for a in axs.flat[len(sel):]: a.axis("off")
        plt.tight_layout(); plt.savefig(f"{pasta}/prev_{nome}.png", dpi=70); plt.close()
    print("ok")
