// Ataque especial da Ceifadora: segurar o botao secundario gira o bastao
// (giro da postura pesada do Wukong, causando dano em volta) e acumula niveis
// de carga. Cada nivel muda o brilho da foice. Ao soltar, sai o golpe
// carregado do nivel atingido (z5_attack_0..4).

using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CeifadoraObsidiana
{
    internal static class Carga
    {
        public static ConfigEntry<float> CfgTempoNivel, CfgStaminaSeg, CfgStaminaGolpe, CfgDanoGiro, CfgRaio;
        public static ConfigEntry<string> CfgMultNiveis, CfgClipInicio, CfgClipGiro, CfgClipGolpes;

        enum Estado { Inativo, Iniciando, Carregando, Transicao, Soltando }
        static Estado _estado = Estado.Inativo;
        static int _nivel;
        static float _tEstado, _tNivel, _tGiro;
        static bool _acertou;
        static PlayableGraph _graph;
        static AnimationClipPlayable _play;
        static AnimationClip _clipAtual;
        public static bool ForcarSegurar; // usado pelas fotos de diagnostico
        public static bool Ativa => _estado != Estado.Inativo;
        public static string ClipeNome => _clipAtual != null ? _clipAtual.name : null;
        public static float Tempo => _tEstado;
        static float _multGolpe = 1f, _anguloGolpe = 140f, _momentoGolpe = 0.42f;
        static float _ultimoQ3 = -10f;
        public static ConfigEntry<string> CfgClipQuarto;
        public static ConfigEntry<float> CfgMultQuarto;
        public static bool Soltando => _estado == Estado.Soltando;

        static readonly Color[] Cores =
        {
            new Color(0.35f, 0.02f, 0.02f), // 0 brasa
            new Color(0.10f, 0.45f, 1.60f), // 1 azul
            new Color(0.20f, 1.40f, 0.30f), // 2 verde
            new Color(1.80f, 1.20f, 0.10f), // 3 dourado
            new Color(2.50f, 0.15f, 0.10f), // 4 vermelho intenso
        };

        static readonly FieldInfo MoveDir = AccessTools.Field(typeof(Character), "m_moveDir");

        public static void Init(ConfigFile c)
        {
            CfgTempoNivel = c.Bind("Carga", "SegundosPorNivel", 1.1f, "Tempo segurando para subir um nivel de carga (0 a 4).");
            CfgStaminaSeg = c.Bind("Carga", "StaminaPorSegundo", 6f, "Stamina gasta por segundo enquanto gira o bastao.");
            CfgStaminaGolpe = c.Bind("Carga", "StaminaGolpe", 20f, "Stamina do golpe carregado ao soltar.");
            CfgDanoGiro = c.Bind("Carga", "MultiplicadorGiro", 0.25f, "Dano de cada acerto do giro (x dano da arma), a cada 0,5 s.");
            CfgRaio = c.Bind("Carga", "Alcance", 3.2f, "Alcance dos acertos do giro e do golpe carregado (m).");
            CfgClipInicio = c.Bind("Carga", "AnimInicioEsmagadora", "AS_Wukong_xuli_start", "Animacao ao comecar a carregar (postura esmagadora).");
            CfgClipGiro = c.Bind("Carga", "AnimGiroEsmagadora", "AS_Wukong_xuli_loop", "Animacao do giro do bastao enquanto carrega (repete).");
            CfgClipGolpes = c.Bind("Carga", "AnimGolpesEsmagadora", "AS_Wukong_xuli_attack_1,AS_Wukong_xuli_attack_1,AS_Wukong_xuli_attack_2,AS_Wukong_xuli_attack_3,AS_Wukong_xuli_attack_4",
                "Golpe ao soltar, por nivel de carga (0..4), separados por virgula.");
            CfgClipQuarto = c.Bind("Animacoes", "GolpeFinal", "AS_Wukong_ComboB_q4", "3o golpe do combo normal (bastao no chao, dano em area a frente).");
            CfgMultQuarto = c.Bind("Animacoes", "MultiplicadorAreaGolpeFinal", 1.0f, "Dano extra em area do 3o golpe (x dano da arma), alem do acerto normal.");
            CfgMultNiveis = c.Bind("Carga", "MultiplicadorNiveis", "1.2,1.6,2.1,2.7,3.5", "Dano do golpe carregado por nivel (0..4).");
        }

        static float Mult(int nivel)
        {
            var v = CfgMultNiveis.Value.Split(',').Select(s => float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 1f).ToArray();
            return v.Length == 0 ? 1f : v[Mathf.Clamp(nivel, 0, v.Length - 1)];
        }

        static string Golpe(int nivel)
        {
            var v = CfgClipGolpes.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            return v.Length == 0 ? "" : v[Mathf.Clamp(nivel, 0, v.Length - 1)];
        }

        static bool ComFoice(Player p)
        {
            var w = p.GetCurrentWeapon();
            return w?.m_dropPrefab != null && w.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName;
        }

        static bool BotaoSegurado()
        {
            if (ForcarSegurar) return true;
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return false;
            return ZInput.GetButton("SecondaryAttack") || ZInput.GetButton("JoySecondaryAttack");
        }

        static void Tocar(Player p, string nome)
        {
            var clip = AnimacoesCustom.Clipe(nome);
            if (clip == null) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] clipe ausente: " + nome); return; }
            var anim = p.GetComponentInChildren<Animator>();
            if (!_graph.IsValid())
            {
                _graph = PlayableGraph.Create("CeifadoraCarga");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            }
            else if (_play.IsValid()) _play.Destroy();
            var saida = _graph.GetOutputCount() > 0 ? (AnimationPlayableOutput)_graph.GetOutput(0) : AnimationPlayableOutput.Create(_graph, "saida", anim);
            _play = AnimationClipPlayable.Create(_graph, clip);
            _play.SetApplyFootIK(false);
            saida.SetSourcePlayable(_play);
            _graph.Play();
            _clipAtual = clip;
            _tEstado = 0f;
        }

        static void Parar()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _estado = Estado.Inativo;
            _nivel = 0;
            Brilho.Definir(null);
        }

        public static void Tick(Player p)
        {
            if (p == null || p.IsDead()) { if (Ativa) Parar(); return; }
            float dt = Time.deltaTime;
            bool segurando = BotaoSegurado();

            if (_estado == Estado.Inativo)
            {
                if (segurando && ComFoice(p) && !p.InAttack() && p.HaveStamina(5f) && AnimacoesCustom.Clipe(CfgClipGiro.Value) != null)
                {
                    _estado = Estado.Iniciando; _nivel = 0; _tNivel = 0f; _tGiro = 0f;
                    if (AnimacoesCustom.Clipe(CfgClipInicio.Value) != null) Tocar(p, CfgClipInicio.Value);
                    else { _estado = Estado.Carregando; Tocar(p, CfgClipGiro.Value); }
                    Brilho.Definir(Cores[0]);
                }
                return;
            }
            if (!ComFoice(p)) { Parar(); return; }

            _tEstado += dt;
            if (_estado != Estado.Soltando) MoveDir?.SetValue(p, Vector3.zero);

            switch (_estado)
            {
                case Estado.Iniciando:
                case Estado.Carregando:
                case Estado.Transicao:
                    // giro do bastao: dano em volta e gasto de stamina
                    p.UseStamina(CfgStaminaSeg.Value * dt);
                    _tGiro += dt;
                    if (_tGiro >= 0.5f) { _tGiro = 0f; Acertar(p, CfgDanoGiro.Value, 360f, false); }

                    if (!segurando || !p.HaveStamina(1f)) { Soltar(p); break; }

                    if (_clipAtual != null && _tEstado >= _clipAtual.length)
                    {
                        if (_estado == Estado.Carregando) { _play.SetTime(0); _tEstado = 0f; }
                        else { _estado = Estado.Carregando; Tocar(p, CfgClipGiro.Value); }
                    }

                    _tNivel += dt;
                    if (_estado == Estado.Carregando && _nivel < 4 && _tNivel >= CfgTempoNivel.Value)
                    {
                        _tNivel = 0f;
                        _nivel++;
                        Brilho.Definir(Cores[_nivel]);
                        p.Message(MessageHud.MessageType.TopLeft, "Carga " + _nivel);
                    }
                    break;

                case Estado.Soltando:
                    if (!_acertou && _clipAtual != null && _tEstado >= _clipAtual.length * _momentoGolpe)
                    {
                        _acertou = true;
                        Acertar(p, _multGolpe, _anguloGolpe, true);
                    }
                    if (_clipAtual == null || _tEstado >= _clipAtual.length * 0.95f) Parar();
                    break;
            }
        }

        static void Soltar(Player p)
        {
            _estado = Estado.Soltando;
            _acertou = false;
            p.UseStamina(CfgStaminaGolpe.Value);
            _multGolpe = Mult(_nivel); _anguloGolpe = 140f; _momentoGolpe = 0.42f;
            Avanco.NovoGolpe(true);
            Tocar(p, Golpe(_nivel));
            float h = AnimacoesCustom.Hit(Golpe(_nivel));
            if (h > 0f && _clipAtual != null && h < _clipAtual.length) _momentoGolpe = h / _clipAtual.length;
        }

        public static void VigiarCombo(Player p) { }

        // 3o golpe (bastao no chao): dano em area a frente, junto do acerto normal do Valheim.
        public static void AreaGolpeFinal(Player p)
        {
            if (CfgMultQuarto.Value <= 0f) return;
            if (AnimacoesCustom.ClipeAtual(p, out var nome, out _) && nome == CfgClipQuarto.Value)
                Acertar(p, CfgMultQuarto.Value, 200f, true);
        }

        static void Acertar(Player p, float mult, float angulo, bool forte)
        {
            var w = p.GetCurrentWeapon();
            if (w == null) return;
            var origem = p.transform.position;
            bool acertou = false;
            foreach (var c in Character.GetAllCharacters().ToList())
            {
                if (c == null || c == p || c.IsDead()) continue;
                var d = c.transform.position - origem; d.y = 0;
                if (d.magnitude > CfgRaio.Value + c.GetRadius()) continue;
                if (angulo < 360f && Vector3.Angle(p.transform.forward, d) > angulo * 0.5f) continue;
                if (!BaseAI.IsEnemy(p, c)) continue;

                var hit = new HitData();
                hit.m_damage = w.GetDamage();
                hit.m_damage.Modify(mult * p.GetRandomSkillFactor(w.m_shared.m_skillType));
                hit.m_point = c.GetCenterPoint();
                hit.m_dir = d.normalized;
                hit.m_pushForce = w.m_shared.m_attackForce * (forte ? mult : 0.3f);
                hit.m_skill = w.m_shared.m_skillType;
                hit.SetAttacker(p);
                c.Damage(hit);
                if (w.m_shared.m_attackStatusEffect != null && Random.value < CeifadoraPlugin.CfgBleedChance.Value)
                    c.GetSEMan()?.AddStatusEffect(w.m_shared.m_attackStatusEffect.NameHash(), true);
                acertou = true;
            }
            if (acertou) p.RaiseSkill(w.m_shared.m_skillType, forte ? 1.5f : 0.3f);
        }
    }

    /// Bloqueia o secundario vanilla da Ceifadora (o especial e o sistema de carga)
    /// e os ataques normais enquanto carrega.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class Patch_StartAttack_Carga
    {
        [HarmonyPrefix]
        public static bool Prefix(Humanoid __instance, bool secondaryAttack, ref bool __result)
        {
            if (!(__instance is Player p) || p != Player.m_localPlayer) return true;
            var w = p.GetCurrentWeapon();
            if (w?.m_dropPrefab == null || w.m_dropPrefab.name != CeifadoraPlugin.ItemPrefabName) return true;
            if (!AnimacoesCustom.TemPacote) return true; // sem pacote: comportamento antigo
            if (secondaryAttack || Carga.Ativa) { __result = false; return false; }
            return true;
        }

        // Golpe normal comecou de verdade: zera o limite de deslocamento e registra o nivel do combo.
        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            if (!__result || secondaryAttack || !(__instance is Player p) || p != Player.m_localPlayer) return;
            if (!Avanco.ComArma(p)) return;
            Avanco.NovoGolpe(false);
            var atk = Traverse.Create(p).Field("m_currentAttack").GetValue<Attack>();
            int nivel = atk != null ? Traverse.Create(atk).Field("m_currentAttackCainLevel").GetValue<int>() : -1;
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] golpe normal, nivel do combo " + nivel);
        }
    }

    /// Brilho da foice (emissao) conforme o nivel de carga.
    internal static class Brilho
    {
        static readonly Dictionary<Material, Color> _base = new Dictionary<Material, Color>();

        public static void Definir(Color? cor)
        {
            foreach (var t in Empunhadura.Instancias())
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.materials)
                    {
                        if (!m.HasProperty("_EmissionColor")) continue;
                        if (!_base.ContainsKey(m)) _base[m] = m.GetColor("_EmissionColor");
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", cor ?? _base[m]);
                    }
        }
    }
}

namespace CeifadoraObsidiana
{
    [HarmonyLib.HarmonyPatch]
    public static class Patch_DoMeleeAttack_Area
    {
        static System.Reflection.MethodBase TargetMethod() => HarmonyLib.AccessTools.Method(typeof(Attack), "DoMeleeAttack");
        static bool Prepare() => TargetMethod() != null;

        [HarmonyLib.HarmonyPostfix]
        public static void Postfix(Attack __instance)
        {
            var p = HarmonyLib.Traverse.Create(__instance).Field("m_character").GetValue<Humanoid>() as Player;
            if (p == null || p != Player.m_localPlayer || !Avanco.ComArma(p)) return;
            Carga.AreaGolpeFinal(p);
        }
    }
}
