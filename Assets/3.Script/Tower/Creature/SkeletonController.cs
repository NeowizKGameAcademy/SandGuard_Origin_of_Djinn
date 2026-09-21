using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class SkeletonController : MonoBehaviour
    {
        public void Despawn()
        {
            gameObject.SetActive(false);
        }
    }
}