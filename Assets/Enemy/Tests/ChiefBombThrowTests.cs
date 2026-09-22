#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Tests
{
    public sealed class ChiefBombThrowTests
    {
        GameObject ground, chief, targetObject, friend;
        ChiefBombThrowSkill skill;
        EnemyBrain brain;
        EnemyTestTarget target;
        [SetUp] public void Setup()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(30, 1, 30);
            var nav = ground.AddComponent<NavMeshSurface>(); nav.collectObjects = CollectObjects.Children;
            nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders; nav.BuildNavMesh();
            chief = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/Enemy_Chief.prefab"));
            brain = chief.GetComponent<EnemyBrain>(); brain.AIEnabled = false;
            skill = chief.GetComponent<ChiefBombThrowSkill>(); Assert.NotNull(skill);
            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube); targetObject.transform.position = new Vector3(0, 1, 5);
            target = targetObject.AddComponent<EnemyTestTarget>(); target.maxHealth = 10000;
            target.kind = CombatTargetKind.Tower; // 철거 폭탄은 타워에만 던진다
            target.definitionId = "tower.cobra"; // 철거 폭탄은 코브라만 노린다
            targetObject.AddComponent<SphereCollider>(); // 복수 충돌체에도 한 번만 피해
            Physics.SyncTransforms();
        }
        IEnumerator Until(Func<bool> condition, string message, float limit = 6)
        {
            float end = Time.time + limit;
            while (!condition() && Time.time < end) yield return null;
            Assert.True(condition(), message);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(chief); Object.Destroy(targetObject); Object.Destroy(ground); if (friend) Object.Destroy(friend);
            foreach (var bomb in Object.FindObjectsByType<ChiefBombProp>(FindObjectsSortMode.None)) Object.Destroy(bomb.gameObject);
            yield return null;
        }
        [UnityTest] public IEnumerator AnimationReleasesBombAndExplosionHitsOnceAfterCasterDeath()
        {
            brain.AIEnabled = true;
            Assert.True(skill.TryUse(target));
            Assert.NotNull(skill.HeldBomb); Assert.False(skill.HeldBomb.hitCollider.enabled);
            Assert.False(chief.GetComponent<ChiefGoldenShieldSkill>().TryUse(), "No simultaneous shield cast");
            Assert.False(skill.TryUse(target), "No duplicate throw");
            var prop = skill.HeldBomb;
            float width = prop.GetComponentInChildren<MeshRenderer>().bounds.size.magnitude;
            Assert.Greater(width, .05f, "Hand parenting preserves bomb size");
            yield return Until(() => skill.ThrowCount == 1, "Real Throw animation event releases bomb");
            CaptureThrow();
            Assert.Null(skill.HeldBomb); Assert.Null(prop.transform.parent); Assert.False(prop.body.isKinematic);
            Assert.True(prop.GetComponent<ChiefBombProjectile>().IsArmed);
            chief.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo(100000, "World"));
            Assert.False(skill.IsCasting);
            yield return Until(() => target.HitCount > 0, "Released bomb survives caster death and explodes");
            Assert.AreEqual(1, target.HitCount); Assert.AreEqual(target.maxHealth - skill.damage, target.CurrentHealth, .001f);
        }
        [UnityTest] public IEnumerator CancelBeforeReleaseAndPoolReuseLeaveNoHeldBomb()
        {
            brain.AIEnabled = true; Assert.True(skill.TryUse(target));
            chief.SetActive(false);
            Assert.False(skill.IsCasting); Assert.Null(skill.HeldBomb);
            chief.SetActive(true); brain.ResetForReuse();
            Assert.AreEqual(0, skill.ThrowCount); Assert.AreEqual(0, skill.CooldownRemaining);
            Assert.True(skill.TryUse(target));
            skill.enabled = false;
            yield return null;
            Assert.Null(skill.HeldBomb); Assert.False(skill.IsCasting);
            Assert.AreEqual(0, Object.FindObjectsByType<ChiefBombProp>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator BombRequestsDisableOnlyForSurvivingHostileTowersOnce()
        {
            var requests = new System.Collections.Generic.List<TowerDisableRequest>();
            CombatEffectSignals.TowerDisableRequested += requests.Add;
            void Explode(float duration = 5f, float amount = 1f)
            {
                var prop = Object.Instantiate(skill.bombPrefab, targetObject.transform.position, Quaternion.identity);
                var bomb = prop.gameObject.AddComponent<ChiefBombProjectile>();
                bomb.Launch(Vector3.zero, chief.GetComponent<EnemyHealth>(), 10, amount, 2.5f, ~0, duration);
                bomb.Detonate(); bomb.Detonate(); // 중복 폭발/충돌체 모두 한 요청만 전달
            }
            try
            {
                target.kind = CombatTargetKind.Tower;
                Explode(3f);
                Assert.AreEqual(1, requests.Count);
                Assert.AreEqual(target.EntityId, requests[0].TargetEntityId);
                Assert.AreEqual(3f, requests[0].Duration);
                Assert.AreEqual(chief.GetComponent<EnemyHealth>().EntityId, requests[0].Cause.SourceEntityId);
                Assert.AreEqual("chief.bomb", requests[0].Cause.CauseId);
                Explode(0f); Assert.AreEqual(1, requests.Count);
                target.factionId = "Enemy"; Explode(); Assert.AreEqual(1, requests.Count);
                target.factionId = "Ally"; target.kind = CombatTargetKind.Player;
                Explode(); Assert.AreEqual(1, requests.Count);
                target.kind = CombatTargetKind.Tower; Explode(5f, 100000);
                Assert.AreEqual(1, requests.Count, "Destroyed towers receive no disable request");
            }
            finally { CombatEffectSignals.TowerDisableRequested -= requests.Add; }
            yield return null;
        }
        [UnityTest] public IEnumerator BrainUsesShieldThenBombAndExplosionIgnoresFriendlies()
        {
            var shield = chief.GetComponent<ChiefGoldenShieldSkill>(); shield.summonDuration = .1f;
            shield.healthThresholds = new[] { 1f }; // 방패→폭탄 순서를 보는 테스트라 시전 체력 조건은 풀어 둔다
            brain.AIEnabled = true; brain.Think();
            Assert.True(shield.IsCasting); Assert.False(skill.IsCasting);
            yield return Until(() => skill.IsCasting, "Brain chooses bomb after shield summon");
            yield return Until(() => skill.ThrowCount == 1, "AI throw releases through animation");
            friend = GameObject.CreatePrimitive(PrimitiveType.Cube); friend.transform.position = new Vector3(.8f, 1, 5);
            var ally = friend.AddComponent<EnemyTestTarget>(); ally.factionId = "Enemy";
            Physics.SyncTransforms();
            yield return Until(() => target.HitCount > 0, "AI bomb damages target");
            Assert.AreEqual(0, ally.HitCount);
            yield return Until(() => !skill.IsCasting, "Throw recovery finishes");
            Assert.Greater(skill.CooldownRemaining, 0); Assert.False(skill.TryUse(target));
            Assert.False(skill.IsSpent, "maxUses가 0이면 다 쓴 상태가 되지 않는다");
        }
        [UnityTest] public IEnumerator BombFliesAtADistantCobraWithNoTargetNearby()
        {
            brain.AIEnabled = true;
            targetObject.SetActive(false); // 탐지 반경(8m) 안에 싸울 상대가 없다
            chief.GetComponent<EnemyTargetSelector>().ClearTarget();
            var cobraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cobraObject.transform.position = new Vector3(0, 1, 14); // 탐지 밖, 폭탄 사거리(20m) 안
            var cobra = cobraObject.AddComponent<EnemyTestTarget>();
            cobra.kind = CombatTargetKind.Tower; cobra.definitionId = "tower.cobra"; cobra.maxHealth = 10000;
            Physics.SyncTransforms();
            try
            {
                Assert.Greater(skill.range, 14f, "폭탄 사거리가 이 거리를 덮어야 한다");
                brain.Think();
                Assert.IsNull(brain.CurrentTarget, "싸울 상대는 없다");
                Assert.True(skill.IsCasting, "상대가 없어도 멀리 있는 코브라에 던진다");
                Assert.AreSame(cobra, skill.AimTarget);
                yield return Until(() => skill.ThrowCount == 1, "투척", 8);
                yield return Until(() => cobra.HitCount > 0, "멀리 있는 코브라에 착탄한다", 8);
            }
            finally { Object.Destroy(cobraObject); }
        }

        [UnityTest] public IEnumerator BombSkipsOtherTowerKindsAndPicksTheCobra()
        {
            brain.AIEnabled = true;
            Assert.AreEqual("tower.cobra", skill.targetFacilityId, "족장은 코브라만 노린다");
            target.definitionId = "tower.obelisk"; // 바로 앞의 오벨리스크
            Assert.False(skill.TryUse(target), "코브라가 아닌 타워에는 던지지 않는다");

            var cobraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cobraObject.transform.position = new Vector3(3, 1, 5); // 오벨리스크보다 멀다
            var cobra = cobraObject.AddComponent<EnemyTestTarget>();
            cobra.kind = CombatTargetKind.Tower; cobra.definitionId = "tower.cobra"; cobra.maxHealth = 10000;
            Physics.SyncTransforms();
            try
            {
                Assert.True(skill.TryUse(target), "오벨리스크와 싸우는 중에도 사거리 안의 코브라를 찾는다");
                Assert.AreSame(cobra, skill.AimTarget, "더 가까운 오벨리스크가 아니라 코브라를 겨냥한다");
                yield return Until(() => skill.ThrowCount == 1, "투척");
                yield return Until(() => cobra.HitCount > 0, "코브라에 터진다");
            }
            finally { Object.Destroy(cobraObject); }
        }

        [UnityTest] public IEnumerator BombIgnoresNonTowersFindsANearbyTowerAndIsUsedOnce()
        {
            brain.AIEnabled = true; skill.cooldown = .1f; skill.maxUses = 1; // 횟수 제한 규칙 자체를 본다
            target.kind = CombatTargetKind.Player;
            Assert.False(skill.TryUse(target), "사거리 안에 타워가 없으면 플레이어에게는 던지지 않는다");
            var towerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towerObject.transform.position = new Vector3(2, 1, 5);
            var tower = towerObject.AddComponent<EnemyTestTarget>(); tower.kind = CombatTargetKind.Tower;
            tower.definitionId = "tower.cobra"; tower.maxHealth = 10000;
            Physics.SyncTransforms();
            try
            {
                Assert.True(skill.TryUse(target), "싸우는 상대가 플레이어여도 사거리 안의 타워를 찾아 던진다");
                Assert.AreSame(tower, skill.AimTarget, "겨냥한 대상은 타워다");
                yield return Until(() => skill.ThrowCount == 1, "투척");
                yield return Until(() => !skill.IsCasting, "투척 회복");
                yield return new WaitForSeconds(.3f); // 쿨다운이 지나도 다시 쓰지 않는다
                Assert.True(skill.IsSpent);
                Assert.False(skill.TryUse(tower), "한 번 던지면 다시 던지지 않는다");
            }
            finally { Object.Destroy(towerObject); }
        }

        void CaptureThrow()
        {
            var cameraObject = new GameObject("Throw test camera");
            var lightObject = new GameObject("Throw test light");
            var camera = cameraObject.AddComponent<Camera>();
            var bounds = new Bounds(chief.transform.position + Vector3.up, Vector3.one);
            foreach (var renderer in chief.GetComponentsInChildren<SkinnedMeshRenderer>()) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(5, 2, 5);
            camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .14f);
            camera.orthographic = true; camera.orthographicSize = bounds.extents.y * 1.3f + .2f;
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var rt = new RenderTexture(1000, 800, 24); camera.targetTexture = rt;
            var previous = RenderTexture.active;
            var texture = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); texture.Apply();
                System.IO.Directory.CreateDirectory("Docs/vfx-preview/ChiefBombSkill");
                System.IO.File.WriteAllBytes("Docs/vfx-preview/ChiefBombSkill/Throw_Release.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
                Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(cameraObject); Object.Destroy(lightObject);
            }
        }
    }
}
#endif
