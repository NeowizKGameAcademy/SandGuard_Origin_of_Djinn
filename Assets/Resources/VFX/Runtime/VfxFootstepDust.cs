using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 발밑 먼지 트레일 (VFX 제작계획 #24). 자식 파티클은 Rate over Distance로 내므로, 이 스크립트는 땅에 붙어 충분히
    /// 빠르게 움직일 때만 방출을 켠다. 캐릭터 발 위치에 자식으로 붙인다. 적·플레이어 공용이며 이동 코드에 의존하지 않는다.
    /// </summary>
    public sealed class VfxFootstepDust : MonoBehaviour
    {
        [Tooltip("수평 속도가 이 값 이상일 때만 먼지가 난다")]
        public float MinSpeed = 0.6f;
        [Tooltip("발 아래 이 거리 안에 바닥이 있어야 한다 (점프 중에는 안 남)")]
        public float GroundCheckDistance = 0.35f;
        public LayerMask GroundMask = ~0;
        public bool RequireGround = true;

        ParticleSystem[] _systems;
        Vector3 _last;
        bool _emitting = true;

        void Awake() { _systems = GetComponentsInChildren<ParticleSystem>(true); _last = transform.position; }
        void OnEnable() { _last = transform.position; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 delta = transform.position - _last;
            _last = transform.position;
            float speed = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / dt;
            bool grounded = !RequireGround || Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                GroundCheckDistance + 0.1f, GroundMask, QueryTriggerInteraction.Ignore);
            SetEmitting(speed >= MinSpeed && grounded);
        }

        void SetEmitting(bool value)
        {
            if (value == _emitting) return;
            _emitting = value;
            foreach (var ps in _systems) { var emission = ps.emission; emission.enabled = value; }
        }
    }
}
