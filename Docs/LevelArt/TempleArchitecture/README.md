# Desert Temple — concept facade

최신 개선본은 `V2/README.md`를 참고한다. 적용 씬은 `Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity`이며, 아래 내용과 이 폴더의 V1 촬영은 이전 단계 비교용으로 보관한다.

적용 씬: `Assets/TempleArt/Architecture/Level_TempleArchitecture.unity`

재사용 프리팹: `Assets/TempleArt/Architecture/DesertTemple_ConceptFacade.prefab`

Blender 원본: `Temple_Architecture_v1.blend`

`Assets/TempleArt/Scenes/Level_TempleArt.unity`를 복사한 씬에 컨셉 외관을 적용했다. 원본 레벨과 원본 DesertTemple FBX는 수정하지 않았다. 사구, 묻힌 도시 유적, 봉인 모래폭풍은 복사한 씬에 포함돼 있다.

## 컨셉과 비교

참조: `Docs/concept-art/desert-temple-architecture-v2.png`

| 요소 | 적용 결과 |
|---|---|
| 방어 플랫폼과 계단 | 원본 17개 플랫폼의 위치·높이·크기와 22개 계단 연결 유지. 계단 몸체·디딤판·난간을 포함한 83개 메시 참조 및 월드 변환을 원본 씬과 비교했다. |
| 기단의 덩어리 | 잔단이 반복되는 피라미드 외피를 큰 석조 기단으로 정리. 보행면 아래로 제한한 구조 메시와 충돌체를 추가했다. |
| 회랑 | 벽에서 돌출한 석재 지붕, 사각 석주, 기둥 머리와 주춧돌, 뒤쪽 어두운 벽으로 깊이를 만들었다. 대각선 계단의 짧은 틈에는 처마를 만들지 않는다. |
| 외벽 장식 | 돌출 처마와 벽기둥, 청록색 띠, 청동색 테두리, 태양과 흘러내리는 선 형태의 부조를 추가했다. |
| 석재 | 밝은 사암·석회석 색조, 큰 석재 이음매, 색 얼룩과 미세 입자를 표현하는 URP 월드 좌표 셰이더를 사용한다. |
| 형태 차이 | 원본은 중앙 플랫폼이 가장 높고 전체 외곽이 정사각형에 가깝다. 컨셉의 길쭉한 배치와 높이 비례를 그대로 복제하면 게임 구조가 달라지므로 원본 비례에 맞춰 외관을 구성했다. |
| 표현 차이 | 컨셉의 손으로 조각한 듯한 균열·모서리 파손·다양한 부조에 비해 실제 모델은 반복 모듈과 간소화한 부조를 사용한다. 컨셉과 동일한 수준의 고유 스컬프나 텍스처 베이크를 한 결과는 아니다. |

## 실제 Unity 촬영

- `architecture-overview.png`: 전체 건축 전경.
- `architecture-detail.png`: 플랫폼 아래 기단과 회랑.
- `architecture-facade.png`: 외벽 재질과 부조 근접.
- `architecture-top.png`: 수직 탑뷰.
- `architecture-in-level.png`: 모래폭풍을 포함한 레벨 전경.

건축 비교용 네 장은 촬영 중에만 모래폭풍 렌더러를 숨겼다. 저장된 씬과 마지막 레벨 전경에는 폭풍이 켜져 있다. 원경 촬영은 게임의 50m 그림자 거리 밖에서 이루어지므로, 렌더 중에만 URP의 메모리상 그림자 거리를 600m로 늘리고 종료 전에 복원한다. 공유 프로젝트 렌더링 에셋은 변경하지 않았다.

## 검증과 범위

- `route-clearance.txt`: 원본 플랫폼·디딤판의 위쪽 삼각형 중심 13,038곳에서 새 기단 충돌체가 보행면보다 올라오는지 검사.
- `validation.txt`: 플랫폼/계단 메시 및 변환 보존 검사 기록.
- `blender-validation.json`: 추가 모델의 오브젝트/삼각형 수, 사전 배치 단계에서 제외한 장식 후보 수.
- 원본 신전 충돌체를 유지하고 새 기단에만 MeshCollider를 추가했다. 처마·기둥·부조 등의 장식에는 충돌체가 없다.
- 이 검사는 전체 플레이, 캐릭터 캡슐 이동, 적 길찾기, 성능 프로파일링을 대신하지 않는다. 추가 기단은 이동 가능한 면적과 시야에 영향을 줄 수 있으므로 실제 플레이 검토가 필요하다.

## 재생성

1. Blender 백그라운드에서 `Tools/Art/build_temple_architecture.py` 실행. 보관한 `structure-reference.json`과 `body-reference.json`을 읽어 FBX와 `.blend`를 만든다.
2. Unity에서 `-batchmode -executeMethod TempleArchitectureBuilder.Refresh` 실행. 기존 적용 씬의 모델·머티리얼을 갱신하고 원본 보행 구조와 비교한 뒤 프리팹 및 촬영을 저장한다.

`Refresh`는 이 적용 씬에 한정한 제작 도구다. 해당 씬/프리팹의 수동 외관 편집을 갱신할 수 있으므로 직접 수정한 뒤에는 재생성을 주의해야 한다. 원본 보행 구조가 바뀌었을 때는 `Inspect`로 새 `survey.json`을 추출하고, 기존 두 참조 JSON을 새 조사 결과로 교체해야 한다. 전체 `survey.json`은 대용량 중간 파일이므로 현재 결과에는 포함하지 않는다.
