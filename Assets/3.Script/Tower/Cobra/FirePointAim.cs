using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirePointAim : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FindEnemy findEnemy;

    private void Awake()
    {
        findEnemy = GetComponentInParent<FindEnemy>();
    }

    private void Update()
    {
        if (findEnemy == null || findEnemy.target == null)
            return;

        // [통합 수정 2026-09-14] 적 발밑(target.position) 대신 몸통 기준점(aimPoint = HitPosition)을 조준한다.
        // 왜: 실제 적의 원점은 발밑이라, 발밑을 보면 화구가 땅을 향해 불꽃이 몸에 닿지 않는다.
        //     수평 회전(RotateTower)은 발밑과 몸통의 수평 위치가 같아 바꾸지 않았다.
        /* 기존 코드
        Vector3 direction = findEnemy.target.position - transform.position;
        */
        Vector3 direction = findEnemy.aimPoint - transform.position;

        float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
        float angle = Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;

        Vector3 rotation = transform.localEulerAngles;

        rotation.x = -angle;

        transform.localEulerAngles = rotation;
    }
}
