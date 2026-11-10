# Reduz o modelo da foice (GLB) para uso no jogo. Rode no console Python do Blender.
import bpy, os
BASE = r"C:\Users\User\Documents\ModsValheim\Modelos"
ENT = os.path.join(BASE, "foice_geo.glb")
with open(ENT, "wb") as f:
    for i in range(4):
        f.write(open(os.path.join(BASE, "foice_geo.part%d" % i), "rb").read())
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.gltf(filepath=ENT)
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
log = []
for i, o in enumerate(objs):
    antes = len(o.data.polygons)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    for ratio, nome in ((0.03, "foice_baixa.glb"), (0.08, "foice_media.glb")):
        m = o.modifiers.new("reduzir", "DECIMATE"); m.ratio = ratio
        deps = bpy.context.evaluated_depsgraph_get()
        n = len(o.evaluated_get(deps).data.polygons)
        bpy.ops.export_scene.gltf(filepath=os.path.join(BASE, nome), use_selection=True,
                                  export_format="GLB", export_materials="NONE", export_apply=True)
        o.modifiers.remove(m)
        log.append("%s: %d -> %d faces (%s)" % (o.name, antes, n, nome))
open(os.path.join(BASE, "reduzir.log"), "w").write("\n".join(log) + "\nFIM\n")
print("\n".join(log)); print("FIM")
