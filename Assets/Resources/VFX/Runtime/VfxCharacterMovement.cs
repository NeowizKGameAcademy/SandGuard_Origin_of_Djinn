using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 플레이어 이동 이펙트 (VFX 제작계획 #6 더블 점프 등). CharacterController의 접지·속도 변화만 보고
    /// 점프·공중 점프·착지 프리팹을 발밑에 스폰하므로 이동 코드를 몰라도 된다. 플레이어 루트에 붙인다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class VfxCharacterMovement : MonoBehaviour
    {
        public GameObject JumpPrefab;
        public GameObject AirJumpPrefab;
        public GameObject LandPrefab;
        public float Lifetime = 2f;
        [Tooltip("이 속도 이상으로 위로 튀면 점프로 본다")]
        public float JumpVelocity = 1.5f;
        [Tooltip("이 속도 이상으로 떨어지다 땅에 닿아야 착지 먼지가 난다")]
        public float MinLandSpeed = 2f;
        public Vector3 FeetOffset;

        CharacterController _controller;
        bool _wasGrounded = true;
        float _lastVerticalSpeed;

        void Awake() => _controller = GetComponent<CharacterController>();

        void LateUpdate()
        {
            bool grounded = _controller.isGrounded;
            float vertical = _controller.velocity.y;
            if (_wasGrounded && !grounded && vertical > JumpVelocity) Spawn(JumpPrefab);
            else if (!_wasGrounded && !grounded && vertical > JumpVelocity && vertical - _lastVerticalSpeed > JumpVelocity) Spawn(AirJumpPrefab);
            if (!_wasGrounded && grounded && -_lastVerticalSpeed >= MinLandSpeed) Spawn(LandPrefab);
            _wasGrounded = grounded;
            _lastVerticalSpeed = vertical;
        }

        void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            Destroy(Instantiate(prefab, transform.position + FeetOffset, Quaternion.identity), Lifetime);
        }
    }
}
