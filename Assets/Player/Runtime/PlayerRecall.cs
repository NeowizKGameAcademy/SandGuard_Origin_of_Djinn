using System;
using DesertTower.VFX;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    /// <summary>
    /// 흔적 귀환: 지상에서 쓰면 그 자리에 흔적을 남기고, 다시 쓰면 흔적으로 순간이동한다. 흔적은 기본적으로 귀환할 때까지 영구히 남는다(<see cref="window"/> 0).
    /// window를 0보다 크게 두면 그 시간 뒤 스스로 사라진다. 흔적이 사라진(귀환·만료·사망·해금 취소) 순간부터 <see cref="cooldown"/>이 돈다. 스킬트리 실행기(PlayerSkillTreeExecutor)가 호출하고,
    /// 연출·소리는 <see cref="onMarked"/>·<see cref="onRecalled"/>·<see cref="onMarkCleared"/>(UnityEvent)와 같은 이름의 C# 이벤트에 붙인다.
    /// 표식 프리팹(<see cref="markPrefab"/>, VfxRecallMark가 있으면 남은 시간으로 어두워지고 풀릴 때 번지며 사라짐)이 있으면 흔적 자리에 세우고, 없으면 납작한 구를 임시로 놓는다.
    /// 귀환 순간에는 출발점에 <see cref="departPrefab"/>, 도착점에 <see cref="arrivePrefab"/>을 놓고 카메라를 짧게 킥·줌한다.
    /// </summary>
    [DefaultExecutionOrder(215)]
    public sealed class PlayerRecall : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerHealth health;
        [Tooltip("IManaWallet. 비우면 같은 오브젝트에서 찾는다")] public MonoBehaviour manaSource;
        [Min(0)] public int manaCost = 12;
        [Min(0f), Tooltip("흔적이 남아 있는 시간. 0이면 귀환할 때까지 영구히 남는다")] public float window = 0f;
        [Min(0.1f), Tooltip("흔적이 사라진 뒤(귀환·만료) 다시 흔적을 남길 때까지")] public float cooldown = 10f;
        [Tooltip("귀환 지점이 막혔는지 볼 레이어")] public LayerMask obstacles = ~0;
        [Header("연출")]
        [Tooltip("흔적 자리에 세울 프리팹(VFX_Recall_Mark). 비우면 임시 표식(납작한 구)")] public GameObject markPrefab;
        [Min(0f), Tooltip("귀환·만료 뒤 표식 오브젝트를 지우기까지 남겨 두는 시간(파티클 꼬리)")] public float markVfxTail = 1.5f;
        [Tooltip("귀환 순간 출발점에 놓는 프리팹(VFX_Recall_Depart)")] public GameObject departPrefab;
        [Tooltip("귀환 순간 도착점에 놓는 프리팹(VFX_Recall_Arrive)")] public GameObject arrivePrefab;
        [Min(0.1f)] public float effectLifetime = 3f;
        [Tooltip("카메라 킥·줌을 줄 리그. 비우면 자식에서 찾는다")] public PlayerCameraRig cameraRig;
        [Min(0f), Tooltip("귀환 순간 감쇠 흔들림 진폭(m)")] public float recallKick = 0.12f;
        [Min(0.01f)] public float recallKickDuration = 0.2f;
        [Tooltip("귀환 순간 FOV 변화(도). 음수면 순간 좁혔다가 풀린다")] public float recallFovPunch = -6f;
        [Min(0.01f)] public float recallFovDuration = 0.3f;
        public UnityEvent onMarked = new UnityEvent();
        public UnityEvent onRecalled = new UnityEvent();
        [Tooltip("귀환하지 않고 흔적이 사라졌을 때(만료·사망·해금 취소)")] public UnityEvent onMarkCleared = new UnityEvent();
        /// <summary>흔적 위치.</summary>
        public event Action<Vector3> Marked;
        /// <summary>(출발, 도착).</summary>
        public event Action<Vector3, Vector3> Recalled;
        /// <summary>귀환 없이 흔적이 사라졌다.</summary>
        public event Action MarkCleared;

        public bool IsMarked { get; private set; }
        public Vector3 MarkPosition { get; private set; }
        /// <summary>흔적이 사라질 때까지 남은 시간. 흔적이 없으면 0.</summary>
        public float MarkRemaining => IsMarked ? (HasWindow ? Mathf.Max(0f, markExpiry - Time.time) : float.PositiveInfinity) : 0f;
        /// <summary>흔적에 만료 시간이 있는가. 0이면 영구.</summary>
        public bool HasWindow => window > 0f;
        /// <summary>다시 흔적을 남길 수 있을 때까지. 흔적이 있는 동안은 0(귀환 가능).</summary>
        public float CooldownRemaining => IsMarked ? 0f : Mathf.Max(0f, readyAt - Time.time);
        /// <summary>HUD 게이지용 전체 길이.</summary>
        public float TotalCooldown => (HasWindow ? window : 0f) + cooldown;
        public GameObject MarkObject { get; private set; }
        public int MarkCount { get; private set; }
        public int RecallCount { get; private set; }

        float markExpiry, readyAt, fovRemaining;
        CharacterController controller;
        VfxRecallMark markVfx;
        IManaWallet Mana => (manaSource as IManaWallet) ?? (motor != null ? motor.manaSource as IManaWallet : null);
        bool Alive => health == null || health.CurrentHealth > 0f;

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (health == null) health = GetComponent<PlayerHealth>();
            if (manaSource == null) manaSource = GetComponent<IManaWallet>() as MonoBehaviour;
            if (cameraRig == null) cameraRig = GetComponentInChildren<PlayerCameraRig>(true);
            controller = GetComponent<CharacterController>();
        }

        /// <summary>흔적을 남길 수 있는가(쿨다운·접지·마나). HUD가 켜짐 표시에 쓴다.</summary>
        public bool CanMark => !IsMarked && CooldownRemaining <= 0f && motor != null && motor.IsGrounded && !motor.IsDashing
            && Alive && (Mana?.CurrentMana ?? 0) >= manaCost;

        /// <summary>흔적이 있으면 귀환, 없으면 흔적 생성.</summary>
        public ActionResult TryUse() => IsMarked ? TryRecall() : TryMark();

        public ActionResult TryMark()
        {
            if (IsMarked) return ActionResult.Fail(ActionFailure.NoChange);
            if (!Alive) return ActionResult.Fail(ActionFailure.NotAlive);
            if (CooldownRemaining > 0f) return ActionResult.Fail(ActionFailure.Cooldown);
            if (motor == null || !motor.IsGrounded || motor.IsDashing) return ActionResult.Fail(ActionFailure.InvalidPlacement);
            var mana = Mana;
            if (mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            if (!mana.TrySpend(manaCost)) return ActionResult.Fail(ActionFailure.InsufficientMana);
            MarkPosition = transform.position;
            IsMarked = true; MarkCount++;
            markExpiry = HasWindow ? Time.time + window : float.PositiveInfinity;
            SpawnMark();
            Marked?.Invoke(MarkPosition);
            onMarked.Invoke();
            return ActionResult.Success();
        }

        public ActionResult TryRecall()
        {
            if (!IsMarked) return ActionResult.Fail(ActionFailure.NotFound);
            if (!Alive) return ActionResult.Fail(ActionFailure.NotAlive);
            if (Time.time > markExpiry) { Clear(false); return ActionResult.Fail(ActionFailure.Cooldown); }
            if (!DestinationClear()) return ActionResult.Fail(ActionFailure.InvalidPlacement);
            Vector3 from = transform.position, to = MarkPosition;
            motor.Teleport(to);
            RecallCount++;
            Clear(true);
            Spawn(departPrefab, from);
            Spawn(arrivePrefab, to);
            if (cameraRig != null)
            {
                if (recallKick > 0f) cameraRig.Kick(recallKick, recallKickDuration);
                fovRemaining = Mathf.Abs(recallFovPunch) > 0.001f ? recallFovDuration : 0f;
            }
            Recalled?.Invoke(from, to);
            onRecalled.Invoke();
            return ActionResult.Success();
        }

        /// <summary>흔적을 지운다(만료·사망·해금 취소). 쿨다운은 이미 흔적 만료 기준으로 잡혀 있어 그대로 둔다.</summary>
        public void ClearMark() { if (IsMarked) Clear(false); }

        void Clear(bool recalled)
        {
            IsMarked = false;
            readyAt = Time.time + cooldown; // 흔적이 사라진 순간부터 다시 남길 수 있을 때까지
            if (MarkObject != null)
            {
                if (markVfx != null) markVfx.Release();
                else foreach (var ps in MarkObject.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                Destroy(MarkObject, markPrefab != null ? markVfxTail : 0f);
                MarkObject = null; markVfx = null;
            }
            if (!recalled) { MarkCleared?.Invoke(); onMarkCleared.Invoke(); }
        }

        void SpawnMark()
        {
            if (markPrefab != null)
            {
                MarkObject = Instantiate(markPrefab, MarkPosition, Quaternion.identity);
                MarkObject.name = "Recall Mark";
                markVfx = MarkObject.GetComponentInChildren<VfxRecallMark>();
                if (markVfx != null) markVfx.SetRemaining(1f);
                return;
            }
            MarkObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            MarkObject.name = "Recall Mark";
            var col = MarkObject.GetComponent<Collider>(); col.enabled = false; Destroy(col);
            MarkObject.transform.position = MarkPosition + Vector3.up * 0.12f;
            MarkObject.transform.localScale = new Vector3(0.65f, 0.12f, 0.65f);
        }

        void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            var instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, effectLifetime);
        }

        /// <summary>흔적 자리에 플레이어 캡슐이 들어갈 수 있고 발밑에 바닥이 있는가(발판이 사라졌을 수 있다).</summary>
        public bool DestinationClear()
        {
            if (controller == null) return false;
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float radius = Mathf.Max(0.05f, controller.radius * scale - controller.skinWidth);
            float half = Mathf.Max(radius, controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f);
            Vector3 center = MarkPosition + transform.TransformVector(controller.center) + Vector3.up * 0.08f;
            foreach (var c in Physics.OverlapCapsule(center + Vector3.up * (half - radius), center - Vector3.up * (half - radius), radius, obstacles, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(transform)) return false;
            return Physics.Raycast(MarkPosition + Vector3.up * 0.25f, Vector3.down, 0.8f, obstacles, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            if (IsMarked && (Time.time > markExpiry || !Alive)) Clear(false);
            if (IsMarked && markVfx != null) markVfx.SetRemaining(HasWindow ? MarkRemaining / window : 1f);
            if (fovRemaining > 0f && cameraRig != null)
            {
                fovRemaining -= Time.deltaTime;
                cameraRig.AddFov(recallFovPunch * Mathf.Clamp01(fovRemaining / recallFovDuration));
            }
        }

        void OnDisable() { if (IsMarked) Clear(false); fovRemaining = 0f; }
    }
}
