// Encaixe da foice (v2.5).
// Na mao, golpes normais: a haste passa pelas duas maos, seguras no meio do cabo (nao na ponta).
//   Se a mao esquerda soltar durante o golpe, ela e puxada de volta para a haste (IK de braco).
// Nos giros (especial e 3o golpe): a foice fica na altura da cintura, para fora do corpo,
//   segura com as duas maos, e a lamina aponta para o lado em que o giro vai (ponta na frente).
// Nas costas (tecla R): presa ao peito do personagem, na diagonal, com a lamina no alto.
// Ajustes no .cfg, secao [Encaixe], valem com o jogo aberto.

using BepInEx.Configuration;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class AjusteFoice
    {
        public static ConfigEntry<bool> CfgDuasMaos, CfgLaminaSegue, CfgPuxarMao;
        public static ConfigEntry<float> CfgComprimento, CfgPegadaBaixo, CfgPegadaCima, CfgGiro, CfgPalma, CfgGiroEspecial,
            CfgAfastarGiro, CfgAlturaGiro, CfgGiroCostas, CfgInclinacaoCostas, CfgAfastarCostas, CfgAlturaCostas;
        public static ConfigEntry<string> CfgAjusteEsq;

        public static void Init(ConfigFile c)
        {
            CfgComprimento = c.Bind("Encaixe", "Comprimento", 2.0f, "Tamanho da foice em metros (do cabo ate o alto da lamina).");
            CfgPegadaBaixo = c.Bind("Encaixe", "PegadaMaoDeBaixo", 0.45f,
                "Distancia (m) do fim do cabo ate a mao de baixo. Maior = segura mais perto da lamina.");
            CfgPegadaCima = c.Bind("Encaixe", "PegadaMaoDeCima", 0.95f,
                "Distancia (m) do fim do cabo ate a mao de cima (usada quando so uma mao segura e nos giros).");
            CfgGiro = c.Bind("Encaixe", "GiroNaHaste", 0f,
                "Gira a foice em volta da haste (graus) nos golpes normais. Ex.: 90, 180, -90.");
            CfgDuasMaos = c.Bind("Encaixe", "EncaixarNasDuasMaos", true, "A cada quadro a haste passa pelas duas maos.");
            CfgPuxarMao = c.Bind("Encaixe", "PuxarMaoParaHaste", true,
                "Durante os golpes, se a animacao soltar uma mao, ela e puxada de volta para a haste.");
            CfgPalma = c.Bind("Encaixe", "PontoDaPalma", 0.6f, "Onde a haste passa na mao direita: 0 = pulso, 1 = nos dos dedos.");
            CfgAjusteEsq = c.Bind("Encaixe", "AjusteMaoEsquerda", "0,0,0",
                "Ajuste fino do ponto da mao esquerda, em centimetros: para os dedos, para o polegar, para a palma. Ex.: 2,0,1");
            CfgLaminaSegue = c.Bind("Encaixe", "LaminaNaFrenteNosGiros", true,
                "Nos giros a foice fica para fora do corpo, nas duas maos, com a ponta da lamina indo na frente.");
            CfgGiroEspecial = c.Bind("Encaixe", "GiroDaLaminaNosGiros", 0f,
                "Ajuste extra (graus) da lamina nos giros, se ainda ficar virada errado. Ex.: 180.");
            CfgAfastarGiro = c.Bind("Encaixe", "AfastarMaosNosGiros", 0.3f, "Nos giros, quanto as maos ficam a frente do peito (m).");
            CfgAlturaGiro = c.Bind("Encaixe", "AlturaNosGiros", -0.3f, "Nos giros, altura das maos em relacao ao peito (m). Negativo = mais baixo.");
            CfgGiroCostas = c.Bind("Encaixe", "GiroNasCostas", 0f, "Gira a foice nas costas em volta da haste (graus). 180 = lamina para o outro lado.");
            CfgInclinacaoCostas = c.Bind("Encaixe", "InclinacaoNasCostas", 30f, "Inclinacao da foice nas costas (graus). Negativo = para o outro lado.");
            CfgAfastarCostas = c.Bind("Encaixe", "AfastarDasCostas", 0.2f, "Distancia da foice ate as costas (m).");
            CfgAlturaCostas = c.Bind("Encaixe", "AlturaNasCostas", 0f, "Sobe (+) ou desce (-) a foice nas costas (m).");
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
            var enc = modelo.gameObject.AddComponent<EncaixeFoice>();
            enc.NasCostas = costas;
            if (costas) { enc.Aplicar(); Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Encaixe nas costas: preso ao peito."); return; }

            var attachA = arma.transform.Find("attach");
            Vector3 eixo = Vector3.up, lado = Vector3.forward;
            if (attachA != null && Caixa(attachA, out var b))
            {
                var s = b.size;
                eixo = s.x >= s.y && s.x >= s.z ? Vector3.right : (s.y >= s.z ? Vector3.up : Vector3.forward);
                if (Mathf.Abs(Vector3.Dot(b.max, eixo)) < Mathf.Abs(Vector3.Dot(b.min, eixo))) eixo = -eixo;
                lado = Vector3.ProjectOnPlane(b.center, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.forward, eixo);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.ProjectOnPlane(Vector3.right, eixo);
                lado.Normalize();
            }
            enc.Base = Quaternion.LookRotation(lado, eixo);
            enc.Aplicar();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Encaixe na mao: haste {eixo}, lamina {lado} (base: {arma.name}).");
        }

        public static Vector3 AjusteEsq()
        {
            var p = (CfgAjusteEsq.Value ?? "").Split(',');
            float f(int i) => i < p.Length && float.TryParse(p[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 100f : 0f;
            return new Vector3(f(0), f(1), f(2));
        }

        /// IK de dois ossos (ombro-cotovelo-pulso): leva o ponto da palma ate "alvo".
        /// Mantem a rotacao da mao. Peso 0..1.
        public static void IKBraco(Transform a, Transform b, Transform c, Vector3 alvo, Vector3 palma, float peso)
        {
            if (a == null || b == null || c == null || peso <= 0f) return;
            var rotMao = c.rotation;
            var t = Vector3.Lerp(c.position, alvo - (palma - c.position), Mathf.Clamp01(peso));
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = (pb - pa).magnitude, lcb = (pc - pb).magnitude;
            if (lab < 1e-4f || lcb < 1e-4f) return;
            float lat = Mathf.Clamp((t - pa).magnitude, 0.01f, lab + lcb - 0.002f);
            Vector3 ab = (pb - pa).normalized, ac = (pc - pa).normalized, at = (t - pa).normalized, ba = -ab, bc = (pc - pb).normalized;
            float acab0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, ab), -1f, 1f));
            float babc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f));
            float acat0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, at), -1f, 1f));
            float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            var eixo0 = Vector3.Cross(ac, ab);
            if (eixo0.sqrMagnitude < 1e-8f) eixo0 = Vector3.Cross(ac, a.forward);
            eixo0.Normalize();
            b.rotation = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, eixo0) * b.rotation;
            a.rotation = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, eixo0) * a.rotation;
            var eixo1 = Vector3.Cross(ac, at);
            if (eixo1.sqrMagnitude > 1e-8f) a.rotation = Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, eixo1.normalized) * a.rotation;
            c.rotation = rotMao;
        }
    }

    [DefaultExecutionOrder(500)]
    public class EncaixeFoice : MonoBehaviour
    {
        public Quaternion Base = Quaternion.identity;
        public bool NasCostas;

        Animator _anim;
        Transform _r, _l, _rd, _l1, _l2, _l3, _li, _lt, _ro, _rc, _lo, _lc, _peito;
        bool _procurou;
        Character _dono;
        Vector3 _radial, _radialAnt;
        float _sinal, _pesoGiro;

        float Escala => AjusteFoice.CfgComprimento.Value / Mathf.Max(0.1f, ModeloFoice.ComprimentoOriginal);

        /// Posicao base presa a mao direita (mao de cima), usada quando nada mais se aplica.
        public void Aplicar()
        {
            if (AjusteFoice.CfgComprimento == null) return;
            if (NasCostas)
            {
                float ps = transform.parent != null ? Mathf.Max(1e-3f, transform.parent.lossyScale.x) : 1f;
                transform.localScale = Vector3.one * (Escala / ps);
                return;
            }
            transform.localScale = Vector3.one * Escala;
            var rot = Base * Quaternion.AngleAxis(AjusteFoice.CfgGiro.Value, Vector3.up);
            transform.localRotation = rot;
            transform.localPosition = -(rot * Vector3.up) * AjusteFoice.CfgPegadaCima.Value;
        }

        void Procurar()
        {
            _procurou = true;
            _dono = GetComponentInParent<Character>();
            _anim = GetComponentInParent<Animator>();
            if (_anim == null || !_anim.isHuman) return;
            Transform B(HumanBodyBones o) => _anim.GetBoneTransform(o);
            _r = B(HumanBodyBones.RightHand); _l = B(HumanBodyBones.LeftHand);
            _rd = B(HumanBodyBones.RightMiddleProximal);
            _l1 = B(HumanBodyBones.LeftMiddleProximal); _l2 = B(HumanBodyBones.LeftMiddleIntermediate); _l3 = B(HumanBodyBones.LeftMiddleDistal);
            _li = B(HumanBodyBones.LeftIndexProximal); _lt = B(HumanBodyBones.LeftThumbProximal);
            _ro = B(HumanBodyBones.RightUpperArm); _rc = B(HumanBodyBones.RightLowerArm);
            _lo = B(HumanBodyBones.LeftUpperArm); _lc = B(HumanBodyBones.LeftLowerArm);
            _peito = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
        }

        Vector3 PalmaDireita() => _rd == null ? _r.position : Vector3.Lerp(_r.position, _rd.position, AjusteFoice.CfgPalma.Value);

        // Centro da mao esquerda fechada na haste: meio dos dedos dobrados + ajuste do .cfg.
        Vector3 PalmaEsquerda()
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
            if (AjusteFoice.CfgComprimento == null) return;
            if (!_procurou) Procurar();
            Aplicar();
            if (NasCostas) { Costas(); return; }
            if (_r == null || _l == null) return;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var p = _dono as Player;
            bool atacando = p != null && p.InAttack() && Golpes.AtaqueAtual(p) != null;
            bool especial = atacando && Golpes.Secundario(p);
            bool giro = atacando && (especial || (p == Player.m_localPlayer && Golpes.GiroAtivo));
            _pesoGiro = Mathf.MoveTowards(_pesoGiro, giro && AjusteFoice.CfgLaminaSegue.Value ? 1f : 0f, 8f * dt);
            bool puxar = AjusteFoice.CfgPuxarMao.Value && atacando;

            // --- golpes normais: haste pelas duas maos ---
            if (AjusteFoice.CfgDuasMaos.Value && _pesoGiro < 1f)
            {
                var pr = PalmaDireita();
                var pl = PalmaEsquerda();
                var d = pl - pr;
                float dist = d.magnitude;
                if (dist > 0.05f && dist < 0.9f)
                {
                    d /= dist;
                    var cima = transform.up;
                    if (Vector3.Dot(d, cima) < 0f) d = -d;
                    var baixo = Vector3.Dot(pr, d) <= Vector3.Dot(pl, d) ? pr : pl;
                    transform.rotation = Quaternion.FromToRotation(cima, d) * transform.rotation;
                    transform.position = baixo - d * AjusteFoice.CfgPegadaBaixo.Value;
                }
                else if (puxar)
                {
                    // mao esquerda solta: volta para a haste, no ponto da mao de baixo
                    var alvo = transform.position + transform.up * AjusteFoice.CfgPegadaBaixo.Value;
                    AjusteFoice.IKBraco(_lo, _lc, _l, alvo, pl, 1f - _pesoGiro);
                }
            }

            // --- giros: foice para fora do corpo, nas duas maos, ponta da lamina na frente ---
            if (_pesoGiro > 0f && _peito != null)
            {
                var up = _dono.transform.up;
                var c = _peito.position;
                // mao que leva a arma = a mais afastada do peito
                var hr = Vector3.ProjectOnPlane(_r.position - c, up);
                var hl = Vector3.ProjectOnPlane(_l.position - c, up);
                var agora = (hr.sqrMagnitude >= hl.sqrMagnitude ? hr : hl);
                agora = agora.sqrMagnitude > 1e-4f ? agora.normalized : _dono.transform.forward;
                if (_radial.sqrMagnitude < 0.5f) { _radial = agora; _radialAnt = agora; }
                // sentido do giro (medido, suavizado)
                float s = Vector3.Dot(agora - _radialAnt, Vector3.Cross(up, _radialAnt)) / dt;
                _sinal = Mathf.Lerp(_sinal, s, 1f - Mathf.Exp(-8f * dt));
                _radialAnt = agora;
                _radial = Vector3.Slerp(_radial, agora, 1f - Mathf.Exp(-25f * dt)).normalized;

                var tang = Vector3.Cross(up, _radial) * (_sinal >= 0f ? 1f : -1f);
                var haste = (_radial - tang * 0.2f - up * 0.12f).normalized;
                var maoBaixo = c + _radial * AjusteFoice.CfgAfastarGiro.Value + up * AjusteFoice.CfgAlturaGiro.Value;
                var cabo = maoBaixo - haste * AjusteFoice.CfgPegadaBaixo.Value;
                var maoCima = cabo + haste * AjusteFoice.CfgPegadaCima.Value;
                var lado = Vector3.ProjectOnPlane(tang, haste);
                if (lado.sqrMagnitude < 1e-4f) lado = Vector3.ProjectOnPlane(_dono.transform.forward, haste);
                var rotG = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(AjusteFoice.CfgGiroEspecial.Value, Vector3.up);

                transform.rotation = Quaternion.Slerp(transform.rotation, rotG, _pesoGiro);
                transform.position = Vector3.Lerp(transform.position, cabo, _pesoGiro);

                if (AjusteFoice.CfgPuxarMao.Value)
                {
                    // a mao mais afastada pega em cima (perto da lamina), a outra embaixo
                    bool direitaFora = hr.sqrMagnitude >= hl.sqrMagnitude;
                    AjusteFoice.IKBraco(_ro, _rc, _r, direitaFora ? maoCima : maoBaixo, PalmaDireita(), _pesoGiro);
                    AjusteFoice.IKBraco(_lo, _lc, _l, direitaFora ? maoBaixo : maoCima, PalmaEsquerda(), _pesoGiro);
                }
            }
            else { _radial = Vector3.zero; _sinal = 0f; }
        }

        // Nas costas: diagonal atras do peito, lamina no alto, deitada rente as costas.
        void Costas()
        {
            if (_peito == null || _dono == null) return;
            var raiz = _dono.transform;
            var up = raiz.up; var frente = raiz.forward; var dir = raiz.right;
            var haste = Quaternion.AngleAxis(AjusteFoice.CfgInclinacaoCostas.Value, frente) * up;
            var centro = _peito.position - frente * AjusteFoice.CfgAfastarCostas.Value + up * AjusteFoice.CfgAlturaCostas.Value;
            var lado = Vector3.ProjectOnPlane(dir, haste);
            if (lado.sqrMagnitude < 1e-4f) lado = -frente;
            transform.rotation = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(AjusteFoice.CfgGiroCostas.Value, Vector3.up);
            transform.position = centro - haste * (AjusteFoice.CfgComprimento.Value * 0.5f);
        }
    }
}
