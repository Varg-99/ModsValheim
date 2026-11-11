# Golpe 3 da Ceifadora: animacao feita do zero (esqueleto humanoide + foice).
# Saidas: golpe3.bvh (esqueleto), golpe3_trilha.txt (foice por quadro, espaco Unity), previews.
# Frame do autor: destro, Y para cima, personagem olhando +Z, esquerda do personagem = +X.
import numpy as np, math, json, sys, os
from scipy.spatial.transform import Rotation as Rot

FPS = 30
DUR = 1.6
N = int(round(DUR * FPS)) + 1
HIT = 0.85
VAO = 0.32          # distancia entre as maos na haste
CABO = 0.30         # cabo atras da mao de baixo
COMP = 2.0          # comprimento da foice

# ---------------- esqueleto (posicao de descanso, T-pose) ----------------
HIPS_Y = 0.97
B = {}  # nome -> (pai, offset)
def osso(n, pai, off): B[n] = (pai, np.array(off, float))
osso("Hips", None, (0, HIPS_Y, 0))
osso("Spine", "Hips", (0, 0.10, 0))
osso("Chest", "Spine", (0, 0.14, 0))
osso("UpperChest", "Chest", (0, 0.14, 0))
osso("Neck", "UpperChest", (0, 0.14, 0))
osso("Head", "Neck", (0, 0.09, 0))
for s, sx in (("Left", 1), ("Right", -1)):
    osso(s + "Shoulder", "UpperChest", (sx * 0.05, 0.10, 0))
    osso(s + "UpperArm", s + "Shoulder", (sx * 0.13, 0, 0))
    osso(s + "LowerArm", s + "UpperArm", (sx * 0.28, 0, 0))
    osso(s + "Hand", s + "LowerArm", (sx * 0.26, 0, 0))
    osso(s + "UpperLeg", "Hips", (sx * 0.10, -0.05, 0))
    osso(s + "LowerLeg", s + "UpperLeg", (0, -0.43, 0))
    osso(s + "Foot", s + "LowerLeg", (0, -0.42, 0))
    osso(s + "Toes", s + "Foot", (0, -0.06, 0.13))
FIM = {"Head": (0, 0.18, 0), "LeftHand": (0.09, 0, 0), "RightHand": (-0.09, 0, 0),
       "LeftToes": (0, 0, 0.06), "RightToes": (0, 0, 0.06)}
ORDEM = list(B.keys())
FILHOS = {n: [c for c in ORDEM if B[c][0] == n] for n in ORDEM}
L_COXA, L_CANELA = 0.43, 0.42
L_BRACO, L_ANTE = 0.28, 0.26
ANKLE_Y = HIPS_Y - 0.05 - L_COXA - L_CANELA   # 0.07

# ---------------- utilitarios ----------------
def nrm(v):
    n = np.linalg.norm(v); return v / n if n > 1e-9 else v
def Rx(a): return Rot.from_euler('x', a, degrees=True).as_matrix()
def Ry(a): return Rot.from_euler('y', a, degrees=True).as_matrix()
def Rz(a): return Rot.from_euler('z', a, degrees=True).as_matrix()
def yaw_dir(psi):  # psi>0 = girado para a direita do personagem (horario visto de cima)
    return Ry(-psi)
def aim(rest_dir, rest_up, dir_, up_):
    """rotacao que leva rest_dir->dir_ e (rest_up projetado)->(up_ projetado)."""
    a0 = nrm(np.array(rest_dir, float)); b0 = nrm(np.array(dir_, float))
    a1 = nrm(np.array(rest_up, float) - a0 * np.dot(rest_up, a0))
    b1 = np.array(up_, float) - b0 * np.dot(up_, b0)
    if np.linalg.norm(b1) < 1e-6:
        b1 = np.cross(b0, [1, 0, 0]) if abs(b0[0]) < 0.9 else np.cross(b0, [0, 0, 1])
    b1 = nrm(b1)
    A = np.stack([a0, a1, np.cross(a0, a1)], 1); Bm = np.stack([b0, b1, np.cross(b0, b1)], 1)
    return Bm @ A.T

def cr(keys, t, ang=False):
    """Catmull-Rom em chaves [(t, valor)] (valor escalar ou vetor)."""
    ts = [k[0] for k in keys]; vs = [np.array(k[1], float) for k in keys]
    if t <= ts[0]: return vs[0]
    if t >= ts[-1]: return vs[-1]
    i = max(j for j in range(len(ts) - 1) if ts[j] <= t)
    u = (t - ts[i]) / (ts[i + 1] - ts[i])
    p0 = vs[max(0, i - 1)]; p1 = vs[i]; p2 = vs[i + 1]; p3 = vs[min(len(vs) - 1, i + 2)]
    # tangentes com tempos desiguais (cardinal suavizada)
    t0 = ts[max(0, i - 1)]; t1 = ts[i]; t2 = ts[i + 1]; t3 = ts[min(len(ts) - 1, i + 2)]
    m1 = (p2 - p0) / max(t2 - t0, 1e-6) * (t2 - t1)
    m2 = (p3 - p1) / max(t3 - t1, 1e-6) * (t2 - t1)
    if i == 0: m1 = (p2 - p1)
    if i + 1 == len(vs) - 1: m2 = (p2 - p1)
    u2, u3 = u * u, u * u * u
    return (2*u3 - 3*u2 + 1) * p1 + (u3 - 2*u2 + u) * m1 + (-2*u3 + 3*u2) * p2 + (u3 - u2) * m2

def ik2(root, alvo, l1, l2, polo):
    """posicao do joelho/cotovelo."""
    d = alvo - root; dist = np.linalg.norm(d)
    dist_c = min(max(dist, 1e-3), l1 + l2 - 1e-4)
    dn = d / max(dist, 1e-6)
    a = (l1 * l1 - l2 * l2 + dist_c * dist_c) / (2 * dist_c)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    p = polo - dn * np.dot(polo, dn)
    p = nrm(p) if np.linalg.norm(p) > 1e-6 else nrm(np.cross(dn, [1, 0, 0]))
    meio = root + dn * a + p * h
    fim = root + dn * dist_c
    return meio, fim

# ---------------- roteiro do golpe ----------------
# psi: giro do quadril (graus, + = para a direita do personagem, sentido do fim do 2o golpe)
K_PSI = [(0.00, 20), (0.15, 50), (0.45, 200), (0.70, 330), (0.85, 360), (1.60, 360)]
K_TWIST = [(0.00, 25), (0.15, 45), (0.45, 15), (0.70, -10), (0.85, 0), (1.05, 0), (1.60, 5)]
K_LEAN = [(0.00, 10), (0.15, 15), (0.45, 5), (0.70, -12), (0.85, 42), (1.05, 38), (1.35, 20), (1.60, 10)]
K_HIPS = [(0.00, (0, -0.06, 0.0)), (0.15, (0, -0.10, 0.10)), (0.45, (0, -0.05, 0.55)),
          (0.70, (0, -0.02, 0.85)), (0.85, (0, -0.27, 1.90)), (1.05, (0, -0.25, 1.97)),
          (1.35, (0, -0.12, 1.98)), (1.60, (0, -0.07, 1.98))]
# foice no espaco do corpo (x=esq, y=cima, z=frente, relativo ao quadril girado por psi+twist):
# G = mao de baixo (esquerda), H = direcao da haste (cabo -> lamina)
K_G = [(0.00, (-0.25, 0.30, 0.25)), (0.15, (-0.28, 0.25, 0.18)), (0.45, (-0.32, 0.55, 0.30)),
       (0.58, (-0.18, 0.75, 0.28)), (0.70, (0.00, 0.85, 0.12)), (0.85, (0.00, 0.35, 0.45)),
       (1.05, (0.00, 0.20, 0.35)), (1.35, (-0.08, 0.30, 0.30)), (1.60, (-0.15, 0.35, 0.25))]
K_H = [(0.00, (-0.6, -0.3, -0.5)), (0.15, (-0.5, -0.45, -0.75)), (0.45, (-0.85, 0.10, 0.50)),
       (0.58, (-0.5, 0.6, 0.6)), (0.70, (-0.1, 0.9, -0.4)), (0.85, (0.0, -0.45, 0.89)),
       (1.05, (0.0, -0.35, 0.94)), (1.35, (-0.3, 0.10, 0.95)), (1.60, (-0.4, 0.5, 0.75))]
# pes (mundo): (t, (x, z), levantado)
K_PE = {
    "Left": [(0.00, (0.15, 0.10)), (0.50, (0.15, 0.10)), (0.62, (0.22, 0.75)), (0.68, (0.22, 0.75)),
             (0.84, (0.22, 1.32)), (1.60, (0.22, 1.32))],
    "Right": [(0.00, (-0.15, -0.10)), (0.30, (-0.15, -0.10)), (0.40, (-0.05, 0.55)), (0.66, (-0.05, 0.55)),
              (0.84, (-0.15, 2.45)), (1.60, (-0.15, 2.45))],
}
K_PE_YAW = {"Left": [(0.0, 20), (0.5, 20), (0.62, 200), (0.68, 250), (0.84, 360), (1.6, 360)],
            "Right": [(0.0, 20), (0.3, 20), (0.40, 140), (0.66, 300), (0.84, 360), (1.6, 360)]}

def pe(lado, t):
    ks = K_PE[lado]
    for i in range(len(ks) - 1):
        t0, a = ks[i]; t1, b = ks[i + 1]
        if t0 <= t <= t1:
            u = (t - t0) / (t1 - t0) if t1 > t0 else 0
            a = np.array(a); b = np.array(b)
            if np.linalg.norm(b - a) < 1e-6: return np.array([a[0], ANKLE_Y, a[1]]), 0
            s = u * u * (3 - 2 * u)
            xz = a + (b - a) * s
            alt = math.sin(math.pi * u) * (0.18 if np.linalg.norm(b - a) > 0.8 else 0.12)
            return np.array([xz[0], ANKLE_Y + alt, xz[1]]), alt
    a = ks[-1][1]; return np.array([a[0], ANKLE_Y, a[1]]), 0
def pe_yaw(lado, t):
    ks = K_PE_YAW[lado]
    return float(np.interp(t, [k[0] for k in ks], [k[1] for k in ks]))

def foice(t):
    psi = float(cr(K_PSI, t)); tw = float(cr(K_TWIST, t))
    hp = np.array([0, HIPS_Y, 0]) + cr(K_HIPS, t)
    Rb = yaw_dir(psi + tw * 0.6)
    G = hp + Rb @ cr(K_G, t)
    H = nrm(Rb @ nrm(cr(K_H, t)))
    return G, H

def pose(t):
    W = {}  # nome -> (pos, R)
    psi = float(cr(K_PSI, t)); tw = float(cr(K_TWIST, t)); lean = float(cr(K_LEAN, t))
    hp = np.array([0, HIPS_Y, 0]) + cr(K_HIPS, t)
    Rh = yaw_dir(psi) @ Rx(lean * 0.25)
    W["Hips"] = (hp, Rh)
    R = Rh
    for n in ("Spine", "Chest", "UpperChest"):
        p = W[B[n][0]][0] + W[B[n][0]][1] @ B[n][1]
        R = R @ Ry(-tw / 3) @ Rx(lean * 0.75 / 3)
        W[n] = (p, R)
    p = W["UpperChest"][0] + W["UpperChest"][1] @ B["Neck"][1]
    Rn = W["UpperChest"][1] @ Rx(-lean * 0.3) @ Ry(tw * 0.3)
    W["Neck"] = (p, Rn)
    W["Head"] = (p + Rn @ B["Head"][1], Rn @ Rx(-lean * 0.25))
    G, H = foice(t)
    alvos = {"Left": G, "Right": G + H * VAO}
    frente = yaw_dir(psi) @ np.array([0, 0, 1.0])
    for s, sx in (("Left", 1), ("Right", -1)):
        sh = W["UpperChest"][0] + W["UpperChest"][1] @ B[s + "Shoulder"][1]
        W[s + "Shoulder"] = (sh, W["UpperChest"][1])
        S = sh + W["UpperChest"][1] @ B[s + "UpperArm"][1]
        grip = alvos[s]
        dedos = nrm((grip - S) - H * np.dot(grip - S, H))
        Wr = grip - dedos * 0.08
        polo = nrm(-W["UpperChest"][1] @ np.array([0, 1.0, 0]) * 0.6 + W["UpperChest"][1] @ np.array([sx * 1.0, 0, -0.4]))
        E, Wr2 = ik2(S, Wr, L_BRACO, L_ANTE, polo)
        rd = np.array([sx * 1.0, 0, 0])
        Ru = aim(rd, [0, 0, -1], E - S, np.cross(E - S, polo) * sx)
        Rl = aim(rd, [0, 0, -1], Wr2 - E, np.cross(E - S, polo) * sx)
        Rhd = aim(rd, [0, 1, 0], dedos, H * sx)
        W[s + "UpperArm"] = (S, Ru); W[s + "LowerArm"] = (E, Rl); W[s + "Hand"] = (Wr2, Rhd)
        # perna
        Hj = hp + Rh @ B[s + "UpperLeg"][1]
        A, alt = pe(s, t)
        fy = pe_yaw(s, t)
        fdir = yaw_dir(fy) @ np.array([0, 0, 1.0])
        polo_j = nrm(fdir + np.array([0, 0.0, 0]) + (yaw_dir(fy) @ np.array([sx * 0.15, 0, 0])))
        K, A2 = ik2(Hj, A, L_COXA, L_CANELA, polo_j)
        Rt = aim([0, -1, 0], [0, 0, 1], K - Hj, polo_j)
        Rs = aim([0, -1, 0], [0, 0, 1], A2 - K, polo_j)
        Rf = yaw_dir(fy)
        if alt > 0: Rf = Rf @ Rx(-25 * alt / 0.18)
        W[s + "UpperLeg"] = (Hj, Rt); W[s + "LowerLeg"] = (K, Rs); W[s + "Foot"] = (A2, Rf)
        W[s + "Toes"] = (A2 + Rf @ B[s + "Toes"][1], Rf)
    return W, G, H

# ---------------- gerar quadros ----------------
quadros = []; trilha = []
for f in range(N):
    t = f / FPS
    W, G, H = pose(t)
    quadros.append(W); trilha.append((G, H))
# direcao da lamina = para onde a ponta vai (vem na frente do movimento)
pontas = [G - H * CABO + H * COMP * 0.9 for G, H in trilha]
lam = []
ult = None
for i, (G, H) in enumerate(trilha):
    a = pontas[max(0, i - 1)]; b = pontas[min(N - 1, i + 1)]
    v = b - a; v = v - H * np.dot(v, H)
    if np.linalg.norm(v) > 0.05 and i / FPS <= HIT + 0.2: ult = nrm(v)
    elif ult is not None: ult = nrm(ult - H * np.dot(ult, H))
    lam.append(ult)
first = next(x for x in lam if x is not None)
lam = [x if x is not None else first for x in lam]
# suaviza a lamina
for _ in range(3):
    lam = [nrm(lam[max(0, i-1)] + 2 * lam[i] + lam[min(N-1, i+1)]) for i in range(N)]

# ---------------- BVH ----------------
def escrever_bvh(arq):
    linhas = ["HIERARCHY"]
    def rec(n, nivel):
        ind = "\t" * nivel
        pai, off = B[n]
        if pai is None:
            linhas.append(f"ROOT {n}"); linhas.append("{")
            linhas.append(f"\tOFFSET 0.000000 0.000000 0.000000")
            linhas.append("\tCHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation")
        else:
            linhas.append(f"{ind}JOINT {n}"); linhas.append(ind + "{")
            linhas.append(f"{ind}\tOFFSET {off[0]:.6f} {off[1]:.6f} {off[2]:.6f}")
            linhas.append(f"{ind}\tCHANNELS 3 Zrotation Xrotation Yrotation")
        for c in FILHOS[n]: rec(c, nivel + 1)
        if not FILHOS[n]:
            e = FIM.get(n, (0, 0.05, 0))
            linhas.append(f"{ind}\tEnd Site"); linhas.append(ind + "\t{")
            linhas.append(f"{ind}\t\tOFFSET {e[0]:.6f} {e[1]:.6f} {e[2]:.6f}"); linhas.append(ind + "\t}")
        linhas.append(ind + "}")
    rec("Hips", 0)
    linhas.append("MOTION"); linhas.append(f"Frames: {N}"); linhas.append(f"Frame Time: {1.0/FPS:.6f}")
    def ordem(n):
        out = [n]
        for c in FILHOS[n]: out += ordem(c)
        return out
    seq = ordem("Hips")
    for W in quadros:
        vals = []
        for n in seq:
            pai = B[n][0]
            Rw = W[n][1]
            Rl = Rw if pai is None else W[pai][1].T @ Rw
            z, x, y = Rot.from_matrix(Rl).as_euler('ZXY', degrees=True)
            if pai is None:
                p = W[n][0]; vals += [p[0], p[1], p[2]]
            vals += [z, x, y]
        linhas.append(" ".join(f"{v:.5f}" for v in vals))
    open(arq, "w").write("\n".join(linhas) + "\n")

def escrever_trilha(arq):
    # espaco Unity: x = direita (= -x do autor). relativo ao quadril, escala por perna (0.85 m).
    with open(arq, "w") as fh:
        fh.write(f"# golpe3 {FPS} fps, {N} quadros, acerto {HIT:.3f} s, perna {L_COXA+L_CANELA:.3f} m\n")
        fh.write("# gx gy gz hx hy hz lx ly lz  (G = mao de baixo relativa ao quadril; H = haste; L = lamina)\n")
        for W, (G, H), Lm in zip(quadros, trilha, lam):
            g = G - W["Hips"][0]
            fh.write(" ".join(f"{v:.4f}" for v in (-g[0], g[1], g[2], -H[0], H[1], H[2], -Lm[0], Lm[1], Lm[2])) + "\n")

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    escrever_bvh(os.path.join(out, "Ceifadora_Golpe3.bvh"))
    escrever_trilha(os.path.join(out, "golpe3_trilha.txt"))
    # checagem: maos na haste
    err = []
    for W, (G, H) in zip(quadros, trilha):
        for s, alvo in (("Left", G), ("Right", G + H * VAO)):
            err.append(np.linalg.norm(W[s + "Hand"][0] - (alvo - nrm((alvo - W[s+"UpperArm"][0]) - H*np.dot(alvo - W[s+"UpperArm"][0], H)) * 0.08)))
    print("erro maximo das maos (m):", round(max(err), 3), "medio:", round(float(np.mean(err)), 3))
    np.save(os.path.join(out, "quadros.npy"), np.array([[np.concatenate([W[n][0]]) for n in ORDEM] for W in quadros]))
    np.save(os.path.join(out, "foice.npy"), np.array([[*(G - H*CABO), *(G - H*CABO + H*COMP), *L] for (G, H), L in zip(trilha, lam)]))
    json.dump({"ordem": ORDEM, "pais": [B[n][0] for n in ORDEM]}, open(os.path.join(out, "esq.json"), "w"))
