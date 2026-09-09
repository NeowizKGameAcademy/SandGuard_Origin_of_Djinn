# 시설 건설 흐름 v1 — 검증 결과

2026-09-09 · `SandGuard > Facility > Create Missing Assets` + `Wire Into Player And Enemy Scene` 결과. 등장 연출은 "짠"(연막 → 드러남) 방식이며 홀로그램 미리보기는 뺐다.

- `captures/menu-open.png`: 플레이어가 3.5m 안으로 오면 받침대 위에 번호 메뉴가 뜬다.
- `captures/poof.png`: 1번 선택 직후. `VFX_Build_Poof` 연막이 자리를 덮고 시설은 숨겨져 있다.
- `captures/reveal.png`: 0.12초 뒤 드러남. 시설이 이미 서 있는 채로 재질 플래시 + 스케일 펀치, `VFX_Build_Complete`.
- `captures/complete.png`: 완료.

검증: `SandGuard.Facility.Tests` PlayMode 1개(접근·메뉴·건설·연막→숨김→드러남→펀치 복귀·점유·재건설 거부·미허용 시설 거부), `SandGuard.PlayerAndEnemy.Tests` 2개 통과. 통합 씬 `PlayerAndEnemyTest.unity`의 슬롯 8곳에 받침대가 놓였다.

사용법·조정 지점·미구현 목록은 `Assets/Facility/README.md`. 등장 연출 컴포넌트는 `Assets/Resources/VFX/Runtime/VfxPopIn.cs`(범용).
