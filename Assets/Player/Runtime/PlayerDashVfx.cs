using DesertTower.VFX;
using UnityEngine;

namespace SandGuard.Player
{
    /// <summary>Dash presentation only: stationary launch ring and direction-aligned body airflow.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerDashVfx : MonoBehaviour
    {
        public GameObject launchPrefab;
        public GameObject airflowPrefab;
        public float bodyHeight = .95f;
        PlayerMotor motor;
        GameObject launch, airflow;
        Quaternion rotation;
        public GameObject ActiveAirflow => airflow;
        public GameObject LaunchRing => launch;

        void Awake() => motor = GetComponent<PlayerMotor>();
        void OnEnable() => motor.DashStarted += Begin;
        void OnDisable()
        {
            motor.DashStarted -= Begin;
            PrefabPool.Release(airflow);
            PrefabPool.Release(launch);
            airflow = launch = null;
        }

        void Begin()
        {
            PrefabPool.Release(airflow);
            PrefabPool.Release(launch);
            Vector3 direction = motor.DashDirection.sqrMagnitude > .01f ? motor.DashDirection : transform.forward;
            rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 center = transform.position + Vector3.up * bodyHeight;
            if (launchPrefab != null)
            {
                launch = PrefabPool.Spawn(launchPrefab, center, rotation);
                PrefabPool.Release(launch, .6f);
            }
            if (airflowPrefab != null) airflow = PrefabPool.Spawn(airflowPrefab, center, rotation);
        }

        void Update()
        {
            if (airflow == null || Time.timeScale <= 0f) return;
            airflow.transform.SetPositionAndRotation(transform.position + Vector3.up * bodyHeight, rotation);
            if (motor.enabled && motor.IsDashing) return;
            foreach (var ps in airflow.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            PrefabPool.Release(airflow, .35f);
            airflow = null;
        }
    }
}
