using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>선택 사항: 외형 프리팹에 붙여 모델 고유의 손/지팡이 위치를 지정한다.</summary>
    public sealed class PlayerVisualBindings : MonoBehaviour
    {
        public Animator animator;
        public Transform firePoint;
    }
}
