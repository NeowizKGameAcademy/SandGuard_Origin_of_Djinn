# Golden Arsenal / 금빛 장비 3종

제공된 검·방패·창 이미지를 참고해 Blender에서 제작한 실제 입체 로우폴리 모델입니다. 뒷면과 두께는 정면 참고 그림을 바탕으로 보완했습니다.

## 사용

- Unity: `Assets/GoldenArsenal/Prefabs`의 프리팹을 씬 또는 캐릭터 손 본 아래로 드래그합니다.
- 원본: `GoldenArsenal.blend` (Blender 5.2). 세 모델, 스튜디오 조명, 카메라 포함.
- 교환 파일: `Assets/GoldenArsenal/Models/*.fbx`.
- URP 재질: `Assets/GoldenArsenal/Materials`. 별도 이미지 텍스처 없이 재질 색상 사용, UV 포함.
- 재생성: Unity 메뉴 `Tools > Golden Arsenal > Build Materials and Prefabs`.
- 모델 재생성: Blender background 모드에서 같은 폴더의 `build.py` 실행.

## 크기와 피벗

Unity 1 unit = 1 m, 세로축 Y. 검은 약 1.53 m / 534 tris, 방패 1.24 m / 876 tris, 창 2.35 m / 1,028 tris.
검과 창의 루트 원점은 손잡이 위치에 있습니다. 방패 루트는 본체 중심이며 뒤쪽에 손잡이가 모델링되어 있습니다. 프리팹의 GripPoint는 루트 기준 장착용 보조 Transform입니다. 캐릭터 리그의 손 방향에 맞춰 로컬 회전과 위치를 조정하세요.

정적 장비이므로 스킨/애니메이션은 없습니다. 충돌체 및 공격 판정, 캐릭터 장착 로직은 포함하지 않습니다. 보석은 약한 emission 재질이며, 참고 그림의 번짐 효과는 Unity Volume의 Bloom으로 별도 연출할 수 있습니다.

`GoldenArsenal_Preview.png`와 `GoldenArsenal_Front.png`는 Blender 렌더이며 Unity 게임 화면은 아닙니다.
