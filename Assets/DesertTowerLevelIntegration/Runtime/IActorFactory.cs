using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>
    /// WaveDirector가 적 개체를 만들고 거두는 방법. 비워 두면 감독은 Instantiate/Destroy를 쓴다.
    /// 풀 구현은 동시 활성 상한에 걸리면 Spawn에서 null을 돌려주고, 감독은 다음 프레임에 다시 시도한다.
    /// </summary>
    public interface IActorFactory
    {
        /// <summary>개체를 준다. 지금 줄 수 없으면 null (감독이 다시 시도한다).</summary>
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        /// <summary>다 쓴 개체를 거둔다.</summary>
        void Despawn(GameObject instance);
    }
}
