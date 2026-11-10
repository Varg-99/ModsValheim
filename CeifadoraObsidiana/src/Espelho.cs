// Espelha a pose do personagem (esquerda <-> direita) por cima do Animator do jogo,
// para o giro do ultimo golpe sair no sentido contrario do giro do especial.
// Usa um "Animation Job" que le o resultado do Animator e troca os musculos dos dois lados.

using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace CeifadoraObsidiana
{
    public struct EspelhoJob : IAnimationJob
    {
        public NativeArray<MuscleHandle> Musculos;
        public NativeArray<int> Par;
        public NativeArray<float> Sinal;
        public NativeArray<float> Tmp;
        public float Peso;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (Peso <= 0.001f || !stream.isHumanStream) return;
            var h = stream.AsHuman();
            int n = Musculos.Length;
            for (int i = 0; i < n; i++) Tmp[i] = h.GetMuscle(Musculos[i]);
            for (int i = 0; i < n; i++)
                h.SetMuscle(Musculos[i], Mathf.Lerp(Tmp[i], Tmp[Par[i]] * Sinal[i], Peso));
            var q = h.bodyLocalRotation;
            h.bodyLocalRotation = Quaternion.Slerp(q, new Quaternion(q.x, -q.y, -q.z, q.w), Peso);
            var p = h.bodyLocalPosition;
            h.bodyLocalPosition = Vector3.Lerp(p, new Vector3(-p.x, p.y, p.z), Peso);
        }
    }

    internal static class Espelho
    {
        static Animator _anim;
        static PlayableGraph _graph;
        static AnimationScriptPlayable _job;
        static AnimationPlayableOutput _saida;
        static NativeArray<MuscleHandle> _m;
        static NativeArray<int> _par;
        static NativeArray<float> _sinal, _tmp;
        static bool _falhou;

        static string Trocar(string n)
        {
            if (n.StartsWith("Left")) return "Right" + n.Substring(4);
            if (n.StartsWith("Right")) return "Left" + n.Substring(5);
            return n;
        }

        static void Criar(Animator anim)
        {
            var hs = new MuscleHandle[MuscleHandle.muscleHandleCount];
            MuscleHandle.GetMuscleHandles(hs);
            int n = hs.Length;
            _m = new NativeArray<MuscleHandle>(hs, Allocator.Persistent);
            _par = new NativeArray<int>(n, Allocator.Persistent);
            _sinal = new NativeArray<float>(n, Allocator.Persistent);
            _tmp = new NativeArray<float>(n, Allocator.Persistent);
            var idx = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < n; i++) idx[hs[i].name] = i;
            int pares = 0;
            for (int i = 0; i < n; i++)
            {
                string nome = hs[i].name, outro = Trocar(nome);
                _par[i] = idx.TryGetValue(outro, out int j) ? j : i;
                if (_par[i] != i) pares++;
                bool central = !nome.StartsWith("Left") && !nome.StartsWith("Right");
                _sinal[i] = central && nome.Contains("Left-Right") ? -1f : 1f;
            }
            _graph = PlayableGraph.Create("CeifadoraEspelho");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _job = AnimationScriptPlayable.Create(_graph, new EspelhoJob { Musculos = _m, Par = _par, Sinal = _sinal, Tmp = _tmp, Peso = 0f }, 0);
            _job.SetProcessInputs(false);
            _saida = AnimationPlayableOutput.Create(_graph, "espelho", anim);
            _saida.SetSourcePlayable(_job);
            _saida.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            _saida.SetSortingOrder(1000);
            _saida.SetWeight(0f);
            _graph.Play();
            _anim = anim;
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Espelho pronto: {n} musculos, {pares} pares esquerda/direita.");
        }

        static void Destruir()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_m.IsCreated) _m.Dispose();
            if (_par.IsCreated) _par.Dispose();
            if (_sinal.IsCreated) _sinal.Dispose();
            if (_tmp.IsCreated) _tmp.Dispose();
            _anim = null;
        }

        public static void Atualizar(Player p)
        {
            if (_falhou) return;
            try
            {
                var anim = Golpes.AnimatorDe(p);
                bool quer = Golpes.ComFoice(p) && Golpes.CfgEspelhar.Value && Golpes.CfgTrocarUltimo.Value && anim != null && anim.isHuman;
                if (!quer || anim != _anim) { if (_anim != null) Destruir(); if (!quer) return; }
                if (_anim == null) Criar(anim);
                float w = Golpes.PesoGiro(anim);
                _saida.SetWeight(w > 0f ? 1f : 0f);
                var j = _job.GetJobData<EspelhoJob>();
                j.Peso = w;
                _job.SetJobData(j);
            }
            catch (System.Exception e)
            {
                _falhou = true;
                Jotunn.Logger.LogError("[CeifadoraObsidiana] Espelho do giro desligado: " + e);
                try { Destruir(); } catch { }
            }
        }
    }
}
