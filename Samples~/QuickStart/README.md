# Quick Start

표 한 장이 ScriptableObject 에셋이 되는 최소 예제입니다. 임포터 코드는 없습니다.

## 들어 있는 것

| 파일 | 역할 |
|---|---|
| `Quests.csv` | 퀘스트 4개가 든 표 |
| `QuestData.cs` | `[CsvAsset]` 이 붙은 ScriptableObject |

## 해 보기

1. 이 샘플을 가져오면 `Assets/Samples/CSV Pipeline/<버전>/Quick Start/` 에 놓입니다.
2. 스크립트가 컴파일되면 **`Quests.csv` 를 한 번 다시 저장**하거나
   `Tools ▸ CSV Pipeline ▸ Rebuild All Data` 를 실행합니다.
   (`AssetPostprocessor` 는 파일이 *변경될 때만* 발화하므로, 이미 놓여 있는 파일은 한 번 건드려 줘야 합니다.
   CSV 루트가 기본값과 다르면 `Rebuild All Data` 대신 파일을 다시 저장하는 쪽이 확실합니다.)
3. 표 옆에 **`QuestData/` 폴더**가 생기고 행마다 에셋이 하나씩 놓입니다.

## 이 예제가 보여 주는 것

**필드 이름과 열 이름은 대소문자를 가리지 않고 붙습니다.** `recommendedLevel` 필드가
`RecommendedLevel` 열에 붙습니다. 따로 적을 것이 없습니다.

**값이 정수로 보여도 필드가 실수면 실수입니다.** `TimeLimit` 의 `600` 은 `float` 로 들어갑니다.
값만 보고 타입을 정하는 방식이 가장 많이 틀리는 자리인데, 여기서는 필드가 기준이라 어긋나지 않습니다.

**한 셀에 여러 값**은 `;` 로 나눕니다. `Rewards` 열이 `List<string>` 이 됩니다.

**열 이름이 다르면** `[CsvColumn("Requires")]` 로 지정합니다.

**표로 저작하지 않을 필드**는 `[CsvIgnore]` 로 뺍니다. `banner` 를 인스펙터에서 붙여 두고
표를 다시 임포트해도 그대로 남습니다.

**빈 셀은 기존 값을 지우지 않습니다.** `Quest_FirstSteps` 의 `Requires` 가 비어 있는데,
이건 "선행 퀘스트를 지우라"가 아니라 "건드리지 말라"는 뜻입니다.
비우는 것을 저작으로 삼고 싶으면 `[CsvColumn(OverwriteWhenEmpty = true)]` 를 붙입니다.

## 되돌리기

에디터에서 값을 고친 뒤 `Tools ▸ CSV Pipeline ▸ ScriptableObject를 표로 내보내기` 를 실행하면
`Quests.csv` 가 에셋 내용으로 다시 쓰입니다. 덮어쓰기 전에 무엇이 바뀌는지 먼저 보여 줍니다.

## 표를 바꿔 보기

- 행을 하나 지우고 다시 저장하면 그 에셋도 지워집니다.
  단 **씬이나 프리팹이 그 에셋을 참조하고 있으면 지우지 않고 경고만 남깁니다.**
- `Difficulty` 에 `Huge` 처럼 없는 값을 적으면, 그 행만 경고를 내고 **가능한 값 목록**을 알려 줍니다.
- `Title` 열 이름을 `Titel` 로 바꾸면 오타로 보고 알려 줍니다.
