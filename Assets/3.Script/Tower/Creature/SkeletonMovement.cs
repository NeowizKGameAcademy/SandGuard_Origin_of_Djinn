using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class SkeletonMovement : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TargetSelector Target;
        [SerializeField] 

        private void Awake()
        {
            TryGetComponent(out Target);
        }

        void Update()
        {

        }
    }
}
