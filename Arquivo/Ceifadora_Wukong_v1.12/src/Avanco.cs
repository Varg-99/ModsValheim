// Dosa o deslocamento (root motion) das animacoes do Wukong:
// - golpes normais: so um pouco (o Viking nao fica 100% parado);
// - golpe carregado (especial): o deslocamento da animacao.
// Cada golpe tem um limite em metros.

using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Avanco
    {
        public static ConfigEntry<float> CfgEscalaNormal, CfgLimiteNormal, CfgEscalaEspecial, CfgLimiteEspecial;

        public static void Init(ConfigFile config, Harmony harmony)
        {
            CfgEscalaNormal = config.Bind("Movimento", "EscalaGolpeNormal", 0.2f,
                "Quanto o Viking acompanha o deslocamento da animacao nos golpes normais (0 = parado, 1 = como no Wukong).");
            CfgLimiteNormal = config.Bind("Movimento", "PassoPorGolpe", 0.25f,
                "Deslocamento maximo por golpe normal (metros).");
            CfgEscalaEspecial = config.Bind("Movimento", "EscalaEspecial", 1f,
                "Quanto o Viking acompanha o deslocamento da animacao no golpe carregado.");
            CfgLimiteEspecial = config.Bind("Movimento", "LimiteEspecial", 8f,
                "Deslocamento maximo do golpe carregado (metros).");
        }

        static float _maxGolpe, _acumulado, _escala, _ultimo;

        // Chamado no inicio de cada golpe: zera o acumulado e define limite e escala.
        public static void NovoGolpe(bool especial)
        {
            _ultimo = Time.time;
            _acumulado = 0f;
            _escala = especial ? CfgEscalaEspecial.Value : CfgEscalaNormal.Value;
            _maxGolpe = especial ? CfgLimiteEspecial.Value : CfgLimiteNormal.Value;
        }

        public static Vector3 Dosar(Vector3 d)
        {
            d.y = 0f;
            d *= _escala;
            float resta = Mathf.Max(0f, _maxGolpe - _acumulado);
            if (d.magnitude > resta) d = d.normalized * resta;
            _acumulado += d.magnitude;
            return d;
        }

        public static bool ComArma(Player p)
        {
            var w = p.GetCurrentWeapon();
            return w?.m_dropPrefab != null && w.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName;
        }

        public static void Garantir(Player p)
        {
            var anim = p.GetComponentInChildren<Animator>();
            if (anim == null || anim.GetComponent<CeifadoraRootMotion>() != null) return;
            var c = anim.gameObject.AddComponent<CeifadoraRootMotion>();
            c.Player = p;
            c.Anim = anim;
            c.Body = p.GetComponent<Rigidbody>();
        }
    }

    [HarmonyPatch]
    public static class Patch_AddRootMotion
    {
        static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Character), "AddRootMotion");
        static bool Prepare() { bool ok = TargetMethod() != null; if (!ok) Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Character.AddRootMotion nao encontrado."); return ok; }

        // Nos golpes com animacao do Wukong o Valheim nao move o corpo; o CeifadoraRootMotion move, dosado.
        [HarmonyPrefix]
        public static bool Prefix(Character __instance)
        {
            if (!(__instance is Player p) || p != Player.m_localPlayer) return true;
            return !(Avanco.ComArma(p) && AnimacoesCustom.Ativo && p.InAttack());
        }
    }

    // Move o corpo nos golpes da arma, com o deslocamento dosado (Avanco.Dosar).
    public class CeifadoraRootMotion : MonoBehaviour
    {
        public Player Player;
        public Animator Anim;
        public Rigidbody Body;
        public static Vector3 UltimoDelta;

        private void OnAnimatorMove()
        {
            if (Player == null || Anim == null || Body == null) return;
            if (Player != Player.m_localPlayer || !Avanco.ComArma(Player)) return;
            bool golpe = Carga.Soltando || (Player.InAttack() && AnimacoesCustom.Ativo && !Carga.Ativa);
            if (!golpe) return;
            var d = Avanco.Dosar(Anim.deltaPosition);
            UltimoDelta = d;
            if (d.sqrMagnitude > 0f) Body.MovePosition(Body.position + d);
        }
    }
}
