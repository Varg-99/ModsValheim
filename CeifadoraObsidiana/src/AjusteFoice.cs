// Encaixe da foice nas maos.
// 1) Posicao base: a haste fica na direcao da haste da arma da empunhadura (atgeir),
//    com a lamina para o lado da lamina dela, e o cabo logo atras da mao direita.
// 2) A cada quadro (depois da animacao) a haste passa pelas duas palmas,
//    com o cabo perto da mao de baixo ("pegando na ponta" da foice).
// Ajustes no .cfg, secao [Encaixe], valem com o jogo aberto.

using BepInEx.Configuration;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class AjusteFoice
    {
        public static ConfigEntry<bool> CfgDuasMaos;
        public static ConfigEntry<float> CfgComprimento, CfgMargem, CfgGiro, CfgPalma, CfgPalmaEsq, CfgMaxMaos, CfgGiroEspecial;

        public static void Init(ConfigFile c)
        {
            CfgComprimento = c.Bind("Encaixe", "Comprimento", 2.0f, "Tamanho da foice em metros (do cabo ate o alto da lamina).");
            CfgMargem = c.Bind("Encaixe", "CaboAbaixoDaMao", 0.12f,
                "Quanto do cabo sobra abaixo da mao de baixo (metros). Menor = segura mais na ponta.");
            CfgGiro = c.Bind("Encaixe", "GiroNaHaste", 0f,
                "Gira a foice em volta da haste (graus) para a lamina apontar para outro lado. Ex.: 90, 180, -90.");
            CfgDuasMaos = c.Bind("Encaixe", "EncaixarNasDuasMaos", true,
                "A cada quadro a haste passa pelas duas maos.");
            CfgPalma = c.Bind("Encaixe", "PontoDaPalma", 0.6f, "Onde a haste passa na mao direita: 0 = pulso, 1 = nos dos dedos.");
            CfgPalmaEsq = c.Bind("Encaixe", "PontoDaPalmaEsquerda", 0.85f, "Onde a haste passa na mao esquerda: 0 = pulso, 1 = nos dos dedos.");
            CfgGiroEspecial = c.Bind("Encaixe", "GiroDaLaminaNoEspecial", 180f,
                "No ataque especial a foice gira em volta da haste (graus) para a lamina ficar virada para o inimigo.");
            CfgMaxMaos = c.Bind("Encaixe", "DistanciaMaximaMaos", 1.0f,
                "Se as maos ficarem mais longe que isso (m), a foice fica so na mao direita.");
        }

        static bool Caixa(Transform attach, out Bounds caixa)
        {
            bool tem = false; caixa = default;
            foreach (var mf in attach.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name == "foice_modelo") continue;
                var b = mf.sharedMesh.bounds;
                var m = attach.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!tem) { caixa = new Bounds(p, Vector3.zero); tem = true; } else caixa.Encapsulate(p);
                }
            }
            return tem;
        }

        /// Posiciona o modelo (filho do attach da foice) conforme a arma da empunhadura.
        public static void Alinhar(Transform modelo, GameObject arma)
        {
            var attachA = arma.transform.Find("attach");
            Vector3 eixo = Vector3.up, lado = Vector3.forward;
            if (attachA != null && Caixa(attachA, out var b))
            {
                var s = b.size;
                eixo = s.x >= s.y && s.x >= s.z ? Vector3.right : (s.y >= s.z ? Vector3.up : Vector3.forward);
                if (Mathf.Abs(Vector3.Dot(b.max, eixo)) < Mathf.Abs(Vector3.Dot(b.min, eixo))) eixo = -eixo;
                // lado da lamina: para onde a caixa se estende para fora da haste (a haste passa pela mao, na origem)
                lado = Vector3.ProjectOnPlane(b.center, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.forward, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.right, eixo);
                lado.Normalize();
            }
            var enc = modelo.gameObject.AddComponent<EncaixeFoice>();
            enc.Base = Quaternion.LookRotation(lado, eixo);
            enc.Aplicar();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Encaixe: haste {eixo}, lamina {lado} (base: {arma.name}).");
        }
    }

    public class EncaixeFoice : MonoBehaviour
    {
        public Quaternion Base = Quaternion.identity;

        Animator _anim;
        Transform _r, _l, _rd, _ld;
        bool _procurou;

        static Vector3 Palma(Transform mao, Transform dedo, float t) =>
            dedo == null ? mao.position : Vector3.Lerp(mao.position, dedo.position, t);

        Player _dono;
        float _giroExtra;

        /// Posicao base, presa a mao direita.
        public void Aplicar()
        {
            if (AjusteFoice.CfgComprimento == null) return;
            float esc = AjusteFoice.CfgComprimento.Value / Mathf.Max(0.1f, ModeloFoice.ComprimentoOriginal);
            transform.localScale = Vector3.one * esc;
            var rot = Base * Quaternion.AngleAxis(AjusteFoice.CfgGiro.Value + _giroExtra, Vector3.up);
            transform.localRotation = rot;
            transform.localPosition = -(rot * Vector3.up) * AjusteFoice.CfgMargem.Value;
        }

        void LateUpdate()
        {
            // No especial a lamina vira para o lado de fora (giro suave).
            if (_dono == null) _dono = GetComponentInParent<Player>();
            bool especial = _dono != null && _dono.InAttack() && Golpes.Secundario(_dono) && Golpes.AtaqueAtual(_dono) != null;
            _giroExtra = Mathf.MoveTowards(_giroExtra, especial ? AjusteFoice.CfgGiroEspecial.Value : 0f, 900f * Time.deltaTime);
            Aplicar();
            if (!AjusteFoice.CfgDuasMaos.Value) return;
            if (!_procurou)
            {
                _procurou = true;
                _anim = GetComponentInParent<Animator>();
                if (_anim != null && _anim.isHuman)
                {
                    _r = _anim.GetBoneTransform(HumanBodyBones.RightHand);
                    _l = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    _rd = _anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                    _ld = _anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                }
            }
            if (_r == null || _l == null) return;

            var pr = Palma(_r, _rd, AjusteFoice.CfgPalma.Value);
            var pl = Palma(_l, _ld, AjusteFoice.CfgPalmaEsq.Value);
            var d = pl - pr;
            float dist = d.magnitude;
            float w = Mathf.Clamp01((AjusteFoice.CfgMaxMaos.Value + 0.15f - dist) / 0.15f);
            if (w <= 0f || dist < 0.05f) return;
            d /= dist;
            var cima = transform.up; // direcao do cabo para a lamina, na posicao base
            if (Vector3.Dot(d, cima) < 0f) d = -d;
            var baixo = Vector3.Dot(pr, d) <= Vector3.Dot(pl, d) ? pr : pl;

            var rotAlvo = Quaternion.FromToRotation(cima, d) * transform.rotation;
            var posAlvo = baixo - d * AjusteFoice.CfgMargem.Value;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, w);
            transform.position = Vector3.Lerp(transform.position, posAlvo, w);
        }
    }
}
