using System;
using SandGuard.Skills.Unity;
using System.Collections.Generic;
using DesertTower.Levels;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>슬롯 건설·수리 서비스. 규칙을 검사하고 마나를 낸 뒤 시설을 생성해 슬롯을 점유한다. 건설 시간은 없고 생성 연출 → 완료다.</summary>
    /// <remarks>
    /// 기존 계약(IFacilityBuilder, BuildSlotRegistry, PlacementPhaseRule, SlotPlacementRule)을 그대로 쓴다. 자유 배치는 아직 없다.
    /// 마나는 예약 → 모든 변경 완료 → 차감 순서로 쓴다. 실패하면 예약만 풀고 마나를 바꾸지 않는다. 지갑이 없는 씬(테스트·데모)은 무료다.
    /// 수리는 살아 있는 시설만 한 번에 최대 체력까지. 비용 = 건설비 × 잃은 체력 비율 × repairCostRatio(올림).
    /// 파괴된 시설은 자리를 비우고, 같은 받침에 다시 지을 수 있다. 강화·철거는 아직 없다(Locked).
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    public sealed class FacilityBuildService : MonoBehaviour, IFacilityBuilder, IFacilityMaintenance, IGameStateReader
    {
        public FacilityCatalog catalog;
        [Tooltip("실제 게임에서는 켜 두세요. 끄면 독립 시설 테스트용으로 해금 검사를 생략합니다.")]
        public bool requireSkillUnlock = true;
        public SkillTreeSession skillSession;
        bool sessionWasBound;
        public bool IsUnlocked(string facilityId)
        {
            var definition=catalog!=null?catalog.Find(facilityId):null;
            if(definition==null)return false;
            if(!requireSkillUnlock)return true;
            if(!skillSession && !sessionWasBound)
            {
                SkillTreeSession found=null;int count=0;
                foreach(var s in FindObjectsByType<SkillTreeSession>(FindObjectsSortMode.None))
                    if(s.gameObject.scene==gameObject.scene && s.isActiveAndEnabled){found=s;count++;}
                if(count==1)skillSession=found;
            }
            if(skillSession)sessionWasBound=true;
            return TowerUnlockPolicy.IsUnlocked(skillSession && skillSession.isActiveAndEnabled?skillSession.Service:null,definition.id,definition.requiredSkillId);
        }
        [Tooltip("비우면 씬에서 찾는다")]
        public LevelRoot level;
        [Tooltip("시설 프리팹의 VfxPopIn에 완료 이펙트가 없을 때 쓰는 기본값(VFX_Build_Complete)")]
        public GameObject buildCompleteVfx;
        [Min(0.5f)] public float vfxLifetime = 3f;
        [Tooltip("게임 단계 제공자(IGameStateReader, 예: WaveDirector). 비우면 씬에서 찾고, 없으면 아래 phase를 쓴다")]
        public MonoBehaviour gameStateSource;
        [Tooltip("제공자가 없을 때의 단계. 준비·전투 모두 슬롯 건설을 허용한다")]
        public GamePhase phase = GamePhase.Preparation;
        public bool paused;
        [Tooltip("건설·수리 비용을 낼 마나 지갑(IManaWallet, 예: PlayerManaWallet). 비우면 씬에서 찾고, 없으면 비용 없이 진행한다")]
        public MonoBehaviour manaSource;
        [Min(0f), Tooltip("수리 비용 = 건설비 × 잃은 체력 비율 × 이 값 (올림)")]
        public float repairCostRatio = .5f;
        public event Action Changed;
        public event Action<FacilityAnchor, FacilityViewData> Built;
        /// <summary>수리에 성공했다. (시설, 회복량, 쓴 마나)</summary>
        public event Action<FacilityInstance, float, int> Repaired;
        IGameStateReader Source => gameStateSource as IGameStateReader;
        IManaWallet Wallet => manaSource as IManaWallet;
        public GamePhase Phase => Source != null ? Source.Phase : phase;
        public bool IsPaused => Source != null ? Source.IsPaused : paused;
        public IBuildSlotQuery Slots => registry;
        BuildSlotRegistry registry;
        IPlacementValidator validator;
        readonly Dictionary<string, FacilityAnchor> anchors = new Dictionary<string, FacilityAnchor>(StringComparer.Ordinal);
        readonly Dictionary<Guid, FacilityInstance> facilities = new Dictionary<Guid, FacilityInstance>();

        void Awake()
        {
            if (level == null) level = FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogError("FacilityBuildService: LevelRoot가 없습니다.", this); enabled = false; return; }
            if (gameStateSource == null || manaSource == null)
                foreach (var candidate in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (gameStateSource == null && candidate != this && candidate is IGameStateReader) gameStateSource = candidate;
                    if (manaSource == null && candidate is IManaWallet) manaSource = candidate;
                }
            registry = new BuildSlotRegistry(level);
            validator = new CompositePlacementValidator(new IPlacementRule[] { new PlacementPhaseRule(this), new SlotPlacementRule(registry) });
            foreach (var anchor in FindObjectsByType<FacilityAnchor>(FindObjectsSortMode.None)) Register(anchor);
        }

        public void SetPhase(GamePhase value, bool isPaused = false)
        {
            if (phase == value && paused == isPaused) return;
            phase = value; paused = isPaused; Changed?.Invoke();
        }

        public void Register(FacilityAnchor anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(anchor.SlotId)) return;
            anchors[anchor.SlotId] = anchor;
        }

        public bool TryGetAnchor(string slotId, out FacilityAnchor anchor)
            => anchors.TryGetValue(slotId ?? string.Empty, out anchor) && anchor != null;

        public bool TryGetFacility(Guid facilityId, out FacilityInstance facility)
            => facilities.TryGetValue(facilityId, out facility) && facility != null;

        public BuildResult TryBuild(PlacementRequest request)
        {
            if (registry == null || catalog == null) return BuildResult.Failed(ActionFailure.InvalidRequest, PlacementResult.Denied(PlacementFailure.InvalidRequest));
            // Reject before reservations, spawning or slot occupancy, including direct API calls.
            if (!IsUnlocked(request.FacilityId))
                return BuildResult.Failed(ActionFailure.Locked, PlacementResult.Denied(PlacementFailure.Locked));
            PlacementResult placement = validator.Validate(request);
            if (!placement.CanPlace) return BuildResult.Failed(Map(placement.Failure), placement);
            if (request.Kind != PlacementKind.DesignatedSlot) // 자유 배치는 아직 없다.
                return BuildResult.Failed(ActionFailure.InvalidPlacement, PlacementResult.Denied(PlacementFailure.PlacementModeNotAllowed));
            FacilityDefinition definition = catalog.Find(request.FacilityId);
            if (definition == null || definition.prefab == null)
                return BuildResult.Failed(ActionFailure.NotFound, PlacementResult.Denied(PlacementFailure.FacilityNotAllowed));
            if (!TryGetAnchor(request.SlotId, out FacilityAnchor anchor))
                return BuildResult.Failed(ActionFailure.NotFound, PlacementResult.Denied(PlacementFailure.SlotNotFound));
            if (anchor.IsOccupied) return BuildResult.Failed(ActionFailure.InvalidPlacement, PlacementResult.Denied(PlacementFailure.Occupied));
            registry.TryGetSlot(request.SlotId, out BuildSlotState slot);

            // 마나를 먼저 잡아 둔다. 이후 어느 단계에서 실패해도 예약만 풀리고 마나는 그대로다.
            IManaReservation reservation = null;
            if (definition.manaCost > 0 && Wallet != null && !Wallet.TryReserve(definition.manaCost, out reservation))
                return BuildResult.Failed(ActionFailure.InsufficientMana, PlacementResult.Denied(PlacementFailure.InsufficientMana));
            try
            {
                RemoveBrokenFacilities(anchor);

                // 생성 → 점유 순서. 점유에 실패하면 만든 것을 지워 부분 상태를 남기지 않는다.
                Guid id = Guid.NewGuid();
                GameObject instance = Instantiate(definition.prefab, slot.Position, slot.Rotation, anchor.transform);
                instance.name = definition.id;
                var facility = instance.GetComponent<FacilityInstance>();
                if (facility == null) facility = instance.AddComponent<FacilityInstance>();
                facility.Initialize(id, definition, request.SlotId);
                id = facility.EntityId; // 남의 체력 컴포넌트는 자기 ID를 지키므로 그쪽에 맞춘다.
                if (!registry.TryOccupy(request.SlotId, definition.id, id))
                {
                    Destroy(instance);
                    return BuildResult.Failed(ActionFailure.InvalidPlacement, PlacementResult.Denied(PlacementFailure.Occupied));
                }
                facilities[id] = facility;
                anchor.Occupant = facility;
                // 파괴되면 슬롯을 비워 다시 지을 수 있게 한다.
                if (facility.Life != null)
                {
                    string slotId = request.SlotId;
                    Guid facilityId = id;
                    facility.Life.Despawned += _ =>
                    {
                        registry.Release(slotId, facilityId);
                        facilities.Remove(facilityId);
                        if (anchor != null && anchor.Occupant == facility) anchor.Occupant = null;
                        // 남의 체력 컴포넌트는 자기 오브젝트만 끄고 받침 아래에 껍데기(사거리 원·판정 상자)를 남긴다.
                        // 여기서 뿌리를 꺼 두면 시체 모양이 한 가지로 통일돼 RemoveBrokenFacilities가 다시 지을 때 치운다.
                        if (facility != null && facility.gameObject.activeSelf) facility.gameObject.SetActive(false);
                    };
                }
                else Debug.LogError(definition.id + ": 체력 컴포넌트(IHealth+ILifeState)를 찾지 못해 파괴돼도 자리가 비지 않습니다. " +
                    "SandGuard/Facility/Wire Catalog Towers For Combat 을 실행하세요.", instance);
                if (reservation != null) reservation.TryCommit();
                // "짠" 등장: 연막 → 드러남 + 펀치/플래시 + 완료 이펙트. 프리팹에 VfxPopIn이 없으면 붙이고 서비스의 완료 이펙트를 쓴다.
                var popIn = instance.GetComponent<VfxPopIn>();
                if (popIn == null) popIn = instance.AddComponent<VfxPopIn>();
                if (popIn.RevealPrefab == null) popIn.RevealPrefab = buildCompleteVfx;
                popIn.EffectLifetime = vfxLifetime;
                popIn.Play();
                FacilityViewData view = facility.ViewData;
                Built?.Invoke(anchor, view);
                return BuildResult.Built(view);
            }
            finally { reservation?.Dispose(); }
        }

        /// <summary>
        /// 파괴되어 꺼진 채 남은 본체를 치운다. 파괴 시 본체를 끄기만 하는 시설(받침이 본체를 참조하는 타워)은
        /// 다시 지을 때 옛 본체가 받침 아래에 남아 있다.
        /// </summary>
        static void RemoveBrokenFacilities(FacilityAnchor anchor)
        {
            foreach (var leftover in anchor.GetComponentsInChildren<FacilityInstance>(true))
                if (leftover != null && leftover != anchor.Occupant && !leftover.gameObject.activeSelf) Destroy(leftover.gameObject);
        }

        // ---- 수리 ----

        public MaintenanceQuote GetRepairQuote(Guid facilityId)
        {
            if (!TryGetFacility(facilityId, out var facility)) return Unavailable(ActionFailure.NotFound);
            var health = facility.Health;
            if (health == null) return Unavailable(ActionFailure.InvalidRequest);
            if (facility.Life != null && facility.Life.State != LifeState.Alive) return Unavailable(ActionFailure.NotAlive);
            if (facility.Repairable == null) return Unavailable(ActionFailure.Locked); // 수리를 지원하지 않는 시설
            if (IsPaused) return Unavailable(ActionFailure.Paused);
            if (Phase != GamePhase.Preparation && Phase != GamePhase.Combat) return Unavailable(ActionFailure.WrongPhase);
            float missing = health.MaxHealth - health.CurrentHealth;
            if (missing <= .01f) return Unavailable(ActionFailure.NoChange);
            return new MaintenanceQuote(ActionResult.Success(), RepairCost(facility, missing), missing);
        }

        public ActionResult TryRepair(Guid facilityId)
        {
            MaintenanceQuote quote = GetRepairQuote(facilityId);
            if (!quote.Availability.Succeeded) return quote.Availability;
            TryGetFacility(facilityId, out var facility);
            IManaReservation reservation = null;
            if (quote.ManaAmount > 0 && Wallet != null && !Wallet.TryReserve(quote.ManaAmount, out reservation))
                return ActionResult.Fail(ActionFailure.InsufficientMana);
            try
            {
                float restored = facility.Repairable.Repair(quote.HealthToRestore);
                if (restored <= 0f) return ActionResult.Fail(ActionFailure.NoChange);
                if (reservation != null) reservation.TryCommit();
                Repaired?.Invoke(facility, restored, quote.ManaAmount);
                return ActionResult.Success();
            }
            finally { reservation?.Dispose(); }
        }

        int RepairCost(FacilityInstance facility, float missing)
        {
            var definition = catalog != null ? catalog.Find(facility.definitionId) : null;
            int buildCost = definition != null ? definition.manaCost : 0;
            float ratio = facility.Health.MaxHealth > 0f ? missing / facility.Health.MaxHealth : 0f;
            return Mathf.Max(0, Mathf.CeilToInt(buildCost * ratio * repairCostRatio - .0001f));
        }

        static MaintenanceQuote Unavailable(ActionFailure failure) => new MaintenanceQuote(ActionResult.Fail(failure), 0);

        // 강화·철거는 아직 기획 확정 전이라 막아 둔다.
        public MaintenanceQuote GetDemolitionQuote(Guid facilityId) => Unavailable(ActionFailure.Locked);
        public FacilityUpgradeQuote GetUpgradeQuote(Guid facilityId) => new FacilityUpgradeQuote(ActionResult.Fail(ActionFailure.Locked), 0, 0, 0);
        public ActionResult TryUpgrade(Guid facilityId) => ActionResult.Fail(ActionFailure.Locked);
        public ActionResult TryDemolish(Guid facilityId) => ActionResult.Fail(ActionFailure.Locked);

        static ActionFailure Map(PlacementFailure failure)
        {
            switch (failure)
            {
                case PlacementFailure.Paused: return ActionFailure.Paused;
                case PlacementFailure.WrongPhase: return ActionFailure.WrongPhase;
                case PlacementFailure.InsufficientMana: return ActionFailure.InsufficientMana;
                case PlacementFailure.Locked: return ActionFailure.Locked;
                case PlacementFailure.InvalidRequest: return ActionFailure.InvalidRequest;
                case PlacementFailure.SlotNotFound: return ActionFailure.NotFound;
                default: return ActionFailure.InvalidPlacement;
            }
        }
    }
}
