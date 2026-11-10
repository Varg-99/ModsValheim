# Acha o momento do acerto (hit) de cada golpe do Wukong, pela velocidade da ponta do bastao.
# Rode no Blender depois do converter (usa as acoes ja carregadas na cena).
# Gera Animacoes/golpes.txt: nome quadro_hit quadro_corte total_quadros (60 fps)
import bpy, os
from mathutils import Vector
BASE = r"C:\Users\User\Documents\ModsValheim"
OUT = os.path.join(BASE, r"Animacoes\golpes.txt")
arm = bpy.data.objects.get("Wukong") or next(o for o in bpy.data.objects if o.type == "ARMATURE")
pb = arm.pose.bones
nomes = [a.name for a in bpy.data.actions if a.name.startswith("AS_Wukong_ComboB_q") or a.name.startswith("AS_Wukong_xuli_attack")]
linhas, info = [], []
for nome in sorted(nomes):
    act = bpy.data.actions[nome]
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    if hasattr(ad, "action_slot") and getattr(act, "slots", None):
        try: ad.action_slot = act.slots[0]
        except Exception: pass
    f0, f1 = (int(x) for x in act.frame_range)
    pts = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        w = pb["weapon_r"].matrix; r = pb["root"].matrix.translation if "root" in pb else Vector()
        base = w.translation - r
        # ponta do bastao: 1 m em cada eixo local (o maior movimento vence)
        pts.append([base] + [base + w.to_3x3() @ Vector(a) * 100 for a in ((1,0,0),(0,1,0),(0,0,1),(-1,0,0),(0,-1,0),(0,0,-1))])
    vel = [0.0]
    for i in range(1, len(pts)):
        vel.append(max((pts[i][k] - pts[i-1][k]).length for k in range(len(pts[i]))))
    n = len(vel)
    a, b = int(n * 0.05), int(n * 0.85)
    pico = max(range(a, b), key=lambda i: vel[i])
    hit = pico
    while hit < n - 1 and vel[hit] > vel[pico] * 0.45: hit += 1
    corte = min(n - 1, hit + 14)
    linhas.append("%s %d %d %d" % (nome, hit, corte, n))
    info.append("%s: pico %d (%.1f), hit %d, corte %d de %d" % (nome, pico, vel[pico], hit, corte, n))
with open(OUT, "w") as fh: fh.write("\n".join(linhas))
with open(OUT.replace(".txt", "_info.txt"), "w") as fh: fh.write("\n".join(info))
print("\n".join(info)); print("FIM")
