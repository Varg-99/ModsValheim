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
        // ---- combo de 4 golpes + especial em 3 partes, com animacoes do Mixamo ----
        public static ConfigEntry<bool> CfgModo4;
        public static ConfigEntry<string> CfgMapa4, CfgAcertos4;
        public static ConfigEntry<float> CfgVelMixamo, CfgDano3, CfgDano4, CfgDanoEspFinal, CfgEmpurrao3;
        public static bool Modo4 => CfgModo4 != null && CfgModo4.Value && Golpe3.Bundle.Count > 0;
        public static int NivelAtual = -1;
        public static bool EspecialAtual;
        public static ConfigEntry<bool> CfgEspecial3;
        public static bool Especial3 => Modo4 && CfgEspecial3 != null && CfgEspecial3.Value;
        public static bool UmaMao => Modo4 && ((EspecialAtual && Especial3) || (!EspecialAtual && NivelAtual >= 2));
        static ConfigFile _cfg; static System.DateTime _cfgData; static float _proxConf; static string _ultAcertos, _ultMapa;
        static bool _remontar;
        public static void InitModo4(ConfigFile c)
        {
            _cfg = c;
            CfgModo4 = c.Bind("Combo4", "Ativar", true, "Combo de 4 golpes (2 do machado + 2 do Mixamo) e especial em 3 partes (aperte o especial 3 vezes).");
            CfgEspecial3 = c.Bind("Combo4", "EspecialEm3Partes", false,
                "false = especial e o giro da atgeir (foice para fora do corpo); true = especial do Mixamo em 3 partes. Precisa reiniciar.");
            CfgMapa4 = c.Bind("Combo4", "Animacoes2",
                "DualAxes Attack 1=BattleAxe1; DualAxes Attack 2 2=BattleAxe2; DualAxes Attack 3 2=Golpe_Mixamo_360; DualAxes Attack 4=Golpe_Mixamo_4; " +
                "Greatsword BaseAttack (1)=Especial_1; Greatsword BaseAttack (2)=Especial_2; Greatsword BaseAttack (3)=Especial_3",
                "Qual animacao entra em cada golpe (original=nova). Precisa reiniciar.");
            CfgAcertos4 = c.Bind("Combo4", "Acertos4", "Golpe_Mixamo_360=1.1; Golpe_Mixamo_4=0.8; Especial_1=0.90; Especial_2=0.55; Especial_3=0.30",
                "Segundo do dano em cada animacao do Mixamo. Pode mudar com o jogo aberto: salve o arquivo e o proximo golpe ja usa o valor novo.");
            CfgVelMixamo = c.Bind("Combo4", "VelocidadeMixamo", 0.92f, "Velocidade dos golpes 3, 4 e do especial.");
            CfgDano3 = c.Bind("Combo4", "Dano3oGolpe", 1.3f, "Multiplicador de dano do 3o golpe.");
            CfgDano4 = c.Bind("Combo4", "Dano4oGolpe", 2.0f, "Multiplicador de dano do 4o golpe (o ultimo).");
            CfgEmpurrao3 = c.Bind("Combo4", "Empurrao3oGolpe", 0.2f, "Empurrao do 3o golpe nos inimigos (1 = normal, 0.2 = bem pouco), para o 4o golpe ainda acertar.");
            CfgDanoEspFinal = c.Bind("Combo4", "DanoUltimaParteEspecial", 1.5f, "Multiplicador de dano da 3a parte do especial.");
        }
        static Dictionary<string, string> Pares(string s) => (s ?? "").Split(';').Select(x => x.Split('='))
            .Where(x => x.Length == 2).ToDictionary(x => x[0].Trim(), x => x[1].Trim());

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
            CfgVelNormal = c.Bind("Golpes", "VelocidadeGolpes", 1f, "Velocidade dos golpes normais (1 = como o machado do jogo; 1.25 = 25% mais rapido).");
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
            return w?.m_dropPrefab != null && CeifadoraPlugin.Prefabs.Contains(w.m_dropPrefab.name);
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
            if (a == null) return 1f;
            if (Modo4)
            {
                if (Secundario(h)) return Especial3 ? CfgVelMixamo.Value : 1f;
                return Nivel(a) >= 2 ? CfgVelMixamo.Value : CfgVelNormal.Value;
            }
            if (Secundario(h)) return 1f;
            if (CfgTrocarUltimo.Value && Ultimo(a)) return Proprio ? CfgVelGolpe3.Value : Ceifada ? CfgVelCeifada.Value : Avanco ? CfgVelAvanco.Value : CfgVelGiro.Value;
            return CfgVelNormal.Value;
        }

        public static Animator AnimatorDe(Character c) => Traverse.Create(c).Field("m_animator").GetValue<Animator>();

        // ---- troca do clipe do ultimo golpe ----
        static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> _cache = new Dictionary<RuntimeAnimatorController, AnimatorOverrideController>();
        static readonly Dictionary<Animator, RuntimeAnimatorController> _originais = new Dictionary<Animator, RuntimeAnimatorController>();

        static bool _listou;
        /// Le de novo o arquivo de configuracao quando ele e salvo com o jogo aberto.
        static void VigiarConfig(Player p)
        {
            if (_cfg == null || Time.unscaledTime < _proxConf) return;
            _proxConf = Time.unscaledTime + 1f;
            try
            {
                var d = System.IO.File.GetLastWriteTimeUtc(_cfg.ConfigFilePath);
                if (_cfgData == default(System.DateTime)) { _cfgData = d; _ultAcertos = CfgAcertos4.Value; _ultMapa = CfgMapa4.Value; return; }
                if (d == _cfgData) return;
                _cfgData = d;
                _cfg.Reload();
            }
            catch (System.Exception e) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Nao consegui reler a configuracao: " + e.Message); return; }
            if (CfgAcertos4.Value != _ultAcertos || CfgMapa4.Value != _ultMapa)
            {
                _ultAcertos = CfgAcertos4.Value; _ultMapa = CfgMapa4.Value; _remontar = true;
                CeifadoraPlugin.Detalhe("[CeifadoraObsidiana] Acertos4 recarregado: " + _ultAcertos);
            }
        }

        public static void Atualizar(Player p)
        {
            VigiarConfig(p);
            var anim = AnimatorDe(p);
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (_remontar && !p.InAttack())
            {
                // volta ao controlador original e descarta os montados: logo abaixo monta de novo com os valores novos
                _remontar = false;
                foreach (var kv in _originais.ToList()) if (kv.Key != null) TrocarMantendo(kv.Key, kv.Value);
                _originais.Clear(); _cache.Clear();
            }
            if (!_listou && ComFoice(p))
            {
                _listou = true;
                var nomes = anim.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().OrderBy(n => n);
                CeifadoraPlugin.Detalhe("[CeifadoraObsidiana] Clipes do personagem: " + string.Join(", ", nomes));
            }
            bool com = ComFoice(p) && (Modo4 || (CfgTrocarUltimo.Value && (Proprio || !string.IsNullOrEmpty(ClipeAlvo))));
            bool trocado = _originais.ContainsKey(anim);
            if (com && !trocado)
            {
                var orig = anim.runtimeAnimatorController;
                if (!_cache.TryGetValue(orig, out var ovr))
                {
                    ovr = new AnimatorOverrideController(orig);
                    if (Modo4) { MontarModo4(orig, ovr); _cache[orig] = ovr; _originais[anim] = orig; TrocarMantendo(anim, ovr); return; }
                    var giro = Proprio ? Golpe3.Clipe : orig.animationClips.FirstOrDefault(c => c.name == ClipeAlvo);
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
                    CeifadoraPlugin.Detalhe($"[CeifadoraObsidiana] Ultimo golpe: {CfgClipeUltimo.Value} -> {(giro ? giro.name : "(clipe do giro nao achado)")} ({n} troca).");
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

        static void MontarModo4(RuntimeAnimatorController orig, AnimatorOverrideController ovr)
        {
            var mapa = Pares(CfgMapa4.Value);
            var acertos = Pares(CfgAcertos4.Value);
            var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ovr.GetOverrides(pares);
            var log = new List<string>();
            for (int i = 0; i < pares.Count; i++)
            {
                if (!mapa.TryGetValue(pares[i].Key.name, out var nomeNovo)) continue;
                AnimationClip novo;
                if (Golpe3.Bundle.TryGetValue(nomeNovo, out var doPacote))
                {
                    novo = UnityEngine.Object.Instantiate(doPacote); novo.name = doPacote.name;
                    float hit = acertos.TryGetValue(nomeNovo, out var hs) && float.TryParse(hs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hv) ? hv : novo.length * 0.5f;
                    Golpe3.CopiarEventos(pares[i].Key, novo, hit);
                }
                else novo = orig.animationClips.FirstOrDefault(c => c.name == nomeNovo);
                if (novo == null) { log.Add(pares[i].Key.name + " -> (" + nomeNovo + " nao achado)"); continue; }
                pares[i] = new KeyValuePair<AnimationClip, AnimationClip>(pares[i].Key, novo);
                log.Add(pares[i].Key.name + " -> " + novo.name + " " + novo.length.ToString("0.00") + "s");
            }
            ovr.ApplyOverrides(pares);
            CeifadoraPlugin.Detalhe("[CeifadoraObsidiana] Combo4: " + string.Join("; ", log));
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
            UmAcertoPorGolpe.Atingidos.Clear();
            var a = Golpes.AtaqueAtual(p);
            bool sec = Golpes.Secundario(p);
            Golpes.GiroAtivo = !sec && Golpes.CfgTrocarUltimo.Value && Golpes.Ultimo(a) && !Golpes.Modo4;
            Golpes.NivelAtual = Golpes.Nivel(a); Golpes.EspecialAtual = sec;
            if (!sec && Golpes.Nivel(a) == 1) Golpes.AcumGolpe2 = 0f;
            if (Golpes.GiroAtivo && Golpes.Avanco)
            {
                var modo = (Golpes.CfgSentidoAvanco.Value ?? "auto").Trim().ToLowerInvariant();
                if (modo.StartsWith("esp")) Golpes.EspelharAvanco = true;
                else if (modo.StartsWith("norm")) Golpes.EspelharAvanco = false;
                else if (Mathf.Abs(Golpes.AcumAtgeir) > 90f && Mathf.Abs(Golpes.AcumGolpe2) > 15f)
                    Golpes.EspelharAvanco = Mathf.Sign(Golpes.AcumAtgeir) != Mathf.Sign(Golpes.AcumGolpe2);
                Golpes.AvancoPendente = Vector3.zero;
            }
            var anim = Golpes.AnimatorDe(p);
            float m = Golpes.Mult(p);
            if (anim != null) anim.speed = m;
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
        static float _forca = -1f;
        [HarmonyPrefix]
        public static void Prefix(Attack __instance, out Vector3 __state)
        {
            __state = new Vector3(-1f, -1f, -1f);
            var h = Traverse.Create(__instance).Field("m_character").GetValue<Humanoid>();
            if (!(h is Player p0) || p0 != Player.m_localPlayer || !Golpes.ComFoice(p0)) return;
            if (Golpes.Modo4)
            {
                int nv = Golpes.Nivel(__instance);
                bool sec = Golpes.Secundario(p0);
                if (!sec && nv < 2) return;
                if (sec && !Golpes.Especial3) return; // giro da atgeir: como no jogo
                __state = new Vector3(__instance.m_attackAngle, __instance.m_attackRange, __instance.m_damageMultiplier);
                // 3o golpe empurra pouco, para o 4o ainda alcancar o inimigo
                var f = Traverse.Create(__instance).Field("m_forceMultiplier");
                if (!sec && nv == 2 && f.FieldExists()) { _forca = f.GetValue<float>(); f.SetValue(_forca * Mathf.Max(0f, Golpes.CfgEmpurrao3.Value)); }
                __instance.m_attackAngle = 360f;
                __instance.m_attackRange += Golpes.CfgAlcanceExtraGiro.Value;
                if (!sec) __instance.m_damageMultiplier *= nv == 2 ? Golpes.CfgDano3.Value : Golpes.CfgDano4.Value;
                else if (nv == 2) __instance.m_damageMultiplier *= Golpes.CfgDanoEspFinal.Value;
                return;
            }
            if (!(h is Player p) || !Golpes.GiroAtivo) return;
            __state = new Vector3(__instance.m_attackAngle, __instance.m_attackRange, __instance.m_damageMultiplier);
            __instance.m_attackAngle = 360f;
            __instance.m_attackRange += Golpes.CfgAlcanceExtraGiro.Value;
            if (Golpes.Avanco || Golpes.Proprio) __instance.m_damageMultiplier *= Mathf.Max(0.1f, Golpes.CfgDanoAvanco.Value);
        }

        [HarmonyPostfix]
        public static void Postfix(Attack __instance, Vector3 __state)
        {
            if (__state.x < 0f) return;
            __instance.m_attackAngle = __state.x;
            __instance.m_attackRange = __state.y;
            __instance.m_damageMultiplier = __state.z;
            if (_forca >= 0f) { Traverse.Create(__instance).Field("m_forceMultiplier").SetValue(_forca); _forca = -1f; }
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

    // Cada inimigo so leva dano uma vez por golpe (pode acertar varios inimigos diferentes).
    // A lista zera a cada golpe novo (StartAttack). So vale para o dano que sai do golpe em si
    // (dentro de DoMeleeAttack), entao o sangramento continua funcionando.
    [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    public static class UmAcertoPorGolpe
    {
        public static readonly HashSet<int> Atingidos = new HashSet<int>();
        public static bool Dentro;
        public static ConfigEntry<bool> CfgLigado;

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static void Prefix(Attack __instance)
        {
            var h = Traverse.Create(__instance).Field("m_character").GetValue<Humanoid>();
            Dentro = h != null && h == Player.m_localPlayer && Golpes.ComFoice(h) && (CfgLigado == null || CfgLigado.Value);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        public static void Postfix() { Dentro = false; }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class Patch_Damage_UmAcerto
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (!UmAcertoPorGolpe.Dentro || __instance == null || __instance == Player.m_localPlayer) return true;
            if (UmAcertoPorGolpe.Atingidos.Add(__instance.GetInstanceID())) return true;
            return false; // ja tomou dano deste golpe
        }
    }
}
