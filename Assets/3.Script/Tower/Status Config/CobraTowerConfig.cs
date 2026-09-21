using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class CobraTowerConfig
    {
        [Header("Fire")]
        [SerializeField] private float TickDamage = 10f;
        [SerializeField] private float TickInterval = 0.5f;

        //Properties
        public float tickDamage => TickDamage;
        public float tickInterval => TickInterval;
    }
}