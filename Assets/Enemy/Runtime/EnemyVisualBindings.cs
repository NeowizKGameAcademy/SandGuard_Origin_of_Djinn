using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>선택 사항: 외형 프리팹에 붙여 모델 고유의 애니메이터, 무기 축, 공격 기준점을 지정한다.</summary>
    public sealed class EnemyVisualBindings : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("애니메이터가 없을 때 코드로 휘두를 무기 축")]
        public Transform weaponPivot;
        [Tooltip("모델 고유의 공격 기준점. 비우면 적 프리팹의 기본 위치를 쓴다")]
        public Transform attackOrigin;
    }
}
