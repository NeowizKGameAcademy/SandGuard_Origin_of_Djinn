using System;
using System.Collections;
using DesertTower.Levels;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    /// <summary>
    /// 플레이어가 죽으면 스폰 지점에서 되살린다. 죽은 프레임에는 무력화 상태로 남고 다음 프레임(또는 <see cref="respawnDelay"/> 뒤)에 부활한다.
    /// 스폰 지점은 레벨 에디터 마커에서 읽는다: 시작은 PlayerStart, 부활은 가장 가까운 Respawn 마커(없으면 PlayerStart, 그것도 없으면 시작 위치).
    /// 부활 연출은 아직 정해지지 않았으므로 <see cref="onRespawned"/> / <see cref="Respawned"/>에 연결한다.
    /// </summary>
    public sealed class PlayerRespawner : MonoBehaviour
    {
        [Tooltip("ILifeState와 IRevivable을 구현한 체력 컴포넌트 (PlayerHealth)")]
        public MonoBehaviour lifeSource;
        public PlayerMotor motor;
        [Tooltip("부활 방향에 맞춰 카메라를 돌린다. 비워도 된다")]
        public PlayerCameraRig cameraRig;
        [Tooltip("공격 쿨다운을 회복할 대상. 비우면 같은 오브젝트에서 찾는다")]
        public PlayerBasicAttack attack;
        [Tooltip("Q/E/R 쿨다운을 회복할 대상. 비우면 같은 오브젝트에서 찾는다")]
        public PlayerSkillCaster skills;
        [Tooltip("부활할 때 마나·대시 쿨다운·공격 쿨다운·스킬 쿨다운·공중 점프를 모두 회복한다 (체력은 항상 최대로 회복)")]
        public bool restoreResources = true;
        [Min(0f), Tooltip("사망 뒤 부활까지의 시간(초). 0이면 다음 프레임에 바로 부활한다")]
        public float respawnDelay = DefaultRespawnDelay;
        /// <summary>기획 기본값: 사망 뒤 5초 뒤 부활.</summary>
        public const float DefaultRespawnDelay = 5f;
        [Tooltip("씬의 레벨 마커(PlayerStart / Respawn)를 스폰 지점으로 쓴다. 끄면 씬에 놓인 시작 위치를 쓴다")]
        public bool useLevelMarkers = true;
        [Tooltip("시작할 때 PlayerStart 마커로 이동한다. 기획자가 마커를 옮기면 씬을 다시 만들지 않아도 거기서 시작한다")]
        public bool moveToStartMarker = true;
        [Tooltip("부활에 Respawn 마커를 우선 쓴다. 끄면 항상 PlayerStart에서 부활한다")]
        public bool preferRespawnMarkers = true;
        [Min(0f), Tooltip("마커 위로 띄우는 높이. 바닥에 파묻히지 않게 한다")]
        public float spawnHeight = 0.1f;
        [Tooltip("부활 연출 훅. 부활한 직후 새 위치에서 호출된다")]
        public UnityEvent onRespawned = new UnityEvent();
        /// <summary>(사망 위치, 부활 위치). 부활 연출을 코드로 붙일 때 쓴다.</summary>
        public event Action<Vector3, Vector3> Respawned;
        /// <summary>마커가 없을 때 쓰는 기본 스폰 위치·방향. 시작 시 PlayerStart 마커로 이동하면 그 값으로 바뀐다.</summary>
        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public bool IsRespawning => routine != null;
        /// <summary>부활까지 남은 시간(초). 부활 대기 중이 아니면 0. HUD의 카운트다운에 쓴다.</summary>
        public float RespawnRemaining => routine != null ? Mathf.Max(0f, respawnAt - Time.time) : 0f;
        /// <summary>사망해서 부활 대기를 시작했다. 인자는 이번 대기 시간(초).</summary>
        public event Action<float> RespawnStarted;
        public int RespawnCount { get; private set; }
        ILifeState Life => lifeSource as ILifeState;
        Coroutine routine;
        float respawnAt;

        void Awake()
        {
            SpawnPosition = transform.position; SpawnRotation = transform.rotation;
            if (attack == null) attack = GetComponent<PlayerBasicAttack>();
            if (skills == null) skills = GetComponent<PlayerSkillCaster>();
        }
        void OnEnable() { if (Life != null) Life.Died += OnDied; }
        void OnDisable()
        {
            if (Life != null) Life.Died -= OnDied;
            if (routine != null) { StopCoroutine(routine); routine = null; }
        }
        void Start()
        {
            if (!useLevelMarkers || !moveToStartMarker) return;
            var start = FindMarker(MarkerKind.PlayerStart, transform.position);
            if (start == null) return;
            Resolve(start, out Vector3 position, out Quaternion rotation);
            SpawnPosition = position; SpawnRotation = rotation;
            Place(position, rotation);
        }

        void OnDied(DeathInfo info)
        {
            if (routine == null && isActiveAndEnabled) routine = StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            Vector3 deathPosition = transform.position;
            float delay = respawnDelay;
            respawnAt = Time.time + delay;
            RespawnStarted?.Invoke(delay);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null; // 사망 알림을 받은 쪽이 같은 프레임에 살아난 플레이어를 보지 않게 한다
            Vector3 position = SpawnPosition; Quaternion rotation = SpawnRotation;
            if (useLevelMarkers)
            {
                var marker = preferRespawnMarkers ? FindMarker(MarkerKind.Respawn, deathPosition) : null;
                if (marker == null) marker = FindMarker(MarkerKind.PlayerStart, deathPosition);
                if (marker != null) Resolve(marker, out position, out rotation);
            }
            Place(position, rotation);
            bool revived = (lifeSource as IRevivable)?.TryRevive() ?? false;
            routine = null;
            if (!revived) { Debug.LogWarning("PlayerRespawner: 체력 컴포넌트가 부활을 거부했습니다.", this); yield break; }
            if (restoreResources) RestoreResources();
            RespawnCount++;
            Respawned?.Invoke(deathPosition, position);
            onRespawned.Invoke();
        }

        /// <summary>마나를 최대로 채우고 대시·공격·스킬 쿨다운을 지운다. 공중 점프 횟수는 이동 시 이미 복구된다.</summary>
        void RestoreResources()
        {
            var wallet = (motor != null ? motor.manaSource : null) as IManaWallet ?? GetComponent<IManaWallet>();
            if (wallet != null && wallet.CurrentMana < wallet.MaxMana) wallet.Gain(wallet.MaxMana - wallet.CurrentMana);
            if (motor != null) motor.ResetDashCooldown();
            if (attack != null) attack.ResetCooldown();
            if (skills != null) skills.ResetCooldowns();
        }

        /// <summary>마커 위치에 spawnHeight를 더한다. 마커가 회전되어 있으면 그 Y 회전을 시작 방향으로 쓰고, 아니면 지금 방향을 유지한다.</summary>
        void Resolve(LevelMarker marker, out Vector3 position, out Quaternion rotation)
        {
            position = marker.transform.position + Vector3.up * spawnHeight;
            float yaw = marker.transform.eulerAngles.y;
            rotation = Quaternion.Angle(marker.transform.rotation, Quaternion.identity) > 0.5f ? Quaternion.Euler(0f, yaw, 0f) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        void Place(Vector3 position, Quaternion rotation)
        {
            if (motor != null) motor.Teleport(position);
            else
            {
                var controller = GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (controller != null) controller.enabled = false;
                transform.position = position;
                if (controller != null) controller.enabled = wasEnabled;
            }
            transform.rotation = rotation;
            if (cameraRig != null) cameraRig.SetYaw(rotation.eulerAngles.y);
        }

        /// <summary>같은 종류의 마커 중 기준 위치에서 가장 가까운 것. 비활성 마커는 제외한다.</summary>
        static LevelMarker FindMarker(MarkerKind kind, Vector3 near)
        {
            LevelMarker best = null; float bestDistance = float.MaxValue;
            foreach (var marker in FindObjectsByType<LevelMarker>(FindObjectsSortMode.None))
            {
                if (marker.kind != kind || !marker.isActiveAndEnabled) continue;
                float distance = (marker.transform.position - near).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = marker; }
            }
            return best;
        }
    }
}
