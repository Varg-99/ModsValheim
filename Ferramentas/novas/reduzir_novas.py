# Blender (sem janela): reduz as foices novas (1,5 milhao de triangulos) e grava a malha em formato cru
# para o Claude converter. Rodado pelo reduzir_novas.bat.
import bpy, bmesh, os, struct, traceback
BASE = r"C:\Users\User\Documents\ModsValheim"
ENT = os.path.join(BASE, r"Modelos\novas")
SAI = os.path.join(BASE, r"Ferramentas\novas")
LOG = os.path.join(SAI, "blender.log")
ALVO = 40000  # triangulos
ALVOS = {"flametal": 80000}  # a de penas precisa de mais detalhe
linhas = []
def log(m):
    print(m); linhas.append(str(m)); open(LOG, "w", encoding="utf-8").write("\n".join(linhas))
try:
    # processa os modelos que chegaram (em pedacos ou .glb) e ainda nao tem .raw
    nomes = sorted({f.split(".glb")[0] for f in os.listdir(ENT) if ".glb" in f})
    nomes = [n for n in nomes if not os.path.exists(os.path.join(SAI, n + ".raw")) or any(f.startswith(n + ".glb.part") for f in os.listdir(ENT))]
    log("modelos: " + ", ".join(nomes))
    for nome in nomes:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        glb = os.path.join(ENT, nome + ".glb")
        partes = sorted(f for f in os.listdir(ENT) if f.startswith(nome + ".glb.part"))
        if partes:  # o arquivo veio em pedacos de 8 MB: junta
            with open(glb, "wb") as out:
                for pp in partes: out.write(open(os.path.join(ENT, pp), "rb").read())
            for pp in partes: os.remove(os.path.join(ENT, pp))
        log("%s: %d MB" % (nome, os.path.getsize(glb) // 1000000))
        bpy.ops.import_scene.gltf(filepath=glb)
        obj = next(o for o in bpy.data.objects if o.type == "MESH")
        me = obj.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        mod = obj.modifiers.new("dec", "DECIMATE"); mod.decimate_type = "COLLAPSE"
        mod.ratio = min(1.0, ALVOS.get(nome, ALVO) / max(1, tris)); mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj; obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier="dec")
        me = obj.data
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
        me.calc_loop_triangles()
        uv = me.uv_layers.active.data
        try: ln = [l.normal for l in me.corner_normals]
        except Exception:
            try: me.calc_normals_split()
            except Exception: pass
            ln = [l.normal for l in me.loops]
        verts = {}; pos = []; idx = []
        for tri in me.loop_triangles:
            for li in tri.loops:
                v = me.loops[li].vertex_index
                co = me.vertices[v].co; n = ln[li]; t = uv[li].uv
                k = (v, round(t[0], 5), round(t[1], 5), round(n[0], 2), round(n[1], 2), round(n[2], 2))
                if k not in verts:
                    verts[k] = len(pos); pos.append((co[0], co[1], co[2], n[0], n[1], n[2], t[0], t[1]))
                idx.append(verts[k])
        arq = os.path.join(SAI, nome + ".raw")
        with open(arq, "wb") as f:
            f.write(b"RAW1"); f.write(struct.pack("<ii", len(pos), len(idx)))
            for p in pos: f.write(struct.pack("<8f", *p))
            f.write(struct.pack("<%dI" % len(idx), *idx))
        log("%s: %d -> %d triangulos, %d vertices, rot %s -> %s" % (nome, tris, len(idx) // 3, len(pos), tuple(obj.rotation_euler), arq))
    log("RESULTADO: OK")
except Exception:
    log("ERRO:\n" + traceback.format_exc())
