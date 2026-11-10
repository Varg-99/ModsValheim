using UnityEngine;

namespace CeifadoraObsidiana
{
    /// Dano verdadeiro ao longo do tempo (sangramento), aplicado a quem é atingido.
    public class SE_Sangramento : StatusEffect
    {
        public float m_damagePerTick = 6f;
        public float m_tickInterval = 1f;
        private float m_timer;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (m_character == null || !m_character.IsOwner()) return;

            m_timer += dt;
            if (m_timer < m_tickInterval) return;
            m_timer = 0f;

            var hit = new HitData();
            hit.m_damage.m_damage = m_damagePerTick;
            hit.m_point = m_character.GetCenterPoint();
            hit.m_dir = Vector3.zero;
            m_character.Damage(hit);
        }
    }
}
