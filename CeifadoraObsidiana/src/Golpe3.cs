// 3o golpe feito do zero (estilo "proprio"): animacao criada fora do jogo (golpe3.py -> Blender -> Unity)
// e carregada do pacote "ceifadora_golpes" na pasta do plugin. A trilha da foice (golpe3_trilha.txt)
// diz, quadro a quadro, onde fica a mao de baixo, a haste e a lamina, para a foice seguir o corpo.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Golpe3
    {
        public const string Pacote = "ceifadora_golpes";
        public const string NomeClipe = "Ceifadora_Golpe3";
        public static AnimationClip Clipe;
        public static readonly Dictionary<string, AnimationClip> Bundle = new Dictionary<string, AnimationClip>();
        public static BepInEx.Configuration.ConfigEntry<string> CfgClipe;
        public static BepInEx.Configuration.ConfigEntry<float> CfgAcerto;
        public static BepInEx.Configuration.ConfigEntry<bool> CfgDuasMaos;
        /// a trilha da foice so vale para a animacao feita aqui (Ceifadora_Golpe3)
        public static bool Nossa => Clipe != null && Clipe.name == NomeClipe;
        public static float Fps = 30f, Acerto = 0.85f, Perna = 0.85f;
        static readonly List<Vector3> _g = new List<Vector3>(), _h = new List<Vector3>(), _l = new List<Vector3>();
        public static bool TemTrilha => _g.Count > 1 && Nossa;

        public static void Init(string pastaPlugin)
        {
            try
            {
                var arq = Directory.GetFiles(pastaPlugin, Pacote, SearchOption.AllDirectories).FirstOrDefault();
                if (arq == null) { Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Golpe 3 proprio: pacote '{Pacote}' nao encontrado (usa o avanco)."); return; }
                var b = AssetBundle.LoadFromFile(arq);
                if (b == null) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Golpe 3 proprio: nao consegui abrir o pacote."); return; }
                var todos = b.LoadAllAssets<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
                foreach (var c in todos) Bundle[c.name] = c;
                Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Golpe 3: animacoes no pacote: " + string.Join(", ", todos.Select(c => c.name + " (" + c.length.ToString("0.00") + "s)")));
                var pedido = (CfgClipe?.Value ?? "auto").Trim();
                if (!string.Equals(pedido, "auto", System.StringComparison.OrdinalIgnoreCase))
                    Clipe = todos.FirstOrDefault(c => string.Equals(c.name, pedido, System.StringComparison.OrdinalIgnoreCase));
                if (Clipe == null) Clipe = todos.FirstOrDefault(c => c.name == "Golpe_Mixamo_360") ?? todos.FirstOrDefault(c => c.name != NomeClipe) ?? todos.FirstOrDefault(c => c.name == NomeClipe);
                var t = Path.Combine(Path.Combine(pastaPlugin, "modelo"), "golpe3_trilha.txt");
                if (File.Exists(t)) LerTrilha(t);
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Golpe 3 proprio: clipe {(Clipe ? Clipe.name + " " + Clipe.length.ToString("0.00") + "s, humano " + Clipe.humanMotion : "NAO achado")}, trilha {_g.Count} quadros, acerto {Acerto:0.00}s.");
            }
            catch (System.Exception e) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Golpe 3 proprio: " + e.Message); }
        }

        static void LerTrilha(string arq)
        {
            var ci = CultureInfo.InvariantCulture;
            foreach (var l in File.ReadAllLines(arq))
            {
                if (l.StartsWith("# golpe3"))
                {
                    var p = l.Split(' ');
                    for (int i = 0; i < p.Length - 1; i++)
                    {
                        if (p[i + 1] == "fps,") float.TryParse(p[i], NumberStyles.Float, ci, out Fps);
                        if (p[i] == "acerto") float.TryParse(p[i + 1], NumberStyles.Float, ci, out Acerto);
                        if (p[i] == "perna") float.TryParse(p[i + 1], NumberStyles.Float, ci, out Perna);
                    }
                    continue;
                }
                if (l.StartsWith("#") || l.Trim().Length == 0) continue;
                var v = l.Split(' ').Select(x => float.Parse(x, ci)).ToArray();
                if (v.Length < 9) continue;
                _g.Add(new Vector3(v[0], v[1], v[2])); _h.Add(new Vector3(v[3], v[4], v[5])); _l.Add(new Vector3(v[6], v[7], v[8]));
            }
        }

        /// Amostra a trilha no tempo normalizado u (0..1).
        public static void Amostra(float u, out Vector3 g, out Vector3 h, out Vector3 l)
        {
            float f = Mathf.Clamp01(u) * (_g.Count - 1);
            int i = Mathf.Min(_g.Count - 2, Mathf.FloorToInt(f));
            float k = f - i;
            g = Vector3.Lerp(_g[i], _g[i + 1], k);
            h = Vector3.Slerp(_h[i], _h[i + 1], k).normalized;
            l = Vector3.Slerp(_l[i], _l[i + 1], k).normalized;
        }

        /// Copia os eventos do golpe original (acerto, encadear, fim) para o clipe novo;
        /// o acerto vai para o momento da batida no chao.
        public static void CopiarEventos(AnimationClip orig, AnimationClip novo) => CopiarEventos(orig, novo, -1f);
        public static void CopiarEventos(AnimationClip orig, AnimationClip novo, float acerto)
        {
            if (orig == null || novo == null || orig.length <= 0f) return;
            float hv = orig.events.Where(e => e.functionName == "OnAttackTrigger").Select(e => e.time).DefaultIfEmpty(-1f).First();
            float alvo = acerto > 0f ? acerto : CfgAcerto != null && CfgAcerto.Value > 0f ? CfgAcerto.Value : (Nossa ? Acerto : novo.length * 0.55f);
            float hw = Mathf.Min(alvo, novo.length * 0.95f);
            bool usa = hv > 0f && hv < orig.length;
            System.Func<float, float> map = t =>
            {
                if (!usa) return t * novo.length / orig.length;
                if (t <= hv) return t * hw / hv;
                return hw + (t - hv) * (novo.length - hw) / (orig.length - hv);
            };
            // sem os eventos "Speed" do machado: o ritmo do golpe novo e o da propria animacao
            novo.events = orig.events.Where(e => e.functionName != "Speed").Select(e => new AnimationEvent
            {
                time = Mathf.Min(map(e.time), novo.length),
                functionName = e.functionName, stringParameter = e.stringParameter, floatParameter = e.floatParameter,
                intParameter = e.intParameter, objectReferenceParameter = e.objectReferenceParameter, messageOptions = e.messageOptions,
            }).ToArray();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] eventos {orig.name} ({orig.length:0.00}s) -> {novo.name} ({novo.length:0.00}s): " +
                string.Join(", ", novo.events.Select(e => e.functionName + "@" + e.time.ToString("0.00"))));
        }
    }
}
