// Estilos (como os escudos): avisa o modelo da foice qual estilo foi escolhido na bancada.
// O jogo faz isso com uma interface interna (IEquipmentVisual) que o mod nao consegue usar,
// entao aqui pegamos o mesmo momento: quando a arma e posta na mao/costas e quando vai para um suporte.

using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Estilos
    {
        public static void Aplicar(GameObject go, int estilo)
        {
            if (go == null) return;
            foreach (var e in go.GetComponentsInChildren<EstiloFoice>(true)) e.Setup(estilo);
        }

        public static int Indice(MethodBase m, string nome)
        {
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++) if (ps[i].Name == nome && ps[i].ParameterType == typeof(int)) return i;
            return -1;
        }
    }

    /// Mao e costas: o jogo so guarda o estilo do item da mao ESQUERDA (escudos). Arma de duas maos vai na
    /// direita, sem estilo. Entao o dono do personagem grava o estilo no proprio personagem (vale tambem no
    /// multiplayer) e o modelo da foice le de la (EstiloFoice.LateUpdate).
    [HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
    static class Estilo_NoPersonagem
    {
        public static readonly int HMao = "ceifadora_estilo_mao".GetStableHashCode();
        public static readonly int HCostas = "ceifadora_estilo_costas".GetStableHashCode();
        static readonly FieldInfo _dir = AccessTools.Field(typeof(Humanoid), "m_rightItem");
        static readonly FieldInfo _escDir = AccessTools.Field(typeof(Humanoid), "m_hiddenRightItem");
        static readonly FieldInfo _nview = AccessTools.Field(typeof(Character), "m_nview");

        static void Postfix(Humanoid __instance)
        {
            var nv = _nview?.GetValue(__instance) as ZNetView;
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            var zdo = nv.GetZDO();
            int mao = (_dir?.GetValue(__instance) as ItemDrop.ItemData)?.m_variant ?? 0;
            int costas = (_escDir?.GetValue(__instance) as ItemDrop.ItemData)?.m_variant ?? 0;
            if (zdo.GetInt(HMao, 0) != mao) zdo.Set(HMao, mao);
            if (zdo.GetInt(HCostas, 0) != costas) zdo.Set(HCostas, costas);
        }
    }

    /// Suportes de parede/chao: ItemStand.SetVisualItem(itemName, variant, ...) cria m_visualItem.
    [HarmonyPatch]
    static class Estilo_NoSuporte
    {
        static MethodBase _m; static int _iv = -1;
        static readonly FieldInfo _vis = AccessTools.Field(typeof(ItemStand), "m_visualItem");
        static bool Prepare()
        {
            _m = AccessTools.GetDeclaredMethods(typeof(ItemStand)).FirstOrDefault(m => m.Name == "SetVisualItem");
            _iv = _m != null ? Estilos.Indice(_m, "variant") : -1;
            if (_iv < 0 || _vis == null) Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Estilos: ItemStand.SetVisualItem nao encontrado; estilo no suporte desligado.");
            return _iv >= 0 && _vis != null;
        }
        static MethodBase TargetMethod() => _m;
        static void Postfix(ItemStand __instance, object[] __args)
        {
            if (__args[_iv] is int v) Estilos.Aplicar(_vis.GetValue(__instance) as GameObject, v);
        }
    }
}
