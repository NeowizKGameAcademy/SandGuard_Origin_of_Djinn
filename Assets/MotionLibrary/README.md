# 모션 라이브러리

`SandGuard > Animation Library > Open Library`로 이 폴더를 찾습니다.

2026-09-09 전체 가져오기 완료: **174개 FBX / 174개 Humanoid 클립**. Magic 56개, Sword and Shield 51개, Great Sword 51개, 개별 다운로드 6개, 플레이어 원본 6개, 적과 함께 받은 모션 4개입니다. 동일 파일의 별칭 39개는 `manifest.json`에 기록했습니다. 선정본은 모두 이 별칭에 포함되어 별도의 중복 폴더를 만들지 않았습니다.

같은 날 `Pro Melee Axe Pack`의 **47개 모션**을 추가해 현재 총 **221개 Humanoid 클립**입니다. 모델 파일 `bandit1.fbx`는 제외했습니다. 새 모션의 Humanoid 임포트와 원본 SHA-256 일치를 확인했으며 미리보기 재생은 하지 않았습니다. 가져오기 로그: `Logs/motion-library-axe-import.log`.

## 하나씩 확인하기

1. 팩 폴더를 열고 원하는 FBX의 펼침 화살표를 누릅니다.
2. 내부의 애니메이션 클립 아이콘을 선택합니다.
3. Inspector 하단 Preview를 펼치고 재생합니다. 시간 막대로 특정 자세를 확인합니다.
4. FBX 자체를 선택한 경우에는 Animation 탭 하단에서 클립을 미리보기할 수 있습니다.
5. 대상 캐릭터로 확인하려면 Preview의 모델 선택 기능에 해당 캐릭터 모델/외형 프리팹을 지정합니다. 현재 프리팹의 추가 손 보정은 이 원본 클립 미리보기에 적용되지 않습니다.

Project 검색에서 `t:AnimationClip`과 `attack`, `idle`, `walk`, `cast` 등의 이름을 조합해 찾을 수 있습니다. 이 폴더 범위에서 검색하면 다른 프로젝트 자산이 섞이지 않습니다.

## 구성

- `Pro Magic Pack`: 마법·빈손 이동·피격·사망
- `Pro Sword and Shield Pack`: 검·방패 동작
- `Great Sword Pack`: 대검·양손 동작
- `Pro Melee Axe Pack`: 도끼 근접 공격·연속 공격·방어·장비 꺼내기·이동 동작
- `Standalone`: 단검·닌자 이동 등 개별 다운로드
- `protagonist`, `bandit-*`: 캐릭터와 함께 받은 모션
- `animation-selection-v1`: 다른 원본과 파일 내용이 다른 선정본만 포함

원본 `Docs/model-art`는 보존합니다. SHA-256이 같은 복사본은 한 번만 가져오며, 대응 경로는 `manifest.json`의 `aliases`에 기록합니다. 팩의 `bandit1.fbx`와 애니메이션 없는 정적 몸체는 모션 목록에서 제외합니다.

## 적용과 재가져오기

모든 클립은 자체 Humanoid Avatar로 가져옵니다. 원본 전체 구간을 유지하고 반복 설정은 기본 꺼짐입니다. 실제 사용 시 필요한 클립의 Loop Time과 재생 속도를 조절하세요. 수평 루트 이동은 추출하며, 현재 Enemy/Player처럼 Apply Root Motion을 끈 Controller에 연결하면 제자리 모션으로 사용합니다.

라이브러리를 가져오는 작업은 기존 Enemy/Player의 Controller나 연결된 클립을 바꾸지 않습니다. 골라낸 클립을 Animator의 Motion에 직접 연결합니다.

검증: 모든 클립의 Humanoid Avatar 유효성·양수 재생 길이·원본 SHA-256 일치를 확인했습니다. Unity 6000.2.8f1 Inspector에서 `Great Sword Pack/great sword attack`의 기본 캐릭터 미리보기 재생도 확인했습니다. 상태 기록은 `Logs/motion-library-status.txt`입니다.

`SandGuard > Animation Library > Import All Local Motions`로 다시 가져올 수 있습니다. 동일한 원본은 기존 임포트 설정을 유지하며, 원본 내용이 바뀐 파일은 갱신합니다. 이 폴더의 FBX 원본 자체를 편집하면 재가져오기 때 원본으로 복원되므로 개별 수정본은 별도 폴더에 저장하세요.
