# 적 idle/walk 도발

- 원본: `Docs/Audio/auda/도발 *_auda*.wav` 8개. Unity용 복사본은 `Assets/8.Audio/AudioResource`에 있다.
- 큐: `Assets/8.Audio/Cues/Enemy/Enemy_Voice_Taunt.asset`.
- 기본 `Enemy.prefab`의 `SfxEnemyTaunt`를 다섯 적 변형이 상속한다.
- 살아 있고 AI가 활성화된 적의 `Locomotion`(Idle/Walk 블렌드 트리)에서만 재생한다. 공격, 다른 애니메이션으로 전환, 낙하, 구속, 사망, 비활성화 때 멈춘다.
- 첫 재생까지 0.8~2.5초를 기다린다. 재생이 끝난 후에도 0.8~2.5초를 기다리며, 매번 큐에서 랜덤 클립을 고른다. 기존 `SfxCue`의 직전 클립 반복 방지를 사용한다.
- 상태 이탈과 풀 재사용 때 대기 시간을 새로 뽑는다. 게임 일시정지 중에는 대기 시간이 진행되지 않는다.
- Voice 믹서, 볼륨 0.6, 3D 선형 감쇠 2~25m. 청취자에서 25m보다 먼 적은 재생하지 않는다.
- 전체 동시 도발 최대 4개, 도발 시작 사이 최소 0.35초. 한도를 채우면 기존 소리를 끊지 않고 다음 랜덤 시각에 재시도한다.

빈도는 기본 적 프리팹의 `Min Delay` / `Max Delay`, 클립과 음량·거리는 `Enemy_Voice_Taunt` 큐에서 조절한다. `AudioWiring`, `SfxCatalog`, `AudioResourceMap`에도 등록했으므로 오디오 재배선 시 유지된다.
