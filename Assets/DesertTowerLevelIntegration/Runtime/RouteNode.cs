using System;
using System.Collections.Generic;
using DesertTower.Levels;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    [Serializable] public sealed class RouteLink
    {
        public RouteNode target;
        [Min(0)] public float weight = 1;
        public bool available = true;
    }

    [DisallowMultipleComponent]
    public sealed class RouteNode : MonoBehaviour
    {
        public string id = Guid.NewGuid().ToString("N");
        public string label = "Node";
        public int floor;
        [Tooltip("입구 또는 코어 노드만 연결합니다.")]
        public LevelMarker marker;
        [Min(.05f)] public float arrivalRadius = .5f;
        [Tooltip("높이 오차를 별도로 제한해 다른 층에서 도착하는 것을 방지합니다.")]
        [Min(.05f)] public float heightTolerance = .5f;
        public List<RouteLink> outgoing = new List<RouteLink>();

        public bool Contains(Vector3 position)
        {
            var delta = position - transform.position;
            return Mathf.Abs(delta.y) <= heightTolerance &&
                new Vector2(delta.x, delta.z).sqrMagnitude <= arrivalRadius * arrivalRadius;
        }
    }
}
