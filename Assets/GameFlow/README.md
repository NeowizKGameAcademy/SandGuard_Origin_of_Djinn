# GameManager 사용법

`GameManager`는 플레이 시작 전에 자동으로 생성되고 씬이 바뀌어도 유지됩니다. 씬에 오브젝트를 직접 배치할 필요가 없습니다.

- 메인 메뉴의 `GameStart` 버튼은 `Level` 씬을 불러옵니다.
- `Quit` 버튼은 빌드에서는 게임을 종료하고, Unity Editor에서는 플레이 모드를 종료합니다.
- `StartGame()`, `ReturnToMainMenu()`, `RestartGame()`, `LoadScene(string)`을 UI Button의 On Click에 직접 연결할 수도 있습니다.
- `SetPaused(true/false)`와 `TogglePause()`는 일반 일시정지 메뉴에서 사용합니다.
- 겹쳐 열릴 수 있는 UI는 `RequestPause(owner)`와 `ReleasePause(owner)`를 사용합니다. 스킬트리 창은 이미 연결되어 있습니다.

빌드 씬 순서는 `MainScene`, `Level`, `SampleScene`입니다. 기본 메뉴/게임 씬 이름을 바꾸려면 `GameManager`의 `Main Menu Scene`, `Game Scene` 값을 변경하세요.
