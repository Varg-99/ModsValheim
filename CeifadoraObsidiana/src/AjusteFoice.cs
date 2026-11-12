// Encaixe da foice (v2.5).
// Na mao, golpes normais: a haste passa pelas duas maos, seguras no meio do cabo (nao na ponta).
//   Se a mao esquerda soltar durante o golpe, ela e puxada de volta para a haste (IK de braco).
// Nos giros (especial e 3o golpe): a foice fica na altura da cintura, para fora do corpo,
//   segura com as duas maos, e a lamina aponta para o lado em que o giro vai (ponta na frente).
// Nas costas (tecla R): presa ao peito do personagem, na diagonal, com a lamina no alto.
// Ajustes no .cfg, secao [Encaixe], valem com o jogo aberto.

using BepInEx.Configuration;
using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class AjusteFoice
    {
        public static ConfigEntry<bool> CfgSuavizar, CfgDuasMaos, CfgLaminaSegue, CfgPuxarMao, CfgMaosNaHaste, CfgLaminaMixamo, CfgEsqParado;
        public static ConfigEntry<float> CfgComprimento, CfgPegadaBaixo, CfgPegadaCima, CfgGiro, CfgPalma, CfgGiroEspecial,
            CfgAfastarGiro, CfgAlturaGiro, CfgVaoMaosGiro, CfgCaboGiro, CfgPegadaParado, CfgAmplitudeCeifada, CfgGiroCeifada, CfgTorcaoCeifada, CfgCurvarCeifada, CfgGiroLaminaMixamo, CfgGiroCostas, CfgInclinacaoCostas, CfgAfastarCostas, CfgAlturaCostas, CfgParaForaCostas, CfgAfastarParado, CfgTamanhoSuporte, CfgGiroSuporte, CfgPegadaSuporte, CfgSuavizarMin, CfgSuavizarVel;
        public static ConfigEntry<string> CfgAjusteEsq, CfgAjusteDir, CfgGiroMaoEsq, CfgGiroMaoDir;

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
            CfgMaosNaHaste = c.Bind("Encaixe", "MaosVaoAteAHaste", true, "No fim de cada quadro as maos vao ate a haste (a foice fica onde esta).");
            CfgLaminaMixamo = c.Bind("Encaixe", "LaminaAcompanhaOCorte", true, "Em todos os golpes: a lamina gira em volta da haste para ficar no plano do corte, apontando para onde ele vai.");
            CfgGiroLaminaMixamo = c.Bind("Encaixe", "GiroDaLaminaNoCorte", 0f, "Ajuste extra (graus) da lamina nesses golpes, se ficar virada errado. Ex.: 180.");
            CfgEsqParado = c.Bind("Encaixe", "MaoEsquerdaNaHasteParado", true, "Parado ou andando: a mao esquerda tambem segura a haste (false = como a atgeir, so a direita).");
            CfgPalma = c.Bind("Encaixe", "PontoDaPalma", 0.6f, "Onde a haste passa na mao direita: 0 = pulso, 1 = nos dos dedos.");
            CfgAjusteDir = c.Bind("Encaixe", "AjusteMaoDireita", "0,0,0",
                "Ajuste fino do ponto da mao direita, em centimetros: para os dedos, para o polegar, para a palma. Valor positivo leva o ponto para la, e a mao vai para o lado contrario. Muda com o jogo aberto.");
            CfgAjusteEsq = c.Bind("Encaixe", "AjusteMaoEsquerda", "0,0,0",
                "Ajuste fino do ponto da mao esquerda, em centimetros: para os dedos, para o polegar, para a palma. Ex.: 2,0,1");
            CfgGiroMaoDir = c.Bind("Encaixe", "RotacaoMaoDireita", "0,0,0",
                "Gira a mao direita para encaixar a pegada, em graus: em volta da linha dos dedos, em volta da linha do polegar, em volta da palma (torce o pulso). Ex.: 0,15,-10. A mao continua no cabo. Muda com o jogo aberto.");
            CfgGiroMaoEsq = c.Bind("Encaixe", "RotacaoMaoEsquerda", "0,0,0",
                "Gira a mao esquerda para encaixar a pegada, em graus: em volta da linha dos dedos, em volta da linha do polegar, em volta da palma (torce o pulso). Ex.: 0,15,-10. A mao continua no cabo. Muda com o jogo aberto.");
            CfgLaminaSegue = c.Bind("Encaixe", "LaminaNaFrenteNosGiros", true,
                "Nos giros a foice fica para fora do corpo, nas duas maos, com a ponta da lamina indo na frente.");
            CfgGiroEspecial = c.Bind("Encaixe", "GiroDaLaminaNosGiros", 0f,
                "Ajuste extra (graus) da lamina nos giros, se ainda ficar virada errado. Ex.: 180.");
            CfgAfastarGiro = c.Bind("Encaixe", "AfastarMaosDoPeitoNosGiros", 0.22f, "Nos giros, quanto as maos ficam a frente do peito (m).");
            CfgVaoMaosGiro = c.Bind("Encaixe", "DistanciaEntreMaosNosGiros", 0.32f, "Nos giros, distancia entre as duas maos na haste (m).");
            CfgCaboGiro = c.Bind("Encaixe", "CaboAtrasDaMaoNosGiros", 0.3f, "Nos giros, quanto do cabo sobra atras da mao de baixo (m).");
            CfgAlturaGiro = c.Bind("Encaixe", "AlturaDasMaosNosGiros", -0.3f, "Nos giros, altura das maos em relacao ao peito (m). Negativo = mais baixo.");
            CfgGiroCostas = c.Bind("Encaixe", "GiroNasCostas", 0f, "Gira a foice nas costas em volta da haste (graus). 180 = lamina para o outro lado.");
            CfgSuavizar = c.Bind("Encaixe", "SuavizarGolpes", true, "Golpes normais (os 4 do combo): tira a tremida da foice com um filtro que suaviza o que e lento e segue o que e rapido.");
            CfgSuavizarMin = c.Bind("Encaixe", "SuavizacaoForca", 4f, "Quanto menor, mais suave (e mais atrasada) a foice fica nas partes lentas do golpe. 2 = bem suave, 8 = quase nada. Muda com o jogo aberto.");
            CfgSuavizarVel = c.Bind("Encaixe", "SuavizacaoSegueOGolpe", 1f, "Quanto o filtro se abre quando o golpe acelera. Maior = segue o golpe rapido mais de perto (menos atraso), menor = mais suave. Muda com o jogo aberto.");
            CfgTamanhoSuporte = c.Bind("Encaixe", "TamanhoNoSuporte", 1f, "Suportes de parede/chao: tamanho da foice (1 = normal).");
            CfgGiroSuporte = c.Bind("Encaixe", "GiroNoSuporte", 0f, "Suportes de parede/chao: gira a foice em volta da haste (graus). 180 = lamina para o outro lado.");
            CfgPegadaSuporte = c.Bind("Encaixe", "PontoPresoNoSuporte", 0.35f, "Suportes de parede/chao: distancia (m) do fim do cabo ate o ponto preso no suporte. Maior = a foice anda para o lado do cabo.");
            CfgParaForaCostas = c.Bind("Encaixe", "InclinarParaForaParado", 14f, "Parado: inclina a foice nas costas para fora (a parte de baixo se afasta da capa), em graus. 0 = nao inclina.");
            CfgAfastarParado = c.Bind("Encaixe", "AfastarMaisParado", 0.08f, "Parado: afasta a foice mais esse tanto (m) das costas, para nao entrar na capa.");
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

        /// Orientacao do modelo de uma arma do jogo: haste no eixo mais comprido (apontando para a cabeca),
        /// lamina para o lado onde esta o volume da peca.
        static bool BaseDe(GameObject arma, out Quaternion q)
        {
            q = Quaternion.identity;
            if (arma == null) return false;
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
            q = Quaternion.LookRotation(lado, eixo);
            return attachA != null;
        }

        public static void Alinhar(Transform modelo, GameObject arma, bool costas)
        {
            var enc = modelo.gameObject.AddComponent<EncaixeFoice>();
            enc.NasCostas = costas;
            enc.Comprimento0 = ModeloFoice.ComprimentoOriginal;
            if (costas) { enc.Aplicar(); Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Encaixe nas costas: preso ao peito."); return; }

            BaseDe(arma, out enc.Base);
            // nos suportes (parede/chao): mesma orientacao da foice de colheita do jogo
            var foiceJogo = Jotunn.Managers.PrefabManager.Instance.GetPrefab("Scythe");
            enc.TemSuporte = BaseDe(foiceJogo, out enc.BaseSuporte);
            
            enc.Aplicar();
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Encaixe na mao: base {arma.name}; nos suportes: {(enc.TemSuporte ? "como a foice do jogo" : "foice do jogo nao achada")}.");
        }

        public static Vector3 AjusteEsq() => Ajuste(CfgAjusteEsq);
        public static Vector3 Graus(ConfigEntry<string> cfg) => Ajuste(cfg) * 100f; // Ajuste() divide por 100 (cm -> m)
        public static Vector3 Ajuste(ConfigEntry<string> cfg)
        {
            var p = ((cfg != null ? cfg.Value : null) ?? "").Split(',');
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
        public Quaternion BaseSuporte = Quaternion.identity;
        public bool TemSuporte;
        bool _noSuporte;

        Animator _anim;
        Transform _r, _l, _rd, _r2, _r3, _ri, _rt, _l1, _l2, _l3, _li, _lt, _ro, _rc, _lo, _lc, _peito;
        bool _procurou;
        Character _dono;
        Vector3 _radial, _radialAnt;
        float _sinal, _pesoGiro, _lado = 1f, _pesoAtq, _pesoCeifa, _u;
        bool _ceifando, _temFrente, _avancando;
        float _pesoProp;
        Transform _hips, _ul, _ll, _ft;
        Vector3 _frenteMed, _frenteAvanco;
        float _uAnt;
        float _yawSuave, _leanSuave;
        Transform _spine, _chest, _pescoco, _cabeca;
        Vector3 _ladoCeifa;

        public float Comprimento0 = 1f;  // tamanho original deste modelo (cada foice tem o seu)
        float Escala => AjusteFoice.CfgComprimento.Value / Mathf.Max(0.1f, Comprimento0);

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
            if (_noSuporte && TemSuporte)
            {
                // pendurada num suporte: orientacao da foice do jogo, mais os ajustes do .cfg
                transform.localScale = Vector3.one * Escala * AjusteFoice.CfgTamanhoSuporte.Value;
                var rs = BaseSuporte * Quaternion.AngleAxis(AjusteFoice.CfgGiroSuporte.Value, Vector3.up);
                transform.localRotation = rs;
                transform.localPosition = -(rs * Vector3.up) * (AjusteFoice.CfgPegadaSuporte.Value * AjusteFoice.CfgTamanhoSuporte.Value);
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
            _noSuporte = GetComponentInParent<ItemStand>() != null;
            _dono = GetComponentInParent<Character>();
            _anim = GetComponentInParent<Animator>();
            if (_anim == null || !_anim.isHuman) return;
            Transform B(HumanBodyBones o) => _anim.GetBoneTransform(o);
            _r = B(HumanBodyBones.RightHand); _l = B(HumanBodyBones.LeftHand);
            _rd = B(HumanBodyBones.RightMiddleProximal); _r2 = B(HumanBodyBones.RightMiddleIntermediate); _r3 = B(HumanBodyBones.RightMiddleDistal);
            _ri = B(HumanBodyBones.RightIndexProximal); _rt = B(HumanBodyBones.RightThumbProximal);
            _l1 = B(HumanBodyBones.LeftMiddleProximal); _l2 = B(HumanBodyBones.LeftMiddleIntermediate); _l3 = B(HumanBodyBones.LeftMiddleDistal);
            _li = B(HumanBodyBones.LeftIndexProximal); _lt = B(HumanBodyBones.LeftThumbProximal);
            _ro = B(HumanBodyBones.RightUpperArm); _rc = B(HumanBodyBones.RightLowerArm);
            _lo = B(HumanBodyBones.LeftUpperArm); _lc = B(HumanBodyBones.LeftLowerArm);
            _peito = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
            _spine = B(HumanBodyBones.Spine); _chest = B(HumanBodyBones.Chest);
            _pescoco = B(HumanBodyBones.Neck); _cabeca = B(HumanBodyBones.Head);
            _hips = B(HumanBodyBones.Hips); _ul = B(HumanBodyBones.LeftUpperLeg); _ll = B(HumanBodyBones.LeftLowerLeg); _ft = B(HumanBodyBones.LeftFoot);
        }

        // Centro da mao direita fechada (meio dos dedos dobrados, onde a haste passa de verdade) + ajuste do .cfg.
        // Antes era um ponto entre o pulso e os nos dos dedos, que fica fora do punho: a mao ficava abaixo do cabo.
        Vector3 PalmaDireita()
        {
            if (_rd == null) return _r.position;
            Vector3 p = _r2 != null && _r3 != null
                ? (_rd.position + _r2.position + _r3.position + (_ri != null ? _ri.position : _rd.position)) * 0.25f
                : Vector3.Lerp(_r.position, _rd.position, AjusteFoice.CfgPalma.Value);
            var aj = AjusteFoice.Ajuste(AjusteFoice.CfgAjusteDir);
            if (aj != Vector3.zero)
            {
                var dedos = (_rd.position - _r.position).normalized;
                var polegar = _rt != null ? Vector3.ProjectOnPlane(_rt.position - _r.position, dedos).normalized : -_r.right;
                var palma = Vector3.Cross(polegar, dedos).normalized;
                p += dedos * aj.x + polegar * aj.y + palma * aj.z;
            }
            return p;
        }

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
            Restaurar();

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var p = _dono as Player;
            bool atacando = p != null && p.InAttack() && Golpes.AtaqueAtual(p) != null;
            bool especial = atacando && Golpes.Secundario(p) && !Golpes.Especial3;
            bool ultimo = atacando && !especial && p == Player.m_localPlayer && Golpes.GiroAtivo;
            bool ceifada = ultimo && Golpes.Ceifada;
            bool proprio = ultimo && Golpes.Proprio && Golpe3.TemTrilha;
            // animacao de fora (Mixamo) segurando com uma mao so: a foice fica na mao direita, sem puxar a esquerda
            bool umaMao = (ultimo && Golpes.Proprio && !Golpe3.Nossa || atacando && Golpes.UmaMao) && !(Golpe3.CfgDuasMaos?.Value ?? false);
            bool giro = especial || (ultimo && !Golpes.Ceifada && !Golpes.Proprio);
            _pesoProp = Mathf.MoveTowards(_pesoProp, proprio ? 1f : 0f, 12f * dt);
            _pesoAtq = Mathf.MoveTowards(_pesoAtq, atacando ? 1f : 0f, 8f * dt);
            _pesoGiro = Mathf.MoveTowards(_pesoGiro, giro && AjusteFoice.CfgLaminaSegue.Value ? 1f : 0f, 8f * dt);
            _pesoCeifa = Mathf.MoveTowards(_pesoCeifa, ceifada ? 1f : 0f, 10f * dt);
            bool avanco = ultimo && Golpes.Avanco;
            float uAtq = 0f;
            if (_anim != null && atacando)
            {
                var info = _anim.IsInTransition(0) ? _anim.GetNextAnimatorStateInfo(0) : _anim.GetCurrentAnimatorStateInfo(0);
                uAtq = Mathf.Clamp01(info.normalizedTime);
            }
            if (p == Player.m_localPlayer) Medir(p, atacando, especial, avanco, uAtq, dt);

            // --- golpes normais: haste pelas duas maos (parado/andando fica como a atgeir) ---
            if (!umaMao && AjusteFoice.CfgDuasMaos.Value && _pesoAtq > 0f && _pesoGiro < 1f && _pesoCeifa < 1f && _pesoProp < 1f)
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

            // --- especial, giro e avanco: foice para fora do corpo, ponta da lamina na frente ---
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
                if (avanco)
                {
                    // fim do avanco: golpe forte para frente e para baixo
                    float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, uAtq));
                    if (f > 0f)
                    {
                        var frente = _frenteAvanco.sqrMagnitude > 0.5f ? _frenteAvanco : _dono.transform.forward;
                        var hChop = (frente * 0.85f - up * 0.5f).normalized;
                        haste = Vector3.Slerp(haste, hChop, f).normalized;
                        tang = Vector3.Slerp(tang, -up, f);
                        maoBaixo = Vector3.Lerp(maoBaixo, c + frente * 0.3f - up * 0.2f, f);
                    }
                }
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

            // --- 3o golpe feito do zero: a foice segue a trilha gravada junto com a animacao ---
            if (_pesoProp > 0f && _hips != null && _anim != null)
            {
                var raiz = _dono.transform;
                float esc = 1f;
                if (_ul != null && _ll != null && _ft != null)
                    esc = ((_ul.position - _ll.position).magnitude + (_ll.position - _ft.position).magnitude) / Mathf.Max(0.1f, Golpe3.Perna);
                Golpe3.Amostra(uAtq, out var g, out var h, out var l);
                var haste = raiz.rotation * h;
                var maoBaixo = _hips.position + raiz.rotation * (g * esc);
                Segurar(haste, raiz.rotation * l, maoBaixo, false, _pesoProp, AjusteFoice.CfgGiroCeifada.Value);
            }

            // --- por ultimo: as maos vao ate a haste (a foice nao se mexe para as maos) ---
            if (AjusteFoice.CfgLaminaMixamo.Value) LaminaNoCorte(dt, atacando); else _temPonta = false;
            // golpes normais: tira a tremida (antes das maos irem ate a haste, para elas seguirem a foice ja suave)
            Filtrar(dt, atacando && !especial && !giro && _pesoCeifa <= 0f);
            if (AjusteFoice.CfgMaosNaHaste.Value) Agarrar(atacando && !umaMao, umaMao);
            Gravar();
        }

        // Mede para que lado o corpo gira (especial e fim do 2o golpe) e empurra o avanco do 3o golpe.
        void Medir(Player p, bool atacando, bool especial, bool avanco, float u, float dt)
        {
            if (_ro == null || _lo == null) return;
            var up = _dono.transform.up;
            var fr = Vector3.ProjectOnPlane(Vector3.Cross(_ro.position - _lo.position, up), up);
            if (fr.sqrMagnitude < 1e-4f) return;
            fr.Normalize();
            float dAng = _temFrente ? Vector3.SignedAngle(_frenteMed, fr, up) : 0f;
            _frenteMed = fr; _temFrente = true;
            if (Mathf.Abs(dAng) > 60f) dAng = 0f; // salto (troca de animacao)
            if (atacando && especial && Golpes.PesoGiro(_anim) < 0.5f) Golpes.AcumAtgeir = Mathf.Clamp(Golpes.AcumAtgeir + dAng, -2000f, 2000f);
            if (atacando && !especial && Golpes.Nivel(Golpes.AtaqueAtual(p)) == 1 && u > 0.45f) Golpes.AcumGolpe2 += dAng;
            if (avanco && u > 0.1f && u < 0.7f) Golpes.AcumAtgeir = Mathf.Clamp(Golpes.AcumAtgeir + (Golpes.EspelharAvanco ? -dAng : dAng), -2000f, 2000f);
            if (avanco)
            {
                if (!_avancando) { _avancando = true; _uAnt = u; _frenteAvanco = _dono.transform.forward; }
                float a0 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.75f, _uAnt));
                float a1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.75f, u));
                if (u > _uAnt) Golpes.AvancoPendente += _frenteAvanco * (Golpes.CfgDistAvanco.Value * (a1 - a0));
                _uAnt = Mathf.Max(_uAnt, u);
            }
            else { _avancando = false; _frenteAvanco = Vector3.zero; }
        }

        // O Animator do Valheim nem sempre roda em todo quadro (anda junto com a fisica). Num quadro sem
        // animacao os ossos ainda tem as nossas correcoes do quadro anterior; aplicar de novo em cima
        // fazia a foice e as maos tremerem. Aqui, se o osso ainda tem exatamente o que escrevemos,
        // volta para a pose original da animacao antes de corrigir.
        Transform[] _mex;
        Quaternion[] _bruto, _escrito;
        void Restaurar()
        {
            if (_mex == null)
            {
                _mex = new[] { _ro, _rc, _r, _lo, _lc, _l, _spine, _chest, _peito }.Where(t => t != null).Distinct().ToArray();
                _bruto = new Quaternion[_mex.Length]; _escrito = new Quaternion[_mex.Length];
                for (int i = 0; i < _mex.Length; i++) { _bruto[i] = _mex[i].localRotation; _escrito[i] = new Quaternion(9, 9, 9, 9); }
            }
            for (int i = 0; i < _mex.Length; i++)
            {
                var q = _mex[i].localRotation;
                if (q.x == _escrito[i].x && q.y == _escrito[i].y && q.z == _escrito[i].z && q.w == _escrito[i].w) _mex[i].localRotation = _bruto[i];
                else _bruto[i] = q;
            }
        }
        void Gravar()
        {
            if (_mex == null) return;
            for (int i = 0; i < _mex.Length; i++) _escrito[i] = _mex[i].localRotation;
        }

        // Leva cada palma ao ponto mais proximo da haste. Como a foice e filha da mao direita,
        // a pose dela e guardada antes e reaplicada depois, para so as maos se mexerem.
        // Filtro "one euro" (filtro adaptativo): quando a foice anda devagar ou treme, suaviza bastante;
        // quando o golpe acelera, o filtro abre e segue o movimento quase sem atraso.
        // Feito no espaco do personagem, para correr ou virar nao contar como movimento da foice.
        Quaternion _fRot; Vector3 _fPos; float _fW, _fV, _pesoFiltro; bool _fTem;
        void Filtrar(float dt, bool ligar)
        {
            if (_dono == null) return;
            var raiz = _dono.transform;
            var r = Quaternion.Inverse(raiz.rotation) * transform.rotation;
            var p = raiz.InverseTransformPoint(transform.position);
            bool usa = ligar && AjusteFoice.CfgSuavizar.Value;
            _pesoFiltro = Mathf.MoveTowards(_pesoFiltro, usa ? 1f : 0f, 6f * dt);
            if (!_fTem || _pesoFiltro <= 0f) { _fRot = r; _fPos = p; _fW = 0f; _fV = 0f; _fTem = true; return; }
            float Alfa(float fc) { float tau = 1f / (2f * Mathf.PI * Mathf.Max(0.1f, fc)); return 1f / (1f + tau / dt); }
            float k = AjusteFoice.CfgSuavizarVel.Value;
            _fW = Mathf.Lerp(_fW, Quaternion.Angle(_fRot, r) / dt, Alfa(8f));   // graus/s
            _fV = Mathf.Lerp(_fV, (p - _fPos).magnitude / dt, Alfa(8f));       // m/s
            float min = AjusteFoice.CfgSuavizarMin.Value;
            _fRot = Quaternion.Slerp(_fRot, r, Alfa(min + 0.015f * k * _fW));
            _fPos = Vector3.Lerp(_fPos, p, Alfa(min + 3f * k * _fV));
            transform.rotation = raiz.rotation * Quaternion.Slerp(r, _fRot, _pesoFiltro);
            transform.position = raiz.TransformPoint(Vector3.Lerp(p, _fPos, _pesoFiltro));
        }

        // Rotacao fina da mao (graus em volta de dedos, polegar e palma). A pose e restaurada a cada quadro,
        // entao nao acumula; o IK logo depois leva a mao ja girada de volta ate o cabo.
        static void GirarMao(Transform mao, Transform dedo, Transform polegar, Vector3 g)
        {
            if (mao == null || dedo == null || g == Vector3.zero) return;
            var dedos = (dedo.position - mao.position).normalized;
            var pol = polegar != null ? Vector3.ProjectOnPlane(polegar.position - mao.position, dedos).normalized : mao.right;
            var palma = Vector3.Cross(dedos, pol).normalized;
            var q = Quaternion.AngleAxis(g.x, dedos) * Quaternion.AngleAxis(g.y, pol) * Quaternion.AngleAxis(g.z, palma);
            mao.rotation = q * mao.rotation;
        }

        void Agarrar(bool atacando, bool soDireita = false)
        {
            if (_ro == null || _lo == null) return;
            var pos = transform.position; var rot = transform.rotation;
            GirarMao(_r, _rd, _rt, AjusteFoice.Graus(AjusteFoice.CfgGiroMaoDir));
            if (!soDireita) GirarMao(_l, _l1, _lt, AjusteFoice.Graus(AjusteFoice.CfgGiroMaoEsq));
            float comp = AjusteFoice.CfgComprimento.Value;
            Vector3 Ponto(Vector3 palma)
            {
                float t = Mathf.Clamp(Vector3.Dot(palma - pos, rot * Vector3.up), 0.05f, comp * 0.7f);
                return pos + (rot * Vector3.up) * t;
            }
            var pr = PalmaDireita();
            var alvoR = Ponto(pr);
            AjusteFoice.IKBraco(_ro, _rc, _r, alvoR, pr, 1f);
            if (soDireita) { transform.SetPositionAndRotation(pos, rot); return; }
            var pl = PalmaEsquerda();
            var alvoL = Ponto(pl);
            float dl = (alvoL - pl).magnitude;
            float wl = atacando || AjusteFoice.CfgEsqParado.Value ? Mathf.Clamp01((0.6f - dl) / 0.2f) : Mathf.Clamp01((0.3f - dl) / 0.1f);
            AjusteFoice.IKBraco(_lo, _lc, _l, alvoL, pl, wl);
            transform.SetPositionAndRotation(pos, rot);
        }

        // Lamina no angulo do corte (todos os golpes). Em vez de seguir a ponta quadro a quadro (que tremia
        // e torcia o rastro), mede o EIXO em que a haste esta girando. Num golpe esse eixo quase nao muda,
        // entao a lamina fica deitada no plano do corte, sempre apontando para onde o corte vai.
        // Gira so em volta da propria haste: maos e cabo nao mudam.
        Vector3 _hAnt, _eixoCorte;
        float _pesoCorte;
        bool _temPonta;
        void LaminaNoCorte(float dt, bool atacando)
        {
            var haste = transform.up;
            if (!_temPonta) { _temPonta = true; _hAnt = haste; _eixoCorte = Vector3.zero; _pesoCorte = 0f; return; }
            var giro = Vector3.Cross(_hAnt, haste);
            float vel = Mathf.Asin(Mathf.Clamp(giro.magnitude, 0f, 1f)) * Mathf.Rad2Deg / dt; // graus/s
            _hAnt = haste;
            if (atacando && vel > 80f)
            {
                var n = giro.normalized;
                if (_eixoCorte.sqrMagnitude < 0.5f || (Vector3.Dot(n, _eixoCorte) < 0f && vel > 250f)) _eixoCorte = n; // golpe novo
                else _eixoCorte = Vector3.Slerp(_eixoCorte, Vector3.Dot(n, _eixoCorte) < 0f ? _eixoCorte : n, 1f - Mathf.Exp(-10f * dt)).normalized;
            }
            float alvoPeso = atacando ? Mathf.Clamp01((vel - 80f) / 160f) : 0f;
            // entra rapido, sai devagar (a lamina nao "volta" de repente no fim do golpe)
            _pesoCorte = Mathf.MoveTowards(_pesoCorte, alvoPeso, (alvoPeso > _pesoCorte ? 10f : 3f) * dt);
            if (_eixoCorte.sqrMagnitude < 0.5f || _pesoCorte <= 0.01f) return;
            var lamina = Vector3.Cross(_eixoCorte, haste);
            if (lamina.sqrMagnitude < 1e-4f) return;
            lamina = Quaternion.AngleAxis(AjusteFoice.CfgGiroLaminaMixamo.Value, haste) * lamina.normalized;
            var atual = Vector3.ProjectOnPlane(transform.forward, haste);
            if (atual.sqrMagnitude < 1e-4f) return;
            var pos = transform.position;
            var q = Quaternion.FromToRotation(atual.normalized, lamina);
            transform.rotation = Quaternion.Slerp(Quaternion.identity, q, _pesoCorte) * transform.rotation;
            transform.position = pos;
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

        // Nas costas: presa ao tronco (peito, pescoco e ombros), entao gira e se inclina junto com as costas.
        void Costas()
        {
            if (_peito == null || _dono == null) { if (!_logou) { _logou = true; Jotunn.Logger.LogWarning($"[CeifadoraObsidiana] Costas: peito {(_peito != null)}, dono {(_dono != null)}."); } return; }
            var raiz = _dono.transform;
            Vector3 up = raiz.up, dir = raiz.right;
            var topo = _pescoco != null ? _pescoco : (_cabeca != null ? _cabeca : null);
            var base_ = _spine != null && _spine != _peito ? _spine : null;
            if (topo != null && base_ != null && (topo.position - base_.position).sqrMagnitude > 1e-4f) up = (topo.position - base_.position).normalized;
            else if (topo != null && (topo.position - _peito.position).sqrMagnitude > 1e-4f) up = (topo.position - _peito.position).normalized;
            if (_ro != null && _lo != null)
            {
                var d = Vector3.ProjectOnPlane(_ro.position - _lo.position, up);
                if (d.sqrMagnitude > 1e-4f) dir = d.normalized;
            }
            var frente = Vector3.Cross(dir, up).normalized;
            // parado: um pouco mais para fora, para aparecer por cima da capa
            float vel = _dono.GetVelocity().magnitude;
            _pesoParado = Mathf.MoveTowards(_pesoParado, vel < 0.6f ? 1f : 0f, 2.5f * Mathf.Max(Time.deltaTime, 1e-4f));
            var haste = Quaternion.AngleAxis(AjusteFoice.CfgInclinacaoCostas.Value, frente) * up;
            var giroFora = Quaternion.AngleAxis(AjusteFoice.CfgParaForaCostas.Value * _pesoParado, dir);
            var centro = _peito.position - frente * (AjusteFoice.CfgAfastarCostas.Value + AjusteFoice.CfgAfastarParado.Value * _pesoParado) + up * AjusteFoice.CfgAlturaCostas.Value;
            // gira em volta da altura do ombro: a parte de cima quase nao sai do lugar, a de baixo sai da capa
            var pivo = centro + haste * (AjusteFoice.CfgComprimento.Value * 0.3f);
            haste = giroFora * haste;
            centro = pivo - haste * (AjusteFoice.CfgComprimento.Value * 0.3f);
            var lado = Vector3.ProjectOnPlane(dir, haste);
            if (lado.sqrMagnitude < 1e-4f) lado = -frente;
            transform.rotation = Quaternion.LookRotation(lado.normalized, haste) * Quaternion.AngleAxis(AjusteFoice.CfgGiroCostas.Value, Vector3.up);
            transform.position = centro - haste * (AjusteFoice.CfgComprimento.Value * 0.5f);
            if (!_logou)
            {
                _logou = true;
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Costas: presa ao tronco (pescoco {(_pescoco != null)}, ombros {(_ro != null && _lo != null)}), escala final {transform.lossyScale}.");
            }
        }
        bool _logou;
        float _pesoParado;
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
