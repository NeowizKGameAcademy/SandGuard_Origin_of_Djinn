using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public enum TowerType
    {
        Cobra,
        Obelisk,
        Coffin,
        Anubis
    }
     
    public class TowerStatus : MonoBehaviour
    {
        [Header("Tower")]
        [SerializeField] private TowerType Tower;

        [Header("Basic Status")]
        [SerializeField] private float MaxHP = 200f;
        [SerializeField] private float DetectRange = 20f;

        [Header("Tower Config")]
        [SerializeField] private CobraTowerConfig cobraConfig;
        [SerializeField] private ObeliskTowerConfig obeliskConfig;
        [SerializeField] private CoffinTowerConfig coffinConfig;
        [SerializeField] private AnubisTowerConfig anubisConfig;

        // Common
        public TowerType towerType => Tower;

        public float maxHP => MaxHP;
        public float detectRange => DetectRange;

        // Config
        public CobraTowerConfig cobra => cobraConfig;
        public ObeliskTowerConfig obelisk => obeliskConfig;
        public CoffinTowerConfig coffin => coffinConfig;
        public AnubisTowerConfig anubis => anubisConfig;
    }
}
