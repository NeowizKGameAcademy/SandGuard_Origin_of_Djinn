using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class TowerStatus : MonoBehaviour
    {
        [Header("Basic Status")]
        [SerializeField] private float MaxHP = 200f;
        [SerializeField] private float CurrentHP;
        [SerializeField] private float DetectRange = 20f;

        //Properties
        public float maxHP => MaxHP;
        public float curHP => CurrentHP;
        public float detectRange => DetectRange;

        private void OnEnable()
        {
            CurrentHP = MaxHP;
        }

        public void TakeDamage(float damage)
        {
            CurrentHP -= damage;
        }

        private void Destroy()
        {
            
        }
    }
}
