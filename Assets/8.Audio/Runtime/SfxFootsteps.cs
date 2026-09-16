using UnityEngine;

namespace SandGuard.Audio
{
    /// <summary>
    /// 이동 거리 기반 발소리. 수평으로 Stride만큼 움직일 때마다 한 걸음을 낸다. 애니메이션 클립을 건드리지 않는다.
    /// 접지는 CharacterController가 있으면 그것, 없으면 발밑 레이캐스트(RequireGround를 끄면 항상 접지로 본다: NavMesh 적).
    /// 표면별 큐(모래/돌)는 후속: 지금은 큐 하나.
    /// </summary>
    public sealed class SfxFootsteps : MonoBehaviour
    {
        public SfxCue Cue;
        [Min(0.1f), Tooltip("한 걸음의 거리(m). 걷기 0.7, 달리기는 자연히 빨라진다")] public float Stride = 0.75f;
        [Min(0f), Tooltip("이보다 느리면 걸음으로 세지 않는다 (m/s)")] public float MinSpeed = 0.6f;
        [Tooltip("공중에서는 내지 않는다")] public bool RequireGround = true;
        [Min(0.05f)] public float GroundProbe = 0.35f;
        public LayerMask GroundMask = ~0;

        public int Steps { get; private set; }
        Vector3 last; float travelled; CharacterController controller;

        void Awake() { controller = GetComponent<CharacterController>(); }
        void OnEnable() { last = transform.position; travelled = 0f; }

        void Update()
        {
            Vector3 now = transform.position;
            Vector3 delta = now - last; delta.y = 0f;
            last = now;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = delta.magnitude / dt;
            if (speed < MinSpeed || (RequireGround && !Grounded())) { travelled = Mathf.Min(travelled, Stride * 0.5f); return; }
            travelled += delta.magnitude;
            if (travelled < Stride) return;
            travelled = 0f;
            Steps++;
            if (Cue != null) SfxPlayer.Play(Cue, now);
        }

        bool Grounded()
        {
            if (controller != null) return controller.isGrounded;
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, GroundProbe + 0.1f, GroundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
