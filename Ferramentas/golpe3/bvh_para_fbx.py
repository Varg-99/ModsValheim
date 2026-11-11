# Blender (sem janela): importa Ceifadora_Golpe3.bvh e exporta FBX para o projeto Unity.
# Rodado pelo gerar_golpe3.bat:  blender.exe -b -P bvh_para_fbx.py
import bpy, os, traceback
BASE = r"C:\Users\User\Documents\ModsValheim"
BVH = os.path.join(BASE, r"Ferramentas\golpe3\Ceifadora_Golpe3.bvh")
OUT = os.path.join(BASE, r"UnityCeifadora\Assets\Ceifada")
LOG = os.path.join(BASE, r"Ferramentas\golpe3\blender.log")
linhas = []
def log(m):
    print(m); linhas.append(str(m)); open(LOG, "w", encoding="utf-8").write("\n".join(linhas))
try:
    os.makedirs(OUT, exist_ok=True)
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    bpy.ops.import_anim.bvh(filepath=BVH, axis_forward='-Z', axis_up='Y', rotate_mode='NATIVE',
                            use_fps_scale=False, update_scene_fps=True, update_scene_duration=True, global_scale=1.0)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "Ceifadora"; arm.data.name = "Ceifadora"
    ad = arm.animation_data
    if ad and ad.action: ad.action.name = "Ceifadora_Golpe3"
    sc = bpy.context.scene
    if ad and ad.action:
        f0, f1 = ad.action.frame_range
        sc.frame_start, sc.frame_end = int(round(f0)), int(round(f1))
    log("ossos: " + ", ".join(b.name for b in arm.data.bones))
    log("fps %s, quadros %d-%d" % (sc.render.fps, sc.frame_start, sc.frame_end))
    for o in bpy.context.selected_objects: o.select_set(False)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    fbx = os.path.join(OUT, "Ceifadora_Golpe3.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
    log("FBX: %s (%d KB)" % (fbx, os.path.getsize(fbx) // 1024))
    log("RESULTADO: OK")
except Exception:
    log("ERRO:\n" + traceback.format_exc())
