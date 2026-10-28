# 배포 진행 상황

마지막 업데이트: 2026-10-19

## 목표

코드 서명 없이, 설치할 때 Windows SmartScreen 경고("Windows의 PC 보호")가 뜨지 않도록 배포한다.

- **Microsoft Store**: MSIX 패키지를 Store가 서명한다. 클릭 한 번 설치, 자동 업데이트, 스마트 앱 컨트롤과도 호환된다.
- **winget**: 터미널에서 `winget install highpen`으로 경고 없이 설치한다.
- 설치 파일은 사용자별 설치(`%LOCALAPPDATA%\Programs\HighPen`, HKCU)라서 UAC 창은 뜨지 않는다.
- 웹에서 직접 받은 exe는 앞으로도 SmartScreen 경고가 뜬다.

## 진행 상황

### 공통

| 단계 | 상태 | 내용 |
| --- | --- | --- |
| 1.3.0 릴리스 | 완료 | `v1.3.0` 태그 → GitHub 릴리스 [HighPen v1.3.0](https://github.com/stepanowon/high-pen/releases/tag/v1.3.0) (`HighPen-Setup.exe`, `HighPen.exe`) |
| GitHub Actions 삭제 | 완료 | `.github/workflows/release.yml` 삭제 (main `df6f4ac`). 릴리스는 이제 직접 만든다 |
| README 정리 | 완료 | [#2](https://github.com/stepanowon/high-pen/pull/2) 병합: winget 설치 안내 추가, SignPath 설정 제거 |

### winget

| 단계 | 상태 | 내용 |
| --- | --- | --- |
| 매니페스트 작성 | 완료 | `StephenWon.HighPen` 1.3.0, 스키마 1.12.0, `winget validate` 통과 |
| winget-pkgs PR 제출 | 완료 | [microsoft/winget-pkgs#455634](https://github.com/microsoft/winget-pkgs/pull/455634) (fork `stepanowon/winget-pkgs`, 브랜치 `StephenWon.HighPen-1.3.0`) |
| CLA 서명 | 완료 | `license/cla` 통과 |
| 자동 검증 | 완료 | 01~10 모두 통과 (설치 파일 스캔, 설치 테스트 포함). 라벨 `Azure-Pipeline-Passed`, `Validation-Completed` |
| moderator 승인·병합 | **대기** | 커뮤니티 moderator 검토 대기 |

### Microsoft Store

| 단계 | 상태 | 내용 |
| --- | --- | --- |
| 개발자 계정 | 완료 | 게시자 이름 `StephenWon` |
| 앱 이름 예약 | 완료 | `HighPen` (MSIX 또는 PWA 앱) |
| MSIX 패키징 | 완료 | [#3](https://github.com/stepanowon/high-pen/pull/3) 병합. `packaging/build-msix.ps1` → `dist/HighPen.msix` (1.3.0.0) |
| Store 목록 로고 | 완료 | [#4](https://github.com/stepanowon/high-pen/pull/4) 병합. `packaging/StoreListing/HighPen-{71,150,300}.png` |
| 제출 (Submission 1) | 완료 | 가격 무료, 연령 등급 IARC 3+ / 전체 이용가, 한·영·일 Store 목록 |
| 인증 | **진행 중** | 사전 처리 중 → 인증 → 게시. 인증을 통과하면 자동 게시 (최대 영업일 3일) |

## Store 정보

| 항목 | 값 |
| --- | --- |
| Store ID | `9PDG1QN5QSL3` |
| 웹 Store URL | https://apps.microsoft.com/detail/9PDG1QN5QSL3 (게시 후 사용 가능) |
| Package/Identity/Name | `StephenWon.HighPen` |
| Package/Identity/Publisher | `CN=58BB6EA5-E7A7-4A4F-9A0C-62261149B3A1` |
| PublisherDisplayName | `StephenWon` |
| Package Family Name | `StephenWon.HighPen_swdd7q8abfzng` |

## 다음 할 일

1. Store 인증 결과 확인 (메일 또는 Partner Center)
   - 통과: 자동 게시 → Store 배지 README PR 병합, `https://apps.microsoft.com/detail/9PDG1QN5QSL3` 열리는지 확인
   - 반려: 인증 보고서 확인 후 수정해 다시 제출
2. winget PR #455634 moderator 승인 대기. 질문 댓글이 달리면 답변
3. 병합 후 확인: `winget install highpen`, `winget install 9PDG1QN5QSL3` (Store 버전)

## winget 매니페스트 요약

경로: `manifests/s/StephenWon/HighPen/1.3.0/` (파일 4개: version, installer, locale.en-US, locale.ko-KR)

| 항목 | 값 |
| --- | --- |
| PackageIdentifier | `StephenWon.HighPen` |
| Moniker | `highpen` |
| InstallerType / Scope | `exe` / `user` |
| 조용한 설치 | `/S` |
| InstallerUrl | `https://github.com/stepanowon/high-pen/releases/download/v1.3.0/HighPen-Setup.exe` |
| InstallerSha256 | `9C10F437B7F13F87377CB5202C4E166E4D4849BE7B2CDACD52BF8506C450A5A8` |
| ARP 항목 | DisplayName `HighPen`, Publisher `HighPen`, DisplayVersion `1.3.0` |

## 다음 버전 배포 절차

1. `src/HighPen.cs`의 `AppVersion`과 `src/Setup.cs`의 `Version`을 같이 올린다.
2. `.\build.ps1`로 `dist\HighPen.exe`, `dist\HighPen-Setup.exe`를 빌드한다.
3. 커밋·푸시한 뒤 릴리스를 만든다.
   ```
   gh release create v1.x.x dist/HighPen-Setup.exe dist/HighPen.exe --title "HighPen v1.x.x" --generate-notes
   ```
4. winget 매니페스트를 갱신해 제출한다 (`winget install wingetcreate`로 설치).
   ```
   wingetcreate update StephenWon.HighPen -v 1.x.x -u https://github.com/stepanowon/high-pen/releases/download/v1.x.x/HighPen-Setup.exe --submit
   ```
5. Store 패키지를 만들고 Partner Center에서 **새 제출**의 패키지만 바꿔 제출한다 (자세한 내용: `packaging/STORE.md`).
   ```
   .\packaging\build-msix.ps1
   ```
