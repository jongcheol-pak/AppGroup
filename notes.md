# AppGroup 작업 이력 (요약/인덱스)

## 최근 변경 요약 (최근 10건)

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
