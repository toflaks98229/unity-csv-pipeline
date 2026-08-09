# Changelog

이 패키지의 주목할 만한 변경을 기록합니다.
형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르고, 버전은 [유의적 버전](https://semver.org/lang/ko/)을 씁니다.

## [0.2.0] - 2026-08-09

조사에서 드러난 기능 격차 여섯 개를 해소했습니다.
가장 큰 것은 **쓰려면 C# 서브클래스를 써야 했다**는 점이었습니다.

### Added

- **속성 기반 매핑** — `[CsvAsset]` 을 ScriptableObject에 붙이면 임포터 코드 없이 표가 붙습니다.
  필드 이름과 열 이름을 대소문자 무시로 맞추고, `[CsvColumn]` 으로 이름·필수 여부·빈 셀 처리·
  리스트 구분자·참조 검색 폴더를 조정합니다. `[CsvIgnore]` 로 제외합니다.
  문자열·정수·실수·bool·열거형·Vector2/3/4·Color·오브젝트 참조와 그 배열/리스트를 다룹니다.
- **역방향 내보내기** — `Tools ▸ CSV Pipeline ▸ ScriptableObject를 표로 내보내기`.
  `[CsvAsset]` 으로 선언된 타입만 됩니다. 바뀐 파일을 먼저 보여 주고 확인해야 씁니다.
- **비공개 구글 시트** — 서비스 계정 키(JSON)로 액세스 토큰을 받아 붙입니다.
  브라우저 로그인 흐름이 없어 배치 모드에서도 됩니다. 키 내용은 로그에 싣지 않습니다.
- **TSV 지원** — `.tsv` / `.tab` 을 함께 다룹니다. 구분자는 확장자로 정합니다.
  Unity가 `.tsv` 를 TextAsset으로 임포트하지 않으므로 디스크에서 직접 읽는 폴백을 두었습니다.
- **임포트 결과 리포트** — 표마다 한 줄의 로그로 생성·갱신·건너뜀·삭제·보존 건수를 냅니다.
  문제는 원본 줄 번호와 열 이름을 달고 나오며, 로그를 클릭하면 원본 표나 문제가 난 에셋으로 갑니다.
- **열 존재 검증** — 요구한 열이 없으면 **아무것도 반영하지 않고** 오류로 보고합니다.
  빠진 열을 빈 셀로 취급해 조용히 기본값을 굽던 자리를 막습니다.
  대소문자만 다른 열이 있으면 오타로 보고 함께 알립니다.
- `CsvTable` — 행뿐 아니라 헤더를 함께 들고 있는 표. 열 검증의 근거입니다.
- `CsvRow.LineNumber` · `HasColumn` · `SplitList` · `LooksLikeList`.

### Changed

- `CsvImportDefinition.Process` 가 `IReadOnlyList<CsvRow>` 대신 `CsvTable` 과 `CsvImportReport` 를 받습니다.
  베이스 넷을 상속한 코드는 `Bake`/`GetId` 만 구현하므로 영향이 없습니다.
- `CsvAssetPipeline.Reconcile*` 이 리포트에 삭제·보존 건수를 기록합니다.
- `CsvAssetPipeline.FindCsvPath` 가 TextAsset 검색에 실패하면 CSV 루트를 직접 훑습니다. (`.tsv` 때문)

## [0.1.1] - 2026-08-09

### Added

- `package.json`에 `documentationUrl`·`changelogUrl`·`licensesUrl`.
  없으면 Package Manager 창의 상세 패널에 문서 링크 버튼이 아예 뜨지 않습니다.
  (README는 패키지 안에 들어가지만 그 패널이 렌더링해 주지는 않습니다)
- README 설치 절에 비공개 저장소 인증 조건.
  Package Manager는 대화형 인증을 못 하므로 자격증명이 미리 캐시돼 있거나 SSH 키가 있어야 합니다.

## [0.1.0] - 2026-08-09

게임 프로젝트 안에 있던 CSV → ScriptableObject 파이프라인을 독립 패키지로 분리한 첫 배포입니다.

### Added

- `CsvReader` — RFC 4180 따옴표 필드·BOM·CRLF를 처리하는 로케일 독립 파서
- `CsvRow` — 셀 접근·타입 파싱·리스트 분리 접근자
- `SoBaker` — `SerializedObject` 필드 세터 모음. `Set*If` 는 빈 셀에 기존 값을 보존
- `CsvAssetPipeline` — 폴더 보장·에셋 생성/로드·산출물 정리.
  참조가 남은 에셋과 원본이 사라진 폴더는 지우지 않고 경고만 남김
- `AssetNameIndex<T>` — 이름으로 다른 에셋을 참조하는 셀 해석용 인덱스
- 임포터 베이스 4종 — `CsvRowImporter<T>` / `CsvGroupImporter<T>` /
  `CsvPatchImporter<T>` / `CsvSingletonImporter<T>` 와 진입점 `CsvImport.Run<T>`
- `GoogleSheetSync` — 구글 시트에서 CSV 받기·비교. HTML 응답 거부, 헤더 불일치 확인,
  동일 내용 기록 생략, 로컬 수정 감지
- `CsvPipelineSettings` + Project Settings ▸ CSV Pipeline — CSV 루트 등 폴더 위치 지정.
  설정 에셋이 없으면 기본값으로 동작하며 에셋을 자동 생성하지 않음
- `Tools ▸ CSV Pipeline ▸ Rebuild All Data` — 전 CSV 강제 재임포트
