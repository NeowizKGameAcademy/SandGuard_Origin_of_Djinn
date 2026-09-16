# Concept materials and atmosphere pass 09

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity
컨셉 비교 항목 6번: 재질, 주변 환경, 조명.

## 변경

- 석재 미세 요철과 균열 대비를 낮췄다. 줄마다 돌의 폭과 시작 위치, 개별 돌의 색을 변화시켰다. 바닥 플랫폼에는 판석 재질을 적용했다.
- 사암/석회암/판석 색과 청록·청동 장식 대비를 조정했다. 그늘에서도 형태가 읽히도록 석재 셰이더의 최소 하늘빛을 보완했다.
- 신전 밖 6개 군집에 바위 36개, 폐허 기둥 12개와 작은 잔해를 배치했다. 바위 주변 모래 퇴적 6개는 원본 Terrain 높이를 샘플링하며 지형과 같은 모래 텍스처/노멀 좌표를 사용한다.
- 원본 Terrain 바깥에 중앙 920m 영역이 비어 있는 먼 사막 배경 메시를 추가했다. 원본 지형 높이를 변경하지 않으면서 정상 시야의 지평선을 연결한다.
- 기존 불투명 폭풍 벽을 현재 씬 전용 투명 먼지 재질로 바꾸고 밀도/속도를 낮췄다. 기존 폭풍 원본 재질은 덮어쓰지 않는다.
- 따뜻한 주광과 차가운 보조광, 옅은 거리 안개를 설정했다. 조명 프리팹 오버라이드와 카메라 원거리 클리핑 1800m를 씬에 저장했다.
- 배경은 콜라이더가 없는 시각용이다. 플레이 가능한 지형의 확장이 아니다.

## 보존 및 검증

- 09-environment-validation.txt: 바위/폐허/근거리 모래 퇴적의 경계 상자가 +/-88m 보호 영역에 침범한 항목 0. 원거리 배경은 생성 시 중앙 +/-460m와 겹치는 면을 제외한다.
- Unity 원본 플랫폼·계단 메시 참조와 월드 변환 보존 검사 통과.
- 원본 보행면 1,356점 누락 0, 최대 높이 오차 약 0.00000763m.
- CharacterController.Move: 플랫폼 17개, 통로 22개 양방향, 벽 밀기 92곳 통과. 밀기 상승 0m, 반복 점프 최고 상승 1.546m, 실패 0.
- URP Forward+의 _CLUSTER_LIGHT_LOOP 변형이 누락돼 씬 재진입 시 석재 주광이 반영되지 않던 문제를 수정했다. 표준 UniversalFragmentPBR 경로와 부드러운 그림자를 유지한다.
- 셰이더 컴파일과 씬 생성 성공. 저장된 씬을 다시 열어 근접 화면과 정상 지평선을 검토했다.
- 원본 Terrain 데이터와 공유 폭풍 재질 변경 없음. 모델/조각상/원본 경사 컬라이더는 유지한다.
- 전체 전투·스킬 조합, GPU 성능/LOD 검증은 이번 자동 검사에 포함되지 않는다.

## 재생성 및 편집

- TempleCourtyardBuilder.Build -courtyardStage 09-atmosphere가 기존 건축과 재질, TempleCourtyardAtmosphere.Apply를 적용한다.
- TempleRepairCaptures.Run이 이동 검사와 09-player-*.png 촬영을 수행한다.
- 환경 배치 코드: Assets/TempleArt/Editor/TempleCourtyardAtmosphere.cs
- 생성 메시/씬 전용 재질: Assets/TempleArt/ArchitectureV2/Environment
- Blender 건축 모델은 이번 단계에서 재생성하지 않았다. 환경 소품은 Unity 편집 가능한 메시 자산이다.
- 주요 이미지: 09-atmosphere-in-level.png, 09-player-entry.png, 09-player-landscape.png, 09-player-summit-landscape.png.