using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>
    /// 부피형 이펙트(벽 파괴)를 대상 크기에 맞춘다. Fit()은 루트를 경계 상자의 바닥 중심에 놓고, Box 모양 시스템의
    /// 방출 영역을 상자 크기로, Cone 모양은 반지름을 바닥 폭으로 바꾸며, 파편 수를 부피 비율만큼 늘린다.
    /// </summary>
    public sealed class VfxVolume : MonoBehaviour
    {
        [Tooltip("크기를 맞출 시스템 (Box 모양은 영역, Cone 모양은 반지름)")]
        public ParticleSystem[] Systems;
        [Tooltip("높이의 절반으로 올릴 자식 (플래시 등)")]
        public Transform[] Centered;
        [Tooltip("프리팹이 기준으로 만든 상자 크기. 파편 수는 이 부피 대비 비율로 늘어난다")]
        public Vector3 BaseSize = Vector3.one;
        public float MaxCountMultiplier = 4f;

        public void Fit(Bounds bounds)
        {
            Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
            transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float baseVolume = Mathf.Max(0.01f, BaseSize.x * BaseSize.y * BaseSize.z);
            float ratio = Mathf.Clamp(size.x * size.y * size.z / baseVolume, 1f, MaxCountMultiplier);
            float floorRadius = Mathf.Max(size.x, size.z) * 0.45f;

            if (Systems != null)
                foreach (var ps in Systems)
                {
                    if (ps == null) continue;
                    var shape = ps.shape;
                    if (shape.shapeType == ParticleSystemShapeType.Box)
                    {
                        shape.scale = size;
                        ps.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                    }
                    else if (shape.shapeType == ParticleSystemShapeType.Cone || shape.shapeType == ParticleSystemShapeType.Circle)
                        shape.radius = floorRadius;
                    else if (shape.shapeType == ParticleSystemShapeType.Sphere)
                        shape.position = new Vector3(0f, size.y * 0.5f, 0f);

                    var main = ps.main;
                    main.maxParticles = Mathf.CeilToInt(main.maxParticles * ratio);
                    var emission = ps.emission;
                    int count = emission.burstCount;
                    if (count == 0) continue;
                    var bursts = new ParticleSystem.Burst[count];
                    emission.GetBursts(bursts);
                    for (int i = 0; i < count; i++) bursts[i].count = Mathf.RoundToInt(bursts[i].count.constant * ratio);
                    emission.SetBursts(bursts);
                }

            if (Centered != null)
                foreach (var child in Centered)
                    if (child != null) child.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        }
    }
}
