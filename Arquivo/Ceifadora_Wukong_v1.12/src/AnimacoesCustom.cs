// Animações próprias da Ceifadora, carregadas de um AssetBundle local opcional.
//
// Se existir "ceifadora_anims" na pasta do plugin, quando a Ceifadora estiver
// equipada as animações de ataque do Viking são trocadas pelas do bundle, usando
// um AnimatorOverrideController. Ao desequipar, volta tudo ao normal.
// Sem o bundle, nada muda (a arma usa as animações do Valheim).
//
// O bundle NÃO é distribuído com o mod: cada pessoa monta o seu.
//
// Configuração (BepInEx/config/fabiano.ceifadoraobsidiana.cfg, seção Animacoes):
//   Trocas = "clipeOriginal=clipeDoBundle; outroOriginal=outroDoBundle"
//   ListarClipes = true -> grava no log todos os clipes do Animator do Viking,
//                         para descobrir os nomes dos clipes do battleaxe/atgeir.

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class AnimacoesCustom
    {
        public const string BundleName = "ceifadora_anims";

        public static ConfigEntry<string> CfgTrocas;
        public static ConfigEntry<bool> CfgListarClipes;

        private static AssetBundle _bundle;
        private static readonly Dictionary<string, AnimationClip> _clipesBundle = new Dictionary<string, AnimationClip>();
        private static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> _cache =
            new Dictionary<RuntimeAnimatorController, AnimatorOverrideController>();
        private static readonly Dictionary<Animator, RuntimeAnimatorController> _originais =
            new Dictionary<Animator, RuntimeAnimatorController>();
        private static bool _listou;
        private static bool _avisouSemAnimator;

        public static void Init(ConfigFile config, string pluginDir)
        {
            CfgTrocas = config.Bind("Animacoes", "TrocasCombo3", "",
                "Pares 'clipeOriginal=clipeDoBundle' separados por ';'. Vazio = padrao: " + TrocasPadrao);
            CfgTrocas.SettingChanged += (_, __) => LimparCache();
            CfgListarClipes = config.Bind("Animacoes", "ListarClipes", true,
                "Grava no log do BepInEx os nomes de todos os clipes do Animator do jogador (uma vez por sessão).");

            var gt = Path.Combine(pluginDir, "golpes.txt");
            if (File.Exists(gt))
                foreach (var l in File.ReadAllLines(gt))
                {
                    var p = l.Split(' ');
                    if (p.Length >= 2 && int.TryParse(p[1], out int q)) _hits[p[0]] = q / 60f;
                }
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] {_hits.Count} momentos de acerto: {string.Join(", ", _hits.Select(kv => kv.Key + "=" + kv.Value.ToString("0.00")))}");

            string path = Directory.GetFiles(pluginDir, BundleName, SearchOption.AllDirectories).FirstOrDefault();
            if (path == null)
            {
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] '{BundleName}' não encontrado: usando animações do Valheim.");
                return;
            }

            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null) { Jotunn.Logger.LogError("[CeifadoraObsidiana] Falha ao abrir o bundle de animações."); return; }

            foreach (var clip in _bundle.LoadAllAssets<AnimationClip>())
                _clipesBundle[clip.name] = clip;
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] {_clipesBundle.Count} animações carregadas: {string.Join(", ", _clipesBundle.Keys)}");
        }

        public const string TrocasPadrao =
            "2Hand-Spear-Attack1=AS_Wukong_ComboB_q1;2Hand-Spear-Attack3=AS_Wukong_ComboB_q2;2Hand-Spear-Attack9=AS_Wukong_ComboB_q4";

        private static readonly Dictionary<string, float> _hits = new Dictionary<string, float>();
        /// Momento (s) em que o golpe acerta, medido no Blender; -1 se desconhecido.
        public static float Hit(string nome) => nome != null && _hits.TryGetValue(nome, out var h) ? h : -1f;

        public static bool Ativo => _originais.Count > 0;
        public static bool TemPacote => _clipesBundle.Count > 0;
        public static AnimationClip Clipe(string nome) => _clipesBundle.TryGetValue(nome, out var c) ? c : null;

        /// Animacao do Wukong tocando agora (golpe normal ou carga) e o tempo nela.
        public static bool ClipeAtual(Player p, out string nome, out float t)
        {
            nome = null; t = 0f;
            if (Carga.Ativa) { nome = Carga.ClipeNome; t = Carga.Tempo; return nome != null; }
            var anim = p.GetComponentInChildren<Animator>();
            if (anim == null) return false;
            for (int l = 0; l < anim.layerCount; l++)
            {
                foreach (var ci in anim.GetCurrentAnimatorClipInfo(l))
                {
                    if (ci.clip == null || ci.weight < 0.5f || !_clipesBundle.ContainsKey(ci.clip.name)) continue;
                    var st = anim.GetCurrentAnimatorStateInfo(l);
                    nome = ci.clip.name;
                    t = Mathf.Clamp01(st.normalizedTime) * ci.clip.length;
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<AnimationClip> ClipesDoPacote() => _clipesBundle.Values;

        private static void LimparCache()
        {
            foreach (var kv in _originais.ToList())
                if (kv.Key) TrocarMantendoEstado(kv.Key, kv.Value);
            _originais.Clear();
            _cache.Clear();
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Trocas de animacao atualizadas.");
        }

        private static Dictionary<string, string> Trocas() =>
            (string.IsNullOrWhiteSpace(CfgTrocas.Value) ? TrocasPadrao : CfgTrocas.Value).Split(';')
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2 && p[0].Trim().Length > 0)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        public static void Atualizar(Player player)
        {
            var animator = Traverse.Create(player).Field("m_animator").GetValue<Animator>()
                           ?? player.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                if (!_avisouSemAnimator)
                {
                    _avisouSemAnimator = true;
                    Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Animator do jogador ainda não disponível.");
                }
                return;
            }

            if (CfgListarClipes.Value && !_listou)
            {
                _listou = true;
                var nomes = animator.runtimeAnimatorController.animationClips
                    .Select(c => $"{c.name} ({c.length:0.00}s)").Distinct().OrderBy(n => n);
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Avatar: {(animator.avatar ? animator.avatar.name : "nenhum")}, humanoide: {animator.isHuman}");
                Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Clipes do jogador:\n  " + string.Join("\n  ", nomes));
            }

            if (_clipesBundle.Count == 0) return;

            var arma = player.GetCurrentWeapon();
            bool comFoice = arma?.m_dropPrefab != null && arma.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName;
            bool trocado = _originais.ContainsKey(animator);

            if (comFoice && !trocado)
            {
                var original = animator.runtimeAnimatorController;
                if (!_cache.TryGetValue(original, out var ovr))
                {
                    ovr = new AnimatorOverrideController(original);
                    var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    ovr.GetOverrides(pares);
                    var trocas = Trocas();
                    for (int i = 0; i < pares.Count; i++)
                    {
                        var orig = pares[i].Key;
                        if (trocas.TryGetValue(orig.name, out string novo) && _clipesBundle.TryGetValue(novo, out var clip))
                        {
                            CopiarEventos(orig, clip);
                            pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, clip);
                            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] {orig.name} ({orig.length:0.00}s) -> {clip.name} ({clip.length:0.00}s), {orig.events.Length} eventos");
                        }
                    }
                    ovr.ApplyOverrides(pares);
                    _cache[original] = ovr;
                }
                _originais[animator] = original;
                TrocarMantendoEstado(animator, ovr);
            }
            else if (!comFoice && trocado)
            {
                TrocarMantendoEstado(animator, _originais[animator]);
                _originais.Remove(animator);
            }
        }

        // Os ataques do Valheim dependem de eventos nos clipes (acerto, encadear combo,
        // fim do ataque). Copia os eventos do clipe original para o novo, na mesma
        // proporcao do tempo.
        private static void CopiarEventos(AnimationClip orig, AnimationClip novo)
        {
            if (orig.length <= 0f) return;
            // O evento de acerto do Valheim vai para o momento real do acerto do Wukong;
            // o resto acompanha (antes e depois do acerto, em proporcao).
            float hv = orig.events.Where(e => e.functionName == "OnAttackTrigger").Select(e => e.time).DefaultIfEmpty(-1f).First();
            float hw = Hit(novo.name);
            bool usaHit = hv > 0f && hw > 0f && hw < novo.length && hv < orig.length;
            System.Func<float, float> map = t =>
            {
                if (!usaHit) return t * novo.length / orig.length;
                if (t <= hv) return t * hw / hv;
                return hw + (t - hv) * (novo.length - hw) / (orig.length - hv);
            };
            novo.events = orig.events.Select(e => new AnimationEvent
            {
                time = Mathf.Min(map(e.time), novo.length),
                functionName = e.functionName,
                stringParameter = e.stringParameter,
                floatParameter = e.floatParameter,
                intParameter = e.intParameter,
                objectReferenceParameter = e.objectReferenceParameter,
                messageOptions = e.messageOptions,
            }).ToArray();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] eventos {novo.name}: " + string.Join(", ", novo.events.Select(e => e.functionName + "@" + e.time.ToString("0.00"))));
        }

        // Trocar o controller reinicia o Animator; guarda e restaura os parâmetros.
        private static void TrocarMantendoEstado(Animator a, RuntimeAnimatorController novo)
        {
            var floats = new Dictionary<int, float>();
            var ints = new Dictionary<int, int>();
            var bools = new Dictionary<int, bool>();
            foreach (var p in a.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float) floats[p.nameHash] = a.GetFloat(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Int) ints[p.nameHash] = a.GetInteger(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Bool) bools[p.nameHash] = a.GetBool(p.nameHash);
            }
            a.runtimeAnimatorController = novo;
            foreach (var kv in floats) a.SetFloat(kv.Key, kv.Value);
            foreach (var kv in ints) a.SetInteger(kv.Key, kv.Value);
            foreach (var kv in bools) a.SetBool(kv.Key, kv.Value);
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    public static class Patch_PlayerUpdate_Animacoes
    {
        private static bool _primeira;

        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            Empunhadura.Tick(CeifadoraPlugin.Instance.Config);
            bool wk = (__instance.InAttack() && AnimacoesCustom.Ativo) || Carga.Ativa;
            Empunhadura.AtualizarPose(wk);
            Empunhadura.GirarCorpo(__instance, wk);
            Empunhadura.Quadro(__instance);
            Carga.VigiarCombo(__instance);
            Fotos.Tick(__instance);
            Avanco.Garantir(__instance);
            Carga.Tick(__instance);
            if (EldenheimCompat.Active) return;
            if (!_primeira) { _primeira = true; Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Jogador local detectado."); }
            AnimacoesCustom.Atualizar(__instance);
        }
    }
}
