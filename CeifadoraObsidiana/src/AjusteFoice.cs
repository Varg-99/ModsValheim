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
            CfgAfastarGiro, CfgAlturaGiro, CfgVaoMaosGiro, CfgCaboGiro, CfgPegadaParado, CfgAmplitudeCeifada, CfgGiroCeifada, CfgTorcaoCeifada, CfgCurvarCeifada, CfgGiroCostas, CfgInclinacaoCostas, CfgAfastarCostas, CfgAlturaCostas;
        public static ConfigEntry<string> CfgAjusteEsq;

        public static void Init(ConfigFile c)
        {
            CfgComprimento = c.Bind("Encaixe", "Comprimento", 2.0f, "Tamanho da foice em metros (do cabo ate o alto da lamina).");
            CfgPegadaBaixo = c.Bind("Encaixe", "PegadaMaoDeBaixo", 0.45f,
                "Distancia (m) do fim do cabo ate a mao de baixo. Maior = segura mais perto da lamina.");
            CfgPegadaCima = c.Bind("Encaixe", "PegadaMaoDeCima", 0.95f,
                "Distancia (m) do fim do cabo ate a mao de cima, quando a mao esquerda solta durante o golpe.");
            CfgPegadaParado = c.Bind("Encaixe", "PegadaParado", 0.35f, "Parado ou andando (uma mao, como a atgeir): distancia (m) do fim do cabo ate a mao.");
            CfgAmplitudeCeifada = c.Bind("Encaixe", "AmplitudeCeifada", 1.0f, "Ultimo golpe (ceifada): quanto as maos se afastam do corpo nos arcos. 1 = normal.");
            CfgTorcaoCeifada = c.Bind("Encaixe", "TorcaoDoTroncoNaCeifada", 70f, "Ultimo golpe (ceifada): quanto (graus) o tronco pode virar para acompanhar a foice. 0 = nao vira.");
            CfgCurvarCeifada = c.Bind("Encaixe", "CurvarCorpoNaCeifada", 1f, "Ultimo golpe (ceifada): quanto o corpo se curva quando a foice desce (0 = nada, 1 = normal).");
            CfgGiroCeifada = c.Bind("Encaixe", "GiroDaLaminaNaCeifada", 0f, "Ultimo golpe (ceifada): ajuste extra (graus) da lamina, se ficar virada errado. Ex.: 180.");
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
            CfgAfastarGiro = c.Bind("Encaixe", "AfastarMaosDoPeitoNosGiros", 0.22f, "Nos giros, quanto as maos ficam a frente do peito (m).");
            CfgVaoMaosGiro = c.Bind("Encaixe", "DistanciaEntreMaosNosGiros", 0.32f, "Nos giros, distancia entre as duas maos na haste (m).");
            CfgCaboGiro = c.Bind("Encaixe", "CaboAtrasDaMaoNosGiros", 0.3f, "Nos giros, quanto do cabo sobra atras da mao de baixo (m).");
            CfgAlturaGiro = c.Bind("Encaixe", "AlturaDasMaosNosGiros", -0.3f, "Nos giros, altura das maos em relacao ao peito (m). Negativo = mais baixo.");
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
        float _sinal, _pesoGiro, _lado = 1f, _pesoAtq, _pesoCeifa, _u;
        bool _ceifando;
        float _yawSuave, _leanSuave;
        Transform _spine, _chest;
        Vector3 _ladoCeifa;

        float Escala => AjusteFoice.CfgComprimento.Value / Mathf.Max(0.1f, ModeloFoice.ComprimentoOriginal);

        /// Posicao base presa a mao direita (mao de cima), usada quando nada mais se aplica.
        public void Aplicar()
        {
            if (AjusteFoice.CfgComprimento == null) return;
            if (NasCostas)
            {
                float ps = transform.parent != null ? Mathf.Abs(transform.parent.lossyScale.y) : 1f;
                if (ps < 0.05f || ps > 20f) ps = 1f;
                transform.localScale = Vector3.one * (Escala / ps);
                return;
            }
            transform.localScale = Vector3.one * Escala;
            var rot = Base * Quaternion.AngleAxis(AjusteFoice.CfgGiro.Value, Vector3.up);
            transform.localRotation = rot;
            transform.localPosition = -(rot * Vector3.up) * AjusteFoice.CfgPegadaParado.Value;
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
            _spine = B(HumanBodyBones.Spine); _chest = B(HumanBodyBones.Chest);
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
            bool ultimo = atacando && !especial && p == Player.m_localPlayer && Golpes.GiroAtivo;
            bool ceifada = ultimo && Golpes.Ceifada;
            bool giro = especial || (ultimo && !Golpes.Ceifada);
            _pesoAtq = Mathf.MoveTowards(_pesoAtq, atacando ? 1f : 0f, 8f * dt);
            _pesoGiro = Mathf.MoveTowards(_pesoGiro, giro && AjusteFoice.CfgLaminaSegue.Value ? 1f : 0f, 8f * dt);
            _pesoCeifa = Mathf.MoveTowards(_pesoCeifa, ceifada ? 1f : 0f, 10f * dt);

            // --- golpes normais: haste pelas duas maos (parado/andando fica como a atgeir) ---
            if (AjusteFoice.CfgDuasMaos.Value && _pesoAtq > 0f && _pesoGiro < 1f && _pesoCeifa < 1f)
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
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.FromToRotation(cima, d) * transform.rotation, _pesoAtq);
                    transform.position = Vector3.Lerp(transform.position, baixo - d * AjusteFoice.CfgPegadaBaixo.Value, _pesoAtq);
                }
                else if (AjusteFoice.CfgPuxarMao.Value)
                {
                    // mao esquerda solta: volta para a haste, no ponto da mao de baixo
                    var alvo = transform.position + transform.up * (AjusteFoice.CfgPegadaCima.Value - AjusteFoice.CfgVaoMaosGiro.Value);
                    AjusteFoice.IKBraco(_lo, _lc, _l, alvo, pl, _pesoAtq * (1f - _pesoGiro) * (1f - _pesoCeifa));
                }
            }

            // --- especial (e estilo giro): foice para fora do corpo, ponta da lamina na frente ---
            if (_pesoGiro > 0f && _peito != null && _ro != null && _lo != null)
            {
                var up = _dono.transform.up;
                var c = _peito.position;
                var agora = Vector3.ProjectOnPlane(Vector3.Cross(_ro.position - _lo.position, up), up);
                agora = agora.sqrMagnitude > 1e-4f ? agora.normalized : _dono.transform.forward;
                if (_radial.sqrMagnitude < 0.5f) { _radial = agora; _radialAnt = agora; }
                float s = Vector3.Dot(agora - _radialAnt, Vector3.Cross(up, _radialAnt)) / dt;
                _sinal = Mathf.Lerp(_sinal, s, 1f - Mathf.Exp(-6f * dt));
                if (_sinal > 1f) _lado = 1f; else if (_sinal < -1f) _lado = -1f;
                _radialAnt = agora;
                _radial = Vector3.Slerp(_radial, agora, 1f - Mathf.Exp(-20f * dt)).normalized;

                var tang = Vector3.Cross(up, _radial) * _lado;
                var haste = (_radial - tang * 0.25f - up * 0.1f).normalized;
                var maoBaixo = c + _radial * AjusteFoice.CfgAfastarGiro.Value + up * AjusteFoice.CfgAlturaGiro.Value;
                bool esquerdaLeva = !especial && Golpes.PesoGiro(_anim) > 0.5f;
                Segurar(haste, tang, maoBaixo, esquerdaLeva, _pesoGiro, AjusteFoice.CfgGiroEspecial.Value);
            }
            else { _radial = Vector3.zero; _sinal = 0f; _lado = 1f; }

            // --- ultimo golpe, estilo ceifada: arcos largos guiados pelo tempo do golpe ---
            if (_pesoCeifa > 0f && _peito != null && _anim != null)
            {
                var info = _anim.IsInTransition(0) ? _anim.GetNextAnimatorStateInfo(0) : _anim.GetCurrentAnimatorStateInfo(0);
                float u = Mathf.Clamp01(info.normalizedTime);
                if (!_ceifando) { _ceifando = true; _u = 0f; }
                if (ceifada) _u = Mathf.Max(_u, u);
                var raiz = _dono.transform;
                Ceifa.Amostra(_u, out var pl0, out var h0);
                Ceifa.Amostra(Mathf.Min(1f, _u + 0.03f), out _, out var hMais);
                Ceifa.Amostra(Mathf.Max(0f, _u - 0.03f), out _, out var hMenos);
                var haste = raiz.TransformDirection(h0).normalized;
                var lado = Vector3.ProjectOnPlane(raiz.TransformDirection(hMais - hMenos), haste);
                if (lado.sqrMagnitude > 1e-5f) _ladoCeifa = lado.normalized;
                else if (_ladoCeifa.sqrMagnitude < 0.5f) _ladoCeifa = Vector3.ProjectOnPlane(raiz.right, haste).normalized;
                float amp = AjusteFoice.CfgAmplitudeCeifada.Value;
                // o corpo acompanha a foice: o tronco vira para onde a lamina vai e se curva quando ela desce
                Torcer(haste, pl0, _pesoCeifa, dt);
                var maoBaixo = _peito.position + raiz.TransformDirection(Vector3.Scale(pl0, new Vector3(amp, 1f, amp)));
                Segurar(haste, _ladoCeifa, maoBaixo, false, _pesoCeifa, AjusteFoice.CfgGiroCeifada.Value);
            }
            else if (!ceifada) { _ceifando = false; _ladoCeifa = Vector3.zero; _yawSuave = 0f; _leanSuave = 0f; }
        }

        /// Gira e inclina a coluna (3 ossos) para o tronco "puxar" a foice: vira para o lado da haste
        /// (como quem enrola o corpo e solta) e se curva para frente quando a lamina esta baixa.
        void Torcer(Vector3 haste, Vector3 maoLocal, float peso, float dt)
        {
            if (_ro == null || _lo == null) return;
            var raiz = _dono.transform; var up = raiz.up;
            var frente = Vector3.ProjectOnPlane(Vector3.Cross(_ro.position - _lo.position, up), up);
            if (frente.sqrMagnitude < 1e-4f) return;
            float maxT = AjusteFoice.CfgTorcaoCeifada.Value;
            var hz = Vector3.ProjectOnPlane(haste, up);
            float alvo = hz.sqrMagnitude > 0.04f ? Mathf.Clamp(Vector3.SignedAngle(raiz.forward, hz, up) * 0.8f, -maxT, maxT) : 0f;
            float atual = Vector3.SignedAngle(raiz.forward, frente, up);
            float yaw = Mathf.DeltaAngle(atual, alvo) * peso;
            float lean = Mathf.Lerp(-10f, 32f, Mathf.InverseLerp(0.3f, -0.35f, maoLocal.y)) * peso * AjusteFoice.CfgCurvarCeifada.Value;
            float k = 1f - Mathf.Exp(-14f * dt);
            _yawSuave = Mathf.Lerp(_yawSuave, yaw, k);
            _leanSuave = Mathf.Lerp(_leanSuave, lean, k);
            int n = (_spine != null ? 1 : 0) + (_chest != null ? 1 : 0) + (_peito != null && _peito != _chest && _peito != _spine ? 1 : 0);
            if (n == 0) return;
            var lado = Vector3.Cross(up, Quaternion.AngleAxis(_yawSuave, up) * frente.normalized);
            var q = Quaternion.AngleAxis(_leanSuave / n, lado) * Quaternion.AngleAxis(_yawSuave / n, up);
            if (_spine != null) _spine.rotation = q * _spine.rotation;
            if (_chest != null) _chest.rotation = q * _chest.rotation;
            if (_peito != null && _peito != _chest && _peito != _spine) _peito.rotation = q * _peito.rotation;
        }

        /// Foice nas duas maos: haste na direcao dada, lamina para "lado", mao de baixo em "maoBaixo".
        void Segurar(Vector3 haste, Vector3 lado, Vector3 maoBaixo, bool esquerdaLeva, float peso, float giroExtra)
        {
            float vao = Mathf.Clamp(AjusteFoice.CfgVaoMaosGiro.Value, 0.1f, 0.8f);
            var maoCima = maoBaixo + haste * vao;
            lado = Vector3.ProjectOnPlane(lado, haste);
            if (lado.sqrMagnitude < 1e-5f) lado = Vector3.ProjectOnPlane(_dono.transform.forward, haste);
            var rot = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(giroExtra, Vector3.up);
            var palmaBaixo = maoBaixo;
            if (AjusteFoice.CfgPuxarMao.Value)
            {
                AjusteFoice.IKBraco(_ro, _rc, _r, esquerdaLeva ? maoBaixo : maoCima, PalmaDireita(), peso);
                AjusteFoice.IKBraco(_lo, _lc, _l, esquerdaLeva ? maoCima : maoBaixo, PalmaEsquerda(), peso);
                // a haste passa pela mao de baixo de verdade (mesmo se o braco nao alcancar)
                palmaBaixo = esquerdaLeva ? PalmaDireita() : PalmaEsquerda();
            }
            var cabo = palmaBaixo - haste * AjusteFoice.CfgCaboGiro.Value;
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, peso);
            transform.position = Vector3.Lerp(transform.position, cabo, peso);
        }

        // Nas costas: diagonal atras do peito, lamina no alto, deitada rente as costas.
        void Costas()
        {
            if (_peito == null || _dono == null) { if (!_logou) { _logou = true; Jotunn.Logger.LogWarning($"[CeifadoraObsidiana] Costas: peito {(_peito != null)}, dono {(_dono != null)}."); } return; }
            var raiz = _dono.transform;
            var up = raiz.up; var frente = raiz.forward; var dir = raiz.right;
            var haste = Quaternion.AngleAxis(AjusteFoice.CfgInclinacaoCostas.Value, frente) * up;
            var centro = _peito.position - frente * AjusteFoice.CfgAfastarCostas.Value + up * AjusteFoice.CfgAlturaCostas.Value;
            var lado = Vector3.ProjectOnPlane(dir, haste);
            if (lado.sqrMagnitude < 1e-4f) lado = -frente;
            transform.rotation = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(AjusteFoice.CfgGiroCostas.Value, Vector3.up);
            transform.position = centro - haste * (AjusteFoice.CfgComprimento.Value * 0.5f);
            if (!_logou)
            {
                _logou = true;
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Costas: pai {transform.parent?.name} escala do pai {transform.parent?.lossyScale}, escala final {transform.lossyScale}, distancia ao peito {(transform.position - _peito.position).magnitude:0.00} m, ativo {gameObject.activeInHierarchy}.");
            }
        }
        bool _logou;
    }

    /// Trajetoria do ultimo golpe (ceifada), inspirada no padrao de foice: sobe por tras e por cima,
    /// desce na diagonal pela frente, ceifa baixo e longe, e puxa rente ao chao de volta para o corpo.
    /// Cada ponto: tempo do golpe (0..1), mao de baixo (em relacao ao peito) e direcao da haste,
    /// no espaco do personagem (x = direita, y = cima, z = frente). A lamina sempre vai na frente do movimento.
    internal static class Ceifa
    {
        static readonly float[] T = { 0.00f, 0.22f, 0.38f, 0.55f, 0.70f, 0.85f, 1.00f };
        static readonly Vector3[] P =
        {
            new Vector3( 0.25f, -0.15f, 0.15f),
            new Vector3( 0.20f,  0.15f, 0.10f),
            new Vector3( 0.05f,  0.30f, 0.20f),
            new Vector3(-0.10f,  0.00f, 0.40f),
            new Vector3(-0.05f, -0.35f, 0.45f),
            new Vector3( 0.15f, -0.35f, 0.30f),
            new Vector3( 0.30f, -0.25f, 0.15f),
        };
        static readonly Vector3[] H =
        {
            new Vector3( 0.55f,  0.35f, -0.75f),
            new Vector3( 0.35f,  0.90f, -0.25f),
            new Vector3(-0.10f,  0.95f,  0.30f),
            new Vector3(-0.55f,  0.25f,  0.80f),
            new Vector3(-0.75f, -0.35f,  0.55f),
            new Vector3( 0.20f, -0.45f,  0.87f),
            new Vector3( 0.75f, -0.30f,  0.40f),
        };

        static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static void Amostra(float u, out Vector3 mao, out Vector3 haste)
        {
            u = Mathf.Clamp01(u);
            int i = 0;
            while (i < T.Length - 2 && u > T[i + 1]) i++;
            float t = Mathf.InverseLerp(T[i], T[i + 1], u);
            int a = Mathf.Max(0, i - 1), b = i, c = i + 1, d = Mathf.Min(T.Length - 1, i + 2);
            mao = CR(P[a], P[b], P[c], P[d], t);
            haste = CR(H[a].normalized, H[b].normalized, H[c].normalized, H[d].normalized, t);
            if (haste.sqrMagnitude < 1e-4f) haste = H[c];
            haste.Normalize();
        }
    }
}
