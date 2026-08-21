# 검사 돌리는 법

이 패키지의 EditMode 검사는 **에디터를 열지 않고** 돌릴 수 있습니다.
값 변환·에셋 생성·정리·왕복은 컴파일로 보증되지 않아, 실제로 돌려 보는 것이 유일한 확인 방법입니다.

## 1. 에디터에서

**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**

패키지가 `Packages/` 아래에 있으면 그대로 보입니다. UPM으로 받은 경우에는
소비하는 프로젝트의 `Packages/manifest.json` 에 `testables` 를 넣어야 목록에 올라옵니다.

```json
"testables": [ "com.toflaks.csv-pipeline" ]
```

## 2. 명령줄에서 (권장)

에디터가 그 프로젝트를 열고 있으면 잠겨서 안 됩니다. **별도 샌드박스 프로젝트**를 만들어 돌리면
쓰던 에디터를 닫지 않아도 됩니다.

```sh
UNITY="C:/Program Files/Unity/Hub/Editor/<버전>/Editor/Unity.exe"
SB=/tmp/csvsandbox

# 1) 빈 프로젝트 생성
"$UNITY" -batchmode -createProject "$SB" -logFile "$SB.create.log" -quit

# 2) manifest.json 에 패키지를 로컬 경로로 물리고 testables 에 등록
#    "com.toflaks.csv-pipeline": "file:/절대/경로/com.toflaks.csv-pipeline"
#    "testables": [ "com.toflaks.csv-pipeline" ]

# 3) 검사 실행
"$UNITY" -batchmode -projectPath "$SB" -runTests -testPlatform EditMode \
         -testResults "$SB/results.xml" -logFile "$SB/test.log"
```

**`-nographics` 를 주지 마십시오.** 창이 실제로 그려지는지 보는 검사가 화면을 열 수 없어
스스로 건너뜁니다. IMGUI 는 `Begin`/`End` 짝이 어긋나도 컴파일이 통과하므로, 그려 보는 것이
그 결함을 잡는 유일한 방법입니다. 화면 없이 돌려야 한다면 그 검사들이 건너뛴다는 것을 알고 쓰십시오.

**종료코드가 곧 결과입니다.** `0` = 전부 통과, `2` = 실패 있음, `3` = 실행 자체 실패.
`-runTests` 에 `-quit` 을 같이 주지 마십시오. 결과를 쓰기 전에 끝나 버립니다.

실패 내역은 `results.xml` 의 `<test-case result="Failed">` 에 메시지와 스택이 들어 있습니다.

### 샌드박스가 썩으면 다시 만드십시오

재사용하다 보면 샌드박스가 이상해집니다. 실제로 겪은 것들입니다.

| 증상 | 실제 원인 | 처방 |
|---|---|---|
| `Couldn't set project path to: <cwd>/<경로>` | `Assets` 폴더가 사라져 유효한 프로젝트로 인식되지 않음 | `mkdir Assets` |
| `The type or namespace name 'NUnit' could not be found` | 외부 도구가 `manifest.json` 에 **상대 경로 `file:` 의존성**을 끼워 넣어 패키지 해석이 통째로 실패 | 그 항목 제거 **+ `packages-lock.json` 삭제 + `Library/PackageCache`·`ScriptAssemblies` 비우기** |

**둘 다 패키지 코드와 무관합니다.**

두 번째는 `manifest.json` 만 고쳐서는 풀리지 않습니다. **잠금 파일이 깨진 해석을 그대로 붙들고
있습니다.** 실제로 Loupedeck용 `com.logi.unity-bridge` 플러그인이 실행 중인 Unity 프로젝트마다
자기를 끼워 넣어 이 증상을 반복해서 만들었습니다. 검사 스크립트가 매번 걷어내게 해 두는 편이 낫습니다.

그러고도 실패하면 `rm -rf` 후 다시 만들어 보십시오. 재생성은 2~3분이면 끝나고,
그러고도 실패하면 그때가 진짜입니다.

경로는 `cygpath -w` 로 Windows 형식으로 넘기십시오. Git Bash에서 그냥 넘기면 앞에 현재
디렉터리가 붙어 버립니다.

## 3. CI에서 돌리기

`.github/workflows/tests.yml` 이 밀 때마다 EditMode 검사를 돌립니다.
**선언한 최소 판(2022.3)과 Unity 6 LTS 양쪽**에서 돌리는 것이 요점입니다 —
`package.json` 의 `unity` 필드는 지켜지지 않으면 그냥 거짓말이 됩니다.

이 저장소에는 `Assets/` 도 `ProjectSettings/` 도 없습니다. **패키지이지 프로젝트가 아니기**
때문입니다. `.ci/project/` 가 Unity에게 열어 줄 최소한의 껍데기이고, 그 `manifest.json` 이
저장소 루트를 로컬 패키지로 뭅니다. 자세한 것은 `.ci/README.md` 에 있습니다.

### 처음 한 번은 사람이 해야 합니다

GameCI 는 Unity 라이선스를 대신 얻어 주지 않습니다. 저장소
**Settings > Secrets and variables > Actions** 에 셋을 넣으십시오.

| 비밀값 | 무엇 |
|---|---|
| `UNITY_LICENSE` | `.ulf` 라이선스 파일의 **내용 전체** |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 암호 |

`.ulf` 는 Unity Hub 에 로그인해 개인 라이선스를 받으면 생깁니다.
윈도우에서는 `C:/ProgramData/Unity/Unity_lic.ulf` 입니다.
(개인 라이선스에는 `UNITY_SERIAL` 이 필요 없습니다. 그쪽은 Pro 용입니다)

비밀값이 없으면 워크플로가 **첫 단계에서 그 사실을 말하고 멈춥니다.** 활성화 오류는 읽어도
무슨 소린지 알기 어려워, 먼저 물어보게 해 두었습니다.

### CI에서는 그리기 검사가 건너뜁니다

컨테이너에는 그래픽 장치가 없어 창을 그릴 수 없습니다. 그리기 검사는 그때
`SystemInfo.graphicsDeviceType` 을 보고 **스스로 건너뜁니다.** 진짜 결함을 "화면이 없어서" 로
덮지 않으려는 것입니다.

**그러니 창을 손댔다면 CI만 믿지 마십시오.** 위 2번을 화면이 있는 기계에서 한 번 돌리십시오.

### 표를 고치고 굽기를 잊은 커밋 잡기

소비하는 프로젝트 쪽 이야기입니다. 표 파일이 바뀐 것은 diff 에 보이지만, **산출물이 안 바뀐 것은
diff 에 보이지 않습니다.** 없는 것은 눈에 띄지 않으니까요.

```sh
Unity -batchmode -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
```

어긋난 표가 있으면 **종료 코드 1** 과 함께 어느 표가 왜 어긋났는지를 로그에 남깁니다.
아무것도 쓰지 않습니다. 에디터에서 확인하려면
`Tools > CSV Pipeline > 표와 산출물이 어긋나는지 확인` 입니다.

판정은 **파이프라인 창의 것과 같습니다.** 화면에서 "바뀌는 것 없음" 인 표가 CI 에서 실패하면
둘 중 하나는 거짓말이고, 사람은 어느 쪽을 믿을지 알 수 없게 됩니다. 검사가 그 둘을 맞춰 둡니다.

---

## 4. 컴파일만 빠르게 보기

Unity를 띄우지 않고 컴파일만 확인하려면, 에디터가 만들어 둔 `.csproj` 의 `<Reference>` 블록을
재활용해 임시 프로젝트를 만들어 `dotnet build` 하면 됩니다.
**패키지를 단독으로 빌드하는 것이 중요합니다** — 소비하는 프로젝트와 합쳐 빌드하면
패키지가 그쪽을 참조해 버려도 드러나지 않습니다.

## 검사가 지키고 있는 것

| 무엇 | 왜 |
|---|---|
| 미리보기는 **아무것도 쓰지 않는다** | 이 기능의 전제입니다. 계획을 세워도 에셋이 생기거나 값이 바뀌면 안 됩니다 |
| 필드 타입이 값 모양을 이긴다 | `30` 이 `float` 필드에 들어갑니다. 값만 보고 타입을 정하는 도구가 가장 많이 틀리는 자리입니다 |
| 참조가 남은 에셋은 지우지 않는다 | 지우면 GUID가 사라져 git으로도 배선이 돌아오지 않습니다 |
| 빈 셀은 기존 값을 보존한다 | 인스펙터에서 저작한 값이 표 때문에 날아가면 안 됩니다 |
| 필수 열이 빠지면 아무것도 굽지 않는다 | 빈 셀로 기본값을 굽는 것보다 멈추는 편이 낫습니다 |
| 내보낸 표가 원본과 왕복한다 | 헤더 표기가 바뀌면 시트와 어긋나 동기화가 멈춥니다 |

## 새 검사는 메모리 위에 쓰십시오

굽기 규칙은 **에셋을 하나도 만들지 않고** 확인할 수 있습니다. `MemoryAssetGateway` 를 끼우면
표도 산출물도 메모리에만 있습니다. 임시 폴더가 없으니 검사끼리 얽힐 자리도 없습니다.

```csharp
using var assets = new MemoryAssetGateway().WithTable("Assets/Memory/Widgets.csv", csvText);
using (CsvAssets.Use(assets))
{
    CsvImportReport report = new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData)))
        .Run("Assets/Memory/Widgets.csv");

    Assert.AreEqual(2, report.Created);
    Assert.AreEqual("첫 위젯", assets.Get<WidgetData>("Assets/Memory/WidgetData/Widget_A.asset").title);
}
```

이 게이트웨이는 **소비하는 프로젝트에서도 씁니다.** 직접 만든 임포터의 규칙을 Unity를 띄우지
않고 확인할 수 있습니다.

몇 가지 거들 것:

| 무엇 | 쓰임 |
|---|---|
| `WithTable(path, text)` | 표 원문을 놓습니다. 폴더도 함께 생깁니다 |
| `WithAsset(path, asset)` · `Add<T>(path)` | 이미 있는 산출물을 놓습니다 |
| `Get<T>(path)` | 구워진 결과를 읽습니다 |
| `Referenced` | 여기 넣은 경로는 "참조가 남은 것"으로 취급돼 정리에서 보존됩니다 |
| `SaveCount` | 저장이 몇 번 일어났는지. 미리보기가 아무것도 쓰지 않는지 확인할 때 씁니다 |

## 실제 AssetDatabase가 필요한 것만 통합 검사로

메모리로 볼 수 없는 것이 셋 있습니다. **디스크 기록**(스크립트 링크가 끊긴 에셋은 값이 하나도
남지 않습니다), **지운 자리에 다시 만들기**(재임포트가 끼어들어 메모리 수정이 버려지던 자리),
**GUID 참조 훑기**. 이것들만 `CsvRoundTripTests` 에 남아 있습니다.

거기에 검사를 더한다면 **서로 다른 임시 폴더**를 쓰십시오. 같은 경로를 한 세션 안에서 지웠다
만들기를 반복하면 AssetDatabase가 지워진 항목을 아직 붙들고 있어, 결과가 실행 순서에 따라
달라집니다. 실제로 이 얽힘 때문에 통과하던 검사가 다른 검사를 추가하자 실패한 적이 있습니다.
`CsvTestFolder.Create()` 가 그 일을 합니다.
