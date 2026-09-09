using UnityEngine;

namespace DesertTower.VFX
{
    /// <summary>UnityEvent에 꽂아 쓰는 아주 약한 카메라 킥. 발사 손맛용. Kick()이 VfxCameraShake를 짧게 부른다.</summary>
    public sealed class VfxCameraKick : MonoBehaviour
    {
        [Min(0f)] public float Strength = 0.02f;
        [Min(0.01f)] public float Duration = 0.08f;
        public void Kick() => VfxCameraShake.Shake(Strength, Duration);
    }
}
