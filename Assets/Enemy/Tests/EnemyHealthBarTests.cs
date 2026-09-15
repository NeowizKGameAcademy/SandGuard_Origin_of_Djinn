#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SandGuard.Enemy.Tests
{
    /// <summary>적 머리 위 체력바: 피해를 받은 적만, 체력 비율만큼, 죽으면 바로 숨김, 보스는 제외.</summary>
    public sealed class EnemyHealthBarTests
    {
        readonly List<GameObject> objects = new List<GameObject>();

        GameObject Track(GameObject value) { objects.Add(value); return value; }

        EnemyHealth Enemy(string prefab, Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + prefab + ".prefab");
            Assert.NotNull(asset, "Run SandGuard/Enemy/Connect Combat Art first.");
            var value = Track(Object.Instantiate(asset, position, Quaternion.identity));
            value.GetComponent<EnemyBrain>().AIEnabled = false;
            return value.GetComponent<EnemyHealth>();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            if (EnemyHealthBars.Instance != null) Object.Destroy(EnemyHealthBars.Instance.gameObject);
            foreach (var clone in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (clone.transform.root.name.StartsWith("VFX_")) Object.Destroy(clone.transform.root.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator OnlyDamagedEnemiesShowARedBarThatHidesOnDeathAndBossesAreSkipped()
        {
            var camera = Track(new GameObject("Main Camera")).AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0f, 4f, -10f); camera.transform.LookAt(new Vector3(0f, 1.5f, 0f));
            var hurt = Enemy("Enemy_Swordsman", new Vector3(-2f, 0f, 0f));
            var untouched = Enemy("Enemy_Swordsman", new Vector3(2f, 0f, 0f));
            var boss = Enemy("Enemy_Chief", new Vector3(0f, 0f, 4f));
            yield return null;
            Assert.IsNull(EnemyHealthBars.Instance, "No bar overlay exists before anyone is hit.");

            hurt.TakeDamage(new DamageInfo(hurt.MaxHealth * .25f, "Ally"));
            var bars = EnemyHealthBars.Instance;
            Assert.NotNull(bars, "The first hit creates the bar overlay by itself.");
            yield return null;
            Assert.True(bars.IsShowing(hurt), "A damaged enemy shows its bar.");
            Assert.False(bars.IsShowing(untouched), "An untouched enemy stays clean.");
            var fill = bars.Canvas.GetComponentsInChildren<RectTransform>().First(r => r.name == "Fill");
            Assert.AreEqual((bars.barSize.x - 4f) * .75f, fill.sizeDelta.x, .5f, "Fill width follows the health ratio.");
            var barRoot = (RectTransform)fill.parent;
            Vector3 head = camera.WorldToScreenPoint(hurt.transform.position + Vector3.up * 1.5f);
            Vector3 feet = camera.WorldToScreenPoint(hurt.transform.position);
            Assert.Greater(barRoot.position.y, head.y, "The bar sits above the head.");
            Assert.AreEqual(feet.x, barRoot.position.x, 30f, "The bar is centered over the enemy.");

            boss.TakeDamage(new DamageInfo(10f, "Ally"));
            yield return null;
            Assert.False(bars.IsShowing(boss), "Bosses use the HUD boss bar instead.");

            hurt.TakeDamage(new DamageInfo(100000f, "Ally"));
            yield return null;
            Assert.False(bars.IsShowing(hurt), "A killed enemy hides its bar at once.");
        }
    }
}
#endif
