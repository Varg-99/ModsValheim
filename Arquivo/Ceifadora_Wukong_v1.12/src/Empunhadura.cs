// Ajuste de como a Ceifadora fica na mão (rotação e deslocamento do modelo).
// Valores em BepInEx/config/fabiano.ceifadoraobsidiana.cfg, seção [Empunhadura].
// O arquivo é recarregado automaticamente: dá para editar com o jogo aberto,
// salvar, e ver o resultado na hora (com a arma na mão).

using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Empunhadura
    {
        public static ConfigEntry<string> CfgRotacao;
        public static ConfigEntry<string> CfgDeslocamento;
        public static ConfigEntry<float> CfgGiroCorpo;
        static Transform _visual; static bool _girado;

        // Gira o corpo (so o visual) durante as animacoes do Wukong, caso sobre desvio de direcao.
        public static void GirarCorpo(Player p, bool wukong)
        {
            if (_visual == null) { var a = p.GetComponentInChildren<Animator>(); if (a == null) return; _visual = a.transform; }
            bool deve = wukong && Mathf.Abs(CfgGiroCorpo.Value) > 0.01f;
            if (deve) { _visual.localRotation = Quaternion.Euler(0f, CfgGiroCorpo.Value, 0f); _girado = true; }
            else if (_girado) { _visual.localRotation = Quaternion.identity; _girado = false; }
        }

        private class Instancia { public Transform T; public Quaternion Rot0; public Vector3 Pos0; }
        private static readonly List<Instancia> _vivas = new List<Instancia>();
        private static FileSystemWatcher _watcher;
        private static volatile bool _recarregar;

        public static void Init(ConfigFile config)
        {
            CfgRotacao = config.Bind("Empunhadura", "RotacaoGolpes", "0,0,0",
                "Rotacao extra (graus X,Y,Z) do modelo na mao, quando a arma esta empunhada. Ex.: 0,90,0 / 90,0,0 / 0,0,90");
            CfgDeslocamento = config.Bind("Empunhadura", "Deslocamento", "0,0,0",
                "Deslocamento extra (metros X,Y,Z) do modelo na mao. Use para centralizar o cabo entre as maos.");
            CfgGiroCorpo = config.Bind("Empunhadura", "GiroCorpo", 0f,
                "Gira o corpo (graus) durante os golpes do Wukong, caso o golpe ainda saia para um lado. Ex.: 90 ou -90.");
            CfgRotacao.SettingChanged += (_, __) => AplicarTodas();
            CfgDeslocamento.SettingChanged += (_, __) => AplicarTodas();

            try
            {
                _watcher = new FileSystemWatcher(Path.GetDirectoryName(config.ConfigFilePath), Path.GetFileName(config.ConfigFilePath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += (_, __) => _recarregar = true;
            }
            catch { /* sem recarga automatica */ }
        }

        // Chamado no Update do jogador (thread principal).
        public static void Tick(ConfigFile config)
        {
            if (!_recarregar) return;
            _recarregar = false;
            try { config.Reload(); AplicarTodas(); } catch { }
        }

        private static Vector3 Vec(string s)
        {
            var p = (s ?? "").Split(',').Select(x => float.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();
            return new Vector3(p.Length > 0 ? p[0] : 0, p.Length > 1 ? p[1] : 0, p.Length > 2 ? p[2] : 0);
        }

        private static bool _poseWukong;
        private static Quaternion _delta = Quaternion.identity;

        // Chamado todo quadro: aplica o giro do bastao da animacao atual.
        public static void Quadro(Player p)
        {
            if (!_poseWukong) return;
            _delta = AnimacoesCustom.ClipeAtual(p, out var nome, out var t) ? TrilhaArma.Delta(nome, t) : Quaternion.identity;
            AplicarTodas();
        }

        // A rotacao extra so vale durante as animacoes do Wukong (golpes e carga).
        // Parado ou andando, a foice fica na pegada normal do Valheim.
        public static void AtualizarPose(bool wukong)
        {
            if (wukong == _poseWukong) return;
            _poseWukong = wukong;
            AplicarTodas();
        }

        private static void Aplicar(Instancia i)
        {
            if (!i.T) return;
            if (!_poseWukong) { i.T.localRotation = i.Rot0; i.T.localPosition = i.Pos0; return; }
            i.T.localRotation = i.Rot0 * Quaternion.Euler(Vec(CfgRotacao.Value)) * _delta;
            i.T.localPosition = i.Pos0 + Vec(CfgDeslocamento.Value);
        }

        private static void AplicarTodas()
        {
            _vivas.RemoveAll(i => !i.T);
            foreach (var i in _vivas) Aplicar(i);
        }

        public static IEnumerable<Transform> Instancias()
        {
            _vivas.RemoveAll(i => !i.T);
            return _vivas.Select(i => i.T).ToList();
        }

        public static void Registrar(GameObject go)
        {
            if (!go) return;
            var parent = go.transform.parent;
            if (parent != null && parent.name.ToLowerInvariant().Contains("back")) return; // nas costas: sem ajuste
            var i = new Instancia { T = go.transform, Rot0 = go.transform.localRotation, Pos0 = go.transform.localPosition };
            _vivas.Add(i);
            Aplicar(i);
        }
    }

    [HarmonyPatch]
    public static class Patch_VisEquipment_AttachItem
    {
        static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(VisEquipment)).Where(m => m.Name == "AttachItem" && m.ReturnType == typeof(GameObject));

        [HarmonyPostfix]
        public static void Postfix(GameObject __result, int itemHash)
        {
            if (__result == null) return;
            if (itemHash != CeifadoraPlugin.ItemPrefabName.GetStableHashCode()) return;
            Empunhadura.Registrar(__result);
        }
    }
}
