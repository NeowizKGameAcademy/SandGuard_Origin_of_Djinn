using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class ObeliskTowerConfig
    {
        [Header("Slow")]
        [SerializeField] private float SlowRatio = 0.3f;

        //Properties
        public float slowRatio => SlowRatio;
    }
}
