using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class AnubisTowerConfig
    {
        [Header("Spawn Point")]
        [SerializeField] private Transform SpawnPoint;

        //Properties
        public Transform spawnPoint => SpawnPoint;
    }
}