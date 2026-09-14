# SandGuard 사암 트림 시트 · V6 시험 적용

최신 난간 정리는 [계단·연결길 난간 v2](RAILS_V2.md)를 참고한다. v2는 전 구간의 난간 22쌍을 44개 독립 오브젝트로 분리하고 UV를 다시 작성한 별도 프리팹이다. 아래 내용은 최초 v1 시험 적용 기록이다.

현재 플레이 레벨이 사용하는 `DesertTemple_V6`의 북동쪽 진입 계단, T1 플랫폼, B_NE 연결 플랫폼과 다음 계단까지 15개 메시를 시험 적용 대상으로 삼았다. 기존 FBX에는 UV가 없어서 Blender 5.2에서 UV를 작성했다. 원본 레벨, 원본 모델, 원본 프리팹을 덮어쓰지 않는다.

## 열기

- Unity 씬: `Assets/DesertTowerLevels/TrimSheet/Scenes/SandGuard_Trim_Comparison.unity`
- 적용 프리팹: `Assets/DesertTowerLevels/TrimSheet/Prefabs/DesertTemple_TrimPilot_v1.prefab`
- Unity 메뉴: `SandGuard > Level Art > Open Trim Sheet Comparison`
- 씬의 `BEFORE`와 `AFTER` 루트를 번갈아 활성화하면 같은 위치에서 비교할 수 있다. 두 개를 동시에 켜면 면이 겹치므로 하나만 켠다.
- `Before_Close.png` / `After_Close.png`: 진입 계단과 첫 플랫폼 근접 비교.
- `Before_Overview.png` / `After_Overview.png`: 시험 구간 전체 비교.

## 이미지와 UV

텍스처: `Assets/DesertTowerLevels/TrimSheet/Textures/SandGuard_Sandstone_Trim_v1.png`

내장 ImageGen으로 생성한 베이스 컬러 시안이다. 생성 및 보정에 2048×2048을 요청했지만 반환 파일은 1254×1254였다. Unity는 ToLarger 설정으로 2048×2048로 리샘플링한다. **원본 2K 디테일을 갖는 텍스처는 아니다.** 원본을 임의로 2K라고 표시하지 않는다.

| 이미지 위→아래 | Unity UV V 범위 | 용도 |
|---|---|---|
| 밝은 사암 | 0.75–1.00 | 보행면, 개별 석재 |
| 깎인 몰딩 | 0.50–0.75 | 계단 측면과 얇은 모서리 |
| 청록 문양 | 0.25–0.50 | 플랫폼 띠 장식 |
| 풍화석 | 0.00–0.25 | 높은 계단 지지면 |

UV는 띠 경계에서 약 0.008만큼 안쪽으로 배치했다. U는 Mirror, V는 Clamp, 밉맵과 이방성 필터링 8을 사용한다. 이미지의 가로 끝이 완벽한 주기 패턴이라는 가정 대신 Mirror 반복으로 색상 이음을 막는다. 가까이서 반복 대칭이 보일 수 있다. 넓은 보행면은 2m 간격으로 면을 분할해 다른 띠까지 UV가 넘어가지 않도록 했다. 실제로 모델링된 석재 블록에는 시트 속 돌 내부를 배정해 이중 줄눈을 피했다.

시험 재질은 URP Lit, Metallic 0, Smoothness 0.14이며 별도 노멀 맵을 사용하지 않는다. 생성 이미지의 명암을 노멀로 변환하면 돌 균열과 조명까지 돌출로 해석할 수 있어 이번 시안에서는 만들지 않았다. 완성 PBR 세트가 아니라 베이스 컬러와 UV 적용을 비교하는 단계다.

## Blender 원본과 재생성

- `SandGuard_TrimPilot_v1.blend`: 시험 구간 15개 메시와 패킹한 텍스처. 원본 레벨의 나머지 부분은 포함하지 않는다.
- `build_trim_pilot.py`: 실제 V6 FBX에서 대상 메시를 가져와 UV를 만들고 FBX와 .blend를 저장한다. 실행하면 이 시험 산출물을 재생성하므로 수동 편집본은 다른 이름으로 저장한다.
- `Assets/DesertTowerLevels/TrimSheet/Editor/SandGuardTrimPilotBuilder.cs`: Unity 임포트, 원본과 맞춘 메시 변환, 프리팹·씬·이미지 생성.
- Unity 메뉴 `SandGuard > Level Art > Build Trim Sheet Comparison`으로 다시 만들 수 있다.
- `prompt.txt`: 최초 생성 프롬프트. 도구는 내장 ImageGen이며 외부 Higgsfield/유료 API 경로는 사용하지 않았다.

보정 프롬프트: “Upscale this exact game trim texture to 2048 x 2048 pixels. Preserve the four equal horizontal bands, shapes, layout, colors and texture character exactly. Output must be a 2048x2048 square production texture, no text or borders. Refine the left and right edges to tile seamlessly horizontally within each strip without visible seam, especially turquoise repeated ornament. Neutralize directional shadows slightly to function as base color. Do not change the four strip positions or widths.”

## 검증

`blender-validation.json`에는 15개 메시의 UV 작성 결과와 바운딩 박스를 기록한다. `unity-validation.txt`에는 임포트 해상도, 변환 후 오차, 원본과 동일한 충돌 메시 참조, 저장 프리팹의 15개 적용 결과를 기록한다. 플레이 이동/전투 회귀 테스트는 이번 아트 비교 범위에 포함하지 않는다. 기존 충돌과 게임플레이 씬을 변경하지 않았다.

## 난간 확장

기존 시험 구간의 진입 계단, T1–B_NE 통로, B_NE–T5 계단에 있는 양쪽 난간 3개 메시를 추가했다. 총 적용 메시 수는 15개다. 같은 시트에서 윗면은 사암, 긴 측면은 몰딩, 끝면은 청록 장식, 아래쪽은 풍화석을 사용한다. UV의 길이 축은 난간의 실제 경사를 따르므로 경사진 옆면에서도 몰딩이 평행하게 이어진다. 새 텍스처와 별도 머티리얼은 추가하지 않았다. Before_Rail_Detail.png와 After_Rail_Detail.png로 난간 근접 비교를 확인할 수 있다.
