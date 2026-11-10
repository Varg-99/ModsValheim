// Bastao simples de madeira no lugar da foice: as animacoes do Wukong sao feitas
// para um bastao, entao assim da para ver como elas realmente ficam.
// A posicao e a direcao da haste sao copiadas da atgeir de metal negro,
// para o bastao ficar na mao como uma atgeir.

using System.Linq;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Bastao
    {
        public static BepInEx.Configuration.ConfigEntry<bool> CfgEncaixe;
        public static BepInEx.Configuration.ConfigEntry<float> CfgAoLongo, CfgMaxMaos, CfgComprimento, CfgPalma;
        public static float ComprimentoPadrao = 2.8f;

        public static void Init(BepInEx.Configuration.ConfigFile c)
        {
            CfgEncaixe = c.Bind("Bastao", "EncaixarNasDuasMaos", true,
                "Quando as duas maos estao perto, o bastao passa pelas duas (fica encaixado). Pode editar com o jogo aberto.");
            CfgAoLongo = c.Bind("Bastao", "DeslizarNaHaste", 0f,
                "Desliza o bastao ao longo da haste (metros). Positivo = mais bastao saindo do lado da mao esquerda; negativo = do lado da direita.");
            CfgMaxMaos = c.Bind("Bastao", "DistanciaMaximaMaos", 0.9f,
                "Distancia maxima (m) entre as maos para considerar que as duas seguram o bastao.");
            CfgPalma = c.Bind("Bastao", "PontoDaPalma", 0.6f,
                "Onde o bastao passa na mao: 0 = no pulso, 1 = nos nos dos dedos.");
            CfgComprimento = c.Bind("Bastao", "Comprimento", 0f,
                "Comprimento do bastao (m). 0 = tamanho da haste da atgeir. Pode editar com o jogo aberto.");
        }

        public static void Trocar(GameObject prefab, GameObject atgeir, float comprimento)
        {
            var attach = prefab.transform.Find("attach");
            var attachA = atgeir.transform.Find("attach");
            if (attach == null || attachA == null)
            {
                Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Bastao: 'attach' nao encontrado; mantendo a foice.");
                return;
            }

            // Caixa da atgeir no espaco do seu 'attach': o eixo mais longo e a haste.
            Bounds? caixa = null;
            foreach (var mf in attachA.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var m = attachA.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (caixa == null) caixa = new Bounds(p, Vector3.zero);
                    else { var bb = caixa.Value; bb.Encapsulate(p); caixa = bb; }
                }
            }
            if (caixa == null) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Bastao: atgeir sem malha."); return; }
            var cx = caixa.Value;
            var tam = cx.size;
            Vector3 eixo = tam.x >= tam.y && tam.x >= tam.z ? Vector3.right : (tam.y >= tam.z ? Vector3.up : Vector3.forward);
            float len = comprimento > 0.1f ? comprimento : Mathf.Max(tam.x, tam.y, tam.z);
            ComprimentoPadrao = Mathf.Max(tam.x, tam.y, tam.z);

            // Material: copia o da foice, sem textura, cor de madeira.
            var r0 = attach.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.sharedMaterial != null);
            Material mat = null;
            if (r0 != null)
            {
                mat = new Material(r0.sharedMaterial) { name = "bastao_madeira" };
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
                if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.42f, 0.26f, 0.13f));
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            // Esconde a foice na mao e no chao.
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // Sem o rastro largo da lamina da foice (o bastao nao tem lamina).
            foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name == "MeleeWeaponTrail") mb.enabled = false;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "bastao";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(attach, false);
            go.transform.localPosition = cx.center;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, eixo);
            go.transform.localScale = new Vector3(0.045f, len * 0.5f, 0.045f); // cilindro padrao: 2 m de altura
            go.layer = attach.gameObject.layer;
            var rend = go.GetComponent<MeshRenderer>();
            if (mat != null) rend.sharedMaterial = mat;
            go.AddComponent<BastaoNasMaos>();

            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] Bastao de madeira: {len:0.00} m, eixo {eixo}, centro {cx.center}.");
        }
    }

    /// Encaixa o bastao nas duas maos a cada quadro (depois da animacao).
    public class BastaoNasMaos : MonoBehaviour
    {
        Animator _anim;
        Transform _r, _l, _rd, _ld;
        Vector3 _pos0; Quaternion _rot0; bool _tem0;

        static Vector3 Palma(Transform mao, Transform dedo)
        {
            if (dedo == null) return mao.position;
            return Vector3.Lerp(mao.position, dedo.position, Bastao.CfgPalma.Value);
        }

        void LateUpdate()
        {
            if (!_tem0) { _pos0 = transform.localPosition; _rot0 = transform.localRotation; _tem0 = true; }
            transform.localPosition = _pos0;
            transform.localRotation = _rot0;
            float len = Bastao.CfgComprimento.Value > 0.1f ? Bastao.CfgComprimento.Value : Bastao.ComprimentoPadrao;
            var esc = transform.localScale; esc.y = len * 0.5f; transform.localScale = esc;

            if (_anim == null)
            {
                _anim = GetComponentInParent<Animator>();
                if (_anim == null || !_anim.isHuman) { _anim = null; return; }
                _r = _anim.GetBoneTransform(HumanBodyBones.RightHand);
                _l = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                _rd = _anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                _ld = _anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            }
            if (_r == null || _l == null || !Bastao.CfgEncaixe.Value) return;

            var pr = Palma(_r, _rd);
            var pl = Palma(_l, _ld);
            var d = pl - pr;
            float dist = d.magnitude;
            float max = Bastao.CfgMaxMaos.Value;
            float w = Mathf.Clamp01((max + 0.2f - dist) / 0.2f);
            if (w <= 0f || dist < 0.01f) return;
            d /= dist;

            var alvoPos = (pr + pl) * 0.5f + d * Bastao.CfgAoLongo.Value;
            var atual = transform.up;
            var alvoRot = Quaternion.FromToRotation(Vector3.up, Vector3.Dot(atual, d) >= 0 ? d : -d);
            transform.position = Vector3.Lerp(transform.position, alvoPos, w);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvoRot, w);
        }
    }
}
