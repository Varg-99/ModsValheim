// Gera o pacote "ceifadora_golpes" com as animacoes feitas do zero para a Ceifadora (Assets/Ceifada).
// Rodado sem janela pelo gerar_golpe3.bat:
//   Unity.exe -batchmode -projectPath ... -executeMethod CeifadoraGolpes.Build -quit
// Tambem no menu: Ceifadora > Gerar golpes proprios.
// Log em Logs/golpes_build.log.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CeifadoraGolpes
{
    const string Dir = "Assets/Ceifada";
    const string BundleName = "ceifadora_golpes";
    const string OutDir = "Build";
    const string PluginDir = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\CeifadoraObsidiana";
    const string LogPath = "Logs/golpes_build.log";

    static readonly string[] Ossos =
    {
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    };

    static void Log(string s)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss ") + s + Environment.NewLine);
        Debug.Log("[Ceifadora] " + s);
    }

    [MenuItem("Ceifadora/Gerar golpes proprios")]
    public static void Build()
    {
        try
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
            Log("==== inicio ====");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var guids = AssetDatabase.FindAssets("t:Model", new[] { Dir });
            Log("FBX encontrados: " + guids.Length);
            var caminhos = new System.Collections.Generic.List<string>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var name = Path.GetFileNameWithoutExtension(path);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.autoGenerateAvatarMappingIfUnspecified = true;
                imp.importAnimation = true;
                imp.resampleCurves = true;
                imp.animationCompression = ModelImporterAnimationCompression.Off;
                imp.SaveAndReimport();

                // mapa explicito: os ossos tem os mesmos nomes do humanoide do Unity
                var hd = imp.humanDescription;
                var nomesNoModelo = hd.skeleton.Select(s => s.name).ToList();
                int iguais = Ossos.Count(o => nomesNoModelo.Contains(o));
                if (iguais >= 15)
                {
                    // esqueleto feito aqui (golpe3.py): nomes iguais aos do humanoide
                    hd.human = Ossos.Where(o => nomesNoModelo.Contains(o)).Select(o =>
                    {
                        var hb = new HumanBone { humanName = o, boneName = o };
                        hb.limit.useDefaultValues = true;
                        return hb;
                    }).ToArray();
                    imp.humanDescription = hd;
                    imp.autoGenerateAvatarMappingIfUnspecified = false;
                }
                else Log(name + ": esqueleto de fora (ex.: Mixamo), mapa automatico do Unity com " + hd.human.Length + " ossos");

                var takes = imp.importedTakeInfos;
                if (takes == null || takes.Length == 0) { imp.SaveAndReimport(); takes = imp.importedTakeInfos; }
                if (takes == null || takes.Length == 0) { Log("SEM ANIMACAO: " + path); continue; }
                var t = takes[0];
                // cortes opcionais: arquivo <nome>.cortes ao lado do FBX, linhas "NomeDoClipe primeiroQuadro ultimoQuadro"
                var cortes = new System.Collections.Generic.List<(string n, float a, float b)>();
                var arqCortes = Path.ChangeExtension(path, ".cortes");
                if (File.Exists(arqCortes))
                    foreach (var l in File.ReadAllLines(arqCortes))
                    {
                        var p = l.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length == 3 && float.TryParse(p[1], out var fa) && float.TryParse(p[2], out var fb)) cortes.Add((p[0], fa, fb));
                    }
                if (cortes.Count == 0) cortes.Add((name, t.startTime * t.sampleRate, t.stopTime * t.sampleRate));
                Log(name + ": clipes " + string.Join(", ", cortes.Select(c => c.n + " [" + c.a + "-" + c.b + "]")));
                imp.clipAnimations = cortes.Select(c => new ModelImporterClipAnimation
                {
                    name = c.n,
                    takeName = t.name,
                    firstFrame = c.a,
                    lastFrame = c.b,
                    loopTime = false,
                    lockRootRotation = true,        // o giro fica na pose (o personagem nao muda de direcao)
                    keepOriginalOrientation = true,
                    lockRootHeightY = true,         // agachar fica na pose
                    keepOriginalPositionY = true,
                    lockRootPositionXZ = false,     // o avanco vira movimento de verdade
                    keepOriginalPositionXZ = false,
                }).ToArray();
                imp.SaveAndReimport();

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
                var clip = clips.FirstOrDefault();
                foreach (var pn in new[] { "importAnimationErrors", "importAnimationWarnings" })
                {
                    var pi = typeof(ModelImporter).GetProperty(pn);
                    var v = pi != null ? pi.GetValue(imp) as string : null;
                    if (!string.IsNullOrEmpty(v)) Log($"{name} {pn}: {v.Replace("\n", " | ")}");
                }
                Log($"{name}: ossos mapeados {hd.human.Length}/{Ossos.Length}, avatar {(avatar ? (avatar.isValid && avatar.isHuman ? "humanoide OK" : "INVALIDO") : "nenhum")}, clipes {string.Join(", ", clips.Select(c => c.name + " " + c.length.ToString("0.00") + "s humano=" + c.humanMotion))}");
                caminhos.Add(path);
            }
            if (caminhos.Count == 0) { Log("ERRO: nenhum FBX valido"); return; }

            Directory.CreateDirectory(OutDir);
            var builds = new[] { new AssetBundleBuild { assetBundleName = BundleName, assetNames = caminhos.ToArray() } };
            var manifest = BuildPipeline.BuildAssetBundles(OutDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null) { Log("ERRO: BuildAssetBundles falhou"); return; }
            var src = Path.Combine(OutDir, BundleName);
            Log("pacote: " + src + " (" + new FileInfo(src).Length / 1024 + " KB)");
            Directory.CreateDirectory(PluginDir);
            File.Copy(src, Path.Combine(PluginDir, BundleName), true);
            Log("copiado para " + PluginDir);
            Log("RESULTADO: OK");
        }
        catch (Exception e)
        {
            Log("ERRO: " + e);
        }
    }
}
