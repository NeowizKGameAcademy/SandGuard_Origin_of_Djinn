using System;
using System.Collections;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>
    /// 한 자리에 머무는 지역 스킬(모래 소용돌이·사막 폭풍)의 공통 뼈대. 지속 시간 동안 매 프레임 <see cref="Tick"/>을 돌리고,
    /// 끝나면 자식 파티클의 방출을 멈춘 뒤 꼬리가 사라질 시간을 두고 스스로 지운다. 이름이 "OnEnd"로 시작하는 자식 파티클은 반대로 그 순간 재생한다(붕괴·쳐올림 버스트).
    /// PlayerSkillCaster가 빈 오브젝트에 붙여 만들고 돌려 쓴다. 꼬리가 다 사라지면 <see cref="ReleaseHandler"/>로 시전자에게 돌아간다.
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
        /// <summary>풀 반납 담당(PlayerSkillCaster). 연결하면 꼬리가 사라진 뒤 파괴 대신 이 쪽으로 돌아간다.</summary>
        [NonSerialized] public Action<PlayerSandZone> ReleaseHandler;
        /// <summary>풀에서 꺼내 붙인 연출. 반납할 때 같이 돌려보낸다.</summary>
        [NonSerialized] public GameObject VfxInstance;

        /// <summary>풀 재사용: 진행 상태와 지난 사용의 구독을 지운다. 시전자가 꺼낼 때 부른다.</summary>
        public void ResetForReuse()
        {
            Elapsed = 0f; Finished = false; Ended = null;
            OnReset();
        }
        /// <summary>파생 클래스가 모아 둔 집계·캐시를 지운다.</summary>
        protected virtual void OnReset() { }

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

        /// <summary>Collider.ClosestPoint는 오목한 MeshCollider(신전 지형)에서 매 프레임 오류를 찍으므로 그 경우 경계 상자 기준으로 대신한다.</summary>
        public static Vector3 ClosestPointSafe(Collider collider, Vector3 point)
            => collider is MeshCollider mesh && !mesh.convex ? collider.ClosestPointOnBounds(point) : collider.ClosestPoint(point);

        /// <summary>지금 끝낸다. 지속 시간이 다하면 자동으로 불린다.</summary>
        public void End()
        {
            if (Finished) return;
            Finished = true;
            OnEnd();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.gameObject.name.StartsWith("OnEnd", StringComparison.Ordinal)) ps.Play(false);
                else ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            Ended?.Invoke();
            if (ReleaseHandler != null) StartCoroutine(ReleaseAfterTail());
            else Destroy(gameObject, vfxTail);
        }

        /// <summary>꼬리가 사라질 때까지 기다렸다가 반납한다. 그동안 오브젝트는 살아 있으므로 코루틴이 끊기지 않는다.</summary>
        IEnumerator ReleaseAfterTail()
        {
            if (vfxTail > 0f) yield return new WaitForSeconds(vfxTail);
            var handler = ReleaseHandler;
            ReleaseHandler = null;
            handler?.Invoke(this);
        }
    }
}
