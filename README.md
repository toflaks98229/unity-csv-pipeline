# CSV Pipeline

스프레드시트로 저작한 표를 저장하면, 에디터가 그 자리에서 ScriptableObject 에셋으로 굽는 Unity 패키지입니다.
구글 시트에서 표를 받아오는 동기화와, 에셋을 다시 표로 뽑는 내보내기를 함께 담고 있습니다.

**에디터 전용**입니다. 런타임 어셈블리가 없어 빌드에는 구워진 에셋만 들어갑니다.

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

---

## 왜 쓰나

밸런스 수치를 인스펙터에서 하나씩 고치면, 표로 보고 싶을 때 볼 수가 없고 변경 이력도 남지 않습니다.
표를 단일 출처로 두면 표로 저작하고 git으로 이력을 남길 수 있습니다.
이 패키지는 그 사이의 반복 작업 — 파일 감지·파싱·에셋 생성·필드 기록·사라진 행 정리 — 을 대신합니다.

직접 만들면 임포터마다 되풀이하게 되는 것들을 이미 처리해 둡니다.

- **참조가 남은 에셋은 지우지 않습니다.** 표에서 행이 사라져도, 그 에셋의 GUID가 씬·프리팹에서 발견되면
  경고만 남기고 보존합니다. 지워 버리면 git으로 파일을 되돌려도 GUID가 달라져 배선이 돌아오지 않습니다.
- **원본 표가 사라져도 산출물 폴더를 지우지 않습니다.** 파일을 잠깐 옮기기만 해도 수작업 데이터가
  통째로 날아가는 사고를 막습니다.
- **빈 셀은 기본적으로 기존 값을 보존합니다.** 아이콘·프리팹처럼 표로 표현할 수 없는 필드를
  인스펙터에서 저작한 채로 두고, 표는 수치만 소유할 수 있습니다.
- **열 이름이 틀리면 멈춥니다.** 빠진 열을 빈 셀로 취급해 조용히 기본값을 굽지 않습니다.
  대소문자만 다른 열이 있으면 오타로 보고 함께 알립니다.
- **로케일 독립 파싱**입니다. 소수점이 `,`인 환경에서도 같은 값이 나옵니다.

---

## 설치

`Packages/manifest.json` 에 추가합니다.

```json
"com.toflaks.csv-pipeline": "https://github.com/toflaks98229/unity-csv-pipeline.git"
```

버전을 고정하려면 `#v0.4.0` 처럼 태그를 붙입니다.
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

`[CsvAsset]` 쪽 옵션도 있습니다. `AutoMap = false` 면 `[CsvColumn]` 을 붙인 필드만,
`DeleteMissing = false` 면 표에서 사라진 행의 에셋을 정리하지 않습니다.

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
| `Tools ▸ CSV Pipeline ▸ Rebuild All Data` | CSV 루트의 전 표를 강제 재임포트 |
| `Tools ▸ CSV Pipeline ▸ ScriptableObject를 표로 내보내기` | 에셋에서 표를 다시 뽑음 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet에서 받기` | 켜진 항목을 받아 **바뀐 파일만** 기록·재임포트 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet와 비교만` | 차이만 보고, 파일은 쓰지 않음 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기` | 설정이 없는 표에 에셋 생성 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 폴더 열기` | 설정 폴더 선택 |

`Rebuild All Data` 가 필요한 이유는 `AssetPostprocessor` 가 파일이 **변경될 때만** 발화하기 때문입니다.
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

## 라이선스

MIT
