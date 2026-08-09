# CSV Pipeline

스프레드시트로 저작한 CSV를 저장하면, 에디터가 그 자리에서 ScriptableObject 에셋으로 굽는 Unity 패키지입니다.
구글 시트에서 CSV를 받아오는 동기화 도구를 함께 담고 있습니다.

**에디터 전용**입니다. 런타임 어셈블리가 없어 빌드에는 구워진 에셋만 들어갑니다.

---

## 왜 쓰나

밸런스 수치를 인스펙터에서 하나씩 고치면, 표로 보고 싶을 때 볼 수가 없고 변경 이력도 남지 않습니다.
CSV를 단일 출처로 두면 표로 저작하고 git으로 이력을 남길 수 있습니다.
이 패키지는 그 사이의 반복 작업 — 파일 감지·파싱·에셋 생성·필드 기록·사라진 행 정리 — 을 대신합니다.

직접 만들면 임포터마다 되풀이하게 되는 것들을 이미 처리해 둡니다.

- **참조가 남은 에셋은 지우지 않습니다.** CSV에서 행이 사라져도, 그 에셋의 GUID가 씬·프리팹에서 발견되면
  경고만 남기고 보존합니다. 지워 버리면 git으로 파일을 되돌려도 GUID가 달라져 배선이 돌아오지 않습니다.
- **원본 CSV가 사라져도 산출물 폴더를 지우지 않습니다.** 파일을 잠깐 옮기기만 해도 수작업 데이터가
  통째로 날아가는 사고를 막습니다.
- **빈 셀은 기존 값을 보존할 수 있습니다.** 아이콘·프리팹처럼 CSV로 표현할 수 없는 필드를
  인스펙터에서 저작한 채로 두고, 표는 수치만 소유할 수 있습니다.
- **로케일 독립 파싱**입니다. 소수점이 `,`인 환경에서도 같은 값이 나옵니다.

---

## 설치

`Packages/manifest.json` 에 추가합니다.

```json
"com.toflaks.csv-pipeline": "https://github.com/toflaks98229/unity-csv-pipeline.git"
```

버전을 고정하려면 `#v0.1.0` 처럼 태그를 붙입니다.
서브모듈로 쓰려면 `Packages/com.toflaks.csv-pipeline` 경로에 두면 됩니다.

---

## 설정

CSV 폴더 위치를 **Project Settings ▸ CSV Pipeline** 에서 지정합니다.
설정 에셋이 없으면 `Assets/CSV` 를 기본값으로 씁니다. (에셋을 말없이 만들지 않습니다)

| 항목 | 뜻 | 기본값 |
|---|---|---|
| CSV 루트 | 임포트 대상 CSV들이 모인 폴더 | `Assets/CSV` |
| 시트 연동 설정 | `SheetSync_*.asset` 이 놓이는 폴더 | *(CSV 루트)*`/Editor` |
| 스냅숏 | 마지막으로 받은 시트 사본 (로컬 수정 감지용) | `Library/CsvSheetSync` |

---

## 임포터 만들기

표의 모양에 따라 베이스 넷 중 하나를 고릅니다.
어느 쪽이든 `AssetPostprocessor`의 정적 콜백에서 `CsvImport.Run<T>` 을 한 줄 부르는 형태입니다.

| 베이스 | 표의 모양 |
|---|---|
| `CsvRowImporter<T>` | 한 행 = 한 에셋 |
| `CsvGroupImporter<T>` | 같은 식별자의 여러 행 = 한 에셋 (행이 리스트 항목) |
| `CsvPatchImporter<T>` | 이미 있는 에셋의 일부 필드만 갱신 (생성·삭제 안 함) |
| `CsvSingletonImporter<T>` | 표 전체 = 프로젝트에 하나뿐인 에셋 |

### 한 행 = 한 에셋

```csharp
using CsvPipeline;
using UnityEditor;

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
            SoBaker.SetString(serialized, "clueId", row.GetString("ClueId"));
            SoBaker.SetStringIf(serialized, "title", row.GetString("Title"));
            SoBaker.SetStringIf(serialized, "body", row.GetString("Body"));
        }
    }
}
```

`Id` 가 곧 에셋 파일명이며, 같은 경로의 기존 에셋을 열어 갱신하므로 **씬·프리팹이 걸어 둔 참조가 살아 있습니다.**
CSV에서 사라진 행의 에셋은 아무도 참조하지 않을 때만 지웁니다.

### 행에 따라 만들 타입이 갈릴 때

`CreateOrLoad` 를 재정의합니다. null을 돌려주면 그 행을 건너뜁니다.

```csharp
protected override ItemEffect CreateOrLoad(string id, CsvRow row)
{
    string path = AssetPathFor(id);
    ItemEffect existing = AssetDatabase.LoadAssetAtPath<ItemEffect>(path);
    if (existing != null) return existing;

    ItemEffect fresh = row.GetString("Type") == "Heal"
        ? ScriptableObject.CreateInstance<HealItemEffect>()
        : null;

    if (fresh == null) return null;
    AssetDatabase.CreateAsset(fresh, path);
    return fresh;
}
```

### 여러 행 = 한 에셋

`Bake` 가 그룹의 행 전부를 한꺼번에 받습니다. 순서는 CSV에 나온 그대로입니다.
**이 경로에서는 `SerializedObject`가 아니라 에셋 필드에 직접 대입합니다.**

```csharp
private sealed class Definition : CsvGroupImporter<NPCRoutineData>
{
    protected override string FileName     => "NPCRoutines.csv";
    protected override string OutputFolder => "Assets/Data/Routines";

    protected override string GetGroupId(CsvRow row) => row.GetString("RoutineId");

    protected override void Bake(string groupId, IReadOnlyList<CsvRow> rows, NPCRoutineData asset)
    {
        var tasks = new List<RoutineTask>();
        foreach (CsvRow row in rows) tasks.Add(new RoutineTask { /* ... */ });
        asset.dailyRoutine = tasks;
    }
}
```

> `CsvRowImporter`/`CsvPatchImporter`/`CsvSingletonImporter` 는 반대입니다. 베이스가 `SerializedObject` 를
> 만들어 넘기고 호출 뒤 적용하므로, 그 경로에서 필드에 **직접 대입하면 적용 시점에 되돌려집니다.**
> `SoBaker` 로 쓰십시오. (private `[SerializeField]` 도 이 경로로 쓸 수 있습니다)

---

## 셀 읽기 — `CsvRow`

| 메서드 | 하는 일 |
|---|---|
| `GetString(key)` | 앞뒤 공백을 뗀 문자열. 없으면 빈 문자열 |
| `GetInt / GetFloat(key, fallback)` | 로케일 독립 파싱. 실패하면 fallback |
| `TryGetInt / TryGetFloat(key, out v)` | 컬럼이 없을 때 기존 값을 보존하고 싶을 때 |
| `GetBool(key, fallback)` | `TRUE`/`1` = 참, `FALSE`/`0` = 거짓 |
| `GetList(key)` | `;` 또는 `\|` 로 나눈 토큰 배열 (CSV의 `,` 와 충돌 방지) |
| `Has(key)` / `Keys` | 컬럼 존재 확인 / 전 컬럼 순회 (동적 컬럼용) |

## 필드 쓰기 — `SoBaker`

`Set*` 는 값을 그대로 씁니다. **`Set*If` 는 셀이 비어 있으면 건너뛰어 기존 값을 보존합니다.**

`SetString(If)` · `SetInt(If)` · `SetFloat(If)` · `SetBool(If)` · `SetEnumIf` · `SetObjectRef` ·
`SetVector3If` · `SetColorIf`

`SetEnumIf` 는 enum 상수 **이름**으로 지정하며(대소문자 무관), 이름 인덱스로 매칭하므로
패키지가 그 enum 타입을 알 필요가 없습니다.

## CSV 저작 규약

- **인코딩** UTF-8. BOM은 파서가 제거합니다.
- **콤마가 든 필드**는 큰따옴표로 감쌉니다. 따옴표 자체는 `""` 로 이스케이프합니다.
  따옴표 안의 개행도 그대로 보존됩니다. (RFC 4180)
- **리스트 셀**은 `;` 또는 `|` 로 나눕니다.
- **숫자**는 로케일 독립입니다. 소수점은 `.` 입니다.

---

## 구글 시트 연동 (선택)

CSV를 손으로 편집하는 대신 시트에서 저작하고 받아올 수 있습니다.
파이프라인은 그대로입니다 — 동기화 도구는 시트 내용으로 CSV를 덮고 강제 재임포트할 뿐이고,
그 뒤 에셋 생성은 원래 경로를 그대로 탑니다.

```
Google Sheets ──(에디터가 주기적으로 당김)──▶ CSV 루트/*.csv
                                                   │ (AssetPostprocessor)
                                                   ▼
                                           ScriptableObject 재생성
```

### 준비

1. 시트를 **공유 → 링크가 있는 모든 사용자 → 뷰어** 로 설정합니다.
2. 대상 탭을 연 상태의 **주소를 그대로 복사**합니다.
3. `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기` 로 CSV마다 설정 에셋을 만듭니다.
4. 에셋의 `Sheet Url` 에 붙여넣고 `Enabled` 를 켭니다.

시트 ID와 gid를 따로 받지 않는 이유는, 그 둘을 손으로 옮겨 적는 과정이 실수가 가장 많이 나는
지점이기 때문입니다. 특히 gid를 잘못 적으면 **엉뚱한 탭의 내용이 조용히 들어옵니다.**

### 메뉴

| 메뉴 | 하는 일 |
|---|---|
| `Tools ▸ CSV Pipeline ▸ Rebuild All Data` | CSV 루트의 전 CSV를 강제 재임포트 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet에서 받기` | 켜진 항목을 받아 **바뀐 파일만** 기록·재임포트 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet와 비교만` | 차이만 보고, 파일은 쓰지 않음 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기` | 설정이 없는 CSV에 에셋 생성 |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 폴더 열기` | 설정 폴더 선택 |

`Rebuild All Data` 가 필요한 이유는 `AssetPostprocessor` 가 파일이 **변경될 때만** 발화하기 때문입니다.
임포터를 고친 뒤 산출물을 다시 굽거나, 새 CSV를 처음 굽고 싶을 때 씁니다.

### 안전장치

- **HTML 응답 거부** — 시트가 공개돼 있지 않으면 구글은 오류가 아니라 **로그인 HTML을 HTTP 200으로**
  돌려줍니다. 그대로 기록하면 CSV가 HTML로 덮여 에셋이 통째로 망가지므로 감지해 거부합니다.
- **헤더 불일치 확인** — 첫 줄이 다르면 열을 바꿨거나 엉뚱한 탭을 가리키는 것이라 확인을 받습니다.
  (자동 받기 경로에서는 대화상자를 띄울 수 없으므로 건너뛰고 경고만 남깁니다)
- **동일 내용은 기록하지 않음** — 불필요한 재임포트와 git 잡음을 막습니다.
- **로컬 수정 감지** — 마지막 동기화본과 비교해 손댄 흔적을 알립니다.

### ⚠️ 연동을 켜면 진실의 소유자가 옮겨갑니다

켠 파일은 **시트가 저작 원본, 로컬 CSV는 사본, git은 이력**입니다.
양쪽에서 고치면 값이 갈라지고, 동기화가 도는 순간 CSV 쪽 수정이 사라집니다.

**코드에서 컬럼을 늘렸다면 시트에도 붙여넣어야 합니다.** 헤더가 어긋난 동안 자동 받기는 그 파일을
건너뛰고, 그 상태로 받아 버리면 새 컬럼이 통째로 사라집니다. (시트에는 아직 없으므로)

---

## 라이선스

MIT
