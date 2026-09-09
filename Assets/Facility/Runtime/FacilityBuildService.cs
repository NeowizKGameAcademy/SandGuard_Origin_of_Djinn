using System;
using System.Collections.Generic;
using DesertTower.Levels;
using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Facility
{
    /// <summary>슬롯 건설 서비스. 규칙을 검사하고 시설을 생성한 뒤 슬롯을 점유한다. 건설 시간은 없고 생성 연출 → 완료다.</summary>
    /// <remarks>
    /// 기존 계약(IFacilityBuilder, BuildSlotRegistry, PlacementPhaseRule, SlotPlacementRule)을 그대로 쓴다.
    /// 마나 지갑은 아직 없어 비용을 차감하지 않는다. 자유 배치도 아직 없다.
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    public sealed class FacilityBuildService : MonoBehaviour, IFacilityBuilder, IGameStateReader
    {
        public FacilityCatalog catalog;
        [Tooltip("비우면 씬에서 찾는다")]
        public LevelRoot level;
        [Tooltip("시설 프리팹의 VfxPopIn에 완료 이펙트가 없을 때 쓰는 기본값(VFX_Build_Complete)")]
        public GameObject buildCompleteVfx;
        [Min(0.5f)] public float vfxLifetime = 3f;
        [Tooltip("현재 단계. 준비·전투 모두 슬롯 건설을 허용한다")]
        public GamePhase phase = GamePhase.Preparation;
        public bool paused;
        public event Action Changed;
        public event Action<FacilityAnchor, FacilityViewData> Built;
        public GamePhase Phase => phase;
        public bool IsPaused => paused;
        public IBuildSlotQuery Slots => registry;
        BuildSlotRegistry registry;
        IPlacementValidator validator;
        readonly Dictionary<string, FacilityAnchor> anchors = new Dictionary<string, FacilityAnchor>(StringComparer.Ordinal);

        void Awake()
        {
            if (level == null) level = FindFirstObjectByType<LevelRoot>();
            if (level == null) { Debug.LogError("FacilityBuildService: LevelRoot가 없습니다.", this); enabled = false; return; }
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

        public BuildResult TryBuild(PlacementRequest request)
        {
            if (registry == null || catalog == null) return BuildResult.Failed(ActionFailure.InvalidRequest, PlacementResult.Denied(PlacementFailure.InvalidRequest));
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

            // 생성 → 점유 순서. 점유에 실패하면 만든 것을 지워 부분 상태를 남기지 않는다.
            Guid id = Guid.NewGuid();
            GameObject instance = Instantiate(definition.prefab, slot.Position, slot.Rotation, anchor.transform);
            instance.name = definition.id;
            var facility = instance.GetComponent<FacilityInstance>() ?? instance.AddComponent<FacilityInstance>();
            facility.Initialize(id, definition, request.SlotId);
            if (!registry.TryOccupy(request.SlotId, definition.id, id))
            {
                Destroy(instance);
                return BuildResult.Failed(ActionFailure.InvalidPlacement, PlacementResult.Denied(PlacementFailure.Occupied));
            }
            anchor.Occupant = facility;
            // "짠" 등장: 연막 → 드러남 + 펀치/플래시 + 완료 이펙트. 프리팹에 VfxPopIn이 없으면 붙이고 서비스의 완료 이펙트를 쓴다.
            var popIn = instance.GetComponent<VfxPopIn>() ?? instance.AddComponent<VfxPopIn>();
            if (popIn.RevealPrefab == null) popIn.RevealPrefab = buildCompleteVfx;
            popIn.EffectLifetime = vfxLifetime;
            popIn.Play();
            FacilityViewData view = facility.ViewData;
            Built?.Invoke(anchor, view);
            return BuildResult.Built(view);
        }

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
