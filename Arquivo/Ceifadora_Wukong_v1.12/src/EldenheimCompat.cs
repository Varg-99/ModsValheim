// Integração opcional com o Eldenheim (github.com/Gargantua1866/Eldenheim).
// Se o Eldenheim estiver instalado, a Ceifadora Obsidiana usa o moveset
// "Reaper" (foice) do Elden Ring, com as animações e o frame data gerados
// pelo próprio jogador a partir da cópia dele do Elden Ring.
// Sem o Eldenheim, nada disso roda e a arma usa as animações vanilla.

using BepInEx.Bootstrap;
using HarmonyLib;
using System.Reflection;

namespace CeifadoraObsidiana
{
    internal static class EldenheimCompat
    {
        public const string EldenheimGuid = "com.gargantua.eldenheim";
        public const string ErClass = "Reaper";

        public static bool Active { get; private set; }

        public static void TryPatch(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(EldenheimGuid)) return;

            // Eldenheim.Controller.ClassOf(ItemData item, bool twoHanded, bool dual) -> string
            var type = AccessTools.TypeByName("Eldenheim.Controller");
            var classOf = type == null ? null : AccessTools.Method(type, "ClassOf",
                new[] { typeof(ItemDrop.ItemData), typeof(bool), typeof(bool) });
            if (classOf == null)
            {
                Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Eldenheim encontrado, mas ClassOf mudou. Integração desligada.");
                return;
            }

            harmony.Patch(classOf, postfix: new HarmonyMethod(typeof(EldenheimCompat).GetMethod(nameof(ClassOfPostfix),
                BindingFlags.Static | BindingFlags.NonPublic)));
            Active = true;
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Eldenheim detectado: a Ceifadora usa o moveset Reaper.");
        }

        private static void ClassOfPostfix(ItemDrop.ItemData item, ref string __result)
        {
            if (item?.m_dropPrefab != null && item.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName)
                __result = ErClass;
        }
    }
}
