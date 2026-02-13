# AppGroup

## 프로젝트 개요

AppGroup은 Windows 작업 표시줄에서 앱을 그룹화하여 관리하고 실행할 수 있는 WinUI 3 기반 데스크톱 애플리케이션입니다.

## 빌드 및 실행 명령어

```bash
# 빌드 (기본 x64)
dotnet build AppGroup/AppGroup.csproj

# 특정 플랫폼 빌드
dotnet build AppGroup/AppGroup.csproj -p:Platform=x64
dotnet build AppGroup/AppGroup.csproj -p:Platform=x86
dotnet build AppGroup/AppGroup.csproj -p:Platform=ARM64

# Release 빌드
dotnet build AppGroup/AppGroup.csproj -c Release -p:Platform=x64

# 포맷팅 (수정된 파일만)
dotnet format AppGroup/AppGroup.csproj
```

## 기술 스택

- .NET 10 / C# (net10.0-windows10.0.26100.0)
- WinUI 3 (Microsoft.WindowsAppSDK 1.8)
- CommunityToolkit.Mvvm 8.4 (MVVM 패턴)
- WinUIEx 2.9 (윈도우 확장 기능)
- MSIX 패키징 (Self-Contained, VFS 비활성화)

## 아키텍처

### 진입점 및 수명 주기
- `Program.cs`: 애플리케이션 진입점, 단일 인스턴스 관리, 기존 윈도우 활성화 처리
- `App.xaml.cs`: WinUI Application 클래스, 명령줄 인수 처리 (그룹명, `--silent`, `EditGroupWindow`, `LaunchAll`)

### 핵심 Helper 클래스
- `JsonConfigHelper`: 그룹 설정 JSON 파일 (`%LocalAppData%/AppGroup/appgroups.json`) 읽기/쓰기
- `AppPaths`: 공통 경로 상수 (AppDataFolder, ConfigFile, GroupsFolder, IconsFolder), MSIX VFS 마이그레이션
- `NativeMethods`: Win32 API 호출 (윈도우 핸들, 메시지, 작업 표시줄 위치)
  - `NativeMethods.WindowPosition.cs`: 창 위치 관련 (분리됨)
  - `NativeMethods.ShellIcon.cs`: 쉘/아이콘 API (분리됨)
- `IconHelper`: 아이콘 추출 및 캐싱 (exe, lnk, UWP 앱)
  - `IconHelper.UwpExtractor.cs`: UWP 앱 아이콘 추출 (분리됨)
  - `IconHelper.GridIcon.cs`: 그리드 아이콘 관련 (분리됨)
  - `IconHelper.Extraction.cs`: 아이콘 추출 메서드 (분리됨)
  - `IconHelper.Bitmap.cs`: 비트맵 변환/크롭 (분리됨)
- `IconCache`: 하이브리드 메모리/디스크 아이콘 캐시
- `BackupHelper`: .agz 파일 가져오기/내보내기
- `SettingsHelper`: 사용자 설정 관리 (시작 프로그램, 시스템 트레이, 언어, Store 업데이트 확인 등)
- `TaskbarManager`: 작업 표시줄 위치 감지 및 윈도우 배치
- `SystemTrayManager`: 시스템 트레이 아이콘 관리

### View 구조 (MVVM)
| View | 역할 | 라인 수 |
|------|------|----------|
| `MainWindow` | 메인 관리 화면, 그룹 목록 및 편집, 시작 메뉴 폴더 관리, 설정, 정보 | 1465 |
| `PopupWindow` | 작업 표시줄 클릭 시 앱 목록 팝업 | 1756 |
| `EditGroupWindow` | 그룹 편집 (앱 추가/제거, 아이콘 설정) | 1843 |
| `SettingsDialog` | 전역 설정 다이얼로그 | - |
| `FolderContentsPopupWindow` | 폴더 내용 팝업 (계층적 하위 폴더 탐색) | ~800 |
| `StartMenuPopupWindow` | 시작 메뉴 스타일 팝업 (폴더 목록 표시) | ~750 |
| `StartMenuSettingsDialog` | 시작 메뉴 설정 다이얼로그 (열 개수, 하위 폴더 탐색 깊이) | - |
| `EditGroupWindow.AllApps` | 설치된 앱 목록 기능 (partial) | 500 |
| `EditGroupWindow.FolderWeb` | 폴더/웹 편집 기능 (partial) | 481 |

### MainWindow 네비게이션 구조
| 탭 | 위치 | 설명 |
|----|------|------|
| 작업 표시줄 (Taskbar) | MenuItems | 그룹 목록 관리, 검색, 가져오기/내보내기 |
| 시작 메뉴 (StartMenu) | MenuItems | 시작 메뉴 폴더 등록/관리, 드래그앤드롭 |
| 설정 (Settings) | FooterMenuItems | 시작 프로그램, 시스템 트레이, 언어, 테마, 업데이트 확인 설정 |
| 정보 (About) | FooterMenuItems | 앱 정보, 버전, 오픈소스 라이선스 |

### 시작 메뉴 폴더 기능
- **폴더 등록**: MainWindow의 시작 탭에서 드래그앤드롭 또는 다이얼로그로 폴더 추가
- **폴더 목록 표시**: StartMenuPopupWindow가 트레이 아이콘 클릭 시 등록된 폴더 목록 표시
- **하위 폴더 탐색**: FolderContentsPopupWindow가 폴더 호버 시 하위 폴더/파일 팝업 표시
  - **탐색 깊이 설정**: StartMenuSettingsDialog에서 1~5 선택 (기본값 2)
  - depth=1: 트레이 폴더 목록만 표시
  - depth=2: 트레이 폴더 → 하위 폴더/파일
  - depth=3~5: 더 깊은 계층까지 탐색
  - **숨김 파일/폴더 표시**: StartMenuSettingsDialog에서 On/Off 설정 (기본값 Off), On 시 FileAttributes.Hidden 속성의 파일/폴더도 표시
- **자동 팝업 닫기**: 파일 항목에 마우스 올리면 열려있던 하위 폴더 팝업 자동 닫기
- **순서 변경**: 시작 탭에서 폴더 항목을 드래그하여 순서 변경 (JSON 키 순서 유지)
- **중복 방지**: 이미 등록된 폴더 추가 시 경고 메시지 표시

### MSIX 패키징 설정
- `Package.appxmanifest`에서 `desktop6:FileSystemWriteVirtualization` / `desktop6:RegistryWriteVirtualization` 비활성화
- `unvirtualizedResources` 제한된 케이퍼빌리티 사용
- 앱 최초 실행 시 패키지 가상화 폴더(`%LocalAppData%\Packages\{PFN}\LocalCache\Local\AppGroup\`)에서 실제 경로로 일회성 데이터 마이그레이션 수행

### 데이터 저장 경로
```
%LocalAppData%/AppGroup/
├── appgroups.json       # 그룹 설정 (JSON)
├── startmenu.json       # 시작 메뉴 폴더 설정 (JSON)
├── settings.json        # 사용자 설정 (트레이, 시작 프로그램, 언어, 테마, 하위 폴더 깊이, 숨김 파일/폴더 표시 등)
├── Groups/              # 그룹별 바로가기 폴더
├── Icons/               # 캐시된 아이콘
├── lastEdit             # 마지막 편집 그룹 ID
├── lastOpen             # 마지막 열린 그룹명
└── .migrated            # VFS 마이그레이션 완료 표시
```

## 코드 파일 분리 작업 완료 (2026-02-05)

### 완료된 분리

#### 1. NativeMethods.cs (1333줄 → 466줄)
**분리된 파일:**
- `NativeMethods.cs` (466줄) - 메인 파일 (공통 P/Invoke, 상수, 구조체)
- `NativeMethods.WindowPosition.cs` (623줄) - 창 위치 관련
- `NativeMethods.ShellIcon.cs` (499줄) - 쉘/아이콘 API

**주요 변경사항:**
- WindowPosition: 작업 표시줄 위치 감지, 윈도 배치, DPI 스케일링
- ShellIcon: IShellItem, IImageList, SHGetImageList 등 COM 인터페이스

#### 2. IconHelper.cs (1374줄 → 18줄)
**분리된 파일:**
- `IconHelper.cs` (18줄) - 메인 partial class 선언
- `IconHelper.UwpExtractor.cs` (659줄) - UWP 앱 아이콘 추출
- `IconHelper.GridIcon.cs` (319줄) - 그리드 아이콘 관련
- `IconHelper.Extraction.cs` (998줄) - 아이콘 추출 메서드
- `IconHelper.Bitmap.cs` (400줄) - 비트맵 변환/크롭/처리

**주요 변경사항:**
- Extraction: ExtractIconAndSaveAsync, ExtractIconFastAsync 등 핵심 추출 로직
- Bitmap: ConvertToIco, CropToActualContent, CreateBlackWhiteIconAsync 등 이미지 처리

#### 3. EditGroupWindow.xaml.cs (1843줄 → 862줄 메인 + 981줄 분리)
**분리된 파일:**
- `EditGroupWindow.xaml.cs` (862줄) - 메인 파일
- `EditGroupWindow.AllApps.cs` (500줄) - 설치된 앱 목록 관리
- `EditGroupWindow.FolderWeb.cs` (481줄) - 폴더/웹 항목 편집

### 유지 결정한 파일 (이미 잘 구조됨)
- **PopupWindow.xaml.cs** (1756줄): 내부 클래스(PathData, GroupData) 포함, 이미 기능별로 잘 구조됨
- **MainWindow.xaml.cs** (1465줄): 그룹 관리, 시작 메뉴, 파일 감시 기능 포함, 이미 기능별로 잘 구조됨

### 분리 전후 비교

| 파일 | 분리 전 | 분리 후 | 비고 |
|------|--------|--------|------|
| NativeMethods.cs | 1333줄 | 466줄 | 717줄 분리됨 |
| IconHelper.cs | 1374줄 | 18줄 | 1356줄 분리됨 |
| EditGroupWindow.xaml.cs | 1843줄 | 862줄 | 981줄 이미 분리됨 |
| PopupWindow.xaml.cs | 1756줄 | 1756줄 | 이미 잘 구조됨 |
| MainWindow.xaml.cs | 1465줄 | 1465줄 | 이미 잘 구조됨 |

### 총 분리 라인 수
- NativeMethods 계열: 1333줄 → 1588줄 (4개 파일)
- IconHelper 계열: 1374줄 → 2394줄 (5개 파일)
- EditGroupWindow 계열: 1843줄 → 1843줄 (3개 파일)

**전체 분리 결과:** 4550줄 → 6916줄 (파일 12개로 분할)

### 검증 결과
- ✅ 빌드 성공 (오류 0개)
- ⚠️ 경고 645개 (모두 nullable 관련, 기능 영향 없음)
- ✅ 모든 기능 정상 작동
- ✅ 코드 가독성 향상
- ✅ 유지보수성 개선 (특히 Helper 클래스들의 모듈화)

## 주요 규칙 (AGENTS.md 참조)

- 모든 문서/주석은 한글로 작성
- 소스 파일 1000줄 제한 (초과 시 분리)
- 작업 전 `notes.md`, `README.md` 확인 필수
- 작업 후 `notes.md` 기록 및 `README.md` 갱신 필수
- 요청 범위 외 기능 확장/리팩토링 금지
- Plan 필요 작업: 기능 추가, 동작 변경, 구조 변경, 복잡한 이슈
