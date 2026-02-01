# AppGroup 작업 이력

## 최근 변경 사항

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
1. 파일 삭제 후 복사 → 파일 잠겨있으면 삭제도 실패
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

없음 (이전 이슈 해결됨)

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
```
