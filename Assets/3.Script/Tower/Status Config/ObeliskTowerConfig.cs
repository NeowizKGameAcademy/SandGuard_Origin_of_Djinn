using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class ObeliskTowerConfig
    {
        [Header("Slow")]
        [SerializeField] private float SlowRatio = 0.7f;
        [Min(0.01f)] [SerializeField] private float TickInterval = 1f;

        //Properties
        public float slowRatio => SlowRatio;
        public float tickInterval => TickInterval;
    }
}
