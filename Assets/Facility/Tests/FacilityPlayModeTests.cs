#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DesertTower.Levels;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Facility.Tests
{
    /// <summary>받침대 접근 → 메뉴 → 번호 선택 → 생성 연출 → 점유의 흐름을 실제 프리팹으로 확인하고 렌더를 남긴다.</summary>
    public sealed class FacilityPlayModeTests
    {
        const string Captures = "Docs/model-art/facility-v1/captures";
        readonly List<GameObject> objects = new List<GameObject>();
        GameObject Track(GameObject value) { objects.Add(value); return value; }

        [SetUp] public void Setup() { Time.timeScale = 1f; }

        /// <summary>받침이 "시설이 있을 때만 켠다"고 지정한 오브젝트들이 기대한 상태인지 확인한다.</summary>
        /// <remarks>
        /// 예전에는 transform.Find("Range Circle")로 봤는데, 담당자가 Tower Base.prefab에서 그 자식을 없애
        /// 테스트가 NRE로 죽었다. 이름이 아니라 FacilityAnchor가 실제로 들고 있는 참조로 본다.
        /// 지금은 지정된 오브젝트가 없다(사거리 표시가 타워 본체의 RangeVisualizer로 옮겨 갔다). 그러면 확인할 것도 없다.
        /// </remarks>
        static void AssertOccupiedVisuals(FacilityAnchor anchor, bool expectedOn)
        {
            foreach (var value in anchor.onlyWhenOccupied)
            {
                Assert.NotNull(value, "받침의 onlyWhenOccupied 참조가 끊겼다.");
                Assert.AreEqual(expectedOn, value.activeSelf, "점유 시에만 켜는 오브젝트의 상태가 다르다: " + value.name);
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            foreach (var leftover in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.Destroy(leftover.transform.root.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator ApproachOpensMenuAndNumberBuildsCobra()
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(30, 1, 30);
            ground.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.30f, .26f, .19f) };
            var sun = Track(new GameObject("Sun")).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .58f);
            var camera = Track(new GameObject("Main Camera")).AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(2.5f, 3f, -5f); camera.transform.LookAt(new Vector3(0, .8f, 0));

            // 레벨: 슬롯 하나. 받침대는 슬롯 아래, 슬롯 크기 1.4에 맞춘 배율.
            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            var slotObject = new GameObject("BuildSlot_Test"); slotObject.transform.SetParent(level.transform, false);
            var slot = slotObject.AddComponent<LevelBuildSlot>();
            slot.id = "slot-test"; slot.occupancySurfaceId = "test"; slot.allowedFacilityIds = new List<string> { "cobra" }; slot.footprint = new Vector2(1.4f, 1.4f);
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/Towers/TowerBaseAnchor.prefab");
            var servicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildService.prefab");
            var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildMenu.prefab");
            Assert.NotNull(anchorPrefab, "Run SandGuard/Facility/Create Missing Assets first.");
            Assert.NotNull(servicePrefab); Assert.NotNull(menuPrefab);
            var anchor = Object.Instantiate(anchorPrefab, slotObject.transform).GetComponent<FacilityAnchor>();
            anchor.transform.localScale = Vector3.one * (1.4f / 4f);
            var service = Track(Object.Instantiate(servicePrefab)).GetComponent<FacilityBuildService>();
            service.requireSkillUnlock = false; // 스킬트리 해금은 이 테스트의 관심사가 아니다.
            var menu = Track(Object.Instantiate(menuPrefab)).GetComponent<FacilityBuildMenu>();
            var player = Track(new GameObject("Dummy Player")).transform;
            player.position = new Vector3(0, 0, 12f);
            menu.player = player;
            yield return new WaitForSeconds(.3f);
            Assert.False(menu.IsOpen, "Far away the menu must stay closed.");

            // 오버레이 캔버스는 RenderTexture에 찍히지 않으므로 캡처용으로 카메라 모드로 바꾼다.
            menu.Canvas.renderMode = RenderMode.ScreenSpaceCamera; menu.Canvas.worldCamera = camera; menu.Canvas.planeDistance = 1f;
            player.position = new Vector3(0, 0, 2f);
            yield return new WaitForSeconds(.3f);
            Assert.True(menu.IsOpen, "Within the interaction radius the menu must open.");
            Assert.AreEqual(anchor, menu.Current);
            Capture(anchor.transform.position, "menu-open");
            // 카탈로그에 있지만 이 슬롯이 허용하지 않는 시설. (카탈로그에 없는 ID는 해금 검사에서 Locked로 먼저 걸린다.)
            var wrong = service.TryBuild(PlacementRequest.AtSlot("obelisk", "slot-test"));
            Assert.AreEqual(PlacementFailure.FacilityNotAllowed, wrong.Placement.Failure, "Slots only accept listed facilities.");
            Assert.False(anchor.IsOccupied);

            menu.Select(0);
            Assert.True(menu.LastResult.Outcome.Succeeded, "Pressing 1 must build: " + menu.LastResult.Placement.Failure);
            Assert.AreEqual("cobra", menu.LastResult.Facility.Value.DefinitionId);
            Assert.AreEqual("slot-test", menu.LastResult.Facility.Value.BuildSlotId);
            var tower = anchor.transform.Find("cobra");
            Assert.NotNull(tower, "The cobra must be a child of the anchor.");
            Assert.NotNull(tower.GetComponent<FacilityInstance>());
            var popIn = tower.GetComponent<VfxPopIn>();
            Assert.NotNull(popIn, "Spawn plays the pop-in.");
            Assert.True(popIn.IsPlaying);
            Assert.NotNull(GameObject.Find("VFX_Build_Poof(Clone)"), "The poof must cover the slot first.");
            Assert.False(tower.GetComponentInChildren<Renderer>().enabled, "The facility stays hidden until the reveal.");
            Assert.True(anchor.IsOccupied); Assert.False(menu.IsOpen);
            Assert.True(service.Slots.TryGetSlot("slot-test", out var state) && state.OccupantId.HasValue, "The registry must record the occupant.");
            yield return null; yield return null;
            Capture(anchor.transform.position, "poof");
            // 배치 모드는 프레임이 느려 고정 대기로는 펀치 순간을 놓친다. 연출이 끝날 때까지 매 프레임 상태를 본다.
            bool revealed = false, revealVfx = false; float maxScale = 1f; float deadline = Time.time + 3f;
            while (popIn.IsPlaying && Time.time < deadline)
            {
                if (!revealed && tower.GetComponentInChildren<Renderer>().enabled) { revealed = true; Capture(anchor.transform.position, "reveal"); }
                if (revealed)
                {
                    maxScale = Mathf.Max(maxScale, tower.localScale.x);
                    revealVfx |= GameObject.Find("VFX_Build_Complete(Clone)") != null;
                }
                yield return null;
            }
            Assert.False(popIn.IsPlaying, "The pop-in must finish.");
            Assert.True(revealed, "After the delay the facility is revealed.");
            Assert.True(revealVfx, "Reveal VFX must spawn.");
            Assert.Greater(maxScale, 1.02f, "The reveal punches the scale above 1.");
            Assert.AreEqual(1f, tower.localScale.x, .01f, "The punch must settle at the prefab scale.");
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(1.4f, anchor.transform.Find("Mesh").GetComponent<Renderer>().bounds.size.x, .15f, "Base width should match the slot footprint.");
            Capture(anchor.transform.position, "complete");

            var again = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-test"));
            Assert.False(again.Outcome.Succeeded); Assert.AreEqual(PlacementFailure.Occupied, again.Placement.Failure);
            player.position = new Vector3(0, 0, 12f);
            yield return new WaitForSeconds(.3f);
            Assert.False(menu.IsOpen, "An occupied anchor never reopens the menu.");
        }

        [UnityTest] public IEnumerator BuiltCobraIsACombatTargetAndFreesItsSlotWhenDestroyed()
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(30, 1, 30);
            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            var slotObject = new GameObject("BuildSlot_Test"); slotObject.transform.SetParent(level.transform, false);
            var slot = slotObject.AddComponent<LevelBuildSlot>();
            slot.id = "slot-test"; slot.occupancySurfaceId = "test"; slot.allowedFacilityIds = new List<string> { "cobra" }; slot.footprint = new Vector2(1.4f, 1.4f);
            var anchor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/Towers/TowerBaseAnchor.prefab"), slotObject.transform).GetComponent<FacilityAnchor>();
            anchor.transform.localScale = Vector3.one * (1.4f / 4f);
            var service = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildService.prefab"))).GetComponent<FacilityBuildService>();
            service.requireSkillUnlock = false; // 스킬트리 해금은 이 테스트의 관심사가 아니다.
            yield return null;

            var built = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-test"));
            Assert.True(built.Outcome.Succeeded, "Build failed: " + built.Placement.Failure);
            // 체력 컴포넌트는 구체 타입이 아니라 인터페이스로 본다. 시설 팀(FacilityHealth)과 타워 팀(TowerHealth)이
            // 서로 다른 어셈블리에 있어, 카탈로그가 어느 쪽 프리팹을 가리켜도 통해야 한다.
            var health = anchor.Occupant.Health;
            Assert.NotNull(health, "Run SandGuard/Facility/Wire Catalog Towers For Combat first.");
            ICombatTarget target = health as ICombatTarget;
            Assert.NotNull(target, "The health component must also be the combat target.");
            Assert.AreEqual(CombatTargetKind.Tower, target.Kind);
            Assert.AreEqual("Ally", target.FactionId);
            Assert.AreEqual(built.Facility.Value.EntityId, target.EntityId, "Combat ID must match the facility ID.");
            Assert.AreSame(health, target.DamageReceiver, "The combat target takes damage on the health component.");

            // 적의 탐색과 같은 방식(트리거 제외 OverlapSphere → 부모의 ICombatTarget)으로 찾혀야 한다.
            yield return new WaitForSeconds(.5f);
            bool found = false;
            foreach (var collider in Physics.OverlapSphere(new Vector3(0, .5f, 3f), 4f, ~0, QueryTriggerInteraction.Ignore))
                found |= ReferenceEquals(collider.GetComponentInParent<ICombatTarget>(), target);
            Assert.True(found, "The tower needs a non-trigger collider that resolves to its ICombatTarget.");

            float max = health.MaxHealth;
            var damageable = (IDamageable)health;
            var body = ((Component)health).gameObject;
            Assert.AreEqual(DamageStatus.NonHostile, damageable.TakeDamage(new DamageInfo(10f, "Ally", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward)).Status);
            var hit = damageable.TakeDamage(new DamageInfo(40f, "Enemy", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward));
            Assert.True(hit.WasApplied);
            Assert.NotNull(GameObject.Find("VFX_Facility_Hit(Clone)"), "A hit on the tower plays the facility hit VFX.");
            Assert.AreEqual(max - 40f, health.CurrentHealth, .01f);
            Assert.AreEqual(max - 40f, anchor.Occupant.ViewData.CurrentHealth, .01f, "View data reads the live health.");

            var kill = damageable.TakeDamage(new DamageInfo(max, "Enemy", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward));
            Assert.True(kill.WasKilled);
            Assert.False(target.IsTargetable);
            yield return new WaitForSeconds(1.2f); // 제거 지연(양쪽 구현 모두 0.5초)보다 넉넉히 기다린다.
            Assert.False(body.activeInHierarchy, "A broken tower body is switched off (the owner's base keeps reading it).");
            Assert.True(anchor != null && anchor.gameObject.activeInHierarchy, "Only the body breaks; the base (anchor) stays.");
            Assert.False(anchor.IsOccupied, "Destroying the tower frees the anchor.");
            Assert.True(service.Slots.TryGetSlot("slot-test", out var state) && !state.OccupantId.HasValue, "Destroying the tower frees the slot.");
            Assert.True(service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-test")).Outcome.Succeeded, "The slot can be built again.");
        }

        // ---- 담당자 타워 설치·수리 ----

        /// <summary>바닥, 레벨, 받침 앵커 여러 개(슬롯 크기 4 = 원본 크기), 선택적으로 마나 지갑, 건설 서비스.</summary>
        FacilityBuildService TowerLevel(int slots, bool withWallet, out FacilityAnchor[] anchors, out SandGuard.Player.PlayerManaWallet wallet)
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            wallet = withWallet ? Track(new GameObject("Wallet")).AddComponent<SandGuard.Player.PlayerManaWallet>() : null;
            if (wallet != null)
            {
                // 지갑 기본값 100은 코브라 한 채(100)로 바닥난다. 짓고 수리까지 볼 수 있게 200으로 올린다.
                var walletFields = new UnityEditor.SerializedObject(wallet);
                walletFields.FindProperty("maxMana").intValue = 200;
                walletFields.FindProperty("startingMana").intValue = 200;
                walletFields.ApplyModifiedPropertiesWithoutUndo();
                wallet.ResetWallet();
            }
            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/Towers/TowerBaseAnchor.prefab");
            Assert.NotNull(anchorPrefab, "Run SandGuard/Facility/Create Tower Build Assets (from Tower.unity) first.");
            anchors = new FacilityAnchor[slots];
            for (int i = 0; i < slots; i++)
            {
                var slotObject = new GameObject("BuildSlot_" + i); slotObject.transform.SetParent(level.transform, false);
                slotObject.transform.position = new Vector3(i * 10f, 0f, 0f);
                var slot = slotObject.AddComponent<LevelBuildSlot>();
                slot.id = "slot-" + i; slot.occupancySurfaceId = "test"; slot.footprint = new Vector2(4f, 4f);
                slot.allowedFacilityIds = new List<string> { "cobra", "obelisk" };
                anchors[i] = Object.Instantiate(anchorPrefab, slotObject.transform).GetComponent<FacilityAnchor>();
            }
            var service = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildService.prefab"))).GetComponent<FacilityBuildService>();
            service.requireSkillUnlock = false; // 스킬트리 해금은 이 테스트들의 관심사가 아니다(씬에 SkillTreeSession이 없다).
            return service;
        }

        [UnityTest] public IEnumerator BuildingATowerSpendsManaAndFailsCleanlyWithoutEnough()
        {
            var service = TowerLevel(3, true, out var anchors, out var wallet);
            yield return null;
            Assert.AreEqual(200, wallet.CurrentMana);
            Assert.AreEqual(100, service.catalog.Find("cobra").manaCost, "Designed cobra cost.");
            Assert.AreEqual(55, service.catalog.Find("obelisk").manaCost, "Temporary obelisk cost.");
            AssertOccupiedVisuals(anchors[0], false); // 빈 받침

            var cobra = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-0"));
            Assert.True(cobra.Outcome.Succeeded, "Cobra build failed: " + cobra.Outcome.Failure);
            Assert.AreEqual(100, wallet.CurrentMana, "Building spends the cobra cost.");
            Assert.NotNull(anchors[0].transform.Find("cobra"), "The cobra body is spawned on the base.");
            Assert.NotNull(anchors[0].Occupant.Health, "The body keeps its combat health.");
            AssertOccupiedVisuals(anchors[0], true); // 본체가 서면 켜진다

            Assert.True(service.TryBuild(PlacementRequest.AtSlot("obelisk", "slot-1")).Outcome.Succeeded);
            Assert.AreEqual(45, wallet.CurrentMana);

            var broke = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-2"));
            Assert.False(broke.Outcome.Succeeded);
            Assert.AreEqual(ActionFailure.InsufficientMana, broke.Outcome.Failure);
            Assert.AreEqual(45, wallet.CurrentMana, "A failed build leaves mana untouched.");
            Assert.False(anchors[2].IsOccupied);
            Assert.True(anchors[2].GetComponentInChildren<FacilityInstance>(true) == null, "No body is left behind.");
            Assert.True(service.Slots.TryGetSlot("slot-2", out var state) && !state.OccupantId.HasValue, "The slot stays free.");
        }

        [UnityTest] public IEnumerator RepairingADamagedTowerCostsManaAndDemolishingRefundsHalf()
        {
            var service = TowerLevel(1, true, out var anchors, out var wallet);
            var menu = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildMenu.prefab"))).GetComponent<FacilityBuildMenu>();
            var player = Track(new GameObject("Dummy Player")).transform;
            player.position = new Vector3(0f, 0f, 20f);
            menu.player = player;
            yield return null;

            Assert.True(service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-0")).Outcome.Succeeded);
            var facility = anchors[0].Occupant;
            var health = facility.Health;
            Assert.NotNull(health, "Run SandGuard/Facility/Wire Catalog Towers For Combat first.");
            Assert.NotNull(facility.Repairable, "A tower must be repairable.");
            var damageable = (IDamageable)health;
            // 최대 체력의 주인은 프리팹(타워 팀은 TowerStatus.maxHP)이므로 절대값을 박지 않고 비율로 본다.
            float max = health.MaxHealth;
            float half = max * .5f;
            Assert.AreEqual(ActionFailure.NoChange, service.GetRepairQuote(facility.EntityId).Availability.Failure, "A full-health tower needs no repair.");

            player.position = new Vector3(0f, 0f, 2.5f);
            yield return new WaitForSeconds(.2f);
            Assert.True(menu.IsOpen, "A standing tower opens the repair/demolish menu even at full health.");
            Assert.AreEqual(FacilityBuildMenu.MenuMode.Repair, menu.Mode);

            damageable.TakeDamage(new DamageInfo(half, "Enemy"));
            var quote = service.GetRepairQuote(facility.EntityId);
            Assert.True(quote.Availability.Succeeded);
            int repairCost = Mathf.CeilToInt(service.catalog.Find("cobra").manaCost * .5f * service.repairCostRatio - .0001f);
            Assert.AreEqual(repairCost, quote.ManaAmount, "Cost = build cost × half health lost × repairCostRatio.");
            Assert.AreEqual(half, quote.HealthToRestore, .01f);
            yield return new WaitForSeconds(.2f);
            Assert.True(menu.IsOpen, "A damaged tower keeps the repair menu open.");

            int manaBefore = wallet.CurrentMana;
            int repaired = 0; service.Repaired += (f, amount, cost) => repaired++;
            menu.Select(0);
            Assert.True(menu.LastRepairResult.Succeeded, "Repair failed: " + menu.LastRepairResult.Failure);
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, "Repair restores full health at once.");
            Assert.AreEqual(manaBefore - repairCost, wallet.CurrentMana);
            Assert.AreEqual(1, repaired);
            yield return new WaitForSeconds(.2f);
            Assert.True(menu.IsOpen, "The menu stays open for demolishing once the tower is whole.");

            damageable.TakeDamage(new DamageInfo(max - 1f, "Enemy"));
            wallet.TrySpend(wallet.CurrentMana - 3);
            Assert.AreEqual(ActionFailure.InsufficientMana, service.TryRepair(facility.EntityId).Failure);
            Assert.AreEqual(1f, health.CurrentHealth, .01f, "A failed repair changes nothing.");
            Assert.AreEqual(3, wallet.CurrentMana);

            // 철거: 건설비의 절반(내림)을 돌려주고 받침을 비운다. 메뉴는 건설 모드로 다시 열린다.
            int refund = service.catalog.Find("cobra").manaCost / 2;
            Assert.AreEqual(refund, service.GetDemolitionQuote(facility.EntityId).ManaAmount);
            string demolishedId = null; int refunded = -1;
            service.Demolished += (a, id, mana) => { demolishedId = id; refunded = mana; };
            var facilityObject = facility.gameObject;
            yield return new WaitForSeconds(.2f);
            menu.Select(1);
            Assert.True(menu.LastDemolishResult.Succeeded, "Demolish failed: " + menu.LastDemolishResult.Failure);
            Assert.AreEqual(3 + refund, wallet.CurrentMana, "Demolishing refunds half the build cost.");
            Assert.AreEqual("cobra", demolishedId); Assert.AreEqual(refund, refunded);
            Assert.False(anchors[0].IsOccupied, "Demolishing frees the base.");
            Assert.True(service.Slots.TryGetSlot("slot-0", out var freed) && !freed.OccupantId.HasValue, "Demolishing frees the slot.");
            Assert.AreEqual(ActionFailure.NotFound, service.TryDemolish(facility.EntityId).Failure, "A demolished tower cannot be demolished twice.");
            yield return new WaitForSeconds(.2f);
            Assert.True(facilityObject == null, "The demolished body is destroyed.");
            Assert.True(menu.IsOpen); Assert.AreEqual(FacilityBuildMenu.MenuMode.Build, menu.Mode, "The empty base offers building again.");
            AssertOccupiedVisuals(anchors[0], false);
        }

        [UnityTest] public IEnumerator ABrokenTowerFreesItsBaseAndRebuildingReplacesTheBrokenBody()
        {
            var service = TowerLevel(1, false, out var anchors, out _);
            yield return null;
            Assert.True(service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-0")).Outcome.Succeeded);
            var broken = anchors[0].Occupant;
            var brokenId = broken.EntityId;
            ((IDamageable)broken.Health).TakeDamage(new DamageInfo(broken.Health.MaxHealth * 10f, "Enemy"));
            Assert.AreEqual(ActionFailure.NotAlive, service.GetRepairQuote(brokenId).Availability.Failure, "A dying tower cannot be repaired.");
            yield return new WaitForSeconds(1.2f); // 제거 지연(양쪽 구현 모두 0.5초)보다 넉넉히 기다린다.
            Assert.False(anchors[0].IsOccupied, "Breaking frees the base.");
            Assert.False(broken.gameObject.activeSelf, "The broken body is switched off, not destroyed (the base still reads it).");
            AssertOccupiedVisuals(anchors[0], false); // 본체가 부서지면 꺼진다
            Assert.AreEqual(ActionFailure.NotFound, service.GetRepairQuote(brokenId).Availability.Failure, "Broken towers are rebuilt, not repaired.");

            var rebuilt = service.TryBuild(PlacementRequest.AtSlot("obelisk", "slot-0"));
            Assert.True(rebuilt.Outcome.Succeeded, "Rebuild failed: " + rebuilt.Outcome.Failure);
            yield return null;
            Assert.True(broken == null, "Rebuilding removes the old broken body.");
            Assert.NotNull(anchors[0].transform.Find("obelisk"));
            AssertOccupiedVisuals(anchors[0], true);
        }

        /// <summary>
        /// 카탈로그가 실제로 짓는 프리팹 전부가 건설 시스템과 맞물리는지 본다.
        /// </summary>
        /// <remarks>
        /// 다른 테스트는 특정 프리팹 경로를 직접 열기 때문에, 카탈로그를 다른 프리팹으로 갈아 끼우면
        /// 테스트는 초록인데 인게임만 깨지는 일이 생긴다(2026-09-21에 실제로 그랬다).
        /// 여기서는 카탈로그를 따라가며 체력·수리·정지 배선이 빠지지 않았는지 확인한다.
        /// </remarks>
        [UnityTest] public IEnumerator EveryCatalogTowerIsRepairableRebuildableAndStoppable()
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(400, 1, 60);
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/Towers/TowerBaseAnchor.prefab");
            var servicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildService.prefab");
            Assert.NotNull(anchorPrefab); Assert.NotNull(servicePrefab);
            // 슬롯은 서비스가 깨어나기 전에 있어야 한다(BuildSlotRegistry가 Awake에서 레벨을 훑는다).
            var catalog = servicePrefab.GetComponent<FacilityBuildService>().catalog;
            Assert.NotNull(catalog, "서비스 프리팹에 카탈로그가 없다.");
            Assert.Greater(catalog.facilities.Count, 0);
            var receiverType = System.Type.GetType("TowerDisableReceiver, Assembly-CSharp");
            Assert.NotNull(receiverType, "TowerDisableReceiver 타입을 찾지 못했다.");

            var level = Track(new GameObject("Level")).AddComponent<LevelRoot>();
            var anchors = new FacilityAnchor[catalog.facilities.Count];
            for (int i = 0; i < anchors.Length; i++)
            {
                var slotObject = new GameObject("BuildSlot_" + i); slotObject.transform.SetParent(level.transform, false);
                slotObject.transform.position = new Vector3(i * 20f, 0f, 0f);
                var slot = slotObject.AddComponent<LevelBuildSlot>();
                slot.id = "slot-" + i; slot.occupancySurfaceId = "test"; slot.footprint = new Vector2(4f, 4f);
                slot.allowedFacilityIds = new List<string> { catalog.facilities[i].id };
                anchors[i] = Object.Instantiate(anchorPrefab, slotObject.transform).GetComponent<FacilityAnchor>();
            }
            var service = Track(Object.Instantiate(servicePrefab)).GetComponent<FacilityBuildService>();
            service.requireSkillUnlock = false; // 해금은 이 테스트의 관심사가 아니다.
            yield return null;

            for (int i = 0; i < anchors.Length; i++)
            {
                var definition = catalog.facilities[i];
                Assert.NotNull(definition.prefab, "카탈로그에 프리팹이 없다: " + definition.id);
                string where = definition.id + " (" + AssetDatabase.GetAssetPath(definition.prefab) + ")";

                var built = service.TryBuild(PlacementRequest.AtSlot(definition.id, "slot-" + i));
                Assert.True(built.Outcome.Succeeded, "건설 실패 " + where + ": " + built.Outcome.Failure + " / " + built.Placement.Failure);
                var facility = anchors[i].Occupant;
                Assert.NotNull(facility, "받침을 점유하지 못했다: " + where);

                Assert.NotNull(facility.Health, "체력(IHealth)이 없다: " + where);
                Assert.NotNull(facility.Life, "생명 상태(ILifeState)가 없다 → 부서져도 자리가 비지 않는다: " + where);
                Assert.NotNull(facility.Repairable, "수리(IRepairable)가 없다 → 수리 UI가 뜨지 않는다: " + where);
                Assert.AreEqual(facility.EntityId, (facility.Health as ICombatTarget).EntityId, "시설 ID와 전투 ID가 같아야 한다: " + where);

                // 우두머리의 철거 폭탄은 정지를 "요청"만 한다. 본체에 수신기가 없으면 요청이 그냥 사라진다.
                var body = ((Component)facility.Health).gameObject;
                Assert.NotNull(body.GetComponent(receiverType), "정지 수신기가 없다 → 폭탄을 맞아도 계속 공격한다: " + where);

                // 체력이 깎이면 수리 견적이 나오고, 수리하면 가득 찬다.
                var damageable = (IDamageable)facility.Health;
                float max = facility.Health.MaxHealth;
                damageable.TakeDamage(new DamageInfo(max * .5f, "Enemy"));
                Assert.True(service.GetRepairQuote(facility.EntityId).Availability.Succeeded, "수리 견적이 나오지 않는다: " + where);
                Assert.True(service.TryRepair(facility.EntityId).Succeeded, "수리에 실패한다: " + where);
                Assert.AreEqual(max, facility.Health.CurrentHealth, .01f, "수리가 체력을 가득 채우지 않는다: " + where);

                // 부수면 자리가 비고, 같은 받침에 다시 지을 수 있다.
                damageable.TakeDamage(new DamageInfo(max * 10f, "Enemy"));
                yield return new WaitForSeconds(1.2f);
                Assert.False(anchors[i].IsOccupied, "부서져도 받침이 비지 않는다: " + where);
                var rebuilt = service.TryBuild(PlacementRequest.AtSlot(definition.id, "slot-" + i));
                Assert.True(rebuilt.Outcome.Succeeded, "다시 지을 수 없다 " + where + ": " + rebuilt.Outcome.Failure + " / " + rebuilt.Placement.Failure);
            }
        }

        void Capture(Vector3 focus, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Camera.main; if (camera == null) return;
            var rt = new RenderTexture(1200, 800, 24); camera.targetTexture = rt;
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); texture.Apply();
            Directory.CreateDirectory(Captures);
            File.WriteAllBytes(Captures + "/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
            Object.Destroy(rt); Object.Destroy(texture);
        }
    }
}
#endif
