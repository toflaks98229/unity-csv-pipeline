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

## 3. 컴파일만 빠르게 보기

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

## 검사끼리 얽히지 않게 하기

검사마다 **서로 다른 임시 폴더**를 씁니다. 같은 경로를 한 세션 안에서 지웠다 만들기를 반복하면
AssetDatabase가 지워진 항목을 아직 붙들고 있어, 결과가 실행 순서에 따라 달라집니다.
실제로 이 얽힘 때문에 통과하던 검사가 다른 검사를 추가하자 실패한 적이 있습니다.
새 검사를 더할 때 `CsvTestFolder.Create()` 를 쓰십시오.
