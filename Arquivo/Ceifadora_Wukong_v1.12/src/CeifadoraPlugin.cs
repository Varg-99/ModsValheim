// Ceifadora Obsidiana - mod de foice para Valheim (BepInEx + Jotunn)
// Inspirada no estilo "reaper" do Elden Ring, com design próprio (não é cópia do modelo).
//
// Ataque normal  : combo de 3 varridas horizontais largas (animação "battleaxe_attack").
// Ataque especial: passo para trás + giro de 360° (animação "atgeir_secondary"),
//                  parecido com o recuo + contra-ataque da arte de arma da Obsidian Lamina.
// Efeito         : chance de aplicar "Sangramento" (dano ao longo do tempo).

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Reflection;
using UnityEngine;

namespace CeifadoraObsidiana
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(EldenheimCompat.EldenheimGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class CeifadoraPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "fabiano.ceifadoraobsidiana";
        public const string PluginName = "CeifadoraObsidiana";
        public const string PluginVersion = "1.12.0";

        public const string ItemPrefabName = "CeifadoraObsidiana";
        public const string BleedSEName = "SE_SangramentoObsidiana";

        // Prefabs vanilla usados como base
        private const string BaseVisualPrefab = "Scythe";          // foice de colheita (modelo)
        private const string PrimaryAttackSource = "Battleaxe";    // varridas horizontais
        private const string SecondaryAttackSource = "AtgeirBlackmetal"; // giro 360°

        public static ConfigEntry<float> CfgSlash;
        public static ConfigEntry<float> CfgSlashPerLevel;
        public static ConfigEntry<float> CfgBleedChance;
        public static ConfigEntry<float> CfgBleedDamagePerTick;
        public static ConfigEntry<float> CfgBleedDuration;
        public static ConfigEntry<float> CfgBackstepForce;
        public static ConfigEntry<float> CfgSpecialMultiplier;
        public static ConfigEntry<bool> CfgBastao;
        public static ConfigEntry<float> CfgComprimentoBastao;

        private Harmony _harmony;
        public static CeifadoraPlugin Instance;

        private void Awake()
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            CfgSlash = Config.Bind("Arma", "DanoCorte", 110f,
                new ConfigDescription("Dano de corte base (nível 1).", null, admin));
            CfgSlashPerLevel = Config.Bind("Arma", "DanoCortePorNivel", 6f,
                new ConfigDescription("Dano de corte extra por nível de melhoria.", null, admin));
            CfgSpecialMultiplier = Config.Bind("Arma", "MultiplicadorEspecial", 1.5f,
                new ConfigDescription("Multiplicador de dano do ataque especial.", null, admin));
            CfgBackstepForce = Config.Bind("Arma", "ForcaRecuo", 12f,
                new ConfigDescription("Força do passo para trás no início do especial (0 desliga).", null, admin));

            CfgBleedChance = Config.Bind("Sangramento", "Chance", 0.35f,
                new ConfigDescription("Chance (0 a 1) de aplicar sangramento por acerto.",
                    new AcceptableValueRange<float>(0f, 1f), admin));
            CfgBleedDamagePerTick = Config.Bind("Sangramento", "DanoPorSegundo", 6f,
                new ConfigDescription("Dano verdadeiro por segundo do sangramento.", null, admin));
            CfgBleedDuration = Config.Bind("Sangramento", "Duracao", 6f,
                new ConfigDescription("Duração do sangramento em segundos.", null, admin));

            CfgBastao = Config.Bind("Visual", "BastaoDeMadeira", true,
                "Troca o modelo da foice por um bastao simples de madeira (para conferir as animacoes do Wukong). Precisa reiniciar o jogo.");
            CfgComprimentoBastao = Config.Bind("Visual", "ComprimentoBastao", 0f,
                "Comprimento do bastao em metros (0 = mesmo tamanho da haste da atgeir de metal negro). Precisa reiniciar o jogo.");

            Instance = this;
            Empunhadura.Init(Config);
            Bastao.Init(Config);
            Fotos.Init(Config);
            Carga.Init(Config);
            TrilhaArma.Init(Config, System.IO.Path.GetDirectoryName(Info.Location));
            AnimacoesCustom.Init(Config, System.IO.Path.GetDirectoryName(Info.Location));
            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += AddContent;

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            EldenheimCompat.TryPatch(_harmony);
            Avanco.Init(Config, _harmony);
        }

        private void AddLocalizations()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("Portuguese_Brazilian", new System.Collections.Generic.Dictionary<string, string>
            {
                { "item_ceifadora", "Ceifadora Obsidiana" },
                { "item_ceifadora_desc", "Uma foice longa de lâmina negra como vidro vulcânico. Cada corte deixa uma trilha vermelha." },
                { "se_sangramento", "Sangramento" },
                { "se_sangramento_start", "Você está sangrando" },
            });
            loc.AddTranslation("English", new System.Collections.Generic.Dictionary<string, string>
            {
                { "item_ceifadora", "Obsidian Reaper" },
                { "item_ceifadora_desc", "A long scythe with a blade black as volcanic glass. Every cut leaves a red trail." },
                { "se_sangramento", "Bleeding" },
                { "se_sangramento_start", "You are bleeding" },
            });
        }

        private void AddContent()
        {
            try
            {
                var bleed = CreateBleedEffect();
                CreateWeapon(bleed);
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"[{PluginName}] Falha ao criar a arma: {e}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= AddContent;
            }
        }

        private SE_Sangramento CreateBleedEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_Sangramento>();
            se.name = BleedSEName;
            se.m_name = "$se_sangramento";
            se.m_startMessageType = MessageHud.MessageType.TopLeft;
            se.m_startMessage = "$se_sangramento_start";
            se.m_ttl = CfgBleedDuration.Value;
            se.m_damagePerTick = CfgBleedDamagePerTick.Value;

            // Reaproveita o ícone do veneno vanilla
            var poison = PrefabManager.Cache.GetPrefab<StatusEffect>("Poison");
            if (poison != null) se.m_icon = poison.m_icon;

            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            return se;
        }

        private void CreateWeapon(StatusEffect bleed)
        {
            var config = new ItemConfig
            {
                Name = "$item_ceifadora",
                Description = "$item_ceifadora_desc",
                CraftingStation = CraftingStations.Forge,
                MinStationLevel = 4,
                RepairStation = CraftingStations.Forge,
                Requirements = new[]
                {
                    new RequirementConfig("BlackMetal", 30, 15),
                    new RequirementConfig("Obsidian", 20, 10),
                    new RequirementConfig("Bloodbag", 10, 5),
                    new RequirementConfig("ElderBark", 10, 0),
                    new RequirementConfig("LinenThread", 5, 2),
                }
            };

            var item = new CustomItem(ItemPrefabName, BaseVisualPrefab, config);
            var shared = item.ItemDrop.m_itemData.m_shared;

            var primaryDrop = PrefabManager.Instance.GetPrefab(PrimaryAttackSource)?.GetComponent<ItemDrop>();
            var secondaryDrop = PrefabManager.Instance.GetPrefab(SecondaryAttackSource)?.GetComponent<ItemDrop>();
            if (primaryDrop == null || secondaryDrop == null)
            {
                Jotunn.Logger.LogError($"[{PluginName}] Não achei {PrimaryAttackSource} ou {SecondaryAttackSource}.");
                return;
            }
            var pShared = primaryDrop.m_itemData.m_shared;
            var sShared = secondaryDrop.m_itemData.m_shared;

            // --- Tipo / postura (segura como o machado de batalha) ---
            shared.m_itemType = ItemDrop.ItemData.ItemType.TwoHandedWeapon;
            shared.m_animationState = sShared.m_animationState; // pegada de alabarda (duas maos)
            shared.m_skillType = Skills.SkillType.Polearms;
            shared.m_maxQuality = 4;

            // --- Dano ---
            shared.m_damages = new HitData.DamageTypes { m_slash = CfgSlash.Value };
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_slash = CfgSlashPerLevel.Value };
            shared.m_attackForce = 70f;
            shared.m_backstabBonus = 3f;
            shared.m_blockPower = 45f;
            shared.m_blockPowerPerLevel = 0f;
            shared.m_deflectionForce = 40f;
            shared.m_timedBlockBonus = 2f;
            shared.m_maxDurability = 200f;
            shared.m_durabilityPerLevel = 50f;
            shared.m_weight = 3.5f;
            shared.m_movementModifier = -0.05f;

            // --- Set 1 (normal): 3 varridas horizontais largas ---
            shared.m_attack = sShared.m_attack.Clone(); // combo da alabarda; os clipes viram os do Wukong
            shared.m_attack.m_attackRange = 3.0f;   // foice é mais longa que o machado
            shared.m_attack.m_attackAngle = 120f;   // arco mais largo
            shared.m_attack.m_attackStamina = 16f;

            // --- Set 2 (especial): recuo + giro 360° ---
            shared.m_secondaryAttack = sShared.m_secondaryAttack.Clone();
            shared.m_secondaryAttack.m_attackRange = 3.2f;
            shared.m_secondaryAttack.m_damageMultiplier = CfgSpecialMultiplier.Value;
            shared.m_secondaryAttack.m_attackStamina = 28f;

            // --- Sangramento ---
            shared.m_attackStatusEffect = bleed;
            // Campo existe nas versões recentes; usa Traverse para não quebrar se mudar.
            var chance = Traverse.Create(shared).Field("m_attackStatusEffectChance");
            if (chance.FieldExists()) chance.SetValue(CfgBleedChance.Value);

            TintObsidian(item.ItemPrefab);
            if (CfgBastao.Value) Bastao.Trocar(item.ItemPrefab, secondaryDrop.gameObject, CfgComprimentoBastao.Value);
            ItemManager.Instance.AddItem(item);
            Jotunn.Logger.LogInfo($"[{PluginName}] Ceifadora Obsidiana adicionada.");
        }

        /// Deixa o modelo preto vítreo com um brilho vermelho no fio.
        private static void TintObsidian(GameObject prefab)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]) { name = mats[i].name + "_obsidiana" };
                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.07f, 0.06f, 0.08f));
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.9f);
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(0.12f, 0.0f, 0.0f));
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }

    /// Recuo no início do ataque especial.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class Patch_StartAttack_Backstep
    {
        private static readonly FieldInfo PushForceField = AccessTools.Field(typeof(Character), "m_pushForce");

        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            if (!__result || !secondaryAttack || EldenheimCompat.Active) return;
            if (CeifadoraPlugin.CfgBackstepForce.Value <= 0f) return;
            if (!(__instance is Player player) || player != Player.m_localPlayer) return;

            var weapon = player.GetCurrentWeapon();
            if (weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != CeifadoraPlugin.ItemPrefabName) return;

            Vector3 back = -player.transform.forward * CeifadoraPlugin.CfgBackstepForce.Value;

            if (PushForceField != null)
            {
                var current = (Vector3)PushForceField.GetValue(player);
                PushForceField.SetValue(player, current + back);
            }
            else
            {
                var body = player.GetComponent<Rigidbody>();
                if (body != null) body.AddForce(back, ForceMode.VelocityChange);
            }
        }
    }
}
