# DesertTemple V6 부하 테스트

## 실행

Unity 메뉴 `SandGuard > Load Test > Build Windows Benchmark`로 전용 씬과 Windows 빌드를 만든다. 닫힌 에디터에서는 `-executeMethod SandGuard.LoadTest.Editor.TempleLoadTestBuilder.Build`로도 빌드할 수 있다.

- 테스트 씬: `Assets/LoadTest/Generated/DesertTemple_LoadTest.unity`
- 실행 파일: `Builds/TempleLoadTest/TempleLoadTest.exe`
- 결과: `Logs/TempleLoadTest/<날짜-모드>/`

프로젝트 루트에서 PowerShell로 실행:

```powershell
./Tools/LoadTest/Run-TempleLoadTest.ps1 -Mode Smoke
./Tools/LoadTest/Run-TempleLoadTest.ps1 -Mode Sweep
./Tools/LoadTest/Run-TempleLoadTest.ps1 -Mode Refine -Counts '75,100,125'
./Tools/LoadTest/Run-TempleLoadTest.ps1 -Mode Heavy -Counts '75,100,125'
./Tools/LoadTest/Run-TempleLoadTest.ps1 -Mode Soak -SoakCount 75
```

Smoke는 기능 검증용 짧은 실행이며 성능 결론을 내리지 않는다. Sweep는 0·25·50·100·150·200마리, 네 상황과 두 카메라를 각각 준비 10초 + 측정 60초로 실행하므로 약 56분이다. Refine/Heavy는 지정한 인원마다 3회 반복한다. Soak는 짧은 빈 장면 측정 후 지정 인원으로 이동을 10분 측정한다. 이때 웨이브 종료 여부는 검사하지 않는다.

## 조건

원본 `Assets/1.Scene/Level.unity` 저장본을 복사한다. 원본 씬, 웨이브, 적 프리팹은 수정하지 않는다. 원본의 카메라 대신 테스트 카메라 두 구도를 사용하고, 원본의 자동 웨이브와 기존 적은 전용 씬에서 비활성화한다. 장면에 저장된 NavMesh의 연결 가능한 영역에서 지점을 뽑는다. 저장하지 않은 원본 수정은 반영되지 않으므로 지형 변경 후 저장하고 재빌드한다.

- 1920×1080 창 모드, DirectX 11, 현재 품질 설정, VSync 해제, FPS 제한 해제, 실시간 1초 기준.
- 일반 혼합: Swordsman / Assassin / ShieldGuard / HammerBrute / Chief 균등.
- Heavy: HammerBrute 60%, Chief 40%.
- Overview: 경로 전체가 보이는 시점. Close: 밀집 지점 주변 시점.
- Travel: 연결된 경로를 순환. Crowd: 같은 지점으로 진입하며 회피/밀집 부담 측정.
- CombatProxy: 체력이 충분한 대체 타워 표적 + 적의 실제 근접 공격, 10마리당 초당 4회 합성 피격. 실제 타워의 표적 검색·발사체 비용은 **포함하지 않는다**.
- Churn: 5초마다 전원 사망, 사망 연출 후 실제 EnemyPool에서 재대여. 사망 연출 중 생존 수가 줄어드므로 지속 인원 상한 판정에서 제외한다.
- 동일 인원 유지 상황에서도 첫 생성 비용은 `spawn_ms`로 별도 기록한다. 측정 구간은 준비 시간 이후다.

## 결과 해석

`results.csv`는 평균 FPS, P50/P95/P99/최대 프레임 시간, CPU/GPU FrameTiming 평균, GC 횟수, 메모리, 생존 수 하한, NavMesh 이탈 수 상한, 총 이동 거리와 생성/사망/재사용 횟수를 기록한다. 각 조건의 전체 프레임 시간도 별도 CSV에 저장된다. `environment.txt`에 PC·품질·Unity 버전·해상도 등을 기록한다. CPU/GPU timing_samples가 0이면 측정 불가이며 비용이 0이라는 뜻이 아니다. 메모리는 Unity 할당량과 관리 힙이며 GPU VRAM이나 OS 전체 사용량은 아니다.

자동 통과 기준은 P95 ≤ 16.67ms, 설정 인원 유지, NavMesh 이탈 없음이다. 여섯 가지 지속 인원 상황/시점 조합을 60초 이상 모두 통과한 최고 **실측 인원**에서 25% 여유를 둔 잠정 예산을 표시한다. Churn 최대 지연, Heavy 결과, 장기 메모리 추이도 별도로 확인해야 한다. 현재 도구는 웨이브 진행/종료와 실제 타워 전투를 검증하지 않으므로 최종 게임 상한 확정 전에 통합 플레이 검증이 필요하다.

빌드 비교에서는 다른 프로그램 부하와 품질·해상도를 동일하게 유지한다. **렌더링 창을 숨기거나 최소화하지 않는다.** 실제 카메라 렌더링 횟수가 측정 프레임의 90% 미만이면 통과 처리하지 않는다. 각 조건의 마지막 화면도 PNG로 저장한다. 가장 느린 프레임과 GC를 함께 보고 평균 FPS만으로 판단하지 않는다. 결과 폴더의 `COMPLETE.txt`가 없으면 중단/실패한 실행이다.

DirectX 12 실행에서는 25마리 측정 이후 장치 오류(`887a0005`, removed reason `887a0007`)와 비정상 메모리 증가가 관측되어 전용 빌드를 DirectX 11로 고정했다. 프로젝트 본래 그래픽 API 설정은 빌드 후 복구한다. DX11 결과를 DX12 성능 수치로 간주하지 않는다.

특정 구간만 재현할 때는 실행 파일에 `--scenario Travel --view Overview --counts 0,25,50 --seconds 60`을 전달한다. 생략하면 전체 시나리오/시점을 실행한다.
