// Gera o AssetBundle "ceifadora_anims" automaticamente ao abrir o projeto.
// 1) Importa os FBX de Assets/Wukong como Humanoid (avatar do próprio FBX).
// 2) Renomeia cada clipe para o nome do arquivo, sem loop, com root motion "assado" na pose.
// 3) Gera o bundle para Windows e copia para BepInEx/plugins/CeifadoraObsidiana.
// Log em Logs/ceifadora_build.log (na pasta do projeto).
// Menu manual: Ceifadora > Gerar pacote de animações.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CeifadoraBuild
{
    const string FbxDir = "Assets/Wukong";
    const string BundleName = "ceifadora_anims";
    const string OutDir = "Build";
    const string PluginDir = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CeifadoraObsidiana";
    const string Flag = "Logs/ceifadora_build.done";
    static readonly string LogPath = "Logs/ceifadora_build.log";

    static CeifadoraBuild()
    {
        if (File.Exists(Flag)) return; // já gerado uma vez; use o menu para regerar
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
            Run();
        };
    }

    const string GolpesTxt = @"C:\Users\User\Documents\ModsValheim\Animacoes\golpes.txt";
    // nome -> quadro de corte (golpes normais terminam logo apos o acerto)
    static System.Collections.Generic.Dictionary<string, int> Cortes()
    {
        var d = new System.Collections.Generic.Dictionary<string, int>();
        if (!File.Exists(GolpesTxt)) return d;
        foreach (var l in File.ReadAllLines(GolpesTxt))
        {
            var p = l.Split(' ');
            if (p.Length >= 3 && p[0].Contains("ComboB") && int.TryParse(p[2], out int c)) d[p[0]] = c;
        }
        return d;
    }

    static void Log(string s)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss ") + s + Environment.NewLine);
        Debug.Log("[Ceifadora] " + s);
    }

    [MenuItem("Ceifadora/Gerar pacote de animações")]
    public static void Run()
    {
        try
        {
            Log("==== inicio ====");
            var guids = AssetDatabase.FindAssets("t:Model", new[] { FbxDir });
            Log("FBX encontrados: " + guids.Length);
            var cortes = Cortes();
            Log("cortes: " + string.Join(", ", cortes.Select(kv => kv.Key + "=" + kv.Value)));
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                var name = Path.GetFileNameWithoutExtension(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.resampleCurves = true;
                imp.animationCompression = ModelImporterAnimationCompression.Optimal;

                // Quadril = "Bip" (pernas e coluna sao filhas dele, nao do pelvis)
                var hd = imp.humanDescription;
                if (hd.human != null && hd.human.Length > 0)
                {
                    var human = hd.human;
                    for (int i = 0; i < human.Length; i++)
                        if (human[i].humanName == "Hips" && human[i].boneName != "Bip") { human[i].boneName = "Bip"; }
                    hd.human = human;
                    imp.autoGenerateAvatarMappingIfUnspecified = false;
                    imp.humanDescription = hd;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }
                else
                {
                    imp.SaveAndReimport();
                    hd = imp.humanDescription;
                    var human = hd.human;
                    for (int i = 0; i < human.Length; i++)
                        if (human[i].humanName == "Hips") human[i].boneName = "Bip";
                    hd.human = human;
                    imp.autoGenerateAvatarMappingIfUnspecified = false;
                    imp.humanDescription = hd;
                }

                var takes = imp.importedTakeInfos;
                if (takes == null || takes.Length == 0) { imp.SaveAndReimport(); takes = imp.importedTakeInfos; }
                if (takes == null || takes.Length == 0) { Log("SEM TAKE: " + path); continue; }
                var t = takes[0];
                var clip = new ModelImporterClipAnimation
                {
                    name = name,
                    takeName = t.name,
                    firstFrame = t.startTime * t.sampleRate,
                    lastFrame = cortes.TryGetValue(name, out int qc) ? Mathf.Min(qc, t.stopTime * t.sampleRate) : t.stopTime * t.sampleRate,
                    loopTime = name.EndsWith("_loop") || (name.Contains("_loop_") && !name.Contains("to")),
                    lockRootRotation = true,
                    lockRootHeightY = true,
                    lockRootPositionXZ = false, // cb_v10: deslocamento vira root motion (o mod dosa quanto o Viking anda)
                    keepOriginalOrientation = true, // usa a frente do esqueleto (corrigida no Blender), nao a do tronco
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = false, // centro de massa
                };
                imp.clipAnimations = new[] { clip };
                imp.assetBundleName = BundleName;
                imp.SaveAndReimport();

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                foreach (var pn in new[] { "importAnimationErrors", "importAnimationWarnings" })
                {
                    var pi = typeof(ModelImporter).GetProperty(pn);
                    var v = pi != null ? pi.GetValue(imp) as string : null;
                    if (!string.IsNullOrEmpty(v)) Log($"{name} {pn}: {v.Replace("\n", " | ")}");
                }
                if (name.EndsWith("ComboA_q1"))
                    foreach (var hb in imp.humanDescription.human)
                        Log($"  mapa {hb.humanName} -> {hb.boneName}");
                Log($"{name}: Hips={imp.humanDescription.human.FirstOrDefault(h => h.humanName == "Hips").boneName}");
                Log($"{name}: take='{t.name}' {t.stopTime - t.startTime:0.00}s avatar={(avatar ? (avatar.isValid && avatar.isHuman ? "humanoide OK" : "INVALIDO") : "nenhum")}");
            }

            Directory.CreateDirectory(OutDir);
            var manifest = BuildPipeline.BuildAssetBundles(OutDir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null) { Log("ERRO: BuildAssetBundles falhou"); return; }
            var src = Path.Combine(OutDir, BundleName);
            Log("bundle gerado: " + src + " (" + new FileInfo(src).Length / 1024 + " KB)");

            try
            {
                Directory.CreateDirectory(PluginDir);
                File.Copy(src, Path.Combine(PluginDir, BundleName), true);
                Log("copiado para " + PluginDir);
                if (File.Exists(GolpesTxt)) { File.Copy(GolpesTxt, Path.Combine(PluginDir, "golpes.txt"), true); Log("golpes.txt copiado"); }
            }
            catch (Exception e) { Log("AVISO: nao consegui copiar para o Valheim (" + e.Message + "). Copie Build/" + BundleName + " manualmente."); }

            File.WriteAllText(Flag, DateTime.Now.ToString());
            Log("RESULTADO: OK");
        }
        catch (Exception e)
        {
            Log("ERRO: " + e);
        }
    }
}
