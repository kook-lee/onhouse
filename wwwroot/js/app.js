let currentUser = null;
let currentProperties = [];
let currentDanggeunDeals = [];
let selectedProperty = null;
let selectedDeal = null;
let currentViewMode = 'properties'; // 'properties' | 'danggeun'
let currentChannel = 'all'; // 'all' | '피터팬' | '당근' | '네이버'
let selectedDealIds = new Set();

document.addEventListener('DOMContentLoaded', () => {
  initAuth();
  initEvents();
});

// 0. Auth Logic
function initAuth() {
  const savedUser = localStorage.getItem('onhouse_user');
  if (savedUser) {
    try {
      currentUser = JSON.parse(savedUser);
      updateUserUI();
      loadProperties();
      loadDanggeunDeals();
      return;
    } catch { }
  }
  showAuthModal();
}

function showAuthModal() {
  document.getElementById('auth-modal').style.display = 'flex';
}

function hideAuthModal() {
  document.getElementById('auth-modal').style.display = 'none';
}

window.switchAuthTab = function(tab) {
  const loginForm = document.getElementById('login-form');
  const regForm = document.getElementById('register-form');
  const loginBtn = document.getElementById('tab-login-btn');
  const regBtn = document.getElementById('tab-register-btn');

  if (tab === 'login') {
    loginForm.style.display = 'block';
    regForm.style.display = 'none';
    loginBtn.classList.add('active');
    regBtn.classList.remove('active');
  } else {
    loginForm.style.display = 'none';
    regForm.style.display = 'block';
    loginBtn.classList.remove('active');
    regBtn.classList.add('active');
  }
};

function updateUserUI() {
  const badge = document.getElementById('user-profile-badge');
  const nameSpan = document.getElementById('user-display-name');
  if (currentUser) {
    badge.style.display = 'flex';
    nameSpan.textContent = `👤 ${currentUser.name} (${currentUser.agencyName || '중개사'})`;
  } else {
    badge.style.display = 'none';
  }
}

function initEvents() {
  // Login Form
  document.getElementById('login-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errDiv = document.getElementById('login-error');
    errDiv.style.display = 'none';

    const username = document.getElementById('login-username').value.trim();
    const password = document.getElementById('login-password').value;

    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.message || '로그인 실패');

      currentUser = data.user;
      localStorage.setItem('onhouse_user', JSON.stringify(currentUser));
      updateUserUI();
      hideAuthModal();
      loadProperties();
      loadDanggeunDeals();
    } catch (err) {
      errDiv.textContent = err.message;
      errDiv.style.display = 'block';
    }
  });

  // Register Form
  document.getElementById('register-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errDiv = document.getElementById('reg-error');
    errDiv.style.display = 'none';

    const inviteCode = document.getElementById('reg-invite').value.trim();
    const username = document.getElementById('reg-username').value.trim();
    const password = document.getElementById('reg-password').value;
    const name = document.getElementById('reg-name').value.trim();
    const agencyName = document.getElementById('reg-agency').value.trim();

    try {
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ inviteCode, username, password, name, agencyName })
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.message || '회원가입 실패');

      alert('🎉 가입 승인 완료! 등록하신 아이디로 로그인해주세요.');
      switchAuthTab('login');
      document.getElementById('login-username').value = username;
      document.getElementById('login-password').focus();
    } catch (err) {
      errDiv.textContent = err.message;
      errDiv.style.display = 'block';
    }
  });

  // Logout
  document.getElementById('btn-logout').addEventListener('click', () => {
    if (!confirm('로그아웃 하시겠습니까?')) return;
    localStorage.removeItem('onhouse_user');
    currentUser = null;
    updateUserUI();
    location.reload();
  });

  // Refresh
  document.getElementById('btn-refresh').addEventListener('click', () => {
    if (currentViewMode === 'properties') loadProperties();
    else loadDanggeunDeals();
  });

  // View Mode Toggle (내 장부 vs 통합 직거래)
  const toggleView = () => {
    currentViewMode = currentViewMode === 'properties' ? 'danggeun' : 'properties';
    updateViewModeUI();
  };

  const btnViewMode = document.getElementById('btn-view-mode');
  if (btnViewMode) btnViewMode.addEventListener('click', toggleView);

  const btnShowDanggeun = document.getElementById('btn-show-danggeun');
  if (btnShowDanggeun) btnShowDanggeun.addEventListener('click', () => {
    currentViewMode = 'danggeun';
    updateViewModeUI();
  });

  // Customer Manager Button
  const btnCust = document.getElementById('btn-customers-manage');
  if (btnCust) btnCust.addEventListener('click', openCustomerModal);

  const btnNavCust = document.getElementById('nav-tab-customers');
  if (btnNavCust) btnNavCust.addEventListener('click', openCustomerModal);

  const formCust = document.getElementById('customer-form');
  if (formCust) formCust.addEventListener('submit', handleCustomerSubmit);

  const formLog = document.getElementById('contact-log-form');
  if (formLog) formLog.addEventListener('submit', handleContactLogSubmit);

  // Channel Tabs Filter
  document.querySelectorAll('.channel-tab-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
      document.querySelectorAll('.channel-tab-btn').forEach(b => b.classList.remove('active'));
      e.target.classList.add('active');
      currentChannel = e.target.getAttribute('data-channel');
      selectedDealIds.clear();
      updateBulkActionBar();
      if (currentViewMode === 'danggeun') {
        applyFilter();
      } else {
        renderDanggeunList(currentDanggeunDeals);
      }
    });
  });

  // Select All Checkbox
  const chkSelectAll = document.getElementById('check-select-all');
  if (chkSelectAll) {
    chkSelectAll.addEventListener('change', (e) => {
      const isChecked = e.target.checked;
      const filteredDeals = getFilteredDeals();
      if (isChecked) {
        filteredDeals.forEach(d => selectedDealIds.add(d.id));
      } else {
        selectedDealIds.clear();
      }
      updateBulkActionBar();
      renderDanggeunList(currentDanggeunDeals);
    });
  }

  // Bulk Import Button
  const btnBulk = document.getElementById('btn-import-bulk');
  if (btnBulk) btnBulk.addEventListener('click', handleBulkImport);

  // Filter Form Submit
  const formFilter = document.getElementById('filter-form');
  if (formFilter) {
    formFilter.addEventListener('submit', (e) => {
      e.preventDefault();
      applyFilter();
    });
  }

  // Filter Reset
  const btnResetFilter = document.getElementById('btn-reset-filter');
  if (btnResetFilter) {
    btnResetFilter.addEventListener('click', () => {
      if (formFilter) formFilter.reset();
      if (currentViewMode === 'properties') {
        loadProperties();
      } else {
        loadDanggeunDeals();
      }
    });
  }

  // Modal Actions
  const btnAddProp = document.getElementById('btn-add-property');
  if (btnAddProp) btnAddProp.addEventListener('click', () => openAddModal());

   const btnModalClose = document.getElementById('btn-modal-close');
  if (btnModalClose) btnModalClose.addEventListener('click', closeModal);

  const btnModalCancel = document.getElementById('btn-modal-cancel');
  if (btnModalCancel) btnModalCancel.addEventListener('click', closeModal);

  const formProp = document.getElementById('property-form');
  if (formProp) formProp.addEventListener('submit', handleFormSubmit);

  // --- 허위매물/과태료 방지 및 건축물대장 이벤트 ---
  const btnPostcode = document.getElementById('btn-search-postcode');
  if (btnPostcode) btnPostcode.addEventListener('click', openPostcodeSearch);

  const btnLedger = document.getElementById('btn-check-ledger');
  if (btnLedger) btnLedger.addEventListener('click', checkBuildingLedger);

  // 입력 변경 시 실시간 과태료 안심 진단
  ['prop-title', 'prop-address', 'prop-detail-address', 'prop-floor', 'prop-total-floor', 'prop-chk-elevator', 'prop-chk-parking', 'prop-chk-violating', 'prop-secret-memo'].forEach(id => {
    const el = document.getElementById(id);
    if (el) {
      el.addEventListener('input', () => runSafetyAudit());
      el.addEventListener('change', () => runSafetyAudit());
    }
  });

  // 허위매물 확인 팝업 모달 이벤트
  const btnSafetyClose = document.getElementById('btn-safety-modal-close');
  if (btnSafetyClose) btnSafetyClose.addEventListener('click', closeSafetyModal);

  const btnSafetyIgnore = document.getElementById('btn-safety-ignore-save');
  if (btnSafetyIgnore) btnSafetyIgnore.addEventListener('click', () => executeSaveProperty(true));

  const btnSafetyAutofix = document.getElementById('btn-safety-autofix-save');
  if (btnSafetyAutofix) btnSafetyAutofix.addEventListener('click', () => applyAllSafetyFixesAndSave());

  // Tabs in Detail Panel
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
      
      e.target.classList.add('active');
      const targetTab = e.target.getAttribute('data-tab');
      document.getElementById(`tab-${targetTab}`).classList.add('active');
    });
  });

  // Copy Buttons
  document.getElementById('btn-copy-briefing').addEventListener('click', () => {
    const text = document.getElementById('briefing-text').value;
    copyToClipboard(text, '손님 브리핑 문구가 복사되었습니다! 카톡에 바로 붙여넣으세요.');
  });

  document.getElementById('btn-copy-terms').addEventListener('click', () => {
    const text = document.getElementById('terms-text').value;
    copyToClipboard(text, '안전 특약 문구가 복사되었습니다!');
  });
}

// 1. Load Properties (내 장부)
async function loadProperties() {
  if (!currentUser) return;
  setStatus('내 장부 매물 목록을 불러오는 중...');
  try {
    const res = await fetch(`/api/properties?userId=${currentUser.id}`);
    currentProperties = await res.json();
    updateStats(currentProperties);
    renderList(currentProperties);
    loadCustomerCount();
    setStatus(`총 ${currentProperties.length}건의 매물을 불러왔습니다.`);
  } catch (err) {
    console.error(err);
    setStatus('매물 목록을 불러오지 못했습니다.');
  }
}

// 2. Render Property List (내 장부)
function renderList(list) {
  const container = document.getElementById('property-list');
  container.innerHTML = '';
  document.getElementById('list-count').textContent = list.length;

  if (list.length === 0) {
    container.innerHTML = '<div style="padding: 20px; text-align: center; color: #94a3b8;">조건에 맞는 매물이 없습니다.</div>';
    clearDetail();
    return;
  }

  list.forEach(item => {
    const isSelected = selectedProperty && selectedProperty.id === item.id;
    const card = document.createElement('div');
    card.className = `property-card ${isSelected ? 'active' : ''}`;
    card.addEventListener('click', () => selectProperty(item));
    
    // 출처별 뱃지
    let sourceBadge = '';
    const src = item.sourceChannel || '직접등록';
    if (src.includes('피터팬')) {
      sourceBadge = `<span class="badge" style="background: #0284c7; color: white;">🏠 피터팬</span>`;
    } else if (src.includes('당근')) {
      sourceBadge = `<span class="badge" style="background: #ea580c; color: white;">🥕 당근</span>`;
    } else {
      sourceBadge = `<span class="badge" style="background: #334155; color: #cbd5e1;">📋 직접등록</span>`;
    }

    // 상태 배지 컬러 (신규/확인중/생존/이미나감/진행중)
    let statusClass = 'badge-primary';
    let statusStyle = '';
    const st = item.status || '신규';
    if (st === '생존확인' || st === '공실') {
      statusStyle = 'background: #10b981; color: white;';
    } else if (st === '확인중') {
      statusStyle = 'background: #f59e0b; color: white;';
    } else if (st === '이미나감' || st === '계약완료') {
      statusStyle = 'background: #ef4444; color: white; text-decoration: line-through;';
    } else if (st === '거래진행중') {
      statusStyle = 'background: #a855f7; color: white;';
    } else {
      statusStyle = 'background: #3b82f6; color: white;';
    }

    // 손님 매칭 배지
    const custBadge = item.matchedCustomerInfo 
      ? `<span class="badge-customer-match" style="margin-left: 4px;">${escapeHtml(item.matchedCustomerInfo)}</span>` 
      : '';

    card.innerHTML = `
      <div class="card-top">
        <div class="badges" style="flex-wrap: wrap; gap: 4px;">
          ${sourceBadge}
          <span class="badge badge-primary">${escapeHtml(item.propertyType)}</span>
          <span class="badge" style="${statusStyle}">${escapeHtml(st)}</span>
          ${custBadge}
        </div>
        <div class="card-price">${escapeHtml(item.formattedPrice)}</div>
      </div>
      <div class="card-title">${escapeHtml(item.title)}</div>
      <div class="card-address">📍 ${escapeHtml(item.address)} ${escapeHtml(item.detailAddress || '')}</div>
      <div class="card-features">${escapeHtml(item.featuresSummary)}</div>
    `;

    container.appendChild(card);
  });

  if (!selectedProperty && list.length > 0) {
    selectProperty(list[0]);
  }
}

// 3. Select Property (내 장부 상세 뷰)
async function selectProperty(item) {
  selectedProperty = item;
  
  document.querySelectorAll('#property-list .property-card').forEach(c => c.classList.remove('active'));
  const cards = document.getElementById('property-list').children;
  for (let c of cards) {
    if (c.innerHTML.includes(escapeHtml(item.title))) {
      c.classList.add('active');
      break;
    }
  }

  document.getElementById('empty-detail').style.display = 'none';
  document.getElementById('deal-detail').style.display = 'none';
  document.getElementById('active-detail').style.display = 'block';

  let sourceBadge = '';
  const src = item.sourceChannel || '직접등록';
  if (src.includes('피터팬')) {
    sourceBadge = `<span class="badge" style="background: #0284c7; color: white;">🏠 피터팬 직거래 수집</span>`;
  } else if (src.includes('당근')) {
    sourceBadge = `<span class="badge" style="background: #ea580c; color: white;">🥕 당근마켓 직거래 수집</span>`;
  } else if (src.includes('네이버') || src.includes('카페')) {
    sourceBadge = `<span class="badge" style="background: #059669; color: white;">🟢 네이버 카페 직거래 수집</span>`;
  } else {
    sourceBadge = `<span class="badge" style="background: #334155; color: #cbd5e1;">📋 직접 등록 매물</span>`;
  }

  document.getElementById('detail-header-wrap').innerHTML = `
    <div class="detail-header">
      <div class="badges">
        ${sourceBadge}
        <span class="badge badge-primary">${escapeHtml(item.propertyType)}</span>
        <span class="badge" style="background: #3b82f6; color: white;">${escapeHtml(item.status || '신규')}</span>
      </div>
      <div>
        <button class="btn btn-sm btn-secondary" onclick="openEditModal(${item.id})">✏️ 수정</button>
        <button class="btn btn-sm btn-danger" onclick="deleteProperty(${item.id})">🗑️ 삭제</button>
      </div>
    </div>
  `;

  // 상태 칩 하이라이트 동기화
  const currentSt = item.status || '신규';
  document.querySelectorAll('.status-chip').forEach(chip => {
    chip.classList.toggle('active', chip.textContent.includes(currentSt));
  });

  // 손님 매칭 카드 표시
  const matchCard = document.getElementById('matched-customer-card');
  const matchText = document.getElementById('matched-customer-text');
  if (matchCard && matchText) {
    if (item.matchedCustomerInfo) {
      matchText.textContent = item.matchedCustomerInfo;
      matchCard.style.display = 'block';
    } else {
      matchCard.style.display = 'none';
    }
  }

  // 통화 및 컨택 이력 불러오기
  loadContactLogs(item.id);

  document.getElementById('detail-title').textContent = item.title;
  document.getElementById('detail-price').textContent = item.formattedPrice;

  // 내 장부 다중 사진 렌더링
  const imgContainer = document.getElementById('prop-image-container');
  const galleryGrid = document.getElementById('prop-gallery-grid');
  const countSpan = document.getElementById('prop-photo-count');

  if (imgContainer && galleryGrid && countSpan) {
    const photos = item.imageUrl ? item.imageUrl.split(',').filter(u => u && u.startsWith('http')) : [];
    if (photos.length > 0) {
      countSpan.textContent = photos.length;
      galleryGrid.innerHTML = photos.map(url => `
        <div style="flex: 0 0 200px; height: 150px; border-radius: 8px; overflow: hidden; border: 1px solid #334155; background: #0f172a;">
          <img src="${url}" alt="방 사진" style="width: 100%; height: 100%; object-fit: cover; cursor: pointer; transition: transform 0.2s;" onmouseover="this.style.transform='scale(1.05)'" onmouseout="this.style.transform='scale(1)'" onclick="window.open('${url}', '_blank');" />
        </div>
      `).join('');
      imgContainer.style.display = 'flex';
    } else {
      imgContainer.style.display = 'none';
    }
  }

  document.getElementById('spec-address').textContent = `${item.address} ${item.detailAddress || ''}`;
  document.getElementById('spec-type').textContent = `${item.propertyType} (${item.transactionType})`;
  document.getElementById('spec-floor').textContent = `${item.floor}층 / 전체 ${item.totalFloor}층`;
  document.getElementById('spec-area').textContent = `약 ${item.areaPyeong}평 (${item.areaM2}㎡)`;
  document.getElementById('spec-maintenance').textContent = item.maintenanceFee > 0 ? `${item.maintenanceFee}만원` : '없음';

  document.getElementById('secret-owner-name').textContent = item.ownerName || '미등록';
  document.getElementById('secret-owner-phone').textContent = item.ownerPhone || '미등록';
  document.getElementById('secret-memo-text').innerHTML = linkify(item.secretMemo);

  try {
    const res = await fetch('/api/tools/generate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(item)
    });
    const tools = await res.json();
    document.getElementById('briefing-text').value = tools.briefing;
    document.getElementById('terms-text').value = tools.terms;
  } catch (err) {
    console.error(err);
  }
}

// 3-1. Select Deal Detail (직거래 클릭 시 우측 상세 요약 뷰)
function selectDealDetail(deal) {
  selectedDeal = deal;

  document.getElementById('empty-detail').style.display = 'none';
  document.getElementById('active-detail').style.display = 'none';
  const dealDetail = document.getElementById('deal-detail');
  dealDetail.style.display = 'block';

  let badgeColor = '#3b82f6';
  if (deal.authorType.includes('피터팬')) badgeColor = '#0284c7';
  else if (deal.authorType.includes('당근')) badgeColor = '#ea580c';
  else if (deal.authorType.includes('네이버') || deal.authorType.includes('카페')) badgeColor = '#059669';

  document.getElementById('deal-detail-header').innerHTML = `
    <div class="detail-header">
      <div class="badges">
        <span class="badge" style="background: ${badgeColor}; color: white;">${deal.authorType}</span>
        <span class="badge badge-danger">집주인/임차인 직거래</span>
      </div>
    </div>
  `;

  document.getElementById('deal-detail-title').textContent = deal.title;
  document.getElementById('deal-detail-price').textContent = deal.priceDisplay;
  document.getElementById('deal-detail-channel').textContent = deal.authorType;
  document.getElementById('deal-detail-region').textContent = deal.region;
  document.getElementById('deal-detail-author').textContent = deal.authorName;
  document.getElementById('deal-detail-time').textContent = new Date(deal.detectedAt).toLocaleString();
  document.getElementById('deal-detail-desc').textContent = deal.description;
  document.getElementById('deal-detail-desc').style.whiteSpace = 'pre-wrap';
  document.getElementById('deal-detail-desc').style.lineHeight = '1.6';

  // 다중 방 사진 갤러리 렌더링 (실제 수집된 모든 방 사진)
  const imgContainer = document.getElementById('deal-image-container');
  const galleryGrid = document.getElementById('deal-gallery-grid');
  const countSpan = document.getElementById('deal-photo-count');

  const photos = deal.imageUrl ? deal.imageUrl.split(',').filter(u => u && u.startsWith('http')) : [];
  
  if (photos.length > 0) {
    countSpan.textContent = photos.length;
    galleryGrid.innerHTML = photos.map(url => `
      <div style="flex: 0 0 200px; height: 150px; border-radius: 8px; overflow: hidden; border: 1px solid #334155; background: #0f172a;">
        <img src="${url}" alt="방 사진" style="width: 100%; height: 100%; object-fit: cover; cursor: pointer; transition: transform 0.2s;" onmouseover="this.style.transform='scale(1.05)'" onmouseout="this.style.transform='scale(1)'" onclick="window.open('${url}', '_blank');" />
      </div>
    `).join('');
    imgContainer.style.display = 'flex';
  } else {
    imgContainer.style.display = 'none';
  }

  document.getElementById('btn-deal-web-link').href = deal.articleUrl;
  document.getElementById('btn-deal-import-now').onclick = () => importDeal(deal.id);
}

// 4. Update Stats
function updateStats(properties) {
  document.getElementById('stat-total').textContent = properties.length;
  const vacant = properties.filter(p => p.status === '공실').length;
  document.getElementById('stat-vacant').textContent = vacant;
  const monthly = properties.filter(p => p.transactionType === '월세').length;
  const jeonse = properties.filter(p => p.transactionType === '전세').length;
  document.getElementById('stat-monthly').textContent = monthly;
  document.getElementById('stat-jeonse').textContent = jeonse;
}

function clearDetail() {
  selectedProperty = null;
  selectedDeal = null;
  document.getElementById('empty-detail').style.display = 'flex';
  document.getElementById('active-detail').style.display = 'none';
  document.getElementById('deal-detail').style.display = 'none';
}

// 5. Filter Search
async function applyFilter() {
  const query = document.getElementById('search-query').value.trim();
  const propertyType = document.getElementById('filter-property-type').value;
  const transactionType = document.getElementById('filter-trans-type').value;
  const maxDeposit = parseInt(document.getElementById('filter-max-deposit').value) || 0;
  const maxRent = parseInt(document.getElementById('filter-max-rent').value) || 0;
  const status = document.getElementById('filter-status').value;
  const hasParking = document.getElementById('chk-parking').checked;
  const hasElevator = document.getElementById('chk-elevator').checked;
  const allowsPets = document.getElementById('chk-pets').checked;
  const excludeViolating = document.getElementById('chk-exclude-violating').checked;

  if (currentViewMode === 'properties') {
    if (!currentUser) return;
    const payload = {
      searchText: query,
      propertyType: propertyType || "전체",
      transactionType: transactionType || "전체",
      maxDeposit,
      maxMonthlyRent: maxRent,
      status: status || "전체",
      onlyWithParking: hasParking,
      onlyWithElevator: hasElevator,
      onlyPetsAllowed: allowsPets,
      excludeViolatingBuilding: excludeViolating
    };

    setStatus('필터 검색 적용 중...');
    try {
      const res = await fetch(`/api/properties/search?userId=${currentUser.id}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      const results = await res.json();
      currentProperties = results;
      renderList(results);
      setStatus(`검색 결과 ${results.length}건이 발견되었습니다.`);
    } catch (err) {
      console.error(err);
      setStatus('검색 처리 중 오류가 발생했습니다.');
    }
  } else {
    // 실시간 직거래 탐색 필터 검색 (클라이언트 단에서 정교하게 필터링 적용)
    setStatus('직거래 매물 필터 검색 중...');
    let filtered = currentDanggeunDeals;

    // 통합 검색 (제목, 지역, 설명 내용)
    if (query) {
      const q = query.toLowerCase();
      filtered = filtered.filter(d => 
        (d.title && d.title.toLowerCase().includes(q)) ||
        (d.region && d.region.toLowerCase().includes(q)) ||
        (d.description && d.description.toLowerCase().includes(q))
      );
    }

    // 매물 종류 필터링 (원룸, 투룸, 오피스텔, 아파트, 상가/사무실 등)
    if (propertyType && propertyType !== '전체') {
      filtered = filtered.filter(d => {
        const titleLower = (d.title || '').toLowerCase();
        const descLower = (d.description || '').toLowerCase();
        if (propertyType === '원룸') {
          return !titleLower.includes('투룸') && !titleLower.includes('쓰리룸') && !titleLower.includes('오피스텔') && !titleLower.includes('아파트') && !titleLower.includes('상가') && !titleLower.includes('사무실') &&
                 !descLower.includes('투룸') && !descLower.includes('쓰리룸');
        } else if (propertyType === '투룸/쓰리룸') {
          return titleLower.includes('투룸') || titleLower.includes('쓰리룸') || titleLower.includes('2룸') || titleLower.includes('3룸') ||
                 descLower.includes('투룸') || descLower.includes('쓰리룸') || descLower.includes('2룸') || descLower.includes('3룸');
        } else if (propertyType === '오피스텔') {
          return titleLower.includes('오피스텔') || descLower.includes('오피스텔') || titleLower.includes('아파텔') || descLower.includes('아파텔');
        } else if (propertyType === '아파트') {
          return titleLower.includes('아파트') || descLower.includes('아파트');
        } else if (propertyType === '상가/사무실') {
          return titleLower.includes('상가') || titleLower.includes('사무실') || descLower.includes('상가') || descLower.includes('사무실');
        }
        return true;
      });
    }

    // 거래 구분 필터링 (월세, 전세, 매매)
    if (transactionType && transactionType !== '전체') {
      filtered = filtered.filter(d => {
        const isMonthly = d.monthlyRent > 0;
        const isSale = (d.priceDisplay || '').includes('매매');
        if (transactionType === '월세') return isMonthly && !isSale;
        if (transactionType === '전세') return !isMonthly && !isSale;
        if (transactionType === '매매') return isSale;
        return true;
      });
    }

    // 최대 보증금 필터링
    if (maxDeposit > 0) {
      filtered = filtered.filter(d => d.deposit <= maxDeposit);
    }

    // 최대 월세 필터링
    if (maxRent > 0) {
      filtered = filtered.filter(d => d.monthlyRent <= maxRent);
    }

    // 공실 상태 필터링
    if (status && status !== '전체') {
      filtered = filtered.filter(d => d.status === status);
    }

    // 편의 시설 및 조건 분석
    if (hasParking) {
      filtered = filtered.filter(d => {
        const text = ((d.title || '') + ' ' + (d.description || '')).toLowerCase();
        return text.includes('주차') && !text.includes('주차불가') && !text.includes('주차 불가');
      });
    }
    if (hasElevator) {
      filtered = filtered.filter(d => {
        const text = ((d.title || '') + ' ' + (d.description || '')).toLowerCase();
        return text.includes('엘베') || text.includes('엘리베이터') || text.includes('승강기');
      });
    }
    if (allowsPets) {
      filtered = filtered.filter(d => {
        const text = ((d.title || '') + ' ' + (d.description || '')).toLowerCase();
        return text.includes('반려동물') || text.includes('애완') || text.includes('강아지') || text.includes('고양이') || text.includes('반려견');
      });
    }

    // 채널 탭 상태(전체/피터팬/당근) 추가 교차 매칭
    if (currentChannel && currentChannel !== 'all') {
      filtered = filtered.filter(d => d.authorType === (currentChannel === 'peterpan' ? '피터팬' : '당근 직거래'));
    }

    renderDanggeunList(filtered);
    setStatus(`직거래 필터 검색 결과 ${filtered.length}건이 발견되었습니다.`);
  }
}

// 6. Danggeun & Direct Deals Logic
async function loadDanggeunDeals() {
  try {
    const res = await fetch('/api/danggeun/deals');
    currentDanggeunDeals = await res.json();
    document.getElementById('stat-danggeun').textContent = currentDanggeunDeals.length;

    updateChannelCounts(currentDanggeunDeals);

    if (currentViewMode === 'danggeun') {
      renderDanggeunList(currentDanggeunDeals);
    }
  } catch (err) {
    console.error('직거래 데이터 로드 실패:', err);
  }
}

function updateChannelCounts(deals) {
  const elAll = document.getElementById('count-all');
  const elPeter = document.getElementById('count-peterpan');
  const elDaangn = document.getElementById('count-daangn');
  if (elAll) elAll.textContent = deals.length;
  if (elPeter) elPeter.textContent = deals.filter(d => d.authorType.includes('피터팬')).length;
  if (elDaangn) elDaangn.textContent = deals.filter(d => d.authorType.includes('당근')).length;
}

function getFilteredDeals() {
  if (currentChannel === '피터팬') {
    return currentDanggeunDeals.filter(d => d.authorType.includes('피터팬'));
  } else if (currentChannel === '당근') {
    return currentDanggeunDeals.filter(d => d.authorType.includes('당근'));
  }
  return currentDanggeunDeals;
}

function updateBulkActionBar() {
  const countSpan = document.getElementById('selected-count');
  const bulkBtn = document.getElementById('btn-import-bulk');
  const allCheck = document.getElementById('check-select-all');

  const count = selectedDealIds.size;
  countSpan.textContent = count;
  bulkBtn.disabled = count === 0;

  const filtered = getFilteredDeals();
  allCheck.checked = filtered.length > 0 && count === filtered.length;
}

async function handleBulkImport() {
  if (!currentUser || selectedDealIds.size === 0) return;

  const ids = Array.from(selectedDealIds);
  if (!confirm(`선택한 ${ids.length}개의 직거래 매물을 한 번에 내 장부로 등록하시겠습니까?`)) return;

  setStatus(`선택한 매물 ${ids.length}건 일괄 등록 중...`);
  try {
    const res = await fetch('/api/danggeun/import-bulk', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ids, userId: currentUser.id })
    });

    const data = await res.json();
    alert(`🎉 총 ${data.importedCount}건의 직거래 매물이 내 장부에 한 번에 등록되었습니다!`);
    selectedDealIds.clear();
    await loadProperties();
    await loadDanggeunDeals();
    currentViewMode = 'properties';
    updateViewModeUI();
  } catch (err) {
    console.error(err);
    alert('일괄 등록 실패');
  }
}

function switchMainView(mode) {
  currentViewMode = mode;
  updateViewModeUI();
}
window.switchMainView = switchMainView;

function updateViewModeUI() {
  const propList = document.getElementById('property-list');
  const dgList = document.getElementById('danggeun-list');
  const naverWrap = document.getElementById('naver-list-wrap');
  const title = document.getElementById('list-title');
  const indicator = document.getElementById('view-mode-indicator');
  const bulkBar = document.getElementById('bulk-action-bar');
  
  const subBarProps = document.getElementById('sub-bar-properties');
  const subBarDeals = document.getElementById('sub-bar-deals');
  const subBarNaver = document.getElementById('sub-bar-naver');
  const tabProps = document.getElementById('nav-tab-properties');
  const tabDeals = document.getElementById('nav-tab-deals');
  const tabNaver = document.getElementById('nav-tab-naver');

  // Reset tabs
  if (tabProps) tabProps.classList.remove('active');
  if (tabDeals) tabDeals.classList.remove('active');
  if (tabNaver) tabNaver.classList.remove('active');

  // Reset subbars
  if (subBarProps) subBarProps.style.display = 'none';
  if (subBarDeals) subBarDeals.style.display = 'none';
  if (subBarNaver) subBarNaver.style.display = 'none';

  // Reset lists
  if (propList) propList.style.display = 'none';
  if (dgList) dgList.style.display = 'none';
  if (naverWrap) naverWrap.style.display = 'none';
  if (bulkBar) bulkBar.style.display = 'none';

  if (currentViewMode === 'danggeun') {
    if (tabDeals) tabDeals.classList.add('active');
    if (subBarDeals) subBarDeals.style.display = 'flex';
    if (dgList) dgList.style.display = 'flex';
    if (bulkBar) bulkBar.style.display = 'flex';
    title.innerHTML = `📡 실시간 직거래 감지 (<span id="list-count">${currentDanggeunDeals.length}</span>건)`;
    indicator.textContent = '실시간 직거래 모드';
    indicator.className = 'badge badge-primary';
    
    updateChannelCounts(currentDanggeunDeals);
    updateBulkActionBar();
    renderDanggeunList(currentDanggeunDeals);
    setStatus('실시간 직거래 탐색 모드 (카드를 클릭하면 우측에 상세 분석이 표시됩니다)');
  } else if (currentViewMode === 'naver') {
    if (tabNaver) tabNaver.classList.add('active');
    if (subBarNaver) subBarNaver.style.display = 'flex';
    if (naverWrap) naverWrap.style.display = 'flex';
    title.innerHTML = `🟢 네이버 매물 대장 전수 검증 (<span id="list-count">0</span>건)`;
    indicator.textContent = '네이버 대장 검증';
    indicator.className = 'badge badge-success';

    clearDetail();
    loadNaverListings();
    loadRealtorSettings();
    setStatus('네이버 매물 대장 검증 허브 모드 (카드를 클릭하면 우측에 1:1 대조 리포트가 표시됩니다)');
  } else {
    // properties (기본)
    if (tabProps) tabProps.classList.add('active');
    if (subBarProps) subBarProps.style.display = 'flex';
    if (propList) propList.style.display = 'flex';
    title.innerHTML = `📋 내 등록 매물 장부 (<span id="list-count">${currentProperties.length}</span>건)`;
    indicator.textContent = '내 장부';
    indicator.className = 'badge badge-primary';
    
    renderList(currentProperties);
    setStatus(`총 ${currentProperties.length}건의 매물을 불러왔습니다.`);
  }
}

function renderDanggeunList(deals) {
  const container = document.getElementById('danggeun-list');
  container.innerHTML = '';

  let listToRender = deals || currentDanggeunDeals;

  if (currentChannel === '피터팬') {
    listToRender = listToRender.filter(d => d.authorType.includes('피터팬'));
  } else if (currentChannel === '당근') {
    listToRender = listToRender.filter(d => d.authorType.includes('당근'));
  }

  document.getElementById('list-count').textContent = listToRender.length;

  if (listToRender.length === 0) {
    container.innerHTML = `<div style="padding: 30px; text-align: center; color: #94a3b8; font-size: 0.9rem;">
      조건에 해당하는 직거래 매물이 없습니다.<br>
      상단의 <strong>[지금 실시간 크롤링]</strong> 버튼을 눌러보세요!
    </div>`;
    clearDetail();
    return;
  }

  listToRender.forEach(deal => {
    const card = document.createElement('div');
    card.className = 'property-card';
    
    // 채널별 뱃지 컬러
    let badgeClass = 'badge-primary';
    let borderColor = '#2563eb';
    if (deal.authorType.includes('피터팬')) {
      badgeClass = 'badge-info';
      borderColor = '#0284c7';
    } else if (deal.authorType.includes('당근')) {
      badgeClass = 'badge-warning';
      borderColor = '#ea580c';
    } else if (deal.authorType.includes('네이버') || deal.authorType.includes('카페')) {
      badgeClass = 'badge-success';
      borderColor = '#059669';
    }

    card.style.borderColor = borderColor;

    const isSelected = selectedDealIds.has(deal.id);
    const timeAgo = Math.round((new Date() - new Date(deal.detectedAt)) / 60000);
    const timeStr = timeAgo <= 1 ? '방금 전' : `${timeAgo}분 전`;

    // 좌측 카드는 깔끔하게 2줄 요약만 표시
    const shortDesc = deal.description 
      ? deal.description.replace(/📝 \[집주인.*?\]/g, '').replace(/━━━━━━━━━+/g, '').replace(/📍 위치.*?$/gs, '').trim()
      : '실시간 수집 직거래 매물입니다.';
    const snippet = shortDesc.length > 70 ? shortDesc.substring(0, 70) + '...' : shortDesc;

    // 의심 신호 배지 (예: ⚠️ 시세 대비 40% 저렴)
    const signalBadge = deal.suspiciousSignal
      ? `<span class="badge-signal" style="margin-left: 4px;">${escapeHtml(deal.suspiciousSignal)}</span>`
      : '';

    // 손님 매칭 배지 (예: 🙋 김철수 손님 일치)
    const custBadge = deal.matchedCustomerInfo
      ? `<span class="badge-customer-match" style="margin-left: 4px;">${escapeHtml(deal.matchedCustomerInfo)}</span>`
      : '';

    card.innerHTML = `
      <div class="card-top">
        <div class="badges" style="align-items: center; flex-wrap: wrap; gap: 4px;">
          <input type="checkbox" class="deal-checkbox" data-id="${deal.id}" ${isSelected ? 'checked' : ''} style="cursor: pointer; transform: scale(1.2); margin-right: 4px;" />
          <span class="badge ${badgeClass}">${deal.authorType}</span>
          <span class="badge" style="background: #334155; color: #cbd5e1;">⏱️ ${timeStr}</span>
          ${signalBadge}
          ${custBadge}
        </div>
        <div class="card-price">${escapeHtml(deal.priceDisplay)}</div>
      </div>
      <div class="card-title">${escapeHtml(deal.title)}</div>
      <div class="card-address">📍 ${escapeHtml(deal.region)} | 작성자: ${escapeHtml(deal.authorName)}</div>
      <div style="font-size: 0.8rem; color: #cbd5e1; margin: 8px 0; line-height: 1.45; background: #141f31; padding: 8px 10px; border-radius: 6px; border: 1px solid #1e293b;">
        💬 ${escapeHtml(snippet)}
      </div>
      <div style="display: flex; gap: 8px; margin-top: 10px;">
        <button class="btn btn-sm btn-accent" style="flex: 1;" onclick="event.stopPropagation(); importDeal(${deal.id})">📥 내 장부로 가져오기</button>
        <a href="${deal.articleUrl}" target="_blank" class="btn btn-sm btn-secondary" style="text-decoration: none; padding-top: 6px;" onclick="event.stopPropagation();">🔗 원문 보기</a>
      </div>
    `;

    // 카드 클릭 시 우측에 상세 분석 렌더링
    card.addEventListener('click', () => {
      document.querySelectorAll('#danggeun-list .property-card').forEach(c => c.classList.remove('active'));
      card.classList.add('active');
      selectDealDetail(deal);
    });

    card.querySelector('.deal-checkbox').addEventListener('change', (e) => {
      e.stopPropagation();
      if (e.target.checked) {
        selectedDealIds.add(deal.id);
      } else {
        selectedDealIds.delete(deal.id);
      }
      updateBulkActionBar();
    });

    container.appendChild(card);
  });

  if (!selectedDeal && listToRender.length > 0) {
    selectDealDetail(listToRender[0]);
  }
}

// 7. Trigger Crawler
let crawlerPollInterval = null;

function openCrawlerProgressModal() {
  const modal = document.getElementById('modal-crawler-progress');
  if (modal) {
    modal.classList.add('active');
    modal.style.display = 'flex';
    modal.style.zIndex = '99999';
    
    // UI 초기화
    document.getElementById('crawler-spinner').style.display = 'block';
    document.getElementById('crawler-status-title').textContent = '실시간 매물을 안전하게 수집하는 중...';
    document.getElementById('crawler-status-title').style.color = '#38bdf8';
    document.getElementById('crawler-status-desc').textContent = '네이버 카페 및 당근마켓 실매물 수집 패킷을 감지하고 있습니다.';
    document.getElementById('crawler-log-box').innerHTML = '<div>[수집 준비] 연결을 수립하고 있습니다...</div>';
    document.getElementById('crawler-log-box').style.display = 'none'; // 로그 박스 기본 숨김
    
    const toggleBtn = document.getElementById('btn-toggle-crawler-log');
    if (toggleBtn) toggleBtn.textContent = '🔍 상세 수집 로그 보기';
    
    document.getElementById('btn-crawler-progress-close').style.display = 'none';
    document.getElementById('btn-crawler-progress-confirm').style.display = 'none';
  }
}

function closeCrawlerProgressModal() {
  const modal = document.getElementById('modal-crawler-progress');
  if (modal) {
    modal.classList.remove('active');
    modal.style.display = 'none';
  }
  if (crawlerPollInterval) {
    clearInterval(crawlerPollInterval);
    crawlerPollInterval = null;
  }
}

function toggleCrawlerLogs() {
  const logBox = document.getElementById('crawler-log-box');
  const btn = document.getElementById('btn-toggle-crawler-log');
  if (logBox && btn) {
    if (logBox.style.display === 'none') {
      logBox.style.display = 'block';
      btn.textContent = '🙈 상세 수집 로그 숨기기';
      logBox.scrollTop = logBox.scrollHeight;
    } else {
      logBox.style.display = 'none';
      btn.textContent = '🔍 상세 수집 로그 보기';
    }
  }
}

window.openCrawlerProgressModal = openCrawlerProgressModal;
window.closeCrawlerProgressModal = closeCrawlerProgressModal;
window.toggleCrawlerLogs = toggleCrawlerLogs;

async function startCrawlerProgressPolling() {
  if (crawlerPollInterval) clearInterval(crawlerPollInterval);
  
  crawlerPollInterval = setInterval(async () => {
    try {
      const res = await fetch('/api/crawler/status');
      if (!res.ok) return;
      const data = await res.json();
      
      // 로그 박스 내용 갱신
      const logBox = document.getElementById('crawler-log-box');
      if (logBox && data.logs) {
        const logHtml = data.logs.map(log => {
          let color = '#a7f3d0';
          if (log.includes('오류') || log.includes('중단') || log.includes('실패') || log.includes('예외')) color = '#f87171';
          else if (log.includes('완료') || log.includes('성공')) color = '#34d399';
          else if (log.includes('시작')) color = '#60a5fa';
          
          return `<div style="color: ${color}; margin-bottom: 4px;">${escapeHtml(log)}</div>`;
        }).join('');
        
        logBox.innerHTML = logHtml;
        logBox.scrollTop = logBox.scrollHeight; // 항상 최하단으로 자동 스크롤
      }
      
      // 현재 진행 단계 텍스트 갱신
      if (data.currentProgress) {
        document.getElementById('crawler-status-title').textContent = data.currentProgress;
      }
      
      // 백엔드 수집 작업이 종료된 경우 (isCrawling === false)
      if (!data.isCrawling) {
        clearInterval(crawlerPollInterval);
        crawlerPollInterval = null;
        
        // 스피너 숨기고 완료 확인 버튼 활성화
        document.getElementById('crawler-spinner').style.display = 'none';
        document.getElementById('btn-crawler-progress-close').style.display = 'block';
        document.getElementById('btn-crawler-progress-confirm').style.display = 'block';
        
        const lastLog = data.logs[data.logs.length - 1] || '';
        if (lastLog.includes('실패') || lastLog.includes('중단') || lastLog.includes('오류') || lastLog.includes('예외')) {
          document.getElementById('crawler-status-title').textContent = '⚠️ 수집 도중 오류가 발생했습니다.';
          document.getElementById('crawler-status-title').style.color = '#ef4444';
          document.getElementById('crawler-status-desc').textContent = '상세 로그 내용을 확인하신 뒤 새로고침 후 다시 시도해 주세요.';
        } else {
          document.getElementById('crawler-status-title').textContent = '🎉 수집 작업이 성공적으로 완료되었습니다!';
          document.getElementById('crawler-status-title').style.color = '#10b981';
          document.getElementById('crawler-status-desc').textContent = '아래 확인 버튼을 클릭하시면 수집된 최신 매물 목록을 조회합니다.';
        }
      }
    } catch (err) {
      console.error(err);
    }
  }, 1000);
}

// 7. Trigger Crawler
window.triggerCrawler = async function(isClean = false) {
  if (isClean) {
    currentDanggeunDeals = [];
    selectedDealIds.clear();
    renderDanggeunList([]);
    clearDetail();
  }
  const days = document.getElementById('crawl-days-select')?.value || '3';
  
  // 수집 현황 모달 오픈 및 실시간 폴링 시작
  openCrawlerProgressModal();
  startCrawlerProgressPolling();
  
  setStatus(isClean ? `🧹 기존 목록 비우고 최근 ${days}일 직거래 실매물 수집 중...` : `📡 최근 ${days}일 직거래 실매물 수집 중...`);
  try {
    const res = await fetch(`/api/crawler/run?days=${days}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reset: isClean })
    });
    
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      throw new Error(data.message || '네트워크 통신 오류가 발생했습니다.');
    }
    
    const data = await res.json();
    
    await loadDanggeunDeals();
    currentViewMode = 'danggeun';
    updateViewModeUI();

    try {
      if (currentDanggeunDeals.length > 0) {
        const topDeal = currentDanggeunDeals[0];
        showToastNotification('🔔 순수 직거래 매물 수집 완료!', `[${topDeal.authorType}] ${topDeal.title} (${topDeal.priceDisplay})`, '🔔');
      }
    } catch { }

    setStatus(`크롤링 완료! 순수 직거래 매물 총 ${currentDanggeunDeals.length}건 보유`);
  } catch (err) {
    console.error(err);
    setStatus('크롤링 실패');
    
    if (crawlerPollInterval) {
      clearInterval(crawlerPollInterval);
      crawlerPollInterval = null;
    }
    
    document.getElementById('crawler-spinner').style.display = 'none';
    document.getElementById('crawler-status-title').textContent = '⚠️ 수집 도중 오류가 발생했습니다.';
    document.getElementById('crawler-status-title').style.color = '#ef4444';
    document.getElementById('crawler-status-desc').textContent = err.message || '네트워크 오류가 발생했습니다.';
    
    const logBox = document.getElementById('crawler-log-box');
    if (logBox) {
      logBox.innerHTML += `<div style="color: #ef4444; margin-top: 8px; font-weight: bold;">[오류 발생] ${escapeHtml(err.message || '서버와의 통신이 중단되었습니다.')}</div>`;
      logBox.scrollTop = logBox.scrollHeight;
    }
    
    document.getElementById('btn-crawler-progress-close').style.display = 'block';
    document.getElementById('btn-crawler-progress-confirm').style.display = 'block';
  }
};

function showToast(title, message, onClick) {
  const container = document.getElementById('toast-container');
  const toast = document.createElement('div');
  toast.className = 'toast-card';
  toast.innerHTML = `
    <div class="toast-icon">🔔</div>
    <div class="toast-body">
      <div class="toast-title">${escapeHtml(title)}</div>
      <div class="toast-msg">${escapeHtml(message)}</div>
    </div>
  `;

  if (onClick) {
    toast.onclick = () => {
      onClick();
      toast.remove();
    };
  }

  container.appendChild(toast);

  // 알림음 (Web Audio API 띵동)
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.type = 'sine';
    osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
    osc.frequency.setValueAtTime(880, ctx.currentTime + 0.15); // A5
    gain.gain.setValueAtTime(0.1, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.4);
    osc.start();
    osc.stop(ctx.currentTime + 0.4);
  } catch { }

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    setTimeout(() => toast.remove(), 300);
  }, 5000);
}

function showToastNotification(title, message, icon) {
  const container = document.getElementById('toast-container');
  if (!container) return;
  const toast = document.createElement('div');
  toast.className = 'toast-card';
  toast.innerHTML = `
    <div class="toast-icon">${escapeHtml(icon || '🔔')}</div>
    <div class="toast-body">
      <div class="toast-title">${escapeHtml(title)}</div>
      <div class="toast-msg">${escapeHtml(message)}</div>
    </div>
  `;

  container.appendChild(toast);

  // 알림음 (Web Audio API 띵동)
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.type = 'sine';
    osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
    osc.frequency.setValueAtTime(880, ctx.currentTime + 0.15); // A5
    gain.gain.setValueAtTime(0.1, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.4);
    osc.start();
    osc.stop(ctx.currentTime + 0.4);
  } catch { }

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    setTimeout(() => toast.remove(), 300);
  }, 5000);
}

// 8. Import Single Deal
async function importDeal(dealId) {
  if (!currentUser) {
    alert('로그인이 필요합니다.');
    return;
  }
  setStatus('직거래 매물을 내 장부로 등록 중...');
  try {
    const res = await fetch(`/api/danggeun/import/${dealId}?userId=${currentUser.id}`, { method: 'POST' });
    if (!res.ok) throw new Error();
    
    alert('🎉 매물이 내 장부에 성공적으로 등록되었습니다!');
    await loadProperties();
    await loadDanggeunDeals();
    currentViewMode = 'properties';
    updateViewModeUI();
  } catch (err) {
    console.error(err);
    alert('매물 등록에 실패했습니다.');
  }
}

// 9. Add / Edit Modal & CRUD & Safety Guard System
let currentBuildingLedger = null;
let currentSafetyIssues = [];

function openAddModal() {
  document.getElementById('modal-title').textContent = '신규 매물 등록';
  document.getElementById('property-form').reset();
  document.getElementById('prop-id').value = '';
  document.getElementById('prop-status').value = '공실';
  currentBuildingLedger = null;
  resetLedgerCard();
  runSafetyAudit();
  const modal = document.getElementById('property-modal');
  if (modal) {
    modal.classList.add('active');
    modal.style.display = 'flex';
  }
}

function openEditModal(id) {
  const item = currentProperties.find(p => p.id === id);
  if (!item) return;

  document.getElementById('modal-title').textContent = '매물 정보 수정';
  document.getElementById('prop-id').value = item.id;
  document.getElementById('prop-title').value = item.title;
  document.getElementById('prop-property-type').value = item.propertyType;
  document.getElementById('prop-trans-type').value = item.transactionType;
  document.getElementById('prop-deposit').value = item.deposit;
  document.getElementById('prop-rent').value = item.monthlyRent;
  document.getElementById('prop-maintenance').value = item.maintenanceFee;
  document.getElementById('prop-address').value = item.address;
  document.getElementById('prop-detail-address').value = item.detailAddress || '';
  document.getElementById('prop-floor').value = item.floor;
  document.getElementById('prop-total-floor').value = item.totalFloor;
  document.getElementById('prop-area').value = item.areaM2;
  document.getElementById('prop-status').value = item.status;

  document.getElementById('prop-chk-elevator').checked = item.hasElevator;
  document.getElementById('prop-chk-parking').checked = item.hasParking;
  document.getElementById('prop-chk-pets').checked = item.allowsPets;
  document.getElementById('prop-chk-loan').checked = item.isLoanAvailable;
  document.getElementById('prop-chk-violating').checked = item.isViolatingBuilding;

  document.getElementById('prop-owner-name').value = item.ownerName || '';
  document.getElementById('prop-owner-phone').value = item.ownerPhone || '';
  document.getElementById('prop-secret-memo').value = item.secretMemo || '';

  currentBuildingLedger = null;
  resetLedgerCard();
  runSafetyAudit();

  const modal = document.getElementById('property-modal');
  if (modal) {
    modal.classList.add('active');
    modal.style.display = 'flex';
  }
}

function closeModal() {
  const modal = document.getElementById('property-modal');
  if (modal) {
    modal.classList.remove('active');
    modal.style.display = 'none';
  }
  closeSafetyModal();
}

function resetLedgerCard() {
  const card = document.getElementById('ledger-result-card');
  if (card) {
    card.style.display = 'none';
    card.innerHTML = '';
  }
}

// 다음/카카오 우편번호 및 정확한 주소 검색
function openPostcodeSearch() {
  if (typeof daum === 'undefined' || !daum.Postcode) {
    alert('주소 검색 서비스를 불러오는 중입니다. 잠시 후 다시 클릭해주세요.');
    return;
  }

  new daum.Postcode({
    oncomplete: function(data) {
      // 대장 조회에 가장 유리한 지번 주소 우선 선택 (지번이 없으면 도로명)
      const chosenAddr = data.jibunAddress || data.autoJibunAddress || data.roadAddress || data.address;
      const addrInput = document.getElementById('prop-address');
      if (addrInput) {
        addrInput.value = chosenAddr;
      }

      if (data.buildingName && !document.getElementById('prop-detail-address').value) {
        document.getElementById('prop-detail-address').placeholder = `예: ${data.buildingName} 302호`;
      }

      // 주소 선택 즉시 건축물대장 1초 대조 자동 실행!
      checkBuildingLedger();
    }
  }).open();
}

// 건축물대장 1초 조회
async function checkBuildingLedger() {
  const addrInput = document.getElementById('prop-address');
  const btn = document.getElementById('btn-check-ledger');
  const card = document.getElementById('ledger-result-card');
  if (!addrInput || !btn || !card) return;

  const addr = addrInput.value.trim();
  if (!addr) {
    alert('기본 주소를 먼저 입력해주세요.');
    addrInput.focus();
    return;
  }

  const originalBtnText = btn.innerHTML;
  btn.innerHTML = '⏳ 조회 중...';
  btn.disabled = true;

  try {
    const res = await fetch(`/api/ledger/check?address=${encodeURIComponent(addr)}`);
    const data = await res.json();
    currentBuildingLedger = data;

    card.style.display = 'block';
    if (data.success) {
      card.style.background = '#1e1b4b';
      card.style.borderColor = '#6366f1';
      card.innerHTML = `
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
          <span style="font-weight: 700; color: #a5b4fc;">🏛️ 건축물대장 확인 완료 (${escapeHtml(data.buildingName || '건물명 없음')})</span>
          <button type="button" onclick="applyLedgerToForm()" style="background: #4f46e5; color: #fff; border: none; padding: 3px 8px; border-radius: 4px; font-size: 11px; cursor: pointer; font-weight: 600;">
            ✨ 대장 정보 폼에 적용
          </button>
        </div>
        <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 4px; font-size: 11px; color: #cbd5e1;">
          <div>• 지상 층수: <b>${data.grndFlrCnt}층</b> (지하 ${data.ugrndFlrCnt}층)</div>
          <div>• 승강기: <b>${data.totalElevatorCount}대</b> (승용 ${data.rideUseElvtCnt} / 비상 ${data.emgenUseElvtCnt})</div>
          <div>• 주차 대수: <b>${data.totalParking}대</b></div>
          <div>• 위반건축물: <b>${data.isViolatingBuilding ? '🚨 위반건축물 등재됨' : '✅ 정상 건축물'}</b></div>
          <div style="grid-column: span 2;">• 용도: ${escapeHtml(data.mainPurps || '미지정')} | 승인일: ${data.useApprovalDate || '-'}</div>
        </div>
      `;
    } else {
      card.style.background = '#312e81';
      card.style.borderColor = '#4338ca';
      card.innerHTML = `
        <div style="color: #cbd5e1;">
          ℹ️ <b>건축물대장 안내:</b> ${escapeHtml(data.message || '건축물 정보를 찾을 수 없습니다.')}<br>
          <span style="color: #94a3b8; font-size: 11px;">※ 지번(번지) 형태(예: 신림동 1432-15)를 확인하세요. 자체 오기 방지 시스템이 실시간 가동 중입니다.</span>
        </div>
      `;
    }
    runSafetyAudit();
  } catch (err) {
    console.error(err);
    card.style.display = 'block';
    card.innerHTML = `<span style="color: #f87171;">⚠️ 건축물대장 연결 중 일시적 오류가 발생했습니다. (자체 논리 검증 시스템으로 보호 중)</span>`;
    runSafetyAudit();
  } finally {
    btn.innerHTML = originalBtnText;
    btn.disabled = false;
  }
}

window.applyLedgerToForm = function() {
  if (!currentBuildingLedger || !currentBuildingLedger.success) return;
  const data = currentBuildingLedger;

  if (data.grndFlrCnt > 0) {
    document.getElementById('prop-total-floor').value = data.grndFlrCnt;
  }
  document.getElementById('prop-chk-elevator').checked = data.hasElevator;
  document.getElementById('prop-chk-violating').checked = data.isViolatingBuilding;
  if (data.totalParking > 0) {
    document.getElementById('prop-chk-parking').checked = true;
  }

  runSafetyAudit();
  alert('🏛️ 건축물대장 정보(총 층수, 승강기 유무, 위반건축물 여부)가 폼에 동기화되었습니다!');
};

// 실시간 허위매물/과태료 방지 사전 진단
function runSafetyAudit() {
  const title = document.getElementById('prop-title')?.value || '';
  const detailAddr = document.getElementById('prop-detail-address')?.value || '';
  const floor = parseInt(document.getElementById('prop-floor')?.value) || 1;
  const totalFloor = parseInt(document.getElementById('prop-total-floor')?.value) || 5;
  const hasElevator = document.getElementById('prop-chk-elevator')?.checked || false;
  const hasParking = document.getElementById('prop-chk-parking')?.checked || false;
  const isViolating = document.getElementById('prop-chk-violating')?.checked || false;
  const memo = document.getElementById('prop-secret-memo')?.value || '';

  const issues = [];
  const fullText = `${title} ${detailAddr} ${memo}`.toLowerCase();

  // 1. 호수 vs 해당 층수 교차 검증 (예: 302호 -> 3층)
  const hoMatch = detailAddr.match(/([1-9]\d{2,3})\s*호?/);
  if (hoMatch) {
    const hoNum = parseInt(hoMatch[1]);
    const expectedFloor = Math.floor(hoNum / 100);
    if (expectedFloor > 0 && expectedFloor <= 50 && floor !== expectedFloor) {
      issues.push({
        field: 'floor',
        type: 'Danger',
        title: '🚨 층수 불일치 (허위매물 과태료 주의)',
        message: `상세주소는 '${hoNum}호'인데 해당 층은 '${floor}층'으로 입력되었습니다!`,
        suggestedText: `${expectedFloor}층으로 변경`,
        fixAction: 'fix_floor',
        fixValue: expectedFloor
      });
    }
  }

  // 지하/반지하 층수 검증
  if ((detailAddr.includes('b0') || detailAddr.includes('지하') || detailAddr.includes('반지하')) && floor > 0) {
    issues.push({
      field: 'floor',
      type: 'Danger',
      title: '🚨 반지하/지하 층수 오기',
      message: `호실명에 '지하/반지하'가 있으나 해당 층이 지상(${floor}층)으로 설정되어 있습니다.`,
      suggestedText: `지하 1층(-1)으로 변경`,
      fixAction: 'fix_floor',
      fixValue: -1
    });
  }

  // 2. 층수 논리 모순 (해당 층 > 전체 층)
  if (floor > totalFloor && floor > 0) {
    issues.push({
      field: 'totalFloor',
      type: 'Danger',
      title: '❌ 층수 논리 모순 차단',
      message: `해당 층(${floor}층)이 전체 층(${totalFloor}층)보다 높습니다.`,
      suggestedText: `전체 층을 ${floor}층으로 변경`,
      fixAction: 'fix_total_floor',
      fixValue: floor
    });
  }

  // 3. 본문 ↔ 옵션 체크박스 불일치 (과태료 1위 원인)
  const mentionsElevator = fullText.includes('엘베') || fullText.includes('엘리베이터') || fullText.includes('승강기') || fullText.includes('ev');
  const mentionsNoElevator = fullText.includes('엘베 없음') || fullText.includes('엘베없음') || fullText.includes('엘베x') || fullText.includes('계단이용');

  if (mentionsElevator && !mentionsNoElevator && !hasElevator) {
    issues.push({
      field: 'hasElevator',
      type: 'Warning',
      title: '⚠️ 엘리베이터 누락 주의 (표시광고 위반)',
      message: "본문(제목/메모)에 '엘베'가 언급되었으나 옵션에 '엘베 없음'으로 체크되어 있습니다.",
      suggestedText: '엘리베이터 있음으로 체크',
      fixAction: 'fix_elevator',
      fixValue: true
    });
  } else if (mentionsNoElevator && hasElevator) {
    issues.push({
      field: 'hasElevator',
      type: 'Danger',
      title: '🚨 엘리베이터 오기 (허위광고 과태료)',
      message: "본문에는 '엘베 없음/계단'이라 적혔으나 옵션에 '엘리베이터 있음'으로 체크되어 있습니다.",
      suggestedText: '엘리베이터 없음으로 해제',
      fixAction: 'fix_elevator',
      fixValue: false
    });
  }

  // 주차 불일치
  const mentionsNoParking = fullText.includes('주차 불가') || fullText.includes('주차불가') || fullText.includes('주차x');
  if (mentionsNoParking && hasParking) {
    issues.push({
      field: 'hasParking',
      type: 'Warning',
      title: '⚠️ 주차 정보 불일치',
      message: "본문에 '주차 불가'로 적혔으나 옵션에 '주차 가능'으로 체크되어 있습니다.",
      suggestedText: '주차 불가로 해제',
      fixAction: 'fix_parking',
      fixValue: false
    });
  }

  // 4. 건축물대장 데이터 교차 대조 (조회된 경우)
  if (currentBuildingLedger && currentBuildingLedger.success) {
    const l = currentBuildingLedger;
    if (l.hasElevator && !hasElevator) {
      issues.push({
        field: 'hasElevator',
        type: 'Warning',
        title: '🏛️ 건축물대장 불일치 (승강기)',
        message: `건축물대장상 승강기(${l.totalElevatorCount}대) 완비 건물이나 옵션에 '엘베 없음'으로 되어 있습니다.`,
        suggestedText: '대장 승강기 적용 (있음)',
        fixAction: 'fix_elevator',
        fixValue: true
      });
    } else if (!l.hasElevator && hasElevator) {
      issues.push({
        field: 'hasElevator',
        type: 'Danger',
        title: '🚨 건축물대장 위반 (허위 승강기)',
        message: "건축물대장상 승강기가 없는 건물인데 '엘리베이터 있음'으로 체크되었습니다. 과태료 대상입니다.",
        suggestedText: '대장 승강기 적용 (없음)',
        fixAction: 'fix_elevator',
        fixValue: false
      });
    }

    if (l.grndFlrCnt > 0 && totalFloor !== l.grndFlrCnt) {
      issues.push({
        field: 'totalFloor',
        type: 'Warning',
        title: '🏛️ 대장 총 층수 불일치',
        message: `건축물대장상 총 지상 층수는 ${l.grndFlrCnt}층입니다. (입력값: ${totalFloor}층)`,
        suggestedText: `총 층수를 ${l.grndFlrCnt}층으로 변경`,
        fixAction: 'fix_total_floor',
        fixValue: l.grndFlrCnt
      });
    }

    if (l.isViolatingBuilding && !isViolating) {
      issues.push({
        field: 'isViolatingBuilding',
        type: 'Danger',
        title: '🚨 위반건축물 미고지 (치명적 과태료)',
        message: "건축물대장에 '위반건축물'로 등재된 건물입니다! 미표시 시 행정처분 및 과태료가 부과됩니다.",
        suggestedText: '위반건축물로 체크',
        fixAction: 'fix_violating',
        fixValue: true
      });
    }
  }

  currentSafetyIssues = issues;
  renderSafetyGuardUI(issues);
}

function renderSafetyGuardUI(issues) {
  const badge = document.getElementById('safety-guard-badge');
  const container = document.getElementById('safety-guard-issues');
  if (!badge || !container) return;

  if (issues.length === 0) {
    badge.textContent = '✅ 입력 정상 (과태료 위험 없음)';
    badge.style.background = '#065f46';
    badge.style.color = '#34d399';
    container.innerHTML = `<span style="color: #10b981;">✓ 층수, 승강기 유무, 옵션 및 대장 정보가 완벽하게 일치합니다. 안전하게 등록 가능합니다.</span>`;
    return;
  }

  const dangerCount = issues.filter(i => i.type === 'Danger').length;
  if (dangerCount > 0) {
    badge.textContent = `🚨 과태료 위험 ${dangerCount}건 감지`;
    badge.style.background = '#991b1b';
    badge.style.color = '#fecaca';
  } else {
    badge.textContent = `⚠️ 주의 요망 ${issues.length}건`;
    badge.style.background = '#92400e';
    badge.style.color = '#fde68a';
  }

  container.innerHTML = issues.map((issue, idx) => `
    <div style="background: ${issue.type === 'Danger' ? 'rgba(239, 68, 68, 0.15)' : 'rgba(245, 158, 11, 0.15)'}; border-left: 3px solid ${issue.type === 'Danger' ? '#ef4444' : '#f59e0b'}; padding: 8px 10px; margin-top: 6px; border-radius: 0 4px 4px 0; display: flex; justify-content: space-between; align-items: center; gap: 10px;">
      <div>
        <div style="font-weight: 700; color: ${issue.type === 'Danger' ? '#fca5a5' : '#fde68a'}; font-size: 11px;">
          ${escapeHtml(issue.title)}
        </div>
        <div style="color: #cbd5e1; font-size: 11px; margin-top: 2px;">
          ${escapeHtml(issue.message)}
        </div>
      </div>
      ${issue.fixAction ? `
        <button type="button" onclick="applySingleFix(${idx})" style="white-space: nowrap; background: ${issue.type === 'Danger' ? '#dc2626' : '#d97706'}; color: #fff; border: none; padding: 4px 9px; border-radius: 4px; font-size: 11px; cursor: pointer; font-weight: 600;">
          ✨ ${escapeHtml(issue.suggestedText || '수정')}
        </button>
      ` : ''}
    </div>
  `).join('');
}

window.applySingleFix = function(idx) {
  const issue = currentSafetyIssues[idx];
  if (!issue) return;
  executeFix(issue);
  runSafetyAudit();
};

function executeFix(issue) {
  if (issue.fixAction === 'fix_floor') {
    document.getElementById('prop-floor').value = issue.fixValue;
  } else if (issue.fixAction === 'fix_total_floor') {
    document.getElementById('prop-total-floor').value = issue.fixValue;
  } else if (issue.fixAction === 'fix_elevator') {
    document.getElementById('prop-chk-elevator').checked = Boolean(issue.fixValue);
  } else if (issue.fixAction === 'fix_parking') {
    document.getElementById('prop-chk-parking').checked = Boolean(issue.fixValue);
  } else if (issue.fixAction === 'fix_violating') {
    document.getElementById('prop-chk-violating').checked = Boolean(issue.fixValue);
  }
}

// 2중 잠금 모달 열기/닫기
function showSafetyConfirmModal(issues) {
  const modal = document.getElementById('safety-confirm-modal');
  const list = document.getElementById('safety-confirm-list');
  if (!modal || !list) return;

  list.innerHTML = issues.map((issue, idx) => `
    <div style="background: #27272a; border-left: 4px solid ${issue.type === 'Danger' ? '#ef4444' : '#f59e0b'}; padding: 8px 12px; border-radius: 4px;">
      <div style="font-weight: 700; color: ${issue.type === 'Danger' ? '#fca5a5' : '#fde68a'}; font-size: 12px;">
        ${escapeHtml(issue.title)}
      </div>
      <div style="color: #e4e4e7; font-size: 12px; margin-top: 3px;">
        ${escapeHtml(issue.message)}
      </div>
      ${issue.suggestedText ? `<div style="color: #34d399; font-size: 11px; margin-top: 3px;">👉 추천 수정: <b>${escapeHtml(issue.suggestedText)}</b></div>` : ''}
    </div>
  `).join('');

  modal.style.display = 'flex';
}

function closeSafetyModal() {
  const modal = document.getElementById('safety-confirm-modal');
  if (modal) modal.style.display = 'none';
}

// 원클릭 일괄 자동 수정 후 즉시 저장
function applyAllSafetyFixesAndSave() {
  currentSafetyIssues.forEach(issue => executeFix(issue));
  closeSafetyModal();
  runSafetyAudit();
  executeSaveProperty(false);
}

// Form 제출 가로채기
async function handleFormSubmit(e) {
  e.preventDefault();
  if (!currentUser) return;

  runSafetyAudit();

  // 위험 또는 경고 이슈가 남아 있으면 확인 모달 팝업으로 방어!
  if (currentSafetyIssues.length > 0) {
    showSafetyConfirmModal(currentSafetyIssues);
    return;
  }

  await executeSaveProperty(false);
}

// 실제 저장 실행 함수
async function executeSaveProperty(forceIgnoreSafety) {
  if (!currentUser) return;
  closeSafetyModal();

  const idStr = document.getElementById('prop-id').value;
  const isEdit = Boolean(idStr);

  const payload = {
    id: isEdit ? parseInt(idStr) : 0,
    userId: currentUser.id,
    title: document.getElementById('prop-title').value.trim(),
    propertyType: document.getElementById('prop-property-type').value,
    transactionType: document.getElementById('prop-trans-type').value,
    deposit: parseInt(document.getElementById('prop-deposit').value) || 0,
    monthlyRent: parseInt(document.getElementById('prop-rent').value) || 0,
    maintenanceFee: parseInt(document.getElementById('prop-maintenance').value) || 0,
    address: document.getElementById('prop-address').value.trim(),
    detailAddress: document.getElementById('prop-detail-address').value.trim(),
    floor: parseInt(document.getElementById('prop-floor').value) || 1,
    totalFloor: parseInt(document.getElementById('prop-total-floor').value) || 5,
    areaM2: parseFloat(document.getElementById('prop-area').value) || 23.0,
    status: document.getElementById('prop-status').value,
    hasElevator: document.getElementById('prop-chk-elevator').checked,
    hasParking: document.getElementById('prop-chk-parking').checked,
    allowsPets: document.getElementById('prop-chk-pets').checked,
    isLoanAvailable: document.getElementById('prop-chk-loan').checked,
    isViolatingBuilding: document.getElementById('prop-chk-violating').checked,
    ownerName: document.getElementById('prop-owner-name').value.trim(),
    ownerPhone: document.getElementById('prop-owner-phone').value.trim(),
    secretMemo: document.getElementById('prop-secret-memo').value.trim()
  };

  try {
    const url = isEdit ? `/api/properties/${payload.id}?userId=${currentUser.id}` : `/api/properties?userId=${currentUser.id}`;
    const method = isEdit ? 'PUT' : 'POST';

    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!res.ok) throw new Error();

    closeModal();
    await loadProperties();
    setStatus(isEdit ? '매물 정보가 수정되었습니다.' : '신규 매물이 등록되었습니다.');
    if (forceIgnoreSafety) {
      showToastNotification('⚠️ 등록 완료', '과태료 주의 항목이 포함된 상태로 저장되었습니다.', '⚠️');
    } else {
      showToastNotification('🛡️ 안심 매물 등록 완료', '허위매물 검증을 무사히 통과하여 안전하게 저장되었습니다!', '✅');
    }
  } catch (err) {
    console.error(err);
    alert('저장에 실패했습니다. 필수 항목을 확인해주세요.');
  }
}

async function deleteProperty(id) {
  if (!confirm('정말 이 매물을 삭제하시겠습니까? (삭제 후 복구 불가)')) return;

  try {
    const res = await fetch(`/api/properties/${id}?userId=${currentUser.id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error();
    
    await loadProperties();
    setStatus('매물이 삭제되었습니다.');
  } catch (err) {
    console.error(err);
    alert('매물 삭제에 실패했습니다.');
  }
}

// Utils
function setStatus(msg) {
  document.getElementById('status-msg').textContent = msg;
}

function escapeHtml(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function linkify(text) {
  if (!text) return '(작성된 비밀 메모가 없습니다.)';
  // XSS 방지를 위해 기본 이스케이프 선진행
  let escaped = escapeHtml(text);
  
  // 개행 문자 (\n)를 <br>로 변환
  escaped = escaped.replace(/\r?\n/g, '<br>');

  // http:// 또는 https:// 로 시작하는 URL 감지용 정규식
  const urlRegex = /(https?:\/\/[^\s<]+)/g;
  return escaped.replace(urlRegex, (url) => {
    let cleanUrl = url;
    let suffix = '';
    // URL 끝에 문장부호가 포함된 경우 잘라내기
    if (/[.,;:!?]$/.test(cleanUrl)) {
      suffix = cleanUrl.substring(cleanUrl.length - 1);
      cleanUrl = cleanUrl.substring(0, cleanUrl.length - 1);
    }
    return `<a href="${cleanUrl}" target="_blank" style="color: #38bdf8; text-decoration: underline; font-weight: 700; word-break: break-all;">${cleanUrl}</a>${suffix}`;
  });
}

function copyToClipboard(text, successMsg) {
  if (!text) return;
  navigator.clipboard.writeText(text).then(() => {
    alert(successMsg);
  }).catch(() => {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand('copy');
    document.body.removeChild(textarea);
    alert(successMsg);
  });
}

// =========================================================
// ⚡ 매물 상태 즉시 변경
// =========================================================
async function updateCurrentPropertyStatus(newStatus) {
  if (!selectedProperty) return;

  try {
    const res = await fetch(`/api/properties/${selectedProperty.id}/status?userId=${currentUser ? currentUser.id : 1}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status: newStatus })
    });

    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      throw new Error(data.message || '매물 상태 변경에 실패했습니다.');
    }

    selectedProperty.status = newStatus;
    document.querySelectorAll('.status-chip').forEach(chip => {
      chip.classList.toggle('active', chip.textContent.includes(newStatus));
    });

    // 목록 리프레시
    await loadProperties();
    showToastNotification('⚡ 매물 상태 변경', `매물 상태가 [${newStatus}]로 업데이트되었습니다.`, '⚡');
  } catch (err) {
    console.error(err);
    alert(err.message || '매물 상태 변경에 실패했습니다.');
  }
}

// =========================================================
// 📞 통화 및 컨택 이력 관리
// =========================================================
function openAddContactLogModal() {
  const prop = selectedProperty;
  if (!prop) {
    alert('먼저 좌측 목록에서 기록을 남길 매물을 선택해주세요.');
    return;
  }
  const modal = document.getElementById('modal-add-contact-log');
  if (modal) {
    const memoEl = document.getElementById('log-memo');
    if (memoEl) memoEl.value = '';
    modal.classList.add('active');
    modal.style.display = 'flex';
    modal.style.zIndex = '99999';
  }
}

function closeAddContactLogModal() {
  const modal = document.getElementById('modal-add-contact-log');
  if (modal) {
    modal.classList.remove('active');
    modal.style.display = 'none';
  }
}

async function handleContactLogSubmit(e) {
  e.preventDefault();
  if (!selectedProperty) return;

  const contactType = document.getElementById('log-contact-type').value;
  const result = document.getElementById('log-result').value;
  const memo = document.getElementById('log-memo').value.trim();

  try {
    const res = await fetch(`/api/properties/${selectedProperty.id}/contact-logs?userId=${currentUser ? currentUser.id : 1}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        propertyId: selectedProperty.id,
        contactType,
        result,
        memo
      })
    });

    if (!res.ok) throw new Error();

    closeAddContactLogModal();
    await loadContactLogs(selectedProperty.id);
    await loadProperties();
    showToastNotification('📞 통화 기록 완료', `[${result}] 메모가 성공적으로 기록되었습니다.`, '📞');
  } catch (err) {
    console.error(err);
    alert('통화 기록 저장에 실패했습니다.');
  }
}

async function loadContactLogs(propertyId) {
  const container = document.getElementById('contact-log-timeline');
  const countSpan = document.getElementById('contact-log-count');
  if (!container) return;

  try {
    const res = await fetch(`/api/properties/${propertyId}/contact-logs`);
    const logs = await res.json();
    if (countSpan) countSpan.textContent = logs.length;

    if (!logs || logs.length === 0) {
      container.innerHTML = '<div style="color: #64748b; font-size: 0.8rem; text-align: center; padding: 10px;">아직 기록된 통화/컨택 이력이 없습니다.</div>';
      return;
    }

    container.innerHTML = logs.map(l => {
      const d = new Date(l.contactDate);
      const dateStr = `${d.getMonth()+1}/${d.getDate()} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
      
      let resColor = '#3b82f6';
      if (l.result === '생존확인') resColor = '#10b981';
      else if (l.result === '이미나감') resColor = '#ef4444';
      else if (l.result === '통화중/부재') resColor = '#f59e0b';
      else if (l.result === '거래진행') resColor = '#a855f7';

      return `
        <div class="contact-item">
          <div class="contact-meta">
            <span>${escapeHtml(l.contactType)} · ${dateStr}</span>
            <span class="contact-result-badge" style="background: ${resColor}; color: white;">${escapeHtml(l.result)}</span>
          </div>
          <div class="contact-memo">${escapeHtml(l.memo)}</div>
        </div>
      `;
    }).join('');
  } catch (err) {
    console.error(err);
  }
}

// =========================================================
// 👥 손님 조건 관리 & 실시간 매칭
// =========================================================
async function openCustomerModal() {
  const modal = document.getElementById('modal-customer-manager');
  if (modal) {
    modal.classList.add('active');
    modal.style.display = 'flex';
    modal.style.zIndex = '99999';
  }
  await loadCustomers();
}

function closeCustomerModal() {
  const modal = document.getElementById('modal-customer-manager');
  if (modal) {
    modal.classList.remove('active');
    modal.style.display = 'none';
  }
}

// 전역 노출 보장 (HTML onclick에서 즉시 호출 가능)
window.openCustomerModal = openCustomerModal;
window.closeCustomerModal = closeCustomerModal;
window.openAddContactLogModal = openAddContactLogModal;
window.closeAddContactLogModal = closeAddContactLogModal;
window.openAddModal = openAddModal;
window.openEditModal = openEditModal;
window.closeModal = closeModal;
window.switchMainView = switchMainView;

async function loadCustomerCount() {
  const badgeSpan = document.getElementById('customer-count-badge');
  if (!badgeSpan || !currentUser) return;
  try {
    const res = await fetch(`/api/customers?userId=${currentUser.id}`);
    const customers = await res.json();
    badgeSpan.textContent = customers.length;
  } catch {}
}

async function loadCustomers() {
  const container = document.getElementById('customer-list-container');
  const countSpan = document.getElementById('customer-list-count');
  const badgeSpan = document.getElementById('customer-count-badge');

  try {
    const res = await fetch(`/api/customers?userId=${currentUser ? currentUser.id : 1}`);
    const customers = await res.json();

    if (countSpan) countSpan.textContent = customers.length;
    if (badgeSpan) badgeSpan.textContent = customers.length;

    if (!customers || customers.length === 0) {
      container.innerHTML = '<div style="color: #94a3b8; text-align: center; padding: 20px;">등록된 손님이 없습니다. 위에서 손님 조건을 등록해보세요!</div>';
      return;
    }

    container.innerHTML = customers.map(c => `
      <div class="customer-card-item">
        <div>
          <div class="customer-info-title">
            👤 ${escapeHtml(c.customerName)} 
            <span style="font-size: 0.8rem; color: #94a3b8; font-weight: normal; margin-left: 6px;">📞 ${escapeHtml(c.customerPhone || '연락처 없음')}</span>
          </div>
          <div class="customer-conditions">
            🎯 [${escapeHtml(c.targetRegion || '전체')}] ${escapeHtml(c.propertyType)} / ${escapeHtml(c.transactionType)} (보증금 ~${c.maxDeposit}만, 월세 ~${c.maxMonthlyRent}만)
          </div>
          ${c.memo ? `<div class="customer-memo-tag">📝 ${escapeHtml(c.memo)}</div>` : ''}
        </div>
        <button class="btn btn-xs btn-danger" onclick="deleteCustomer(${c.id})">🗑️ 삭제</button>
      </div>
    `).join('');
  } catch (err) {
    console.error(err);
  }
}

async function handleCustomerSubmit(e) {
  e.preventDefault();

  const customerName = document.getElementById('cust-name').value.trim();
  const customerPhone = document.getElementById('cust-phone').value.trim();
  const propertyType = document.getElementById('cust-prop-type').value;
  const transactionType = document.getElementById('cust-trans-type').value;
  const maxDeposit = parseInt(document.getElementById('cust-max-deposit').value) || 10000;
  const maxMonthlyRent = parseInt(document.getElementById('cust-max-rent').value) || 100;
  const targetRegion = document.getElementById('cust-region').value.trim();
  const memo = document.getElementById('cust-memo').value.trim();

  try {
    const res = await fetch(`/api/customers?userId=${currentUser ? currentUser.id : 1}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        customerName,
        customerPhone,
        propertyType,
        transactionType,
        minDeposit: 0,
        maxDeposit,
        maxMonthlyRent,
        targetRegion,
        memo,
        isActive: true
      })
    });

    if (!res.ok) throw new Error();

    document.getElementById('cust-name').value = '';
    document.getElementById('cust-phone').value = '';
    document.getElementById('cust-memo').value = '';

    await loadCustomers();
    await loadProperties();
    showToastNotification('👥 손님 등록 완료', `[${customerName}] 손님 조건이 등록되어 실시간 매칭이 활성화되었습니다!`, '👥');
  } catch (err) {
    console.error(err);
    alert('손님 등록에 실패했습니다.');
  }
}

async function deleteCustomer(id) {
  if (!confirm('해당 손님 정보를 삭제하시겠습니까?')) return;
  try {
    const res = await fetch(`/api/customers/${id}?userId=${currentUser ? currentUser.id : 1}`, { method: 'DELETE' });
    if (!res.ok) throw new Error();
    await loadCustomers();
    await loadProperties();
  } catch (err) {
    console.error(err);
    alert('손님 삭제 실패');
  }
}

// ==========================================
// 8. 네이버 매물 관리 & 건축물대장 전수 대조 분석
// ==========================================
window._lastNaverInspection = null;
let currentNaverListings = [];
let selectedNaverListingId = null;
let selectedNaverArticleIds = new Set();
let currentNaverFilter = 'all';

// 비밀번호 표시/숨김 토글
window.togglePasswordVisibility = function(id, btn) {
  const el = document.getElementById(id);
  if (!el) return;
  if (el.type === 'password') {
    el.type = 'text';
    btn.textContent = '🔒';
  } else {
    el.type = 'password';
    btn.textContent = '👁️';
  }
};

// 탭 전환: 'login' | 'bulk' | 'url'
window.switchNaverModalTab = function(tab) {
  const btnLogin = document.getElementById('tab-naver-mode-login');
  const btnBulk = document.getElementById('tab-naver-mode-bulk');
  const btnUrl = document.getElementById('tab-naver-mode-url');

  const paneLogin = document.getElementById('naver-tab-content-login');
  const paneBulk = document.getElementById('naver-tab-content-bulk');
  const paneUrl = document.getElementById('naver-tab-content-url');

  if (btnLogin) {
    btnLogin.style.background = tab === 'login' ? '#03c75a' : '#1e293b';
    btnLogin.style.color = tab === 'login' ? '#fff' : '#94a3b8';
  }
  if (btnBulk) {
    btnBulk.style.background = tab === 'bulk' ? '#03c75a' : '#1e293b';
    btnBulk.style.color = tab === 'bulk' ? '#fff' : '#94a3b8';
  }
  if (btnUrl) {
    btnUrl.style.background = tab === 'url' ? '#03c75a' : '#1e293b';
    btnUrl.style.color = tab === 'url' ? '#fff' : '#94a3b8';
  }

  if (paneLogin) paneLogin.style.display = tab === 'login' ? 'block' : 'none';
  if (paneBulk) paneBulk.style.display = tab === 'bulk' ? 'block' : 'none';
  if (paneUrl) paneUrl.style.display = tab === 'url' ? 'block' : 'none';
};

// 모달 열기/닫기
window.openNaverInspectModal = function(tab = 'login') {
  const modal = document.getElementById('naver-inspect-modal');
  if (modal) {
    modal.style.display = 'flex';
    switchNaverModalTab(tab);
    loadRealtorSettings();
    if (tab === 'login') {
      loadModalListingTable();
    }
  }
};

window.closeNaverInspectModal = function() {
  const modal = document.getElementById('naver-inspect-modal');
  if (modal) {
    modal.style.display = 'none';
  }
};

window.openRealtorSettingsModal = function() {
  openNaverInspectModal('login');
};

window.openNaverBulkModal = function() {
  openNaverInspectModal('bulk');
};

// 중개사 네이버 계정 정보 불러오기
window.loadRealtorSettings = async function() {
  const uId = currentUser ? currentUser.id : 1;
  try {
    const res = await fetch(`/api/naver/realtor/settings?userId=${uId}`);
    if (!res.ok) return;
    const settings = await res.json();
    if (settings) {
      const idEl = document.getElementById('naver-login-id');
      const pwEl = document.getElementById('naver-login-pw');
      const agencyEl = document.getElementById('naver-login-agency');
      const realtorIdEl = document.getElementById('naver-login-realtor-id');
      const subAgencyEl = document.getElementById('sub-naver-agency');

      if (idEl && settings.naverId) idEl.value = settings.naverId;
      if (pwEl && settings.naverPassword) pwEl.value = settings.naverPassword;
      if (agencyEl && settings.agencyName) agencyEl.value = settings.agencyName;
      if (realtorIdEl && settings.realtorId) realtorIdEl.value = settings.realtorId;

      if (subAgencyEl) {
        if (settings.agencyName) {
          subAgencyEl.textContent = settings.agencyName;
        } else if (settings.naverId) {
          subAgencyEl.textContent = settings.naverId;
        } else {
          subAgencyEl.textContent = '미연동 (클릭하여 로그인)';
        }
      }
    }
  } catch (err) {
    console.error('loadRealtorSettings error:', err);
  }
};

// 중개사 네이버 계정 정보 로컬 SQLite 저장
window.saveRealtorSettings = async function(silent = false) {
  const uId = currentUser ? currentUser.id : 1;
  const naverId = (document.getElementById('naver-login-id')?.value || '').trim();
  const naverPassword = (document.getElementById('naver-login-pw')?.value || '').trim();
  const agencyName = (document.getElementById('naver-login-agency')?.value || '').trim();
  const realtorId = (document.getElementById('naver-login-realtor-id')?.value || '').trim();

  try {
    const res = await fetch(`/api/naver/realtor/settings?userId=${uId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        userId: uId,
        naverId,
        naverPassword,
        agencyName,
        realtorId
      })
    });
    const data = await res.json();
    if (res.ok) {
      const subAgencyEl = document.getElementById('sub-naver-agency');
      if (subAgencyEl) subAgencyEl.textContent = agencyName || naverId || '연동 완료';
      if (!silent) {
        showToastNotification('🔒 계정 저장 완료', '이실장 계정 정보가 내 PC 로컬 DB에 안전하게 암호화 저장되었습니다!', '🏢');
      }
      return true;
    }
  } catch (err) {
    console.error('saveRealtorSettings error:', err);
    if (!silent) alert('계정 정보 저장 중 오류가 발생했습니다: ' + err.message);
  }
  return false;
};

// 이실장(aipartner.plus) 공식 웹사이트 새 창 열기 (직접 로그인)
window.openAiPartnerWeb = function() {
  window.open('https://www.aipartner.plus/', '_blank');
};

// 클립보드에서 이실장 매물 복사본 즉시 가져와 대장 대조 실행
window.importFromClipboardAndAudit = async function() {
  let clipText = '';
  try {
    if (navigator.clipboard && navigator.clipboard.readText) {
      clipText = await navigator.clipboard.readText();
    }
  } catch (err) {
    console.warn('Clipboard read error:', err);
  }

  if (clipText && clipText.trim().length > 0) {
    const textEl = document.getElementById('naver-bulk-text');
    if (textEl) textEl.value = clipText.trim();
    switchNaverModalTab('bulk');
    await runNaverBulkRegister();
  } else {
    switchNaverModalTab('bulk');
    const textEl = document.getElementById('naver-bulk-text');
    if (textEl) {
      textEl.focus();
      alert('이실장(aipartner.plus)에서 매물 목록이나 번호를 복사(Ctrl+C)하신 후, 여기에 붙여넣기(Ctrl+V) 해주세요!');
    }
  }
};

// 클립보드에 이실장 1초 전송 스크립트 복사
window.copyBookmarkletScript = function() {
  const script = "(function(){const m=document.body.innerText.match(/\\b\\d{9,11}\\b/g)||[];const u=[...new Set(m)];if(!u.length){alert('화면에서 매물번호를 찾지 못했습니다.');return;}fetch('http://localhost:5000/api/naver/listings/register-bulk',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({articleNumbers:u,userId:1})}).then(r=>r.json()).then(d=>alert('🎉 온하우스로 '+u.length+'건 매물 전송 완료! 온하우스 창을 확인하세요.')).catch(e=>alert('온하우스 전송 실패: '+e));})();";
  navigator.clipboard.writeText(script).then(() => {
    alert('✅ [온하우스 1초 전송 스크립트]가 복사되었습니다!\n\n사용법:\n현재 띄워두신 AI실장 브라우저 창에서 F12(개발자도구) -> Console(콘솔) 탭에 붙여넣기(Ctrl+V) 후 Enter를 누르시면, 현재 화면의 109건 매물이 온하우스로 즉시 날아옵니다!');
  }).catch(() => {
    prompt('아래 스크립트를 복사하여 AI실장 창의 F12 콘솔에 붙여넣으세요:', script);
  });
};

// 로그 클립보드 복사
window.copyAiPartnerLogs = function() {
  const content = document.getElementById('aipartner-log-content');
  if (content && content.textContent) {
    navigator.clipboard.writeText(content.textContent).then(() => {
      showToastNotification('📋 로그 복사', '실시간 통신 로그가 클립보드에 복사되었습니다.', 'ℹ️');
    });
  }
};

// 이실장(AI실장 / aipartner.com) 로그인 및 내 매물 목록 불러오기
window.runAiPartnerLoginAndFetch = async function() {
  const idEl = document.getElementById('naver-login-id');
  const pwEl = document.getElementById('naver-login-pw');
  const agencyEl = document.getElementById('naver-login-agency');
  const realtorIdEl = document.getElementById('naver-login-realtor-id');
  const btn = document.getElementById('btn-do-naver-login');

  const logBox = document.getElementById('aipartner-log-container');
  const logContent = document.getElementById('aipartner-log-content');

  const memberId = idEl ? idEl.value.trim() : '';
  const memberPw = pwEl ? pwEl.value.trim() : '';
  const agencyName = agencyEl ? agencyEl.value.trim() : '';
  const realtorInput = realtorIdEl ? realtorIdEl.value.trim() : '';

  if (!memberId || !memberPw) {
    alert('이실장(AI실장) 아이디(또는 휴대폰 번호)와 비밀번호를 모두 입력해주세요.');
    if (!memberId && idEl) idEl.focus();
    else if (!memberPw && pwEl) pwEl.focus();
    return;
  }

  if (logBox) logBox.style.display = 'block';
  if (logContent) {
    logContent.textContent = `[${new Date().toLocaleTimeString()}] 🚀 이실장 서버(${memberId}) 접속 시작...\n[${new Date().toLocaleTimeString()}] 🔑 보안 세션 및 CSRF 토큰 협상 중...`;
  }

  btn.disabled = true;
  btn.innerHTML = '⏳ 이실장 통신 및 매물 수집 중...';

  try {
    await saveRealtorSettings(true);

    const res = await fetch('/api/aipartner/login-and-fetch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: memberId,
        password: memberPw,
        agencyName,
        realtorInput,
        userId: currentUser ? currentUser.id : 1
      })
    });

    const data = await res.json();
    
    // 실시간 서버 로그 콘솔에 표시
    if (logContent && data.logs && data.logs.length > 0) {
      logContent.textContent = data.logs.join('\n');
      logContent.scrollTop = logContent.scrollHeight;
    }

    await loadNaverListings();
    loadModalListingTable();

    if (data.success) {
      showToastNotification('🎉 이실장 연동 성공', data.message, '🟢');
      if (data.successCount > 0) {
        if (confirm(`${data.message}\n\n지금 바로 공공 건축물대장 1초 전수 대조 검증을 실행하시겠습니까?`)) {
          closeNaverInspectModal();
          switchMainView('naver');
          runAuditAllListings();
        }
      }
    } else {
      if (logContent) {
        logContent.textContent += `\n[${new Date().toLocaleTimeString()}] ⚠️ 결과 안내: ${data.message}`;
        logContent.scrollTop = logContent.scrollHeight;
      }
      alert('⚠️ ' + (data.message || '이실장 연동 실패: 아래 실시간 로그 창의 오류 상세를 확인해주세요.'));
    }

    const btnAudit = document.getElementById('btn-modal-audit-all');
    if (btnAudit) btnAudit.style.display = 'inline-block';
  } catch (err) {
    console.error(err);
    if (logContent) {
      logContent.textContent += `\n[${new Date().toLocaleTimeString()}] ❌ 클라이언트 통신 오류: ${err.message}`;
    }
    alert('이실장 연동 중 오류가 발생했습니다: ' + err.message);
  } finally {
    btn.disabled = false;
    btn.innerHTML = '🚀 이실장 로그인 및 매물 수집 실행';
  }
};

window.runNaverLoginAndFetch = window.runAiPartnerLoginAndFetch;

// 매물 일괄 등록 (붙여넣기) 실행
window.runNaverBulkRegister = async function() {
  const textEl = document.getElementById('naver-bulk-text');
  const btn = document.getElementById('btn-do-naver-bulk');
  const text = textEl ? textEl.value.trim() : '';

  if (!text) {
    alert('등록할 네이버 매물 URL 링크 또는 매물번호를 입력해주세요.');
    if (textEl) textEl.focus();
    return;
  }

  btn.disabled = true;
  btn.innerHTML = '⏳ 신규 매물 스펙 분석 및 수집 중...';

  try {
    const res = await fetch('/api/naver/listings/register-bulk', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        text,
        userId: currentUser ? currentUser.id : 1,
        skipAlreadyAudited: true
      })
    });

    const data = await res.json();
    if (!res.ok || !data.success) {
      alert('⚠️ ' + (data.message || '매물 등록 실패'));
      return;
    }

    textEl.value = '';
    await loadNaverListings();
    loadModalListingTable();

    showToastNotification('➕ 매물 등록 결과', data.message, '🟢');

    if (data.successCount > 0) {
      if (confirm(`${data.message}\n\n지금 바로 신규 매물에 대한 공공 건축물대장 전수 대조 검증을 실행하시겠습니까?`)) {
        closeNaverInspectModal();
        switchMainView('naver');
        runAuditAllListings();
      }
    } else if (data.skippedCount > 0) {
      alert(`입력하신 매물은 이미 대장 검수가 완료되어 기존 결과를 안전하게 보존하였습니다.\n(신규 수집: 0건 / 기존 검수완료 건너뜀: ${data.skippedCount}건)`);
    }
  } catch (err) {
    console.error(err);
    alert('일괄 등록 중 통신 오류가 발생했습니다: ' + err.message);
  } finally {
    btn.disabled = false;
    btn.innerHTML = '➕ 매물 일괄 등록 및 스펙 수집';
  }
};

// 모달 내부의 내 매물 테이블 렌더링
function loadModalListingTable() {
  const area = document.getElementById('naver-realtor-listing-area');
  const countEl = document.getElementById('naver-modal-list-count');
  const tbody = document.getElementById('naver-modal-listing-tbody');
  if (!area || !tbody) return;

  countEl.textContent = currentNaverListings.length;
  if (currentNaverListings.length === 0) {
    area.style.display = 'block';
    tbody.innerHTML = `
      <div style="padding: 24px; text-align: center; color: #94a3b8; font-size: 13px;">
        등록된 매물이 없습니다.<br>
        <span style="color: #64748b; font-size: 12px; margin-top: 4px; display: inline-block;">
          상단의 <b>[➕ 매물 일괄 등록]</b> 탭에서 매물 링크들을 붙여넣으시면 즉시 목록이 구성됩니다.
        </span><br>
        <button type="button" class="btn btn-sm btn-primary" onclick="switchNaverModalTab('bulk')" style="margin-top: 12px; background: #03c75a; border-color: #03c75a; font-weight: 700; padding: 8px 16px;">
          ➕ 매물 링크 붙여넣기 탭으로 이동 &gt;
        </button>
      </div>
    `;
    return;
  }

  area.style.display = 'block';
  tbody.innerHTML = `
    <table style="width: 100%; border-collapse: collapse; font-size: 12px; text-align: left;">
      <thead style="background: #1e293b; color: #94a3b8; border-bottom: 1px solid #334155;">
        <tr>
          <th style="padding: 8px 10px;">매물명</th>
          <th style="padding: 8px 10px;">가격/층수</th>
          <th style="padding: 8px 10px;">소재지</th>
          <th style="padding: 8px 10px; text-align: center;">대장 검증 상태</th>
        </tr>
      </thead>
      <tbody style="background: #0f172a;">
        ${currentNaverListings.map(item => {
          let badge = '<span style="color: #94a3b8;">⏳ 미검증</span>';
          if (item.ledgerStatus === 'Safe') badge = '<span style="color: #4ade80; font-weight: 700;">✅ 정상 일치</span>';
          else if (item.ledgerStatus === 'Warning') badge = '<span style="color: #facc15; font-weight: 700;">⚠️ 주의 요망</span>';
          else if (item.ledgerStatus === 'Danger') badge = '<span style="color: #f87171; font-weight: 700;">🚨 과태료 위험</span>';

          return `
            <tr style="border-bottom: 1px solid #1e293b;">
              <td style="padding: 8px 10px; font-weight: 600; color: #f8fafc;">${escapeHtml(item.articleName || '매물 ' + item.articleNumber)}</td>
              <td style="padding: 8px 10px; color: #fbbf24;">${escapeHtml(item.priceDisplay || '-')} / ${escapeHtml(item.floorInfo || '-')}</td>
              <td style="padding: 8px 10px; color: #cbd5e1; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${escapeHtml(item.address || '-')}</td>
              <td style="padding: 8px 10px; text-align: center;">${badge}</td>
            </tr>
          `;
        }).join('')}
      </tbody>
    </table>
  `;
}

// 네이버 매물 목록 불러오기 (메인 허브)
window.loadNaverListings = async function() {
  const uId = currentUser ? currentUser.id : 1;
  try {
    const res = await fetch(`/api/naver/listings?userId=${uId}`);
    if (!res.ok) return;
    currentNaverListings = await res.json();

    let safeCount = 0;
    let warningCount = 0;
    let dangerCount = 0;
    let pendingCount = 0;

    currentNaverListings.forEach(item => {
      if (item.ledgerStatus === 'Safe') safeCount++;
      else if (item.ledgerStatus === 'Warning') warningCount++;
      else if (item.ledgerStatus === 'Danger') dangerCount++;
      else pendingCount++;
    });

    const totalEl = document.getElementById('kpi-naver-total');
    const safeEl = document.getElementById('kpi-naver-safe');
    const warningEl = document.getElementById('kpi-naver-warning');
    const dangerEl = document.getElementById('kpi-naver-danger');
    const pendingEl = document.getElementById('kpi-naver-pending');
    const navBadge = document.getElementById('stat-naver-count');

    if (totalEl) totalEl.textContent = currentNaverListings.length;
    if (safeEl) safeEl.textContent = safeCount;
    if (warningEl) warningEl.textContent = warningCount;
    if (dangerEl) dangerEl.textContent = dangerCount;
    if (pendingEl) pendingEl.textContent = pendingCount;
    if (navBadge) navBadge.textContent = currentNaverListings.length;

    renderNaverList(currentNaverListings);
  } catch (err) {
    console.error('loadNaverListings error:', err);
  }
};

// 필터링 버튼 클릭 (all, Safe, Warning, Danger, Pending)
window.filterNaverListings = function(status) {
  currentNaverFilter = status;
  document.querySelectorAll('.naver-kpi-btn').forEach(b => b.classList.remove('active'));
  const btn = document.getElementById(`kpi-btn-${status.toLowerCase()}`);
  if (btn) btn.classList.add('active');

  let filtered = currentNaverListings;
  if (status !== 'all') {
    filtered = currentNaverListings.filter(x => x.ledgerStatus === status);
  }
  renderNaverList(filtered);
};

// 네이버 매물 카드 그리드 렌더링
window.renderNaverList = function(list) {
  const container = document.getElementById('naver-list');
  if (!container) return;
  container.innerHTML = '';

  const countSpan = document.getElementById('list-count');
  if (countSpan && currentViewMode === 'naver') {
    countSpan.textContent = list.length;
  }

  if (list.length === 0) {
    container.innerHTML = `
      <div style="padding: 40px 20px; text-align: center; color: #94a3b8; background: #0f172a; border-radius: 8px; border: 1px dashed #334155; margin-top: 10px;">
        <div style="font-size: 36px; margin-bottom: 12px;">🏢</div>
        <div style="font-size: 15px; font-weight: 700; color: #f8fafc; margin-bottom: 6px;">등록된 이실장(AI실장+) 매물이 없습니다</div>
        <div style="font-size: 13px; color: #64748b; margin-bottom: 16px;">
          이실장(aipartner.plus) 계정으로 로그인하시거나, 올리신 매물 번호/링크들을 복사하여 붙여넣으세요.
        </div>
        <div style="display: flex; justify-content: center; gap: 10px;">
          <button type="button" class="btn btn-primary" onclick="openNaverInspectModal('login')" style="background: #03c75a; border-color: #03c75a; font-weight: 700; font-size: 13px; padding: 10px 18px;">
            🤖 이실장(AI실장+) 로그인 및 계정 연동
          </button>
          <button type="button" class="btn btn-secondary" onclick="openNaverInspectModal('bulk')" style="font-size: 13px; padding: 10px 18px;">
            ➕ 이실장 매물 일괄 붙여넣기
          </button>
        </div>
      </div>
    `;
    clearDetail();
    return;
  }

  list.forEach(item => {
    const card = document.createElement('div');
    card.className = 'property-card' + (selectedNaverListingId === item.id ? ' active' : '');
    card.setAttribute('data-id', item.id);

    let badgeHtml = '';
    let borderColor = '#334155';
    if (item.ledgerStatus === 'Safe') {
      badgeHtml = '<span class="badge" style="background: #065f46; color: #6ee7b7; font-weight: 700;">✅ 대장 정상 일치</span>';
      borderColor = '#059669';
    } else if (item.ledgerStatus === 'Warning') {
      badgeHtml = '<span class="badge" style="background: #78350f; color: #fde68a; font-weight: 700;">⚠️ 주의 요망</span>';
      borderColor = '#d97706';
    } else if (item.ledgerStatus === 'Danger') {
      badgeHtml = '<span class="badge" style="background: #7f1d1d; color: #fca5a5; font-weight: 700;">🚨 과태료 위험</span>';
      borderColor = '#dc2626';
    } else {
      badgeHtml = '<span class="badge" style="background: #1e293b; color: #94a3b8;">⏳ 미검증</span>';
    }

    const isChecked = selectedNaverArticleIds.has(item.id);
    const importedBadge = item.isImported ? '<span class="badge" style="background: #1e1b4b; color: #a5b4fc;">📥 장부 저장됨</span>' : '';

    card.style.borderColor = borderColor;
    card.innerHTML = `
      <div class="card-top">
        <div class="badges" style="align-items: center; flex-wrap: wrap; gap: 4px;">
          <input type="checkbox" class="naver-item-checkbox" data-id="${item.id}" ${isChecked ? 'checked' : ''} style="cursor: pointer; transform: scale(1.15); margin-right: 4px;" />
          <span class="badge" style="background: #03c75a; color: #fff; font-weight: 700;">N부동산</span>
          ${badgeHtml}
          ${importedBadge}
        </div>
        <div class="card-price">${escapeHtml(item.priceDisplay || '-')}</div>
      </div>
      <div class="card-title">${escapeHtml(item.articleName || '매물 ' + item.articleNumber)}</div>
      <div class="card-address">📍 ${escapeHtml(item.address || '-')} (${escapeHtml(item.floorInfo || '-')})</div>
      <div style="font-size: 0.8rem; color: #cbd5e1; margin: 6px 0; display: flex; gap: 12px;">
        <span>🛗 승강기 ${item.hasElevator ? '있음' : '없음'}</span>
        <span>🚗 주차 ${item.totalParking}대</span>
        <span>📐 면적 ${item.areaM2 ? item.areaM2.toFixed(1) + '㎡' : '-'}</span>
      </div>
      <div style="display: flex; gap: 6px; margin-top: 8px;">
        <button class="btn btn-sm btn-accent" style="flex: 1; font-size: 11px; padding: 4px;" onclick="event.stopPropagation(); auditSingleNaverListing(${item.id})">
          ⚡ 1초 대장 대조
        </button>
        <button class="btn btn-sm btn-secondary" style="flex: 1; font-size: 11px; padding: 4px;" onclick="event.stopPropagation(); importNaverListingToProperties(${item.id})">
          📥 내 장부로 저장
        </button>
      </div>
    `;

    card.addEventListener('click', () => selectNaverListing(item.id));
    const chk = card.querySelector('.naver-item-checkbox');
    if (chk) {
      chk.addEventListener('change', (e) => {
        e.stopPropagation();
        if (e.target.checked) selectedNaverArticleIds.add(item.id);
        else selectedNaverArticleIds.delete(item.id);
        updateNaverSelectedCount();
      });
    }

    container.appendChild(card);
  });

  if (!selectedNaverListingId && list.length > 0) {
    selectNaverListing(list[0].id);
  }
};

// 전체 선택 토글
window.toggleSelectAllNaverListings = function(checked) {
  if (checked) {
    currentNaverListings.forEach(x => selectedNaverArticleIds.add(x.id));
  } else {
    selectedNaverArticleIds.clear();
  }
  updateNaverSelectedCount();
  renderNaverList(currentNaverListings);
};

function updateNaverSelectedCount() {
  const el = document.getElementById('naver-selected-count');
  if (el) el.textContent = selectedNaverArticleIds.size;
}

// 네이버 매물 선택 시 우측 1:1 대조 리포트 렌더링
window.selectNaverListing = function(id) {
  selectedNaverListingId = id;
  document.querySelectorAll('#naver-list .property-card').forEach(c => c.classList.remove('active'));
  const card = document.querySelector(`#naver-list .property-card[data-id="${id}"]`);
  if (card) card.classList.add('active');

  const item = currentNaverListings.find(x => x.id === id);
  if (!item) return;

  document.getElementById('empty-detail').style.display = 'none';
  document.getElementById('active-detail').style.display = 'none';
  document.getElementById('deal-detail').style.display = 'none';
  const detailPanel = document.getElementById('naver-detail');
  detailPanel.style.display = 'block';

  document.getElementById('naver-detail-header').innerHTML = `
    <div style="display: flex; align-items: center; justify-content: space-between;">
      <span class="badge" style="background: #03c75a; color: #fff; font-weight: 700;">🟢 NAVER 매물번호: ${escapeHtml(item.articleNumber)}</span>
      <span style="font-size: 11px; color: #94a3b8;">수집일: ${new Date(item.createdAt).toLocaleDateString()}</span>
    </div>
  `;
  document.getElementById('naver-detail-title').textContent = item.articleName || '매물 정보';
  document.getElementById('naver-detail-price').textContent = item.priceDisplay || '-';

  const banner = document.getElementById('naver-detail-banner');
  if (item.ledgerStatus === 'Safe') {
    banner.style.background = '#064e3b';
    banner.style.border = '1px solid #059669';
    banner.style.color = '#6ee7b7';
    banner.innerHTML = `✅ <b>안심 매물 검증 완료:</b> ${escapeHtml(item.ledgerMessage || '네이버 등록 정보가 실제 건축물대장과 정확히 일치합니다.')}`;
  } else if (item.ledgerStatus === 'Warning') {
    banner.style.background = '#451a03';
    banner.style.border = '1px solid #d97706';
    banner.style.color = '#fde68a';
    banner.innerHTML = `⚠️ <b>주의 요망:</b> ${escapeHtml(item.ledgerMessage || '일부 스펙에 주의가 필요한 항목이 있습니다.')}`;
  } else if (item.ledgerStatus === 'Danger') {
    banner.style.background = '#450a0a';
    banner.style.border = '1px solid #dc2626';
    banner.style.color = '#fca5a5';
    banner.innerHTML = `🚨 <b>과태료 위험 항목 감지!</b> ${escapeHtml(item.ledgerMessage || '층수 또는 승강기, 위반건축물 등 불일치가 감지되었습니다!')}`;
  } else {
    banner.style.background = '#1e293b';
    banner.style.border = '1px solid #475569';
    banner.style.color = '#cbd5e1';
    banner.innerHTML = `⏳ <b>대장 미검증:</b> 우측 하단의 [⚡ 이 매물 대장 재검증] 버튼을 눌러 국토부 대장과 즉시 비교하세요.`;
  }

  const tbody = document.getElementById('naver-detail-discrepancy-tbody');
  let discrepancies = [];
  if (item.ledgerDiscrepanciesJson) {
    try {
      discrepancies = JSON.parse(item.ledgerDiscrepanciesJson);
    } catch {}
  }

  if (discrepancies.length > 0) {
    tbody.innerHTML = discrepancies.map(d => {
      let badge = '';
      let rowBg = '';
      const st = d.Status || d.status;
      if (st === 'Match') {
        badge = '<span style="color: #4ade80; font-weight: 700;">✅ 일치</span>';
      } else if (st === 'Warning') {
        badge = '<span style="color: #facc15; font-weight: 700;">⚠️ 주의</span>';
        rowBg = 'background: rgba(245, 158, 11, 0.05);';
      } else {
        badge = '<span style="color: #f87171; font-weight: 700;">🚨 불일치</span>';
        rowBg = 'background: rgba(239, 68, 68, 0.1);';
      }

      return `
        <tr style="border-bottom: 1px solid #1e293b; ${rowBg}">
          <td style="padding: 8px 10px; font-weight: 600; color: #f8fafc;">${escapeHtml(d.ItemName || d.itemName)}</td>
          <td style="padding: 8px 10px; color: #cbd5e1;">${escapeHtml(d.NaverValue || d.naverValue || '-')}</td>
          <td style="padding: 8px 10px; color: #cbd5e1;">${escapeHtml(d.LedgerValue || d.ledgerValue || '-')}</td>
          <td style="padding: 8px 10px;">${badge}</td>
        </tr>
      `;
    }).join('');
  } else {
    tbody.innerHTML = `<tr><td colspan="4" style="padding: 16px; text-align: center; color: #64748b;">아직 대장 대조가 수행되지 않았습니다.</td></tr>`;
  }

  let raw = {};
  try { raw = JSON.parse(item.rawJson || '{}'); } catch {}

  const naverSpecsEl = document.getElementById('naver-detail-specs');
  naverSpecsEl.innerHTML = `
    <div><b>광고 제목:</b> ${escapeHtml(raw.title || item.articleName)}</div>
    <div><b>층수 정보:</b> ${escapeHtml(item.floorInfo || '-')}</div>
    <div><b>전용 면적:</b> ${item.areaM2 ? item.areaM2.toFixed(2) + '㎡' : '-'}</div>
    <div><b>승강기/주차:</b> ${item.hasElevator ? '엘리베이터 있음 🛗' : '없음'} / ${item.totalParking}대</div>
    <div><b>소재지:</b> ${escapeHtml(item.address || '-')}</div>
  `;

  const ledgerEl = document.getElementById('naver-detail-ledger');
  ledgerEl.innerHTML = `
    <div><b>검증 상태:</b> ${escapeHtml(item.ledgerStatus)}</div>
    <div><b>판정 요약:</b> ${escapeHtml(item.ledgerMessage || '미검증')}</div>
    <div><b>검증 시각:</b> ${item.inspectedAt ? new Date(item.inspectedAt).toLocaleString() : '미수행'}</div>
  `;

  document.getElementById('btn-naver-audit-single').onclick = () => auditSingleNaverListing(item.id);
  document.getElementById('btn-naver-import-prop').onclick = () => importNaverListingToProperties(item.id);
  const webLink = document.getElementById('btn-naver-web-link');
  if (webLink) {
    webLink.href = `https://fin.land.naver.com/articles/${item.articleNumber}`;
  }
};

// 미검증 대장 일괄 검증 실행
window.runAuditAllListings = async function() {
  const uId = currentUser ? currentUser.id : 1;
  const progressWrap = document.getElementById('naver-audit-progress-wrap');
  const bar = document.getElementById('naver-progress-bar');
  const percentEl = document.getElementById('naver-progress-percent');
  const labelEl = document.getElementById('naver-progress-label');
  const btn = document.getElementById('btn-audit-all');

  if (progressWrap) progressWrap.style.display = 'block';
  if (btn) btn.disabled = true;

  let progress = 10;
  if (bar) bar.style.width = '10%';
  if (percentEl) percentEl.textContent = '10%';
  if (labelEl) labelEl.textContent = '국토교통부 건축물대장 서버 접속 및 전수 대조 중...';

  const timer = setInterval(() => {
    if (progress < 90) {
      progress += 10;
      if (bar) bar.style.width = progress + '%';
      if (percentEl) percentEl.textContent = progress + '%';
    }
  }, 400);

  try {
    const res = await fetch(`/api/naver/listings/audit-all?userId=${uId}&forceAll=false`, { method: 'POST' });
    clearInterval(timer);

    if (bar) bar.style.width = '100%';
    if (percentEl) percentEl.textContent = '100%';
    if (labelEl) labelEl.textContent = '대장 검증 완료!';

    const data = await res.json();
    await loadNaverListings();
    loadModalListingTable();

    if (selectedNaverListingId) selectNaverListing(selectedNaverListingId);

    showToastNotification('⚡ 대장 전수 검증 완료', data.message || '건축물대장 대조가 완료되었습니다.', '🏛️');

    setTimeout(() => {
      if (progressWrap) progressWrap.style.display = 'none';
      if (btn) btn.disabled = false;
    }, 1500);
  } catch (err) {
    clearInterval(timer);
    console.error(err);
    if (progressWrap) progressWrap.style.display = 'none';
    if (btn) btn.disabled = false;
    alert('대장 일괄 검증 중 오류가 발생했습니다: ' + err.message);
  }
};

// 단일 매물 대장 검증
window.auditSingleNaverListing = async function(id) {
  try {
    setStatus(`매물(ID: ${id}) 건축물대장 1초 대조 중...`);
    const res = await fetch(`/api/naver/listings/audit/${id}`, { method: 'POST' });
    const data = await res.json();
    await loadNaverListings();
    selectNaverListing(id);
    showToastNotification('⚖️ 대장 대조 완료', data.summary || '건축물대장 대조가 완료되었습니다.', data.overallStatus === 'Safe' ? '✅' : '🚨');
  } catch (err) {
    console.error(err);
    alert('대장 대조 실패: ' + err.message);
  }
};

// 내 매물 장부로 저장
window.importNaverListingToProperties = async function(id) {
  const uId = currentUser ? currentUser.id : 1;
  try {
    const res = await fetch(`/api/naver/listings/import-to-property/${id}?userId=${uId}`, { method: 'POST' });
    const data = await res.json();
    if (!res.ok || !data.success) {
      alert('⚠️ ' + (data.message || '저장 실패'));
      return;
    }

    await loadNaverListings();
    await loadProperties();
    showToastNotification('📥 내 장부 저장 완료', '네이버 매물이 내 장부에 등록되었습니다!', '📋');
  } catch (err) {
    console.error(err);
    alert('내 장부 저장 중 오류가 발생했습니다: ' + err.message);
  }
};

// 선택된 매물 일괄 내 장부로 저장
window.importSelectedNaverListings = async function() {
  if (selectedNaverArticleIds.size === 0) {
    alert('저장할 매물을 1개 이상 체크해주세요.');
    return;
  }

  const ids = Array.from(selectedNaverArticleIds);
  if (!confirm(`선택한 ${ids.length}개의 네이버 매물을 내 매물 장부로 일괄 등록하시겠습니까?`)) return;

  setStatus(`${ids.length}건의 매물을 내 장부로 저장 중...`);
  let success = 0;
  for (const id of ids) {
    try {
      const res = await fetch(`/api/naver/listings/import-to-property/${id}?userId=${currentUser ? currentUser.id : 1}`, { method: 'POST' });
      if (res.ok) success++;
    } catch {}
  }

  selectedNaverArticleIds.clear();
  updateNaverSelectedCount();
  await loadNaverListings();
  await loadProperties();
  showToastNotification('📥 일괄 저장 완료', `총 ${success}건의 매물이 내 장부에 등록되었습니다!`, '📋');
};

window.importAllCheckedToProperties = window.importSelectedNaverListings;

// 선택 매물 삭제
window.deleteSelectedNaverListings = async function() {
  if (selectedNaverArticleIds.size === 0) {
    alert('삭제할 매물을 1개 이상 체크해주세요.');
    return;
  }
  if (!confirm(`선택한 ${selectedNaverArticleIds.size}개 매물을 목록에서 삭제하시겠습니까?`)) return;

  for (const id of selectedNaverArticleIds) {
    try {
      await fetch(`/api/naver/listings/${id}?userId=${currentUser ? currentUser.id : 1}`, { method: 'DELETE' });
    } catch {}
  }
  selectedNaverArticleIds.clear();
  updateNaverSelectedCount();
  await loadNaverListings();
  showToastNotification('🗑️ 삭제 완료', '선택 매물이 삭제되었습니다.', '🧹');
};

// 목록 전체 비우기
window.clearAllNaverListings = async function() {
  if (!confirm('네이버 매물 목록 전체를 비우시겠습니까?')) return;
  try {
    await fetch(`/api/naver/listings/clear?userId=${currentUser ? currentUser.id : 1}`, { method: 'POST' });
    selectedNaverArticleIds.clear();
    await loadNaverListings();
    clearDetail();
    showToastNotification('🗑️ 전체 삭제 완료', '네이버 매물 목록이 초기화되었습니다.', '🧹');
  } catch (err) {
    alert('초기화 실패');
  }
};

// --- 단일 URL 검증 로직 ---
async function runNaverInspect() {
  const input = document.getElementById('naver-inspect-url');
  const loading = document.getElementById('naver-inspect-loading');
  const resultArea = document.getElementById('naver-inspect-result');
  const btn = document.getElementById('btn-do-naver-inspect');

  if (!input || !input.value.trim()) {
    alert('네이버 부동산 매물 링크(URL) 또는 매물번호를 입력해주세요.');
    if (input) input.focus();
    return;
  }

  const url = input.value.trim();
  loading.style.display = 'block';
  resultArea.style.display = 'none';
  btn.disabled = true;

  try {
    const res = await fetch('/api/naver/inspect', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url })
    });

    const data = await res.json();
    loading.style.display = 'none';
    btn.disabled = false;

    if (!res.ok || !data.success) {
      alert('⚠️ ' + (data.message || '매물 정보를 조회할 수 없습니다.'));
      return;
    }

    window._lastNaverInspection = data;
    renderNaverInspectionResult(data);
    resultArea.style.display = 'block';
  } catch (err) {
    console.error(err);
    loading.style.display = 'none';
    btn.disabled = false;
    alert('검증 중 통신 오류가 발생했습니다: ' + err.message);
  }
}
window.runNaverInspect = runNaverInspect;

function renderNaverInspectionResult(data) {
  const banner = document.getElementById('naver-overall-banner');
  const bannerText = document.getElementById('naver-overall-text');
  const tbody = document.getElementById('naver-discrepancy-tbody');
  const naverCard = document.getElementById('naver-card-details');
  const ledgerCard = document.getElementById('ledger-card-details');

  if (data.overallStatus === 'Safe') {
    banner.style.background = '#064e3b';
    banner.style.border = '1px solid #059669';
    bannerText.style.color = '#6ee7b7';
    bannerText.innerHTML = `✅ <b>안심 매물 검증 완료:</b> ${escapeHtml(data.summary || '네이버 등록 정보가 건축물대장과 정확히 일치합니다.')}`;
  } else if (data.overallStatus === 'Warning') {
    banner.style.background = '#451a03';
    banner.style.border = '1px solid #d97706';
    bannerText.style.color = '#fde68a';
    bannerText.innerHTML = `⚠️ <b>주의 항목 발견:</b> ${escapeHtml(data.summary || '일부 스펙에 주의가 필요한 항목이 있습니다.')}`;
  } else {
    banner.style.background = '#450a0a';
    banner.style.border = '1px solid #dc2626';
    bannerText.style.color = '#fca5a5';
    bannerText.innerHTML = `🚨 <b>허위매물/과태료 위험 감지:</b> ${escapeHtml(data.summary || '층수 또는 승강기, 위반건축물 등 불일치가 감지되었습니다!')}`;
  }

  if (data.discrepancies && data.discrepancies.length > 0) {
    tbody.innerHTML = data.discrepancies.map(d => {
      let statusBadge = '';
      let rowBg = '';
      if (d.status === 'Match') {
        statusBadge = '<span style="color: #4ade80; font-weight: 700;">✅ 일치</span>';
      } else if (d.status === 'Warning') {
        statusBadge = '<span style="color: #facc15; font-weight: 700;">⚠️ 주의</span>';
        rowBg = 'background: rgba(245, 158, 11, 0.05);';
      } else {
        statusBadge = '<span style="color: #f87171; font-weight: 700;">🚨 불일치</span>';
        rowBg = 'background: rgba(239, 68, 68, 0.1);';
      }

      return `
        <tr style="border-bottom: 1px solid #1e293b; ${rowBg}">
          <td style="padding: 10px 12px; font-weight: 600; color: #f8fafc;">${escapeHtml(d.itemName)}</td>
          <td style="padding: 10px 12px; color: #cbd5e1;">${escapeHtml(d.naverValue || '-')}</td>
          <td style="padding: 10px 12px; color: #cbd5e1;">${escapeHtml(d.ledgerValue || '-')}</td>
          <td style="padding: 10px 12px;">
            <div>${statusBadge}</div>
            <div style="font-size: 11px; color: #94a3b8; margin-top: 2px;">${escapeHtml(d.note || '')}</div>
          </td>
        </tr>
      `;
    }).join('');
  } else {
    tbody.innerHTML = '<tr><td colspan="4" style="padding: 15px; text-align: center; color: #64748b;">대조 항목이 없습니다.</td></tr>';
  }

  const n = data.naverItem || {};
  let priceStr = '';
  if (n.tradeType === '전세') priceStr = `전세 ${(n.price / 10000).toLocaleString()}만원`;
  else if (n.tradeType === '매매') priceStr = `매매 ${(n.price / 10000).toLocaleString()}만원`;
  else priceStr = `보증금 ${(n.price / 10000).toLocaleString()}만원`;

  naverCard.innerHTML = `
    <div><b>매물명/유형:</b> ${escapeHtml(n.articleName || '기타')} (${escapeHtml(n.realEstateType || '')})</div>
    <div><b>광고 제목:</b> <span style="color: #a7f3d0;">${escapeHtml(n.title || '-')}</span></div>
    <div><b>가격:</b> <span style="color: #fbbf24; font-weight: 700;">${priceStr}</span></div>
    <div><b>면적:</b> 전용 ${n.exclusiveArea ? n.exclusiveArea.toFixed(2) + '㎡' : '-'} / 공급 ${n.supplyArea ? n.supplyArea.toFixed(2) + '㎡' : '-'}</div>
    <div><b>층수:</b> 총 ${n.totalFloor || '-'}층 중 <b style="color: #38bdf8;">${escapeHtml(n.targetFloor || '-')}층</b></div>
    <div><b>구조:</b> 방 ${n.roomCount || 0}개 / 욕실 ${n.bathRoomCount || 0}개 (${escapeHtml(n.direction || '남향')})</div>
    <div><b>승강기/주차:</b> 엘리베이터 ${n.hasElevator ? '있음 🛗' : '없음'} / 주차 ${n.totalParking || 0}대</div>
    <div><b>사용승인일:</b> ${escapeHtml(n.approvalDate || '-')} (총 ${n.householdCount || 0}세대)</div>
    <div style="margin-top: 4px; padding-top: 6px; border-top: 1px dashed #334155; font-size: 11px; color: #94a3b8;">
      <b>매물번호:</b> ${escapeHtml(n.articleNumber)} | <b>법정동 지번:</b> ${escapeHtml(n.sigunguCd)}${escapeHtml(n.bjdongCd)} ${escapeHtml(n.bun)}-${escapeHtml(n.ji)}
    </div>
  `;

  const l = data.ledgerItem || {};
  if (l.success) {
    ledgerCard.innerHTML = `
      <div><b>건물명:</b> ${escapeHtml(l.buildingName || '미등록')}</div>
      <div><b>대지위치:</b> ${escapeHtml(l.platAddress || '-')}</div>
      <div><b>주용도:</b> ${escapeHtml(l.mainPurps || '-')}</div>
      <div><b>규모:</b> 지상 <b style="color: #38bdf8;">${l.grndFlrCnt}층</b> / 지하 ${l.ugrndFlrCnt}층</div>
      <div><b>승강기:</b> <b style="${l.rideUseElvtCnt > 0 ? 'color: #34d399;' : ''}">${l.rideUseElvtCnt > 0 ? '승용 ' + l.rideUseElvtCnt + '대 완비 🛗' : '승강기 없음'}</b></div>
      <div><b>주차설비:</b> 총 ${l.totalParking}대 (자주식/기계식)</div>
      <div><b>사용승인일:</b> ${l.useApprovalDate ? l.useApprovalDate.replace(/(\d{4})(\d{2})(\d{2})/, '$1-$2-$3') : '-'}</div>
      <div><b>위반건축물:</b> <b style="${l.isViolatingBuilding ? 'color: #f87171;' : 'color: #34d399;'}">${l.isViolatingBuilding ? '🚨 위반건축물 등재' : '✅ 정상 (위반 없음)'}</b></div>
    `;
  } else {
    ledgerCard.innerHTML = `
      <div style="color: #94a3b8; padding: 20px 0; text-align: center;">
        건축물대장 정보가 조회되지 않았습니다.<br>
        <span style="font-size: 11px; color: #64748b;">(${escapeHtml(l.message || '주소 정보 확인 필요')})</span>
      </div>
    `;
  }
}
window.renderNaverInspectionResult = renderNaverInspectionResult;

function importNaverPropertyToForm() {
  const data = window._lastNaverInspection;
  if (!data || !data.naverItem) {
    alert('가져올 매물 정보가 없습니다.');
    return;
  }

  const n = data.naverItem;
  const l = data.ledgerItem || {};

  closeNaverInspectModal();
  openPropertyModal();

  document.getElementById('prop-trans-type').value = n.tradeType || '전세';
  
  const depositMan = n.price > 0 ? Math.round(n.price / 10000) : 0;
  document.getElementById('prop-deposit').value = depositMan;
  if (n.previousMonthlyRent > 0) {
    document.getElementById('prop-rent').value = Math.round(n.previousMonthlyRent / 10000);
  } else {
    document.getElementById('prop-rent').value = '';
  }

  const addr = l.platAddress || l.newPlatAddress || `${n.sigunguCd} ${n.bun}-${n.ji}`;
  document.getElementById('prop-address').value = addr;
  document.getElementById('prop-detail-addr').value = `${n.targetFloor}층 (${n.articleName})`;

  let floorNum = parseInt(n.targetFloor, 10);
  if (isNaN(floorNum)) floorNum = 1;
  document.getElementById('prop-floor').value = floorNum;
  document.getElementById('prop-total-floor').value = n.totalFloor || l.grndFlrCnt || 5;

  if (n.exclusiveArea > 0) {
    document.getElementById('prop-area').value = n.exclusiveArea.toFixed(2);
  }

  if (n.roomCount > 0) document.getElementById('prop-rooms').value = n.roomCount;
  if (n.bathRoomCount > 0) document.getElementById('prop-bathrooms').value = n.bathRoomCount;

  const hasElvt = n.hasElevator || (l.rideUseElvtCnt > 0);
  document.getElementById('prop-elevator').checked = hasElvt;
  document.getElementById('prop-parking').checked = (n.totalParking > 0) || (l.totalParking > 0);

  document.getElementById('prop-title').value = n.title || `${n.articleName} ${depositMan}만`;
  document.getElementById('prop-memo').value = `[네이버 매물번호: ${n.articleNumber} 연동]\n${n.description || ''}`;

  if (addr) {
    checkBuildingLedger();
  }

  showToastNotification('📥 네이버 매물 가져오기 완료', '네이버 매물 및 건축물대장 스펙이 등록 폼에 자동 입력되었습니다!', '🏢');
}
window.importNaverPropertyToForm = importNaverPropertyToForm;

