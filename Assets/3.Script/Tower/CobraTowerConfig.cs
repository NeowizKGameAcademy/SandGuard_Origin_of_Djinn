using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class CobraTowerConfig
    {
        [Header("Fire")]
        [SerializeField] private float TickDamage = 10f;
        [SerializeField] private float TickInterval = 0.5f;
    }
}
