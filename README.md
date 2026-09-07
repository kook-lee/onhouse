# 🏠 OnHouse - 부동산 매물 관리 솔루션 & 스마트 런처

**OnHouse**는 부동산 공인중개사를 위한 강력하고 편리한 매물 관리 솔루션입니다.
네이버 부동산 매물 연동/대장 검증, 원클릭 카카오톡 브리핑, 계약서 특약 자동 생성, 그리고 **GitHub Actions 및 OCI VPS 클라우드 기반 자동 업데이트 스마트 런처**를 탑재하고 있습니다.

---

## 🚀 스마트 런처 (배포 & 자동 업데이트)

매번 프로그램이 수정될 때마다 `exe` 파일을 일일이 전달하거나 복사할 필요가 없습니다!

1. **배포 방식**:
   - 사용자나 동료에게 `OnHouseLauncher.exe` 파일 1개만 전달하면 끝납니다.
2. **자동 업데이트 메커니즘**:
   - `OnHouseLauncher.exe`를 실행할 때마다 GitHub Releases 및 OCI VPS(`134.185.122.169`)에서 최신 버전을 감지합니다.
   - 새 버전이 있으면 실시간 다운로드 프로그레스 바와 함께 최신 실행 파일(`OnHouseLocal.exe`)을 교체하고 자동으로 실행합니다.
   - 네트워크 연결이 불안정하거나 오프라인인 경우에도 기존 버전으로 즉시 실행됩니다.

---

## 🌟 핵심 기능

1. **네이버 부동산 대장 검증 허브 (`/api/naver/...`):**
   - 네이버 부동산 매물 목록 및 내 장부 매물 실시간 대조 검증
   - 매물 일치 여부(완료/불일치/미등록/거래완료) 시각화 및 원클릭 동기화
   - 네이버 계정 보안 연동 및 2단계 인증/캡차 안내 지원

2. **서버 0원 / 로컬 SQLite 기반의 가볍고 강력한 데이터 관리:**
   - 설치 없이 단독 실행 파일로 즉시 구동
   - 데이터베이스(`realestate.db`)가 로컬에 안전하게 보관

3. **🔒 중개사 전용 비밀 장부:**
   - 도어락 비밀번호, 임대인 통화 메모, 성향, 비밀 전달 사항 로컬 보관

4. **💬 1초 완성 카톡 브리핑 & 📜 계약서 안전 특약 자동완성:**
   - 손님용 맞춤 카카오톡 추천 양식 원클릭 복사
   - 보증보험/반려동물/전세대출 등 상황별 맞춤 특약 생성

---

## 🛠️ CI/CD 자동 빌드 및 배포 파이프라인

개발자가 소스코드를 수정하고 GitHub `main` 브랜치에 푸시하면 자동으로 배포됩니다.

```bash
git add .
git commit -m "feat: 새로운 기능 추가 또는 버그 수정"
git push origin main
```

- **GitHub Actions (`.github/workflows/deploy.yml`)**:
  1. `OnHouseLocal` (.NET 8 Win-x64 단일 실행 파일) 자동 빌드
  2. `OnHouseLauncher` (스마트 자동 업데이터) 자동 빌드
  3. GitHub Release(`v1.0.x`) 자동 생성 및 바이너리(`exe`, `version.json`) 배포
  4. OCI Linux VPS (`134.185.122.169:/var/www/onhouse/updates/`)로 미러 동기화

---

## 📁 프로젝트 구조

```
onhouse/
  ├── .github/workflows/deploy.yml # GitHub Actions CI/CD 파이프라인
  ├── OnHouseLauncher/             # 스마트 자동 업데이트 런처 (.NET 8)
  │    ├── Program.cs              # 듀얼 업데이트 체크, 다운로드 바, 프로세스 교체 로직
  │    └── OnHouseLauncher.csproj
  ├── OnHouseLocal.csproj          # OnHouse 메인 서비스 프로젝트
  ├── Program.cs                   # Kestrel 웹 서버 및 백엔드 REST API
  ├── Models/                      # 데이터 모델 (PropertyItem, NaverProperty 등)
  ├── Services/                    # SQLite DB, NaverService 등 비즈니스 로직
  ├── version.txt                  # 현재 로컬 버전 식별자
  └── wwwroot/                     # 웹 대시보드 UI (HTML/CSS/JS)
```
