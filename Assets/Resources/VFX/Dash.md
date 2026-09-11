# 모래 대시

- `Prefabs/VFX_Dash_SonicBoom.prefab`: 출발 위치에 남는 얇은 모래빛 공기 링과 모래 알갱이. 약 0.3초 재생.
- `Prefabs/VFX_Dash_Airflow.prefab`: 어깨·허리 주변에서 뒤로 뻗는 기류 3줄. 루프이며 방출 종료 후 0.17초 안에 페이드.

두 프리팹 모두 로컬 +Z가 진행 방향입니다. 플레이어의 `PlayerDashVfx`가 `DashStarted` 이벤트와 `DashDirection`을 읽어 몸 높이에서 생성합니다. 대시 종료·사망으로 `IsDashing`이 꺼지면 방출을 종료하고, 컴포넌트 비활성화 시 생성한 효과를 제거합니다. 일시정지에는 파티클 시간이 함께 멈춥니다. 카메라 효과는 포함하지 않습니다.

Unity 메뉴 `DesertTower > VFX > Build and Connect Sand Dash`로 재생성 및 플레이어 프리팹 연결이 가능합니다. 공유 VFX 머티리얼과 기존 이동 설정은 변경하지 않습니다.
