# Converte animações do Black Myth: Wukong (.ueanim, exportadas pelo FModel)
# para FBX, para serem importadas no Unity como Humanoid.
# Rode no Blender: aba Scripting > Abrir este arquivo > Run Script (Alt+P).
# Uso pessoal: os FBX gerados vêm de arquivos do jogo e não devem ser publicados.

import bpy, os, sys, importlib, traceback

BASE = r"C:\Users\User\Documents\ModsValheim"
TOOLS = os.path.join(BASE, "Ferramentas")
EXPORTS = os.path.join(TOOLS, r"FModel\Output\Exports\b1\Content")
SKEL = os.path.join(EXPORTS, r"00MainHZ\Characters\Wukong\Meshes\Main\SK_Wukong_Main.uemodel")
ANIMDIR = os.path.join(EXPORTS, r"00Main\Animation\Player\Wukong\AS\Attack")
OUT = os.path.join(BASE, r"UnityCeifadora\Assets\Wukong")
LOGDIR = os.path.join(BASE, "Animacoes")
os.makedirs(OUT, exist_ok=True)
LOG = os.path.join(LOGDIR, "conversao.log")

ANIMS = [
    r"ComBoB\AS_Wukong_ComboB_q1", r"ComBoB\AS_Wukong_ComboB_q2", r"ComBoB\AS_Wukong_ComboB_q3", r"ComBoB\AS_Wukong_ComboB_q4",
    r"Xuli\AS_Wukong_xuli_start", r"Xuli\AS_Wukong_xuli_loop",
    r"Xuli\AS_Wukong_xuli_attack_1", r"Xuli\AS_Wukong_xuli_attack_2", r"Xuli\AS_Wukong_xuli_attack_3", r"Xuli\AS_Wukong_xuli_attack_4",
]
VELOCIDADE = {"AS_Wukong_ComboB_q2": 1.3, "AS_Wukong_ComboB_q4": 1.5}

def todas_fcurves(act):
    out = []
    for layer in getattr(act, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                out.extend(bag.fcurves)
    if not out and hasattr(act, "fcurves"):
        out = list(act.fcurves)
    return out

TRILHAS = os.path.join(BASE, r"Animacoes\trilhas")
os.makedirs(TRILHAS, exist_ok=True)

def gravar_trilha(arm, act, name):
    # rotacao do osso da arma (weapon_r) relativa a mao (hand_r), quadro a quadro (60 fps)
    pb = arm.pose.bones
    if "weapon_r" not in pb or "hand_r" not in pb:
        log("sem weapon_r/hand_r para trilha"); return
    f0, f1 = (int(x) for x in act.frame_range)
    linhas = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        m = pb["hand_r"].matrix.inverted() @ pb["weapon_r"].matrix
        q = m.to_quaternion()
        linhas.append("%.5f %.5f %.5f %.5f" % (q.w, q.x, q.y, q.z))
    with open(os.path.join(TRILHAS, name + ".txt"), "w") as fh:
        fh.write("\n".join(linhas))
    log("trilha %s: %d quadros" % (name, len(linhas)))


log_lines = []
def log(msg):
    print(msg)
    log_lines.append(str(msg))
    with open(LOG, "w", encoding="utf-8") as f:
        f.write("\n".join(log_lines))

def ensure_addon():
    for name in list(sys.modules):
        if name.endswith("io_scene_ueformat.importer.import_context"):
            return importlib.import_module(name.rsplit(".importer", 1)[0])
    zip_path = os.path.join(TOOLS, "io_scene_ueformat.zip")
    try:
        bpy.ops.extensions.package_install_files(filepath=zip_path, repo="user_default", enable_on_install=True)
        log("addon instalado como extensao")
    except Exception as e:
        log("instalacao como extensao falhou: %s; tentando addon_install" % e)
        bpy.ops.preferences.addon_install(filepath=zip_path, overwrite=True)
        bpy.ops.preferences.addon_enable(module="io_scene_ueformat")
    for cand in ("bl_ext.user_default.io_scene_ueformat", "io_scene_ueformat"):
        try:
            return importlib.import_module(cand)
        except ImportError:
            pass
    raise RuntimeError("addon UEFormat nao encontrado")

try:
    pkg = ensure_addon()
    ic = importlib.import_module(pkg.__name__ + ".importer.import_context")
    opts = importlib.import_module(pkg.__name__ + ".options")
    log("addon: " + pkg.__name__)
    if not hasattr(pkg, "zstd_decompressor"):
        import zstandard
        pkg.zstd_decompressor = zstandard.ZstdDecompressor()
        log("zstd_decompressor criado")

    # cena limpa
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    arm = ic.UEFormatImport(opts.UEModelOptions()).import_file(SKEL)
    if arm is None or arm.type != "ARMATURE":
        arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "Wukong"
    # O Wukong olha para +X (Unreal); o Unity espera a frente em -Y do Blender.
    import math
    arm.rotation_mode = "XYZ"
    arm.rotation_euler[2] += math.radians(-90)
    bpy.context.view_layer.update()
    log("ossos (%d):" % len(arm.data.bones))
    for b in arm.data.bones:
        log("  %s <- %s" % (b.name, b.parent.name if b.parent else "-"))

    for rel in ANIMS:
        path = os.path.join(ANIMDIR, rel + ".ueanim")
        name = os.path.basename(rel)
        if not os.path.exists(path):
            log("FALTA: " + path); continue
        try:
            for o in bpy.context.selected_objects: o.select_set(False)
            arm.select_set(True)
            bpy.context.view_layer.objects.active = arm
            act = ic.UEFormatImport(opts.UEAnimOptions(override_skeleton=arm)).import_file(path)
            ad = arm.animation_data or arm.animation_data_create()
            if act is not None:
                ad.action = act
            act = ad.action
            act.name = name
            if hasattr(ad, "action_slot") and getattr(act, "slots", None) and ad.action_slot is None:
                ad.action_slot = act.slots[0]
            # v12: acelera os golpes normais mais longos (ritmo mais proximo do Valheim)
            fator = VELOCIDADE.get(name, 1.0)
            if fator != 1.0:
                n = 0
                for fc in todas_fcurves(act):
                    for kp in fc.keyframe_points:
                        kp.co.x /= fator; kp.handle_left.x /= fator; kp.handle_right.x /= fator
                    fc.update(); n += 1
                log("velocidade %s x%.2f (%d curvas)" % (name, fator, n))
            bpy.context.scene.render.fps = 60
            bpy.context.scene.render.fps_base = 1.0
            f0, f1 = act.frame_range
            bpy.context.scene.frame_start, bpy.context.scene.frame_end = int(f0), int(f1)
            gravar_trilha(arm, act, name)
            # v11: mantem o deslocamento do osso "root" (vira root motion no Unity,
            # dosado pelo mod) e remove so o giro.
            removidas = []
            for fc in list(act.fcurves) if hasattr(act, "fcurves") else []:
                if fc.data_path.startswith('pose.bones["root"].rotation'):
                    vals = [k.co[1] for k in fc.keyframe_points]
                    removidas.append("%s[%d] %.2f..%.2f" % (fc.data_path.split('.')[-1], fc.array_index, min(vals), max(vals)))
                    act.fcurves.remove(fc)
            if not hasattr(act, "fcurves"):
                # Blender 5 (acoes em camadas): percorre os canais
                for layer in act.layers:
                    for strip in layer.strips:
                        for bag in strip.channelbags:
                            for fc in list(bag.fcurves):
                                if fc.data_path.startswith('pose.bones["root"].rotation'):
                                    vals = [k.co[1] for k in fc.keyframe_points]
                                    removidas.append("%s[%d] %.2f..%.2f" % (fc.data_path.split('.')[-1], fc.array_index, min(vals), max(vals)))
                                    bag.fcurves.remove(fc)
            log("root removido em %s: %s" % (name, ", ".join(removidas) or "nada"))
            rb = arm.pose.bones.get("root")
            if rb:
                rb.rotation_quaternion = (1, 0, 0, 0)
            fbx = os.path.join(OUT, name + ".fbx")
            bpy.ops.export_scene.fbx(
                filepath=fbx, use_selection=True, object_types={"ARMATURE"},
                add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
            log("OK %s (%d-%d) -> %s" % (name, f0, f1, fbx))
        except Exception:
            log("ERRO em %s:\n%s" % (name, traceback.format_exc()))
    log("FIM")
except Exception:
    log("ERRO GERAL:\n" + traceback.format_exc())
