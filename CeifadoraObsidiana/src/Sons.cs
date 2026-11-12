// Som dos golpes da foice: usa os sons de golpe do machado de duas maos (Battleaxe), numa ordem escolhida.
//  - tira o som de balanco da foice de colheita (que vinha da base do item);
//  - os sons do machado sao separados (um por gravacao) e cada golpe toca o que o .cfg mandar.
// [Sons] SequenciaDosGolpes = "1,2,1,1" -> golpe 1 = som 1 do machado, golpe 2 = som 2, golpes 3 e 4 = som 1.
// Os sons encontrados ficam listados no LogOutput.log (procure "Sons do machado").

using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Sons
    {
        public static ConfigEntry<bool> CfgLigado;
        public static ConfigEntry<string> CfgSequencia, CfgMomento, CfgMomentoEspecial;
        public static ConfigEntry<int> CfgSomEspecial;
        static List<GameObject> _sons;   // um prefab por gravacao do machado
        static bool _montou;

        public static void Init(ConfigFile c)
        {
            CfgLigado = c.Bind("Sons", "SomDoMachado", true, "Os golpes da foice usam o som de golpe do machado de duas maos (em vez do som da foice de colheita).");
            CfgSequencia = c.Bind("Sons", "SequenciaDosGolpes", "1,2,1,1",
                "Qual som do machado toca em cada golpe do combo (1o, 2o, 3o, 4o). 1 = som do 1o golpe do machado, 2 = do 2o golpe. Muda com o jogo aberto.");
            CfgMomento = c.Bind("Sons", "MomentoDosSons", "auto,auto,auto,auto",
                "Quando o som sai em cada golpe (1o, 2o, 3o, 4o): auto = junto com o rastro da lamina; ou segundos depois do inicio do golpe (ex.: 0.35). Muda com o jogo aberto.");
            CfgMomentoEspecial = c.Bind("Sons", "MomentoDoSomDoEspecial", "auto", "Quando o som do especial sai: auto ou segundos depois do inicio do golpe.");
            CfgSomEspecial = c.Bind("Sons", "SomDoEspecial", 1, "Qual som do machado toca no especial. 0 = nenhum.");
        }

        static bool EhSom(GameObject g) => g != null && (g.GetComponentInChildren<ZSFX>(true) != null || g.GetComponentInChildren<AudioSource>(true) != null);

        static EffectList.EffectData[] Lista(object dono, string campo)
        {
            if (dono == null) return new EffectList.EffectData[0];
            var f = Traverse.Create(dono).Field(campo);
            if (!f.FieldExists()) return new EffectList.EffectData[0];
            var l = f.GetValue<EffectList>();
            return l?.m_effectPrefabs ?? new EffectList.EffectData[0];
        }

        /// Tira os sons de balanco (foice de colheita) da foice. Chamado ao criar cada foice.
        public static void TirarSomDaFoice(ItemDrop.ItemData.SharedData shared)
        {
            if (!CfgLigado.Value) return;
            void Limpa(object dono, string campo)
            {
                if (dono == null) return;
                var f = Traverse.Create(dono).Field(campo);
                if (!f.FieldExists()) return;
                var l = f.GetValue<EffectList>();
                var nova = new EffectList { m_effectPrefabs = (l?.m_effectPrefabs ?? new EffectList.EffectData[0]).Where(e => e != null && !EhSom(e.m_prefab)).ToArray() };
                f.SetValue(nova);
            }
            Limpa(shared, "m_trailStartEffect");
            Limpa(shared.m_attack, "m_trailStartEffect");
            Limpa(shared.m_secondaryAttack, "m_trailStartEffect");
        }

        static void Montar()
        {
            _montou = true;
            _sons = new List<GameObject>();
            var machado = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Battleaxe") : null;
            var sh = machado != null ? machado.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            if (sh == null) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Sons: machado de duas maos nao encontrado."); return; }
            var fontes = Lista(sh, "m_trailStartEffect").Concat(Lista(sh.m_attack, "m_trailStartEffect"))
                .Concat(Lista(sh, "m_triggerEffect")).Concat(Lista(sh.m_attack, "m_triggerEffect"))
                .Concat(Lista(sh, "m_startEffect")).Concat(Lista(sh.m_attack, "m_startEffect"))
                .Where(e => e != null && e.m_enabled && EhSom(e.m_prefab)).Select(e => e.m_prefab).Distinct().ToList();
            var log = new List<string>();
            foreach (var p in fontes)
            {
                var z = p.GetComponentInChildren<ZSFX>(true);
                var clipes = z != null ? Traverse.Create(z).Field("m_audioClips").GetValue<AudioClip[]>() : null;
                if (clipes == null || clipes.Length <= 1) { _sons.Add(p); log.Add($"{_sons.Count} = {p.name}"); continue; }
                // um prefab para cada gravacao, para o golpe tocar sempre a mesma
                foreach (var c in clipes.Where(c => c != null))
                {
                    var nome = "sfx_ceifadora_" + p.name + "_" + c.name;
                    var copia = Jotunn.Managers.PrefabManager.Instance.GetPrefab(nome)
                                ?? Jotunn.Managers.PrefabManager.Instance.CreateClonedPrefab(nome, p);
                    var zc = copia.GetComponentInChildren<ZSFX>(true);
                    if (zc != null) Traverse.Create(zc).Field("m_audioClips").SetValue(new[] { c });
                    _sons.Add(copia);
                    log.Add($"{_sons.Count} = {p.name}/{c.name}");
                }
            }
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Sons do machado: " + (log.Count > 0 ? string.Join("; ", log) : "(nenhum)"));
        }

        static int SomDoGolpe(int nivel)
        {
            var p = (CfgSequencia.Value ?? "").Split(',');
            if (nivel < 0) nivel = 0;
            if (p.Length == 0) return 1;
            var s = p[Mathf.Min(nivel, p.Length - 1)].Trim();
            return int.TryParse(s, out var n) ? n : 1;
        }

        static Attack _ultimo; static int _ultimoNivel = -99; static float _ultimoTempo;

        static float Momento(string lista, int nivel)
        {
            var p = (lista ?? "").Split(',');
            if (p.Length == 0) return -1f;
            var s = p[Mathf.Clamp(nivel, 0, p.Length - 1)].Trim();
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0f ? v : -1f;
        }
        static float MomentoDe(Humanoid h, Attack a) => Golpes.Secundario(h) ? Momento(CfgMomentoEspecial.Value, 0) : Momento(CfgMomento.Value, Golpes.Nivel(a));

        /// Inicio do golpe: se o .cfg der um tempo, agenda o som para esse momento.
        public static void Inicio(Humanoid h)
        {
            if (!CfgLigado.Value || h == null || !Golpes.ComFoice(h)) return;
            var a = Golpes.AtaqueAtual(h);
            if (a == null) return;
            float t = MomentoDe(h, a);
            if (t < 0f) return;
            h.StartCoroutine(Depois(h, a, Golpes.Nivel(a), t));
        }
        static System.Collections.IEnumerator Depois(Humanoid h, Attack a, int nivel, float t)
        {
            yield return new WaitForSeconds(t);
            if (h == null || Golpes.AtaqueAtual(h) != a || Golpes.Nivel(a) != nivel) yield break; // golpe cancelado
            Tocar(a, true);
        }

        public static void Tocar(Attack a, bool agendado = false)
        {
            if (!CfgLigado.Value || a == null) return;
            var h = Traverse.Create(a).Field("m_character").GetValue<Character>() as Humanoid;
            if (h == null || !Golpes.ComFoice(h)) return;
            if (!agendado && MomentoDe(h, a) >= 0f) return; // esse golpe usa o tempo do .cfg
            int nivel = Golpes.Nivel(a);
            // um som por golpe (alguns golpes tem mais de um "TrailOn")
            if (a == _ultimo && nivel == _ultimoNivel && Time.time - _ultimoTempo < 0.5f) return;
            _ultimo = a; _ultimoNivel = nivel; _ultimoTempo = Time.time;
            if (!_montou) Montar();
            if (_sons == null || _sons.Count == 0) return;
            bool especial = Golpes.Secundario(h);
            int n = especial ? CfgSomEspecial.Value : SomDoGolpe(nivel);
            if (n <= 0) return;
            var som = _sons[Mathf.Clamp(n, 1, _sons.Count) - 1];
            var fx = new EffectList { m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = som, m_enabled = true } } };
            fx.Create(h.transform.position + Vector3.up, h.transform.rotation, h.transform);
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class Patch_SomInicio
    {
        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result) return;
            try { Sons.Inicio(__instance); }
            catch (System.Exception e) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Som do golpe: " + e.Message); }
        }
    }

    // O som toca junto com o rastro da lamina (como nas armas do jogo).
    [HarmonyPatch]
    public static class Patch_SomDoGolpe
    {
        static System.Reflection.MethodBase Alvo() => AccessTools.Method(typeof(Attack), "OnTrailStart");
        [HarmonyPrepare] static bool Prepare() => Alvo() != null;
        [HarmonyTargetMethod] static System.Reflection.MethodBase TargetMethod() => Alvo();
        [HarmonyPostfix]
        public static void Postfix(Attack __instance)
        {
            try { Sons.Tocar(__instance); }
            catch (System.Exception e) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Som do golpe: " + e.Message); }
        }
    }
}
