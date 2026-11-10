// Ceifadora Obsidiana - foice para Valheim (BepInEx + Jotunn)
// Versao 2 (simples): usa so os golpes que ja existem no Valheim.
//   Ataque normal : golpes de uma arma do jogo (padrao: machado de batalha), mais rapidos;
//                   o ultimo vira um giro em volta do personagem (Golpes.cs).
//   Especial      : golpe secundario de outra arma (padrao: giro 360 da atgeir).
//   Empunhadura   : postura de uma terceira arma (padrao: atgeir); a foice (modelo
//                   proprio, ModeloFoice.cs) e encaixada nas duas maos (AjusteFoice.cs).
//   Efeito        : chance de Sangramento.
// A versao com as animacoes do Wukong esta guardada em ModsValheim\Arquivo.

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CeifadoraObsidiana
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class CeifadoraPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "fabiano.ceifadoraobsidiana";
        public const string PluginName = "CeifadoraObsidiana";
        public const string PluginVersion = "2.4.0";

        public const string ItemPrefabName = "CeifadoraObsidiana";
        public const string BleedSEName = "SE_SangramentoObsidiana";
        private const string BaseVisualPrefab = "Scythe"; // modelo da foice do jogo

        public static ConfigEntry<float> CfgBrilho;
        public static ConfigEntry<bool> CfgRastro;
        public static ConfigEntry<float> CfgSlash, CfgSlashPerLevel, CfgSpecialMultiplier;
        public static ConfigEntry<float> CfgBleedChance, CfgBleedDamagePerTick, CfgBleedDuration;
        public static ConfigEntry<string> CfgArmaNormal, CfgArmaEspecial, CfgArmaEmpunhadura, CfgEfeitoSangue;

        private FileSystemWatcher _watcher;
        private volatile bool _recarregar;

        private void Awake()
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            CfgArmaNormal = Config.Bind("Golpes", "ArmaDoAtaqueNormal", "Battleaxe",
                "De qual arma do jogo vem o ataque normal. Exemplos: Battleaxe (machado de batalha, 3 varridas), " +
                "AtgeirBlackmetal (atgeir, estocadas), SledgeIron (marreta), SwordIron (espada). Precisa reiniciar o jogo.");
            CfgArmaEspecial = Config.Bind("Golpes", "ArmaDoEspecial", "AtgeirBlackmetal",
                "De qual arma do jogo vem o ataque especial (botao do meio). AtgeirBlackmetal = giro de 360 graus; " +
                "Battleaxe = golpe que derruba. Precisa reiniciar o jogo.");
            CfgArmaEmpunhadura = Config.Bind("Golpes", "ArmaDaEmpunhadura", "AtgeirBlackmetal",
                "Como o personagem segura a foice parado e andando (postura de qual arma do jogo). " +
                "AtgeirBlackmetal = como atgeir; Battleaxe = como machado; Scythe = a pegada original da foice. Precisa reiniciar o jogo.");

            CfgEfeitoSangue = Config.Bind("Visual", "EfeitoDeSangue", "auto",
                "Efeito extra quando a foice acerta: auto = respingo de sangue do jogo; vazio = nenhum; ou o nome de um efeito (vfx_...). Precisa reiniciar.");

            CfgBrilho = Config.Bind("Visual", "BrilhoDaFoice", 0.35f,
                "Brilho proprio da textura da foice (0 = nenhum). Precisa reiniciar.");

            CfgRastro = Config.Bind("Visual", "LinhaNoFio", true, "Linha fina que segue o fio da lamina durante o golpe. Precisa reiniciar.");

            CfgSlash = Config.Bind("Arma", "DanoCorte", 110f,
                new ConfigDescription("Dano de corte base (nivel 1).", null, admin));
            CfgSlashPerLevel = Config.Bind("Arma", "DanoCortePorNivel", 6f,
                new ConfigDescription("Dano de corte extra por nivel de melhoria.", null, admin));
            CfgSpecialMultiplier = Config.Bind("Arma", "MultiplicadorEspecial", 1.5f,
                new ConfigDescription("Multiplicador de dano do ataque especial.", null, admin));

            CfgBleedChance = Config.Bind("Sangramento", "Chance", 0.35f,
                new ConfigDescription("Chance (0 a 1) de aplicar sangramento por acerto.",
                    new AcceptableValueRange<float>(0f, 1f), admin));
            CfgBleedDamagePerTick = Config.Bind("Sangramento", "DanoPorSegundo", 6f,
                new ConfigDescription("Dano por segundo do sangramento.", null, admin));
            CfgBleedDuration = Config.Bind("Sangramento", "Duracao", 6f,
                new ConfigDescription("Duracao do sangramento em segundos.", null, admin));

            AjusteFoice.Init(Config);
            Golpes.Init(Config);
            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += AddContent;
            new Harmony(PluginGUID).PatchAll(Assembly.GetExecutingAssembly());

            // Recarrega o .cfg quando ele e salvo (ajustes da foice na mao valem na hora).
            try
            {
                _watcher = new FileSystemWatcher(Path.GetDirectoryName(Config.ConfigFilePath), Path.GetFileName(Config.ConfigFilePath));
                _watcher.Changed += (s, e) => _recarregar = true;
                _watcher.NotifyFilter = NotifyFilters.LastWrite;
                _watcher.EnableRaisingEvents = true;
            }
            catch (System.Exception e) { Logger.LogWarning("Sem recarga automatica do .cfg: " + e.Message); }
        }

        private void Update()
        {
            if (!_recarregar) return;
            _recarregar = false;
            try { Config.Reload(); } catch { }
        }

        private void AddLocalizations()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("Portuguese_Brazilian", new System.Collections.Generic.Dictionary<string, string>
            {
                { "item_ceifadora", "Ceifadora Obsidiana" },
                { "item_ceifadora_desc", "Uma foice longa de lamina negra como vidro vulcanico. Cada corte deixa uma trilha vermelha." },
                { "se_sangramento", "Sangramento" },
                { "se_sangramento_start", "Voce esta sangrando" },
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
            try { CreateWeapon(CreateBleedEffect()); }
            catch (System.Exception e) { Jotunn.Logger.LogError($"[{PluginName}] Falha ao criar a arma: {e}"); }
            finally { PrefabManager.OnVanillaPrefabsAvailable -= AddContent; }
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
            var poison = PrefabManager.Cache.GetPrefab<StatusEffect>("Poison");
            if (poison != null) se.m_icon = poison.m_icon;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            return se;
        }

        private static ItemDrop Fonte(string nome, string padrao)
        {
            var d = PrefabManager.Instance.GetPrefab(nome)?.GetComponent<ItemDrop>();
            if (d != null) return d;
            Jotunn.Logger.LogWarning($"[{PluginName}] Arma '{nome}' nao existe no jogo; usando '{padrao}'.");
            return PrefabManager.Instance.GetPrefab(padrao)?.GetComponent<ItemDrop>();
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

            var normal = Fonte(CfgArmaNormal.Value, "Battleaxe");
            var especial = Fonte(CfgArmaEspecial.Value, "AtgeirBlackmetal");
            var pegada = Fonte(CfgArmaEmpunhadura.Value, "AtgeirBlackmetal");
            if (normal == null || especial == null || pegada == null)
            {
                Jotunn.Logger.LogError($"[{PluginName}] Armas de origem nao encontradas.");
                return;
            }

            shared.m_itemType = ItemDrop.ItemData.ItemType.TwoHandedWeapon;
            shared.m_animationState = pegada.m_itemData.m_shared.m_animationState;
            shared.m_skillType = Skills.SkillType.Polearms;
            shared.m_maxQuality = 4;

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

            // Golpes do proprio jogo, sem mudar o ritmo nem o movimento.
            shared.m_attack = normal.m_itemData.m_shared.m_attack.Clone();
            shared.m_secondaryAttack = especial.m_itemData.m_shared.m_secondaryAttack.Clone();
            shared.m_secondaryAttack.m_damageMultiplier = CfgSpecialMultiplier.Value;

            shared.m_attackStatusEffect = bleed;
            var chance = Traverse.Create(shared).Field("m_attackStatusEffectChance");
            if (chance.FieldExists()) chance.SetValue(CfgBleedChance.Value);

            var pasta = Path.Combine(Path.GetDirectoryName(Info.Location), "modelo");
            var modelo = ModeloFoice.Trocar(item.ItemPrefab, pasta);
            if (modelo != null)
            {
                AjusteFoice.Alinhar(modelo, pegada.gameObject, false);
                // Nas costas no mesmo lugar da atgeir.
                var atgeir = PrefabManager.Instance.GetPrefab("AtgeirBlackmetal") ?? pegada.gameObject;
                var ov = Traverse.Create(atgeir.GetComponent<ItemDrop>().m_itemData.m_shared).Field("m_attachOverride");
                if (ov.FieldExists()) Traverse.Create(shared).Field("m_attachOverride").SetValue(ov.GetValue());
                ModeloFoice.CriarCostas(item.ItemPrefab, atgeir);
                if (ModeloFoice.ModeloCostas != null) AjusteFoice.Alinhar(ModeloFoice.ModeloCostas, atgeir, true);
            }
            else TintObsidian(item.ItemPrefab);

            AdicionarSangue(shared);
            ItemManager.Instance.AddItem(item);
            Jotunn.Logger.LogInfo($"[{PluginName}] Ceifadora adicionada: normal={normal.name}, especial={especial.name}, empunhadura={pegada.name}.");
        }

        /// Respingo de sangue (efeito do proprio jogo) quando a foice acerta.
        private static void AdicionarSangue(ItemDrop.ItemData.SharedData shared)
        {
            string nome = (CfgEfeitoSangue.Value ?? "").Trim();
            if (nome.Length == 0) return;
            GameObject fx = null;
            var todos = PrefabManager.Cache.GetPrefabs(typeof(GameObject));
            var sangue = todos.Keys.Where(k => k.StartsWith("vfx_", System.StringComparison.OrdinalIgnoreCase)
                                               && k.IndexOf("blood", System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(k => k).ToList();
            Jotunn.Logger.LogInfo($"[{PluginName}] Efeitos de sangue do jogo: {string.Join(", ", sangue)}");
            if (nome != "auto") fx = PrefabManager.Instance.GetPrefab(nome);
            else
            {
                string escolhido = new[] { "vfx_blood_hit", "vfx_BloodHit", "vfx_bloodhit" }.FirstOrDefault(n => todos.ContainsKey(n))
                                   ?? sangue.FirstOrDefault(k => k.IndexOf("hit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                   ?? sangue.FirstOrDefault();
                if (escolhido != null) fx = todos[escolhido] as GameObject;
            }
            if (fx == null) { Jotunn.Logger.LogWarning($"[{PluginName}] Efeito de sangue nao encontrado."); return; }
            if (shared.m_hitEffect == null) shared.m_hitEffect = new EffectList();
            var lista = (shared.m_hitEffect.m_effectPrefabs ?? new EffectList.EffectData[0]).ToList();
            lista.Add(new EffectList.EffectData { m_prefab = fx, m_enabled = true });
            shared.m_hitEffect.m_effectPrefabs = lista.ToArray();
            Jotunn.Logger.LogInfo($"[{PluginName}] Efeito de sangue no acerto: {fx.name}");
        }

        /// Deixa o modelo preto vitreo com um brilho vermelho.
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
                        m.SetColor("_EmissionColor", new Color(0.12f, 0f, 0f));
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
