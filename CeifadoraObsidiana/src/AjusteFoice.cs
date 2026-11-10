// Encaixe da foice.
// Na mao: posicao base alinhada a haste da atgeir; a cada quadro (depois da animacao)
//   a haste passa pelas duas maos, com o cabo logo abaixo da mao de baixo.
//   No especial, a lamina vira para o lado em que a foice esta indo (fio para o inimigo).
// Nas costas: no lugar da atgeir nas costas.
// Ajustes no .cfg, secao [Encaixe], valem com o jogo aberto.

using BepInEx.Configuration;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class AjusteFoice
    {
        public static ConfigEntry<bool> CfgDuasMaos, CfgLaminaSegue;
        public static ConfigEntry<float> CfgComprimento, CfgMargem, CfgGiro, CfgPalma, CfgMaxMaos, CfgGiroEspecial, CfgGiroCostas;
        public static ConfigEntry<string> CfgAjusteEsq;

        public static void Init(ConfigFile c)
        {
            CfgComprimento = c.Bind("Encaixe", "Comprimento", 2.0f, "Tamanho da foice em metros (do cabo ate o alto da lamina).");
            CfgMargem = c.Bind("Encaixe", "CaboAbaixoDaMao", 0.12f,
                "Quanto do cabo sobra abaixo da mao de baixo (metros). Menor = segura mais na ponta.");
            CfgGiro = c.Bind("Encaixe", "GiroNaHaste", 0f,
                "Gira a foice em volta da haste (graus) para a lamina apontar para outro lado. Ex.: 90, 180, -90.");
            CfgDuasMaos = c.Bind("Encaixe", "EncaixarNasDuasMaos", true, "A cada quadro a haste passa pelas duas maos.");
            CfgPalma = c.Bind("Encaixe", "PontoDaPalma", 0.6f, "Onde a haste passa na mao direita: 0 = pulso, 1 = nos dos dedos.");
            CfgAjusteEsq = c.Bind("Encaixe", "AjusteMaoEsquerda", "0,0,0",
                "Ajuste fino do ponto da mao esquerda, em centimetros: para os dedos, para o polegar, para a palma. Ex.: 2,0,1");
            CfgMaxMaos = c.Bind("Encaixe", "DistanciaMaximaMaos", 1.0f,
                "Se as maos ficarem mais longe que isso (m), a foice fica so na mao direita.");
            CfgLaminaSegue = c.Bind("Encaixe", "LaminaSegueOGiroNoEspecial", true,
                "No especial a lamina vira para o lado em que a foice esta indo (o fio vai na frente, para o inimigo).");
            CfgGiroEspecial = c.Bind("Encaixe", "GiroDaLaminaNoEspecial", 0f,
                "Ajuste extra (graus) da lamina no especial, se ainda ficar virada errado. Ex.: 180.");
            CfgGiroCostas = c.Bind("Encaixe", "GiroNasCostas", 0f, "Gira a foice nas costas em volta da haste (graus).");
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

        public static void Alinhar(Transform modelo, GameObject arma, bool costas)
        {
            var attachA = costas ? (arma.transform.Find("attach_back") ?? arma.transform.Find("attach")) : arma.transform.Find("attach");
            Vector3 eixo = Vector3.up, lado = Vector3.forward, centro = Vector3.zero;
            if (attachA != null && Caixa(attachA, out var b))
            {
                var s = b.size;
                eixo = s.x >= s.y && s.x >= s.z ? Vector3.right : (s.y >= s.z ? Vector3.up : Vector3.forward);
                if (Mathf.Abs(Vector3.Dot(b.max, eixo)) < Mathf.Abs(Vector3.Dot(b.min, eixo))) eixo = -eixo;
                lado = Vector3.ProjectOnPlane(b.center, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.forward, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.right, eixo);
                lado.Normalize();
                centro = b.center;
            }
            var enc = modelo.gameObject.AddComponent<EncaixeFoice>();
            enc.Base = Quaternion.LookRotation(lado, eixo);
            enc.NasCostas = costas;
            enc.Centro = centro;
            enc.Aplicar();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Encaixe {(costas ? "nas costas" : "na mao")}: haste {eixo}, lamina {lado} (base: {arma.name}).");
        }

        public static Vector3 AjusteEsq()
        {
            var p = (CfgAjusteEsq.Value ?? "").Split(',');
            float f(int i) => i < p.Length && float.TryParse(p[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 100f : 0f;
            return new Vector3(f(0), f(1), f(2));
        }
    }

    [DefaultExecutionOrder(-500)]
    public class EncaixeFoice : MonoBehaviour
    {
        public Quaternion Base = Quaternion.identity;
        public bool NasCostas;
        public Vector3 Centro;

        Animator _anim;
        Transform _r, _l, _rd, _l1, _l2, _l3, _li, _lp, _lt;
        bool _procurou;
        Player _dono;
        Vector3 _ultimaCabeca, _dirLamina;
        float _pesoEspecial;

        float Escala => AjusteFoice.CfgComprimento.Value / Mathf.Max(0.1f, ModeloFoice.ComprimentoOriginal);

        /// Posicao base (presa a mao direita, ou nas costas).
        public void Aplicar()
        {
            if (AjusteFoice.CfgComprimento == null) return;
            transform.localScale = Vector3.one * Escala;
            if (NasCostas)
            {
                var rc = Base * Quaternion.AngleAxis(AjusteFoice.CfgGiroCostas.Value, Vector3.up);
                transform.localRotation = rc;
                transform.localPosition = Centro - (rc * Vector3.up) * (AjusteFoice.CfgComprimento.Value * 0.5f);
                return;
            }
            var rot = Base * Quaternion.AngleAxis(AjusteFoice.CfgGiro.Value, Vector3.up);
            transform.localRotation = rot;
            transform.localPosition = -(rot * Vector3.up) * AjusteFoice.CfgMargem.Value;
        }

        // Centro da mao esquerda fechada na haste: meio dos dedos dobrados + ajuste do .cfg.
        Vector3 CentroMaoEsquerda()
        {
            Vector3 p;
            if (_l1 != null && _l2 != null && _l3 != null)
                p = (_l1.position + _l2.position + _l3.position + (_li != null ? _li.position : _l1.position)) * 0.25f;
            else if (_l1 != null) p = Vector3.Lerp(_l.position, _l1.position, 0.85f);
            else p = _l.position;
            var aj = AjusteFoice.AjusteEsq();
            if (aj != Vector3.zero && _l1 != null)
            {
                var dedos = (_l1.position - _l.position).normalized;
                var polegar = _lt != null ? Vector3.ProjectOnPlane(_lt.position - _l.position, dedos).normalized : _l.right;
                var palma = Vector3.Cross(dedos, polegar).normalized;
                p += dedos * aj.x + polegar * aj.y + palma * aj.z;
            }
            return p;
        }

        void LateUpdate()
        {
            Aplicar();
            if (NasCostas || AjusteFoice.CfgComprimento == null) return;
            if (!_procurou)
            {
                _procurou = true;
                _dono = GetComponentInParent<Player>();
                _anim = GetComponentInParent<Animator>();
                if (_anim != null && _anim.isHuman)
                {
                    _r = _anim.GetBoneTransform(HumanBodyBones.RightHand);
                    _l = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    _rd = _anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                    _l1 = _anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                    _l2 = _anim.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate);
                    _l3 = _anim.GetBoneTransform(HumanBodyBones.LeftMiddleDistal);
                    _li = _anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
                    _lt = _anim.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
                }
            }
            if (_r == null || _l == null) return;

            // --- haste pelas duas maos ---
            if (AjusteFoice.CfgDuasMaos.Value)
            {
                var pr = _rd == null ? _r.position : Vector3.Lerp(_r.position, _rd.position, AjusteFoice.CfgPalma.Value);
                var pl = CentroMaoEsquerda();
                var d = pl - pr;
                float dist = d.magnitude;
                float w = Mathf.Clamp01((AjusteFoice.CfgMaxMaos.Value + 0.15f - dist) / 0.15f);
                if (w > 0f && dist > 0.05f)
                {
                    d /= dist;
                    var cima = transform.up;
                    if (Vector3.Dot(d, cima) < 0f) d = -d;
                    var baixo = Vector3.Dot(pr, d) <= Vector3.Dot(pl, d) ? pr : pl;
                    var rotAlvo = Quaternion.FromToRotation(cima, d) * transform.rotation;
                    var posAlvo = baixo - d * AjusteFoice.CfgMargem.Value;
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, w);
                    transform.position = Vector3.Lerp(transform.position, posAlvo, w);
                }
            }

            // --- especial: lamina vira para onde a foice esta indo ---
            bool especial = _dono != null && _dono.InAttack() && Golpes.Secundario(_dono) && Golpes.AtaqueAtual(_dono) != null
                            && AjusteFoice.CfgLaminaSegue.Value;
            var haste = transform.up;
            var cabeca = transform.position + haste * AjusteFoice.CfgComprimento.Value * 0.9f;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var vel = Vector3.ProjectOnPlane((cabeca - _ultimaCabeca) / dt, haste);
            _ultimaCabeca = cabeca;
            if (vel.magnitude > 1.5f) _dirLamina = Vector3.Slerp(_dirLamina.sqrMagnitude > 0f ? _dirLamina : vel.normalized, vel.normalized, 1f - Mathf.Exp(-20f * dt));
            _pesoEspecial = Mathf.MoveTowards(_pesoEspecial, especial ? 1f : 0f, 6f * dt);
            if (_pesoEspecial > 0f && _dirLamina.sqrMagnitude > 0.01f)
            {
                var lado = Vector3.ProjectOnPlane(_dirLamina, haste);
                if (lado.sqrMagnitude > 1e-4f)
                {
                    var alvo = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(AjusteFoice.CfgGiroEspecial.Value, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, alvo, _pesoEspecial);
                }
            }
        }
    }
}
