using System;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 한 자리에 머무는 지역 스킬(모래 소용돌이·사막 폭풍)의 공통 뼈대. 지속 시간 동안 매 프레임 <see cref="Tick"/>을 돌리고,
    /// 끝나면 자식 파티클의 방출을 멈춘 뒤 꼬리가 사라질 시간을 두고 스스로 지운다. PlayerSkillCaster가 빈 오브젝트에 붙여 만든다.
    /// </summary>
    public abstract class PlayerSandZone : MonoBehaviour
    {
        [Tooltip("시전자. 자기 콜라이더는 무시한다")] public Transform owner;
        public string faction = "Ally";
        [Min(0.1f)] public float radius = 4f;
        [Min(0.01f)] public float duration = 1.5f;
        public LayerMask mask = ~0;
        [Min(0f), Tooltip("끝난 뒤 연출 꼬리가 사라질 때까지 남겨 두는 시간")] public float vfxTail = 1.2f;
        public float Elapsed { get; protected set; }
        public bool Finished { get; private set; }
        public event Action Ended;

        void Update()
        {
            if (Finished) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Tick(dt);
            Elapsed += dt;
            if (Elapsed >= duration) End();
        }

        /// <summary>매 프레임. Elapsed는 아직 더해지기 전이라 첫 호출은 0이다.</summary>
        protected abstract void Tick(float dt);
        protected virtual void OnEnd() { }

        /// <summary>적대 대상인가. 전투 개체가 아니면(테스트 대역·지형) 통과시키고, 개체면 살아 있고 다른 진영일 때만.</summary>
        protected bool Hostile(Collider collider, out ICombatTarget target)
        {
            target = collider.GetComponentInParent<ICombatTarget>();
            if (owner != null && collider.transform.IsChildOf(owner)) return false;
            return target == null || (target.IsTargetable && target.FactionId != faction);
        }

        protected Collider[] Overlap() => Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        /// <summary>지금 끝낸다. 지속 시간이 다하면 자동으로 불린다.</summary>
        public void End()
        {
            if (Finished) return;
            Finished = true;
            OnEnd();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Ended?.Invoke();
            Destroy(gameObject, vfxTail);
        }
    }
}
