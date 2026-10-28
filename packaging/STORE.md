# Microsoft Store 제출

Store에는 MSIX 패키지(`dist\HighPen.msix`)를 올립니다. 패키지 서명은 Store가 하므로 인증서가 필요 없습니다.

## 1. 앱 이름 예약과 패키지 ID

1. [Partner Center](https://partner.microsoft.com/dashboard/apps-and-games/overview)에서 **새 제품 → MSIX 또는 PWA 앱**을 고르고 이름 `HighPen`을 예약합니다.
2. 제품 페이지의 **제품 관리 → 제품 ID**에서 세 값을 확인합니다.
   - `Package/Identity/Name`
   - `Package/Identity/Publisher` (`CN=...`)
   - `Package/Properties/PublisherDisplayName`
3. 세 값을 `packaging\AppxManifest.xml`의 `Identity Name`, `Identity Publisher`, `PublisherDisplayName`에 넣습니다. 값이 다르면 업로드할 때 거부됩니다.

## 2. 패키지 빌드

```
.\packaging\build-msix.ps1
```

- Windows SDK의 `makeappx.exe`, `makepri.exe`를 씁니다. SDK가 없으면 NuGet 패키지 [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools)를 받아 압축을 풀고 `-MakeAppx <bin\...\x64\makeappx.exe>`로 지정합니다.
- 버전은 `src\HighPen.cs`의 `AppVersion` 뒤에 `.0`을 붙여 정합니다(예: `1.3.0` → `1.3.0.0`). Store는 마지막 자리가 0이어야 하고, 새로 올릴 때마다 이전보다 커야 합니다.
- 로고(`packaging\Assets`)는 `src\make-icon.ps1`이 아이콘과 함께 만듭니다.

## 3. 제출 항목

| 항목 | 입력 |
| --- | --- |
| 가격 및 사용 가능 여부 | 무료, 모든 시장 |
| 범주 | 생산성 (또는 교육) |
| 개인정보 처리방침 URL | https://github.com/stepanowon/high-pen/blob/main/PRIVACY.md |
| 웹 사이트 | https://github.com/stepanowon/high-pen |
| 지원 연락처 | https://github.com/stepanowon/high-pen/issues |
| 연령 등급 | 설문에서 사용자 간 소통·개인정보 수집·구매 없음으로 답변 |
| 패키지 | `dist\HighPen.msix` |
| 스크린샷 | `screen\` 폴더의 화면 (1366×768 이상 권장, 최소 1장) |

### 제한된 기능(runFullTrust) 사용 사유

> HighPen is a classic Win32 (WPF) desktop app for on-screen annotation during online lectures. It needs full trust to register global hotkeys and to draw a topmost transparent overlay across the whole screen and multiple monitors. It does not access the network or collect any data.

### 설명

**English**

> HighPen is a pen app for online lectures. Draw and highlight on top of any window, spotlight the mouse pointer, add text notes, use a blackboard/whiteboard, and zoom the screen — all with global shortcuts. Supports pen pressure and multiple monitors. Korean, Japanese and English UI.

**한국어**

> HighPen은 비대면 강의용 펜 앱입니다. 어떤 창 위에서든 필기와 형광펜, 마우스 스팟, 말풍선 메모, 칠판, 화면 확대를 단축키로 바로 쓸 수 있습니다. 필압과 다중 모니터를 지원하며 한국어, 일본어, 영어 화면을 제공합니다.

**日本語**

> HighPen はオンライン授業向けのペンアプリです。どのウィンドウの上でも書き込み、蛍光ペン、マウススポット、吹き出しメモ、ボード、画面拡大をショートカットですぐに使えます。筆圧とマルチモニターに対応し、韓国語・日本語・英語の画面を備えています。

## 4. 심사 후

- 인증 심사는 보통 1~3일 걸립니다.
- 게시되면 Store에서 설치할 수 있고, `winget install --source msstore`로도 검색됩니다.
- 새 버전: `AppVersion`을 올리고 → `build-msix.ps1` → Partner Center에서 **새 제출**로 패키지를 바꿔 올립니다.
