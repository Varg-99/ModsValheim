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

    /// Mao e costas: VisEquipment.AttachItem(itemHash, variant, joint, ...) devolve o objeto criado.
    [HarmonyPatch]
    static class Estilo_NaMao
    {
        static MethodBase _m; static int _iv = -1;
        static bool Prepare()
        {
            _m = AccessTools.GetDeclaredMethods(typeof(VisEquipment)).FirstOrDefault(m => m.Name == "AttachItem" && m.ReturnType == typeof(GameObject));
            _iv = _m != null ? Estilos.Indice(_m, "variant") : -1;
            if (_iv < 0) Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Estilos: VisEquipment.AttachItem nao encontrado; estilo na mao desligado.");
            return _iv >= 0;
        }
        static MethodBase TargetMethod() => _m;
        static void Postfix(GameObject __result, object[] __args)
        {
            if (__result != null && __args[_iv] is int v) Estilos.Aplicar(__result, v);
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
