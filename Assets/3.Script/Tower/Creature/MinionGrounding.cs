using UnityEngine;

namespace Tower
{
    /// <summary>발바닥이 루트 원점인 아군 하수인의 높이를 실제 바닥에 맞춘다.</summary>
    public static class MinionGrounding
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static Vector3 Project(Transform unit, Vector3 position)
        {
            TryProject(unit, position, out Vector3 projected);
            return projected;
        }

        // 기존 Project의 바닥 계산을 공유하면서, 바닥이 없는 낭떠러지도 복귀 코드에 알린다.
        public static bool TryProject(Transform unit, Vector3 position, out Vector3 projected)
        {
            // 타워 소환점의 높이를 유지하던 부유 현상을 막고 경사면의 높이도 매번 반영한다.
            Vector3 origin = position + Vector3.up * 2f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, 64f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // 밀집 지역에서도 버퍼 밖의 실제 바닥이 누락되지 않도록 한다.
            RaycastHit[] hits = count == Hits.Length
                ? Physics.RaycastAll(origin, Vector3.down, 64f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : Hits;
            if (hits != Hits) count = hits.Length;

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(unit)) continue;
                // 자기 몸, 다른 전투 유닛, 타워의 피격체 위에 올라서지 않는다.
                if (hit.collider.GetComponentInParent<ICombatTarget>() != null) continue;
                if (hit.normal.y < 0.5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                position.y = hit.point.y;
            }

            // 바닥을 찾지 못한 경우 임의의 월드 Y=0으로 순간이동하지 않는다.
            // return position; // 기존에는 바닥 미검출 여부를 호출자가 구분할 수 없었다.
            projected = position;
            return !float.IsPositiveInfinity(nearest);
        }
    }
}
