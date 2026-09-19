#!/usr/bin/env python3
"""배포본에 나가는 것 중 사람이 보는 자리에 한국어가 남았는지 봅니다.

구현 주석(`//`)과 CHANGELOG 는 한국어로 두는 것이 정한 바이므로 보지 않습니다.
LICENSE 의 한국어 요약도 의도한 것이라 봐주지 않습니다 — 영문이 정본임을 스스로 밝히고 있습니다.

이 검사를 두는 이유는 '반쯤 번역된 상태' 로 되돌아가는 것을 막기 위해서입니다. 문자열 하나가
한국어로 남으면 영어 리포트 안에서 언어가 섞이고, 그것은 번역하지 않은 것보다 나쁩니다.
"""
import re
import sys
import pathlib

KO = re.compile(r'[가-힣]')
ROOT = pathlib.Path(__file__).resolve().parent.parent   # .ci/ 의 한 단계 위가 저장소 루트

# 통째로 영문이어야 하는 문서
DOCS = [
    'README.md',
    'Documentation~/manual-en.md',
    'Documentation~/running-tests.md',
    'Samples~/QuickStart/README.md',
    'Samples~/QuickStart/Quests.csv',
]

problems = []

for name in DOCS:
    path = ROOT / name
    if not path.exists():
        problems.append(f'{name}: 배포본에 있어야 할 문서가 없습니다')
        continue
    for i, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
        if KO.search(line):
            problems.append(f'{name}:{i}: {line.strip()[:80]}')

# 코드 — 문자열 리터럴과 XML 문서 주석만 봅니다
for path in sorted(ROOT.glob('**/*.cs')):
    rel = path.relative_to(ROOT).as_posix()
    if not (rel.startswith('Editor/') or rel.startswith('Runtime/') or rel.startswith('Samples~/')):
        continue
    for i, line in enumerate(path.read_text(encoding='utf-8').splitlines(), 1):
        if not KO.search(line):
            continue
        stripped = line.strip()
        if stripped.startswith('///'):
            problems.append(f'{rel}:{i}: XML 문서 주석 — {stripped[:70]}')
            continue
        if stripped.startswith(('//', '*', '/*')):
            continue                      # 구현 주석은 한국어로 둡니다
        for literal in re.findall(r'"(?:[^"\\\n]|\\.)*"', line):
            if KO.search(literal):
                problems.append(f'{rel}:{i}: 문자열 — {literal[:70]}')
                break

# 채우지 않은 자리 표시자
PLACEHOLDER = re.compile(r'<[A-Z][A-Z0-9_]{3,}>|TODO\(release\)|FIXME')
for name in ['README.md', 'LICENSE.md', 'package.json',
             'Documentation~/manual-en.md', 'Documentation~/running-tests.md']:
    path = ROOT / name
    if not path.exists():
        continue
    for i, line in enumerate(path.read_text(encoding='utf-8').splitlines(), 1):
        hit = PLACEHOLDER.search(line)
        if hit:
            problems.append(f'{name}:{i}: 채우지 않은 자리 — {hit.group(0)}')

if problems:
    print('배포본에 나가면 안 되는 것이 있습니다:\n')
    for p in problems:
        print('  ' + p)
    print(f'\n{len(problems)}건.')
    sys.exit(1)

print('배포본의 사람이 보는 자리는 전부 영문이고, 채우지 않은 자리도 없습니다.')
