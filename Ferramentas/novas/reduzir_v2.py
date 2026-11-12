# Blender (sem janela): refaz a reducao das 4 foices SEM frestas.
# Diferenca da versao antiga: antes de reduzir, junta os vertices repetidos (as costuras dos pedacos da textura).
# Assim a reducao trata a foice como uma peca so e as bordas continuam encostadas.
import bpy, bmesh, os, struct, traceback, math
BASE = r"C:\Users\User\Documents\ModsValheim"
SAI = os.path.join(BASE, r"Ferramentas\novas\v2")
os.makedirs(SAI, exist_ok=True)
LOG = os.path.join(SAI, "blender.log")
MODELOS = [  # nome, arquivo, triangulos
    ("ferro", r"Modelos\novas\ferro.glb", 40000),
    ("Metal_negro", r"Modelos\novas\Metal_negro.glb", 40000),
    ("flametal", r"Modelos\novas\flametal.glb", 80000),
    ("obsidiana", r"Modelos\foice_geo.glb", 33000),
]
linhas = []
def log(m):
    print(m); linhas.append(str(m)); open(LOG, "w", encoding="utf-8").write("\n".join(linhas))
try:
    for nome, rel, alvo in MODELOS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        glb = os.path.join(BASE, rel)
        if not os.path.exists(glb): log("%s: arquivo nao encontrado (%s)" % (nome, glb)); continue
        bpy.ops.import_scene.gltf(filepath=glb)
        objs = [o for o in bpy.data.objects if o.type == "MESH"]
        obj = objs[0]
        if len(objs) > 1:  # varias pecas: junta numa so
            bpy.ops.object.select_all(action="DESELECT")
            for o in objs: o.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.join()
        me = obj.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        # 1) junta vertices repetidos (a correcao das frestas)
        bm = bmesh.new(); bm.from_mesh(me)
        xs = [v.co for v in bm.verts]
        diag = math.dist((min(c.x for c in xs), min(c.y for c in xs), min(c.z for c in xs)),
                         (max(c.x for c in xs), max(c.y for c in xs), max(c.z for c in xs)))
        n0 = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=diag * 2e-5)
        n1 = len(bm.verts)
        bm.to_mesh(me); bm.free()
        # 2) reduz
        mod = obj.modifiers.new("dec", "DECIMATE"); mod.decimate_type = "COLLAPSE"
        mod.ratio = min(1.0, alvo / max(1, tris)); mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj; obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier="dec")
        me = obj.data
        # 3) triangula e marca quinas vivas (angulo > 45 graus) para o sombreamento
        bm = bmesh.new(); bm.from_mesh(me)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        for f in bm.faces: f.smooth = True
        for e in bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(45): e.smooth = False
        bm.to_mesh(me); bm.free()
        me.calc_loop_triangles()
        uv = me.uv_layers.active.data
        try: ln = [l.vector for l in me.corner_normals]
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
        log("%s: juntou %d -> %d vertices; %d -> %d triangulos; %d vertices finais" % (nome, n0, n1, tris, len(idx) // 3, len(pos)))
    log("RESULTADO: OK")
except Exception:
    log("ERRO:\n" + traceback.format_exc())
