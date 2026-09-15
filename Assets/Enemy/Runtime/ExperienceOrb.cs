using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 떨어진 경험치 한 덩어리. 처치 지점에서 튀어 오른 뒤 <see cref="scatterTime"/>이 지나면 받을 수 있는 가장 가까운 수신자에게 가속해 날아가고,
    /// 닿으면 경험치를 넘기고 풀로 돌아간다. 받을 대상이 없으면(플레이어 무력화 등) 제자리에 떠서 기다린다.
    /// 외형은 자식 VFX_Experience_Mote가 맡는다.
    /// </summary>
    public sealed class ExperienceOrb : MonoBehaviour, IPoolable
    {
        [Tooltip("튀어 오른 뒤 흡수가 시작되기까지의 시간")] public float scatterTime = 0.55f;
        [Tooltip("튀어 오르는 동안의 중력")] public float gravity = 14f;
        public float acceleration = 38f;
        public float maxSpeed = 20f;
        [Tooltip("수신자 위치에 이만큼 가까워지면 획득")] public float collectRadius = 0.45f;
        [Tooltip("받는 대상이 끝내 없을 때 사라지기까지의 시간")] public float maxLifetime = 60f;

        public int Value { get; private set; }
        public bool IsLive { get; private set; }
        Vector3 velocity;
        float age;

        public void Launch(int value, Vector3 initialVelocity)
        {
            Value = Mathf.Max(0, value);
            velocity = initialVelocity;
            age = 0f;
            IsLive = true;
        }

        public void OnRent() { IsLive = false; age = 0f; velocity = Vector3.zero; }
        public void OnReturn() { IsLive = false; Value = 0; }

        void Update()
        {
            if (!IsLive) return;
            float dt = Time.deltaTime;
            age += dt;
            if (age > maxLifetime) { Finish(); return; }
            if (age < scatterTime)
            {
                velocity.y -= gravity * dt;
                transform.position += velocity * dt;
                return;
            }
            if (!ExperienceReceivers.TryFindNearest(transform.position, out var receiver))
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * dt);
                transform.position += velocity * dt;
                return;
            }
            Vector3 toGoal = receiver.CollectPosition - transform.position;
            float distance = toGoal.magnitude;
            if (distance <= collectRadius)
            {
                receiver.GainExperience(Value);
                Finish();
                return;
            }
            // 목표를 지나치지 않는 속도로 조향해 궤도를 돌지 않게 한다.
            float speed = Mathf.Min(maxSpeed, distance / Mathf.Max(dt, 1e-4f));
            velocity = Vector3.MoveTowards(velocity, toGoal / distance * speed, acceleration * dt);
            transform.position += velocity * dt;
        }

        void Finish()
        {
            IsLive = false;
            PrefabPool.Release(gameObject);
        }
    }
}
