# CSV Pipeline

스프레드시트로 저작한 표를 저장하면, 에디터가 그 자리에서 ScriptableObject 에셋으로 굽는 Unity 패키지입니다.
구글 시트에서 표를 받아오는 동기화와, 에셋을 다시 표로 뽑는 내보내기를 함께 담고 있습니다.

**굽는 일은 전부 에디터에서 일어납니다.** 빌드에 들어가는 것은 구워진 에셋과,
`[CsvAsset]` 같은 **선언용 속성만 담긴 작은 어셈블리 하나**뿐입니다.
그 어셈블리에는 실행되는 코드가 없고 `UnityEngine` 조차 참조하지 않습니다.

> 속성을 빌드에 함께 넣는 데는 이유가 있습니다. 게임의 데이터 타입은 **런타임 타입**이고,
> 런타임 어셈블리는 에디터 어셈블리를 볼 수 없습니다. 속성이 에디터 쪽에 있으면
> 위 예제가 **에디터에서는 컴파일되고 빌드에서만 깨집니다.**

```csharp
[CsvAsset("Clues.csv", "ClueId", OutputFolder = "Assets/Data/Clues")]
public class ClueData : ScriptableObject
{
    public string title;
    public string body;
}
```

이게 전부입니다. `Clues.csv`를 저장하면 행마다 `ClueData` 에셋이 생기고 갱신됩니다.

> 동작하는 예제가 패키지에 들어 있습니다. **Package Manager ▸ CSV Pipeline ▸ Samples ▸ Quick Start**
>
> **English**: see [`Documentation~/manual-en.md`](Documentation~/manual-en.md).

---

## 왜 쓰나

밸런스 수치를 인스펙터에서 하나씩 고치면, 표로 보고 싶을 때 볼 수가 없고 변경 이력도 남지 않습니다.
표를 단일 출처로 두면 표로 저작하고 git으로 이력을 남길 수 있습니다.
이 패키지는 그 사이의 반복 작업 — 파일 감지·파싱·에셋 생성·필드 기록·사라진 행 정리 — 을 대신합니다.

직접 만들면 임포터마다 되풀이하게 되는 것들을 이미 처리해 둡니다.

- **참조가 남은 에셋은 지우지 않습니다.** 표에서 행이 사라져도, 그 에셋의 GUID가 씬·프리팹에서 발견되면
  경고만 남기고 보존합니다. 지워 버리면 git으로 파일을 되돌려도 GUID가 달라져 배선이 돌아오지 않습니다.
  **조사할 수 없는 프로젝트에서는 아예 지우지 않습니다.** (아래 참고)
- **원본 표가 사라져도 산출물 폴더를 지우지 않습니다.** 파일을 잠깐 옮기기만 해도 수작업 데이터가
  통째로 날아가는 사고를 막습니다.
- **빈 셀은 기본적으로 기존 값을 보존합니다.** 아이콘·프리팹처럼 표로 표현할 수 없는 필드를
  인스펙터에서 저작한 채로 두고, 표는 수치만 소유할 수 있습니다.
- **열 이름이 틀리면 멈춥니다.** 빠진 열을 빈 셀로 취급해 조용히 기본값을 굽지 않습니다.
  대소문자만 다른 열이 있으면 오타로 보고 함께 알립니다.
- **식별자가 겹치면 알립니다.** 같은 `Id` 를 가진 행이 둘이면 뒤 행이 앞 행을 덮습니다.
  건수로는 드러나지 않는 손실이라(앞은 '생성', 뒤는 '갱신') 줄 번호를 달아 경고합니다.
  대소문자만 다른 것도 겹침으로 봅니다 — 윈도우와 macOS에서 결과가 갈리기 때문입니다.
- **파일 이름이 될 수 없는 식별자는 거절합니다.** `Item/Sword` 처럼 경로에 쓸 수 없는 값을
  말없이 바꿔 굽지 않습니다. 고칠 곳은 표입니다.
- **로케일 독립 파싱**입니다. 소수점이 `,`인 환경에서도 같은 값이 나옵니다.

---

## 설치

`Packages/manifest.json` 에 추가합니다.

```json
"com.toflaks.csv-pipeline": "https://github.com/toflaks98229/unity-csv-pipeline.git"
```

버전을 고정하려면 `#v0.7.0` 처럼 태그를 붙입니다.
서브모듈로 쓰려면 `Packages/com.toflaks.csv-pipeline` 경로에 두면 됩니다.

### ⚠️ 비공개 저장소라면 — 자격증명이 미리 있어야 합니다

이 저장소는 비공개입니다. **Package Manager는 대화형 인증을 못 하고 암호를 물어볼 수도 없어서**,
git이 프롬프트 없이 통과하지 못하면 그냥 인증 오류로 끝납니다. 둘 중 하나를 갖춰 두십시오.

- **HTTPS** — 그 PC에서 `git clone https://github.com/toflaks98229/unity-csv-pipeline.git` 을
  한 번 수동으로 돌려 자격증명 관리자(Windows는 Git Credential Manager)에 토큰을 심습니다.
- **SSH** — 주소를 `git@github.com:toflaks98229/unity-csv-pipeline.git` 으로 쓰고,
  **암호 없는 키**를 두거나 ssh-agent에 미리 올려 둡니다.

`git`이 **Unity가 보는 PATH**에 있어야 합니다. (Git Bash 안에서만 되는 것으로는 부족합니다)
잘 되는지는 Unity를 열기 전에 이 명령으로 미리 확인할 수 있습니다. 종료코드 0이면 됩니다.

```sh
GIT_TERMINAL_PROMPT=0 git ls-remote https://github.com/toflaks98229/unity-csv-pipeline.git
```

> 서브모듈로 쓰는 경우에는 해당하지 않습니다. 이미 클론된 작업 트리를 그대로 쓰기 때문입니다.

---

## 설정

표 폴더 위치를 **Project Settings ▸ CSV Pipeline** 에서 지정합니다.
설정 에셋이 없으면 `Assets/CSV` 를 기본값으로 씁니다. (에셋을 말없이 만들지 않습니다)

| 항목 | 뜻 | 기본값 |
|---|---|---|
| CSV 루트 | 임포트 대상 표들이 모인 폴더 | `Assets/CSV` |
| 시트 연동 설정 | `SheetSync_*.asset` 이 놓이는 폴더 | *(CSV 루트)*`/Editor` |
| 스냅숏 | 마지막으로 받은 시트 사본 (로컬 수정 감지용) | `Library/CsvSheetSync` |
| 서비스 계정 키 | 비공개 시트를 읽을 때만 필요 | *(비움)* |

`.csv` 와 `.tsv` / `.tab` 을 함께 다룹니다. 구분자는 확장자로 정합니다.

---

## 표 하나 붙이기 — 코드 없이

`[CsvAsset]` 을 ScriptableObject에 붙이면 끝입니다. 임포터를 따로 쓰지 않습니다.

```csharp
[CsvAsset("Vehicles.csv", "Id", OutputFolder = "Assets/Data/Vehicles")]
public class VehicleData : ScriptableObject
{
    public float maxSpeed;          // ← MaxSpeed 열 (대소문자 무시)
    public int   trunkCapacity;     // ← TrunkCapacity 열
    [SerializeField] private string ownerId;   // private 도 됩니다
}
```

필드 이름과 열 이름을 **대소문자를 무시하고** 맞춥니다. `maxSpeed` 필드가 `MaxSpeed` 열에 붙습니다.
표에 대응 열이 없는 필드는 건드리지 않습니다.

**값이 정수로 보여도 필드가 실수면 실수입니다.** `MaxSpeed` 열의 `30` 은 `float` 로 들어갑니다.
값만 보고 타입을 정하는 도구가 가장 많이 틀리는 자리인데, 여기서는 필드가 기준이라 어긋나지 않습니다.

`OutputFolder` 를 비우면 산출물이 **원본 표 옆의 타입 이름 폴더**에 놓입니다.
설치 위치를 미리 알 수 없는 배포용 예제에 씁니다. 보통은 위처럼 적어 두는 편이 낫습니다.

### 이름이 다르거나 동작을 바꿔야 할 때

```csharp
[CsvColumn("HP", Required = true)]      public int health;
[CsvColumn(OverwriteWhenEmpty = true)]  public string note;      // 빈 셀이면 지웁니다
[CsvColumn(Separators = "|")]           public List<string> tags;
[CsvColumn(ReferenceFolder = "Assets/Data/Items")] public ItemData drop;
[CsvIgnore]                             public Sprite icon;      // 표에서 제외
```

| 옵션 | 하는 일 | 기본값 |
|---|---|---|
| `Required` | 이 열이 없으면 **표 전체를 반영하지 않습니다** | `false` |
| `OverwriteWhenEmpty` | 빈 셀로 기존 값을 덮어씁니다 | `false` (=보존) |
| `Separators` | 리스트 셀 구분자 | `;` 과 `\|` |
| `ReferenceFolder` | 오브젝트 참조를 찾을 폴더 | 프로젝트 전체 |

`[CsvAsset]` 쪽 옵션도 있습니다.

| 옵션 | 하는 일 | 기본값 |
|---|---|---|
| `OutputFolder` | 산출물이 놓이는 폴더. 비우면 원본 표 옆의 타입 이름 폴더 | *(비움)* |
| `AutoMap` | 이름이 맞는 필드를 저절로 연결 | `true` |
| `DeleteMissing` | 표에서 사라진 행의 에셋을 정리 | `true` |
| `ReconcileByPath` | 정리 대조를 이름이 아니라 **경로**로 | `false` |

`ReconcileByPath` 는 산출물 폴더에 **이 표가 만들지 않은 같은 타입 에셋**이 섞여 있을 때 켭니다.

### 다룰 수 있는 타입

`string` · 정수 계열 · `float`/`double` · `bool` · **열거형**(이름으로, 대소문자 무시) ·
`Vector2/3/4` · `Color`(`#RRGGBB`) · **오브젝트 참조**(에셋 이름으로) ·
그리고 위 전부의 **배열과 리스트**.

---

## 표 하나 붙이기 — 코드로

속성으로 표현되지 않는 표가 있습니다. 값의 의미가 다른 열에 따라 달라지거나,
행에 따라 만들 구체 타입이 갈리거나, 여러 행이 한 에셋의 리스트가 되는 경우입니다.
그럴 때는 베이스 넷 중 하나를 골라 상속합니다.

| 베이스 | 표의 모양 |
|---|---|
| `CsvRowImporter<T>` | 한 행 = 한 에셋 |
| `CsvGroupImporter<T>` | 같은 식별자의 여러 행 = 한 에셋 (행이 리스트 항목) |
| `CsvPatchImporter<T>` | 이미 있는 에셋의 일부 필드만 갱신 (생성·삭제 안 함) |
| `CsvSingletonImporter<T>` | 표 전체 = 프로젝트에 하나뿐인 에셋 |

```csharp
public sealed class ClueImporter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        => CsvImport.Run<Definition>(imported, deleted, moved);

    private sealed class Definition : CsvRowImporter<ClueData>
    {
        protected override string FileName     => "Clues.csv";
        protected override string OutputFolder => "Assets/Data/Clues";

        protected override string GetId(CsvRow row) => row.GetString("ClueId");

        protected override void Bake(CsvRow row, ClueData asset, SerializedObject serialized)
        {
            SoBaker.SetStringIf(serialized, "title", row.GetString("Title"));
            SoBaker.SetStringIf(serialized, "body", row.GetString("Body"));
        }
    }
}
```

행에 따라 만들 타입이 갈리면 `CreateOrLoad` 를 재정의합니다. null을 돌려주면 그 행을 건너뜁니다.

> **`CsvGroupImporter` 만 규칙이 반대입니다.** 나머지 셋은 베이스가 `SerializedObject` 를 만들어 넘기고
> 호출 뒤 적용하므로, 그 경로에서 필드에 **직접 대입하면 적용 시점에 되돌려집니다.** `SoBaker` 로 쓰십시오.
> 그룹 임포터는 리스트를 통째로 갈아 끼우는 자리라 에셋 필드에 직접 대입합니다.

---

## 창 하나에서 다 합니다

**`Tools ▸ CSV Pipeline ▸ CSV 파이프라인`**

갈래가 셋입니다.

| 갈래 | 무엇을 |
|---|---|
| **표** | 표마다 지금 구우면 무엇이 달라지는지. 검색·필터·펼치기, 표별 `지금 굽기` |
| **시트 연동** | 설정마다 상태 한 줄과 `받기`·`비교`·`선택` |
| **설정** | 실제 적용되는 경로들과 Project Settings 로 가는 길 |

**이 창은 사람이 버튼을 누를 때만 씁니다.** 열어 두는 것만으로는 아무것도 바뀌지 않습니다.

표 갈래는 **마우스 없이도 다 됩니다.**

| 키 | 하는 일 |
|---|---|
| `↑` `↓` · `Home` `End` | 표 고르기 |
| `→` `←` | 펼치기 · 접기 |
| `Space` | 펼침 뒤집기 |
| `Enter` | 고른 표 굽기 |
| `Ctrl`(`⌘`)`+F` | 찾기 |
| `Esc` | 검색어 지우기 |
| 오른쪽 누르기 | 그 표의 차림표 (굽기 · 표 열기 · 산출물 폴더 · 경로 복사) |

보기는 셋입니다. **바뀌는 것만** · **손볼 것만** · **전부**.

표 갈래는 지금 구우면 무엇이 생기고·바뀌고·지워지는지 적용 전에 보여 줍니다.

```
QuestData · Quests.csv                       생성 1 / 갱신 2 / 삭제 1 / 보존 1
  ＋ 생성  Quest_NightWatch      5행
  ·  갱신  Quest_DeepWell        4행
       TimeLimit    900  →  1200
       Difficulty   Normal  →  Hard
  －  삭제  Quest_Removed
  ◦  보존  Quest_Old             다른 곳에서 참조 중이라 지우지 않습니다
```

`[CsvAsset]` 로 선언한 표는 **어느 열이 무엇에서 무엇으로 바뀌는지**까지 나옵니다.
사본에 실제 변환기로 구워 본 뒤 비교하므로 미리보기와 실제 결과가 어긋나지 않습니다.
직접 작성한 임포터도 목록에 오르며, 생성·갱신·삭제·보존을 에셋 단위로 보여 줍니다.

값이 하나도 달라지지 않는 행은 올라오지 않습니다. 그래야 실제로 바뀌는 것이 눈에 들어옵니다.

### ⚠️ 임포트는 Ctrl+Z 로 되돌릴 수 없습니다

**의도적으로 지원하지 않습니다.** 한 번의 임포트는 필드 수정·에셋 생성·에셋 삭제를 함께 합니다.
Unity의 Undo는 이 중 필드 수정만 되돌릴 수 있어서, Ctrl+Z 를 받아 주면 **값은 되돌아가는데
만들어진 에셋과 지워진 에셋은 그대로 남는** 어긋난 상태가 됩니다.
그건 되돌릴 수 없는 것보다 나쁩니다. 되돌아간 줄 알고 넘어가게 되기 때문입니다.

대신 두 가지를 두었습니다.

- **미리보기** — 무엇이 달라지는지 적용 전에 확인합니다. 되돌리기가 필요 없게 만드는 쪽입니다.
- **git** — 산출물이 에셋 파일이므로 커밋해 두면 언제든 되돌아갑니다.
  이 패키지가 참조 남은 에셋을 지우지 않는 것도 같은 이유입니다. GUID가 사라지면 git으로도 못 돌아옵니다.

큰 표를 구울 때는 진행 막대가 뜨고 취소할 수 있습니다. **취소하면 읽은 데까지만 반영되고,
표에서 사라진 행의 정리는 하지 않습니다.** 아직 읽지 않은 행의 에셋을 사라진 것으로 오해해
지우면 안 되기 때문입니다.

---

### 참조 조사는 어떻게 하는가

"참조가 남은 에셋은 지우지 않는다"는 **AssetDatabase 가 임포트할 때 만들어 둔 의존성 그래프**에
묻습니다. 파일이 글자로 저장됐는지 이진으로 저장됐는지와 무관하고, 프로젝트 설정의
**프리로드 목록**까지 함께 봅니다.

같이 사라질 것들끼리의 참조는 세지 않습니다. 서로 붙잡아 주면 아무것도 정리되지 않기 때문입니다.

> 예전에는 파일을 글자로 읽어 GUID 문자열을 찾았습니다. 그 방식은 Asset Serialization 이
> `Force Text` 가 아닌 프로젝트에서 **무엇을 물어도 "참조 없음"** 을 돌려주었습니다.
> 실제로 재 보니 참조 300건 중 **0건**을 찾았습니다. 지금 방식은 같은 프로젝트에서 300건을 찾습니다.

---

## 결과 보기

임포트 결과는 표마다 **한 줄의 로그**로 나옵니다. 흩어진 로그를 뒤질 필요가 없습니다.

```
[ClueData] Clues.csv — 생성 2 / 갱신 14 / 건너뜀 1 / 보존 1
  [경고] 23행 · Tier — 'Huge'는 없는 값입니다. (가능: Small/Medium/Large)
  [경고] 41행 — 'ClueId'가 비어 있어 건너뜁니다.
  [경고] 표에서 사라졌지만 아직 참조 중이라 보존합니다: Assets/Data/Clues/Clue_Old.asset
```

문제가 있으면 로그가 경고·오류로 올라가고, 클릭하면 원본 표나 문제가 난 에셋으로 갑니다.

---

## 셀 읽기 — `CsvRow`

| 메서드 | 하는 일 |
|---|---|
| `GetString(key)` | 앞뒤 공백을 뗀 문자열. 없으면 빈 문자열 |
| `GetInt / GetFloat(key, fallback)` | 로케일 독립 파싱. 실패하면 fallback |
| `TryGetInt / TryGetFloat(key, out v)` | 컬럼이 없을 때 기존 값을 보존하고 싶을 때 |
| `GetBool(key, fallback)` | `TRUE`/`1` = 참, `FALSE`/`0` = 거짓 |
| `GetList(key)` | `;` 또는 `\|` 로 나눈 토큰 배열 |
| `Has(key)` / `HasColumn(key)` | 이 행에 셀이 있는지 / 표에 열이 있는지 |
| `LineNumber` | 원본 줄 번호. 오류 메시지에 위치를 붙일 때 |

## 필드 쓰기 — `SoBaker`

`Set*` 는 값을 그대로 씁니다. **`Set*If` 는 셀이 비어 있으면 건너뛰어 기존 값을 보존합니다.**

`SetString(If)` · `SetInt(If)` · `SetFloat(If)` · `SetBool(If)` · `SetEnumIf` · `SetObjectRef` ·
`SetVector3If` · `SetColorIf`

## 표 저작 규약

- **인코딩** UTF-8. BOM은 파서가 제거합니다.
- **구분자가 든 필드**는 큰따옴표로 감쌉니다. 따옴표 자체는 `""` 로 이스케이프합니다.
  따옴표 안의 개행도 그대로 보존됩니다. (RFC 4180)
- **리스트 셀**은 `;` 또는 `|` 로 나눕니다.
- **숫자**는 로케일 독립입니다. 소수점은 `.` 입니다.
- **`Vector`** 는 공백이나 `;` 로 나눕니다. (예: `1 0 0`)

---

## 에셋을 다시 표로 — 내보내기

에디터에서 손본 값을 표에 되돌립니다. **`[CsvAsset]` 으로 선언된 타입만** 됩니다 —
직접 작성한 임포터는 표의 구조를 코드로만 알고 있어 자동으로 되돌릴 수 없습니다.

**`Tools ▸ CSV Pipeline ▸ ScriptableObject를 표로 내보내기`**

바뀐 파일 목록을 먼저 보여 주고, 확인해야 씁니다. 내용이 같은 표는 건드리지 않아 git 잡음이 생기지 않습니다.

---

## 구글 시트 연동 (선택)

표를 손으로 편집하는 대신 시트에서 저작하고 받아올 수 있습니다.
파이프라인은 그대로입니다 — 동기화 도구는 시트 내용으로 표를 덮고 강제 재임포트할 뿐이고,
그 뒤 에셋 생성은 원래 경로를 그대로 탑니다.

```
Google Sheets ──(에디터가 주기적으로 당김)──▶ CSV 루트/*.csv
                                                   │ (AssetPostprocessor)
                                                   ▼
                                           ScriptableObject 재생성
```

### 공개 시트로 쓰기

1. 시트를 **공유 → 링크가 있는 모든 사용자 → 뷰어** 로 설정합니다.
2. 대상 탭을 연 상태의 **주소를 그대로 복사**합니다.
3. `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기` 로 표마다 설정 에셋을 만듭니다.
4. 에셋의 `Sheet Url` 에 붙여넣고 `Enabled` 를 켭니다.

시트 ID와 gid를 따로 받지 않는 이유는, 그 둘을 손으로 옮겨 적는 과정이 실수가 가장 많이 나는
지점이기 때문입니다. 특히 gid를 잘못 적으면 **엉뚱한 탭의 내용이 조용히 들어옵니다.**

### 비공개 시트로 쓰기

사내 데이터라 공개할 수 없다면 **서비스 계정**을 씁니다. 브라우저 로그인 흐름이 없어 배치 모드에서도 됩니다.

1. Google Cloud Console에서 프로젝트를 만들고 **Google Drive API**를 켭니다.
2. **서비스 계정**을 만들고 **JSON 키**를 내려받습니다.
3. 키 파일을 **`Assets` 밖**에 두고 **버전 관리에서 제외**합니다. (예: 프로젝트 루트의 `.secrets/`)
4. Project Settings ▸ CSV Pipeline 의 **서비스 계정 키**에 그 경로를 적습니다.
5. 시트를 그 서비스 계정 **이메일 주소와 공유**합니다. (뷰어면 충분합니다)

> 키 파일 내용은 로그에 절대 실리지 않습니다. 다만 키를 커밋하면 저장소를 가진 누구나 시트를 읽을 수 있으니
> `.gitignore` 에 넣었는지 확인하십시오.

### 메뉴

| 메뉴 | 하는 일 |
|---|---|
| `Tools ▸ CSV Pipeline ▸ CSV 파이프라인` | 파이프라인 창 열기 |
| `Tools ▸ CSV Pipeline ▸ 전체 다시 굽기` | CSV 루트의 전 표를 강제 재임포트 |
| `Tools ▸ CSV Pipeline ▸ 에셋을 표로 내보내기` | 에셋에서 표를 다시 뽑음 |
| `Tools ▸ CSV Pipeline ▸ 표와 산출물이 어긋나는지 확인` | 표를 고치고 굽기를 잊지 않았는지 확인 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet에서 받기` | 켜진 항목을 받아 **바뀐 파일만** 기록·재임포트 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet와 비교만` | 차이만 보고, 파일은 쓰지 않음 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 만들기` | 설정이 없는 표에 에셋 생성 |

미리보기·모두 펼치기·설정 폴더 열기는 메뉴가 아니라 **창 안에** 있습니다.

`전체 다시 굽기` 가 필요한 이유는 `AssetPostprocessor` 가 파일이 **변경될 때만** 발화하기 때문입니다.
임포터를 고친 뒤 산출물을 다시 굽거나, 새 표를 처음 굽고 싶을 때 씁니다.
끝난 뒤 `CsvRebuildMenu.AfterRebuildAll` 이벤트가 불리므로, 프로젝트별 마무리 작업을 붙일 수 있습니다.

### 안전장치

- **HTML 응답 거부** — 시트에 접근할 권한이 없으면 구글은 오류가 아니라 **로그인 HTML을 HTTP 200으로**
  돌려줍니다. 그대로 기록하면 표가 HTML로 덮여 에셋이 통째로 망가지므로 감지해 거부합니다.
- **헤더 불일치 확인** — 첫 줄이 다르면 열을 바꿨거나 엉뚱한 탭을 가리키는 것이라 확인을 받습니다.
  (자동 받기 경로에서는 대화상자를 띄울 수 없으므로 건너뛰고 경고만 남깁니다)
- **동일 내용은 기록하지 않음** — 불필요한 재임포트와 git 잡음을 막습니다.
- **로컬 수정 감지** — 마지막 동기화본과 비교해 손댄 흔적을 알립니다.

### ⚠️ 연동을 켜면 진실의 소유자가 옮겨갑니다

켠 파일은 **시트가 저작 원본, 로컬 표는 사본, git은 이력**입니다.
양쪽에서 고치면 값이 갈라지고, 동기화가 도는 순간 표 쪽 수정이 사라집니다.

**코드에서 컬럼을 늘렸다면 시트에도 붙여넣어야 합니다.** 헤더가 어긋난 동안 자동 받기는 그 파일을
건너뛰고, 그 상태로 받아 버리면 새 컬럼이 통째로 사라집니다. (시트에는 아직 없으므로)

---

## 직접 만든 임포터 검사하기

굽기 규칙은 **에셋을 하나도 만들지 않고** 확인할 수 있습니다. `MemoryAssetGateway` 를 끼우면
표도 산출물도 메모리에만 있어, 임시 폴더도 재임포트도 필요 없습니다.

```csharp
[Test]
public void 표의_값이_에셋에_들어간다()
{
    const string path = "Assets/Memory/Quests.csv";

    using var assets = new MemoryAssetGateway()
        .WithTable(path, "Id,Title,Reward\nQ_01,첫 의뢰,100\n");

    using (CsvAssets.Use(assets))
    {
        CsvImportReport report = new QuestImporter().Run(path);

        Assert.AreEqual(1, report.Created);
        Assert.AreEqual("첫 의뢰", assets.Get<QuestData>("Assets/Memory/QuestData/Q_01.asset").title);
    }
}
```

거들 것 몇 가지:

| 무엇 | 쓰임 |
|---|---|
| `WithTable(path, text)` | 표 원문을 놓습니다. 폴더도 함께 생깁니다 |
| `WithAsset(path, asset)` · `Add<T>(path)` | 이미 있는 산출물을 놓습니다 |
| `Get<T>(path)` | 구워진 결과를 읽습니다 |
| `Referenced` | 여기 넣은 경로는 "참조가 남은 것"으로 취급돼 정리에서 보존됩니다 |
| `SaveCount` | 저장이 몇 번 일어났는지 |

검사가 목록에 보이려면 소비하는 프로젝트의 `Packages/manifest.json` 에 `testables` 가 필요합니다.

```json
"testables": [ "com.toflaks.csv-pipeline" ]
```

---

## 표를 고치고 굽기를 잊지 않았는지 — CI에서 확인

표 파일이 바뀐 것은 diff 에 보입니다. **산출물이 안 바뀐 것은 diff 에 보이지 않습니다.**
없는 것은 눈에 띄지 않으니까요. 그래서 표만 고치고 굽기를 잊은 커밋이 조용히 지나갑니다.

```sh
Unity -batchmode -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
```

어긋난 표가 있으면 **종료 코드 1** 과 함께 어느 표가 왜 어긋났는지를 로그에 남깁니다.
**아무것도 쓰지 않습니다.** 판정은 파이프라인 창의 것과 같아서, 화면에서 "바뀌는 것 없음" 인 표가
CI 에서 실패하는 일은 없습니다.

---

## 고지

**네트워크.** 구글 시트 연동은 선택 기능이고, 켰을 때만 나가는 요청이 생깁니다.
접속하는 곳은 **둘뿐**입니다.

| 호스트 | 언제 |
|---|---|
| `docs.google.com` | 시트 내용을 받을 때 |
| `oauth2.googleapis.com` | 서비스 계정을 설정했을 때, 액세스 토큰을 받으러 |

**연동 설정 에셋을 만들어 켜기 전에는 아무 데도 아무것도 보내지 않습니다.**
자동 받기는 기본이 꺼짐(`autoPull = false`)입니다. 원격 측정이나 분석은 하지 않습니다.
(창의 `?` 단추는 브라우저로 이 저장소의 README 를 엽니다 — 사람이 누를 때만입니다)

**자격증명 보관.** 비공개 시트를 쓸 때 **여러분이 지정한 경로**의 구글 서비스 계정 JSON 키를 읽습니다.
키는 **프로젝트로 복사되지 않고, 빌드에 들어가지 않으며, 로그에 실리지 않습니다.**
설정 에셋에 남는 것은 파일 경로뿐입니다. 키 파일을 `Assets` 밖에 두고 버전 관리에서 빼는 것은
쓰는 쪽의 몫이며, 그 방법은 위 [비공개 시트로 쓰기](#비공개-시트로-쓰기) 에 적어 두었습니다.

**외부 의존성.** 없습니다. 패키지 의존성이 하나도 없고 남의 코드를 담고 있지 않습니다.

**AI 보조.** 이 패키지는 AI 도구의 보조를 받아 작성했습니다. 코드는 사람이 검토했고,
난독화하지 않았으며, 함께 들어 있는 자동 검사가 덮고 있습니다.

> 에셋스토어에 낸다면 이 네 가지는 **스토어 설명란에도** 있어야 합니다.
> (네트워크 사용·키 보관 방식·AI 고지)

---

## 라이선스

MIT
