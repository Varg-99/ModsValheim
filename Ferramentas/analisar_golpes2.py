# v2: velocidade das maos (sem o deslocamento do root), suavizada; grava a curva para conferencia.
import bpy, os
from mathutils import Vector
BASE = r"C:\Users\User\Documents\ModsValheim"
OUT = os.path.join(BASE, r"Animacoes\golpes.txt")
arm = bpy.data.objects.get("Wukong") or next(o for o in bpy.data.objects if o.type == "ARMATURE")
pb = arm.pose.bones
nomes = [a.name for a in bpy.data.actions if a.name.startswith("AS_Wukong_ComboB_q") or a.name.startswith("AS_Wukong_xuli_attack")]
linhas, info, curvas = [], [], []
for nome in sorted(set(nomes)):
    act = bpy.data.actions[nome]
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    if hasattr(ad, "action_slot") and getattr(act, "slots", None):
        try: ad.action_slot = act.slots[0]
        except Exception: pass
    f0, f1 = (int(x) for x in act.frame_range)
    pos = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        r = pb["root"].matrix.translation.copy() if "root" in pb else Vector()
        pos.append([(pb[b].matrix.translation - r) for b in ("hand_r", "hand_l", "weapon_r") if b in pb])
    v = [0.0] + [max((pos[i][k] - pos[i-1][k]).length for k in range(len(pos[i]))) for i in range(1, len(pos))]
    n = len(v)
    s = [sum(v[max(0,i-2):i+3]) / len(v[max(0,i-2):i+3]) for i in range(n)]
    a, b = int(n * 0.05), int(n * 0.8)
    pico = max(range(a, b), key=lambda i: s[i])
    hit = pico
    while hit < n - 1 and s[hit] > s[pico] * 0.35: hit += 1
    corte = min(n - 1, hit + 12)
    linhas.append("%s %d %d %d" % (nome, hit, corte, n))
    info.append("%s: pico %d (%.3f), hit %d, corte %d de %d" % (nome, pico, s[pico], hit, corte, n))
    curvas.append(nome + ": " + " ".join("%.2f" % x for x in s))
with open(OUT, "w") as fh: fh.write("\n".join(linhas))
with open(OUT.replace(".txt", "_info.txt"), "w") as fh: fh.write("\n".join(info) + "\n\n" + "\n".join(curvas))
print("\n".join(info)); print("FIM")
