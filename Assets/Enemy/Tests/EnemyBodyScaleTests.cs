#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SandGuard.Enemy.Tests
{
    /// <summary>
    /// EnemyScaleBuilder가 모델만 키우고 사람 크기 기준 수치는 그대로 두면 조준점·연출이 몸에 맞지 않는다.
    /// 조준점은 타워·근접 공격·대상 거리 판단이 함께 쓰므로 모델 배율을 따라 올라가야 한다.
    /// </summary>
    public sealed class EnemyBodyScaleTests
    {
        [Test] public void HitPositionRisesWithModelScale()
        {
            float Height(string name, out float scale)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Generated/" + name + ".prefab");
                var health = prefab.GetComponent<EnemyHealth>();
                var visuals = prefab.GetComponent<EnemyVisuals>();
                scale = visuals ? visuals.BodyScale : 1f;
                float height = health.HitPosition.y - prefab.transform.position.y;
                Assert.AreEqual(health.hitOffset.y * scale, height, 0.001f, name + " 조준점이 모델 배율을 따르지 않는다");
                return height;
            }
            float swordsman = Height("Enemy_Swordsman", out _);
            float guard = Height("Enemy_ShieldGuard", out float guardScale);
            float chief = Height("Enemy_Chief", out float chiefScale);
            Assert.Greater(guardScale, 1.5f, "방패병 모델은 커져 있다(전제)");
            Assert.Greater(chief, 2f, "4m 넘는 우두머리의 조준점이 사람 크기 기준 1m(무릎)에 머물면 안 된다");
            Assert.Greater(chief, guard);
            Assert.Greater(guard, swordsman);
            Assert.Greater(chiefScale, guardScale);
        }
    }
}
#endif
