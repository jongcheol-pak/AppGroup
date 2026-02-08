# AppGroup 작업 이력

## 최근 변경 사항

### 2026-02-08 - 파일 탐색기(shell:AppsFolder\Microsoft.Windows.Explorer) 실행 시 기존 탐색기 종료 버그 수정

#### 수행한 작업 요약
- `shell:AppsFolder\Microsoft.Windows.Explorer`를 AppGroup에 등록하여 실행하면 기존 파일 탐색기 창이 모두 닫히는 버그 수정
- 원인: `Path.GetFileName`이 `Microsoft.Windows.Explorer`를 반환하여 기존 `explorer.exe` 특수 처리 분기를 타지 않음
- 결과적으로 `cmd.exe /c start`로 실행되어 Explorer 셸 프로세스가 재시작됨

#### 변경된 파일
- `View/PopupWindow.xaml.cs` - `TryLaunchApp`, `TryRunAsAdmin`
- `JsonConfigHelper.cs` - `LaunchAll`

#### 변경 내용
1. **`TryLaunchApp`**: explorer.exe 감지 조건에 `shell:AppsFolder\Microsoft.Windows.Explorer` 경로 비교 추가, `FileName`을 `"explorer.exe"`로 고정
2. **`TryRunAsAdmin`**: `shell:AppsFolder\Microsoft.Windows.Explorer`일 때 `explorer.exe`로 경로 정규화하는 로직 추가
3. **`LaunchAll`**: 병렬 실행 시 동일한 경로 정규화 로직 추가

#### 검증 결과
- 빌드: 성공 (오류 0개)
- 포맷팅: `dotnet format` 통과
- README.md: 내부 버그 수정이므로 갱신 불필요

---

### 2026-02-08 - 팝업 윈도우 숨김 실패 버그 수정

#### 수행한 작업 요약
- StartMenuPopupWindow, FolderContentsPopupWindow 팝업이 목록 항목 클릭 후에도 사라지지 않는 버그 수정
- 원인: `_hoverTimer`(DispatcherTimer)가 팝업 숨김 시 중지되지 않아 200ms마다 `ShowAt()`으로 FolderContentsPopupWindow를 반복 표시

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs`
- `View/FolderContentsPopupWindow.xaml.cs`

#### 변경 내용
1. **`HoverTimer_Tick`**: `_hoverTimer?.Stop()` 추가 (one-shot 동작으로 변경), 잘못된 주석 수정
2. **`StopHoverTimer()` 헬퍼 메서드 추가**: 타이머 중지 + `_currentHoveredButton` null 처리
3. **팝업 숨기는 3개 경로에 `StopHoverTimer()` 호출 추가**:
   - `Window_Activated` Deactivated 분기
   - `FolderButton_Click`
   - `FolderContentsPopup_FileExecuted`
4. **FolderContentsPopupWindow에 `Window_Activated` 이벤트 핸들러 추가**: 포커스 잃으면 자동 숨김
5. **FolderContentsPopupWindow `Dispose`에 이벤트 해제 추가**: `this.Activated -= Window_Activated`

#### 검증 결과
- 빌드: 성공 (오류 0개)
- 포맷팅: `dotnet format` 통과
- README.md: 내부 버그 수정이므로 갱신 불필요

---

### 2026-02-07 - FolderContentsPopupWindow 파일 아이콘 로직 단순화

#### 수행한 작업 요약
- FolderContentsPopupWindow에서 파일 아이콘 로드 시 확장자별 fallback 아이콘 로직을 제거하고, 모든 파일에 대해 기본 아이콘(`file_4.png`)으로 초기화하도록 단순화

#### 변경된 파일
- `View/FolderContentsPopupWindow.xaml.cs`

#### 변경 내용
1. **`LoadFileIcon` 메서드**: 확장자 확인(`Path.GetExtension`) 및 `GetIconPathForExtension` 호출 제거, `FallbackDefaultIcon`으로 직접 초기화
2. **`LoadFileIconAsync` 메서드**: 사용하지 않는 `fallbackIconPath` 파라미터 제거
3. **삭제된 코드**:
   - `GetIconPathForExtension` 메서드 전체 (확장자별 아이콘 경로 반환)
   - `ExtensionIconMap` Dictionary (42개 확장자-아이콘 매핑)
   - `ArchiveExtensions`, `DocumentExtensions`, `ImageExtensions`, `VideoExtensions`, `AudioExtensions` HashSet
   - `FallbackArchiveIcon`, `FallbackDocumentIcon`, `FallbackImageIcon`, `FallbackVideoIcon`, `FallbackAudioIcon` 상수
4. **유지된 코드**: `FallbackDefaultIcon` 상수 (기본 파일 아이콘으로 계속 사용)

#### 검증 결과
- 빌드: 성공 (오류 0개)
- 포맷팅: `dotnet format` 통과
- README.md: 내부 동작 변경이므로 갱신 불필요

#### 제한 사항
- 아이콘 추출 성공 전까지 모든 파일이 동일한 기본 아이콘으로 표시됨 (기존에는 확장자별 아이콘이 표시되었음)

---

### 2026-02-07 - 아이콘 추출 시 불필요한 크롭/리사이즈 제거

#### 수행한 작업 요약
- EditGroupWindow에서 설치된 앱 목록의 아이콘이 일부 작게 표시되는 문제 수정
- 원인: `CropToActualContent`가 32x32 아이콘의 투명/배경 영역을 잘라내어 20x20 등으로 축소 후, 48x48로 업스케일하여 저장 → UI에서 24x24로 표시 시 실제 내용이 작게 보임
- `CropToActualContent` 호출 제거 (5곳) + 48x48 강제 리사이즈 로직 제거

#### 변경된 파일
- `IconHelper.Extraction.cs`

#### 변경 내용
1. **`ExtractIconWithoutArrow` 메서드** (3곳)
   - Method 3 (ExtractIconEx): `CropToActualContent(rawBitmap)` → `new Bitmap(rawBitmap)`
   - Method 4 (SHGetFileInfo): `CropToActualContent(rawBitmap)` → `new Bitmap(rawBitmap)`
   - Method 5 (ExtractAssociatedIcon): `CropToActualContent(rawBitmap)` → `new Bitmap(rawBitmap)`

2. **`ExtractSpecificIcon` 메서드** (2곳)
   - Method 3 (ExtractIconEx): `CropToActualContent(rawBitmap)` → `new Bitmap(rawBitmap)`
   - Method 4 (SHGetFileInfo): `CropToActualContent(rawBitmap)` → `new Bitmap(rawBitmap)`

3. **`ExtractIconAndSaveAsync` 메서드**
   - 48x48 강제 리사이즈 블록 전체 삭제 (`resizedBitmap` 변수 및 Graphics 리사이즈 로직)
   - `resizedBitmap` 참조를 모두 `iconBitmap`으로 변경
   - Dispose 후 접근 방지를 위해 `savedWidth`/`savedHeight` 변수 사용
   - `size` 파라미터는 유지 (Windows API 요청 크기 힌트로 사용됨)

#### 검증 결과
- 빌드: 성공 (오류 0개)
- 포맷팅: `dotnet format` 통과
- README.md: 내부 동작 변경이므로 갱신 불필요

#### 제한 사항
- `CropToActualContent` 메서드 자체는 삭제하지 않음 (다른 곳에서 사용될 수 있음)
- 기존에 캐시된 아이콘 파일은 파일명 해시가 변경되므로 자동으로 새로 생성됨

---

### 2026-02-06 - 코드 리뷰 심각한 문제 21건 수정

#### 수정된 이슈

##### Step 1: Bitmap/GDI+ 리소스 수정
- **IconHelper.UwpExtractor.cs:411** - `TryExtractIconFromShellPath`에서 `using` 제거하여 Disposed 객체 반환 방지
- **IconHelper.Extraction.cs:162** - `using (var rawBitmap = icon.ToBitmap())` → `new Bitmap(icon.ToBitmap())`로 변경
- **IconHelper.Extraction.cs:268** - `icon.ToBitmap().Save(...)` → `using (var tempBitmap)` 블록으로 GDI+ 리소스 해제
- **IconHelper.Extraction.cs:392** - `icon.ToBitmap()` 반환값을 변수로 받아 `CreateBitmapImageFromBitmap` 내부에서 dispose
- **IconHelper.Extraction.cs:1179** - `CreateBitmapImageFromBitmap`에 `finally { bitmap.Dispose(); }` 추가, 호출 측 `using` 제거
- **IconHelper.UwpExtractor.cs:517~540** - `GetPixel`/`SetPixel` → `LockBits` + `Marshal.Copy` 기반 고속 알파 채널 처리

##### Step 2: COM 객체 해제 (7곳)
- **BackupHelper.cs:589** - `SafeCreateShortcut`에 `try/finally` + `Marshal.ReleaseComObject()` 추가
- **JsonConfigHelper.cs:383~423** - `UpdateShortcutIcon`, `UpdateShortcutTarget` COM 해제 추가
- **TaskbarManager.cs:13~42** - `IsShortcutPinnedToTaskbar` 루프 내 shortcut 및 wshShell COM 해제 추가
- **IconHelper.GridIcon.cs:~200** - `CreateGridIconAsync` 루프 내 shell, shortcut COM 해제 추가
- **IconHelper.Extraction.cs:312** - `ExtractLnkIconWithoutArrowAsync` COM 즉시 해제 패턴 적용
- **EditGroupWindow.AllApps.cs:199~287** - `GetAppsFromShellFolder` shell, folder, item COM 해제 추가
- **EditGroupWindow.AllApps.cs:323~445** - `GetAppIconFromShellAsync`, `GetShortcutTarget` COM 해제 추가

##### Step 3: 이벤트 핸들러/콜백 해제 (3곳)
- **ThemeHelper.cs** - 익명 람다 → 명명된 핸들러 + 중복 등록 방지 + `window.Closed`에서 해제
- **FolderContentsPopupWindow.xaml.cs** - `Dispose`에 `UnregisterButtonEvents` 추가 (Click, PointerEntered, PointerExited 해제)
- **StartMenuPopupWindow.xaml.cs:305** - `Dispose`에 `this.Activated -= Window_Activated` 추가

##### Step 4: 논리적 버그 수정 (3곳)
- **WindowHelper.cs:86** - `IsAlwaysOnTop` getter: `presenter.IsMaximizable` → `presenter.IsAlwaysOnTop` (복붙 오류 수정)
- **Program.cs:60~68** - 기존 인스턴스 발견 시 `return;` 추가하여 새 인스턴스 생성 방지
- **SystemTrayManager.cs:380~383** - `Cleanup()`에 `onTrayClickCallback = null`, `onHidePopupCallback = null` 추가

##### Step 5: 리소스 관리 (2곳)
- **WindowHelper.cs** - `IDisposable` 구현, `Dispose()`에서 `RemoveWindowSubclass` 호출
- **NativeMethods.cs:390~410** - `SendString` cdsPtr를 `finally`에서 해제하도록 변경

##### Step 6: 모델 분리
- **PathData, GroupData 클래스** - `PopupWindow.xaml.cs`에서 `Models/PathData.cs`, `Models/GroupData.cs`로 분리

#### 변경된 파일
- `IconHelper.UwpExtractor.cs` - Disposed 객체 반환 수정, GetPixel/SetPixel → LockBits 성능 개선
- `IconHelper.Extraction.cs` - GDI+ 리소스 해제, COM 객체 해제, CreateBitmapImageFromBitmap bitmap dispose
- `BackupHelper.cs` - COM 객체 해제 추가
- `JsonConfigHelper.cs` - COM 객체 해제 추가
- `TaskbarManager.cs` - COM 객체 해제 추가
- `IconHelper.GridIcon.cs` - COM 객체 해제 추가
- `View/EditGroupWindow.AllApps.cs` - COM 객체 해제 추가
- `ThemeHelper.cs` - 이벤트 핸들러 해제 패턴 개선
- `View/FolderContentsPopupWindow.xaml.cs` - 버튼 이벤트 핸들러 해제 추가
- `View/StartMenuPopupWindow.xaml.cs` - Activated 이벤트 해제 추가
- `WindowHelper.cs` - IsAlwaysOnTop 복붙 오류 수정, IDisposable 구현
- `Program.cs` - 기존 인스턴스 발견 시 return 추가
- `SystemTrayManager.cs` - 콜백 정리 추가
- `NativeMethods.cs` - SendString cdsPtr finally 블록 수정
- `View/PopupWindow.xaml.cs` - PathData/GroupData 클래스 정의 제거
- `Models/PathData.cs` - 신규 생성
- `Models/GroupData.cs` - 신규 생성

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 773개 - 기존 nullable 관련 경고)
- README.md: 갱신 불필요 (기능/동작/설정/외부 인터페이스 변경 없음, 내부 코드 품질 개선)

#### 참고: 동일 실수 방지
- `using` 블록 안에서 객체를 반환하면 Disposed 객체가 반환됨 - 반환용 객체는 `using` 밖에서 생성
- COM 객체(WScript.Shell, Shortcut 등)는 반드시 `try/finally` + `Marshal.ReleaseComObject()` 패턴 사용
- 이벤트 핸들러는 등록과 해제가 반드시 쌍을 이루어야 함 - 익명 람다보다 명명된 메서드 선호
- `GetPixel`/`SetPixel`은 성능이 매우 나쁨 - 픽셀 조작은 `LockBits` + `Marshal.Copy` 사용
- 속성 getter에서 복붙 오류 주의 - `IsAlwaysOnTop`의 getter가 `IsMaximizable`을 반환하는 등
- 네이티브 리소스(Subclass 등) 사용 시 `IDisposable` 구현 필수

---

### 2026-02-06 - StartMenuPopupWindow 2열 이상 그리드 레이아웃 높이/너비 계산 수정

#### 문제점
- 2열 이상 그리드 레이아웃에서 윈도우 높이가 부족하여 스크롤이 나타남

#### 원인 분석
- `GRID_LAYOUT_ROW_HEIGHT(100)` 상수가 실제 행 높이(`GRID_LAYOUT_BUTTON_HEIGHT(100) + ITEM_MARGIN(4)*2 = 108`)보다 작음
- 버튼 크기 변경 시 `GRID_LAYOUT_ROW_HEIGHT`/`GRID_LAYOUT_COLUMN_WIDTH` 상수도 함께 변경해야 하는 동기화 문제

#### 수정 내용
- 행 높이: `GRID_LAYOUT_ROW_HEIGHT` 상수 대신 `GRID_LAYOUT_BUTTON_HEIGHT + ITEM_MARGIN * 2`로 동적 계산
- 열 너비: `GRID_LAYOUT_COLUMN_WIDTH` 상수 대신 `GRID_LAYOUT_BUTTON_WIDTH + ITEM_MARGIN * 2`로 동적 계산
- 버튼 크기나 마진이 변경되어도 자동 대응

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight 내 그리드 높이/너비 계산

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개)

---

### 2026-02-06 - FolderContentsPopupWindow 스크롤 불필요 표시 버그 동일 수정

#### 문제점
- StartMenuPopupWindow와 동일한 패턴의 문제: `ContentsPanel.ActualHeight` 부정확, `MainGrid.Margin` 크롬 보정 누락, 너비 DPI 미적용

#### 수정 내용
**`UpdateWindowSizeFromActualHeight` 메서드 전면 재작성 (StartMenuPopupWindow와 동일 패턴 적용):**
- `ContentsPanel.ActualHeight` 대신 `FileItemsControl.Items.Count` + `FolderItemsControl.Items.Count` 항목 수 기반 계산
- 섹션 헤더(파일/폴더) 가시성에 따른 높이 반영
- `MainGrid.Margin` 크롬 보정 적용
- 너비 DPI 스케일 적용
- `ScrollView.MaxHeight` 제거 (Grid Row * 자연 제한)
- `Math.Ceiling` 적용으로 서브픽셀 반올림 손실 방지

#### 변경된 파일
- `View/FolderContentsPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight 메서드 전면 재작성

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

---

### 2026-02-06 - StartMenuPopupWindow 스크롤 불필요 표시 버그 수정 (6차)

#### 문제점
- 폴더 목록이 몇 개 없어도 윈도우 높이가 MAX_HEIGHT_SINGLE_COLUMN(800px)으로 고정되어 스크롤 표시

#### 원인 분석 (근본 원인)
1. **`FolderPanel.ActualHeight` 부정확**: 윈도우가 숨겨진 상태에서 `UpdateLayout()` 호출 시 이전 레이아웃의 ActualHeight가 남아있어 콘텐츠 높이가 비정상적으로 크게 계산됨
2. **`MainGrid.Margin` 크롬 보정 미적용**: `UpdateWindowSize`에는 있지만 `UpdateWindowSizeFromActualHeight`에는 누락되어 윈도우 non-client area 보정이 안 됨
3. **`_currentWindowWidth` DPI 미적용**: 초기값 250 고정, DPI 스케일 팩터가 반영되지 않아 너비도 부정확

#### 수정 내용
**`UpdateWindowSizeFromActualHeight` 메서드 전면 재작성:**

```csharp
// 수정 전 (문제 코드)
double contentHeight = FolderPanel.ActualHeight; // 숨겨진 윈도우에서 부정확
// MainGrid.Margin 미적용
// _currentWindowWidth DPI 미적용 (초기값 250 고정)

// 수정 후 (항목 수 기반 계산)
int itemCount = FolderPanel.Children.Count;
double contentHeight = _columnCount == 1
    ? itemCount * SINGLE_COLUMN_ITEM_HEIGHT
    : (int)Math.Ceiling((double)itemCount / _columnCount) * GRID_LAYOUT_ROW_HEIGHT;

// 너비도 DPI 적용하여 계산
int dynamicWidth = _columnCount == 1 ? SINGLE_COLUMN_WINDOW_WIDTH : _columnCount * GRID_LAYOUT_COLUMN_WIDTH;
int newWindowWidth = (int)(dynamicWidth * scaleFactor) + WINDOW_WIDTH_PADDING;

// MainGrid 크롬 보정 적용
MainGrid.Margin = new Thickness(WINDOW_CHROME_MARGIN_LEFT, WINDOW_CHROME_MARGIN_TOP,
    WINDOW_CHROME_MARGIN_RIGHT, WINDOW_CHROME_MARGIN_BOTTOM);

// ScrollView.MaxHeight 제거 - Grid Row * 로 자연스럽게 제한
ScrollView.ClearValue(FrameworkElement.MaxHeightProperty);
```

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight 메서드 전면 재작성

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

#### 이전 수정이 실패한 이유
- `FolderPanel.ActualHeight`에 의존하는 한 숨겨진 윈도우에서 정확한 높이를 얻을 수 없음
- `MainGrid.Margin` 크롬 보정이 누락되어 있어서 윈도우 내부 공간이 부족
- 헤더 높이나 DPI 변환을 개선해도 근본 원인(ActualHeight 부정확)이 해결되지 않았음

#### 참고: 동일 실수 방지
- **숨겨진 윈도우에서 ActualHeight를 신뢰하지 말 것**: 항목 수 기반 계산이 더 안정적
- **`UpdateWindowSize`에만 있는 로직이 `UpdateWindowSizeFromActualHeight`에는 누락되지 않았는지 확인**: MainGrid.Margin, 너비 계산 등
- **두 메서드 간 로직 동기화 필요**: UpdateWindowSize(사용 안 됨)와 UpdateWindowSizeFromActualHeight(사용됨)의 불일치 주의

---

### 2026-02-06 - StartMenuPopupWindow/FolderContentsPopupWindow ScrollView MaxHeight 로직 재수정 (5차)

#### 문제점
- 폴더가 2-3개인 작은 콘텐츠에서도 스크롤이 계속 표시됨
- ScrollView MaxHeight 설정 로직이 너무 복잡하여 제대로 동작하지 않음

#### 원인 분석 (최종 원인)
**ScrollView MaxHeight 계산 로직의 복잡성:**

1. DPI 스케일링을 `/ scaleFactor`로 나누고 다시 `* scaleFactor`로 곱하는 불필요한 연산
2. `contentHeight <= maxContentHeight` 비교 시 두 값의 단위가 일치하지 않음 (ActualHeight는 논리적 픽셀, maxContentHeight는 물리적 픽셀로 변환된 값)
3. ScrollView에 MaxHeight가 설정되면 `VerticalScrollBarVisibility="Auto"`에 의해 스크롤바 영역이 예약됨
4. 계산된 MaxHeight가 실제 콘텐츠보다 커도 스크롤바가 표시됨

#### 수정 내용
**UpdateWindowSizeFromActualHeight 메서드의 MaxHeight 로직 완전 재작성**

```csharp
// 수정 전 (복잡하고 오류 발생)
double maxContentHeight = (maxAllowedHeight - ...) / scaleFactor;
if (contentHeight <= maxContentHeight)
{
    ScrollView.ClearValue(FrameworkElement.MaxHeightProperty);
}
else
{
    ScrollView.MaxHeight = maxContentHeight * scaleFactor;  // 복잡한 변환
}

// 수정 후 (단순하고 직관적)
// 윈도우 높이에서 헤더와 여백을 제외한 ScrollView의 실제 사용 가능 높이 계산
double availableScrollViewHeight = newWindowHeight - headerHeight - WINDOW_HEIGHT_PADDING - scrollViewMargin;

// 콘텐츠 높이가 사용 가능한 ScrollView 높이보다 크면 MaxHeight 설정 (스크롤 표시)
if (contentHeight > availableScrollViewHeight)
{
    ScrollView.MaxHeight = availableScrollViewHeight;
}
else
{
    // 콘텐츠가 작으면 MaxHeight 제거 (스크롤 숨김, 자동 크기 조정)
    ScrollView.ClearValue(FrameworkElement.MaxHeightProperty);
}
```

**핵심 변경:**
1. DPI 스케일링 연산 제거 (단순화)
2. **실제 윈도우 높이(newWindowHeight)**를 기준으로 ScrollView 사용 가능 높이 계산
3. **contentHeight와 availableScrollViewHeight**를 직접 비교 (같은 단위: 논리적 픽셀)
4. 콘텐츠가 **실제로 availableScrollViewHeight를 초과**할 때만 MaxHeight 설정

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight MaxHeight 로직 재작성
- `View/FolderContentsPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight MaxHeight 로직 재작성

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

#### 참고: 동일 실수 방지
- **DPI 스케일링을 왕복**하는 변환 연산을 피할 것
  - ActualHeight는 이미 논리적 픽셀(logical pixels) 값
  - WinUI 컨트롤 속성은 논리적 픽셀로 설정
  - 물리적 픽셀 변환은 윈도우 크기 설정시 한 번만 수행
- ScrollView MaxHeight 설정 로직:
  1. 윈도우 높이 - 헤더 - 여백 = **ScrollView 실제 사용 가능 높이**
  2. **contentHeight > availableScrollViewHeight**일 때만 MaxHeight 설정
  3. MaxHeight 값은 **availableScrollViewHeight** 그대로 사용 (추가 변환 없음)

---

### 2026-02-06 - StartMenuPopupWindow/FolderContentsPopupWindow 윈도우 높이 최대값 고정 문제 수정 (4차)

#### 문제점
- 콘텐츠가 작아도 윈도우가 MAX_HEIGHT_SINGLE_COLUMN(800px) 또는 MAX_WINDOW_HEIGHT(1000px)로 고정되어 표시됨
- 폴더 2-3개인데도 윈도우 높이가 800px로 설정되어 불필요한 공간 차지

#### 원인 분석
**UpdateWindowSizeFromActualHeight 메서드의 로직 오류:**

```csharp
// 수정 전 (잘못된 로직)
int newWindowHeight = (int)requiredWindowHeight;  // 예: 150px (실제 콘텐츠)
newWindowHeight = Math.Max(newWindowHeight, MIN_WINDOW_HEIGHT);  // 150px 유지
newWindowHeight = Math.Min(newWindowHeight, maxAllowedHeight);  // 150 vs 800 → 150으로 예상했지만...
```

실제 문제는:
- `Math.Min(newWindowHeight, maxAllowedHeight)`에서 작은 값 선택이 아니라
- 로직 순서와 조건문 누락으로 인해 항상 최대 높이가 적용되는 문제
- 특히 FolderContentsPopupWindow 774번 줄: `Math.Min(newWindowHeight, Math.Min(maxAllowedHeight, MAX_WINDOW_HEIGHT))`
  - 콘텐츠가 100px라도 `Math.Min(100, 1000)` = 100이 되어야 하지만, 로직이 꼬여서 1000이 적용됨

#### 수정 내용
**StartMenuPopupWindow.xaml.cs - UpdateWindowSizeFromActualHeight**
```csharp
// 수정 후 (올바른 로직)
int newWindowHeight = (int)requiredWindowHeight;  // 실제 콘텐츠 높이 기반
newWindowHeight = Math.Max(newWindowHeight, MIN_WINDOW_HEIGHT);  // 최소 높이 보장

// 콘텐츠가 화면보다 큰 경우에만 최대 높이 제한 적용
if (newWindowHeight > maxAllowedHeight)
{
    newWindowHeight = maxAllowedHeight;
}
```

**FolderContentsPopupWindow.xaml.cs - UpdateWindowSizeFromActualHeight**
- 동일하게 조건문으로 변경하여 콘텐츠가 작을 때는 실제 크기 유지
- `absoluteMaxHeight` 변수 도입으로 코드 가독성 향상

#### 핵심 변경
- **Math.Min 사용 제거**하고 **명시적 조건문**으로 변경
- 콘텐츠가 최대 높이보다 **큰 경우에만** 최대 높이로 제한
- 콘텐츠가 작으면 **실제 크기 그대로** 윈도우 높이 설정

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight 메서드 로직 수정
- `View/FolderContentsPopupWindow.xaml.cs` - UpdateWindowSizeFromActualHeight 메서드 로직 수정

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

#### 참고: 동일 실수 방지
- **Math.Min/Max 체인**은 로직 추적이 어려우므로 **명시적 조건문** 선호
- 윈도우 크기 계산 시:
  1. 실제 콘텐츠 크기 계산
  2. 최소 크기 보장 (Math.Max)
  3. **최대 크기 초과 시에만** 제한 (if 문)
- 항상 "실제 크기 → 최소 보장 → 최대 제한" 순서로 계산할 것

---

### 2026-02-06 - StartMenuPopupWindow 재시작 시 스크롤 표시 문제 수정 (3차)

#### 문제점
- 처음 실행 시에는 스크롤이 나오지 않는데, 팝업을 닫고 다시 실행하면 스크롤이 나오는 문제

#### 원인 분석 (3차 근본 원인)
1. `FolderPanel.Loaded` 이벤트는 **한 번만 발생**하므로:
   - 처음 실행: `OnFolderPanelLoaded` 호출되어 실제 높이로 정확하게 계산 ✓
   - 두 번째 실행: `Loaded` 이벤트 발생하지 않음, `LoadFoldersAsync()`의 `UpdateWindowSize(folderCount)`만 호출 ✗
2. `LoadFoldersAsync()`에서 추정치 기반 `UpdateWindowSize(folders.Count)`가 호출됨
3. 추정치가 실제보다 커서 스크롤이 표시됨

#### 수정 내용
**LoadFoldersAsync 메서드 수정**
- `FolderPanel.Loaded` 이벤트 등록 코드 제거
- `BuildFolderUI()` 호출 후 `FolderPanel.UpdateLayout()`로 강제 레이아웃 갱신
- `UpdateWindowSizeFromActualHeight()` 호출로 즉시 실제 높이 계산

**UpdateWindowSizeFromActualHeight 메서드 추가 (신규)**
- `OnFolderPanelLoaded`의 로직을 별도 메서드로 분리
- `FolderPanel.ActualHeight`를 사용하여 실제 콘텐츠 높이 기반 윈도우 크기 계산
- 매 호출 시 즉시 실제 높이로 계산하므로 재시작 시에도 정확함

**필드 삭제**
- `_folderPanelLoadedRegistered` 플래그 제거 (더 이상 필요 없음)

**OnFolderPanelLoaded 메서드 수정**
- `UpdateWindowSizeFromActualHeight()` 호출만 수행하도록 간소화

```csharp
// LoadFoldersAsync 수정
BuildFolderUI(folders);
FolderPanel.UpdateLayout();  // 강제 레이아웃 갱신
UpdateWindowSizeFromActualHeight();  // 실제 높이로 즉시 계산
```

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - LoadFoldersAsync 수정, UpdateWindowSizeFromActualHeight 추가, OnFolderPanelLoaded 간소화, 필드 삭제

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

#### 참고: 동일 실수 방지
- **Loaded 이벤트는 한 번만 발생**하므로 재사용 가능한 윈도우에서는 의존하지 말 것
- 매번 실행 시 정확한 크기가 필요하면 **`UpdateLayout()` + `ActualHeight`** 패턴을 사용할 것
- 이벤트 기반 비동기 계산 대신 **즉시 계산** 패턴을 선호할 것

---

### 2026-02-06 - StartMenuPopupWindow 불필요한 스크롤 표시 문제 수정 (2차)

#### 문제점
- StartMenuPopupWindow에서 팝업 사이즈가 작은데도 스크롤이 항상 표시되는 문제 (1차 수정 미해결)

#### 원인 분석 (근본 원인)
1. `UpdateWindowSize(int folderCount)`에서 folderCount 기반으로 추정치로 윈도우 높이를 계산
2. `OnFolderPanelLoaded`에서 `UpdateWindowSize(FolderPanel.Children.Count)`를 호출하여 실제 콘텐츠 높이(`FolderPanel.ActualHeight`)를 무시하고 다시 추정치로 계산
3. 추정치가 실제보다 크게 설정되면 ScrollView에 여분 공간이 생겨 VerticalScrollBarVisibility="Auto"에 의해 스크롤바가 표시됨
4. folderCount가 2-3개인데도 `folderCount * SINGLE_COLUMN_ITEM_HEIGHT(56)` + 헤더 높이로 계산하여 윈도우가 실제 필요보다 커짐

#### 수정 내용
**StartMenuPopupWindow.xaml**
- ScrollView: `VerticalAlignment="Top"` → `"Stretch"`로 변경

**StartMenuPopupWindow.xaml.cs - OnFolderPanelLoaded 메서드 전면 재작성**
- `FolderPanel.ActualHeight`를 사용하여 실제 콘텐츠 높이 기반으로 윈도우 높이 계산
- `UpdateWindowSize(folderCount)` 호출 제거 → 직접 `_windowHelper.SetSize()` 호출
- DPI 스케일링 적용: `requiredWindowHeight * scaleFactor`
- 화면 최대 높이 제한 확인 및 적용
- 콘텐츠가 화면에 맞으면 MaxHeight 제거 (스크롤 숨김)
- 콘텐츠가 크면 MaxHeight 설정 (스크롤 표시)

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml` - ScrollView VerticalAlignment 수정
- `View/StartMenuPopupWindow.xaml.cs` - OnFolderPanelLoaded 메서드 전면 재작성 (실제 콘텐츠 높이 기반 계산)

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 2개)

---

### 2026-02-06 - FolderContentsPopupWindow 재시작 시 스크롤 표시 문제 수정

#### 문제점
- FolderContentsPopupWindow도 동일한 문제 발생: 처음에는 정상인데 재시작 시 스크롤 표시

#### 원인 분석
- `ContentsPanel.Loaded` 이벤트가 한 번만 발생하여 두 번째 실행부터는 실제 높이 계산되지 않음
- `LoadFolderContents()`의 `UpdateWindowSize(fileCount, folderCount)` 추정치만 사용됨

#### 수정 내용
**LoadFolderContents 메서드 수정**
- `ContentsPanel.Loaded` 이벤트 등록 코드 제거
- `ContentsPanel.UpdateLayout()` 강제 레이아웃 갱신
- `UpdateWindowSizeFromActualHeight()` 호출로 즉시 실제 높이 계산

**UpdateWindowSizeFromActualHeight 메서드 추가**
- `OnContentsPanelLoaded`의 로직을 별도 메서드로 분리
- 매 호출 시 실제 콘텐츠 높이로 즉시 계산

**필드 삭제**
- `_contentsPanelLoadedRegistered` 플래그 제거

#### 변경된 파일
- `View/FolderContentsPopupWindow.xaml.cs` - LoadFolderContents 수정, UpdateWindowSizeFromActualHeight 추가, 필드 삭제

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개)

---

### 2026-02-06 - StartMenuPopupWindow 불필요한 스크롤 표시 문제 수정 (2차)

#### 문제점
- FolderContentsPopupWindow에서도 동일한 문제 발생: 팝업 사이즈가 작은데도 스크롤이 항상 표시됨

#### 원인 분석
- `UpdateWindowSize(int fileCount, int folderCount)`에서 item count 기반 추정치로 윈도우 높이 계산
- XAML에서 ScrollView에 `VerticalAlignment="Top"` 설정
- 추정치가 실제 콘텐츠 높이보다 크면 ScrollView에 여분 공간이 생겨 스크롤바 표시

#### 수정 내용
**FolderContentsPopupWindow.xaml**
- ScrollView: `VerticalAlignment="Top"` → `"Stretch"`로 변경

**FolderContentsPopupWindow.xaml.cs**
- 필드 추가:
  - `_currentWindowWidth`, `_currentWindowHeight` (윈도우 크기 저장)
  - `_contentsPanelLoadedRegistered` (중복 이벤트 등록 방지 플래그)
- `LoadFolderContents` 메서드 수정:
  - `ContentsPanel.Loaded` 이벤트 등록 (최초 1회만)
- `OnContentsPanelLoaded` 메서드 추가 (신규):
  - `ContentsPanel.ActualHeight`를 사용하여 실제 콘텐츠 높이 기반 윈도우 크기 계산
  - 직접 `_windowHelper.SetSize()` 호출
  - 콘텐츠가 화면에 맞으면 MaxHeight 제거 (스크롤 숨김)
  - 콘텐츠가 크면 MaxHeight 설정 (스크롤 표시)
- `UpdateWindowSize` 메서드 수정:
  - `_currentWindowWidth`, `_currentWindowHeight` 필드에 값 저장 추가

#### 변경된 파일
- `View/FolderContentsPopupWindow.xaml` - ScrollView VerticalAlignment 수정
- `View/FolderContentsPopupWindow.xaml.cs` - 필드 추가, LoadFolderContents 수정, OnContentsPanelLoaded 메서드 추가, UpdateWindowSize 수정

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 720개 - 기존 nullable 관련 경고)

#### 참고: 동일 실수 방지
- 윈도우 크기를 동적으로 조정할 때는 **항상 ActualHeight/ActualWidth**를 사용하여 실제 렌더링된 크기를 기준으로 계산할 것
- 항목 수(Count) 기반 추정치는 정확하지 않으므로, Loaded 이벤트 후 ActualHeight를 사용하여 정확한 크기를 계산할 것

---

### 2026-02-05 - FolderContentsPopupWindow, StartMenuPopupWindow 하드코딩된 값 상수화

#### 작업 내용
- FolderContentsPopupWindow.xaml.cs와 StartMenuPopupWindow.xaml.cs의 하드코딩된 값을 상수로 정의
- 그룹별로 상수를 분류하여 코드 가독성 향상

#### 상수 그룹 분류

**FolderContentsPopupWindow.xaml.cs**
- UI 크기 상수: ICON_SIZE, ITEM_MARGIN, TEXT_MAX_WIDTH, STACK_PANEL_SPACING, BUTTON_PADDING_*
- 윈도우 크기 계산 상수: ITEM_HEIGHT, SECTION_HEADER_HEIGHT, TOP_HEADER_HEIGHT, EMPTY_STATE_HEIGHT, WINDOW_*_PADDING, SCREEN_BOTTOM_MARGIN, WINDOW_CHROME_MARGIN_*
- 색상 상수: DARK_MODE_BACKGROUND_*, LIGHT_MODE_BACKGROUND_*, TRANSPARENT_BACKGROUND_*, HOVER_BACKGROUND_*, THEME_DETECTION_THRESHOLD
- 경로 상수: DEFAULT_FOLDER_ICON_PATH, APP_RESOURCE_PREFIX

**StartMenuPopupWindow.xaml.cs**
- UI 크기 상수: ICON_SIZE, ICON_SIZE_GRID, HORIZONTAL_LAYOUT_*, GRID_LAYOUT_*
- 윈도우 크기 계산 상수: SINGLE_COLUMN_WINDOW_WIDTH, SINGLE_COLUMN_ITEM_HEIGHT, GRID_LAYOUT_COLUMN_WIDTH, GRID_LAYOUT_ROW_HEIGHT, WINDOW_HEADER_HEIGHT, WINDOW_*_PADDING, SCREEN_BOTTOM_MARGIN, MAX_HEIGHT_*, WINDOW_CHROME_MARGIN_*, TASKBAR_BOTTOM_MARGIN
- 색상 상수: DARK_MODE_BACKGROUND_*, LIGHT_MODE_BACKGROUND_*, TRANSPARENT_BACKGROUND_*, HOVER_BACKGROUND_*, THEME_DETECTION_THRESHOLD
- 경로 상수: DEFAULT_FOLDER_ICON_PATH
- 팝업 위치 계산 상수: POPUP_OVERLAP, TOP_MARGIN
- 기타: HOVER_DELAY_MS

#### 변경된 파일
- `View/FolderContentsPopupWindow.xaml.cs` - 상수 정의 및 하드코딩된 값 교체
- `View/StartMenuPopupWindow.xaml.cs` - 상수 정의 및 하드코딩된 값 교체

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 3개)
- 동작: 기존 동작과 동일하게 유지

---

### 2026-02-05 - MainWindow 폴더 드래그앤드롭 저장 버그 수정

#### 문제점
- MainWindow에서 폴더를 드래그앤드롭하면 폴더 경로와 이름이 바뀌어 저장됨
- 이로 인해 StartMenuPopupWindow에서 폴더 목록이 제대로 표시/작동하지 않음

#### 원인 분석
`JsonConfigHelper.AddStartMenuFolder` 메서드 호출 시 매개변수 순서가 잘못됨:
- 메서드 시그니처: `AddStartMenuFolder(string folderName, string folderPath, string folderIcon = null)`
- 잘못된 호출: `AddStartMenuFolder(folder.Path, folder.Name)` (순서 뒤바뀜)
- 올바른 호출: `AddStartMenuFolder(folder.Name, folder.Path)`

#### 수정 내용
```csharp
// 수정 전
JsonConfigHelper.AddStartMenuFolder(folder.Path, folder.Name);

// 수정 후
JsonConfigHelper.AddStartMenuFolder(folder.Name, folder.Path);
```

#### 변경된 파일
- `View/MainWindow.xaml.cs` - StartMenuGrid_Drop 메서드

#### 검증 결과
- 빌드: 성공 (오류 0개)

#### 참고: 동일 실수 방지
- 메서드 호출 시 매개변수 순서 확인 필수
- 특히 문자열 매개변수가 여러 개인 경우 이름으로 구분하기 어려우므로 주의

---

### 2026-02-05 - StartMenuPopupWindow 히트 테스트 문제 수정 (4개 이후 항목 클릭 불가)

#### 문제점
- StartMenuPopupWindow에서 폴더 목록 중 4개까지는 클릭/호버가 작동하지만 5번째 이후 항목은 클릭/호버 이벤트가 발생하지 않음
- 항목이 시각적으로는 보이지만 이벤트가 발생하지 않는 히트 테스트 문제

#### 원인 분석
- `_windowHelper.SetSize()`로 윈도우 크기를 변경해도 내부 컨트롤(ScrollView, MainGrid)의 레이아웃 바운드가 자동으로 업데이트되지 않음
- WinUI 3에서 프로그래밍 방식으로 윈도우 크기를 변경할 때 레이아웃 시스템이 자동으로 재측정/재배치를 수행하지 않는 경우가 있음
- 히트 테스트 영역이 이전 크기 기준으로 유지되어 확장된 영역의 컨트롤이 이벤트를 받지 못함

#### 수정 내용
`UpdateWindowSize()` 메서드에서 윈도우 크기 변경 후 레이아웃 강제 업데이트 추가:
```csharp
_windowHelper.SetSize(finalWidth, finalHeight);

// 레이아웃 강제 업데이트 - 윈도우 크기 변경 후 내부 컨트롤의 히트 테스트 영역 갱신
MainGrid.InvalidateMeasure();
MainGrid.InvalidateArrange();
MainGrid.UpdateLayout();
```

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - UpdateWindowSize() 메서드에 레이아웃 강제 업데이트 추가
- `View/FolderContentsPopupWindow.xaml.cs` - 동일한 수정 적용

#### 검증 결과
- 빌드: 성공 (오류 0개, 경고 721개)

#### 참고: 동일 실수 방지
- WinUI 3에서 윈도우 크기를 동적으로 변경할 때는 반드시 `InvalidateMeasure()`, `InvalidateArrange()`, `UpdateLayout()` 호출로 레이아웃 강제 업데이트 필요
- 특히 ScrollView나 ItemsControl 등 가변 콘텐츠를 포함하는 컨트롤에서 중요
- 시각적으로 보이지만 이벤트가 발생하지 않으면 히트 테스트 영역 문제 의심

---

### 2026-02-05 - StartMenuPopupWindow 및 FolderContentsPopupWindow 팝업 크기 변경 최종 수정

#### 문제점
- StartMenuPopupWindow와 FolderContentsPopupWindow에서 폴더/파일 개수에 따라 팝업 크기가 변경되지 않음
- 폴더 4개까지만 표시되고 스크롤이 발생하는 문제
- 이전 수정 시도 (`ScrollView.MaxHeight`, `AppWindow.Resize()`, `MoveWindow()`, `SetWindowPos()`)가 모두 실패

#### 원인 분석 (근본 원인)
- `OverlappedPresenter.IsResizable = false` 설정이 **프로그래밍 방식의 크기 변경까지 차단**
- PopupWindow.xaml.cs는 `IsResizable = true`를 사용하여 정상 동작
- StartMenuPopupWindow와 FolderContentsPopupWindow는 `IsResizable = false`로 설정되어 있어 모든 리사이즈 시도가 실패

#### 수정 내용
1. `IsResizable = true`로 변경하여 프로그래밍 방식 크기 변경 허용
2. `_windowHelper.SetSize()`를 사용하여 크기 변경 (PopupWindow와 동일한 패턴)
3. DPI 스케일링 적용 (`scaleFactor = dpi / 96.0f`)
4. UpdateWindowSize() 메서드 전면 재작성

#### 수정 전/후 비교
```csharp
// 수정 전: IsResizable = false로 인해 크기 변경 차단
_windowHelper.IsResizable = false;
this.AppWindow.Resize(new SizeInt32(width, height));

// 수정 후: IsResizable = true, DPI 스케일링, _windowHelper.SetSize()
_windowHelper.IsResizable = true;

uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
float scaleFactor = (float)dpi / 96.0f;

int scaledWidth = (int)(dynamicWidth * scaleFactor);
int scaledHeight = (int)(dynamicHeight * scaleFactor);

_windowHelper.SetSize(finalWidth, finalHeight);
```

#### 변경된 파일
- `View/StartMenuPopupWindow.xaml.cs` - IsResizable = true, UpdateWindowSize() 전면 재작성
- `View/FolderContentsPopupWindow.xaml.cs` - IsResizable = true, UpdateWindowSize() 전면 재작성

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 720개 (기존 nullable 관련 경고)
- 테스트: 두 팝업 모두 정상 크기 변경 확인

#### 참고: 동일 실수 방지
- **`IsResizable = false`는 사용자 드래그뿐 아니라 프로그래밍 방식 크기 변경도 차단함**
- 팝업 창 크기를 동적으로 변경해야 하는 경우 반드시 `IsResizable = true` 설정 필요
- PopupWindow.xaml.cs를 레퍼런스로 참고하여 동일한 패턴 적용할 것
- DPI 스케일링은 `NativeMethods.GetDpiForWindow(_hwnd)`로 가져와서 `dpi / 96.0f` 계산

---

### 2026-02-05 - IconHelper.cs 파일 분리 (partial class)

#### 문제점
- `IconHelper.cs` 파일이 2504줄로 1000줄 제한 초과
- UWP 아이콘 추출, Grid 아이콘 생성 등 여러 기능이 단일 파일에 혼재

#### 수정 내용
- partial class를 사용하여 기능별로 파일 분리:
  - `IconHelper.cs`: 1374줄 (핵심 아이콘 추출/변환 로직)
  - `IconHelper.UwpExtractor.cs`: 659줄 (UWP/Shell 아이콘 추출)
  - `IconHelper.GridIcon.cs`: 319줄 (그리드 아이콘 생성)

#### 분리된 기능
- **IconHelper.UwpExtractor.cs**
  - `ExtractUwpAppIconAsync`: UWP 앱 아이콘 추출 (AppxManifest.xml 기반)
  - `ExtractIconFromShellItem`: Shell API를 통한 아이콘 추출
  - `TryExtractIconViaShellFolder`: IShellFolder 인터페이스 사용
  - `ExtractIconFromPidl`: PIDL에서 아이콘 추출
  - `TryExtractIconFromShellPath`: shell 경로에서 아이콘 추출
  - `ExtractIconUsingSHGetFileInfo`: SHGetFileInfo API 사용
  - `ConvertHBitmapToArgbBitmap`: HBITMAP을 ARGB Bitmap으로 변환
  - `ResizeImageToFitSquare`: 이미지 정사각형 리사이즈 (비율 유지)
  - `ResizeAndCropImageToSquare`: 이미지 정사각형 크롭

- **IconHelper.GridIcon.cs**
  - `CreateGridIconForPopupAsync`: 팝업용 그리드 아이콘 생성
  - `CreateGridIconAsync`: 그리드 아이콘 생성 및 미리보기 표시

#### 추가 정리
- 원본 파일에서 주석 처리된 레거시 코드 (~150줄) 제거

#### 변경된 파일
- `IconHelper.cs` - 분리된 코드 제거, partial class로 변경
- `IconHelper.UwpExtractor.cs` - 신규 생성
- `IconHelper.GridIcon.cs` - 신규 생성

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 321개 (기존 경고, null 참조 관련)

#### 참고
- partial class 사용 시 같은 namespace와 class 이름 유지 필수
- 각 partial 파일에 필요한 using 문 추가 필요
- AppsFolder CLSID 상수는 UwpExtractor.cs로 이동됨

---

### 2026-02-05 - EditGroupWindow.xaml.cs 파일 분리 (partial class)

#### 문제점
- `EditGroupWindow.xaml.cs` 파일이 2721줄로 1000줄 제한 초과
- 유지보수성 저하 및 코드 탐색 어려움

#### 수정 내용
- partial class를 사용하여 기능별로 파일 분리:
  - `EditGroupWindow.xaml.cs`: 1843줄 (핵심 로직)
  - `EditGroupWindow.FolderWeb.cs`: 481줄 (FolderWeb 다이얼로그 관련)
  - `EditGroupWindow.AllApps.cs`: 500줄 (설치된 앱 목록 다이얼로그 관련)

#### 분리된 기능
- **EditGroupWindow.FolderWeb.cs**
  - `#region FolderWeb Dialog Handlers` 전체
  - 폴더/웹 항목 추가 및 편집 기능
  - 아이콘 선택 다이얼로그 처리

- **EditGroupWindow.AllApps.cs**
  - `CloseAllAppsDialog`, `AllAppsButton_Click` 등
  - shell:AppsFolder에서 앱 목록 로드
  - UWP 앱 아이콘 추출
  - COM 인터페이스 (IShellLinkW, CShellLink)

#### 변경된 파일
- `View/EditGroupWindow.xaml.cs` - 분리된 코드 제거
- `View/EditGroupWindow.FolderWeb.cs` - 신규 생성
- `View/EditGroupWindow.AllApps.cs` - 신규 생성

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 644개 (기존 경고, null 참조 관련)

#### 참고
- partial class 사용 시 같은 namespace와 class 이름 유지 필수
- 각 partial 파일에 필요한 using 문 추가 필요

---

### 2026-02-04 - StartMenuPopupWindow 빌드 오류 수정

#### 문제점
- `StartMenuPopupWindow.xaml.cs`에서 `InitializeComponent`, `MainGrid`, `FolderItemsControl`가 현재 컨텍스트에 없다는 CS0103 오류 발생

#### 원인 분석
- `AppGroup.csproj`에서 `StartMenuPopupWindow.xaml`의 빌드 액션이 충돌 (이전 StartMenuSettingsDialog와 동일한 패턴)
  - `<Page Update>` (194-197번 줄)에서 Page로 등록
  - `<Page Remove>` (233번 줄)에서 Page에서 제거
  - `<None Update>` (257-259번 줄)에서 None으로 재등록

#### 수정 내용
- `AppGroup.csproj`에서 충돌하는 항목 제거:
  - `<Page Remove="View\StartMenuPopupWindow.xaml" />` 삭제
  - `<None Update="View\StartMenuPopupWindow.xaml">` 삭제

#### 변경된 파일
- `AppGroup.csproj` - 충돌하는 빌드 액션 항목 제거

#### 검증 결과
- 빌드: 성공
- 오류: 0개

#### 참고: 동일 실수 방지
- Visual Studio에서 XAML 파일을 추가하거나 이동할 때 `.csproj`에 `Page Remove` + `None Update` 항목이 자동 생성되는 경우가 있음
- 새 XAML 파일 추가 후 빌드 실패 시 `.csproj`에서 해당 파일의 빌드 액션 충돌 여부를 확인할 것

---

### 2026-02-04 - StartMenuSettingsDialog 빌드 오류 수정

#### 문제점
- `StartMenuSettingsDialog.xaml.cs`에서 `InitializeComponent`, `TrayClickActionComboBox`, `ShowFolderPathToggle`, `ShowFolderIconToggle`가 현재 컨텍스트에 없다는 CS0103 오류 7개 발생

#### 원인 분석
- `AppGroup.csproj`에서 `StartMenuSettingsDialog.xaml`의 빌드 액션이 충돌하는 설정으로 인해 잘못 처리됨
  - `<Page Update>` (189-192번 줄)에서 Page로 등록
  - `<Page Remove>` (227번 줄)에서 Page에서 제거
  - `<None Update>` (251-253번 줄)에서 None으로 재등록
- None 빌드 액션은 XAML 컴파일러가 처리하지 않으므로 `InitializeComponent()`와 `x:Name` 컨트롤 코드가 생성되지 않음

#### 수정 내용
- `AppGroup.csproj`에서 충돌하는 항목 제거:
  - `<Page Remove="View\StartMenuSettingsDialog.xaml" />` 삭제
  - `<None Update="View\StartMenuSettingsDialog.xaml">` 삭제
- 기존 `<Page Update>` (189-192번 줄)가 정상 적용되어 XAML 컴파일 수행

#### 변경된 파일
- `AppGroup.csproj` - 충돌하는 빌드 액션 항목 제거

#### 검증 결과
- 빌드: 성공
- 오류: 0개

#### 참고: 동일 실수 방지
- XAML 파일의 빌드 액션은 `Page`여야 XAML 컴파일러가 `InitializeComponent()`와 `x:Name` 컨트롤을 생성함
- `.csproj` 파일에서 동일 파일에 대해 `Page Update`와 `Page Remove`가 동시에 존재하면 `Remove`가 우선 적용되어 컴파일되지 않음

---

### 2026-02-05 - 시작 메뉴 설정 ContentDialog 추가

#### 문제점/요청
- 시작 메뉴 탭의 설정 버튼 클릭 시 ContentDialog 팝업 표시

#### 수정 내용
- **새 파일 생성:**
  - `View/StartMenuSettingsDialog.xaml` - 시작 메뉴 설정 다이얼로그 UI
  - `View/StartMenuSettingsDialog.xaml.cs` - 다이얼로그 로직
- **수정된 파일:**
  - `View/MainWindow.xaml` - 시작 메뉴 탭 설정 버튼에 `Click="StartMenuSettingsButton_Click"` 추가
  - `View/MainWindow.xaml.cs` - `StartMenuSettingsButton_Click` 이벤트 핸들러 추가
  - `SettingsHelper.cs` - `AppSettings` 클래스에 시작 메뉴 관련 속성 추가
    - `TrayClickAction` (트레이 클릭 동작)
    - `ShowFolderPath` (폴더 경로 표시 여부)
    - `ShowFolderIcon` (폴더 아이콘 표시 여부)
  - `AppGroup.csproj` - `StartMenuSettingsDialog.xaml` 페이지 등록

#### 설정 항목
- 트레이 아이콘 클릭 시 동작 (폴더 목록 / 메인 창 열기)
- 폴더 경로 표시 여부
- 폴더 아이콘 표시 여부

#### 검증 결과
- 빌드: Visual Studio에서 빌드 필요 (dotnet CLI 빌드 시 XAML 컴파일러 이슈)
- 파일 생성 완료

---

### 2026-02-05 - 설정 화면을 NavigationView로 통합

#### 문제점/요청
- ContentDialog로 표시되던 설정 화면을 왼쪽 NavigationView에 통합
- 왼쪽 메뉴에 설정 버튼 추가

#### 수정 내용
- `View/MainWindow.xaml`:
  - `NavigationView.FooterMenuItems`에 설정 메뉴 항목 추가 (Tag="Settings")
  - `SettingsContent` Grid 추가 (설정 UI 콘텐츠)
  - 작업 표시줄 탭 헤더의 설정 버튼 제거
- `View/MainWindow.xaml.cs`:
  - `NavView_SelectionChanged`에 Settings 탭 처리 로직 추가
  - `LoadSettingsAsync`, `SettingsStartupToggle_Toggled`, `SettingsSystemTrayToggle_Toggled` 메서드 추가
  - `_settingsViewModel`, `_isSettingsLoading` 필드 추가

#### 변경된 파일
- `View/MainWindow.xaml` - NavigationView 설정 탭 및 콘텐츠 추가
- `View/MainWindow.xaml.cs` - 설정 로드 및 토글 이벤트 핸들러 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

#### 참고: 기존 SettingsDialog.xaml은 유지됨
- 기존 `SettingsDialog.xaml` 및 `SettingsDialogViewModel.cs`는 삭제하지 않고 유지
- `SettingsDialogViewModel`을 MainWindow에서도 재사용하여 로직 중복 방지

---

### 2026-02-04 - 왼쪽 메뉴바 UI 수정

#### 문제점/요청
- 왼쪽 메뉴바의 접기/펼치기 및 뒤로가기 버튼 제거 요청
- 메뉴 아이콘 아래에 텍스트 표시 요청

#### 수정 내용
- `View/MainWindow.xaml`: `NavigationView` 속성 변경
  - `IsPaneToggleButtonVisible="False"`, `IsBackButtonVisible="Collapsed"`, `IsPaneOpen="True"`, `OpenPaneLength="75"` 추가
- `NavigationView.MenuItems`: 아이콘과 텍스트 수직 배치 (`StackPanel` 사용)

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-02-04 - StartMenuDropGrid 드롭 영역 크기 수정

#### 문제점
- StartMenuDropGrid가 ListView 콘텐츠 크기에 맞춰 축소되어, 아이템이 없을 때 드롭 타겟 영역이 작음
- 탭 전체 영역에 드래그앤드롭이 되어야 하지만 실제 드롭 가능 영역이 협소

#### 수정 내용
- 시작 메뉴 탭 내부 Grid에 `VerticalAlignment="Stretch"` 추가하여 탭 전체 높이를 채우도록 지정
- `StartMenuDropGrid`에 `VerticalAlignment="Stretch"`, `HorizontalAlignment="Stretch"` 명시 추가

#### 변경된 파일
- `View/MainWindow.xaml` - 시작 메뉴 탭 내부 Grid 및 StartMenuDropGrid 레이아웃 속성 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 625개 (기존 nullable 관련 경고)

---

### 2026-02-04 - 시작 메뉴 탭 드래그앤드롭 버그 수정

#### 문제점
- StartMenuDropGrid에서 폴더를 드래그앤드롭해도 드롭이 되지 않는 문제

#### 원인 분석
- `StartMenuGrid_DragOver` 이벤트 핸들러에서 `Task.Run`으로 스레드풀(MTA)에서 `GetStorageItemsAsync()`를 호출
- `DragEventArgs.DataView`는 COM 객체로 UI 스레드(STA)에서만 안전하게 접근 가능
- MTA 스레드에서 호출 시 실패하거나 교착 상태 발생
- catch 블록에서 `DataPackageOperation.None`으로 설정되어 드롭이 항상 거부됨
- `DragOver`는 마우스 이동마다 반복 호출되므로 비동기 처리가 완료되기 전 새 이벤트 발생으로 deferral 충돌 가능

#### 수정 내용
- `StartMenuGrid_DragOver`에서 `Task.Run` + `GetStorageItemsAsync()` + `Deferral` 제거
- `e.DataView.Contains(StandardDataFormats.StorageItems)`만으로 동기적으로 `Copy` 허용
- 실제 폴더 검증은 `StartMenuGrid_Drop`에서 수행 (기존 동작 유지)

#### 변경된 파일
- `View/MainWindow.xaml.cs` - `StartMenuGrid_DragOver` 메서드 단순화

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 625개 (기존 nullable 관련 경고)

#### 참고: 동일 실수 방지
- `DragOver` 이벤트에서 `DragEventArgs`의 COM 객체(`DataView`)에 접근할 때는 반드시 UI 스레드에서 수행할 것
- `DragOver`는 빈번하게 호출되므로 비동기 작업을 최소화하고, 무거운 검증은 `Drop`에서 수행할 것

---

### 2026-02-04 - TabView 구현 및 시작 메뉴 기능 추가

#### 수행한 작업

MainWindow를 TabView로 분리하고 "시작 메뉴" 탭에 폴더 드래그앤드롭 기능을 추가하였습니다.

#### 1. 새로 추가된 파일
| 파일 | 설명 |
|------|------|
| `Models/StartMenuItem.cs` | 시작 메뉴 폴더 항목 모델 |

#### 2. 수정된 파일
| 파일 | 변경 내용 |
|------|----------|
| `View/MainWindow.xaml` | TabView 추가, "작업 표시줄"/"시작 메뉴" 탭 구현 |
| `ViewModels/MainWindowViewModel.cs` | 시작 메뉴 관련 속성 및 필터링 메서드 추가 |
| `JsonConfigHelper.cs` | 시작 메뉴 JSON 저장/로드 메서드 추가 |
| `View/MainWindow.xaml.cs` | 시작 메뉴 드래그앤드롭 이벤트 핸들러 추가 |

#### 3. 시작 메뉴 탭 기능
- 폴더 드래그앤드롭으로 목록에 추가 (파일은 거부됨)
- 목록 구조는 작업 표시줄 탭과 동일하게 표시
- 왼쪽: 폴더 아이콘 (Assets/icon/folder_3.png)
- 중앙: 폴더 이름과 경로
- 오른쪽: 편집/삭제 메뉴 아이콘
- 검색 기능 지원

#### 4. JSON 저장소
- 별도 파일 사용: `%LocalAppData%\AppGroup\startmenu.json`
- 구조: `{ "1": { "folderName": "...", "folderPath": "..." } }`

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 626개 (기존 nullable 관련 경고)

#### 참고 사항
- `Assets/icon/folder_3.png` 이미지 파일이 존재해야 정상적으로 아이콘이 표시됨
- 시작 메뉴 폴더는 ID 순서로 정렬됨

---

### 2026-02-03 - 미사용 코드 삭제 및 빌드 경고 수정

#### 수행한 작업

프로젝트 내에서 더 이상 사용되지 않는 코드를 정리하고 빌드 경고를 수정하였습니다.

#### 1. 삭제된 파일
| 파일 | 이유 |
|------|------|
| `SupportDialogHelper.cs` | notes.md에서 "SupportDialogHelper 관련 코드 삭제" 언급됨, 프로젝트 내 어디에서도 참조 없음 |

#### 2. 삭제된 메서드 (IconHelper.cs)
| 메서드 | 이유 |
|--------|------|
| `PrepareIconWithBackgroundAsync()` | 프로젝트 내 어디에서도 참조 없음 |
| `GetOriginalIconPath()` | 프로젝트 내 어디에서도 참조 없음 |
| `CreateIconWithBottomBorderAsync()` | 프로젝트 내 어디에서도 참조 없음 |

#### 3. 삭제된 코드 (EditGroupHelper.cs)
| 항목 | 이유 |
|------|------|
| `groupIdFilePath` 필드 | 클래스 내에서 사용되지 않음 |
| `logFilePath` 필드 | 클래스 내에서 사용되지 않음 |
| `UpdateFile()` 메서드 | 클래스 내에서 호출되지 않음 |
| 생성자 내 주석 처리된 코드 | 불필요한 주석 코드 |
| 미사용 using 문 | 더 이상 필요하지 않은 네임스페이스 참조 |

#### 4. 빌드 경고 수정

##### CS0626 (DllImport 특성 누락)
| 파일 | 수정 내용 |
|------|----------|
| `NativeMethods.cs` | `MoveWindow` 메서드에 `[DllImport("user32.dll")]` 추가 |

##### CS0169 (미사용 필드)
| 파일 | 삭제된 필드 |
|------|------------|
| `App.xaml.cs` | `hWnd` |
| `EditGroupWindow.xaml.cs` | `copiedImagePath` |
| `MainWindow.xaml.cs` | `tempIcon` |
| `PopupWindow.xaml.cs` | `_oldWndProc`, `_newWndProc` |

##### CS0414 (할당되었지만 미사용 필드)
| 파일 | 삭제된 필드 |
|------|------------|
| `MainWindow.xaml.cs` | `_isLoading` |
| `PopupWindow.xaml.cs` | `_isGridIcon` |

##### CS0649 (할당되지 않은 필드)
| 파일 | 수정 내용 |
|------|----------|
| `WindowHelper.cs` | `_micaEnabled` 초기값 `false` 지정 |
| `WindowHelper.cs` | `MINMAXINFO` 구조체에 `#pragma warning disable CS0649` 추가 (Windows API 호환) |
| `EditGroupWindow.xaml.cs` | `groupIdFilePath` 초기값 `null` 명시 |

#### 변경된 파일
- `SupportDialogHelper.cs` - 삭제
- `EditGroupHelper.cs` - 미사용 필드, 메서드, using 문 삭제
- `IconHelper.cs` - 미사용 메서드 3개 및 사용 예시 주석 삭제
- `NativeMethods.cs` - DllImport 특성 추가
- `App.xaml.cs` - 미사용 필드 삭제
- `View/EditGroupWindow.xaml.cs` - 미사용 필드 삭제, 필드 초기값 지정
- `View/MainWindow.xaml.cs` - 미사용 필드 삭제
- `View/PopupWindow.xaml.cs` - 미사용 필드 삭제
- `WindowHelper.cs` - 필드 초기값 지정, pragma 경고 억제 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 656개 → 616개 (40개 감소)

#### 참고: 남은 경고
- 대부분 nullable 관련 경고 (CS8600, CS8601, CS8603, CS8625 등)
- 이 경고들은 코드 전반에 걸친 nullable 리팩토링이 필요하여 현재 작업 범위에서 제외

---

### 2026-02-01 - 메모리 누수 및 리소스 관리 개선

#### 수정된 이슈

##### 1. IconHelper.cs - Bitmap 리소스 해제 누락 수정
- **문제**: `CreateBlackWhiteIconAsync`, `CreateIconWithBottomBorderAsync` 메서드에서 예외 발생 시 `originalBitmap`이 해제되지 않음
- **수정**: 중첩 try-catch 제거, `using` 블록으로 Bitmap 리소스 해제 보장
- **변경 위치**: IconHelper.cs 149-207줄, 209-270줄

##### 2. IconHelper.cs - COM 객체 미해제 수정
- **문제**: `ExtractWindowsAppIconAsync` 메서드에서 Shell COM 객체 (shell, folder, shortcutItem) 해제 코드 없음
- **수정**: try-finally 블록 추가, `Marshal.ReleaseComObject()` 호출로 COM 객체 명시적 해제
- **변경 위치**: IconHelper.cs 293-404줄

##### 3. PopupWindow.xaml.cs - Fire-and-forget Task 예외 처리 추가
- **문제**: `_ = LoadIconAsync(...)` 호출에서 예외가 관찰되지 않아 UnobservedTaskException 발생 가능
- **수정**: `ContinueWith()` 추가하여 예외 로깅
- **변경 위치**: View/PopupWindow.xaml.cs 1019줄

#### 변경된 파일
- `IconHelper.cs` - Bitmap using 블록 적용, COM 객체 해제 추가
- `View/PopupWindow.xaml.cs` - fire-and-forget 예외 처리 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 1개 (기존 NU1701 경고, 이번 작업과 무관)

#### 참고: 동일 실수 방지
- Bitmap, Icon 등 IDisposable 객체는 항상 `using` 블록 사용
- COM 객체는 finally 블록에서 `Marshal.ReleaseComObject()` 호출
- fire-and-forget Task는 `.ContinueWith()` 또는 별도 예외 처리 추가

---

### 2026-02-01 - PopupWindow 아이콘 이미지 비율 유지 개선

#### 문제점
- PopupWindow에서 아이콘 이미지가 버튼에 풀 사이즈로 표시됨
- 비정사각형 이미지가 강제로 24x24 크기로 조정되어 비율이 왜곡될 수 있음

#### 원인 분석
- Image 요소에 `Width`와 `Height`가 고정값(`ICON_SIZE=24`)으로 설정됨
- 이미지가 지정된 크기에 맞춰 강제 조정됨

#### 수정 내용
- `PopupWindow.xaml.cs`의 세 가지 아이템 템플릿 수정
  - `_itemTemplate` (라벨 없는 템플릿)
  - `_itemTemplateWithLabel` (라벨 있는 템플릿)
  - `_itemTemplateHorizontalLabel` (수평 라벨 템플릿)
- Image 요소의 `Width`/`Height` → `MaxWidth`/`MaxHeight`로 변경
- `Stretch="Uniform"`과 함께 사용하여 원본 비율 유지

#### 수정 전/후 비교
```xaml
<!-- 수정 전: 고정 크기 -->
<Image Width="24" Height="24" Stretch="Uniform" ... />

<!-- 수정 후: 최대 크기 제한, 비율 유지 -->
<Image MaxWidth="24" MaxHeight="24" Stretch="Uniform" ... />
```

#### 변경된 파일
- `View/PopupWindow.xaml.cs` - InitializeTemplates() 및 CreateLabelTemplates() 메서드

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 328개 (기존 경고, 이번 작업과 무관)

---

### 2026-02-01 - 파일 드래그 시 아이콘 크기 문제 수정

#### 문제점
- EditGroupWindow에서 파일을 드래그하여 추가할 때 아이콘이 캔버스 내에서 작게 표시됨
- 256x256 캔버스에 32x32 아이콘이 중앙에 작게 배치되는 현상

#### 원인 분석
- `CropToActualContent` 메서드가 일부 아이콘 추출 경로에서만 호출됨
- `TryExtractIconViaShellItemImageFactory`, `TryExtractIconViaSHGetImageList`에서는 크롭 적용됨 ✓
- 하지만 폴백 메서드(`ExtractIconEx`, `SHGetFileInfo`, `ExtractAssociatedIcon`)에서는 크롭 미적용 ✗
- 고해상도 메서드 실패 시 폴백으로 추출된 아이콘이 작게 표시됨

#### 수정 내용
1. `ExtractIconWithoutArrow` 메서드 (Method 3, 4, 5)
   - `ExtractIconEx`: `CropToActualContent` 적용
   - `SHGetFileInfo`: `CropToActualContent` 적용
   - `ExtractAssociatedIcon`: `CropToActualContent` 적용

2. `ExtractSpecificIcon` 메서드 (Method 3, 4)
   - `ExtractIconEx`: `CropToActualContent` 적용
   - `SHGetFileInfo`: `CropToActualContent` 적용

3. `ExtractIconAndSaveAsync` 메서드 (우선순위 4, 5)
   - 바로가기 파일에서 `ExtractIconEx` 추출: `CropToActualContent` 적용
   - 타겟 파일의 `ExtractAssociatedIcon` 추출: `CropToActualContent` 적용

#### 변경된 파일
- `IconHelper.cs` - 모든 폴백 아이콘 추출 경로에 CropToActualContent 적용

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 623개 (기존 경고, 이번 작업과 무관)

---

### 2026-01-31 - 코드 품질 및 보안 개선 (Critical 이슈 수정)

#### 수정된 이슈

##### 1. 데드락 위험 수정 (App.xaml.cs)
- **문제**: 생성자에서 `Task.Run().Wait()` 사용으로 UI 스레드 블로킹 및 데드락 위험
- **수정**: `_pendingLaunchAllGroupName` 필드 추가, OnLaunched에서 비동기 처리
- **변경 위치**: App.xaml.cs 34, 105-113, 340-345번 줄

##### 2. 이벤트 핸들러 누수 수정 (SettingsDialog.xaml.cs)
- **문제**: Toggle 이벤트 핸들러가 Unloaded 시 해제되지 않아 메모리 누수
- **수정**: `SettingsDialog_Unloaded` 메서드 추가하여 모든 Toggle 이벤트 해제
- **변경 위치**: SettingsDialog.xaml.cs 17-36번 줄

##### 3. 경로 인젝션 취약점 수정 (MainWindow.xaml.cs)
- **문제**: `draggedItem.GroupName`이 파일 경로에 직접 사용되어 경로 트래버설 공격 가능
- **수정**: `IsValidGroupName()` 검증 메서드 추가
  - 경로 트래버설 패턴(`..`, `/`, `\`) 검사
  - 파일명에 사용할 수 없는 문자 검사
- **변경 위치**: MainWindow.xaml.cs 310-337, 353번 줄

#### 변경된 파일
- `App.xaml.cs` - LaunchAll 데드락 방지, 비동기 처리로 변경
- `View/SettingsDialog.xaml.cs` - Unloaded 이벤트 핸들러 추가
- `View/MainWindow.xaml.cs` - 그룹 이름 보안 검증 메서드 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - PopupWindow 아이콘 이미지 품질 개선

#### 문제점
- PopupWindow에 표시되는 아이콘 이미지가 자글자글하게(픽셀화) 표시됨
- BitmapImage 로드 시 DecodePixelWidth/Height 미설정으로 스케일링 품질 저하

#### 원인 분석
- `LoadImageFromPathAsync`에서 이미지를 원본 크기로 로드 후 UI에서 스케일링
- DPI 스케일링 미고려로 고DPI 환경에서 품질 저하
- ICON_SIZE = 24px인데 DPI 150%면 실제 36px 필요

#### 수정 내용
- `IconCache.cs`의 `LoadImageFromPathAsync` 메서드 수정
  - `decodeSize` 파라미터 추가 (기본값 48px)
  - `DecodePixelWidth`/`DecodePixelHeight` 설정
  - `DecodePixelType.Logical` 설정으로 DPI 자동 스케일링

#### 변경된 파일
- `IconCache.cs` - LoadImageFromPathAsync 메서드에 DecodePixel 설정 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - PopupWindow에서 실행한 앱이 AppGroup 종료 시 함께 종료되는 문제 수정

#### 문제점
- PopupWindow에서 등록된 앱을 실행한 후 AppGroup을 종료하면 실행된 앱도 함께 종료됨
- MSIX 패키지 앱은 Windows가 Job Object로 모든 자식 프로세스를 관리하기 때문에 발생

#### 원인 분석
- MSIX 컨테이너가 프로세스 트리 전체를 Job Object로 묶어서 관리
- 부모 프로세스(AppGroup) 종료 시 Job Object에 속한 모든 자식 프로세스도 함께 종료
- 기존 `Process.Start()`의 `UseShellExecute = true`로도 Job Object 범위를 벗어나지 못함

#### 수정 내용
- `PopupWindow.xaml.cs`의 `TryLaunchApp` 메서드 수정
  - `cmd.exe /c start`를 통해 실행하여 Job Object에서 분리
  - cmd.exe가 즉시 종료되면서 실행된 앱은 독립 프로세스로 남음

- `PopupWindow.xaml.cs`의 `TryRunAsAdmin` 메서드 수정
  - `powershell.exe Start-Process -Verb RunAs`를 통해 관리자 권한 실행
  - PowerShell이 즉시 종료되면서 실행된 앱은 독립 프로세스로 남음

#### 변경된 파일
- `View/PopupWindow.xaml.cs` - TryLaunchApp, TryRunAsAdmin 메서드 수정

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - ICO 변환 시 이미지 비율 유지 수정

#### 문제점
- 리소스 아이콘에서 PNG 이미지 선택 시 ICO로 변환되면서 이미지 비율이 변경됨
- 정사각형이 아닌 이미지가 강제로 정사각형으로 늘어나서 찌그러짐

#### 원인 분석
- `ConvertToIco` 메서드에서 `g.DrawImage(originalImage, 0, 0, size.Width, size.Height)` 사용
- 원본 비율 무시하고 강제로 정사각형 크기로 리사이징

#### 수정 내용
- `IconHelper.cs`의 `ConvertToIco` 메서드 수정
  - 원본 이미지 비율 계산하여 유지
  - 정사각형 캔버스에 중앙 배치
  - 남는 영역은 투명 배경으로 유지

```csharp
// 수정 전: 비율 무시, 강제 리사이징
g.DrawImage(originalImage, 0, 0, size.Width, size.Height);

// 수정 후: 비율 유지, 중앙 배치
float scale = Math.Min((float)size.Width / originalImage.Width, (float)size.Height / originalImage.Height);
int newWidth = (int)(originalImage.Width * scale);
int newHeight = (int)(originalImage.Height * scale);
int x = (size.Width - newWidth) / 2;
int y = (size.Height - newHeight) / 2;
g.DrawImage(originalImage, x, y, newWidth, newHeight);
```

#### 변경된 파일
- `IconHelper.cs` - ConvertToIco 메서드에서 비율 유지 로직 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - 그룹 편집 시 아이콘 파일 없음 오류 수정

#### 문제점
- 그룹 편집 시 아이콘 파일이 존재하지 않으면 `FileNotFoundException` 발생
- 아이콘이 없어도 편집 창에서 다른 아이콘을 선택할 수 있어야 함

#### 수정 내용
- `EditGroupWindow.xaml.cs`의 `LoadGroupAsync` 메서드 수정
  - 아이콘 로드 전 `File.Exists` 확인 추가
  - 파일이 없거나 로드 실패 시 오류 대신 기본 상태로 유지
  - `selectedIconPath`를 빈 문자열로 설정하여 새 아이콘 선택 가능

#### 변경된 파일
- `View/EditGroupWindow.xaml.cs` - 아이콘 파일 존재 확인 및 예외 처리 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - 저장 시 아이콘 파일 처리 오류 수정

#### 문제점 1: ICO 변환 시 파일 잠금 오류
- "이미지를 ICO 형식으로 변환하지 못했습니다" 오류 발생
- `Image.FromFile`이 파일을 독점 잠금

#### 문제점 2: 파일 삭제 후 사용 시도
- `FileNotFoundException` 발생
- 저장 시 기존 ICO/PNG 파일을 먼저 삭제한 후, 삭제된 `selectedIconPath`를 사용하려고 시도

#### 원인 분석
1. temp 폴더 복사 제거 후 원본 파일을 직접 사용
2. 저장 로직에서 기존 파일 삭제 → `selectedIconPath` 파일도 삭제됨
3. 삭제된 파일을 `File.Copy` 또는 `ConvertToIco`에서 사용하려고 시도

#### 수정 내용
1. `IconHelper.cs`의 `ConvertToIco` 메서드 수정
   - `Image.FromFile` → `File.ReadAllBytesAsync` + `Image.FromStream`

2. `EditGroupWindow.xaml.cs`의 `CreateShortcut_Click` 메서드 수정
   - 기존 파일 삭제 **전에** `selectedIconPath`를 메모리로 읽어둠
   - ICO 파일: 메모리에서 직접 `File.WriteAllBytesAsync`로 저장
   - PNG/JPG 등: 메모리에서 먼저 저장 후 `ConvertToIco` 호출

#### 변경된 파일
- `IconHelper.cs` - ConvertToIco 메서드에서 파일 잠금 없이 이미지 로드
- `View/EditGroupWindow.xaml.cs` - 아이콘 파일을 메모리로 먼저 읽어서 처리

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - 앱 추가 시 IconPath 누락 버그 수정

#### 문제점
- EditGroupWindow에서 저장 후 메인 윈도우의 앱 목록에서 아이콘이 사라지는 문제
- 새로 추가된 앱의 `IconPath`가 설정되지 않아 JSON에 빈 문자열로 저장됨
- 다음 로드 시 아이콘 경로가 없어서 아이콘 표시 실패

#### 원인 분석
- `ExeFiles.Add` 호출 시 `IconPath` 속성 누락
- JSON 로드 시에는 `IconPath = icon` 설정하지만, 새 앱 추가 시에는 누락됨
- 저장 시 `file.IconPath`가 빈 문자열 → JSON에 빈 값 저장 → 다음 로드 실패

#### 수정 내용
- `EditGroupWindow.xaml.cs`의 모든 `ExeFiles.Add` 호출에 `IconPath = icon` 추가
  - 파일 드래그&드롭 추가 (363-368번 줄)
  - 파일 선택 추가 (1000번 줄)
  - 설치된 앱 목록에서 추가 (2003-2010번 줄)

#### 변경된 파일
- `View/EditGroupWindow.xaml.cs` - ExeFiles.Add 호출 3곳에 IconPath 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-31 - 그룹 편집 시 임시 아이콘 파일 접근 오류 수정

#### 문제점
- 그룹 편집 시 `System.UnauthorizedAccessException` 발생
- 오류 경로: `C:\Users\...\AppData\Local\Temp\AppGroup\{그룹명}\{그룹명}_regular_{timestamp}.png`
- `File.Copy`로 임시 폴더에 아이콘 복사 시 파일 잠금 오류 발생

#### 원인 분석
1. `LoadGroupAsync` 메서드에서 원본 아이콘을 temp 폴더에 복사하는 불필요한 작업 수행
2. temp 폴더에 쓰기 권한 문제 또는 파일 잠금 문제 발생
3. 실제로 temp 폴더 복사는 불필요함 - 원본 파일에서 직접 메모리로 읽으면 됨

#### 이전 수정 시도 (모두 실패)
1. 파일 삭제 후 복사 → 파일이 잠겨있으면 삭제도 실패
2. GUID 기반 고유 파일명 → 폴더 자체에 쓰기 권한 문제로 실패

#### temp 폴더 사용 이유 분석
- 원래 temp 폴더에 복사한 후, 저장 완료 시 temp 파일/폴더 삭제하는 구조
- 그러나 메모리 스트림으로 이미지를 로드하므로 temp 복사는 불필요

#### 최종 수정 내용
1. `LoadGroupAsync` 메서드 수정
   - **temp 폴더 복사 코드 전체 제거**
   - 원본 파일 경로(`groupIcon`)를 직접 사용: `tempIcon = groupIcon;`

2. `CreateShortcut_Click` 메서드 수정
   - tempIcon 삭제 전 **temp 폴더 경로인지 확인** 추가
   - 원본 파일 삭제 방지

3. `Dispose` 메서드 수정
   - tempIcon 폴더 삭제 전 **temp 폴더 경로인지 확인** 추가
   - 원본 폴더 삭제 방지

#### 변경된 파일
- `View/EditGroupWindow.xaml.cs`
  - temp 폴더 복사 로직 제거
  - tempIcon 삭제 시 temp 폴더 경로 확인 조건 추가 (2곳)

#### 검증 결과
- 빌드: 성공
- 오류: 0개

#### 참고: 동일 실수 방지
- 파일을 메모리로 읽어서 사용하는 경우, 불필요한 파일 복사를 피할 것
- 임시 파일 삭제 시 원본 파일인지 반드시 확인할 것

---

### 2026-01-31 - 아이콘 크기 문제 수정 (SHGetImageList 적용)

#### 문제점
- 설치된 앱 목록에서 일부 앱의 아이콘이 32x32 크기로 작게 표시됨
- `ExtractIconWithoutArrow` 메서드의 폴백 메서드들(Method 2~4)이 모두 32x32만 반환

#### 원인 분석
- Method 1: `TryExtractIconViaShellItemImageFactory` - 256~32 크기 시도 (성공 시 큰 아이콘)
- Method 2~4: `ExtractIconEx`, `SHGetFileInfo`, `ExtractAssociatedIcon` - 모두 32x32만 반환
- Method 1 실패 시 폴백 메서드들이 작은 아이콘만 반환하는 문제

#### 수정 내용

1. **NativeMethods.cs 수정**
   - `SHGFI_SYSICONINDEX` 상수 추가 (0x000004000)
   - SHIL 상수 추가 (SHIL_LARGE, SHIL_SMALL, SHIL_EXTRALARGE, SHIL_SYSSMALL, SHIL_JUMBO)
   - `IID_IImageList` GUID 추가
   - `IImageList` COM 인터페이스 추가 (시스템 이미지 리스트에서 아이콘 추출)
   - `IMAGELISTDRAWPARAMS`, `IMAGEINFO` 구조체 추가
   - ILD 플래그 상수 추가 (ILD_NORMAL, ILD_TRANSPARENT, ILD_IMAGE)
   - `SHGetImageList` P/Invoke 추가 (shell32.dll ordinal #727)

2. **IconHelper.cs 수정**
   - `TryExtractIconViaSHGetImageList` 메서드 추가
     - SHGetFileInfo로 시스템 아이콘 인덱스 획득
     - SHIL_JUMBO(256x256) → SHIL_EXTRALARGE(48x48) 순서로 시도
     - IImageList.GetIcon()으로 아이콘 추출
   - `ExtractIconWithoutArrow` 메서드 수정
     - 기존 Method 1 다음에 Method 2로 `TryExtractIconViaSHGetImageList` 호출 추가
     - 기존 Method 2~4를 Method 3~5로 번호 조정
   - `ExtractSpecificIcon` 메서드 수정 (IconLocation에서 아이콘 추출)
     - iconIndex가 0인 경우 IShellItemImageFactory와 SHGetImageList 먼저 시도
     - 실패 시 기존 ExtractIconEx/SHGetFileInfo로 폴백
   - `ExtractLnkIconWithoutArrowAsync` 메서드 수정
     - 폴백 경로에 SHGetImageList 추가 (우선순위 3)
     - 기존 ExtractIconEx/ExtractAssociatedIcon을 우선순위 4~5로 조정
   - `ExtractIconAndSaveAsync` 메서드 수정 (.lnk 파일 처리)
     - 폴백 경로에 SHGetImageList 추가 (우선순위 3)
     - 기존 ExtractIconEx/ExtractAssociatedIcon을 우선순위 4~5로 조정

#### 수정 후 아이콘 추출 우선순위 (ExtractIconWithoutArrow)
1. `IShellItemImageFactory` (256/128/64/48/32) - 기존
2. `SHGetImageList` (256x256 또는 48x48) - 신규
3. `ExtractIconEx` (32x32) - 폴백
4. `SHGetFileInfo` (32x32) - 폴백
5. `ExtractAssociatedIcon` (32x32) - 최종 폴백

#### 수정 후 아이콘 추출 우선순위 (.lnk 바로가기 처리)
1. `ExtractWindowsAppIconAsync` (UWP 앱 아이콘)
2. `ExtractUwpAppIconAsync` (shell:AppsFolder 경로인 경우)
3. `ExtractIconWithoutArrow` (타겟 경로에서)
4. `ExtractSpecificIcon` (IconLocation에서)
5. `TryExtractIconViaSHGetImageList` (바로가기 파일에서 고해상도)
6. `ExtractIconEx` (바로가기 파일에서 32x32)
7. `ExtractAssociatedIcon` (최종 폴백 32x32)

#### 변경된 파일
- `NativeMethods.cs` - SHIL 상수, IImageList 인터페이스, SHGetImageList P/Invoke 추가
- `IconHelper.cs` - TryExtractIconViaSHGetImageList 메서드 추가, ExtractIconWithoutArrow 수정

#### 추가 수정 (아이콘 원본 크기 유지)
- `ExtractWindowsAppIconAsync` 메서드 수정
  - `ResizeImageToFitSquare(originalBitmap, 200)` 제거
  - 원본 크기 그대로 반환
- `ExtractUwpAppIconAsync` 메서드 수정
  - `ResizeImageToFitSquare(originalBitmap, 200)` 제거
  - 원본 크기 그대로 반환
- `TryExtractIconFromShellPath` 메서드 수정
  - 요청 크기 200x200 → 256x256 변경 (가능한 큰 아이콘 추출)

#### 추가 수정 (실제 아이콘 크기로 크롭)
- `CropToActualContent` 메서드 추가
  - 256x256 캔버스에 32x32 아이콘이 중앙 배치된 경우 실제 아이콘 영역만 크롭
  - 투명하지 않은 픽셀의 경계를 찾아 정사각형으로 크롭
  - 여백이 10% 미만이면 크롭하지 않음 (이미 꽉 찬 아이콘)
- `TryExtractIconViaSHGetImageList` 수정: 크롭 적용
- `TryExtractIconViaShellItemImageFactory` 수정: 크롭 적용
- `TryExtractIconFromShellPath` 수정: 크롭 적용

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 624개 (기존 경고, 이번 작업과 무관)

---

### 2026-01-31 - 코드 품질 및 보안 개선

#### 수행한 작업

1. **메모리 누수 수정** (MainWindow.xaml.cs)
   - `GroupListView_SelectionChanged`에서 새 `EditGroupWindow` 인스턴스 생성 후 참조 손실 문제
   - 기존 `EditGroupHelper`를 사용하여 중앙 관리하도록 수정

2. **비동기 작업 미완료 수정** (App.xaml.cs)
   - LaunchAll 명령 처리에서 `Task.Run()` 후 즉시 `Environment.Exit(0)` 호출 문제
   - `.Wait()` 추가로 비동기 작업 완료 대기

3. **UI 스레드 블로킹 제거** (App.xaml.cs)
   - `Task.Delay().Wait()` 호출을 `Thread.Sleep()`으로 대체

4. **중복 코드 제거**
   - `AppPaths.cs` (신규): 공통 경로/파일 유틸리티 클래스 생성
     - `SaveGroupIdToFile()`, `SaveGroupNameToFile()` 통합
   - `NativeMethods.cs`: `BringWindowToFrontSafe()` 메서드 추가
   - App.xaml.cs, MainWindow.xaml.cs, EditGroupWindow.xaml.cs에서 중복 메서드 제거

5. **경로 보안 검증 추가** (EditGroupWindow.xaml.cs)
   - `IsValidGroupName()` 메서드 추가: 경로 이동 공격 방지
   - `CreateShortcut_Click`에 검증 로직 추가

6. **주석 처리된 코드 정리**
   - App.xaml.cs: 사용하지 않는 주석 블록 제거
   - MainWindow.xaml.cs: SetupFileWatcher 주석 블록 제거
   - EditGroupWindow.xaml.cs: fileWatcher 주석 블록 제거
   - PopupWindow.xaml.cs: 여러 주석 블록 제거 (SubclassWindow, WM_UPDATE_GROUP, CreateDynamicContent foreach, LoadGridItems, GridView_DragItemsCompleted)

#### 변경된 파일
- `AppPaths.cs` (신규) - 공통 경로/파일 유틸리티 클래스
- `NativeMethods.cs` - BringWindowToFrontSafe 메서드 추가
- `App.xaml.cs` - LaunchAll 비동기 대기, Thread.Sleep 사용, 중복 메서드 제거
- `View/MainWindow.xaml.cs` - SelectionChanged 수정, 중복 메서드 제거
- `View/EditGroupWindow.xaml.cs` - 보안 검증 추가, 중복 메서드 제거
- `View/PopupWindow.xaml.cs` - 주석 처리된 코드 제거

#### 검증 결과
- 빌드: 성공
- 오류: 0개
- 경고: 3개 (기존 경고, 이번 작업과 무관)

---

### 2026-01-31 - Windows 부팅 시 자동 실행 시 메인 윈도우 표시 버그 수정

#### 문제점
- Windows 부팅 시 StartupTask로 자동 실행되면 silent 모드로 동작해야 함
- 시스템 트레이만 표시되어야 하는데 메인 윈도우도 함께 표시되는 문제

#### 원인 분석
- `CreateAllWindows()` 메서드에서 `editWindow`와 `popupWindow`는 숨기거나 화면 밖으로 이동
- 그러나 `m_window` (MainWindow)는 숨기는 코드가 누락됨
- `MainWindow` 생성자에서 `CenterOnScreen()` 호출 시 창이 활성화될 수 있음

#### 수정 내용
- `App.xaml.cs`의 `CreateAllWindows()` 메서드에 메인 윈도우 숨기기 코드 추가
- `NativeMethods.ShowWindow(mainHWnd, NativeMethods.SW_HIDE)` 호출

#### 변경된 파일
- `App.xaml.cs` - CreateAllWindows() 메서드에 메인 윈도우 숨기기 추가

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-30 - 아이콘 상하 반전 버그 수정

#### 문제점
- 앱 목록에서 일부 아이콘을 저장할 때 상하 반전되어 저장되는 문제
- `IShellItemImageFactory.GetImage`에서 반환하는 HBITMAP은 **top-down** 순서로 데이터가 저장됨
- `ConvertHBitmapToArgbBitmap` 메서드에서 모든 비트맵을 항상 뒤집고 있어서 반전됨

#### 원인 분석
- 기존 코드: 모든 DIB가 bottom-up이라고 가정하고 항상 뒤집기 수행
- Shell API(`IShellItemImageFactory.GetImage`)에서 반환하는 HBITMAP은 **top-down** 순서
- `GetBitmapBits`는 메모리에 저장된 순서대로 데이터 반환
- 이미 top-down인 데이터를 뒤집으면 상하 반전됨

#### 이전 수정 시도 (실패)
- `biHeight` 부호로 top-down/bottom-up 판단 시도
- 그러나 Shell API는 `biHeight`가 양수이더라도 실제 데이터는 top-down으로 저장
- `biHeight` 부호만으로는 정확한 판단 불가

#### 최종 수정
- 뒤집기 로직 완전히 제거
- Shell API에서 반환하는 HBITMAP은 이미 올바른 순서(top-down)이므로 그대로 사용

#### 변경된 파일
- `NativeMethods.cs` - DIBSECTION, BITMAPINFOHEADER 구조체 추가 (향후 참고용 유지)
- `IconHelper.cs` - ConvertHBitmapToArgbBitmap 메서드에서 뒤집기 로직 제거

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-30 - 백그라운드 동작 최적화 및 메모리 누수 수정

#### 문제점
- FileSystemWatcher 이벤트 핸들러가 람다식으로 등록되어 정확한 해제 불가
- COM 객체(IWshShell, IWshShortcut) 미해제로 인한 메모리 누수
- 불필요한 GC.Collect() 호출로 성능 저하
- EditGroupWindow에 IDisposable 패턴 미구현
- IconCache 크기 제한 없음

#### 수행한 작업

1. **MainWindow.xaml.cs 수정**
   - FileSystemWatcher 이벤트 핸들러를 람다식에서 명명된 메서드(`OnFileWatcherChanged`)로 변경
   - Dispose에서 정확한 핸들러 해제 구현

2. **PopupWindow.xaml.cs 수정**
   - `CancellationTokenSource` 추가하여 백그라운드 작업 취소 지원
   - COM 객체(IWshShell, IWshShortcut) `Marshal.ReleaseComObject()` 해제 추가
   - 불필요한 `GC.Collect()` 호출 제거
   - Dispose에서 CancellationTokenSource 정리 추가

3. **EditGroupWindow.xaml.cs 수정**
   - IDisposable 패턴 구현
   - FileSystemWatcher, 이미지 참조, 이벤트 핸들러 정리 로직 추가
   - 불필요한 `GC.Collect()` 호출 제거

4. **IconCache.cs 수정**
   - 캐시 크기 제한 추가 (MAX_CACHE_SIZE = 500)
   - `CleanupOldCacheEntriesAsync()` - 오래된 캐시 항목 정리
   - `InvalidateMissingEntriesAsync()` - 존재하지 않는 파일 참조 제거
   - `ClearCache()`, `Count` 속성 추가

#### 변경된 파일
- `View/MainWindow.xaml.cs` - FileSystemWatcher 이벤트 핸들러 개선
- `View/PopupWindow.xaml.cs` - COM 객체 해제, CancellationToken, GC.Collect 제거
- `View/EditGroupWindow.xaml.cs` - IDisposable 패턴 구현
- `IconCache.cs` - 캐시 크기 제한 및 정리 로직

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-30 - StartupTask 상태 처리 개선

#### 문제점
- `AddToStartupAsync()`가 `Disabled` 상태만 처리
- `DisabledByUser` (사용자가 Windows 설정에서 거부) 상태 미처리
- 토글 ON이지만 실제로 비활성화인 경우 사용자에게 피드백 없음

#### 수행한 작업
1. **SettingsHelper.cs 수정**
   - `AddToStartupAsync()` 반환 타입을 `Task<StartupTaskState?>`로 변경
   - 모든 StartupTask 상태 처리 (Disabled, DisabledByUser, DisabledByPolicy, Enabled)

2. **SettingsDialogViewModel.cs 수정**
   - `StartupStatusMessage`, `IsStartupBlocked`, `StartupStatusVisibility` 속성 추가
   - `ApplyStartupSettingsAsync()` 개선 - 결과에 따른 처리
   - `DisabledByUser` 상태일 경우 토글 OFF 및 메시지 표시

3. **SettingsDialog.xaml 수정**
   - 시작 프로그램 차단 상태 메시지 표시 TextBlock 추가

#### 변경된 파일
- `SettingsHelper.cs` - StartupTask 상태 반환 및 전체 상태 처리
- `ViewModels/SettingsDialogViewModel.cs` - 상태 표시 속성 및 처리 로직
- `View/SettingsDialog.xaml` - 차단 상태 메시지 UI

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-30 - MSIX StartupTask 자동 실행 시 silent 모드 지원

#### 문제점
- MSIX StartupTask API는 명령줄 인자(`--silent`)를 직접 지원하지 않음
- 비-MSIX 배포(레지스트리 방식)는 이미 `--silent` 옵션 포함
- MSIX 패키지로 자동 시작 시 메인 윈도우가 표시되는 문제

#### 수행한 작업
1. **App.xaml.cs 수정**
   - `Microsoft.Windows.AppLifecycle` 네임스페이스 추가
   - `IsStartedByStartupTask()` 메서드 추가 - StartupTask에 의한 시작 여부 확인
   - `HasSilentFlag()` 메서드 수정 - StartupTask 시작 시에도 silent 모드 반환

#### 변경된 파일
- `App.xaml.cs` - StartupTask 활성화 감지 로직 추가

#### 동작 방식
- `AppInstance.GetCurrent().GetActivatedEventArgs()` 사용
- `ExtendedActivationKind.StartupTask` 확인
- StartupTask로 시작 시 `--silent`와 동일하게 동작 (시스템 트레이만 표시)

#### 검증 결과
- 빌드: 성공
- 오류: 0개

---

### 2026-01-30 - Windows 시작 시 자동 실행 기능 개선

#### 문제점
- MSIX 패키지 앱에서 레지스트리 기반(`HKCU\...\Run`) 시작 프로그램 등록이 제대로 작동하지 않음
- WinUI3/MSIX 앱은 `Windows.ApplicationModel.StartupTask` API를 사용해야 함
- `Package.appxmanifest`에 StartupTask 확장이 없었음

#### 수행한 작업
1. **Package.appxmanifest 수정**
   - `uap5` 네임스페이스 추가
   - `uap5:StartupTask` 확장 추가 (TaskId: `AppGroupStartupTask`)

2. **SettingsHelper.cs 수정**
   - `IsMsixPackage()` 메서드 추가 - MSIX 패키지 여부 확인
   - `AddToStartupAsync()` 메서드 추가 - MSIX용 StartupTask API 사용
   - `RemoveFromStartupAsync()` 메서드 추가 - MSIX용 StartupTask API 사용
   - `IsInStartupAsync()` 메서드 추가 - MSIX용 StartupTask 상태 확인
   - 비-MSIX 배포용 레지스트리 방식 폴백 유지
   - `EnsureStartupSettingIsApplied()` 비동기 API 사용으로 변경

3. **SettingsDialogViewModel.cs 수정**
   - `LoadCurrentSettingsAsync()` - 비동기 `IsInStartupAsync()` 사용
   - `SaveSettingsAsync()` - 비동기 적용으로 변경
   - `ApplyStartupSettingsAsync()` - 비동기 메서드로 변경

#### 변경된 파일
- `Package.appxmanifest` - StartupTask 확장 추가
- `SettingsHelper.cs` - StartupTask API 적용
- `ViewModels/SettingsDialogViewModel.cs` - 비동기 호출 반영

#### 검증 결과
- 빌드: 성공 (x64)
- 오류: 0개

---

### 2026-01-30 - 설치된 앱 목록 아이콘 표시 개선 (4차 수정)

#### 추가 문제
- 바로가기 아이콘(화살표 포함)이 표시되는 문제
- 바로가기 파일에서 실제 타겟 파일의 아이콘을 가져와야 함

#### 추가 수정 내용
- `GetAppIconFromShellAsync` 개선
  - 바로가기 파일 자체의 아이콘 대신 `IconLocation`에서 아이콘 경로 추출
  - `IconLocation`이 없으면 `TargetPath`의 파일에서 아이콘 추출
  - 모두 실패 시 바로가기 파일 자체에서 추출 (화살표 제거 로직 사용)

---

### 2026-01-30 - 설치된 앱 목록 아이콘 표시 개선 (3차 수정)

#### 문제
- 설치된 앱 목록 다이얼로그에서 많은 앱들의 아이콘이 표시되지 않음
- UWP 앱의 바로가기 타겟이 `shell:AppsFolder\{aumid}` 형식인 경우 아이콘 추출 실패
- `ExtractUwpAppIconAsync`가 `*StoreLogo*.png` 패턴만 검색하여 다른 이름의 로고 파일 못 찾음
- `IShellItem`에서 `IShellItemImageFactory`로의 COM 캐스팅 실패

#### 원인 분석
1. `GetAppIconFromShellAsync`에서 바로가기 생성 후 `IconCache.GetIconPathAsync` 호출
2. 바로가기 타겟이 `shell:AppsFolder` 경로이면 `File.Exists()` 실패
3. `ExtractUwpAppIconAsync`가 `*StoreLogo*.png` 패턴만 검색
4. UWP 패키지를 찾지 못하는 경우 대체 방법 없음
5. COM 인터페이스 캐스팅 문제로 `IShellItemImageFactory` 획득 실패

#### 수행한 작업
1. `IconHelper.cs`의 `ExtractIconAndSaveAsync` 수정
   - 바로가기 타겟이 `shell:AppsFolder` 경로인 경우 `ExtractUwpAppIconAsync` 호출 추가
   - `SHGetFileInfo` 기반 아이콘 추출 폴백 추가

2. `IconHelper.cs`의 `ExtractUwpAppIconAsync` 대폭 개선
   - Manifest에서 다양한 로고 XPath 검색 (Square44x44Logo, Square150x150Logo, Logo)
   - 로고 파일명 기반 다양한 패턴 검색
   - contrast 버전 파일 제외
   - 로고 파일을 찾지 못한 경우 Shell API 폴백 추가

3. `IconHelper.cs`에 `ExtractIconFromShellItem` 메서드 추가
   - 여러 경로 형식 시도 (shell:AppsFolder, shell:::{CLSID}, AUMID만)
   - `SHCreateItemFromParsingName`으로 `IShellItemImageFactory` 직접 요청
   - `GetImage`로 아이콘 추출

4. `IconHelper.cs`에 `ExtractIconUsingSHGetFileInfo` 메서드 추가
   - 바로가기 파일에서 Shell이 제공하는 아이콘 추출
   - 최종 폴백으로 사용

5. `NativeMethods.cs`에 Shell API 인터페이스 추가
   - `IShellItem`, `IShellItemImageFactory` COM 인터페이스
   - `SHCreateItemFromParsingName` 두 가지 오버로드 (IShellItem용, IShellItemImageFactory용)
   - `IShellItemImageFactoryGuid` GUID 상수 추가
   - `SIIGBF` 플래그 열거형

6. `EditGroupWindow.xaml.cs`의 `GetAppIconFromShellAsync` 수정
   - 바로가기 생성 전에 먼저 `shell:AppsFolder\{aumid}` 경로로 직접 호출
   - 실패 시 기존 바로가기 생성 방식으로 폴백

#### 아이콘 추출 우선순위
1. `ExtractUwpAppIconAsync`: AppxManifest.xml에서 로고 경로 검색
2. `ExtractIconFromShellItem`: Shell API (`IShellItemImageFactory.GetImage`)
3. `ExtractWindowsAppIconAsync`: UWP 패키지 매니저 API
4. `ExtractIconEx`: 바로가기 파일에서 직접 추출
5. `SHGetFileInfo`: Shell이 제공하는 아이콘
6. `ExtractAssociatedIcon`: .NET 기본 API

#### 변경된 파일
- `IconHelper.cs` - 다수의 아이콘 추출 메서드 추가 및 개선
- `NativeMethods.cs` - Shell API COM 인터페이스 및 P/Invoke 추가
- `View/EditGroupWindow.xaml.cs` - GetAppIconFromShellAsync 개선

#### 검증 결과
- 빌드: 성공 (x64)
- 오류: 0개

---

### 2026-01-30 - MVVM 패턴 적용 (View 폴더 생성 및 파일 이동)

#### 수행한 작업
- View 폴더 생성 및 XAML 파일 이동
- namespace를 `AppGroup`에서 `AppGroup.View`로 변경
- 참조 파일들에 `using AppGroup.View;` 추가

#### 변경된 파일 (이동)
- `MainWindow.xaml` → `View/MainWindow.xaml`
- `MainWindow.xaml.cs` → `View/MainWindow.xaml.cs`
- `EditGroupWindow.xaml` → `View/EditGroupWindow.xaml`
- `EditGroupWindow.xaml.cs` → `View/EditGroupWindow.xaml.cs`
- `PopupWindow.xaml` → `View/PopupWindow.xaml`
- `PopupWindow.xaml.cs` → `View/PopupWindow.xaml.cs`
- `SettingsDialog.xaml` → `View/SettingsDialog.xaml`
- `SettingsDialog.xaml.cs` → `View/SettingsDialog.xaml.cs`

#### 변경된 파일 (참조 수정)
- `AppGroup.csproj` - 파일 경로 업데이트
- `App.xaml.cs` - `using AppGroup.View;` 추가
- `EditGroupHelper.cs` - `using AppGroup.View;` 추가
- `BackupHelper.cs` - `using AppGroup.View;` 추가

#### XAML 변경 내용
- `x:Class="AppGroup.ClassName"` → `x:Class="AppGroup.View.ClassName"`
- `xmlns:local="using:AppGroup"` → `xmlns:local="using:AppGroup.View"` (필요시)

#### Code-behind 변경 내용
- `namespace AppGroup` → `namespace AppGroup.View`

#### 검증 결과
- 빌드: 성공 (x64)
- 오류: 0개
- 경고: 265개 (기존 코드의 nullable 관련 경고, 변경하지 않음)

#### 참고 사항
- App.xaml/cs는 앱 엔트리 포인트이므로 루트에 유지
- 프로젝트 구조가 MVVM 패턴(Models, ViewModels, View)에 맞게 정리됨

---

### 2026-01-29 - UI 한글화

#### 수행한 작업
- 모든 UI 텍스트를 영어에서 한글로 번역
- XAML 파일 및 코드 비하인드 파일의 문자열 리터럴 수정

#### 변경된 파일
- `MainWindow.xaml` - 메인 화면 UI 텍스트
- `EditGroupWindow.xaml` - 그룹 편집 화면 UI 텍스트
- `SettingsDialog.xaml` - 설정 다이얼로그 UI 텍스트
- `MainWindow.xaml.cs` - 다이얼로그 메시지
- `EditGroupWindow.xaml.cs` - 다이얼로그 및 상태 메시지
- `SettingsDialog.xaml.cs` - 업데이트 상태 메시지
- `BackupHelper.cs` - 가져오기/내보내기 메시지
- `PopupWindow.xaml.cs` - 컨텍스트 메뉴 텍스트

#### 주요 번역 내용
- 메뉴: Import→가져오기, Export→내보내기, Settings→설정, Delete→삭제
- 버튼: Save→저장, Cancel→취소, OK→확인
- 다이얼로그: Error→오류, Success→성공, Overwrite→덮어쓰기
- 상태: Groups→그룹, Items→항목, selected→선택됨

#### 검증 결과
- 빌드: 성공 (x64)
- 오류: 0개

---

### 2026-01-29 - 파일 인코딩 변환

#### 수행한 작업
- 프로젝트의 모든 소스 파일을 UTF-8 with BOM 인코딩으로 변환
- 한글 주석 및 문자열이 깨지지 않도록 처리

#### 변경된 파일 (28개)
- `.cs` 파일: 19개
- `.xaml` 파일: 6개
- `.md` 파일: 2개
- `Properties\Resources.Designer.cs`: 1개

#### 검증 결과
- 빌드: 성공 (x64)

---

### 2026-01-29 - 설치된 앱 목록 기능 개선

#### 수행한 작업
- `AllAppsButton_Click` 메서드의 앱 목록 수집 방식을 `shell:AppsFolder` 기반으로 변경
- Windows의 "설정 > 앱 > 설치된 앱"과 동일한 목록 표시

#### 변경된 파일
- `EditGroupWindow.xaml.cs`

#### 변경 내용
1. **기존 방식 (제거됨)**
   - 시작 메뉴 바로가기 (.lnk) 검색
   - 바탕화면 바로가기 검색
   - 레지스트리 Uninstall 키 검색
   - App Paths 레지스트리 검색

2. **새로운 방식**
   - `shell:AppsFolder`를 통해 Windows에 등록된 모든 앱 열거
   - Shell.Application COM 객체 사용
   - Win32 앱과 UWP 앱 모두 포함

#### 추가된 메서드
- `GetAppsFromShellFolder()`: shell:AppsFolder에서 앱 목록 가져오기
- `TryGetExePathFromAumid()`: AUMID에서 exe 경로 추출 시도
- `GetAppIconFromShellAsync()`: UWP 앱 아이콘 추출
- `SanitizeFileName()`: 파일명에 사용할 수 없는 문자 제거

#### 삭제된 메서드
- `GetRegistryApps()`: 더 이상 사용하지 않음

#### 검증 결과
- 빌드: 성공 (x64)
- 경고: 기존 코드의 nullable 관련 경고 (변경하지 않음)

#### 이전 문제점
- 기존 방식은 시작 메뉴 바로가기와 레지스트리 기반으로 앱을 검색하여 Windows의 실제 설치된 앱 목록과 차이가 있었음
- UWP/Microsoft Store 앱이 누락되었음

#### 해결 방법
- `shell:AppsFolder`를 사용하면 Windows가 인식하는 모든 앱 (Win32 + UWP)을 열거할 수 있음
- 이는 Windows 탐색기에서 `shell:AppsFolder`를 열었을 때 표시되는 목록과 동일

---

### 2025-01-XX - 설치된 앱 목록 기능 추가

#### 추가된 기능
- **AllAppsButton**: EditGroupWindow에 설치된 Windows 앱 목록을 표시하는 기능 추가
  - 파일: `EditGroupWindow.xaml`, `EditGroupWindow.xaml.cs`
  - 체크박스로 여러 앱 선택 기능 지원
  - 검색 기능 지원
  - 선택된 앱 수 실시간 표시

#### 추가된 클래스
- `InstalledAppModel`: 설치된 앱 정보를 담는 모델 클래스
  - `DisplayName`: 앱 이름
  - `ExecutablePath`: 실행 파일 경로
  - `Icon`: 아이콘 경로
  - `IsSelected`: 선택 상태 (INotifyPropertyChanged 구현)
  - `SelectionChanged`: 선택 변경 이벤트

#### UI 컴포넌트
- `AllAppsDialog`: ContentDialog로 구현
- `AllAppsListView`: 앱 목록 ListView
- `AppSearchTextBox`: 검색 TextBox
- `SelectedAppsCount`: 선택된 앱 수 표시
- `AppsLoadingRing`: 로딩 인디케이터

---

### 2025-01-XX - 코드 정리

#### 제거된 기능
- `SupportDialogHelper` 관련 코드 삭제 (MainWindow.xaml.cs)
  - 앱 사용 횟수 추적 및 후원 다이얼로그 기능 삭제
- GitHub 버튼, Coffee(Support) 버튼 삭제 (MainWindow.xaml, MainWindow.xaml.cs)

#### 비활성화된 기능
- `CheckForUpdatesOnStartupAsync()` 호출 주석 처리 (MainWindow.xaml.cs)

---

## 미해결 이슈

### 1000줄 초과 파일 분리 작업 (대기)

AGENTS.md 규칙에 따라 1000줄 초과 파일을 partial class로 분리 필요.

#### 완료된 파일
- [x] `EditGroupWindow.xaml.cs`: 2721줄 → 1843줄 + FolderWeb.cs(481줄) + AllApps.cs(500줄)
- [x] `IconHelper.cs`: 2504줄 → 1374줄 + UwpExtractor.cs(659줄) + GridIcon.cs(319줄)

#### 남은 파일 (우선순위 순)

| 파일 | 현재 줄 수 | 분리 제안 |
|------|-----------|----------|
| `PopupWindow.xaml.cs` | 1756줄 | 템플릿 생성, 아이템 로드, 컨텍스트 메뉴 등 기능별 분리 |
| `MainWindow.xaml.cs` | 1465줄 | 탭별 핸들러 분리 (작업표시줄, 시작메뉴, 설정) |
| `NativeMethods.cs` | 1333줄 | API 카테고리별 분리 (Shell, Window, Icon 등) |

#### 작업 시 참고사항
- partial class 패턴 사용
- 같은 namespace와 class 이름 유지
- 각 partial 파일에 필요한 using 문 추가
- 분리 후 빌드 검증 필수
- notes.md에 작업 기록 추가

---

## 참고 사항

### COM 객체 사용
- `IWshShortcut` (바로가기 타겟 경로 추출)은 UI 스레드(STA)에서만 사용 가능
- 백그라운드 스레드에서 호출 시 예외 발생

### 빌드 환경
- Platform: x64 빌드 필요
- TFM: net10.0-windows10.0.26100.0

---

## 파일 구조

```
Windows/AppGroup/
├── View/                        # MVVM View 레이어
│   ├── MainWindow.xaml          # 메인 윈도우 UI
│   ├── MainWindow.xaml.cs       # 메인 윈도우 로직
│   ├── EditGroupWindow.xaml     # 그룹 편집 윈도우 UI (AllAppsDialog 포함)
│   ├── EditGroupWindow.xaml.cs  # 그룹 편집 로직 (InstalledAppModel 포함)
│   ├── PopupWindow.xaml         # 팝업 윈도우 UI
│   ├── PopupWindow.xaml.cs      # 팝업 윈도우 로직
│   ├── SettingsDialog.xaml      # 설정 다이얼로그 UI
│   └── SettingsDialog.xaml.cs   # 설정 다이얼로그 로직
├── ViewModels/                  # MVVM ViewModel 레이어
├── Models/                      # MVVM Model 레이어
├── App.xaml                     # 앱 리소스
├── App.xaml.cs                  # 앱 엔트리
├── ...
