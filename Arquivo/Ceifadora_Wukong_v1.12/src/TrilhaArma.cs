// Giro da arma na mao: le a rotacao do osso do bastao (weapon_r) relativa a mao
// (hand_r) gravada no Blender para cada animacao (pasta "trilhas", 60 quadros/s)
// e aplica a mesma variacao na foice, para ela girar junto com o Wukong.

using BepInEx.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class TrilhaArma
    {
        public static ConfigEntry<bool> CfgAtivo;
        public static ConfigEntry<string> CfgEspelho, CfgEixos;
        public static ConfigEntry<float> CfgIntensidade;
        static readonly Dictionary<string, Quaternion[]> _trilhas = new Dictionary<string, Quaternion[]>();

        public static void Init(ConfigFile c, string pluginDir)
        {
            CfgAtivo = c.Bind("Empunhadura", "SeguirGiroDoBastao", true, "A foice gira na mao seguindo o bastao do Wukong.");
            CfgEspelho = c.Bind("Empunhadura", "EspelhoTrilha", "x", "Conversao de eixo da trilha (nenhum, x, y, z). Troque se o giro sair invertido.");
            CfgEixos = c.Bind("Empunhadura", "EixosTrilha", "0,0,0", "Rotacao (graus) que alinha os eixos do bastao aos da foice.");
            CfgIntensidade = c.Bind("Empunhadura", "IntensidadeTrilhaV2", 0f, "0 = sem giro, 1 = giro completo.");

            var dir = Path.Combine(pluginDir, "trilhas");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.txt"))
            {
                var qs = File.ReadAllLines(f).Where(l => l.Trim().Length > 0).Select(l =>
                {
                    var v = l.Split(' ').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    return new Quaternion(v[1], v[2], v[3], v[0]); // arquivo: w x y z
                }).ToArray();
                if (qs.Length > 0) _trilhas[Path.GetFileNameWithoutExtension(f)] = qs;
            }
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] {_trilhas.Count} trilhas de giro da arma carregadas.");
        }

        static Quaternion Espelhar(Quaternion q)
        {
            switch ((CfgEspelho.Value ?? "").Trim().ToLowerInvariant())
            {
                case "x": return new Quaternion(q.x, -q.y, -q.z, q.w);
                case "y": return new Quaternion(-q.x, q.y, -q.z, q.w);
                case "z": return new Quaternion(-q.x, -q.y, q.z, q.w);
                default: return q;
            }
        }

        static Vector3 Vec(string s)
        {
            var p = (s ?? "").Split(',').Select(x => float.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();
            return new Vector3(p.Length > 0 ? p[0] : 0, p.Length > 1 ? p[1] : 0, p.Length > 2 ? p[2] : 0);
        }

        /// Variacao de rotacao da arma em relacao ao inicio da animacao, no tempo t (s).
        public static Quaternion Delta(string clip, float t)
        {
            if (!CfgAtivo.Value || clip == null || !_trilhas.TryGetValue(clip, out var qs)) return Quaternion.identity;
            int i = Mathf.Clamp(Mathf.RoundToInt(t * 60f), 0, qs.Length - 1);
            var d = Quaternion.Inverse(Espelhar(qs[0])) * Espelhar(qs[i]);
            var p = Quaternion.Euler(Vec(CfgEixos.Value));
            d = p * d * Quaternion.Inverse(p);
            return Quaternion.Slerp(Quaternion.identity, d, Mathf.Clamp01(CfgIntensidade.Value));
        }
    }
}
