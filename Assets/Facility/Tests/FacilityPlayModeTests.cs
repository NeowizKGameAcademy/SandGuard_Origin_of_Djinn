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
            var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Facility/Generated/TowerAnchor.prefab");
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
            Assert.AreEqual(1.4f, anchor.GetComponentInChildren<Renderer>().bounds.size.x, .15f, "Base width should match the slot footprint.");
            Capture(anchor.transform.position, "complete");

            var again = service.TryBuild(PlacementRequest.AtSlot("cobra", "slot-test"));
            Assert.False(again.Outcome.Succeeded); Assert.AreEqual(PlacementFailure.Occupied, again.Placement.Failure);
            player.position = new Vector3(0, 0, 12f);
            yield return new WaitForSeconds(.3f);
            Assert.False(menu.IsOpen, "An occupied anchor never reopens the menu.");
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
