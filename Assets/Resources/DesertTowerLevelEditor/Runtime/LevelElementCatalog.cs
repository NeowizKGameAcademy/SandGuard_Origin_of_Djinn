using System.Collections.Generic;
using UnityEngine;

namespace DesertTower.Levels
{
    /// <summary>
    /// 웨이브 편집기에 보여줄 "고를 수 있는 종류" 목록을 낸다. 레벨 패키지는 게임 프리팹을 모르므로
    /// 게임 쪽 카탈로그가 이 클래스를 상속해 목록을 공급한다. 무엇을 노출할지는 게임이 정한다.
    /// </summary>
    public abstract class LevelElementCatalog : ScriptableObject
    {
        /// <summary>웨이브에 넣을 수 있는 키를 담는다. 호출자가 리스트를 소유하므로 먼저 비운다.</summary>
        public abstract void CollectKeys(List<string> keys);

        /// <summary>편집기에 보여줄 이름. 비어 있으면 키를 그대로 쓴다.</summary>
        public abstract string DisplayNameFor(string key);
    }
}
