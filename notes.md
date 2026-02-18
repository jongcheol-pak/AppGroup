# AppGroup 작업 이력 (요약/인덱스)

## 최근 변경 요약 (최근 10건)

### 2026-02-18
1. **부팅 자동실행 시 메인창 표시 버그 수정** - Program.Main()의 정적 HasSilentFlag가 MSIX StartupTask 활성화를 감지하지 못해, 이미 실행 중인 앱의 메인창을 전면으로 가져오던 문제 수정. StartupTask 감지 로직 추가

### 2026-02-14
1. **MSIX VFS 호환을 위한 Groups 폴더 분리** - MS Store 배포를 위해 `unvirtualizedResources` 제거, Shell 접근 필요 파일(.lnk, .ico)을 비가상화 경로(`%USERPROFILE%\AppGroup\Groups`)로 분리, 설정 UI에서 폴더 경로 변경 가능, 아이콘 파일명 안정화(타임스탬프 제거), 백업/복원 경로 대응, 5개 언어 리소스 추가
2. **코드 리뷰 버그 수정 (Groups 폴더 변경)** - 동일 경로 선택 시 불필요한 복사 방지, 기본 경로 선택 시 설정 리셋 처리, CopyDirectoryRecursive 중복 제거(AppPaths 공유)

### 2026-02-13
1. **업데이트 확인 시 Store 앱 페이지 열기로 변경** - DownloadAndInstallStoreUpdatesAsync 제거, 업데이트 발견 시 ms-windows-store URI로 Store 앱 실행, "다운로드 중..." 문구 제거
2. **업데이트 확인 기능을 정보(About) 화면으로 이동** - Settings 탭에서 About 탭으로 Update Section UI 이동
3. **설정 화면에 업데이트 확인 기능 추가** - StoreContext API로 업데이트 확인, 5개 언어 리소스 추가
2. **숨김 파일 및 폴더 표시 설정 기능 추가** - StartMenuSettingsDialog에 토글 추가, FolderContentsPopupWindow에서 FileAttributes.Hidden 기준 필터링, 기본값 Off
2. **MSIX 파일시스템 가상화 비활성화 및 데이터 마이그레이션** - Package.appxmanifest에 desktop6:FileSystemWriteVirtualization/RegistryWriteVirtualization 비활성화 선언, AppPaths에 패키지 가상화 폴더→실제 경로 일회성 마이그레이션 로직 추가, Program.Main()에서 앱 시작 시 마이그레이션 호출
3. **AllApps 아이콘 추출 품질 개선** - 3단계 개선: (1) ExtractIconFromPidl PIDL 구성 버그 수정 (상대 PIDL → 절대 PIDL), (2) TryGetExePathFromAumid에 IShellItem.GetDisplayName(FILESYSPATH) 및 App Paths 레지스트리 방법 추가, (3) GetAppIconFromShellAsync에서 아이콘 크기 확인 (40x40 미만 시 폴백), UWP 추출기 아이콘 요청 사이즈 256→48로 변경

### 2026-02-12
1. **팝업 윈도우 화면 깜빡임/잘못된 위치 이동 수정** - 거대한 윈도우가 화면 가운데에 잠깐 표시된 후 작업 표시줄 아래로 이동하는 문제 해결. 팝업이 자체적으로 크기/위치 설정 후 표시하도록 변경
2. **팝업 윈도우 위치를 작업 표시줄 아이콘 클릭 위치 기준으로 변경** - 프로세스 시작 즉시 커서 위치를 캡처하여 마우스 이동에 영향받지 않고 클릭한 아이콘 위치 기준으로 팝업 배치
2. **언어 변경 기능 추가** - 설정 화면에서 앱 표시 언어 선택 가능 (시스템 기본값/English/한국어), 설정 영속 저장, 앱 재시작 시 적용
2. **일본어/중국어 리소스 추가** - ja-JP, zh-CN, zh-TW 리소스 파일 생성 및 언어 선택 목록에 추가
3. **DefaultLanguage 설정 추가** - .csproj에 DefaultLanguage=en-US 추가, 리소스 미지원 언어에서 영문 fallback 보장
4. **정보(About) 메뉴 분리** - 설정 화면의 About/Open Source Licenses 섹션을 별도 정보 탭으로 이동
5. **테마 변경 기능 추가** - 설정 화면에서 시스템 기본값/다크/라이트 테마 선택, 즉시 적용 및 영속 저장, 모든 윈도우에 적용
6. **팝업 윈도우 테마 배경 미반영 수정** - StartMenuPopupWindow, FolderContentsPopupWindow, PopupWindow에서 앱 테마를 우선 참조하도록 배경 로직 수정
7. **ContentDialog 테마 미반영 수정** - 코드로 생성한 ContentDialog에 RequestedTheme 명시 적용 (StartMenuSettingsDialog, MainWindow 인라인 다이얼로그, EditGroupWindow 다이얼로그), XAML 정의 ContentDialog도 ShowAsync 전 테마 적용, PopupWindow 테마 적용 조건 수정
8. **EditGroupWindow 재활성화 시 테마 미반영 수정** - EditGroupHelper가 기존 윈도우를 재사용하므로 Activated 이벤트에서 테마 재적용
9. **ContentDialog 테마 깜빡임 수정** - 모든 XAML 정의 ContentDialog에 ContentDialogBackground/TopOverlay를 Transparent로 오버라이드하여 팝업 표시 시 밝은색 깜빡임 제거
10. **테마 기능 코드 리뷰 버그 수정** - PopupWindow Accent Color 로직에서 잘못된 테마 참조(Application.Current.RequestedTheme) 및 동일 값 반환 버그 수정, MainWindow의 EditStartMenuDialog/FolderIconDialog ShowAsync 전 테마 미적용 6곳 수정, GetSavedTheme() 캐시 우선 사용으로 불필요한 파일 I/O 제거

### 2026-02-10
1. **하위 폴더 팝업 계층 탐색 기능 추가** - SubfolderDepth 설정 추가 (기본값 2), 계층적 하위 폴더 탐색, 파일 호버 시 팝업 닫기
2. **시작 탭 중복 폴더 등록 방지** - 드래그앤드롭 및 다이얼로그 추가 시 중복 검사 및 메시지 표시
3. **시작 탭 목록 드래그 순서 변경** - ListView 드래그 순서 변경으로 폴더 순서 영속 저장

### 2026-02-08
4. **파일 탐색기 실행 버그 수정** - `shell:AppsFolder\Microsoft.Windows.Explorer` 실행 시 기존 탐색기 종료 문제 해결
5. **팝업 윈도우 숨김 실패 버그 수정** - hover timer 중지 누락으로 팝업이 반복 표시되던 문제 해결

### 2026-02-07
6. **FolderContentsPopupWindow 아이콘 로직 단순화** - 확장자별 fallback 아이콘 제거, 기본 아이콘으로 통일
7. **아이콘 추출 시 크롭/리사이즈 제거** - 불필요한 CropToActualContent 호출 제거, 원본 크기 유지

### 2026-02-06
8. **코드 리뷰 심각한 문제 21건 수정** - Bitmap/GDI+ 리소스, COM 객체, 이벤트 핸들러, 논리적 버그 수정
9. **StartMenuPopupWindow 그리드 레이아웃 개선** - 동적 높이/너비 계산으로 스크롤 문제 해결
10. **코드 파일 분리 작업 완료** - NativeMethods, IconHelper, EditGroupWindow 1000줄 제한 준수

---

## 미해결 이슈

### 1000줄 초과 파일 분리 작업 (대기)

#### 완료된 파일
- [x] EditGroupWindow.xaml.cs: 2721줄 → 1843줄 + 2개 partial 파일
- [x] IconHelper.cs: 2504줄 → 1374줄 + 4개 partial 파일
- [x] NativeMethods.cs: 1333줄 → 466줄 + 2개 partial 파일

#### 남은 파일 (우선순위 순)

| 파일 | 현재 줄 수 | 분리 제안 |
|------|-----------|----------|
| PopupWindow.xaml.cs | 1756줄 | 템플릿 생성, 아이템 로드, 컨텍스트 메뉴 등 기능별 분리 |
| MainWindow.xaml.cs | 1465줄 | 탭별 핸들러 분리 (작업표시줄, 시작메뉴, 설정) |

---

## 주의 사항

### COM 객체 사용
- IWshShortcut (바로가기 타겟 경로 추출)은 UI 스레드(STA)에서만 사용 가능
- 백그라운드 스레드에서 호출 시 예외 발생
- 반드시 try/finally + Marshal.ReleaseComObject() 패턴 사용

### 아이콘 추출
- IShellItemImageFactory.GetImage는 이미 top-down 순서 - 뒤집기 불필요
- CropToActualContent는 32x32 이하 아이콘에서 문제 발생 가능 - 신중히 사용
- SHGetImageList를 우선 사용하여 고해상도 아이콘 추출

### 리소스 관리
- Bitmap, Icon 등 IDisposable 객체는 항상 using 블록 사용
- 네이티브 리소스(Subclass 등) 사용 시 IDisposable 구현 필수
- 이벤트 핸들러는 등록과 해제가 반드시 쌍을 이루어야 함

### 빌드 환경
- Platform: x64 빌드 필요
- TFM: net10.0-windows10.0.26100.0

---

## 상세 로그 링크

### 2026년
- [2026년 2월 작업 로그](docs/notes/2026-02.md) - 2월 상세 작업 기록
- [2026년 1월 작업 로그](docs/notes/2026-01.md) - 1월 상세 작업 기록

### 2025년
- 상세 로그 미분리 (작업 내역이 적음)
