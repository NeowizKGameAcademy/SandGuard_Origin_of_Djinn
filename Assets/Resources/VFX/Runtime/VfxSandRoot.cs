using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>Visual only. Spawn at the target's feet; the status system owns the root duration.</summary>
    public sealed class VfxSandRoot : MonoBehaviour, IPoolable
    {
        ParticleSystem[] systems;
        bool releasing;
        float remaining;
        Transform mass;
        Vector3 massScale;
        Transform leftCoil, rightCoil, leftFoot, rightFoot;

        void Awake() => Bind();

        /// <summary>풀에서 다른 적에게 다시 붙을 수 있으므로 대여할 때마다 발 본을 다시 찾는다.</summary>
        void IPoolable.OnRent() => Bind();
        void IPoolable.OnReturn() { releasing = false; leftFoot = rightFoot = null; }

        void Bind()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            mass = transform.Find("BindingMass");
            if (mass != null && massScale == Vector3.zero) massScale = mass.localScale;
            leftCoil = rightCoil = leftFoot = rightFoot = null;
            var animator = GetComponentInParent<Animator>();
            if (animator == null && transform.parent != null) animator = transform.parent.GetComponentInChildren<Animator>();
            if (mass != null && animator != null && animator.isHuman)
            {
                leftCoil = mass.Find("LeftAnkle"); rightCoil = mass.Find("RightAnkle");
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        void LateUpdate()
        {
            Align(leftCoil, leftFoot); Align(rightCoil, rightFoot);
        }

        void Align(Transform coil, Transform foot)
        {
            if (coil == null || foot == null) return;
            Vector3 position = transform.InverseTransformPoint(foot.position);
            coil.localPosition = new Vector3(position.x, 0f, position.z);
        }

        void OnEnable()
        {
            releasing = false;
            if (mass != null) mass.localScale = massScale;
            foreach (var ps in systems) ps.Play(false);
        }

        /// <summary>Stop emission, allow the sand to fade, then destroy this VFX instance.</summary>
        public void Release()
        {
            if (releasing) return;
            releasing = true;
            remaining = 1f;
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update()
        {
            if (!releasing) return;
            remaining -= Time.deltaTime;
            if (mass != null) mass.localScale = Vector3.Scale(massScale, new Vector3(1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((remaining - .2f) / .8f)), 1f));
            if (remaining <= 0f) PrefabPool.Release(gameObject);
        }

        void OnDisable()
        {
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
