# 에셋스토어 출시 준비 조사

조사일 2026-08-09 · 대상 `com.toflaks.csv-pipeline` v0.1.1 (2,261줄, Editor 전용)

각 항목의 **근거**를 표시합니다. `측정` = 이 저장소에서 실제로 돌려 본 것,
`인용` = Unity 공식 문서·스토어 페이지에서 확인한 것, `판단` = 조사에 기반한 내 의견입니다.

> **진행 상황 (v0.3.0 기준)** — §5의 기능 격차 **F1~F6 전부**, 제출 요건 **B3(샘플)**,
> 위험 **R4(테스트·샘플 부재)** 를 해소했습니다.
> 아래 본문은 조사 시점(v0.1.1)의 기록이며, 해소된 항목에는 ✅ 를 붙였습니다.
>
> **남은 것**: 제출 요건 **B1**(최소 Unity 2022.3)·**B2**(영문 문서),
> 위험 **R1**(Domain Reload 정적 상태)·**R2**(상시 콜백)·**R3**(네트워크 고지),
> 그리고 §6의 UI/UX 격차 U1~U7.

---

## 0. 한 줄 결론

**기술 품질은 이미 상당 부분 요건을 넘습니다. 막는 것은 언어와 진입 장벽 두 가지입니다.**

지금 제출하면 걸리는 하드 요건은 3개(최소 Unity 버전·샘플 부재·영문 문서)이고 전부 기계적으로 해결됩니다.
그러나 그것만 고쳐 올려도 팔리지 않을 가능성이 높습니다. **이 도구는 C# 서브클래스를 작성해야만 쓸 수 있는데,
경쟁 도구들은 "코딩 불필요"를 전면에 내겁니다.** 시장 조사 결과 이 니치의 수요 신호 자체가 약한 것도 함께 고려해야 합니다(§4).

---

## 1. 측정한 현재 상태

| 항목 | 값 | 근거 |
|---|---|---|
| 코드량 | 2,261줄 / C# 17파일 | 측정 |
| 컴파일러 경고 | **0개** (`NoWarn` 해제 후 빌드) | 측정 |
| 컴파일러 오류 | **0개** (게임 어셈블리 참조 없이 단독 빌드) | 측정 |
| 최장 경로 길이 | 51자 (제한 150자) | 측정 |
| 자동화 테스트 | ~~0개~~ → **EditMode 60여 건** (v0.3.0) | 측정 |
| 샘플/데모 | ~~없음~~ → **Samples~/QuickStart** (v0.3.0) | 측정 |
| 사용자에게 보이는 문자열 | 50곳 중 **33곳이 한국어** | 측정 |
| 한국어가 든 소스 파일 | **17 / 17** (전부) | 측정 |
| `package.json`의 최소 Unity | `2021.3` | 측정 |

---

## 2. 제출 요건 대조

Unity [Submission Guidelines](https://assetstore.unity.com/publishing/submission-guidelines) 기준입니다.

### 2-1. 이미 충족 (측정으로 확인)

| 요건 | 현재 |
|---|---|
| 단일 루트 폴더 | ✅ 패키지 루트 하나 |
| 경로 150자 이하 | ✅ 최장 51자 |
| 모든 코드가 사용자 선언 네임스페이스 안 | ✅ 전부 `CsvPipeline` (단 §5-4 참고) |
| 네임스페이스에 Unity 등 상표 미포함 | ✅ |
| 유효한 asmdef (JSON) | ✅ `CsvPipeline.Editor` |
| meta 파일 존재·중복 없음 | ✅ 29개, GUID 고정 |
| **설정 후 패키지에서 오류·경고 미발생** | ✅ 경고 0 / 오류 0 |
| 난독화되지 않은, 수정 가능한 코드 | ✅ |
| 사용자 동의 없는 패키지 자동 설치 없음 | ✅ 의존성 0 |

### 2-2. 미충족 — 제출 전 필수

| # | 요건 | 현재 | 근거 |
|---|---|---|---|
| **B1** | 신규 제출은 **Unity 2022.3 이상** | `2021.3` | 인용 |
| **B2** | 코드·설정이 있는 패키지는 **문서 필수, "comprehensive"** | README는 있으나 **전부 한국어** | 인용 + 측정 |
| ✅ **B3** | 외부 에셋을 다루는 도구는 **데모용 샘플 에셋 포함** | ~~없음~~ → Samples~/QuickStart | 인용 + 측정 |

> B3의 원문은 "Tools that manipulate external assets must include sample assets for demonstration"입니다.
> 이 패키지는 CSV(외부 파일)를 읽어 에셋을 만드는 도구이므로 정면으로 해당합니다.

### 2-3. 위험 — 심사에서 걸릴 수 있음

| # | 항목 | 내용 | 근거 |
|---|---|---|---|
| **R1** | **Domain Reload 비활성 지원** (Unity 6.6+ 요구) | 가변 정적 3개: `CsvPipelineSettings._cached`, `GoogleSheetSync._running`, `_nextAutoPullTime`. 특히 `_running`이 참인 채로 남으면 **동기화가 영영 안 돕니다** | 인용 + 측정 |
| **R2** | 상시 에디터 콜백 | `[InitializeOnLoadMethod]`가 연동 설정이 하나도 없어도 `EditorApplication.update`를 무조건 겁니다. 30초마다 `AssetDatabase.FindAssets` | 측정 |
| **R3** | 네트워크 사용 고지 | 구글 시트로 나가는 외부 요청이 있습니다. 자동 받기 기본값이 꺼짐(`autoPull = false`)인 점은 유리 | 측정 |
| ✅ **R4** | 전문적 완성도 심사 | 테스트·샘플은 갖췄습니다. **에디터 창은 여전히 없습니다**(§6 U2) | 인용 + 판단 |

---

## 3. 이 패키지가 이미 잘하는 것 (마케팅 자산)

경쟁 도구 페이지에서 **광고되지 않는** 것들입니다. 팔 거리가 된다면 여기입니다.

| 강점 | 실제 동작 |
|---|---|
| **참조가 남은 에셋은 안 지움** | CSV에서 행이 사라져도 그 GUID가 씬·프리팹에 있으면 경고만 남기고 보존 |
| **원본이 사라져도 산출물 폴더 보존** | CSV를 잠깐 옮기기만 해도 수작업 데이터가 날아가는 사고를 막음 |
| **HTML 응답 거부** | 시트 공개 설정 누락 시 구글은 오류가 아니라 로그인 HTML을 **HTTP 200**으로 돌려줌. 그대로 쓰면 데이터가 통째로 파괴됨 |
| **헤더 불일치 확인** | 엉뚱한 탭(gid)을 가리키는 실수를 잡음 |
| **동일 내용 미기록** | 불필요한 재임포트와 git 잡음 차단 |
| **빈 셀 = 기존 값 보존** | 아이콘·프리팹 등 CSV로 표현 못 하는 수작업 배선과 공존 |
| **로케일 독립 파싱** | 소수점이 `,`인 환경에서도 같은 값 |

> **판단**: 이 항목들은 전부 "실제로 데이터를 날려 본 사람"만 쓰는 방어입니다.
> 경쟁 도구가 광고하지 않는 이유는 대개 구현돼 있지 않기 때문일 가능성이 큽니다(미검증).
> 포지셔닝을 "가장 기능 많은 CSV 도구"가 아니라 **"데이터를 잃지 않는 CSV 파이프라인"** 으로 잡는 편이 승산이 있습니다.

---

## 4. 경쟁 지형 — 먼저 읽으십시오

| 에셋 | 가격 | 평점 | 비고 |
|---|---|---|---|
| CSV To ScriptableObjects (StarOff1) | $59 | **평점 없음** (즐겨찾기 4) | "Created with AI" 표기 |
| CSV to ScriptableObject (AI Games & Tools) | $5.99 | **평점 없음** (즐겨찾기 20) | 2023년 이후 미갱신 |
| CSV to Scriptable Object Import (Gemons) | — | — | Input Management 분류(오분류) |
| **Scriptable Sheets** (Luna Wolf Studios) | 유료 | 활발 | 카테고리 실질 강자 |

**근거**: 스토어 페이지 조회.

읽어야 할 신호가 둘입니다.

1. **직접 경쟁군(CSV→SO)은 전부 평점이 안 붙었습니다.** 가격은 $5.99~$59로 흩어져 있고 갱신도 뜸합니다.
   시장이 비어 있다기보다 **수요가 약하다**고 읽는 편이 안전합니다.
2. **실제로 팔리는 쪽은 결이 다릅니다.** Scriptable Sheets는 CSV 변환기가 아니라
   **에디터 안의 스프레드시트 격자 뷰**입니다. "폴더를 뒤지며 인스펙터를 오가는 것"을 없애 주는 것이 상품이고,
   CSV 임포트는 그 부속 기능입니다.

> **판단**: 지금 형태 그대로 다듬어 올리는 것은 투자 대비 회수가 낮습니다.
> 진행한다면 §6의 F1(무코드 경로)과 §7의 U1(임포트 미리보기) 없이는 의미가 없다고 봅니다.

---

## 5. 진입 장벽 — 가장 큰 기능 격차 ✅ 해소됨 (v0.2.0)

> **해소 결과**: §5-2의 **A안(속성 기반)** 을 채택해 구현했습니다. `[CsvAsset]` 만 붙이면 임포터 코드가
> 필요 없고, 예상대로 **베이스 4종은 그대로 남겨** 속성으로 표현되지 않는 표를 계속 담습니다.
> F2·F3·F4·F5·F6도 함께 해소했습니다. 자세한 내용은 CHANGELOG의 0.2.0 항목을 보십시오.

### 5-1. 지금은 C#을 써야만 쓸 수 있습니다

현재 최소 사용 절차입니다.

```csharp
public sealed class ClueImporter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] i, string[] d, string[] m, string[] mf)
        => CsvImport.Run<Definition>(i, d, m);

    private sealed class Definition : CsvRowImporter<ClueData>
    {
        protected override string FileName     => "Clues.csv";
        protected override string OutputFolder => "Assets/Data/Clues";
        protected override string GetId(CsvRow row) => row.GetString("ClueId");
        protected override void Bake(CsvRow row, ClueData asset, SerializedObject so)
            => SoBaker.SetStringIf(so, "title", row.GetString("Title"));
    }
}
```

경쟁 도구는 이 자리에서 **속성 한 줄**(PotatoSheets: `[Content("columnName")]`)이거나
**아예 코드가 없습니다**(Scriptable Sheets: "No Coding Required").

**근거**: 각 도구 문서/스토어 페이지 인용.

### 5-2. 세 가지 선택지

| 안 | 사용자가 하는 일 | 노력 | 판단 |
|---|---|---|---|
| **A. 속성 기반** `[CsvColumn("Title")]` 를 SO 필드에 붙이고, 임포터 등록은 에셋 하나 | 필드에 속성만 | 중 | **권장.** 기존 베이스 4종을 그대로 두고 그 위에 얹는 리플렉션 계층 하나 |
| **B. 매핑 에셋 UI** 창에서 CSV 열 ↔ SO 필드를 드래그로 연결 | 코드 0줄 | 상 | A 이후. 사실상 새 제품 |
| **C. 현행 유지** | C# 서브클래스 | 0 | 개발자 전용 틈새로 남음 |

> A를 넣어도 **현행 베이스 4종은 남겨야 합니다.** 다형 생성(`ItemEffect`)·중첩 구조체
> (`CombatEffectConfig`)·그룹핑(`NPCRoutine`)처럼 속성으로 표현 못 하는 표가 실제로 존재합니다.
> SCPPJ의 15개 임포터 중 속성만으로 되는 것은 대략 5개입니다(측정: 형태 분류 결과).

### 5-3. 그 밖의 기능 격차

| # | 없는 것 | 왜 필요한가 | 노력 |
|---|---|---|---|
| ✅ **F2** | **역방향 내보내기 (SO → CSV)** | 에디터에서 손본 값을 표로 되돌릴 길이 없음. 왕복이 안 되면 시트가 금세 낡음. SCPPJ는 이걸 대화 전용으로 따로 만들어 씀 — **이미 필요성이 증명된 기능** | 중 |
| ✅ **F3** | 비공개 구글 시트 (OAuth) | 지금은 "링크가 있는 모든 사용자" 공개가 필수. 사내 데이터엔 못 씀 | 상 |
| ✅ **F4** | TSV / Excel | CSV만 지원 | 하(TSV) / 상(xlsx) |
| ✅ **F5** | 임포트 결과 리포트 | 성공·실패·건너뜀이 Console 로그로만 흩어짐 | 하 |
| ✅ **F6** | 열 존재 검증 | 헤더 오타를 임포트 시점에 못 잡음. 빈 셀 취급돼 조용히 넘어감 | 하 |

---

## 6. UI/UX 격차

**현재 UI는 메뉴 6개 + Project Settings 화면 하나 + Console 로그가 전부입니다.** 전용 창이 없습니다.

| # | 항목 | 현재 | 있어야 할 것 | 노력 |
|---|---|---|---|---|
| **U1** | **미리보기 / 드라이런** | 없음. 저장하면 즉시 반영 | 무엇이 **생성·갱신·삭제**되는지 적용 전에 보여 주는 diff. 이 패키지의 "잃지 않는다" 성격과 정확히 맞음 | 중 |
| **U2** | **대시보드 창** | 없음 | 어떤 CSV가 어떤 임포터에 물려 있고, 마지막 임포트가 언제였고, 무엇이 실패했는지 한 화면 | 중 |
| **U3** | 오류의 위치 | Console 텍스트뿐 | 실패한 **행·열**을 가리키고 클릭하면 해당 에셋으로 이동 | 중 |
| **U4** | 첫 실행 안내 | 설정 에셋이 없으면 기본값으로 조용히 동작 | 설치 직후 무엇을 해야 하는지 알려 주는 진입점. 지금은 Project Settings를 열어 봐야 알 수 있음 | 하 |
| **U5** | 연동 설정 인스펙터 | 원시 필드 나열 | 시트 URL 유효성·연결 상태·마지막 동기화 시각을 그 자리에서 표시. `IsConfigured`는 이미 있는데 **화면에 안 드러남** | 하 |
| **U6** | 임포트 진행·취소 | 시트 받기에만 진행바 있음. 임포트 자체는 없음 | 큰 표에서 진행률과 취소 | 하 |
| **U7** | Undo | `ApplyModifiedPropertiesWithoutUndo` 사용 | 의도된 선택이나, 스토어 사용자는 Ctrl+Z를 기대함. **최소한 문서에 명시** | 하~중 |

---

## 7. 단계별 계획

### 1단계 — 제출 가능 상태 (기계적, 판단 불필요)

| 할 일 | 대응 |
|---|---|
| `package.json`의 `unity`를 `2022.3`으로 | B1 |
| **모든 사용자 문자열·XML 주석·README를 영문화** (17파일 / 33개소) | B2 |
| `Documentation~/` 에 영문 사용 문서 | B2 |
| ~~`Samples~/` 에 예제~~ ✅ 완료 | B3 |
| ~~EditMode 테스트~~ ✅ 완료 | R4 |
| `_running` 등 정적 상태를 Domain Reload 비활성에서 점검 | R1 |
| 연동 설정이 없으면 `EditorApplication.update` 구독 해제 | R2 |
| 네트워크 사용을 설명란에 명시 | R3 |

> **영문화가 이 단계의 대부분입니다.** 17개 파일 전부에 한국어가 있어 기계적 치환으로 끝나지 않습니다.
> 한국어 원문을 유지하고 싶다면 주석만 영문화하고 별도 한국어 문서를 병기하는 방법이 있습니다.

### 2단계 — 팔릴 수 있는 상태

- **F1-A 속성 기반 매핑** — 진입 장벽 제거. 이것 없이는 1단계를 마쳐도 §4의 경쟁군에 합류할 뿐입니다.
- **U1 미리보기/드라이런** — 기존 강점(§3)을 **눈에 보이게** 만듭니다. 지금은 안전장치가 다 있는데 사용자가 그걸 알 방법이 없습니다.
- **F5 + F6 결과 리포트와 열 검증** — 노력 대비 효과가 가장 큽니다.

### 3단계 — 차별화

- **U2 대시보드 창** — 상품의 얼굴. 스토어 스크린샷이 여기서 나옵니다.
- **F2 역방향 내보내기** — 왕복 완성.
- **F3 OAuth** — 사내 사용 해금. 다만 인증 흐름은 지원 부담이 큽니다.

---

## 8. 판단 보류 — 사람이 정해야 할 것

1. **애초에 낼 것인가.** §4의 수요 신호가 약합니다. 1단계만 해도 상당한 작업이고, 그 대부분(영문화)은
   SCPPJ에 아무 이득이 없습니다. **비공개 사내 패키지로 두는 선택**도 합리적입니다.
2. **무료 공개 vs 유료.** GitHub 공개 + MIT로 평판을 얻는 쪽이, 평점 0인 $5.99 에셋보다 나을 수 있습니다.
   현재 라이선스가 이미 MIT입니다.
3. **한국어 유지 여부.** 영문 전환은 되돌리기 번거롭습니다. SCPPJ 문서 규약과도 어긋납니다.
4. **속성 기반(F1-A)의 범위.** SCPPJ 임포터 15개 중 5개만 속성으로 표현됩니다.
   나머지를 못 담는 기능을 전면에 내세우면 리뷰에서 역효과가 납니다.

---

## 출처

- [Unity Asset Store — Submission Guidelines](https://assetstore.unity.com/publishing/submission-guidelines)
- [Unity Asset Store — Start publishing](https://assetstore.unity.com/publishing/publish-and-sell-assets)
- [Asset Store Publishing Tools](https://assetstore.unity.com/packages/tools/utilities/asset-store-publishing-tools-115)
- [CSV To ScriptableObjects (StarOff1)](https://assetstore.unity.com/packages/tools/utilities/csv-to-scriptableobjects-318944)
- [CSV to ScriptableObject (AI Games & Tools)](https://assetstore.unity.com/packages/tools/utilities/csv-to-scriptableobject-236219)
- [Scriptable Sheets — 개발자 스레드](https://discussions.unity.com/t/released-scriptable-sheets/1490189)
- [PotatoSheets (GitHub)](https://github.com/tlauterbach/potato-sheets)
- [Unity-QuickSheet (GitHub)](https://github.com/kimsama/Unity-QuickSheet)
