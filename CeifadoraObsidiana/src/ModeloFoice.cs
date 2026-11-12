// Carrega o modelo da foice (pasta "modelo" do plugin) e coloca no lugar do modelo do jogo.
// foice.malha: malha ja convertida (haste em pe no eixo Y, cabo na origem, lamina para +Z).

using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace CeifadoraObsidiana
{
    internal static class ModeloFoice
    {
        public static float ComprimentoOriginal = 1f;

        // ImageConversion.LoadImage por reflexao (referencia-lo direto quebra a compilacao: netstandard 2.1).
        static readonly System.Reflection.MethodInfo _loadImage =
            System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

        /// Imagem simples (sem mipmaps), para icones.
        public static Texture2D Imagem(string arq)
        {
            if (!File.Exists(arq)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!(_loadImage?.Invoke(null, new object[] { t, File.ReadAllBytes(arq) }) is bool ok) || !ok) return null;
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Texture2D Textura(string arq, bool linear)
        {
            if (!File.Exists(arq)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
            if (!(_loadImage?.Invoke(null, new object[] { t, File.ReadAllBytes(arq) }) is bool ok) || !ok)
            {
                Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Nao consegui ler a textura " + arq);
                return null;
            }
            t.name = Path.GetFileNameWithoutExtension(arq);
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 4;
            return t;
        }

        static Mesh Malha(string arq)
        {
            using (var br = new BinaryReader(File.OpenRead(arq)))
            {
                if (new string(br.ReadChars(4)) != "FOIC") return null;
                int nv = br.ReadInt32(), ni = br.ReadInt32();
                ComprimentoOriginal = br.ReadSingle();
                var p = new Vector3[nv]; var n = new Vector3[nv]; var uv = new Vector2[nv];
                for (int i = 0; i < nv; i++)
                {
                    p[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    n[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                }
                var idx = new int[ni];
                for (int i = 0; i < ni; i++) idx[i] = (int)br.ReadUInt32();
                // Dois lados: a reducao do modelo deixou triangulos virados ao contrario, que apareciam
                // como furos/pontinhos transparentes. Cada triangulo ganha uma copia virada para o outro lado.
                var p2 = new Vector3[nv * 2]; var n2 = new Vector3[nv * 2]; var uv2 = new Vector2[nv * 2];
                for (int i = 0; i < nv; i++) { p2[i] = p2[i + nv] = p[i]; n2[i] = n[i]; n2[i + nv] = -n[i]; uv2[i] = uv2[i + nv] = uv[i]; }
                var idx2 = new int[ni * 2];
                for (int i = 0; i < ni; i += 3)
                {
                    idx2[i] = idx[i]; idx2[i + 1] = idx[i + 1]; idx2[i + 2] = idx[i + 2];
                    idx2[ni + i] = idx[i] + nv; idx2[ni + i + 1] = idx[i + 2] + nv; idx2[ni + i + 2] = idx[i + 1] + nv;
                }
                var m = new Mesh { name = "foice_ceifadora" };
                m.indexFormat = IndexFormat.UInt32;
                m.vertices = p2; m.normals = n2; m.uv = uv2; m.triangles = idx2;
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        /// Esconde a foice do jogo e poe o modelo novo dentro do "attach". Devolve o objeto do modelo.
        public static Transform Trocar(GameObject prefab, string pasta)
        {
            var attach = prefab.transform.Find("attach");
            string arq = Path.Combine(pasta, "foice.malha");
            if (attach == null || !File.Exists(arq))
            {
                Jotunn.Logger.LogWarning($"[CeifadoraObsidiana] Modelo novo nao encontrado ({arq}); usando a foice do jogo.");
                return null;
            }
            var mesh = Malha(arq);
            if (mesh == null) return null;

            Material baseMat = null;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (baseMat == null && (r is MeshRenderer || r is SkinnedMeshRenderer) && r.sharedMaterial != null
                    && !r.sharedMaterial.shader.name.Contains("Particle"))
                    baseMat = r.sharedMaterial;
                r.enabled = false;
            }
            var mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
            mat.name = "foice_ceifadora";
            var cor = Textura(Path.Combine(pasta, "foice_albedo.png"), false);
            var nor = Textura(Path.Combine(pasta, "foice_normal.png"), true);
            var met = Textura(Path.Combine(pasta, "foice_metal.png"), true);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            // sem recorte por transparencia (a textura nova e opaca)
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            if (cor != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", cor);
            if (nor != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nor); mat.EnableKeyword("_NORMALMAP"); }
            if (met != null && mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICGLOSSMAP"); }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.6f);
            // Brilho leve da propria textura (detalhes vermelhos visiveis a noite).
            float brilho = CeifadoraPlugin.CfgBrilho.Value;
            if (mat.HasProperty("_EmissionColor"))
            {
                if (brilho > 0f && cor != null && mat.HasProperty("_EmissionMap"))
                {
                    mat.SetTexture("_EmissionMap", cor);
                    mat.SetColor("_EmissionColor", Color.white * brilho);
                    mat.EnableKeyword("_EMISSION");
                }
                else mat.SetColor("_EmissionColor", Color.black);
            }

            Malha_ = mesh; Mat_ = mat;
            Modelo = CriarModelo(attach, mesh, mat);

            // Penas (penduricalhos) separadas do resto: balancam com o movimento da arma.
            Penas_ = null;
            var arqP = Path.Combine(pasta, "penas.malha");
            if (File.Exists(arqP))
            {
                float c0 = ComprimentoOriginal;
                Penas_ = Malha(arqP);
                ComprimentoOriginal = c0;
                PenasRaiz = 0.9f; PenasPonta = 0.65f;
                var txt = Path.Combine(pasta, "penas.txt");
                if (File.Exists(txt))
                {
                    var p = File.ReadAllText(txt).Trim().Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (p.Length >= 2) { float.TryParse(p[0], System.Globalization.NumberStyles.Float, ci, out PenasRaiz); float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out PenasPonta); }
                }
                AdicionarPenas(Modelo);
                Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Penas: {Penas_.vertexCount} vertices (raiz {PenasRaiz:0.00}, ponta {PenasPonta:0.00}).");
            }

            // Rastro: o rastro do jogo (faixa larga) e desligado; fica uma linha fina que sai do fio da lamina.
            Material matRastro = null;
            foreach (var trail in prefab.GetComponentsInChildren<MeleeWeaponTrail>(true))
            {
                matRastro = matRastro ?? HarmonyLib.Traverse.Create(trail).Field("_material").GetValue<Material>();
                trail.enabled = false;
            }
            var ponta = new GameObject("rastro_fio").transform;
            ponta.SetParent(Modelo, false);
            ponta.localPosition = new Vector3(0f, 0.86f, 0.45f); // perto da ponta da lamina
            var tr = ponta.gameObject.AddComponent<TrailRenderer>();
            tr.material = matRastro != null ? matRastro : new Material(Shader.Find("Sprites/Default"));
            tr.time = 0.18f;
            tr.minVertexDistance = 0.02f;
            tr.widthMultiplier = 1f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.8f, 0.8f), 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.emitting = false;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            ponta.gameObject.AddComponent<RastroFio>();
            // faixa entre a base e a ponta da lamina: mostra o plano do corte
            var rl = Modelo.gameObject.AddComponent<RastroLamina>();
            // ponta da lamina = vertice mais a frente (+Z); topo = vertice mais alto (+Y)
            Vector3 vPonta = Vector3.zero, vTopo = Vector3.zero;
            foreach (var vv in mesh.vertices) { if (vv.z > vPonta.z) vPonta = vv; if (vv.y > vTopo.y) vTopo = vv; }
            rl.Ponta = vPonta; rl.Base = Vector3.Lerp(vTopo, vPonta, 0.6f);
            // rastro do proprio machado de duas maos (faixa lisa do jogo), esticado sobre a lamina da foice
            RastroMachado.Criar(Modelo, vTopo, vPonta);

            var props = new System.Collections.Generic.List<string>();
            for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                if (mat.shader.GetPropertyType(i) == ShaderPropertyType.Texture) props.Add(mat.shader.GetPropertyName(i));
            var flt = new System.Collections.Generic.List<string>();
            for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                if (mat.shader.GetPropertyType(i) == ShaderPropertyType.Float || mat.shader.GetPropertyType(i) == ShaderPropertyType.Range)
                    flt.Add(mat.shader.GetPropertyName(i) + "=" + mat.GetFloat(mat.shader.GetPropertyName(i)).ToString("0.##"));
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Material: " + string.Join(" ", flt) + " | keywords: " + string.Join(" ", mat.shaderKeywords));
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Modelo novo: {mesh.vertexCount} vertices, {ComprimentoOriginal:0.00} m; shader {mat.shader.name} ({string.Join(",", props)}).");
            return Modelo;
        }

        public static Transform Modelo, ModeloCostas;
        static Mesh Malha_, Penas_;
        static Material Mat_;
        static float PenasRaiz, PenasPonta;

        static void AdicionarPenas(Transform modelo)
        {
            if (Penas_ == null || modelo == null) return;
            var go = new GameObject("penas");
            go.layer = modelo.gameObject.layer;
            go.transform.SetParent(modelo, false);
            go.AddComponent<MeshFilter>().sharedMesh = Penas_;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat_;
            mr.shadowCastingMode = ShadowCastingMode.On;
            var pm = go.AddComponent<PenasMoles>();
            pm.Raiz = PenasRaiz; pm.Ponta = PenasPonta;
        }

        /// Nas costas (tecla R): o jogo procura um "attach_back" na arma. A foice do jogo nao tem,
        /// entao criamos um igual ao da arma de referencia (atgeir).
        public static void CriarCostas(GameObject prefab, GameObject referencia)
        {
            if (Malha_ == null) return;
            var costas = prefab.transform.Find("attach_back");
            if (costas == null)
            {
                var refC = referencia.transform.Find("attach_back") ?? referencia.transform.Find("attach");
                var go = new GameObject("attach_back");
                go.SetActive(false); // o jogo ativa a copia que poe nas costas
                go.layer = prefab.transform.Find("attach")?.gameObject.layer ?? prefab.layer;
                costas = go.transform;
                costas.SetParent(prefab.transform, false);
                if (refC != null) { costas.localPosition = refC.localPosition; costas.localRotation = refC.localRotation; costas.localScale = refC.localScale; }
            }
            // O jogo procura os filhos em ordem e para no primeiro "attach" que achar, mesmo nas costas.
            // Por isso o attach_back tem que ser o primeiro filho, senao a foice da mao vai para as costas.
            costas.SetSiblingIndex(0);
            ModeloCostas = CriarModelo(costas, Malha_, Mat_);
            AdicionarPenas(ModeloCostas);
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Foice nas costas criada (referencia: {referencia.name}, attach_back da referencia: {(referencia.transform.Find("attach_back") != null ? "sim" : "nao")}).");
        }

        static Transform CriarModelo(Transform pai, Mesh mesh, Material mat)
        {
            var go = new GameObject("foice_modelo");
            go.layer = pai.gameObject.layer;
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }
    }

    /// Penas que balancam: cada vertice se desloca junto com um "peso" que segue a arma com mola e
    /// amortecimento (atrasa, passa do ponto e volta). Perto da amarracao (Raiz) nao mexe; na ponta mexe tudo.
    [DefaultExecutionOrder(650)]
    public class PenasMoles : MonoBehaviour
    {
        public float Raiz = 0.9f, Ponta = 0.65f; // altura (eixo da haste, espaco do modelo)
        Mesh _mesh; Vector3[] _base, _v; float[] _w; Vector3 _centro, _p, _vel, _restAnt; bool _ini;

        void Start()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !CeifadoraPlugin.CfgPenas.Value) { enabled = false; return; }
            _mesh = Instantiate(mf.sharedMesh); _mesh.MarkDynamic(); mf.sharedMesh = _mesh;
            _base = _mesh.vertices; _v = new Vector3[_base.Length]; _w = new float[_base.Length];
            float soma = 0f; var c = Vector3.zero;
            for (int i = 0; i < _base.Length; i++)
            {
                float t = Mathf.Clamp01((Raiz - _base[i].y) / Mathf.Max(0.01f, Raiz - Ponta));
                _w[i] = Mathf.Pow(t, 1.4f);
                c += _base[i] * _w[i]; soma += _w[i];
            }
            _centro = soma > 0f ? c / soma : Vector3.zero;
        }

        void OnDestroy() { if (_mesh != null) Destroy(_mesh); }

        void LateUpdate()
        {
            if (_mesh == null) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0.0001f, 0.05f);
            var rest = transform.TransformPoint(_centro);
            if (!_ini || (rest - _restAnt).sqrMagnitude > 4f) { _p = rest; _vel = Vector3.zero; _ini = true; }
            _restAnt = rest;
            float k = CeifadoraPlugin.CfgPenasMola.Value, amort = 2f * Mathf.Sqrt(k) * 0.35f;
            var acc = (rest - _p) * k - _vel * amort + Physics.gravity * 0.25f;
            _vel += acc * dt; _p += _vel * dt;
            var off = _p - rest;
            float max = 0.18f * transform.lossyScale.y;
            if (off.magnitude > max) { off = off.normalized * max; _p = rest + off; }
            var local = transform.InverseTransformVector(off);
            for (int i = 0; i < _base.Length; i++) _v[i] = _base[i] + local * _w[i];
            _mesh.vertices = _v;
            _mesh.RecalculateBounds();
        }
    }

    /// Liga a linha do fio durante o golpe (quando o jogo liga o rastro da arma).
    /// Rastro em faixa da lamina: liga a base da lamina a ponta, quadro a quadro, por ~0,3 s.
    /// Mostra o angulo da lamina durante o corte (se ela "gira" no meio, a faixa torce).
    [DefaultExecutionOrder(600)]
    public class RastroLamina : MonoBehaviour
    {
        // pontos no espaco do modelo (haste em +Y com o cabo na origem, lamina para +Z)
        // so a parte de fora da lamina (rastro discreto, parecido com o das outras armas)
        public Vector3 Ponta = new Vector3(0f, 0.83f, 0.47f), Base = Vector3.Lerp(new Vector3(0f, 1.08f, 0.06f), new Vector3(0f, 0.83f, 0.47f), 0.6f);
        struct Amostra { public Vector3 a, b; public float t; }
        readonly System.Collections.Generic.List<Amostra> _pts = new System.Collections.Generic.List<Amostra>();
        Player _dono; GameObject _go; Mesh _mesh; Material _mat;
        Vector3[] _v = new Vector3[0]; Color[] _c = new Color[0]; int[] _i = new int[0];

        void Start()
        {
            _dono = GetComponentInParent<Player>();
            if (_dono == null || !CeifadoraPlugin.CfgRastroLamina.Value || CeifadoraPlugin.CfgRastroMachado.Value) { enabled = false; return; }
            _go = new GameObject("rastro_lamina");
            _mesh = new Mesh { name = "rastro_lamina" }; _mesh.MarkDynamic();
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = _go.AddComponent<MeshRenderer>();
            _mat = new Material(Shader.Find("Sprites/Default"));
            mr.sharedMaterial = _mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        void OnDestroy() { if (_go != null) Destroy(_go); if (_mesh != null) Destroy(_mesh); if (_mat != null) Destroy(_mat); }
        void OnDisable() { _pts.Clear(); if (_mesh != null) _mesh.Clear(); }

        void LateUpdate()
        {
            if (_go == null) return;
            if (CeifadoraPlugin.CfgRastroArco.Value) { Arco(); return; }
            float agora = Time.time, vida = Mathf.Max(0.05f, CeifadoraPlugin.CfgRastroTempo.Value);
            bool liga = _dono != null && _dono.InAttack();
            if (liga)
            {
                var a = transform.TransformPoint(Base); var b = transform.TransformPoint(Ponta);
                if (_pts.Count > 0 && (b - _pts[_pts.Count - 1].b).sqrMagnitude > 1.5f * 1.5f) _pts.Clear(); // pulo de pose
                _pts.Add(new Amostra { a = a, b = b, t = agora });
            }
            _pts.RemoveAll(p => agora - p.t > vida);
            int n = _pts.Count;
            if (n < 2) { _mesh.Clear(); return; }
            if (_v.Length != n * 2) { _v = new Vector3[n * 2]; _c = new Color[n * 2]; }
            var cor = CeifadoraPlugin.CorRastro();
            for (int q = 0; q < n; q++)
            {
                float f = 1f - (agora - _pts[q].t) / vida;
                _v[2 * q] = _pts[q].a; _v[2 * q + 1] = _pts[q].b;
                _c[2 * q] = new Color(cor.r, cor.g, cor.b, 0f);
                _c[2 * q + 1] = new Color(cor.r, cor.g, cor.b, cor.a * f * f);
            }
            Desenhar(n);
        }

        void Desenhar(int n)
        {
            if (_i.Length != (n - 1) * 6) _i = new int[(n - 1) * 6];
            for (int q = 0; q < n - 1; q++)
            {
                int o = q * 6, v = q * 2;
                _i[o] = v; _i[o + 1] = v + 1; _i[o + 2] = v + 2; _i[o + 3] = v + 1; _i[o + 4] = v + 3; _i[o + 5] = v + 2;
            }
            _mesh.Clear();
            _mesh.vertices = _v; _mesh.colors = _c; _mesh.triangles = _i;
            _mesh.RecalculateBounds();
        }

        // ---- rastro "de mentira" em arco perfeito (como o do machado) ----
        // So aparece quando a ponta da lamina passa de uma velocidade (a parte rapida do golpe).
        // Os pontos da ponta sao guardados no espaco do personagem; a cada quadro um circulo e ajustado
        // a eles (plano + centro + raio) e o rastro e desenhado sobre esse circulo, nao sobre a lamina.
        readonly System.Collections.Generic.List<Vector3> _arcoP = new System.Collections.Generic.List<Vector3>();
        readonly System.Collections.Generic.List<Vector3> _arcoB = new System.Collections.Generic.List<Vector3>();
        Vector3 _ultLocal; bool _temUlt; bool _riscando; float _fimRisco = -99f; float _velSuave;
        // ultimo arco calculado (espaco do personagem)
        Vector3 _c0, _e1, _e2; float _r, _rIn, _ang0, _ang1; bool _temArco;

        void Arco()
        {
            float agora = Time.time, dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float vida = Mathf.Max(0.05f, CeifadoraPlugin.CfgRastroTempo.Value);
            var raiz = _dono.transform;
            var ponta = raiz.InverseTransformPoint(transform.TransformPoint(Ponta));
            var baseL = raiz.InverseTransformPoint(transform.TransformPoint(Base));
            float vel = _temUlt ? (ponta - _ultLocal).magnitude / dt : 0f;
            _ultLocal = ponta; _temUlt = true;
            _velSuave = Mathf.Lerp(_velSuave, vel, 1f - Mathf.Exp(-25f * dt));
            bool atacando = _dono.InAttack();
            float vMin = CeifadoraPlugin.CfgRastroVelMin.Value;
            bool rapido = atacando && _velSuave > vMin;
            // histerese: comeca acima de vMin, termina abaixo de 60% dele
            if (!_riscando && rapido) { _riscando = true; _arcoP.Clear(); _arcoB.Clear(); _temArco = false; }
            else if (_riscando && (!atacando || _velSuave < vMin * 0.6f || vel > 60f)) { _riscando = false; _fimRisco = agora; }
            if (_riscando)
            {
                _arcoP.Add(ponta); _arcoB.Add(baseL);
                if (_arcoP.Count > 90) { _arcoP.RemoveAt(0); _arcoB.RemoveAt(0); }
                AjustarCirculo();
            }
            float fade = _riscando ? 1f : 1f - (agora - _fimRisco) / vida;
            if (!_temArco || fade <= 0f) { _mesh.Clear(); return; }

            // desenha: cauda transparente -> cabeca opaca
            float maxAng = CeifadoraPlugin.CfgRastroArcoMax.Value * Mathf.Deg2Rad;
            float a1 = _ang1, a0 = _ang0;
            if (Mathf.Abs(a1 - a0) > maxAng) a0 = a1 - Mathf.Sign(a1 - a0) * maxAng;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(a1 - a0) / (4f * Mathf.Deg2Rad)) + 1, 2, 120);
            if (_v.Length != n * 2) { _v = new Vector3[n * 2]; _c = new Color[n * 2]; }
            var cor = CeifadoraPlugin.CorRastro();
            for (int q = 0; q < n; q++)
            {
                float u = q / (float)(n - 1);
                float a = Mathf.Lerp(a0, a1, u);
                var dir = _e1 * Mathf.Cos(a) + _e2 * Mathf.Sin(a);
                _v[2 * q] = raiz.TransformPoint(_c0 + dir * _rIn);
                _v[2 * q + 1] = raiz.TransformPoint(_c0 + dir * _r);
                float al = cor.a * u * u * fade;
                _c[2 * q] = new Color(cor.r, cor.g, cor.b, 0f);
                _c[2 * q + 1] = new Color(cor.r, cor.g, cor.b, al);
            }
            Desenhar(n);
        }

        void AjustarCirculo()
        {
            int n = _arcoP.Count;
            if (n < 4) { _temArco = false; return; }
            // plano: normal = soma dos produtos vetoriais entre passos seguidos (sentido do giro)
            var nor = Vector3.zero;
            for (int q = 1; q < n - 1; q++) nor += Vector3.Cross(_arcoP[q] - _arcoP[q - 1], _arcoP[q + 1] - _arcoP[q]);
            if (nor.sqrMagnitude < 1e-10f) { _temArco = false; return; }
            nor.Normalize();
            var m = Vector3.zero; foreach (var p in _arcoP) m += p; m /= n;
            var e1 = Vector3.ProjectOnPlane(_arcoP[0] - m, nor);
            if (e1.sqrMagnitude < 1e-8f) e1 = Vector3.ProjectOnPlane(Vector3.forward, nor);
            e1.Normalize();
            var e2 = Vector3.Cross(nor, e1);
            // circulo por minimos quadrados (Kasa): x2+y2 + D x + E y + F = 0
            double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, sxz = 0, syz = 0, sz = 0;
            foreach (var p in _arcoP)
            {
                var d = p - m; double x = Vector3.Dot(d, e1), y = Vector3.Dot(d, e2), z = x * x + y * y;
                sxx += x * x; sxy += x * y; syy += y * y; sx += x; sy += y; sxz += x * z; syz += y * z; sz += z;
            }
            // [sxx sxy sx][D]   [-sxz]
            // [sxy syy sy][E] = [-syz]
            // [sx  sy  n ][F]   [-sz ]
            double a11 = sxx, a12 = sxy, a13 = sx, a22 = syy, a23 = sy, a33 = n;
            double det = a11 * (a22 * a33 - a23 * a23) - a12 * (a12 * a33 - a23 * a13) + a13 * (a12 * a23 - a22 * a13);
            if (System.Math.Abs(det) < 1e-12) { _temArco = false; return; }
            double b1 = -sxz, b2 = -syz, b3 = -sz;
            double D = (b1 * (a22 * a33 - a23 * a23) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a23 - a22 * b3)) / det;
            double Ee = (a11 * (b2 * a33 - a23 * b3) - b1 * (a12 * a33 - a23 * a13) + a13 * (a12 * b3 - b2 * a13)) / det;
            double F = (a11 * (a22 * b3 - b2 * a23) - a12 * (a12 * b3 - b2 * a13) + b1 * (a12 * a23 - a22 * a13)) / det;
            double cx = -D / 2, cy = -Ee / 2, rr = cx * cx + cy * cy - F;
            if (rr <= 0) { _temArco = false; return; }
            float r = (float)System.Math.Sqrt(rr);
            if (r < 0.25f || r > 3.5f) { _temArco = false; return; }
            var c = m + e1 * (float)cx + e2 * (float)cy;
            // angulos (desenrolados) da primeira e da ultima amostra
            float Ang(Vector3 p) { var d = p - c; return Mathf.Atan2(Vector3.Dot(d, e2), Vector3.Dot(d, e1)); }
            float a0 = Ang(_arcoP[0]), ac = a0, prev = a0;
            for (int q = 1; q < n; q++) { float a = Ang(_arcoP[q]); ac += Mathf.DeltaAngle(prev * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad; prev = a; }
            // largura: distancia media da base da lamina ao centro
            float rb = 0f; foreach (var b in _arcoB) rb += Vector3.ProjectOnPlane(b - c, nor).magnitude; rb /= _arcoB.Count;
            float larg = Mathf.Clamp(r - rb, 0.08f, 0.6f);
            _c0 = c; _e1 = e1; _e2 = e2; _r = r; _rIn = r - larg; _ang0 = a0; _ang1 = ac; _temArco = true;
        }
    }

    /// Copia o rastro (MeleeWeaponTrail) do machado de duas maos para a foice. O jogo liga e desliga esse
    /// rastro sozinho nos eventos TrailOn/TrailOff do golpe, e ele mesmo alisa a curva (por isso parece reto).
    public class RastroMachado : MonoBehaviour
    {
        public Vector3 Topo, PontaLamina;
        public Transform B, T;

        public static void Criar(Transform modelo, Vector3 topo, Vector3 ponta)
        {
            var machado = Jotunn.Managers.PrefabManager.Instance.GetPrefab("Battleaxe");
            var orig = machado != null ? machado.GetComponentInChildren<MeleeWeaponTrail>(true) : null;
            if (orig == null) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Rastro do machado nao encontrado."); return; }
            var go = new GameObject("rastro_machado");
            go.transform.SetParent(modelo, false);
            var b = new GameObject("rastro_base").transform; b.SetParent(go.transform, false);
            var t = new GameObject("rastro_ponta").transform; t.SetParent(go.transform, false);
            var mw = go.AddComponent<MeleeWeaponTrail>();
            // copia os ajustes do rastro do machado (material, cores, larguras, tempo de vida, suavizacao)
            var log = new System.Collections.Generic.List<string>();
            foreach (var f in typeof(MeleeWeaponTrail).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                var ft = f.FieldType;
                bool copia = ft.IsPrimitive || ft.IsEnum || ft == typeof(Material) || ft == typeof(Color) || ft == typeof(Vector3)
                             || ft == typeof(Color[]) || ft == typeof(float[]) || ft == typeof(Gradient) || ft == typeof(AnimationCurve);
                if (!copia || f.Name == "_emit") continue;
                var v = f.GetValue(orig);
                if (v is System.Array arr) v = arr.Clone();
                f.SetValue(mw, v);
                log.Add(f.Name);
            }
            var tr = HarmonyLib.Traverse.Create(mw);
            tr.Field("_base").SetValue(b); tr.Field("_tip").SetValue(t);
            tr.Field("_emit").SetValue(false); // so acende no golpe (antes ficava aceso ao sacar a arma)
            var r = go.AddComponent<RastroMachado>();
            r.Topo = topo; r.PontaLamina = ponta; r.B = b; r.T = t;
            r.Posicionar();
            Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Rastro do machado copiado (" + string.Join(", ", log) + ").");
        }

        public void Posicionar()
        {
            if (B == null || T == null) return;
            B.localPosition = Vector3.LerpUnclamped(Topo, PontaLamina, CeifadoraPlugin.CfgRastroMachadoBase.Value);
            T.localPosition = Vector3.LerpUnclamped(Topo, PontaLamina, CeifadoraPlugin.CfgRastroMachadoPonta.Value);
        }

        void Start()
        {
            var mw = GetComponent<MeleeWeaponTrail>();
            if (mw != null) mw.enabled = CeifadoraPlugin.CfgRastroMachado.Value && GetComponentInParent<Character>() != null;
            if (mw != null) HarmonyLib.Traverse.Create(mw).Field("_emit").SetValue(false);
        }

        void LateUpdate() => Posicionar(); // muda com o jogo aberto
    }

    [DefaultExecutionOrder(600)]
    public class RastroFio : MonoBehaviour
    {
        static readonly System.Reflection.FieldInfo _emit = HarmonyLib.AccessTools.Field(typeof(MeleeWeaponTrail), "_emit");
        TrailRenderer _tr;
        MeleeWeaponTrail _mw;
        Player _dono;
        Vector3 _ultPos;
        bool _temPos;

        void Start()
        {
            _tr = GetComponent<TrailRenderer>();
            _dono = GetComponentInParent<Player>();
            // procura o rastro do jogo dentro da mesma arma (sobe ate o "attach")
            var t = transform.parent;
            for (int i = 0; i < 3 && t != null && _mw == null; i++) { _mw = t.GetComponentInChildren<MeleeWeaponTrail>(true); t = t.parent; }
            if (!CeifadoraPlugin.CfgRastro.Value || CeifadoraPlugin.CfgRastroLamina.Value || CeifadoraPlugin.CfgRastroMachado.Value) _tr.enabled = false;
        }

        void LateUpdate()
        {
            if (_tr == null || !_tr.enabled) return;
            bool liga;
            if (_mw != null && _emit != null) liga = (bool)_emit.GetValue(_mw);
            else liga = _dono != null && _dono.InAttack();
            if (_dono == null) liga = false;
            // se a foice pulou de lugar (troca de pose), apaga o rastro para nao riscar a tela
            var pos = transform.position;
            if (_temPos && (pos - _ultPos).sqrMagnitude > 0.8f * 0.8f) _tr.Clear();
            _ultPos = pos; _temPos = true;
            _tr.emitting = liga;
        }
    }
}
