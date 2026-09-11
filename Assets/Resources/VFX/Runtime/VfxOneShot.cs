using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// UnityEvent에 꽂아 쓰는 1회 재생기: Play()가 기준 Transform의 위치·방향에 프리팹을 스폰하고 수명 뒤 없앤다.
    /// 예: PlayerVisuals.onFired → VfxOneShot.Play (지팡이 끝의 시전 이펙트).
    /// </summary>
    public sealed class VfxOneShot : MonoBehaviour
    {
        public GameObject Prefab;
        [Tooltip("스폰 위치와 방향. 비우면 이 오브젝트")]
        public Transform Anchor;
        public Vector3 LocalOffset;
        [Tooltip("기준 Transform의 자식으로 붙여 같이 움직이게 한다")]
        public bool ParentToAnchor;
        public float Lifetime = 2f;
        public Vector3 SpawnScale = Vector3.one;

        /// <summary>UnityEvent 인스펙터 연결용 (반환값 없음).</summary>
        public void Fire() => Play();

        public GameObject Play()
        {
            if (Prefab == null) return null;
            Transform anchor = Anchor != null ? Anchor : transform;
            var instance = PrefabPool.Spawn(Prefab, anchor.TransformPoint(LocalOffset), anchor.rotation, ParentToAnchor ? anchor : null);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, SpawnScale);
            PrefabPool.Release(instance, Lifetime);
            return instance;
        }

        public GameObject PlayAt(Vector3 position, Vector3 forward)
        {
            if (Prefab == null) return null;
            var rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            var instance = PrefabPool.Spawn(Prefab, position, rotation);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, SpawnScale);
            PrefabPool.Release(instance, Lifetime);
            return instance;
        }
    }
}
