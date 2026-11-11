// Golpes da foice (so animacoes do proprio Valheim):
//  - golpes normais mais rapidos (velocidade da animacao);
//  - o ultimo golpe do combo vira um giro (clipe do giro da atgeir), mais rapido,
//    espelhado para girar no sentido contrario do especial, com dano em volta (360).
// O especial (giro da atgeir) fica como no jogo.

using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Golpes
    {
        public static ConfigEntry<float> CfgVelNormal, CfgVelGiro, CfgAlcanceExtraGiro;
        public static ConfigEntry<string> CfgClipeUltimo, CfgClipeGiro, CfgEstilo, CfgClipeCeifada;
        public static ConfigEntry<float> CfgVelCeifada, CfgVelAvanco, CfgDistAvanco, CfgDanoAvanco;
        public static ConfigEntry<string> CfgSentidoAvanco;
        static bool Estilo(string e) => CfgTrocarUltimo.Value && string.Equals((CfgEstilo.Value ?? "").Trim(), e, System.StringComparison.OrdinalIgnoreCase);
        public static bool Ceifada => Estilo("ceifada");
        public static bool Proprio => Estilo("proprio") && Golpe3.Clipe != null;
        public static bool Avanco => Estilo("avanco") || (Estilo("proprio") && Golpe3.Clipe == null);
        public static ConfigEntry<float> CfgVelGolpe3, CfgEscalaAvancoGolpe3;
        static string ClipeAlvo => Ceifada ? (CfgClipeCeifada.Value ?? "").Trim() : CfgClipeGiro.Value;

        // ---- avanco: medidas de giro para continuar no mesmo sentido do 2o golpe ----
        public static float AcumAtgeir, AcumGolpe2;  // rotacao acumulada (graus) do especial e do fim do 2o golpe
        public static bool EspelharAvanco;          // decidido no inicio do 3o golpe
        public static Vector3 AvancoPendente;       // deslocamento extra a somar no movimento do golpe
        public static ConfigEntry<bool> CfgEspelhar, CfgTrocarUltimo;

        public static void Init(ConfigFile c)
        {
            CfgVelNormal = c.Bind("Golpes", "VelocidadeGolpes", 1.25f, "Velocidade dos golpes normais (1 = como o machado do jogo; 1.25 = 25% mais rapido).");
            CfgTrocarUltimo = c.Bind("Golpes", "UltimoGolpeGiratorio", true, "O ultimo golpe do combo vira um giro em volta do personagem.");
            CfgVelGiro = c.Bind("Golpes", "VelocidadeGiro", 1.4f, "Velocidade do giro do ultimo golpe.");
            CfgEspelhar = c.Bind("Golpes", "GiroNoSentidoContrario", true, "O giro do ultimo golpe vai no sentido contrario do giro do especial.");
            CfgAlcanceExtraGiro = c.Bind("Golpes", "AlcanceExtraGiro", 0.5f, "Alcance extra (m) do giro do ultimo golpe.");
            CfgClipeUltimo = c.Bind("Golpes", "ClipeDoUltimoGolpe", "BattleAxe_Combo3", "Animacao do jogo que e trocada pelo giro (precisa reiniciar).");
            CfgClipeGiro = c.Bind("Golpes", "ClipeDoGiro", "Atgeir360Attack", "Animacao do jogo usada no giro (precisa reiniciar).");
            CfgEstilo = c.Bind("Golpes", "EstiloDoUltimoGolpe4", "proprio",
                "proprio = golpe feito do zero (gira, avanca e bate a foice no chao a frente; precisa do pacote ceifadora_golpes). avanco = continua o giro do 2o golpe, avanca e golpeia para frente com muito dano. ceifada = arcos largos guiados. giro = giro de 360 graus antigo.");
            CfgClipeCeifada = c.Bind("Golpes", "ClipeDoCorpoNaCeifada", "",
                "Animacao do corpo na ceifada. Vazio = a do proprio machado (BattleAxe_Combo3). Precisa reiniciar.");
            CfgVelCeifada = c.Bind("Golpes", "VelocidadeCeifada", 1.15f, "Velocidade do ultimo golpe no estilo ceifada.");
            CfgVelAvanco = c.Bind("Golpes", "VelocidadeAvanco", 1.3f, "Velocidade do ultimo golpe no estilo avanco.");
            CfgDistAvanco = c.Bind("Golpes", "DistanciaAvanco", 2.5f, "Estilo avanco: quanto (m) o personagem avanca para frente durante o giro.");
            CfgDanoAvanco = c.Bind("Golpes", "DanoExtraAvanco", 1.6f, "Estilo avanco: multiplicador de dano do ultimo golpe (1.6 = 60% a mais).");
            CfgVelGolpe3 = c.Bind("Golpes", "VelocidadeGolpe3Proprio", 1.0f, "Velocidade do 3o golpe feito do zero (estilo proprio).");
            CfgEscalaAvancoGolpe3 = c.Bind("Golpes", "AvancoGolpe3Proprio", 1.0f, "Quanto do avanco do 3o golpe proprio e usado (1 = ~2 m, 0.5 = metade, 0 = parado).");
            CfgSentidoAvanco = c.Bind("Golpes", "SentidoDoGiroNoAvanco", "auto",
                "auto = continua girando no mesmo sentido em que o 2o golpe termina (medido no jogo). normal / espelhado = forca um sentido.");
        }

        public static bool ComFoice(Humanoid h)
        {
            var w = h?.GetCurrentWeapon();
            return w?.m_dropPrefab != null && w.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName;
        }

        public static Attack AtaqueAtual(Humanoid h) => Traverse.Create(h).Field("m_currentAttack").GetValue<Attack>();
        public static bool Secundario(Humanoid h) => Traverse.Create(h).Field("m_currentAttackIsSecondary").GetValue<bool>();
        public static int Nivel(Attack a) => a == null ? -1 : Traverse.Create(a).Field("m_currentAttackCainLevel").GetValue<int>();
        public static int Niveis(Attack a) => a == null ? 0 : Traverse.Create(a).Field("m_attackChainLevels").GetValue<int>();
        public static bool Ultimo(Attack a) => a != null && Niveis(a) > 1 && Nivel(a) >= Niveis(a) - 1;

        /// Velocidade da animacao para o ataque atual do jogador local com a foice.
        public static float Mult(Humanoid h)
        {
            if (!(h is Player p) || p != Player.m_localPlayer || !ComFoice(h)) return 1f;
            var a = AtaqueAtual(h);
            if (a == null || Secundario(h)) return 1f;
            if (CfgTrocarUltimo.Value && Ultimo(a)) return Proprio ? CfgVelGolpe3.Value : Ceifada ? CfgVelCeifada.Value : Avanco ? CfgVelAvanco.Value : CfgVelGiro.Value;
            return CfgVelNormal.Value;
        }

        public static Animator AnimatorDe(Character c) => Traverse.Create(c).Field("m_animator").GetValue<Animator>();

        // ---- troca do clipe do ultimo golpe ----
        static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> _cache = new Dictionary<RuntimeAnimatorController, AnimatorOverrideController>();
        static readonly Dictionary<Animator, RuntimeAnimatorController> _originais = new Dictionary<Animator, RuntimeAnimatorController>();

        static bool _listou;
        public static void Atualizar(Player p)
        {
            var anim = AnimatorDe(p);
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (!_listou && ComFoice(p))
            {
                _listou = true;
                var nomes = anim.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().OrderBy(n => n);
                Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Clipes do personagem: " + string.Join(", ", nomes));
            }
            bool com = ComFoice(p) && CfgTrocarUltimo.Value && (Proprio || !string.IsNullOrEmpty(ClipeAlvo));
            bool trocado = _originais.ContainsKey(anim);
            if (com && !trocado)
            {
                var orig = anim.runtimeAnimatorController;
                if (!_cache.TryGetValue(orig, out var ovr))
                {
                    var giro = Proprio ? Golpe3.Clipe : orig.animationClips.FirstOrDefault(c => c.name == ClipeAlvo);
                    ovr = new AnimatorOverrideController(orig);
                    var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    ovr.GetOverrides(pares);
                    int n = 0;
                    for (int i = 0; i < pares.Count; i++)
                        if (giro != null && pares[i].Key.name == CfgClipeUltimo.Value)
                        {
                            if (Proprio) Golpe3.CopiarEventos(pares[i].Key, giro);
                            pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(pares[i].Key, giro); n++;
                        }
                    ovr.ApplyOverrides(pares);
                    _cache[orig] = ovr;
                    Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Ultimo golpe: {CfgClipeUltimo.Value} -> {(giro ? giro.name : "(clipe do giro nao achado)")} ({n} troca).");
                }
                _originais[anim] = orig;
                TrocarMantendo(anim, ovr);
            }
            else if (!com && trocado)
            {
                TrocarMantendo(anim, _originais[anim]);
                _originais.Remove(anim);
            }
        }

        static void TrocarMantendo(Animator a, RuntimeAnimatorController novo)
        {
            var f = new Dictionary<int, float>(); var i = new Dictionary<int, int>(); var b = new Dictionary<int, bool>();
            foreach (var p in a.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float) f[p.nameHash] = a.GetFloat(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Int) i[p.nameHash] = a.GetInteger(p.nameHash);
                else if (p.type == AnimatorControllerParameterType.Bool) b[p.nameHash] = a.GetBool(p.nameHash);
            }
            a.runtimeAnimatorController = novo;
            foreach (var kv in f) a.SetFloat(kv.Key, kv.Value);
            foreach (var kv in i) a.SetInteger(kv.Key, kv.Value);
            foreach (var kv in b) a.SetBool(kv.Key, kv.Value);
        }

        // ---- espelhamento do giro ----
        public static bool GiroAtivo; // ultimo golpe normal em andamento

        public static float PesoGiro(Animator anim)
        {
            if (!GiroAtivo || Ceifada || Proprio || anim == null) return 0f;
            if (Avanco ? !EspelharAvanco : !CfgEspelhar.Value) return 0f;
            float w = 0f;
            foreach (var ci in anim.GetCurrentAnimatorClipInfo(0)) if (ci.clip != null && ci.clip.name == ClipeAlvo) w += ci.weight;
            if (anim.IsInTransition(0))
                foreach (var ci in anim.GetNextAnimatorClipInfo(0)) if (ci.clip != null && ci.clip.name == ClipeAlvo) w += ci.weight;
            return Mathf.Clamp01(w);
        }
    }

    // Inicio de cada golpe: velocidade e marcacao do giro.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class Patch_StartAttack
    {
        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result || !(__instance is Player p) || p != Player.m_localPlayer || !Golpes.ComFoice(p)) return;
            var a = Golpes.AtaqueAtual(p);
            bool sec = Golpes.Secundario(p);
            Golpes.GiroAtivo = !sec && Golpes.CfgTrocarUltimo.Value && Golpes.Ultimo(a);
            if (!sec && Golpes.Nivel(a) == 1) Golpes.AcumGolpe2 = 0f;
            if (Golpes.GiroAtivo && Golpes.Avanco)
            {
                var modo = (Golpes.CfgSentidoAvanco.Value ?? "auto").Trim().ToLowerInvariant();
                if (modo.StartsWith("esp")) Golpes.EspelharAvanco = true;
                else if (modo.StartsWith("norm")) Golpes.EspelharAvanco = false;
                else if (Mathf.Abs(Golpes.AcumAtgeir) > 90f && Mathf.Abs(Golpes.AcumGolpe2) > 15f)
                    Golpes.EspelharAvanco = Mathf.Sign(Golpes.AcumAtgeir) != Mathf.Sign(Golpes.AcumGolpe2);
                Golpes.AvancoPendente = Vector3.zero;
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] avanco: giro do especial {Golpes.AcumAtgeir:0}, fim do 2o golpe {Golpes.AcumGolpe2:0} -> {(Golpes.EspelharAvanco ? "espelhado" : "normal")}");
            }
            var anim = Golpes.AnimatorDe(p);
            float m = Golpes.Mult(p);
            if (anim != null) anim.speed = m;
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] golpe {(sec ? "especial" : "normal")} nivel {Golpes.Nivel(a)}, velocidade {m:0.00}{(Golpes.GiroAtivo ? (Golpes.Proprio ? ", golpe 3 proprio" : Golpes.Ceifada ? ", ceifada" : Golpes.Avanco ? ", avanco" : ", giro") : "")}");
        }
    }

    // O jogo volta a velocidade para 1 fora dos ataques; durante o golpe mantem a nossa.
    [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
    public static class Patch_AnimEventFixed
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterAnimEvent __instance)
        {
            var ch = Traverse.Create(__instance).Field("m_character").GetValue<Character>() as Humanoid;
            if (!(ch is Player p) || p != Player.m_localPlayer || !Golpes.ComFoice(p)) return;
            if (Golpes.AtaqueAtual(p) == null) { Golpes.GiroAtivo = false; return; }
            if (Traverse.Create(__instance).Field("m_pauseTimer").GetValue<float>() > 0f) return;
            var anim = Traverse.Create(__instance).Field("m_animator").GetValue<Animator>();
            float m = Golpes.Mult(p);
            if (anim != null && Mathf.Abs(anim.speed - 1f) < 0.001f && m != 1f) anim.speed = m;
        }
    }

    // Eventos "Speed" das animacoes do jogo: multiplica pela nossa velocidade.
    [HarmonyPatch(typeof(CharacterAnimEvent), "Speed")]
    public static class Patch_AnimEventSpeed
    {
        [HarmonyPrefix]
        public static void Prefix(CharacterAnimEvent __instance, ref float __0)
        {
            var ch = Traverse.Create(__instance).Field("m_character").GetValue<Character>() as Humanoid;
            __0 *= Golpes.Mult(ch);
        }
    }

    // Giro do ultimo golpe acerta em volta toda.
    [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    public static class Patch_DoMeleeAttack_Giro
    {
        [HarmonyPrefix]
        public static void Prefix(Attack __instance, out Vector3 __state)
        {
            __state = new Vector3(-1f, -1f, -1f);
            var h = Traverse.Create(__instance).Field("m_character").GetValue<Humanoid>();
            if (!(h is Player p) || p != Player.m_localPlayer || !Golpes.ComFoice(p) || !Golpes.GiroAtivo) return;
            __state = new Vector3(__instance.m_attackAngle, __instance.m_attackRange, __instance.m_damageMultiplier);
            __instance.m_attackAngle = 360f;
            __instance.m_attackRange += Golpes.CfgAlcanceExtraGiro.Value;
            if (Golpes.Avanco || Golpes.Proprio) __instance.m_damageMultiplier *= Mathf.Max(0.1f, Golpes.CfgDanoAvanco.Value);
            Impacto.Antes(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(Attack __instance, Vector3 __state)
        {
            if (__state.x < 0f) return;
            __instance.m_attackAngle = __state.x;
            __instance.m_attackRange = __state.y;
            __instance.m_damageMultiplier = __state.z;
            var h = Traverse.Create(__instance).Field("m_character").GetValue<Humanoid>();
            if (h is Player p) Impacto.Depois(__instance, p);
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    public static class Patch_PlayerUpdate
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            Golpes.Atualizar(__instance);
            Espelho.Atualizar(__instance);
        }
    }

    // Avanco do ultimo golpe: soma um deslocamento para frente ao movimento da animacao.
    [HarmonyPatch(typeof(Character), "AddRootMotion")]
    public static class Patch_AddRootMotion_Avanco
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, ref Vector3 __0)
        {
            if (__instance != Player.m_localPlayer) return;
            if (Golpes.GiroAtivo && Golpes.Proprio && __instance.InAttack()) __0 *= Mathf.Max(0f, Golpes.CfgEscalaAvancoGolpe3.Value);
            if (Golpes.AvancoPendente == Vector3.zero) return;
            __0 += Golpes.AvancoPendente;
            Golpes.AvancoPendente = Vector3.zero;
        }
    }
}
