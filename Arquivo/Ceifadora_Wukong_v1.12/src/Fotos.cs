// Fotos de diagnostico (como no mod das adagas): ao equipar a Ceifadora pela
// primeira vez na sessao (ou com o comando de console "ceifadora_fotos"), o mod:
//  1) toca cada animacao do pacote ceifadora_anims no personagem e grava uma
//     folha de contato (varios quadros) vista de lado;
//  2) executa os ataques reais da arma (3 normais + especial) e grava de lado e de frente.
// Saida: BepInEx/plugins/CeifadoraObsidiana/fotos/*.png

using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CeifadoraObsidiana
{
    internal static class Fotos
    {
        public static ConfigEntry<bool> CfgAuto;
        private static bool _feitoNaSessao;
        private static bool _rodando;
        private const int W = 480, H = 360, Colunas = 4, Linhas = 2;

        public static void Init(ConfigFile config)
        {
            CfgAuto = config.Bind("Diagnostico", "FotosAoEquipar", true,
                "Grava folhas de fotos das animacoes na primeira vez que a Ceifadora e equipada na sessao.");
            CommandManager.Instance.AddConsoleCommand(new ComandoFotos());
        }

        private class ComandoFotos : ConsoleCommand
        {
            public override string Name => "ceifadora_fotos";
            public override string Help => "Grava fotos de diagnostico das animacoes da Ceifadora";
            public override void Run(string[] args) => Iniciar(true);
        }

        public static void Tick(Player p)
        {
            if (_feitoNaSessao || _rodando || !CfgAuto.Value) return;
            var w = p.GetCurrentWeapon();
            if (w?.m_dropPrefab != null && w.m_dropPrefab.name == CeifadoraPlugin.ItemPrefabName) Iniciar(false);
        }

        private static void Iniciar(bool manual)
        {
            if (_rodando || Player.m_localPlayer == null) return;
            _feitoNaSessao = true;
            CeifadoraPlugin.Instance.StartCoroutine(Rodar(Player.m_localPlayer));
        }

        private static string Pasta()
        {
            var d = Path.Combine(Path.GetDirectoryName(CeifadoraPlugin.Instance.Info.Location), "fotos");
            Directory.CreateDirectory(d);
            return d;
        }

        private static Camera CriarCamera(out RenderTexture rt)
        {
            rt = new RenderTexture(W, H, 24);
            var go = new GameObject("CeifadoraFotoCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.targetTexture = rt;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            return cam;
        }

        private static void Posicionar(Camera cam, Player p, bool frente)
        {
            var t = p.transform;
            var alvo = t.position + Vector3.up * 1.1f;
            var pos = frente ? alvo + t.forward * 4.2f + Vector3.up * 0.4f : alvo + t.right * 4.2f + Vector3.up * 0.4f;
            cam.transform.position = pos;
            cam.transform.LookAt(alvo);
        }

        private static Texture2D Capturar(Camera cam)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static void SalvarFolha(List<Texture2D> quadros, string nome)
        {
            var folha = new Texture2D(W * Colunas, H * Linhas, TextureFormat.RGB24, false);
            var fundo = Enumerable.Repeat(Color.black, folha.width * folha.height).ToArray();
            folha.SetPixels(fundo);
            for (int i = 0; i < quadros.Count && i < Colunas * Linhas; i++)
            {
                int cx = i % Colunas, cy = Linhas - 1 - i / Colunas;
                folha.SetPixels(cx * W, cy * H, W, H, quadros[i].GetPixels());
            }
            folha.Apply();
            File.WriteAllBytes(Path.Combine(Pasta(), nome + ".png"), folha.EncodeToPNG());
            Object.Destroy(folha);
            foreach (var q in quadros) Object.Destroy(q);
        }

        private static IEnumerator Rodar(Player p)
        {
            _rodando = true;
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Fotos: iniciando...");
            var cam = CriarCamera(out var rt);
            var animator = p.GetComponentInChildren<Animator>();
            var log = new List<string>();

            // 1) cada animacao do pacote, isolada
            foreach (var clip in AnimacoesCustom.ClipesDoPacote().Where(c => c.name.Contains("ComboB_q") || c.name.Contains("xuli")).OrderBy(c => c.name))
            {
                var graph = PlayableGraph.Create("CeifadoraPrevia");
                var saida = AnimationPlayableOutput.Create(graph, "saida", animator);
                var play = AnimationClipPlayable.Create(graph, clip);
                play.SetApplyFootIK(false);
                saida.SetSourcePlayable(play);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Play();
                var quadros = new List<Texture2D>();
                int n = Colunas * Linhas;
                for (int i = 0; i < n; i++)
                {
                    double t = clip.length * i / (n - 1);
                    play.SetTime(t);
                    graph.Evaluate();
                    yield return new WaitForEndOfFrame();
                    play.SetTime(t);
                    graph.Evaluate();
                    Posicionar(cam, p, false);
                    quadros.Add(Capturar(cam));
                }
                graph.Destroy();
                SalvarFolha(quadros, "clipe_" + clip.name);
                log.Add($"clipe {clip.name}: {clip.length:0.00}s, eventos={clip.events.Length}");
                yield return null;
            }

            // 2) ataques reais
            foreach (var sec in new[] { false, true })
            {
                string nome = sec ? "real_especial" : "real_normal";
                foreach (var frente in new[] { false, true })
                {
                    yield return new WaitForSeconds(1.0f);
                    var pos0 = p.transform.position;
                    var quadros = new List<Texture2D>();
                    float inicio = Time.time, dur = sec ? 8.5f : 8.0f;
                    if (sec) Carga.ForcarSegurar = true;
                    int n = Colunas * Linhas;
                    for (int i = 0; i < n; i++)
                    {
                        float alvoT = inicio + dur * i / (n - 1);
                        while (Time.time < alvoT)
                        {
                            if (!sec) p.StartAttack(null, false);
                            else if (Time.time - inicio > 5.5f) Carga.ForcarSegurar = false;
                            yield return null;
                        }
                        yield return new WaitForEndOfFrame();
                        Posicionar(cam, p, frente);
                        quadros.Add(Capturar(cam));
                    }
                    Carga.ForcarSegurar = false;
                    var desloc = p.transform.position - pos0;
                    SalvarFolha(quadros, $"{nome}_{(frente ? "frente" : "lado")}");
                    log.Add($"{nome} {(frente ? "frente" : "lado")}: deslocamento {desloc.magnitude:0.00} m");
                }
            }

            Object.Destroy(cam.gameObject);
            rt.Release();
            File.WriteAllLines(Path.Combine(Pasta(), "fotos.log"), log);
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Fotos: concluido em " + Pasta());
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, "Ceifadora: fotos concluidas");
            _rodando = false;
        }
    }
}
