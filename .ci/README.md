# CI가 쓰는 껍데기 프로젝트

이 패키지 저장소에는 `Assets/` 도 `ProjectSettings/` 도 없습니다. **패키지이지 프로젝트가 아니기**
때문입니다. 그런데 검사를 돌리려면 Unity에게 열어 줄 프로젝트가 하나 필요합니다.

`project/` 가 그 최소한의 껍데기입니다. 하는 일은 하나뿐입니다 — `Packages/manifest.json` 이
**저장소 루트를 로컬 패키지로 물고**, `testables` 에 올려 검사가 목록에 뜨게 합니다.

```
.ci/project/Packages/manifest.json  →  "com.toflaks.csv-pipeline": "file:../../.."
```

경로가 셋 위인 것은 UPM이 `file:` 을 **그 프로젝트의 `Packages` 폴더 기준**으로 풀기 때문입니다.
`Packages` → `project` → `.ci` → 저장소 루트.

`ProjectSettings/ProjectVersion.txt` 는 **커밋하지 않습니다.** 워크플로가 돌리는 Unity 판마다
그때 써 넣습니다. 그러지 않으면 판을 하나 늘릴 때마다 이 파일이 발목을 잡습니다.

`.` 으로 시작하는 폴더는 Unity가 보지 않으므로, 이 껍데기가 패키지에 딸려 들어가지 않습니다.

## 손으로 한 번 돌리고 나면

Unity 가 `ProjectSettings/` 와 `Library/` 를 만들어 놓습니다. 전부 무시 목록에 있으니
그대로 두십시오. 커밋하는 것은 **껍데기 자체 세 파일뿐**입니다.

한 가지 조심할 것이 있습니다 — 에디터에 붙는 외부 도구가 **열려 있는 프로젝트의 manifest.json 에
자기를 끼워 넣는** 경우가 있습니다. 실제로 Loupedeck 용 `com.logi.unity-bridge` 가 그렇게 했고,
하마터면 CI 껍데기에 남의 패키지가 딸려 들어갈 뻔했습니다. 이 폴더를 커밋하기 전에
`manifest.json` 에 낯선 줄이 붙지 않았는지 보십시오.

## 손으로 돌리려면

`Documentation~/running-tests.md` 를 보십시오. 그쪽은 샌드박스를 따로 만드는 길을 적어 두었습니다.
