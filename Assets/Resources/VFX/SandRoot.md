# 모래 속박 VFX

`Prefabs/VFX_Sand_Root.prefab` — 양 발목을 감는 두꺼운 모래 띠, 바닥에 붙은 모래 덩어리, 소량의 흩날리는 모래 알갱이. 원점은 양발 사이 지면이며, 기본 반경 약 0.6m / 높이 약 0.5m입니다. 불투명한 모래 메시로 발을 붙잡는 실루엣을 유지합니다.

```csharp
var prefab = Resources.Load<GameObject>("VFX/Prefabs/VFX_Sand_Root");
var instance = Instantiate(prefab, feetAnchor.position, Quaternion.identity, feetAnchor);
// 속박이 해제되는 시점:
instance.GetComponent<DesertTower.VFX.VfxSandRoot>().Release();
```

생성하면 반복 재생됩니다. `Release()`는 방출을 멈추고 모래 띠를 지면으로 주저앉힌 뒤 1초 후 인스턴스를 제거합니다. 일시정지(Time.timeScale = 0) 중에는 입자와 제거 시간이 함께 멈춥니다. 발 기준 앵커의 스케일은 1을 권장하며, 캐릭터 크기에 맞춰 인스턴스의 스케일을 조절할 수 있습니다.

Humanoid 캐릭터의 자식으로 생성하면 양발 뼈의 수평 위치에 맞춰 각각의 모래 띠를 정렬합니다. 실제 이동 제한과 지속 시간은 상태 이상 시스템이 관리합니다. 이 프리팹은 시각 효과만 제공하며 적의 속박 로직에는 아직 연결하지 않았습니다.

재생성: Unity 메뉴 `DesertTower > VFX > Build Sand Root`.
