using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class CoffinTowerConfig
    {
        [Header("Summon")]
        [SerializeField] private int Num = 7;
        [SerializeField] private float Distance = 0.3f;

        //Properties
        public int num => Num;
        public float distance => Distance;
    }
}