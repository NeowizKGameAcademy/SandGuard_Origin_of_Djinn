# 적 장비 프리팹

ShortSword, AssassinDagger, ChiefScimitar, Warhammer, RoundShield, TowerShield, ChiefCape의 `.prefab`을 사용하세요. `.fbx`는 원본 모델입니다. 모두 URP 재질이 연결되어 있습니다.

무기·방패는 캐릭터의 손 본 아래에 장착한 뒤 위치·회전을 맞춥니다. 망토는 등/가슴 본 아래에 연결합니다. ChiefCape는 별도 Generic Animator로 2초 흔들림 루프를 재생합니다. 현재 적 5종의 전투용 외형에 연결되어 있습니다.

`SandGuard > Enemy > Fit Hands and Equipment`로 손바닥 중심에 손잡이를 맞추고 방패를 드는 팔·망치 보조 손·손가락 쥐기 자세를 갱신합니다. 장비의 원본 FBX와 공용 장비 프리팹은 유지하며 캐릭터별 부착 위치와 동작을 보정합니다. 상세 내용은 `Assets/Enemy/Art/Characters/README.md`의 장비 부착 규칙을 참고하세요.

기본 크기는 약 1.8m 캐릭터를 기준으로 조절할 수 있는 제작 치수입니다. 현재 Tripo 몸체 GLB는 높이 약 1단위이므로 체형과 부모 스케일에 맞춰 조정하세요. ShortSword 약 0.65배, AssassinDagger 약 0.7배를 출발점으로 삼을 수 있습니다.

편집 원본, 전체 치수, 좌표와 검증 기록: `Docs/model-art/enemy-equipment-v1/README.md`.
