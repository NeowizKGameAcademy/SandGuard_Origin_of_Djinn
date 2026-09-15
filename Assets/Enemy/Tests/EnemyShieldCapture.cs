#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using DesertTower.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// 방패 연출과 방패 위치를 눈으로 비교하기 위한 캡처. 판정하지 않고 PNG와 치수만 남긴다.
    /// 방패 IK는 LateUpdate에서, 파티클은 실제 시간으로 흘러야 해서 플레이 모드에서 찍는다.
    /// 명령줄 실행은 [Explicit]를 무시하므로, 환경변수 SHIELD_CAPTURE_DIR(출력 폴더)를 줬을 때만 찍고 아니면 건너뛴다.
    /// 실행: SHIELD_CAPTURE_DIR=폴더 Unity.exe ... -testFilter SandGuard.Enemy.Tests.EnemyShieldCapture
    /// </summary>
    [Explicit("시각 확인용 캡처")]
    public sealed class EnemyShieldCapture
    {
        const string Generated = "Assets/Enemy/Generated/";
        static readonly Type[] Frozen =
        {
            typeof(EnemyBrain), typeof(EnemyMotor), typeof(EnemyMeleeAttack), typeof(EnemyTargetSelector), typeof(EnemyFall),
            typeof(ChiefBombThrowSkill), typeof(ChiefGoldenShieldSkill), typeof(EnemyRestraint), typeof(EnemyExperienceDrop)
        };

        [UnityTest] public IEnumerator CaptureShieldVisuals()
        {
            string dir = Environment.GetEnvironmentVariable("SHIELD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("SHIELD_CAPTURE_DIR를 지정했을 때만 캡처한다."); yield break; }
            LogAssert.ignoreFailingMessages = true; // NavMesh 없는 빈 무대라 이동 부품 경고가 난다
            Directory.CreateDirectory(dir);

            var scene = SceneManager.CreateScene("Shield Capture " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 3f;
            var light = new GameObject("Key").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, 160f, 0f); light.intensity = 1.3f;

            // 적은 카메라(-Z 쪽)를 바라본다.
            var face = Quaternion.Euler(0f, 180f, 0f);
            var swordsman = Place("Enemy_Swordsman", new Vector3(-2.6f, 0f, 0f), face);
            var guard = Place("Enemy_ShieldGuard", new Vector3(0f, 0f, 0f), face);
            var chief = Place("Enemy_Chief", new Vector3(3f, 0f, 0f), face);
            for (int i = 0; i < 45; i++) yield return null; // 애니메이터 안착, 방패 IK 적용

            // 황금 방패: 스킬이 붙이는 방식 그대로(우두머리의 자식, 스킬의 localPosition).
            var skill = chief.GetComponent<ChiefGoldenShieldSkill>();
            VfxGoldenShield golden = null;
            if (skill && skill.vfxPrefab)
            {
                golden = Object.Instantiate(skill.vfxPrefab, chief.transform);
                golden.transform.localPosition = skill.localPosition * skill.BodyScale;
                golden.transform.localRotation = Quaternion.identity;
                golden.transform.localScale = skill.vfxPrefab.transform.localScale * skill.BodyScale;
                golden.gameObject.SetActive(true); golden.Restart();
            }
            yield return new WaitForSeconds(0.6f);

            // 방패병 정면 막기: EnemyShield.ModifyIncoming과 같은 위치·방향으로 연출을 띄운다.
            var shield = guard.GetComponent<EnemyShield>();
            var health = guard.GetComponent<EnemyHealth>();
            GameObject guardVfx = null;
            if (shield && shield.guardVfx)
            {
                Vector3 outward = guard.transform.forward;
                // 방패 앞면에 맞은 것처럼 방패 중심 바로 앞에서 띄운다.
                var shieldCenter = Combined(ShieldRenderersRoot(guard), r => !(r is SkinnedMeshRenderer)).center;
                Vector3 point = shieldCenter + outward * 0.1f;
                var visuals = guard.GetComponent<EnemyVisuals>();
                guardVfx = PrefabPool.Spawn(shield.guardVfx, point, Quaternion.LookRotation(outward));
                guardVfx.transform.localScale = shield.guardVfx.transform.localScale * (visuals ? visuals.localScale.x : 1f);
            }
            yield return new WaitForSeconds(0.12f);

            var report = new StringBuilder();
            report.AppendLine("=== 방패 캡처 치수 ===");
            Measure(report, "검병", swordsman); Measure(report, "방패병", guard); Measure(report, "우두머리", chief);
            if (guardVfx) Bounds(report, "  방패병 막기 연출(VFX_Shield_Front_Guard)", guardVfx);
            else report.AppendLine("  방패병 막기 연출 없음 (guardVfx 비어 있음)");
            if (golden) Bounds(report, "  우두머리 황금 방패", golden.gameObject);
            else report.AppendLine("  황금 방패 없음");
            ShieldFit(report, guard);

            Vector3 guardChest = Chest(guard);
            Shot(dir, "overview", new Vector3(0.2f, 1.5f, -8.5f), new Vector3(0.2f, 1.1f, 0f), 38f, 1800, 900);
            Shot(dir, "guard_front", guardChest + guard.transform.forward * 5.5f + Vector3.up * 0.3f, guardChest, 40f, 1000, 1000);
            // 이웃을 잠시 멀리 치우고 옆에서 찍어 머리와 방패면의 앞뒤 관계를 본다.
            var away = Vector3.forward * 100f; swordsman.transform.position += away; chief.transform.position += away;
            Shot(dir, "guard_side_shield", guardChest - guard.transform.right * 5.5f + Vector3.up * 0.3f, guardChest, 40f, 1000, 1000);
            swordsman.transform.position -= away; chief.transform.position -= away;
            Shot(dir, "guard_player_view", guardChest + guard.transform.forward * 7f + Vector3.up * 2.5f, guardChest, 45f, 1000, 1000);
            Vector3 chiefChest = Chest(chief);
            Shot(dir, "chief_front", chiefChest + chief.transform.forward * 8f + Vector3.up * 0.5f, chiefChest, 42f, 1000, 1000);
            var aside = Vector3.forward * 100f; swordsman.transform.position += aside; guard.transform.position += aside;
            Shot(dir, "chief_side", chiefChest + chief.transform.right * 8f + Vector3.up * 0.5f, chiefChest, 42f, 1000, 1000);
            swordsman.transform.position -= aside; guard.transform.position -= aside;
            yield return new WaitForSeconds(0.18f);
            Shot(dir, "guard_front_t030", guardChest + guard.transform.forward * 5.5f + Vector3.up * 0.3f, guardChest, 40f, 1000, 1000);

            File.WriteAllText(Path.Combine(dir, "measurements.txt"), report.ToString());
            Debug.Log(report.ToString());
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        static GameObject Place(string prefabName, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + prefabName + ".prefab");
            Assert.NotNull(prefab, prefabName + " 없음");
            // 비활성 부모 아래에서 만들어 Awake 전에 이동·AI 부품을 끈다.
            var holder = new GameObject("holder"); holder.SetActive(false);
            var go = Object.Instantiate(prefab, position, rotation, holder.transform);
            foreach (var type in Frozen) foreach (var c in go.GetComponentsInChildren(type, true)) ((Behaviour)c).enabled = false;
            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            go.transform.SetParent(null, true); Object.Destroy(holder);
            go.name = prefabName;
            return go;
        }

        /// <summary>손에 든 방패의 렌더러를 품은 오브젝트. shieldFrame 자체는 빈 기준점이라 이름으로 찾는다.</summary>
        static GameObject ShieldRenderersRoot(GameObject enemy)
        {
            foreach (var t in enemy.GetComponentsInChildren<Transform>())
                if (t.name == "TowerShield") return t.gameObject;
            foreach (var r in enemy.GetComponentsInChildren<Renderer>())
                if (!(r is SkinnedMeshRenderer) && r.name.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0) return r.gameObject;
            var grip = enemy.GetComponentInChildren<EnemyEquipmentGrip>();
            return grip && grip.shieldFrame ? grip.shieldFrame.gameObject : enemy;
        }

        static Vector3 Chest(GameObject go)
        {
            var animator = go.GetComponentInChildren<Animator>();
            var bone = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            return bone ? bone.position : go.transform.position + Vector3.up * 1.3f;
        }

        static void Measure(StringBuilder report, string label, GameObject go)
        {
            var b = Combined(go, r => r is SkinnedMeshRenderer);
            var animator = go.GetComponentInChildren<Animator>();
            var head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            report.AppendLine(label + ": 몸 크기 " + b.size.ToString("F2") + "  머리 높이 " + (head ? head.position.y.ToString("F2") : "?"));
        }

        static void Bounds(StringBuilder report, string label, GameObject go)
        {
            var b = Combined(go, r => true);
            report.AppendLine(label + ": 크기 " + b.size.ToString("F2") + "  중심 " + b.center.ToString("F2"));
        }

        static void ShieldFit(StringBuilder report, GameObject guard)
        {
            var grip = guard.GetComponentInChildren<EnemyEquipmentGrip>();
            if (!grip || !grip.shieldFrame) { report.AppendLine("방패 부착 정보 없음"); return; }
            var shieldBounds = Combined(ShieldRenderersRoot(guard), r => !(r is SkinnedMeshRenderer));
            var animator = grip.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var forward = guard.transform.forward;
            float headAhead = Vector3.Dot(head.position - shieldBounds.center, forward);
            report.AppendLine("--- 방패병 방패 위치 ---");
            report.AppendLine("  shieldOffset " + grip.shieldOffset.ToString("F2") + "  characterHeight " + grip.characterHeight.ToString("F2"));
            report.AppendLine("  방패 크기 " + shieldBounds.size.ToString("F2") + "  중심 " + shieldBounds.center.ToString("F2")
                + "  위끝 y " + shieldBounds.max.y.ToString("F2"));
            report.AppendLine("  머리 " + head.position.ToString("F2") + "  머리가 방패 중심보다 앞으로 " + headAhead.ToString("F2") + "m"
                + "  머리 높이 - 방패 위끝 " + (head.position.y - shieldBounds.max.y).ToString("F2") + "m");
        }

        static UnityEngine.Bounds Combined(GameObject go, Func<Renderer, bool> accept)
        {
            bool any = false; var b = new UnityEngine.Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !accept(r)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void Shot(string dir, string name, Vector3 eye, Vector3 target, float fov, int width, int height)
        {
            var camera = new GameObject("Capture camera").AddComponent<Camera>();
            camera.transform.position = eye; camera.transform.LookAt(target);
            camera.fieldOfView = fov; camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f, .20f, .24f);
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old;
                rt.Release(); Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(camera.gameObject);
            }
        }
    }
}
#endif
