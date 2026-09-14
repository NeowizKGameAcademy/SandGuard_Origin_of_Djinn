using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>원본을 대신해 부서지는 연출이 원본을 받아 숨긴다. <see cref="VfxCobraDestruction"/>, <see cref="VfxCoreDestruction"/>.</summary>
    public interface IVfxTargetBinding
    {
        void BindTarget(Transform target);
    }

    /// <summary>
    /// 같은 오브젝트의 ILifeState가 죽으면 파괴 연출을 원본 위치·회전·크기에 생성하고 원본을 넘긴다.
    /// 원본의 체력·제거는 게임 코드가 맡는다. 연출은 원본의 자식이 아니므로 원본이 꺼지거나 파괴되어도 잔해가 남는다.
    /// ILifeState가 없는 대상(코어 등)은 UnityEvent에서 <see cref="Fire"/>를 연결한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxDestructionOnDeath : MonoBehaviour
    {
        [Tooltip("VFX_Cobra_Destruction 등. 원본과 같은 원점·크기 기준으로 만들어진 프리팹")]
        public GameObject Prefab;
        [Min(0.1f), Tooltip("반환까지의 시간. 반환하면 잔해도 사라진다")]
        public float Lifetime = 3f;
        [Tooltip("숨길 원본. 비우면 이 오브젝트")]
        public Transform Target;

        ILifeState life;

        void Awake() => life = GetComponent<ILifeState>();
        void OnEnable() { if (life != null) life.Died += OnDied; }
        void OnDisable() { if (life != null) life.Died -= OnDied; }
        void OnDied(DeathInfo info) => Play();

        /// <summary>UnityEvent 연결용(onDefeated 등).</summary>
        public void Fire() => Play();

        /// <summary>연출을 직접 재생한다.</summary>
        public GameObject Play()
        {
            if (Prefab == null) return null;
            Transform target = Target != null ? Target : transform;
            var instance = PrefabPool.Spawn(Prefab, target.position, target.rotation);
            instance.transform.localScale = Vector3.Scale(Prefab.transform.localScale, target.lossyScale);
            foreach (var binding in instance.GetComponentsInChildren<IVfxTargetBinding>()) binding.BindTarget(target);
            PrefabPool.Release(instance, Lifetime);
            return instance;
        }
    }
}
