# Changelog

이 패키지의 주목할 만한 변경을 기록합니다.
형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르고, 버전은 [유의적 버전](https://semver.org/lang/ko/)을 씁니다.

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
