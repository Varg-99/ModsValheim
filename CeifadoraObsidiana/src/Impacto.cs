// Impacto do 3o golpe: o que faz o golpe "doer", independente da animacao.
// - Pausa curta no acerto (o proprio FreezeFrame do Valheim, como nas armas pesadas).
// - Tremida de camera.
// - Efeito e som de batida da marreta (Stagbreaker) no ponto do golpe.
// - Mais empurrao e mais atordoamento (stagger) nos inimigos.
// Ajustes no .cfg, secao [Impacto].

using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CeifadoraObsidiana
{
    internal static class Impacto
    {
        public static ConfigEntry<float> CfgPausa, CfgTremer, CfgEmpurrao, CfgAtordoar;
        public static ConfigEntry<bool> CfgEfeitoMarreta;
        public static bool NoGolpe, Acertou;
        static EffectList _fxMarreta;
        static bool _procurou;

        public static void Init(ConfigFile c)
        {
            CfgPausa = c.Bind("Impacto", "PausaNoAcerto", 0.09f, "3o golpe: segundos que o golpe 'trava' quando acerta alguem (0 = desliga).");
            CfgTremer = c.Bind("Impacto", "TremerCamera", 2.5f, "3o golpe: forca da tremida de camera (0 = desliga). Acertando alguem treme o dobro.");
            CfgEfeitoMarreta = c.Bind("Impacto", "EfeitoDeMarreta", true, "3o golpe: efeito e som de batida da marreta no ponto do golpe.");
            CfgEmpurrao = c.Bind("Impacto", "EmpurraoExtra", 2.0f, "3o golpe: multiplicador do empurrao nos inimigos.");
            CfgAtordoar = c.Bind("Impacto", "AtordoamentoExtra", 2.0f, "3o golpe: multiplicador do atordoamento (stagger) nos inimigos.");
        }

        static EffectList FxMarreta()
        {
            if (_procurou) return _fxMarreta;
            _procurou = true;
            foreach (var nome in new[] { "SledgeStagbreaker", "SledgeIron", "SledgeDemolisher" })
            {
                var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(nome) : null;
                var fx = go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack?.m_triggerEffect : null;
                if (fx != null && fx.m_effectPrefabs != null && fx.m_effectPrefabs.Length > 0) { _fxMarreta = fx; Jotunn.Logger.LogInfo("[CeifadoraObsidiana] Impacto: efeito de " + nome); break; }
            }
            if (_fxMarreta == null) Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Impacto: efeito de marreta nao encontrado.");
            return _fxMarreta;
        }

        // EffectList.Create(pos, rot, ...) com os parametros opcionais que a versao do jogo tiver
        static void CriarEfeito(EffectList fx, Vector3 pos, Quaternion rot)
        {
            try
            {
                var m = AccessTools.Method(typeof(EffectList), "Create");
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                    args[i] = i == 0 ? (object)pos : i == 1 ? (object)rot : (ps[i].HasDefaultValue ? ps[i].DefaultValue : null);
                m.Invoke(fx, args);
            }
            catch (System.Exception e) { Jotunn.Logger.LogWarning("[CeifadoraObsidiana] Impacto: efeito falhou: " + e.Message); }
        }

        static void Mult(Attack a, string campo, float k)
        {
            var t = Traverse.Create(a).Field(campo);
            if (t.FieldExists()) t.SetValue(t.GetValue<float>() * k);
        }

        public static void Antes(Attack a)
        {
            NoGolpe = true; Acertou = false;
            Mult(a, "m_forceMultiplier", Mathf.Max(0f, CfgEmpurrao.Value));
            Mult(a, "m_staggerMultiplier", Mathf.Max(0f, CfgAtordoar.Value));
        }

        public static void Depois(Attack a, Player p)
        {
            NoGolpe = false;
            Mult(a, "m_forceMultiplier", 1f / Mathf.Max(0.0001f, CfgEmpurrao.Value));
            Mult(a, "m_staggerMultiplier", 1f / Mathf.Max(0.0001f, CfgAtordoar.Value));
            var ponto = p.transform.position + p.transform.forward * 1.6f;
            if (CfgEfeitoMarreta.Value && FxMarreta() != null) CriarEfeito(_fxMarreta, ponto, p.transform.rotation);
            if (CfgTremer.Value > 0f && GameCamera.instance != null)
                AccessTools.Method(typeof(GameCamera), "AddShake")?.Invoke(GameCamera.instance, new object[] { ponto, 20f, CfgTremer.Value * (Acertou ? 2f : 1f), false });
            if (Acertou && CfgPausa.Value > 0f)
            {
                var ev = p.GetComponentInChildren<CharacterAnimEvent>();
                if (ev != null) AccessTools.Method(typeof(CharacterAnimEvent), "FreezeFrame")?.Invoke(ev, new object[] { CfgPausa.Value });
            }
            Jotunn.Logger.LogInfo($"[CeifadoraObsidiana] impacto do 3o golpe: {(Acertou ? "acertou" : "no ar")}");
        }
    }

    // Marca quando o 3o golpe acerta algum personagem.
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class Patch_Damage_Impacto
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (!Impacto.NoGolpe || hit == null || Player.m_localPlayer == null || __instance == Player.m_localPlayer) return;
            if (hit.m_attacker == Player.m_localPlayer.GetZDOID()) Impacto.Acertou = true;
        }
    }
}
