using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class SkeletonController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TowerStatus status;

        public void Despawn()
        {
            gameObject.SetActive(false);
        }
    }
}