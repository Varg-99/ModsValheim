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
                var m = new Mesh { name = "foice_ceifadora" };
                if (nv > 65000) m.indexFormat = IndexFormat.UInt32;
                m.vertices = p; m.normals = n; m.uv = uv; m.triangles = idx;
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

            Modelo = CriarModelo(attach, mesh, mat);

            // Nas costas (tecla R): o jogo usa o "attach_back" da foice.
            var costas = prefab.transform.Find("attach_back");
            if (costas != null) ModeloCostas = CriarModelo(costas, mesh, mat);
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Foice nas costas: {(costas != null ? "sim" : "attach_back nao existe")}.");

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

            var props = new System.Collections.Generic.List<string>();
            for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                if (mat.shader.GetPropertyType(i) == ShaderPropertyType.Texture) props.Add(mat.shader.GetPropertyName(i));
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Modelo novo: {mesh.vertexCount} vertices, {ComprimentoOriginal:0.00} m; shader {mat.shader.name} ({string.Join(",", props)}).");
            return Modelo;
        }

        public static Transform Modelo, ModeloCostas;

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

    /// Liga a linha do fio durante o golpe (quando o jogo liga o rastro da arma).
    public class RastroFio : MonoBehaviour
    {
        static readonly System.Reflection.FieldInfo _emit = HarmonyLib.AccessTools.Field(typeof(MeleeWeaponTrail), "_emit");
        TrailRenderer _tr;
        MeleeWeaponTrail _mw;
        Player _dono;

        void Start()
        {
            _tr = GetComponent<TrailRenderer>();
            _dono = GetComponentInParent<Player>();
            // procura o rastro do jogo dentro da mesma arma (sobe ate o "attach")
            var t = transform.parent;
            for (int i = 0; i < 3 && t != null && _mw == null; i++) { _mw = t.GetComponentInChildren<MeleeWeaponTrail>(true); t = t.parent; }
            if (!CeifadoraPlugin.CfgRastro.Value) _tr.enabled = false;
        }

        void LateUpdate()
        {
            if (_tr == null || !_tr.enabled) return;
            bool liga;
            if (_mw != null && _emit != null) liga = (bool)_emit.GetValue(_mw);
            else liga = _dono != null && _dono.InAttack();
            if (_dono == null) liga = false;
            _tr.emitting = liga;
        }
    }
}
