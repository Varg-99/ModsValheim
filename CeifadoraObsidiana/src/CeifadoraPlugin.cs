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
        public const string PluginVersion = "3.3.2";

        public const string ItemPrefabName = "CeifadoraObsidiana";
        public const string BleedSEName = "SE_SangramentoObsidiana";
        private const string BaseVisualPrefab = "Scythe"; // modelo da foice do jogo

        public static ConfigEntry<float> CfgBrilho;
        public static ConfigEntry<bool> CfgRastro, CfgRastroLamina, CfgPenas;
        public static ConfigEntry<float> CfgPenasMola;
        public static ConfigEntry<float> CfgRastroTempo, CfgRastroVelMin, CfgRastroArcoMax;
        public static ConfigEntry<bool> CfgRastroArco, CfgRastroMachado;
        public static ConfigEntry<float> CfgRastroMachadoBase, CfgRastroMachadoPonta;
        public static ConfigEntry<string> CfgRastroCor;
        public static Color CorRastro()
        {
            return ColorUtility.TryParseHtmlString((CfgRastroCor.Value ?? "").Trim(), out var c) ? c : new Color(1f, 0.25f, 0.2f, 0.9f);
        }
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
            CfgRastroLamina = Config.Bind("Visual", "RastroDaLamina", true, "Faixa que liga a base a ponta da lamina durante o golpe (mostra o angulo do corte). Substitui a linha fina.");
            CfgPenas = Config.Bind("Visual", "PenasBalancam", true, "Foice de flametal: as penas balancam com o movimento.");
            CfgPenasMola = Config.Bind("Visual", "RigidezDasPenas", 60f, "Quanto maior, mais duras as penas (30 = bem soltas, 120 = firmes).");
            CfgRastroMachado = Config.Bind("Visual", "RastroDoMachado", true, "Usa o rastro do proprio machado de duas maos (o jogo liga e desliga no golpe e alisa a curva). Desliga os outros rastros da foice. Precisa reiniciar.");
            CfgRastroMachadoBase = Config.Bind("Visual", "RastroDoMachadoInicio", 0.2f, "Onde o rastro do machado comeca na lamina: 0 = no alto da haste, 1 = na ponta da lamina. Muda com o jogo aberto.");
            CfgRastroMachadoPonta = Config.Bind("Visual", "RastroDoMachadoFim", 1.0f, "Onde o rastro do machado termina: 1 = na ponta da lamina; mais que 1 estica alem da ponta. Muda com o jogo aberto.");
            CfgRastroArco = Config.Bind("Visual", "RastroEmArcoPerfeito", false, "O rastro e desenhado num arco perfeito (como o do machado), em vez de seguir a lamina ponto a ponto. Muda com o jogo aberto.");
            CfgRastroVelMin = Config.Bind("Visual", "RastroVelocidadeMinima", 5f, "O rastro so aparece quando a ponta da lamina passa dessa velocidade (m/s): a parte rapida do golpe. Maior = rastro mais curto. Muda com o jogo aberto.");
            CfgRastroArcoMax = Config.Bind("Visual", "RastroArcoMaximo", 160f, "Tamanho maximo do rastro em graus de arco. Muda com o jogo aberto.");
            CfgRastroTempo = Config.Bind("Visual", "DuracaoDoRastro2", 0.14f, "Quanto tempo (s) o rastro fica visivel.");
            CfgRastroCor = Config.Bind("Visual", "CorDoRastro2", "#FFFFFF66", "Cor do rastro (RGBA em hexadecimal).");

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
            Golpes.InitModo4(Config);
            UmAcertoPorGolpe.CfgLigado = Config.Bind("Combo4", "UmAcertoPorInimigo", true, "Cada inimigo leva dano no maximo uma vez por golpe (outros inimigos podem ser acertados no mesmo golpe).");
            Impacto.Init(Config);
            Sons.Init(Config);
            Golpe3.CfgClipe = Config.Bind("Golpes", "ClipeDoGolpe3", "auto",
                "Animacao do 3o golpe dentro do pacote ceifadora_golpes. auto = usa a do Mixamo se houver, senao Ceifadora_Golpe3. Precisa reiniciar.");
            Golpe3.CfgAcerto = Config.Bind("Golpes", "AcertoDoGolpe3", 0f,
                "Segundo da animacao do 3o golpe em que o dano acontece (0 = automatico). Ajuste para animacoes do Mixamo.");
            Golpe3.CfgDuasMaos = Config.Bind("Golpes", "DuasMaosNoGolpe3Mixamo", false,
                "Com animacao do Mixamo no 3o golpe: false = segura com uma mao, como na animacao; true = puxa a mao esquerda para o cabo.");
            Golpe3.Init(Path.GetDirectoryName(Info.Location));
            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += AddContent;
            CfgNoTopo = Config.Bind("Arma", "ReceitasNoTopo", true, "As foices aparecem no topo da lista de fabricacao.");
            ItemManager.OnItemsRegistered += ReceitasNoTopo;
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
                { "item_ceifadora_flametal", "Ceifadora de Flametal" },
                { "item_ceifadora_flametal_desc", "Foice de flametal com cabo de madeira de cinzas, osso carbonizado e couro de asksvin. As penas dancam a cada golpe." },
                { "item_ceifadora_ferro", "Ceifadora de Ferro" },
                { "item_ceifadora_ferro_desc", "Uma foice de ferro forjada com casca antiga do pantano. Pesada e confiavel." },
                { "item_ceifadora_metalnegro", "Ceifadora de Metal Negro" },
                { "item_ceifadora_metalnegro_desc", "Uma foice de metal negro, leve e quase inquebravel. A lamina corta como o vento das planicies." },
                { "se_sangramento", "Sangramento" },
                { "se_sangramento_start", "Voce esta sangrando" },
            });
            loc.AddTranslation("English", new System.Collections.Generic.Dictionary<string, string>
            {
                { "item_ceifadora", "Obsidian Reaper" },
                { "item_ceifadora_desc", "A long scythe with a blade black as volcanic glass. Every cut leaves a red trail." },
                { "item_ceifadora_flametal", "Flametal Reaper" },
                { "item_ceifadora_flametal_desc", "A flametal scythe with an ashwood haft, charred bone and asksvin leather. Its feathers dance with every swing." },
                { "item_ceifadora_ferro", "Iron Reaper" },
                { "item_ceifadora_ferro_desc", "An iron scythe forged with ancient bark from the swamp. Heavy and reliable." },
                { "item_ceifadora_metalnegro", "Black Metal Reaper" },
                { "item_ceifadora_metalnegro_desc", "A black metal scythe, light and nearly unbreakable. The blade cuts like the wind of the plains." },
                { "se_sangramento", "Bleeding" },
                { "se_sangramento_start", "You are bleeding" },
            });
        }

        private void AddContent()
        {
            try
            {
                var bleed = CreateBleedEffect();
                foreach (var v in Variantes())
                {
                    try { CreateWeapon(bleed, v); }
                    catch (System.Exception e) { Jotunn.Logger.LogError($"[{PluginName}] Falha ao criar {v.Prefab}: {e}"); }
                }
            }
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

        // Acha o ataque de uma arma do jogo: primeiro pelo nome, depois procurando em todas.
        private static Attack AcharAtaque(System.Func<Attack, bool> filtro, string nome)
        {
            var d = PrefabManager.Instance.GetPrefab(nome)?.GetComponent<ItemDrop>();
            var a = d?.m_itemData?.m_shared?.m_attack;
            if (a != null && filtro(a)) return a;
            foreach (var go in PrefabManager.Cache.GetPrefabs(typeof(GameObject)).Values.OfType<GameObject>())
            {
                var at = go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack;
                if (at != null && filtro(at)) { Jotunn.Logger.LogInfo($"[{PluginName}] ataque de {go.name}: {at.m_attackAnimation}"); return at; }
            }
            Jotunn.Logger.LogWarning($"[{PluginName}] ataque nao achado ({nome}).");
            return null;
        }

        private static ItemDrop Fonte(string nome, string padrao)
        {
            var d = PrefabManager.Instance.GetPrefab(nome)?.GetComponent<ItemDrop>();
            if (d != null) return d;
            Jotunn.Logger.LogWarning($"[{PluginName}] Arma '{nome}' nao existe no jogo; usando '{padrao}'.");
            return PrefabManager.Instance.GetPrefab(padrao)?.GetComponent<ItemDrop>();
        }

        // ---- as foices (variantes) ----
        public class Variante
        {
            public string Prefab, Chave, Pasta, Estacao = CraftingStations.Forge, ReceitaDoJogo;
            public int Nivel = 4;
            public RequirementConfig[] Itens;
            public float Corte, CortePorNivel, Durabilidade = 200f, Peso = 3.5f;
            public bool Sangue; // sangramento + respingo de sangue
        }
        public static readonly System.Collections.Generic.HashSet<string> Prefabs = new System.Collections.Generic.HashSet<string>();
        public static ConfigEntry<bool> CfgNoTopo;

        // Poe as receitas das foices no topo da lista de fabricacao (o jogo mostra na ordem do ObjectDB).
        static void ReceitasNoTopo()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_recipes == null || CfgNoTopo == null || !CfgNoTopo.Value) return;
            var nossas = db.m_recipes.Where(r => r != null && r.m_item != null && Prefabs.Contains(r.m_item.name)).ToList();
            if (nossas.Count == 0) return;
            foreach (var r in nossas) db.m_recipes.Remove(r);
            db.m_recipes.InsertRange(0, nossas);
            Jotunn.Logger.LogInfo($"[{PluginName}] {nossas.Count} receitas de foice no topo da lista.");
        }

        System.Collections.Generic.IEnumerable<Variante> Variantes()
        {
            yield return new Variante
            {
                // Obsidiana (vermelha): arma das Brumas, com eitr. Forja Negra 2.
                Prefab = ItemPrefabName, Chave = "item_ceifadora", Pasta = "modelo",
                Estacao = CraftingStations.BlackForge, Nivel = 2,
                Itens = new[]
                {
                    new RequirementConfig("BlackMetal", 20, 10), new RequirementConfig("Eitr", 10, 5),
                    new RequirementConfig("YggdrasilWood", 10, 5), new RequirementConfig("Obsidian", 10, 5),
                    new RequirementConfig("Bloodbag", 10, 5),
                },
                Corte = Mathf.Max(CfgSlash.Value, 125f), CortePorNivel = CfgSlashPerLevel.Value, Sangue = true,
            };
            // Metal negro: mesmos itens do Machado de Batalha de Metal Negro (Forja nivel 4).
            yield return new Variante
            {
                Prefab = "CeifadoraMetalNegro", Chave = "item_ceifadora_metalnegro", Pasta = "modelo_metalnegro", Nivel = 4,
                ReceitaDoJogo = "BattleaxeBlackmetal",
                Itens = new[] { new RequirementConfig("FineWood", 10, 0), new RequirementConfig("BlackMetal", 30, 15), new RequirementConfig("LinenThread", 5, 5) },
                Corte = 110f, CortePorNivel = 6f, Durabilidade = 200f,
            };
            // Flametal (Terras de Cinzas): flametal, madeira de cinzas, osso carbonizado e couro de asksvin. Forja Negra 3.
            yield return new Variante
            {
                Prefab = "CeifadoraFlametal", Chave = "item_ceifadora_flametal", Pasta = "modelo_flametal",
                Estacao = CraftingStations.BlackForge, Nivel = 3,
                Itens = new[] { new RequirementConfig("FlametalNew", 30, 15), new RequirementConfig("Blackwood", 10, 5),
                                new RequirementConfig("CharredBone", 10, 5), new RequirementConfig("AskHide", 4, 2) },
                Corte = 140f, CortePorNivel = 6f, Durabilidade = 300f, Sangue = true,
            };
            // Ferro: itens da Lanca de Casca Antiga (pantano), em quantidade de arma de duas maos (Forja nivel 3).
            yield return new Variante
            {
                Prefab = "CeifadoraFerro", Chave = "item_ceifadora_ferro", Pasta = "modelo_ferro", Nivel = 3,
                Itens = new[] { new RequirementConfig("ElderBark", 20, 5), new RequirementConfig("Iron", 30, 15), new RequirementConfig("TrollHide", 4, 2) },
                Corte = 75f, CortePorNivel = 6f, Durabilidade = 175f, Peso = 4f,
            };
        }

        // Confere se cada item da receita existe no jogo (alguns mudaram de nome entre versoes).
        static readonly System.Collections.Generic.Dictionary<string, string[]> Alternativos = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "FlametalNew", new[] { "Flametal" } }, { "Blackwood", new[] { "Ashwood", "AshWood" } },
            { "AskHide", new[] { "AsksvinHide", "Asksvin_Hide" } }, { "CharredBone", new[] { "Charredbone", "BoneCharred" } },
        };
        static void ConferirItens(Variante v)
        {
            var ok = new System.Collections.Generic.List<RequirementConfig>();
            foreach (var r in v.Itens)
            {
                string nome = r.Item;
                if (PrefabManager.Instance.GetPrefab(nome) == null && Alternativos.TryGetValue(nome, out var alt))
                    nome = alt.FirstOrDefault(a => PrefabManager.Instance.GetPrefab(a) != null) ?? nome;
                if (PrefabManager.Instance.GetPrefab(nome) == null) { Jotunn.Logger.LogWarning($"[{PluginName}] {v.Prefab}: item '{r.Item}' nao existe; tirado da receita."); continue; }
                r.Item = nome; ok.Add(r);
            }
            v.Itens = ok.ToArray();
        }

        // Copia os itens e a estacao de uma receita do jogo (se existir), para ficar igual a arma equivalente.
        static void CopiarReceita(Variante v)
        {
            if (string.IsNullOrEmpty(v.ReceitaDoJogo)) return;
            // acha a receita pelo item que ela faz (o nome da receita muda entre versoes)
            var r = PrefabManager.Cache.GetPrefabs(typeof(Recipe)).Values.OfType<Recipe>()
                .FirstOrDefault(x => x != null && x.m_item != null && x.m_item.name == v.ReceitaDoJogo);
            if (r == null || r.m_resources == null || r.m_resources.Length == 0) { Jotunn.Logger.LogInfo($"[{PluginName}] receita {v.ReceitaDoJogo} nao achada; usando a padrao."); return; }
            v.Itens = r.m_resources.Where(q => q?.m_resItem != null)
                .Select(q => new RequirementConfig(q.m_resItem.name, q.m_amount, q.m_amountPerLevel)).ToArray();
            if (r.m_craftingStation != null) v.Estacao = r.m_craftingStation.name;
            if (r.m_minStationLevel > 0) v.Nivel = r.m_minStationLevel;
            Jotunn.Logger.LogInfo($"[{PluginName}] {v.Prefab}: receita de {v.ReceitaDoJogo}: " + string.Join(", ", v.Itens.Select(i => i.Item + " x" + i.Amount)) + $" ({v.Estacao} {v.Nivel})");
        }

        private void CreateWeapon(StatusEffect bleed, Variante v)
        {
            var pastaV = Path.Combine(Path.GetDirectoryName(Info.Location), v.Pasta);
            if (!File.Exists(Path.Combine(pastaV, "foice.malha")) && v.Prefab != ItemPrefabName)
            {
                Jotunn.Logger.LogWarning($"[{PluginName}] {v.Prefab}: modelo nao encontrado em {pastaV}; foice nao criada.");
                return;
            }
            CopiarReceita(v);
            ConferirItens(v);
            var config = new ItemConfig
            {
                Name = "$" + v.Chave,
                Description = "$" + v.Chave + "_desc",
                CraftingStation = v.Estacao,
                MinStationLevel = v.Nivel,
                RepairStation = v.Estacao,
                Requirements = v.Itens,
            };

            var item = new CustomItem(v.Prefab, BaseVisualPrefab, config);
            Prefabs.Add(v.Prefab);
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

            shared.m_damages = new HitData.DamageTypes { m_slash = v.Corte };
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_slash = v.CortePorNivel };
            shared.m_attackForce = 70f;
            shared.m_backstabBonus = 3f;
            shared.m_blockPower = 45f;
            shared.m_blockPowerPerLevel = 0f;
            shared.m_deflectionForce = 40f;
            shared.m_timedBlockBonus = 2f;
            shared.m_maxDurability = v.Durabilidade;
            shared.m_durabilityPerLevel = 50f;
            shared.m_weight = v.Peso;
            shared.m_movementModifier = -0.05f;

            // Golpes do proprio jogo, sem mudar o ritmo nem o movimento.
            shared.m_attack = normal.m_itemData.m_shared.m_attack.Clone();
            shared.m_secondaryAttack = especial.m_itemData.m_shared.m_secondaryAttack.Clone();
            shared.m_secondaryAttack.m_damageMultiplier = CfgSpecialMultiplier.Value;

            // Combo de 4 golpes: usa o gatilho dos machados duplos (4 golpes encadeados).
            // Especial em 3 partes: gatilho da espada grande (3 golpes encadeados), apertando 3 vezes.
            if (Golpes.CfgModo4.Value)
            {
                var duplo = AcharAtaque(a => a.m_attackChainLevels >= 4 && (a.m_attackAnimation ?? "").ToLower().Contains("dual"), "AxeBerzerkr");
                if (duplo != null)
                {
                    shared.m_attack.m_attackAnimation = duplo.m_attackAnimation;
                    shared.m_attack.m_attackChainLevels = duplo.m_attackChainLevels;
                    shared.m_attack.m_lastChainDamageMultiplier = 1f; // o dano extra vem do [Combo4]
                }
                var gs = Golpes.CfgEspecial3.Value ? AcharAtaque(a => a.m_attackChainLevels == 3 && (a.m_attackAnimation ?? "").ToLower().Contains("greatsword"), "THSwordKrom") : null;
                if (gs != null)
                {
                    var sec = shared.m_attack.Clone();
                    sec.m_attackAnimation = gs.m_attackAnimation;
                    sec.m_attackChainLevels = 3;
                    sec.m_lastChainDamageMultiplier = 1f;
                    sec.m_damageMultiplier = CfgSpecialMultiplier.Value;
                    sec.m_attackStamina = especial.m_itemData.m_shared.m_secondaryAttack.m_attackStamina / 2f;
                    shared.m_secondaryAttack = sec;
                }
                Jotunn.Logger.LogInfo($"[{PluginName}] Combo4: normal '{shared.m_attack.m_attackAnimation}' x{shared.m_attack.m_attackChainLevels}, especial '{shared.m_secondaryAttack.m_attackAnimation}' x{shared.m_secondaryAttack.m_attackChainLevels}.");
            }

            if (v.Sangue)
            {
                shared.m_attackStatusEffect = bleed;
                var chance = Traverse.Create(shared).Field("m_attackStatusEffectChance");
                if (chance.FieldExists()) chance.SetValue(CfgBleedChance.Value);
            }
            else shared.m_attackStatusEffect = null;
            // a "travadinha" no acerto fica como nas outras armas do jogo

            var pasta = pastaV;
            var modelo = ModeloFoice.Trocar(item.ItemPrefab, pasta);
            if (modelo != null)
            {
                AjusteFoice.Alinhar(modelo, pegada.gameObject, false);
                AjustarColisor(item.ItemPrefab, modelo);
                // Nas costas no mesmo lugar da atgeir.
                var atgeir = PrefabManager.Instance.GetPrefab("AtgeirBlackmetal") ?? pegada.gameObject;
                var ov = Traverse.Create(atgeir.GetComponent<ItemDrop>().m_itemData.m_shared).Field("m_attachOverride");
                if (ov.FieldExists()) Traverse.Create(shared).Field("m_attachOverride").SetValue(ov.GetValue());
                ModeloFoice.CriarCostas(item.ItemPrefab, atgeir);
                if (ModeloFoice.ModeloCostas != null) AjusteFoice.Alinhar(ModeloFoice.ModeloCostas, atgeir, true);
            }
            else TintObsidian(item.ItemPrefab);

            // cada foice com a sua propria lista de efeitos (nunca dividida com outra foice);
            // respingo de sangue so nas foices de sangue
            // efeitos de acerto do machado de duas maos (som de corte + tremida de tela), como nas armas do jogo
            var sn = normal.m_itemData.m_shared;
            shared.m_hitEffect = SemSangue(sn.m_hitEffect);
            shared.m_hitTerrainEffect = SemSangue(sn.m_hitTerrainEffect);
            // defesa e parry com o efeito do machado (som + tremida de tela)
            shared.m_blockEffect = SemSangue(sn.m_blockEffect);
            if (!shared.m_blockEffect.m_effectPrefabs.Any(e => e.m_prefab != null && e.m_prefab.name.IndexOf("camshake", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var cs = PrefabManager.Instance.GetPrefab("fx_block_camshake") ?? PrefabManager.Instance.GetPrefab("fx_hit_camshake");
                if (cs != null) shared.m_blockEffect.m_effectPrefabs = shared.m_blockEffect.m_effectPrefabs.Concat(new[] { new EffectList.EffectData { m_prefab = cs, m_enabled = true } }).ToArray();
            }
            Jotunn.Logger.LogInfo($"[{PluginName}] {v.Prefab} efeitos de defesa: " + string.Join(", ", shared.m_blockEffect.m_effectPrefabs.Where(e => e.m_prefab != null).Select(e => e.m_prefab.name)));
            GarantirTremida(shared.m_hitEffect);
            Sons.TirarSomDaFoice(shared);
            Jotunn.Logger.LogInfo($"[{PluginName}] {v.Prefab} efeitos de acerto: " + string.Join(", ", shared.m_hitEffect.m_effectPrefabs.Where(e => e.m_prefab != null).Select(e => e.m_prefab.name)));
            foreach (var at in new[] { shared.m_attack, shared.m_secondaryAttack })
            {
                var f = Traverse.Create(at).Field("m_hitEffect");
                if (at != null && f.FieldExists()) f.SetValue(SemSangue(f.GetValue<EffectList>()));
            }
            if (v.Sangue) AdicionarSangue(shared);
            ItemManager.Instance.AddItem(item);
            Jotunn.Logger.LogInfo($"[{PluginName}] {v.Prefab} adicionada: normal={normal.name}, especial={especial.name}, empunhadura={pegada.name}.");
        }

        /// A caixa de colisao do item (usada para pegar do chao) vinha da foice de colheita do jogo, num lugar
        /// diferente do modelo novo. Troca por uma caixa do tamanho e no lugar do modelo da foice.
        private static void AjustarColisor(GameObject prefab, Transform modelo)
        {
            bool tem = false; var caixa = new Bounds();
            var raiz = prefab.transform.worldToLocalMatrix;
            foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds; var m = raiz * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!tem) { caixa = new Bounds(p, Vector3.zero); tem = true; } else caixa.Encapsulate(p);
                }
            }
            if (!tem) return;
            int desligados = 0;
            foreach (var c in prefab.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) { c.enabled = false; desligados++; }
            var bc = prefab.AddComponent<BoxCollider>();
            bc.center = caixa.center;
            bc.size = Vector3.Max(caixa.size, Vector3.one * 0.08f);
            Jotunn.Logger.LogInfo($"[{PluginName}] {prefab.name}: colisao do item no lugar do modelo (centro {caixa.center}, tamanho {caixa.size}; {desligados} colisao(oes) da foice do jogo desligada(s)).");
        }

        /// Tremida de tela no acerto (a mesma das armas do jogo), se a lista ainda nao tiver.
        private static void GarantirTremida(EffectList fx)
        {
            if (fx.m_effectPrefabs.Any(e => e.m_prefab != null && e.m_prefab.name.IndexOf("camshake", System.StringComparison.OrdinalIgnoreCase) >= 0)) return;
            var cs = PrefabManager.Instance.GetPrefab("fx_hit_camshake");
            if (cs == null) { Jotunn.Logger.LogWarning($"[{PluginName}] fx_hit_camshake nao encontrado."); return; }
            fx.m_effectPrefabs = fx.m_effectPrefabs.Concat(new[] { new EffectList.EffectData { m_prefab = cs, m_enabled = true } }).ToArray();
        }

        /// Copia da lista de efeitos sem nenhum efeito de sangue.
        private static EffectList SemSangue(EffectList fx)
        {
            var nova = new EffectList();
            nova.m_effectPrefabs = (fx?.m_effectPrefabs ?? new EffectList.EffectData[0])
                .Where(e => e != null && !(e.m_prefab != null && e.m_prefab.name.IndexOf("blood", System.StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            return nova;
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
