using System.Collections.Generic;
using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>
    /// 낙사 수동 테스트 HUD. 씬의 NavMesh 가장자리를 훑어 (치명 / 생존·NavMesh 착지 / 생존·NavMesh 밖 착지 / 바닥 없음) 네 종류로 나누고,
    /// 고른 자리에 적을 세운 뒤 소용돌이처럼 바깥으로 밀어 낙하·사망·복귀를 눈으로 확인한다. 카메라는 적을 따라간다.
    /// </summary>
    public sealed class EnemyFallDemoOverlay : MonoBehaviour
    {
        public GameObject enemyPrefab;
        [Min(0f), Tooltip("바깥으로 미는 속도(m/s). PlayerSandVortex.pullSpeed와 같게 둔다")]
        public float pushSpeed = 3f;
        public Vector3 cameraOffset = new Vector3(0f, 5f, -8f);
        [Tooltip("적을 세운 뒤 두뇌를 켠다. 끄면 제자리에 서서 밀림만 받는다")]
        public bool aiEnabled;

        public enum Kind { Fatal, SafeOntoNavMesh, SafeOffNavMesh, Void }
        static readonly string[] KindLabels = { "1 치명 (죽는 높이)", "2 생존 → NavMesh 착지", "3 생존 → NavMesh 밖 착지", "4 바닥 없음" };
        readonly List<NavMeshLedge>[] ledges = { new List<NavMeshLedge>(), new List<NavMeshLedge>(), new List<NavMeshLedge>(), new List<NavMeshLedge>() };
        readonly int[] cursor = new int[4];
        GameObject enemy; EnemyFall fall; EnemyMotor motor; EnemyHealth health; EnemyBrain brain; IDisplaceable displaceable;
        NavMeshLedge current; bool hasCurrent, pushHeld, pushLatched;
        Camera cam;
        float fatalDrop = 5f, minDrop = 0.8f;
        string lastEvent = "-";

        public IReadOnlyList<NavMeshLedge> Ledges(Kind kind) => ledges[(int)kind];
        public GameObject CurrentEnemy => enemy;

        void Start()
        {
            cam = Camera.main;
            Scan();
        }

        /// <summary>NavMesh 가장자리를 다시 훑어 종류별로 나눈다.</summary>
        public void Scan()
        {
            var prefabFall = enemyPrefab != null ? enemyPrefab.GetComponent<EnemyFall>() : null;
            if (prefabFall != null) { fatalDrop = prefabFall.fatalDropHeight; minDrop = prefabFall.ledgeDrop; }
            foreach (var list in ledges) list.Clear();
            foreach (var ledge in NavMeshLedges.Find(minDrop))
                ledges[(int)Classify(ledge)].Add(ledge);
        }

        Kind Classify(NavMeshLedge ledge)
        {
            if (ledge.IsVoid) return Kind.Void;
            if (ledge.Drop >= fatalDrop) return Kind.Fatal;
            return ledge.LandsOnNavMesh ? Kind.SafeOntoNavMesh : Kind.SafeOffNavMesh;
        }

        /// <summary>종류별 목록에서 다음 자리에 적을 세운다. 목록이 비었으면 아무것도 하지 않는다.</summary>
        public EnemyFall Spawn(Kind kind)
        {
            var list = ledges[(int)kind];
            if (list.Count == 0 || enemyPrefab == null) return null;
            Remove();
            current = list[cursor[(int)kind] % list.Count];
            cursor[(int)kind]++;
            hasCurrent = true;
            enemy = Instantiate(enemyPrefab, current.Inside, Quaternion.LookRotation(current.Outward));
            enemy.name = "Enemy (fall test)";
            fall = enemy.GetComponent<EnemyFall>(); motor = enemy.GetComponent<EnemyMotor>(); health = enemy.GetComponent<EnemyHealth>();
            brain = enemy.GetComponent<EnemyBrain>(); displaceable = enemy.GetComponent<IDisplaceable>();
            if (brain != null) brain.AIEnabled = aiEnabled;
            if (fall != null)
            {
                fall.Fell += () => lastEvent = "Fell";
                fall.Landed += (drop, fatal) => lastEvent = (fatal ? "Landed FATAL " : "Landed ") + drop.ToString("F1") + " m";
                fall.Returned += walked => lastEvent = walked ? "Returned (walked)" : "Returned (moved)";
            }
            return fall;
        }

        public void Remove()
        {
            if (enemy != null) Destroy(enemy);
            enemy = null; fall = null; motor = null; health = null; brain = null; displaceable = null;
            pushLatched = false; lastEvent = "-";
        }

        void Update()
        {
            if (enemy != null && hasCurrent && (pushHeld || pushLatched) && displaceable != null)
                displaceable.Displace(current.Outward * pushSpeed * Time.deltaTime);
            pushHeld = false;
            if (cam != null && enemy != null)
            {
                Vector3 target = enemy.transform.position;
                cam.transform.position = Vector3.Lerp(cam.transform.position, target + cameraOffset, 1f - Mathf.Exp(-6f * Time.deltaTime));
                cam.transform.LookAt(target + Vector3.up);
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 520, 250), "SandGuard | Enemy Fall Test (Desert Temple)");
            GUI.Label(new Rect(28, 40, 500, 22), "가장자리를 골라 적을 세우고 바깥으로 밀어 낙사·복귀를 본다. 치명 높이 " + fatalDrop.ToString("F1") + " m");
            for (int i = 0; i < 4; i++)
            {
                var kind = (Kind)i;
                GUI.enabled = ledges[i].Count > 0;
                if (GUI.Button(new Rect(28, 66 + i * 26, 250, 22), KindLabels[i] + "  (" + ledges[i].Count + ")")) Spawn(kind);
                GUI.enabled = true;
            }
            if (GUI.RepeatButton(new Rect(292, 66, 110, 48), "밀기\n(누르는 동안)")) pushHeld = true;
            pushLatched = GUI.Toggle(new Rect(410, 66, 110, 22), pushLatched, "계속 밀기");
            aiEnabled = GUI.Toggle(new Rect(410, 92, 110, 22), aiEnabled, "두뇌 켜기");
            if (brain != null) brain.AIEnabled = aiEnabled;
            if (GUI.Button(new Rect(292, 120, 110, 22), "제거")) Remove();
            if (GUI.Button(new Rect(410, 120, 110, 22), "다시 훑기")) Scan();
            string line1 = enemy == null ? "적 없음" :
                "fall: " + (fall != null ? fall.State.ToString() : "-") + "  navmesh: " + (motor != null && motor.IsOnNavMesh) +
                "  drop: " + (fall != null ? fall.DropHeight.ToString("F1") : "-") + " m  life: " + (health != null ? health.State.ToString() : "-");
            string line2 = hasCurrent ? "자리: edge " + current.Edge.ToString("F1") + "  drop " + (current.IsVoid ? "∞" : current.Drop.ToString("F1") + " m") +
                "  landing on navmesh: " + current.LandsOnNavMesh : "";
            GUI.Label(new Rect(28, 176, 500, 22), line1);
            GUI.Label(new Rect(28, 198, 500, 22), line2);
            GUI.Label(new Rect(28, 220, 500, 22), "마지막 이벤트: " + lastEvent);
        }

        void OnDrawGizmos()
        {
            Color[] colors = { Color.red, Color.green, Color.yellow, Color.magenta };
            for (int i = 0; i < ledges.Length; i++)
            {
                Gizmos.color = colors[i];
                foreach (var ledge in ledges[i])
                {
                    Gizmos.DrawSphere(ledge.Edge + Vector3.up * 0.1f, 0.12f);
                    Gizmos.DrawLine(ledge.Edge + Vector3.up * 0.1f, ledge.Edge + ledge.Outward * 0.6f + Vector3.up * 0.1f);
                }
            }
            if (hasCurrent) { Gizmos.color = Color.white; Gizmos.DrawWireSphere(current.Inside, 0.5f); }
        }
    }
}
