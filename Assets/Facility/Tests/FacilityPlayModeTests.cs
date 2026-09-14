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
            var wrong = service.TryBuild(PlacementRequest.AtSlot("nope", "slot-test"));
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
            yield return null;

            var built = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-test"));
            Assert.True(built.Outcome.Succeeded, "Build failed: " + built.Placement.Failure);
            var health = anchor.Occupant.GetComponent<FacilityHealth>();
            Assert.NotNull(health, "Run SandGuard/Facility/Add Combat Health To Towers first.");
            ICombatTarget target = health;
            Assert.AreEqual(CombatTargetKind.Tower, target.Kind);
            Assert.AreEqual("Ally", target.FactionId);
            Assert.AreEqual(built.Facility.Value.EntityId, target.EntityId, "Combat ID must match the facility ID.");
            Assert.AreEqual(target.EntityId, target.DamageReceiver is FacilityHealth receiver ? receiver.EntityId : System.Guid.Empty);

            // 적의 탐색과 같은 방식(트리거 제외 OverlapSphere → 부모의 ICombatTarget)으로 찾혀야 한다.
            yield return new WaitForSeconds(.5f);
            bool found = false;
            foreach (var collider in Physics.OverlapSphere(new Vector3(0, .5f, 3f), 4f, ~0, QueryTriggerInteraction.Ignore))
                found |= ReferenceEquals(collider.GetComponentInParent<ICombatTarget>(), target);
            Assert.True(found, "The tower needs a non-trigger collider that resolves to its ICombatTarget.");

            float max = health.MaxHealth;
            Assert.AreEqual(DamageStatus.NonHostile, health.TakeDamage(new DamageInfo(10f, "Ally", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward)).Status);
            var hit = health.TakeDamage(new DamageInfo(40f, "Enemy", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward));
            Assert.True(hit.WasApplied);
            Assert.NotNull(GameObject.Find("VFX_Facility_Hit(Clone)"), "A hit on the tower plays the facility hit VFX.");
            Assert.AreEqual(max - 40f, health.CurrentHealth, .01f);
            Assert.AreEqual(max - 40f, anchor.Occupant.ViewData.CurrentHealth, .01f, "View data reads the live health.");

            var kill = health.TakeDamage(new DamageInfo(max, "Enemy", System.Guid.NewGuid(), "test", Vector3.zero, Vector3.forward));
            Assert.True(kill.WasKilled);
            Assert.False(target.IsTargetable);
            yield return new WaitForSeconds(health.removeDelay + .3f);
            Assert.False(health.gameObject.activeSelf, "A broken tower body is switched off (the owner's base keeps reading it).");
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
            return Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/FacilityBuildService.prefab"))).GetComponent<FacilityBuildService>();
        }

        [UnityTest] public IEnumerator BuildingATowerSpendsManaAndFailsCleanlyWithoutEnough()
        {
            var service = TowerLevel(3, true, out var anchors, out var wallet);
            yield return null;
            Assert.AreEqual(100, wallet.CurrentMana);
            Assert.AreEqual(40, service.catalog.Find("cobra").manaCost, "Designed cobra cost.");
            Assert.AreEqual(55, service.catalog.Find("obelisk").manaCost, "Temporary obelisk cost.");
            Assert.False(anchors[0].transform.Find("Range Circle").gameObject.activeSelf, "The range circle stays off on an empty base (ShowRange needs a body).");

            var cobra = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-0"));
            Assert.True(cobra.Outcome.Succeeded, "Cobra build failed: " + cobra.Outcome.Failure);
            Assert.AreEqual(60, wallet.CurrentMana, "Building spends the cobra cost.");
            Assert.NotNull(anchors[0].transform.Find("cobra"), "The cobra body is spawned on the base.");
            Assert.NotNull(anchors[0].transform.Find("cobra").GetComponent<FacilityHealth>(), "The body keeps its combat health.");
            Assert.True(anchors[0].transform.Find("Range Circle").gameObject.activeSelf, "The range circle turns on with the body.");

            Assert.True(service.TryBuild(PlacementRequest.AtSlot("obelisk", "slot-1")).Outcome.Succeeded);
            Assert.AreEqual(5, wallet.CurrentMana);

            var broke = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-2"));
            Assert.False(broke.Outcome.Succeeded);
            Assert.AreEqual(ActionFailure.InsufficientMana, broke.Outcome.Failure);
            Assert.AreEqual(5, wallet.CurrentMana, "A failed build leaves mana untouched.");
            Assert.False(anchors[2].IsOccupied);
            Assert.True(anchors[2].GetComponentInChildren<FacilityInstance>(true) == null, "No body is left behind.");
            Assert.True(service.Slots.TryGetSlot("slot-2", out var state) && !state.OccupantId.HasValue, "The slot stays free.");
        }

        [UnityTest] public IEnumerator RepairingADamagedTowerCostsManaAndTheMenuSwitchesToRepair()
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
            Assert.AreEqual(ActionFailure.NoChange, service.GetRepairQuote(facility.EntityId).Availability.Failure, "A full-health tower needs no repair.");

            player.position = new Vector3(0f, 0f, 2.5f);
            yield return new WaitForSeconds(.2f);
            Assert.False(menu.IsOpen, "A healthy tower does not open a menu.");

            health.TakeDamage(new DamageInfo(75f, "Enemy"));
            var quote = service.GetRepairQuote(facility.EntityId);
            Assert.True(quote.Availability.Succeeded);
            Assert.AreEqual(10, quote.ManaAmount, "Cost = 40 × half health lost × 0.5.");
            Assert.AreEqual(75f, quote.HealthToRestore, .01f);
            yield return new WaitForSeconds(.2f);
            Assert.True(menu.IsOpen, "A damaged tower opens the repair menu.");
            Assert.AreEqual(FacilityBuildMenu.MenuMode.Repair, menu.Mode);

            int manaBefore = wallet.CurrentMana;
            int repaired = 0; service.Repaired += (f, amount, cost) => repaired++;
            menu.Select(0);
            Assert.True(menu.LastRepairResult.Succeeded, "Repair failed: " + menu.LastRepairResult.Failure);
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, "Repair restores full health at once.");
            Assert.AreEqual(manaBefore - 10, wallet.CurrentMana);
            Assert.AreEqual(1, repaired);
            yield return new WaitForSeconds(.2f);
            Assert.False(menu.IsOpen, "The menu closes once the tower is whole.");

            health.TakeDamage(new DamageInfo(150f - 1f, "Enemy"));
            wallet.TrySpend(wallet.CurrentMana - 3);
            Assert.AreEqual(ActionFailure.InsufficientMana, service.TryRepair(facility.EntityId).Failure);
            Assert.AreEqual(1f, health.CurrentHealth, .01f, "A failed repair changes nothing.");
            Assert.AreEqual(3, wallet.CurrentMana);
        }

        [UnityTest] public IEnumerator ABrokenTowerFreesItsBaseAndRebuildingReplacesTheBrokenBody()
        {
            var service = TowerLevel(1, false, out var anchors, out _);
            yield return null;
            Assert.True(service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-0")).Outcome.Succeeded);
            var broken = anchors[0].Occupant;
            var brokenId = broken.EntityId;
            broken.Health.TakeDamage(new DamageInfo(100000f, "Enemy"));
            Assert.AreEqual(ActionFailure.NotAlive, service.GetRepairQuote(brokenId).Availability.Failure, "A dying tower cannot be repaired.");
            yield return new WaitForSeconds(broken.Health.removeDelay + .3f);
            Assert.False(anchors[0].IsOccupied, "Breaking frees the base.");
            Assert.False(broken.gameObject.activeSelf, "The broken body is switched off, not destroyed (the base still reads it).");
            Assert.False(anchors[0].transform.Find("Range Circle").gameObject.activeSelf, "The range circle turns off with the body.");
            Assert.AreEqual(ActionFailure.NotFound, service.GetRepairQuote(brokenId).Availability.Failure, "Broken towers are rebuilt, not repaired.");

            var rebuilt = service.TryBuild(PlacementRequest.AtSlot("obelisk", "slot-0"));
            Assert.True(rebuilt.Outcome.Succeeded, "Rebuild failed: " + rebuilt.Outcome.Failure);
            yield return null;
            Assert.True(broken == null, "Rebuilding removes the old broken body.");
            Assert.NotNull(anchors[0].transform.Find("obelisk"));
            Assert.True(anchors[0].transform.Find("Range Circle").gameObject.activeSelf);
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
