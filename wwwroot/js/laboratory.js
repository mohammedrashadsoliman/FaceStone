/* ==========================================================================
   FACESTONE LABORATORY DEPARTMENT CONTROLLER (قسم المعمل)
   Single-Page Continuous Testing Workflow • Central Sample Number (رقم العينة)
   Automatic Warehouse Raw Material Registration • ASTM C1240 Quality Standards
   ========================================================================== */

(function () {
  'use strict';

  const LAB_SECTION_AR = { PHYSICAL: 'المعمل الفيزيائي', CHEMICAL: 'المعمل الكيميائي' };

  const CHEMICAL_TESTS = [
    { index: 1, name: 'أكسيد السيليكون (SiO₂)', unit: '%', min: 85, minLabel: '85%', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 2, name: 'الكلوريدات (Cl⁻)', unit: '%', min: null, minLabel: '-', max: 0.10, maxLabel: '≤ 0.10%', method: 'ASTM C1240', defaultVal: '' },
    { index: 3, name: 'أكسيد الكالسيوم (CaO)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 4, name: 'أكسيد الماغنسيوم (MgO)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 5, name: 'أكسيد الحديد (Fe₂O₃)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 6, name: 'أكسيد الالومنيوم (Al₂O₃)', unit: '%', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 7, name: 'الفقد بالحرق (L.O.I)', unit: '%', min: null, minLabel: '-', max: 6.0, maxLabel: '≤ 6%', method: 'ASTM C1240', defaultVal: '' },
    { index: 8, name: 'الرطوبة (Moisture Content)', unit: '%', min: null, minLabel: '-', max: 3.0, maxLabel: '≤ 3%', method: 'ASTM C1240', defaultVal: '' }
  ];

  const PHYSICAL_TESTS = [
    { index: 1, name: 'المساحة السطحية للسطح (النعومة)', unit: 'm²/gm', min: 15.0, minLabel: '15 m²/g', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 2, name: 'المحتجز على منخل مقاس 45 ميكرون', unit: '%', min: null, minLabel: '-', max: 10.0, maxLabel: '10%', method: 'ASTM C1240', defaultVal: '' },
    { index: 3, name: 'اللون', unit: '-', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: 'رمادى', isText: true },
    { index: 4, name: 'زمن الشك الإبتدائي', unit: 'بالدقيقة', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 5, name: 'الضغط بعد (يومين)', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 6, name: 'الضغط بعد 7 أيام', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 7, name: 'معامل الفاعلية بعد 7 أيام', unit: '%', min: 105.0001, minLabel: '> 105 %', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' },
    { index: 8, name: 'الضغط بعد 28 يوم', unit: 'N/mm²', min: null, minLabel: '-', max: null, maxLabel: '-', method: 'ASTM C1240', defaultVal: '' }
  ];

  const STATUS_AR = {
    PENDING: ['قيد المراجعة', 'lab-badge-pending'],
    APPROVED: ['معتمد', 'lab-badge-approved'],
    REJECTED: ['مرفوض', 'lab-badge-rejected']
  };

  const CONFORMITY_AR = {
    CONFORMS: ['مطابق للمواصفات', 'lab-badge-conforms'],
    NON_CONFORMING: ['غير مطابق للمواصفات', 'lab-badge-nonconforming'],
    PENDING_TESTING: ['يحتاج فحوصات إضافية', 'lab-badge-testing']
  };

  class LaboratoryApp {
    static init() {
      this.root = document.getElementById('laboratory-page-root');
      if (!this.root) return;

      this.activeSection = 'PHYSICAL';
      this.activeTab = 'raw';
      this.hasPhysical = false;
      this.hasChemical = false;
      this.nextSampleNumber = 1;
      this.nextSampleCode = 'SMP-0001';
      this.batchReferences = [];
      this.cachedReports = [];
      this.isDirty = false;
      this.finalFormLoaded = false;
      this.inprocFormLoaded = false;
      this.selectedQtyFile = null;

      this.bindElements();
      if (!this.eventsBound) {
        this.bindEvents();
        this.eventsBound = true;
      }
      this.updateUserBadge();
      this.resolveAccessAndRedirect();
    }

    static bindElements() {
      // Header & section switch
      this.sectionSwitch = document.getElementById('lab-section-switch');
      this.btnPhysical = document.getElementById('lab-btn-section-physical');
      this.btnChemical = document.getElementById('lab-btn-section-chemical');
      this.sectionSubtitle = document.getElementById('lab-section-subtitle');
      this.backBtn = document.getElementById('laboratory-back-to-departments-btn');

      // Navigation Tabs
      this.formTabs = document.querySelectorAll('.lab-form-tab');
      this.panels = document.querySelectorAll('.lab-panel');
      this.tabRawTitle = document.getElementById('lab-tab-raw-title');

      // Panels
      this.panelRaw = document.getElementById('lab-panel-raw');
      this.panelFinal = document.getElementById('lab-panel-final');
      this.panelInproc = document.getElementById('lab-panel-inproc');
      this.panelHistory = document.getElementById('lab-panel-history');
      this.panelTrace = document.getElementById('lab-panel-trace');

      // Hide Traceability tab and panel from Laboratory UI (Requirement 6)
      const tabTrace = document.getElementById('lab-form-tab-trace');
      if (tabTrace) tabTrace.style.display = 'none';
      if (this.panelTrace) this.panelTrace.style.display = 'none';

      // Inner Form Containers
      this.formContainer = document.getElementById('lab-form-container');
      this.finalContainer = document.getElementById('lab-final-container');
      this.inprocContainer = document.getElementById('lab-inproc-container');

      // History view elements
      this.historyTbody = document.getElementById('lab-history-tbody');
      this.histSectionFilter = document.getElementById('lab-hist-f-section');
      this.histComplyFilter = document.getElementById('lab-hist-f-comply');
      this.histSearchInput = document.getElementById('lab-hist-f-search');
      this.histRefreshBtn = document.getElementById('lab-hist-refresh-btn');

      // Modal elements
      this.reportModal = document.getElementById('lab-report-modal');
      this.modalSheetContent = document.getElementById('lab-modal-sheet-content');
      this.modalPrintBtn = document.getElementById('lab-modal-print-btn');
      this.modalCloseBtn = document.getElementById('lab-modal-close-btn');

      // Production Order Form Elements
      this.panelOrder = document.getElementById('lab-panel-order');
      this.panelOrders = document.getElementById('lab-panel-orders');
      this.poSeqPreview = document.getElementById('lab-order-seq-preview');
      this.poForm = document.getElementById('lab-production-order-form');
      this.poBatchesTbody = document.getElementById('lab-po-batches-tbody');
      this.poRefreshBatchesBtn = document.getElementById('btn-refresh-lab-batches');
      this.poTotalInput = document.getElementById('lab-formula-total-input');
      this.poTargetOutput = document.getElementById('lab-formula-target-output');
      this.poItemsCount = document.getElementById('lab-formula-items-count');

      // Orders Log Elements
      this.ordersTbody = document.getElementById('lab-orders-tbody');
      this.ordersSearchInput = document.getElementById('lab-orders-search-input');
      this.ordersStatusSelect = document.getElementById('lab-orders-status-select');
      this.ordersFromDate = document.getElementById('lab-orders-from-date');
      this.ordersToDate = document.getElementById('lab-orders-to-date');
      this.ordersFilterBtn = document.getElementById('btn-filter-lab-orders');
      this.ordersResetBtn = document.getElementById('btn-reset-lab-orders');

      // Order Details Modal
      this.orderDetailsModal = document.getElementById('lab-modal-order-details');
      this.orderDetailsCloseBtn = document.getElementById('lab-mod-order-close-btn');
      this.orderDetailsContent = document.getElementById('lab-mod-order-content');
      this.cachedAvailableBatches = [];
      this.cachedOrders = [];

      // Clean up tab badge
      if (this.tabRawTitle) this.tabRawTitle.textContent = 'نموذج فحص واستلام العينات';
      const rawBadge = document.querySelector('#lab-form-tab-raw .lab-tab-badge');
      if (rawBadge) rawBadge.textContent = 'صفحة موحدة';
    }

    static bindEvents() {
      // Tab navigation
      this.formTabs.forEach((tab) => {
        tab.addEventListener('click', () => {
          const tabKey = tab.getAttribute('data-tab');
          if (tabKey && tabKey !== 'trace') this.switchTab(tabKey);
        });
      });

      // Section switches
      this.btnPhysical?.addEventListener('click', () => this.switchSection('PHYSICAL'));
      this.btnChemical?.addEventListener('click', () => this.switchSection('CHEMICAL'));

      // History filters
      this.histSectionFilter?.addEventListener('change', () => this.loadHistoryList());
      this.histComplyFilter?.addEventListener('change', () => this.loadHistoryList());
      this.histSearchInput?.addEventListener('input', this.debounce(() => this.loadHistoryList(), 300));
      this.histRefreshBtn?.addEventListener('click', () => this.loadHistoryList());

      // Print Modal
      this.modalCloseBtn?.addEventListener('click', () => this.closePrintModal());
      this.modalPrintBtn?.addEventListener('click', () => {
        if (this.activePrintReport) {
          this.executePrint(this.activePrintReport);
        } else {
          window.print();
        }
      });
      this.reportModal?.addEventListener('click', (e) => {
        if (e.target === this.reportModal) this.closePrintModal();
      });

      // Production Order Form Events
      this.poForm?.addEventListener('submit', (e) => this.submitProductionOrder(e));
      this.poRefreshBatchesBtn?.addEventListener('click', () => this.loadAvailableBatches());
      document.getElementById('lab-po-target-qty')?.addEventListener('input', () => this.recalcFormula());

      // Orders Log Events
      this.ordersFilterBtn?.addEventListener('click', () => this.loadOrdersList());
      this.ordersResetBtn?.addEventListener('click', () => {
        if (this.ordersSearchInput) this.ordersSearchInput.value = '';
        if (this.ordersStatusSelect) this.ordersStatusSelect.value = 'ALL';
        if (this.ordersFromDate) this.ordersFromDate.value = '';
        if (this.ordersToDate) this.ordersToDate.value = '';
        this.loadOrdersList();
      });
      this.ordersSearchInput?.addEventListener('input', this.debounce(() => this.loadOrdersList(), 300));
      this.ordersStatusSelect?.addEventListener('change', () => this.loadOrdersList());

      // Order Details Modal Events
      this.orderDetailsCloseBtn?.addEventListener('click', () => {
        if (this.orderDetailsModal) this.orderDetailsModal.style.display = 'none';
      });
      this.orderDetailsModal?.addEventListener('click', (e) => {
        if (e.target === this.orderDetailsModal) this.orderDetailsModal.style.display = 'none';
      });

      // Track unsaved changes
      this.root?.addEventListener('input', (e) => {
        if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) {
          this.isDirty = true;
        }
      });

      // Confirm leaving if unsaved
      this.backBtn?.addEventListener('click', (e) => {
        if (!this.confirmLeaveIfDirty()) {
          e.preventDefault();
          e.stopImmediatePropagation();
        }
      }, true);

      // Real-time updates for Warehouse inventory and Production Orders
      window.addEventListener('facestone:warehouse-inventory-changed', () => {
        if (this.activeTab === 'order') {
          this.loadAvailableBatches();
        }
      });
      window.addEventListener('facestone:production-order-changed', () => {
        if (this.activeTab === 'orders') {
          this.loadOrdersList();
        }
      });
      window.addEventListener('facestone:data-changed', (e) => {
        if (e.detail?.department === 'storage' && this.activeTab === 'order') {
          this.loadAvailableBatches();
        } else if (e.detail?.department === 'laboratory' && this.activeTab === 'orders') {
          this.loadOrdersList();
        }
      });
    }

    static confirmLeaveIfDirty() {
      if (!this.isDirty) return true;
      const confirmLeave = window.confirm('تنبيه: يوجد بيانات تم إدخالها في نموذج المعمل لم تُحفظ بعد.\nهل أنت متأكد من رغبتك في مغادرة القسم وفقدان هذه البيانات؟');
      if (confirmLeave) {
        this.isDirty = false;
        return true;
      }
      return false;
    }

    // ======================================================================
    // TAB NAVIGATION
    // ======================================================================
    static switchTab(tabKey) {
      if (!tabKey || tabKey === 'trace') return;
      this.activeTab = tabKey;

      if (this.formTabs) {
        this.formTabs.forEach((tab) => {
          const isActive = tab.getAttribute('data-tab') === tabKey;
          tab.classList.toggle('active', isActive);
          tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });
      }

      const tabMap = {
        raw: this.panelRaw,
        final: this.panelFinal,
        inproc: this.panelInproc,
        history: this.panelHistory,
        order: this.panelOrder,
        orders: this.panelOrders
      };

      Object.entries(tabMap).forEach(([key, panel]) => {
        if (!panel) return;
        const isActive = key === tabKey;
        panel.classList.toggle('active', isActive);
        panel.style.display = isActive ? 'block' : 'none';
      });

      if (tabKey === 'final' && !this.finalFormLoaded) {
        this.renderFinalCertView();
        this.finalFormLoaded = true;
      } else if (tabKey === 'inproc' && !this.inprocFormLoaded) {
        this.renderInProcessView();
        this.inprocFormLoaded = true;
      } else if (tabKey === 'history') {
        this.loadHistoryList();
      } else if (tabKey === 'order') {
        this.loadOrderForm();
      } else if (tabKey === 'orders') {
        this.loadOrdersList();
      }
    }

    static openTab(tabKey) {
      this.switchTab(tabKey);
    }

    static goToSlide(index) {
      const keys = ['raw', 'final', 'inproc', 'history'];
      if (typeof index === 'number' && keys[index]) {
        this.switchTab(keys[index]);
      } else if (typeof index === 'string') {
        this.switchTab(index);
      }
    }

    static updateUserBadge(userParam) {
      const user = userParam || (window.ChatApi ? window.ChatApi.getCurrentUser() : null);
      if (!user) return;
      const initials = (user.fullName || user.username || 'FS').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
      const avatar = document.getElementById('lab-user-avatar');
      const name = document.getElementById('lab-user-name');
      const role = document.getElementById('lab-user-role');
      if (avatar) avatar.textContent = initials;
      if (name) name.textContent = user.fullName || user.username;
      if (role) role.textContent = this.roleArabic(user.role);
    }

    static roleArabic(r) {
      if (!r) return 'موظف معمل';
      const map = {
        CEO: 'المدير التنفيذي',
        IT: 'مسؤول النظام (IT)',
        LABORATORY: 'موظف معمل معتمد',
        PHYSICAL_LAB: 'فني معمل فيزيائي',
        CHEMICAL_LAB: 'فني معمل كيميائي',
        MANAGEMENT: 'الإدارة العليا',
        CONTROL_MONITORING: 'التحكم والمراقبة'
      };
      return map[r] || r;
    }

    static async resolveAccessAndRedirect() {
      try {
        const user = window.ChatApi ? window.ChatApi.getCurrentUser() : null;
        const role = user ? (user.role || '').toUpperCase() : '';
        const isCeoOrIt = role === 'CEO' || role === 'IT' || role === 'MANAGEMENT';

        let hasPhysical = false, hasChemical = false;
        try {
          const res = await window.ChatApi.getDepartments();
          if (res && res.success && Array.isArray(res.data)) {
            hasPhysical = res.data.some(d => d.code === 'PHYSICAL_LAB' || d.code === 'LABORATORY');
            hasChemical = res.data.some(d => d.code === 'CHEMICAL_LAB' || d.code === 'LABORATORY');
          }
        } catch (e) {
          console.warn('[LaboratoryApp] departments check fallback:', e);
        }

        if (isCeoOrIt) {
          hasPhysical = true;
          hasChemical = true;
        }

        this.hasPhysical = hasPhysical;
        this.hasChemical = hasChemical;

        if (hasPhysical && hasChemical) {
          if (this.sectionSwitch) this.sectionSwitch.style.display = 'flex';
        } else {
          if (this.sectionSwitch) this.sectionSwitch.style.display = 'none';
        }

        let target = 'PHYSICAL';
        if (hasChemical && !hasPhysical) {
          target = 'CHEMICAL';
        }

        this.switchSection(target);
        this.switchTab('raw');
      } catch (err) {
        console.error('[LaboratoryApp] Access check failed:', err);
        this.switchSection('PHYSICAL');
      }
    }

    static switchSection(section) {
      this.activeSection = section;

      if (this.btnPhysical) {
        this.btnPhysical.classList.toggle('active', section === 'PHYSICAL');
      }
      if (this.btnChemical) {
        this.btnChemical.classList.toggle('active', section === 'CHEMICAL');
      }

      if (this.sectionSubtitle) {
        const titleAr = section === 'CHEMICAL' ? 'المعمل الكيميائي — التحاليل والمطابقة' : 'المعمل الفيزيائي — الفحص والخواص الميكانيكية';
        this.sectionSubtitle.textContent = titleAr;
      }

      this.renderOfficialDocumentForm();
      if (this.finalFormLoaded) this.renderFinalCertView();
      if (this.inprocFormLoaded) this.renderInProcessView();
      if (this.activeTab === 'history') this.loadHistoryList();
    }

    // ======================================================================
    // 2. CHEMICAL LABORATORY INSPECTION WORKFLOW (Dual Approval Gate)
    // ======================================================================
    static async renderChemicalInspectionForm() {
      if (!this.formContainer) return;
      this.formContainer.innerHTML = '<div class="lab-loading">جاري تحميل عينات المعمل الفيزيائي المؤهلة للفحص الكيميائي...</div>';

      const user = window.ChatApi ? window.ChatApi.getCurrentUser() : null;
      const userName = user ? (user.fullName || user.username || 'فني المعمل الكيميائي') : 'فني المعمل الكيميائي';
      const tests = CHEMICAL_TESTS;

      try {
        const res = await window.ChatApi.getPendingChemicalReferences();
        this.cachedPendingChemicalReferences = (res && res.success && Array.isArray(res.data)) ? res.data : [];
      } catch (err) {
        console.error('Failed to load pending chemical references:', err);
        this.cachedPendingChemicalReferences = [];
      }

      const refs = this.cachedPendingChemicalReferences || [];
      let refOptions = '';
      if (refs.length === 0) {
        refOptions = '<option value="">-- لا توجد عينات مسجلة بالمعمل الفيزيائي بانتظار الفحص الكيميائي حالياً --</option>';
      } else {
        refOptions = '<option value="">-- اختر المرجع المعملي للبدء بالفحص الكيميائي (' + refs.length + ' عينة متاحة) --</option>';
        refs.forEach(r => {
          refOptions += `<option value="${r.id}">[${this.esc(r.sampleCode)}] ${this.esc(r.sampleName)} (${parseFloat(r.receivedQuantity).toFixed(2)} ${this.esc(r.unit)}) - المورد: ${this.esc(r.supplierName || 'غير محدد')}</option>`;
        });
      }

      this.selectedChemicalReference = null;
      this.selectedChemAttachment = null;

      this.formContainer.innerHTML = `
        <div class="lab-single-form-container" dir="rtl">
          <!-- Hero Banner -->
          <div class="lab-sample-prominent-card" style="background: linear-gradient(135deg, #1e3a8a 0%, #0d9488 100%);">
            <div style="display: flex; align-items: center; gap: 14px; flex-wrap: wrap;">
              <div class="lab-sample-code-pill" id="lab-chem-hero-code" style="background: #047857; color: #fff;">بانتظار اختيار المرجع</div>
              <div>
                <div style="font-size: 0.82rem; color: #99f6e4; font-weight: 700;">المعمل الكيميائي — المرحلة الثانية للرقابة المزدوجة (Dual Approval Gate)</div>
                <h3 style="margin: 2px 0 0; color: #FFFFFF; font-size: 1.15rem; font-weight: 800;">تقرير الفحص والتحاليل الكيميائية — مواصفة ASTM C1240</h3>
              </div>
            </div>
            <div style="display: flex; gap: 8px; flex-wrap: wrap;">
              <button type="button" class="lab-btn lab-btn-primary" id="lab-chem-btn-submit-top" style="background: #059669; border-color: #059669;">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                <span>اعتماد وحفظ الفحص الكيميائي</span>
              </button>
            </div>
          </div>

          <div id="lab-chem-form-alert"></div>

          <!-- Step 1: Select Physical Reference -->
          <div class="lab-form-card-section" style="border: 2px solid #2563eb; background: #f8fafc;">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading" style="color: #1e3a8a;">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
                <span>1. اختيار المرجع المعملي المسجل من المعمل الفيزيائي (Physical Reference Selection)</span>
              </h4>
              <button type="button" class="lab-btn lab-btn-ghost" id="lab-chem-btn-refresh-refs" style="padding: 4px 10px; font-size: 0.8rem;">
                🔄 تحديث العينات
              </button>
            </div>

            <div style="margin-top: 10px;">
              <label class="lab-label" for="lab-chem-ref-select" style="font-weight: 800; color: #1e3a8a; margin-bottom: 6px; display: block;">
                المرجع المعملي للعينة الواردة <span class="req">*</span>
              </label>
              <select id="lab-chem-ref-select" class="lab-select" style="width: 100%; font-size: 1rem; font-weight: 700; padding: 0.7rem; border: 2px solid #2563eb; border-radius: 8px;" required>
                ${refOptions}
              </select>
              <span style="font-size: 0.8rem; color: #64748b; margin-top: 4px; display: block;">
                يتم استرجاع العينات المسجلة مبدئياً من قِبل موظف المعمل الفيزيائي والمثبتة برقم تسلسلي دائم (LAB-REF-...)
              </span>
            </div>

            <!-- Auto-populated details card -->
            <div id="lab-chem-ref-details-panel" style="display: none; margin-top: 15px; background: #ffffff; border-radius: 8px; border: 1px solid #cbd5e1; padding: 14px;">
              <div style="font-size: 0.88rem; font-weight: 800; color: #1e3a8a; margin-bottom: 10px; border-bottom: 1px solid #e2e8f0; padding-bottom: 6px;">
                📋 البيانات المشتركة الواردة من المعمل الفيزيائي (قراءة فقط):
              </div>
              <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; font-size: 0.85rem;">
                <div><span style="color:#64748b; display:block;">الرقم المرجعي المعملي:</span><strong id="lab-chem-disp-code" style="color:#1e40af; font-size:1rem;">-</strong></div>
                <div><span style="color:#64748b; display:block;">اسم المادة / العينة:</span><strong id="lab-chem-disp-name" style="color:#0f172a;">-</strong></div>
                <div><span style="color:#64748b; display:block;">المورد:</span><strong id="lab-chem-disp-supplier" style="color:#0f172a;">-</strong></div>
                <div><span style="color:#64748b; display:block;">تاريخ الدخول والوصول:</span><strong id="lab-chem-disp-date" style="color:#0f172a;">-</strong></div>
                <div><span style="color:#64748b; display:block;">الكمية المستلمة:</span><strong id="lab-chem-disp-qty" style="color:#059669; font-size:1rem;">-</strong></div>
                <div><span style="color:#64748b; display:block;">تصنيف المادة:</span><span id="lab-chem-disp-classification" class="lab-badge">-</span></div>
                <div><span style="color:#64748b; display:block;">الفاحص الفيزيائي:</span><strong id="lab-chem-disp-inspector" style="color:#0f172a;">-</strong></div>
                <div><span style="color:#64748b; display:block;">ملاحظات الفحص الفيزيائي:</span><span id="lab-chem-disp-physnotes" style="color:#334155;">-</span></div>
              </div>
            </div>
          </div>

          <!-- Step 2: ASTM C1240 Chemical Test Parameters -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                <span>2. نتائج التحاليل والفحوصات الكيميائية (Chemical Test Results — ASTM C1240)</span>
              </h4>
            </div>

            <table class="lab-doc-table" style="margin-top: 10px;">
              <thead>
                <tr style="background: #F1F5F9;">
                  <th style="width: 5%;">#</th>
                  <th style="width: 35%;">نوع الفحص والتحليل الكيميائي</th>
                  <th style="width: 15%;">الوحدة</th>
                  <th style="width: 25%;">الحدود القياسية (ASTM C1240)</th>
                  <th style="width: 20%;">النتيجة المخبرية الفعلية <span class="req">*</span></th>
                </tr>
              </thead>
              <tbody>
                ${tests.map(t => `
                  <tr>
                    <td style="text-align: center; font-weight: 700;">${t.index}</td>
                    <td style="font-weight: 700; color: #1E293B;">${this.esc(t.name)}</td>
                    <td style="text-align: center; color: #64748B;">${this.esc(t.unit)}</td>
                    <td style="text-align: center; font-size: 0.8rem; color: #475569;">
                      ${t.min ? `أدنى: ${t.minLabel} ` : ''} ${t.max ? `أقصى: ${t.maxLabel}` : ''}
                      ${!t.min && !t.max ? 'تسجيل القيمة' : ''}
                    </td>
                    <td>
                      <input type="number" step="0.001" class="lab-sheet-input lab-chem-test-input" data-index="${t.index}" data-name="${this.esc(t.name)}" data-unit="${this.esc(t.unit)}" placeholder="0.00" value="${t.defaultVal || ''}" style="text-align: center; font-weight: 700; color: #1E3A8A;">
                    </td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>

          <!-- Step 3: Dual Approval Gate Decision -->
          <div class="lab-form-card-section" style="border: 2px solid #059669; background: #f0fdf4;">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading" style="color: #065f46;">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
                <span>3. قرار المطابقة الكيميائية والاعتماد النهائي (Dual Approval Gate Decision)</span>
              </h4>
              <span class="lab-section-tag" style="background: #059669; color: #fff;">بوابة الاعتماد المزدوجة</span>
            </div>

            <div style="margin-top: 10px;">
              <label class="lab-label" for="lab-chem-comply-select" style="font-weight: 800; color: #065f46; margin-bottom: 6px; display: block;">
                قرار المطابقة الكيميائية النهائي <span class="req">*</span>
              </label>
              <select id="lab-chem-comply-select" class="lab-select" style="width: 100%; font-size: 1rem; font-weight: 800; padding: 0.65rem; border: 2px solid #059669; border-radius: 8px;" required>
                <option value="COMPLY" selected>✓ مطابق للمواصفات القياسية (COMPLY) — اعتماد رسمي وتأهيل للتوريد والاستلام بالمستودع</option>
                <option value="NOT_COMPLIED">✗ غير مطابق للمواصفات القياسية (NOT COMPLIED) — حظر المادة ومنع توريدها للمستودع</option>
              </select>
              <span style="font-size: 0.8rem; color: #047857; margin-top: 4px; display: block;">
                وفقاً لبروتوكول المصنع: لا يُسمح بإدخال أو استلام أي مادة في المستودع إلا إذا كانت معتمدة فيزيائياً ومطابقة كيميائياً (COMPLIANT_APPROVED).
              </span>
            </div>
          </div>

          <!-- Step 4: Notes & Attachment -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>
                <span>4. ملاحظات الفاحص الكيميائي ومرفق تقرير التحليل</span>
              </h4>
            </div>

            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 14px; margin-top: 10px;">
              <div class="lab-form-group">
                <label class="lab-label" for="lab-chem-notes" style="font-weight: 700;">ملاحظات الفحص والتحليل الكيميائي</label>
                <textarea id="lab-chem-notes" class="lab-textarea" rows="3" placeholder="أدخل أي ملاحظات حول نتائج التحليل الكيميائي أو الشوائب..."></textarea>
              </div>

              <div class="lab-form-group">
                <label class="lab-label" for="lab-chem-manager-name" style="font-weight: 700;">اسم الكيميائي / مدير المعمل المسئول</label>
                <input type="text" id="lab-chem-manager-name" class="lab-sheet-input" value="${this.esc(userName)}" style="font-weight: 700;">

                <label class="lab-label" for="lab-chem-attachment" style="font-weight: 700; margin-top: 10px; display: block;">مرفق تقرير المعمل الكيميائي (اختياري / موصى به)</label>
                <input type="file" id="lab-chem-attachment" class="lab-input" accept=".pdf,image/jpeg,image/png" style="padding: 0.4rem;">
              </div>
            </div>
          </div>

          <!-- Bottom Actions -->
          <div style="display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px;">
            <button type="button" class="lab-btn lab-btn-primary" id="lab-chem-btn-submit" style="background: #059669; border-color: #059669; padding: 0.75rem 2rem; font-size: 1rem; font-weight: 800;">
              ✓ اعتماد وحفظ الفحص الكيميائي (Dual Approval Gate)
            </button>
          </div>
        </div>
      `;

      // Wire event listeners
      const refSelect = document.getElementById('lab-chem-ref-select');
      refSelect?.addEventListener('change', () => {
        const refId = refSelect.value ? parseInt(refSelect.value, 10) : null;
        this.onChemicalReferenceSelected(refId);
      });

      document.getElementById('lab-chem-btn-refresh-refs')?.addEventListener('click', () => {
        this.renderChemicalInspectionForm();
      });

      const handleChemSubmit = () => this.submitChemicalInspectionForm();
      document.getElementById('lab-chem-btn-submit')?.addEventListener('click', handleChemSubmit);
      document.getElementById('lab-chem-btn-submit-top')?.addEventListener('click', handleChemSubmit);
    }

    static onChemicalReferenceSelected(refId) {
      const panel = document.getElementById('lab-chem-ref-details-panel');
      const heroCode = document.getElementById('lab-chem-hero-code');
      if (!refId) {
        this.selectedChemicalReference = null;
        if (panel) panel.style.display = 'none';
        if (heroCode) {
          heroCode.textContent = 'بانتظار اختيار المرجع';
          heroCode.style.background = '#64748b';
        }
        return;
      }

      const r = (this.cachedPendingChemicalReferences || []).find(x => x.id === refId);
      if (!r) return;

      this.selectedChemicalReference = r;
      if (panel) panel.style.display = 'block';
      if (heroCode) {
        heroCode.textContent = r.sampleCode;
        heroCode.style.background = '#047857';
      }

      const setText = (id, txt) => {
        const el = document.getElementById(id);
        if (el) el.textContent = txt;
      };

      setText('lab-chem-disp-code', r.sampleCode);
      setText('lab-chem-disp-name', r.sampleName);
      setText('lab-chem-disp-supplier', r.supplierName || 'غير محدد');
      setText('lab-chem-disp-date', r.sampleReceivedDate ? new Date(r.sampleReceivedDate).toLocaleDateString('ar-EG') : '—');
      setText('lab-chem-disp-qty', `${parseFloat(r.receivedQuantity).toFixed(2)} ${r.unit}`);
      setText('lab-chem-disp-inspector', r.physicalInspectedBy || 'فني المعمل الفيزيائي');
      setText('lab-chem-disp-physnotes', r.physicalNotes || 'تم الفحص الفيزيائي بنجاح وبانتظار الاختبارات الكيميائية');

      const classEl = document.getElementById('lab-chem-disp-classification');
      if (classEl) {
        classEl.textContent = r.materialClassification === 'CHEMICAL' ? 'كيميائية (Chemical)' : 'ميكانيكية (Mechanical)';
        classEl.className = 'lab-badge ' + (r.materialClassification === 'CHEMICAL' ? 'lab-badge-chemical' : 'lab-badge-physical');
      }
    }

    static async submitChemicalInspectionForm() {
      const alertBox = document.getElementById('lab-chem-form-alert');
      const ref = this.selectedChemicalReference;
      if (!ref) {
        if (alertBox) alertBox.innerHTML = '<div class="lab-alert lab-alert-error">يرجى اختيار المرجع المعملي المسجل من المعمل الفيزيائي أولاً.</div>';
        document.getElementById('lab-chem-ref-select')?.focus();
        return;
      }

      const complyVal = document.getElementById('lab-chem-comply-select')?.value || 'COMPLY';
      const notes = document.getElementById('lab-chem-notes')?.value?.trim() || '';
      const managerName = document.getElementById('lab-chem-manager-name')?.value?.trim() || 'فني المعمل الكيميائي';
      const attachInput = document.getElementById('lab-chem-attachment');
      const attachFile = (attachInput && attachInput.files && attachInput.files.length > 0) ? attachInput.files[0] : null;

      const testInputs = document.querySelectorAll('.lab-chem-test-input');
      const testResults = [];
      testInputs.forEach(inp => {
        const idx = inp.getAttribute('data-index');
        const name = inp.getAttribute('data-name');
        const unit = inp.getAttribute('data-unit');
        testResults.push({
          index: parseInt(idx, 10),
          testName: name,
          result: inp.value.trim(),
          unit: unit,
          method: 'ASTM C1240'
        });
      });

      const fd = new FormData();
      fd.append('laboratoryReportId', ref.id);
      fd.append('sampleCode', ref.sampleCode);
      fd.append('complyStatus', complyVal);
      fd.append('analysisResultsJson', JSON.stringify(testResults));
      fd.append('notes', notes);
      fd.append('labManagerName', managerName);
      if (attachFile) {
        fd.append('attachment', attachFile);
      }

      const submitBtns = [document.getElementById('lab-chem-btn-submit'), document.getElementById('lab-chem-btn-submit-top')];
      submitBtns.forEach(b => {
        if (b) {
          b.disabled = true;
          b.textContent = 'جاري حفظ واعتماد الفحص الكيميائي...';
        }
      });

      try {
        const res = await window.ChatApi.submitChemicalInspection(fd);
        if (res && res.success) {
          const isComp = complyVal === 'COMPLY';
          if (alertBox) {
            alertBox.innerHTML = `
              <div class="lab-alert ${isComp ? 'lab-alert-success' : 'lab-alert-error'}" style="padding: 1rem; border-radius: 8px;">
                <div style="font-size: 1.05rem; font-weight: 800; margin-bottom: 4px;">
                  ${isComp ? '✓ تم اعتماد الفحص الكيميائي بنجاح (Dual Approval Gate Passed)!' : '⚠️ تم تسجيل نتيجة الفحص الكيميائي كـ (غير مطابق)!'}
                </div>
                <div>
                  ${isComp 
                    ? `تم اعتماد المرجع المعملي <b>${this.esc(ref.sampleCode)}</b> لمادة <b>${this.esc(ref.sampleName)}</b>. المادة أصبحت الآن مطابقة ومعتمدة رسمياً ومتاحة للاستلام والتوريد في المستودع.` 
                    : `تم تسجيل المرجع المعملي <b>${this.esc(ref.sampleCode)}</b> كـ غير مطابق. تم حظر المادة ومنع توريدها للمستودع نهائياً.`}
                </div>
              </div>
            `;
          }
          window.dispatchEvent(new CustomEvent('facestone:data-changed', { detail: { department: 'laboratory' } }));
          window.dispatchEvent(new CustomEvent('facestone:warehouse-inventory-changed'));
          setTimeout(() => {
            this.renderChemicalInspectionForm();
          }, 2500);
        } else {
          if (alertBox) alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc(res?.message || 'فشل حفظ الفحص الكيميائي')}</div>`;
        }
      } catch (err) {
        console.error('Error submitting chemical inspection:', err);
        if (alertBox) alertBox.innerHTML = `<div class="lab-alert lab-alert-error">حدث خطأ أثناء حفظ الفحص الكيميائي: ${this.esc(err.message || 'خطأ غير معروف')}</div>`;
      } finally {
        submitBtns.forEach(b => {
          if (b) {
            b.disabled = false;
            b.textContent = 'اعتماد وحفظ الفحص الكيميائي (Dual Approval Gate)';
          }
        });
      }
    }

    // ======================================================================
    // 2. SINGLE-PAGE CONTINUOUS LABORATORY FORM (النموذج الموحد على صفحة واحدة)
    // ======================================================================
    static async renderOfficialDocumentForm() {
      if (!this.formContainer) return;

      const isChemical = this.activeSection === 'CHEMICAL';
      if (isChemical) {
        return await this.renderChemicalInspectionForm();
      }

      this.formContainer.innerHTML = '<div class="lab-loading">جاري إعداد نموذج التقرير المعتمد وتوليد رقم العينة...</div>';

      const user = window.ChatApi ? window.ChatApi.getCurrentUser() : null;
      const userName = user ? (user.fullName || user.username || 'مدير المعمل') : 'مدير المعمل';
      const tests = PHYSICAL_TESTS;
      const reportTitle = 'تقرير الفحص والتحاليل الفيزيائية';
      const defaultClassification = 'MECHANICAL';

      // 1. Fetch Central Unique Sample Number & Code from Server
      try {
        const nextRes = await window.ChatApi.getLaboratoryNextSampleNumber();
        if (nextRes && nextRes.success && nextRes.data) {
          this.nextSampleNumber = nextRes.data.sampleNumber || 1;
          this.nextSampleCode = nextRes.data.sampleCode || `SMP-${String(this.nextSampleNumber).padStart(4, '0')}`;
        }
      } catch (err) {
        console.warn('[LaboratoryApp] next sample number lookup failed:', err);
        this.nextSampleNumber = 1;
        this.nextSampleCode = 'SMP-0001';
      }

      const today = new Date().toISOString().slice(0, 10);
      this.selectedQtyFile = null;

      // Render the complete SINGLE PAGE FORM (No slider, no steps, natural scrolling)
      this.formContainer.innerHTML = `
        <div class="lab-single-form-container" dir="rtl">
          
          <!-- Central Reference Hero Banner -->
          <div class="lab-sample-prominent-card">
            <div style="display: flex; align-items: center; gap: 14px; flex-wrap: wrap;">
              <div class="lab-sample-code-pill" id="lab-hero-sample-code">${this.esc(this.nextSampleCode)}</div>
              <div>
                <div style="font-size: 0.82rem; color: #93C5FD; font-weight: 700;">رقم العينة المعتمد — المرجع المركزي الموحد للنظام</div>
                <h3 style="margin: 2px 0 0; color: #FFFFFF; font-size: 1.15rem; font-weight: 800;">${this.esc(reportTitle)} — مواصفة ASTM C1240</h3>
              </div>
            </div>
            <div style="display: flex; gap: 8px; flex-wrap: wrap;">
              <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-submit-form-top">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                <span>حفظ واعتماد التقرير</span>
              </button>
              <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-preview-form-top" style="color: #FFFFFF; border: 1px solid rgba(255,255,255,0.4);">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
                <span>معاينة للطباعة</span>
              </button>
            </div>
          </div>

          <div id="lab-form-alert"></div>

          <!-- SECTION 1: بيانات العينة وتصنيف المادة -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
                <span>1. بيانات العينة الأساسية وتصنيف المادة</span>
              </h4>
              <span class="lab-section-tag">بيانات إلزامية</span>
            </div>

            <!-- Header Banner -->
            <div class="lab-sheet-header-banner" style="border-radius: 8px; margin-bottom: 18px;">
              <div class="lab-sheet-header-brand">
                <span class="lab-sheet-header-brand-name">FACESTONE</span>
                <span class="lab-sheet-header-brand-sub">full concrete solutions</span>
              </div>
              <div class="lab-sheet-header-logo-wrap">
                <img src="/images/facestone-logo-transparent.png" alt="FaceStone Logo" class="lab-sheet-header-shield">
              </div>
            </div>

            <!-- Material Classification (Requirement 3) -->
            <div style="margin-bottom: 20px;">
              <label class="lab-label" style="font-weight: 800; color: #0F2A5C; margin-bottom: 6px; display: block;">
                تصنيف المادة الخام <span class="req">*</span>
                <span style="font-weight: 400; color: #64748B; font-size: 0.82rem; margin-right: 6px;">(يتم تسجيل المادة تلقائياً في دليل خامات المستودع دون زيادة الرصيد المخزني)</span>
              </label>
              <div class="lab-classification-grid">
                <label class="lab-classification-card ${defaultClassification === 'MECHANICAL' ? 'is-selected' : ''}" id="lab-class-card-mech">
                  <input type="radio" name="lab-mat-classification" id="lab-radio-mech" value="MECHANICAL" ${defaultClassification === 'MECHANICAL' ? 'checked' : ''}>
                  <div>
                    <div class="lab-class-title">⚙️ ميكانيكية (Mechanical)</div>
                    <div class="lab-class-desc">مواد خام وخامات تشغيلية ميكانيكية تُضاف لدليل أصناف المستودع</div>
                  </div>
                </label>
                <label class="lab-classification-card ${defaultClassification === 'CHEMICAL' ? 'is-selected' : ''}" id="lab-class-card-chem">
                  <input type="radio" name="lab-mat-classification" id="lab-radio-chem" value="CHEMICAL" ${defaultClassification === 'CHEMICAL' ? 'checked' : ''}>
                  <div>
                    <div class="lab-class-title">🧪 كيميائية (Chemical)</div>
                    <div class="lab-class-desc">مواد كيميائية وإضافات سائلة أو مساحيق تخضع للتحاليل الكيميائية</div>
                  </div>
                </label>
              </div>
            </div>

            <!-- Basic Sample Info Table -->
            <table class="lab-doc-table">
              <tbody>
                <tr>
                  <td style="width: 20%; font-weight: 800; background: #F8FAFC;">مسمى العينة / المادة <span class="req">*</span></td>
                  <td style="width: 30%;">
                    <input type="text" class="lab-sheet-input" id="lab-in-sample-name" value="أسمنت بورتلاندي" placeholder="مثال: أسمنت بورتلاندي أو سيليكا باودر" required>
                  </td>
                  <td style="width: 20%; font-weight: 800; background: #F8FAFC;">رقم العينة (Sample No) <span class="req">*</span></td>
                  <td style="width: 30%;">
                    <input type="text" class="lab-sheet-input" id="lab-in-sample-no" value="${this.esc(this.nextSampleCode)}" readonly style="font-weight: 800; color: #1E3A8A; background: #EFF6FF;" title="رقم تسلسلي موحد وفريد مثبت في قاعدة البيانات">
                  </td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F8FAFC;">وصف العينة <span class="req">*</span></td>
                  <td>
                    <input type="text" class="lab-sheet-input" id="lab-in-sample-desc" value="بودرة رمادية اللون" placeholder="وصف حالة العينة أو اللون" required>
                  </td>
                  <td style="font-weight: 800; background: #F8FAFC;">تاريخ استلام العينة <span class="req">*</span></td>
                  <td>
                    <input type="date" class="lab-sheet-input" id="lab-in-received-date" value="${today}" required>
                  </td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F8FAFC;">تاريخ إصدار التقرير <span class="req">*</span></td>
                  <td>
                    <input type="date" class="lab-sheet-input" id="lab-in-issue-date" value="${today}" required>
                  </td>
                  <td style="font-weight: 800; background: #F8FAFC;">القسم المسؤول</td>
                  <td style="font-weight: 700; color: #0F2A5C;">
                    ${this.esc(LAB_SECTION_AR[this.activeSection] || this.activeSection)}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- SECTION 2: الكميات والمستندات (Requirements 5 & 7) -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
                <span>2. الكميات المستلمة وإجمالي الوزن المختبر والمستندات</span>
              </h4>
              <span class="lab-section-tag">أوزان بالطن + تأكيد المستندات</span>
            </div>

            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 20px;">
              <div class="lab-field">
                <label class="lab-label" style="font-weight: 800;">الكمية المستلمة (بالطن) <span class="req">*</span></label>
                <input type="number" step="any" min="0" class="lab-input" id="lab-in-received-qty" value="1.000" placeholder="0.000" required>
                <span class="lab-hint">الكمية الإجمالية للمادة المستلمة بموجب إشعار التوريد / النقل</span>
              </div>
              <div class="lab-field">
                <label class="lab-label" style="font-weight: 800; color: #1D4ED8;">إجمالي وزن المواد التي تم اختبارها (طن) <span class="req">*</span></label>
                <input type="number" step="any" min="0" class="lab-input" id="lab-in-tested-weight" value="1.000" placeholder="0.000" style="border-color: #93C5FD; font-weight: 700;" required>
                <span class="lab-hint">الوزن الفعلي للمواد التي خضعت للاختبار والتحليل المخبري بدقة عشرية</span>
              </div>
            </div>

            <!-- Quantity Confirmation Document Upload (Requirement 7) -->
            <div>
              <label class="lab-label" style="font-weight: 800; color: #0F2A5C; margin-bottom: 8px; display: block;">
                مستند تأكيد الكمية المستلمة (وثيقة إثبات الكمية المستلمة)
              </label>
              <div class="lab-upload-dropzone" id="lab-qty-dropzone">
                <svg class="lab-upload-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
                <div style="font-weight: 700; color: #1E293B; margin-bottom: 4px;">اضغط لاختيار مستند تأكيد الكمية أو اسحب الملف وأفلته هنا</div>
                <div style="font-size: 0.78rem; color: #64748B;">يدعم ملفات PDF والصور (JPG, PNG) بحد أقصى 15 ميجابايت</div>
                <input type="file" id="lab-in-qty-file" accept=".pdf,image/jpeg,image/png,image/webp" style="display: none;">
              </div>
              <div id="lab-qty-file-preview" style="display: none;" class="lab-upload-file-info">
                <div style="display: flex; align-items: center; gap: 10px;">
                  <span style="font-size: 1.3rem;">📄</span>
                  <div>
                    <div id="lab-qty-file-name" style="font-weight: 700; font-size: 0.85rem; color: #0F172A;"></div>
                    <div id="lab-qty-file-size" style="font-size: 0.75rem; color: #64748B;"></div>
                  </div>
                </div>
                <button type="button" class="lab-btn lab-btn-ghost" id="lab-qty-file-remove" style="color: #DC2626; padding: 4px 10px; font-size: 0.75rem;">إلغاء الملف</button>
              </div>
            </div>
          </div>

          <!-- SECTION 3: نتائج الفحوصات والتحاليل المعملية ASTM C1240 -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>
                <span>3. نتائج الفحوصات والتحاليل المعملية — مواصفة ASTM C1240</span>
              </h4>
              <span class="lab-section-tag">مطابقة قياسية</span>
            </div>

            <table class="lab-doc-table" id="lab-test-results-table">
              <thead>
                <tr>
                  <th class="col-idx">م</th>
                  <th class="col-name" style="text-align: center;">الاختبار</th>
                  <th class="col-val">نتيجة الاختبار</th>
                  <th class="col-unit">وحدة القياس</th>
                  <th class="col-min">Min</th>
                  <th class="col-max">Max</th>
                  <th class="col-method">طريقة الاختبار</th>
                </tr>
              </thead>
              <tbody>
                ${tests.map(t => `
                  <tr data-test-index="${t.index}">
                    <td class="col-idx">${t.index}</td>
                    <td class="col-name">${this.esc(t.name)}</td>
                    <td class="col-val">
                      <input type="${t.isText ? 'text' : 'number'}"
                             step="any"
                             class="lab-sheet-input lab-test-input"
                             id="lab-test-val-${t.index}"
                             data-index="${t.index}"
                             data-min="${t.min != null ? t.min : ''}"
                             data-max="${t.max != null ? t.max : ''}"
                             data-name="${this.esc(t.name)}"
                             data-unit="${this.esc(t.unit)}"
                             value="${this.esc(t.defaultVal)}"
                             placeholder="أدخل النتيجة">
                    </td>
                    <td class="col-unit">${this.esc(t.unit)}</td>
                    <td class="col-min">${this.esc(t.minLabel)}</td>
                    <td class="col-max">${this.esc(t.maxLabel)}</td>
                    <td class="col-method">
                      <input type="text" class="lab-sheet-input" id="lab-test-method-${t.index}" value="${this.esc(t.method)}">
                    </td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>

          <!-- SECTION 4: الملاحظات الفنية وقرار المطابقة النهائي -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
                <span>4. الملاحظات الفنية وقرار المطابقة النهائي</span>
              </h4>
            </div>

            <div style="margin-bottom: 20px;">
              <label class="lab-label" style="font-weight: 800;">الملاحظات الفنية:</label>
              <textarea class="lab-textarea" id="lab-in-notes" rows="3">مطابقة لمواصفة ASTM C1240</textarea>
            </div>

            <div>
              <label class="lab-label" style="font-weight: 800; margin-bottom: 8px; display: block;">قرار المطابقة لمواصفات الجودة:</label>
              <div class="lab-decision-selection">
                <label class="lab-decision-option comply-tag">
                  <input type="radio" name="lab-doc-comply" id="lab-radio-comply" value="COMPLY" checked>
                  <span>☑ Comply (مطابق لمواصفة ASTM C1240)</span>
                </label>
                <label class="lab-decision-option notcomply-tag">
                  <input type="radio" name="lab-doc-comply" id="lab-radio-notcomply" value="NOT_COMPLIED">
                  <span>☒ Not Complied (غير مطابق للمواصفات)</span>
                </label>
              </div>
            </div>
          </div>

          <!-- SECTION 5: بيانات واعتماد مدير المعمل وإجراءات الحفظ والطباعة -->
          <div class="lab-form-card-section">
            <div class="lab-section-header-row">
              <h4 class="lab-section-heading">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
                <span>5. بيانات الاعتماد وإصدار التقرير</span>
              </h4>
            </div>

            <div class="lab-field" style="margin-bottom: 18px;">
              <label class="lab-label">المورد / المصدر (اختياري)</label>
              <input type="text" class="lab-input" id="lab-in-supplier" placeholder="اسم المورد أو جهة التوريد">
            </div>

            <!-- Manager Table -->
            <table class="lab-manager-table" style="margin-bottom: 24px;">
              <thead><tr><th colspan="2">مدير المعمل / الفاحص المعتمد</th></tr></thead>
              <tbody>
                <tr>
                  <td class="label-cell">الاسم</td>
                  <td><input type="text" class="lab-sheet-input" id="lab-in-manager-name" value="${this.esc(userName)}" style="text-align: right;"></td>
                </tr>
                <tr>
                  <td class="label-cell">التوقيع</td>
                  <td><input type="text" class="lab-sheet-input" id="lab-in-manager-sig" value="${this.esc(userName)}" style="text-align: right; font-family: monospace;"></td>
                </tr>
                <tr>
                  <td class="label-cell">التاريخ</td>
                  <td><input type="date" class="lab-sheet-input" id="lab-in-manager-date" value="${today}"></td>
                </tr>
              </tbody>
            </table>

            <!-- Actions Bar at Bottom of Single Page -->
            <div style="display: flex; gap: 12px; justify-content: flex-start; align-items: center; flex-wrap: wrap; padding-top: 14px; border-top: 1px solid #E2E8F0;">
              <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-submit-form" style="padding: 12px 32px; font-size: 0.95rem; font-weight: 800;">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                <span>حفظ واعتماد التقرير وتسجيل المادة</span>
              </button>
              <button type="button" class="lab-btn lab-btn-outline" id="lab-btn-preview-form" style="padding: 12px 24px; font-size: 0.9rem;">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
                <span>معاينة للطباعة (Print Preview)</span>
              </button>
              <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-reset-form">إعادة تعيين النموذج</button>
            </div>
          </div>
        </div>
      `;

      this.bindFormEvents();
    }

    static bindFormEvents() {
      // Classification selection cards (Requirement 3)
      const cardMech = document.getElementById('lab-class-card-mech');
      const cardChem = document.getElementById('lab-class-card-chem');
      const radioMech = document.getElementById('lab-radio-mech');
      const radioChem = document.getElementById('lab-radio-chem');

      const updateClassCards = (isChem) => {
        if (cardMech) cardMech.classList.toggle('is-selected', !isChem);
        if (cardChem) cardChem.classList.toggle('is-selected', isChem);
      };

      cardMech?.addEventListener('click', () => {
        if (radioMech) radioMech.checked = true;
        updateClassCards(false);
      });
      cardChem?.addEventListener('click', () => {
        if (radioChem) radioChem.checked = true;
        updateClassCards(true);
      });
      radioMech?.addEventListener('change', () => updateClassCards(false));
      radioChem?.addEventListener('change', () => updateClassCards(true));

      // Quantity confirmation document upload (Requirement 7)
      const dropzone = document.getElementById('lab-qty-dropzone');
      const fileInput = document.getElementById('lab-in-qty-file');
      const previewBox = document.getElementById('lab-qty-file-preview');
      const nameEl = document.getElementById('lab-qty-file-name');
      const sizeEl = document.getElementById('lab-qty-file-size');
      const removeBtn = document.getElementById('lab-qty-file-remove');

      dropzone?.addEventListener('click', () => fileInput?.click());

      const handleFile = (file) => {
        if (!file) return;
        const validExts = ['.pdf', '.jpg', '.jpeg', '.png', '.webp'];
        const ext = '.' + file.name.split('.').pop().toLowerCase();
        if (!validExts.includes(ext)) {
          alert('الملف يجب أن يكون بصيغة PDF أو صورة (JPG / PNG).');
          return;
        }
        if (file.size > 15 * 1024 * 1024) {
          alert('حجم الملف يتجاوز الحد الأقصى المسموح به (15 ميجابايت).');
          return;
        }

        this.selectedQtyFile = file;
        if (nameEl) nameEl.textContent = file.name;
        if (sizeEl) sizeEl.textContent = (file.size / (1024 * 1024)).toFixed(2) + ' ميجابايت';
        if (dropzone) dropzone.style.display = 'none';
        if (previewBox) previewBox.style.display = 'flex';
      };

      fileInput?.addEventListener('change', (e) => {
        if (e.target.files && e.target.files.length > 0) {
          handleFile(e.target.files[0]);
        }
      });

      removeBtn?.addEventListener('click', () => {
        this.selectedQtyFile = null;
        if (fileInput) fileInput.value = '';
        if (previewBox) previewBox.style.display = 'none';
        if (dropzone) dropzone.style.display = 'block';
      });

      // Drag and drop
      dropzone?.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropzone.classList.add('is-dragover');
      });
      dropzone?.addEventListener('dragleave', () => dropzone.classList.remove('is-dragover'));
      dropzone?.addEventListener('drop', (e) => {
        e.preventDefault();
        dropzone.classList.remove('is-dragover');
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
          handleFile(e.dataTransfer.files[0]);
        }
      });

      // Live test evaluation for ASTM C1240 compliance
      const testInputs = document.querySelectorAll('.lab-test-input');
      testInputs.forEach(input => {
        input.addEventListener('input', () => this.evaluateCompliance());
      });

      // Submit, Preview, Reset buttons
      document.getElementById('lab-btn-submit-form')?.addEventListener('click', () => this.submitOfficialForm());
      document.getElementById('lab-btn-submit-form-top')?.addEventListener('click', () => this.submitOfficialForm());
      document.getElementById('lab-btn-preview-form')?.addEventListener('click', () => this.previewCurrentForm());
      document.getElementById('lab-btn-preview-form-top')?.addEventListener('click', () => this.previewCurrentForm());
      document.getElementById('lab-btn-reset-form')?.addEventListener('click', () => this.renderOfficialDocumentForm());
    }

    static evaluateCompliance() {
      const testInputs = document.querySelectorAll('.lab-test-input');
      let anyViolation = false;

      testInputs.forEach(input => {
        const valStr = input.value.trim();
        if (!valStr || input.type === 'text') {
          input.classList.remove('is-violated', 'is-valid');
          return;
        }

        const val = parseFloat(valStr);
        if (isNaN(val)) return;

        const minStr = input.getAttribute('data-min');
        const maxStr = input.getAttribute('data-max');
        const min = minStr !== '' ? parseFloat(minStr) : null;
        const max = maxStr !== '' ? parseFloat(maxStr) : null;

        let violated = false;
        if (min !== null && val < min) violated = true;
        if (max !== null && val > max) violated = true;

        if (violated) {
          input.classList.add('is-violated');
          input.classList.remove('is-valid');
          anyViolation = true;
        } else if (min !== null || max !== null) {
          input.classList.add('is-valid');
          input.classList.remove('is-violated');
        }
      });

      const radioComply = document.getElementById('lab-radio-comply');
      const radioNotComply = document.getElementById('lab-radio-notcomply');

      if (anyViolation) {
        if (radioNotComply) radioNotComply.checked = true;
      } else {
        if (radioComply) radioComply.checked = true;
      }
    }

    static async submitOfficialForm() {
      const alertBox = document.getElementById('lab-form-alert');
      const submitBtn = document.getElementById('lab-btn-submit-form');
      const submitBtnTop = document.getElementById('lab-btn-submit-form-top');
      const val = (id) => (document.getElementById(id)?.value || '').trim();

      const sampleName = val('lab-in-sample-name');
      const sampleDesc = val('lab-in-sample-desc');
      const classification = document.querySelector('input[name="lab-mat-classification"]:checked')?.value || 'MECHANICAL';
      const receivedDate = val('lab-in-received-date');
      const issueDate = val('lab-in-issue-date');
      const receivedQtyStr = val('lab-in-received-qty');
      const testedWeightStr = val('lab-in-tested-weight');

      const managerName = val('lab-in-manager-name');
      const managerSig = val('lab-in-manager-sig');
      const managerDate = val('lab-in-manager-date');
      const notes = val('lab-in-notes');
      const complyVal = document.querySelector('input[name="lab-doc-comply"]:checked')?.value || 'COMPLY';
      const supplier = val('lab-in-supplier');

      let error = null;
      let invalidEl = null;

      if (!sampleName) {
        error = 'مسمى العينة / المادة مطلوب.';
        invalidEl = document.getElementById('lab-in-sample-name');
      } else if (!sampleDesc) {
        error = 'وصف العينة مطلوب.';
        invalidEl = document.getElementById('lab-in-sample-desc');
      } else if (!receivedDate) {
        error = 'تاريخ استلام العينة مطلوب.';
        invalidEl = document.getElementById('lab-in-received-date');
      } else if (!issueDate) {
        error = 'تاريخ إصدار التقرير مطلوب.';
        invalidEl = document.getElementById('lab-in-issue-date');
      } else if (!receivedQtyStr || isNaN(parseFloat(receivedQtyStr)) || parseFloat(receivedQtyStr) < 0) {
        error = 'الكمية المستلمة (بالطن) غير صالحة.';
        invalidEl = document.getElementById('lab-in-received-qty');
      } else if (!testedWeightStr || isNaN(parseFloat(testedWeightStr)) || parseFloat(testedWeightStr) < 0) {
        error = 'إجمالي وزن المواد التي تم اختبارها (طن) مطلوب ولا يمكن أن يكون سالباً.';
        invalidEl = document.getElementById('lab-in-tested-weight');
      }

      if (error) {
        document.querySelectorAll('.lab-sheet-input, .lab-input').forEach(el => el.classList.remove('is-invalid'));
        if (invalidEl) {
          invalidEl.classList.add('is-invalid');
          invalidEl.focus();
        }
        if (alertBox) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc(error)}</div>`;
          alertBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
        return;
      }

      // Collect test results array
      const testInputs = document.querySelectorAll('.lab-test-input');
      const testResults = [];
      testInputs.forEach(inp => {
        const idx = inp.getAttribute('data-index');
        const name = inp.getAttribute('data-name');
        const unit = inp.getAttribute('data-unit');
        const method = val(`lab-test-method-${idx}`);
        testResults.push({
          index: parseInt(idx, 10),
          testName: name,
          result: inp.value.trim(),
          unit: unit,
          method: method
        });
      });

      const fd = new FormData();
      fd.append('laboratorySection', this.activeSection);
      fd.append('sampleName', sampleName);
      fd.append('materialClassification', classification);
      fd.append('sampleDescription', sampleDesc);
      fd.append('sampleCode', this.nextSampleCode);
      fd.append('sampleReceivedDate', receivedDate ? new Date(receivedDate).toISOString() : new Date().toISOString());
      fd.append('reportIssueDate', issueDate ? new Date(issueDate).toISOString() : new Date().toISOString());
      fd.append('receivedQuantity', receivedQtyStr);
      fd.append('testedMaterialWeight', testedWeightStr);
      fd.append('labManagerName', managerName);
      fd.append('labManagerSignature', managerSig);
      fd.append('labManagerDate', managerDate ? new Date(managerDate).toISOString() : new Date().toISOString());
      fd.append('complyStatus', complyVal);
      fd.append('analysisResultsJson', JSON.stringify(testResults));
      fd.append('notes', notes);
      fd.append('inspectionDecision', complyVal === 'COMPLY' ? 'ACCEPTED' : 'REJECTED');
      fd.append('supplierName', supplier);
      fd.append('rawMaterialDescription', sampleName);
      fd.append('analysisDateTime', new Date().toISOString());

      if (this.selectedQtyFile) {
        fd.append('quantityConfirmationDocument', this.selectedQtyFile);
      }

      const setButtonsLoading = (isLoading) => {
        [submitBtn, submitBtnTop].forEach(btn => {
          if (!btn) return;
          btn.disabled = isLoading;
          if (isLoading) {
            btn.innerHTML = '<span>جاري الحفظ والاعتماد...</span>';
          } else {
            btn.innerHTML = `
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
              <span>حفظ واعتماد التقرير وتسجيل المادة</span>`;
          }
        });
      };

      setButtonsLoading(true);

      try {
        const res = await window.ChatApi.submitLaboratoryRawMaterialReport(fd);
        if (res && res.success) {
          const report = res.data;
          const assignedCode = report ? (report.sampleCode || `SMP-${report.sampleNumber}`) : this.nextSampleCode;

          this.isDirty = false;

          alertBox.innerHTML = `
            <div class="lab-alert lab-alert-success" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px;">
              <div>
                <b>✓ تم حفظ واعتماد التقرير بنجاح وتسجيل الصنف في دليل المستودع!</b>
                <div>رقم العينة المثبت: <b>${this.esc(assignedCode)}</b> | تصنيف المادة: <b>${classification === 'CHEMICAL' ? 'كيميائية' : 'ميكانيكية'}</b> | الحالة: <b>${complyVal === 'COMPLY' ? 'مطابق لمواصفة ASTM C1240' : 'غير مطابق'}</b></div>
              </div>
              <div style="display: flex; gap: 8px;">
                <button type="button" class="lab-btn lab-btn-primary" id="lab-btn-print-just-saved" style="padding: 7px 16px; font-size: 0.82rem;">
                  🖨️ طباعة التقرير الرسمي
                </button>
                <button type="button" class="lab-btn lab-btn-ghost" id="lab-btn-goto-history" style="padding: 7px 16px; font-size: 0.82rem;">
                  سجل الفحوصات &larr;
                </button>
              </div>
            </div>
          `;

          document.getElementById('lab-btn-print-just-saved')?.addEventListener('click', () => {
            this.openPrintModal(report);
          });
          document.getElementById('lab-btn-goto-history')?.addEventListener('click', () => {
            this.switchTab('history');
          });

          // Refresh sample number for next entry
          const nextRes = await window.ChatApi.getLaboratoryNextSampleNumber();
          if (nextRes && nextRes.success && nextRes.data) {
            this.nextSampleNumber = nextRes.data.sampleNumber || (report.sampleNumber + 1);
            this.nextSampleCode = nextRes.data.sampleCode || `SMP-${String(this.nextSampleNumber).padStart(4, '0')}`;
            const heroEl = document.getElementById('lab-hero-sample-code');
            const sampleNoInp = document.getElementById('lab-in-sample-no');
            if (heroEl) heroEl.textContent = this.nextSampleCode;
            if (sampleNoInp) sampleNoInp.value = this.nextSampleCode;
          }

          // Pre-load history
          this.loadHistoryList();
        } else {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل حفظ تقرير الفحص.')}</div>`;
        }
      } catch (err) {
        alertBox.innerHTML = `<div class="lab-alert lab-alert-error">تعذر الاتصال بالخادم: ${this.esc(err.message || '')}</div>`;
      } finally {
        setButtonsLoading(false);
        alertBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }

    static previewCurrentForm() {
      const val = (id) => (document.getElementById(id)?.value || '').trim();
      const isChemical = this.activeSection === 'CHEMICAL';
      const tests = isChemical ? CHEMICAL_TESTS : PHYSICAL_TESTS;

      const testInputs = document.querySelectorAll('.lab-test-input');
      const testResults = [];
      testInputs.forEach(inp => {
        const idx = inp.getAttribute('data-index');
        const name = inp.getAttribute('data-name');
        const unit = inp.getAttribute('data-unit');
        const method = val(`lab-test-method-${idx}`);
        testResults.push({
          index: parseInt(idx, 10),
          testName: name,
          result: inp.value.trim(),
          unit: unit,
          method: method
        });
      });

      const classification = document.querySelector('input[name="lab-mat-classification"]:checked')?.value || 'MECHANICAL';

      const dummyReport = {
        sampleNumber: this.nextSampleNumber,
        sampleCode: this.nextSampleCode,
        materialClassification: classification,
        sampleName: val('lab-in-sample-name') || 'أسمنت بورتلاندي',
        sampleDescription: val('lab-in-sample-desc') || 'بودرة رمادية اللون',
        receivedQuantity: parseFloat(val('lab-in-received-qty')) || 1.0,
        testedMaterialWeight: parseFloat(val('lab-in-tested-weight')) || 1.0,
        quantityConfirmationFileName: this.selectedQtyFile ? this.selectedQtyFile.name : null,
        sampleReceivedDate: val('lab-in-received-date') || new Date().toISOString(),
        reportIssueDate: val('lab-in-issue-date') || new Date().toISOString(),
        laboratorySection: this.activeSection,
        complyStatus: document.querySelector('input[name="lab-doc-comply"]:checked')?.value || 'COMPLY',
        notes: val('lab-in-notes') || 'مطابقة لمواصفة ASTM C1240',
        labManagerName: val('lab-in-manager-name') || 'مدير المعمل',
        labManagerSignature: val('lab-in-manager-sig') || 'معتمد',
        labManagerDate: val('lab-in-manager-date') || new Date().toISOString(),
        supplierName: val('lab-in-supplier'),
        analysisResultsJson: JSON.stringify(testResults)
      };

      this.openPrintModal(dummyReport);
    }

    // ======================================================================
    // 1. OFFICIAL PRINT & PREVIEW SYSTEM (Requirement 1)
    // ======================================================================
    static generatePrintSheetHtml(report) {
      const isChemical = (report.laboratorySection || this.activeSection) === 'CHEMICAL';
      const titleAr = isChemical ? 'تقرير الفحص والتحاليل الكيميائية' : 'تقرير الفحص والتحاليل الفيزيائية';
      const tests = isChemical ? CHEMICAL_TESTS : PHYSICAL_TESTS;

      let parsedResults = [];
      try {
        if (report.analysisResultsJson) {
          parsedResults = JSON.parse(report.analysisResultsJson);
        }
      } catch (e) {
        console.warn('Failed to parse analysis results JSON:', e);
      }

      const rows = tests.map(t => {
        const found = Array.isArray(parsedResults) ? parsedResults.find(p => p.index === t.index) : null;
        const resVal = found ? found.result : (t.defaultVal || '-');
        const methodVal = found ? found.method : t.method;
        return {
          ...t,
          result: resVal,
          method: methodVal
        };
      });

      const rDate = report.sampleReceivedDate ? report.sampleReceivedDate.slice(0, 10) : '';
      const iDate = report.reportIssueDate ? report.reportIssueDate.slice(0, 10) : '';
      const mDate = report.labManagerDate ? report.labManagerDate.slice(0, 10) : '';
      const isComply = (report.complyStatus || 'COMPLY') === 'COMPLY';

      const sampleRefCode = report.sampleCode || `SMP-${String(report.sampleNumber || 1).padStart(4, '0')}`;
      const classificationAr = (report.materialClassification === 'CHEMICAL') ? 'كيميائية (Chemical)' : 'ميكانيكية (Mechanical)';
      const qtyDocText = report.quantityConfirmationFileName 
        ? report.quantityConfirmationFileName 
        : (report.quantityConfirmationPath ? 'مستند رسمي مرفق ومحفوظ بالنظام' : 'غير مرفق');

      return `
        <div class="lab-report-sheet" dir="rtl" style="box-shadow: none; border: 1.5px solid #0f172a; background: #ffffff; color: #000000; font-family: 'Cairo', 'Segoe UI', Tahoma, sans-serif; padding: 0; margin: 0 auto; width: 100%; max-width: 820px;">
          
          <!-- Official Company Header Banner -->
          <div class="lab-sheet-header-banner" style="display: flex; align-items: center; justify-content: space-between; background: #0f2a5c; color: #ffffff; padding: 14px 20px; border-top-left-radius: 4px; border-top-right-radius: 4px;">
            <div class="lab-sheet-header-brand" style="text-align: right;">
              <span class="lab-sheet-header-brand-name" style="font-size: 1.45rem; font-weight: 900; letter-spacing: 1px; display: block; color: #ffffff;">FACESTONE</span>
              <span class="lab-sheet-header-brand-sub" style="font-size: 0.78rem; color: #94a3b8; text-transform: uppercase;">full concrete solutions</span>
            </div>
            <div style="text-align: center;">
              <div style="font-size: 0.95rem; font-weight: 800; color: #ffffff;">قسم المعمل — Laboratory Department</div>
              <div style="font-size: 0.75rem; color: #cbd5e1;">وحدة الفحص والتحليل والمطابقة لمواصفات الجودة</div>
            </div>
            <div class="lab-sheet-header-logo-wrap" style="background: #ffffff; padding: 4px; border-radius: 6px;">
              <img src="/images/facestone-logo-transparent.png" alt="FaceStone Logo" style="height: 44px; width: auto; object-fit: contain;">
            </div>
          </div>

          <!-- Document Title & Central Sample Number Badge -->
          <div style="padding: 16px 20px 8px 20px; text-align: center; border-bottom: 2px solid #0f2a5c;">
            <h2 style="margin: 0 0 6px 0; font-size: 1.3rem; font-weight: 900; color: #0f2a5c;">${this.esc(titleAr)}</h2>
            <div style="display: inline-block; background: #fef08a; border: 1.5px solid #ca8a04; border-radius: 6px; padding: 6px 16px; margin-top: 4px;">
              <span style="font-size: 0.85rem; font-weight: 800; color: #854d0e;">رقم العينة المعتمد:</span>
              <span style="font-size: 1.15rem; font-weight: 900; color: #0f172a; margin-right: 6px; font-family: monospace;">${this.esc(sampleRefCode)}</span>
            </div>
          </div>

          <!-- Sheet Body -->
          <div class="lab-sheet-body" style="padding: 18px 20px;">
            
            <!-- 1. بيانات العينة والمادة -->
            <h3 style="margin: 0 0 8px 0; font-size: 0.95rem; font-weight: 800; color: #0f2a5c; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px;">
              1. بيانات العينة وتصنيف المادة
            </h3>
            <table class="lab-doc-table" style="width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 0.85rem;">
              <tbody>
                <tr>
                  <td style="width: 22%; font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">مسمى العينة / المادة</td>
                  <td style="width: 28%; border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 800;">${this.esc(report.sampleName || 'أسمنت بورتلاندي')}</td>
                  <td style="width: 22%; font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">رقم العينة (Sample No)</td>
                  <td style="width: 28%; border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 900; color: #0f2a5c; font-family: monospace;">${this.esc(sampleRefCode)}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">تصنيف المادة الخام</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 800; color: #1e3a8a;">${this.esc(classificationAr)}</td>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">وصف العينة</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px;">${this.esc(report.sampleDescription || 'بودرة رمادية اللون')}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">تاريخ استلام العينة</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px;">${this.esc(rDate)}</td>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">تاريخ إصدار التقرير</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px;">${this.esc(iDate)}</td>
                </tr>
              </tbody>
            </table>

            <!-- 2. الكميات والأوزان وتأكيد المستندات -->
            <h3 style="margin: 0 0 8px 0; font-size: 0.95rem; font-weight: 800; color: #0f2a5c; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px;">
              2. الكميات وإجمالي الوزن المختبر والمستندات
            </h3>
            <table class="lab-doc-table" style="width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 0.85rem;">
              <tbody>
                <tr>
                  <td style="width: 25%; font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">الكمية المستلمة (بالطن)</td>
                  <td style="width: 25%; border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 800;">${this.esc(String(report.receivedQuantity ?? 1.0))} طن</td>
                  <td style="width: 25%; font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">إجمالي وزن المواد المختبرة (طن)</td>
                  <td style="width: 25%; border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 900; color: #1e3a8a;">${this.esc(String(report.testedMaterialWeight ?? report.receivedQuantity ?? 1.0))} طن</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">مستند تأكيد الكمية المستلمة</td>
                  <td colspan="3" style="border: 1px solid #cbd5e1; padding: 6px 8px;">
                    <span style="font-weight: 700; color: #334155;">${this.esc(qtyDocText)}</span>
                  </td>
                </tr>
              </tbody>
            </table>

            <!-- 3. نتائج التحليل المخبري -->
            <h3 style="margin: 0 0 8px 0; font-size: 0.95rem; font-weight: 800; color: #0f2a5c; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px;">
              3. نتائج القياسات والتحاليل المعملية — مطابقة لمواصفة ASTM C1240
            </h3>
            <table class="lab-doc-table" style="width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 0.82rem;">
              <thead>
                <tr style="background: #F1F5F9;">
                  <th style="border: 1px solid #cbd5e1; padding: 6px 4px; width: 6%; text-align: center;">م</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 8px; width: 34%; text-align: center;">الاختبار المطلوب</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 8px; width: 16%; text-align: center;">نتيجة الاختبار</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 6px; width: 12%; text-align: center;">الوحدة</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 6px; width: 10%; text-align: center;">Min</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 6px; width: 10%; text-align: center;">Max</th>
                  <th style="border: 1px solid #cbd5e1; padding: 6px 6px; width: 12%; text-align: center;">طريقة الفحص</th>
                </tr>
              </thead>
              <tbody>
                ${rows.map(t => `
                  <tr>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 4px; text-align: center;">${t.index}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 8px; font-weight: 700;">${this.esc(t.name)}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 8px; text-align: center; font-weight: 800; color: #0f2a5c;">${this.esc(String(t.result))}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 6px; text-align: center;">${this.esc(t.unit)}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 6px; text-align: center;">${this.esc(t.minLabel)}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 6px; text-align: center;">${this.esc(t.maxLabel)}</td>
                    <td style="border: 1px solid #cbd5e1; padding: 5px 6px; text-align: center;">${this.esc(t.method || 'ASTM C1240')}</td>
                  </tr>
                `).join('')}
              </tbody>
            </table>

            <!-- 4. الملاحظات وقرار المطابقة -->
            <div style="border: 1px solid #cbd5e1; border-radius: 6px; padding: 10px 14px; margin-bottom: 16px; background: #fafafa;">
              <div style="font-weight: 800; font-size: 0.85rem; color: #0f2a5c; margin-bottom: 4px;">الملاحظات الفنية:</div>
              <div style="font-size: 0.9rem; font-weight: 700; color: #1e293b;">
                ${this.esc(report.notes || 'مطابقة لمواصفة ASTM C1240')}
              </div>
            </div>

            <!-- Decision Box -->
            <div style="display: flex; gap: 20px; align-items: center; justify-content: center; margin-bottom: 16px; padding: 8px; background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 6px;">
              <span style="font-weight: 800; font-size: 0.9rem; color: #0f2a5c;">قرار المطابقة للمواصفة القياسية:</span>
              <div style="display: flex; gap: 14px; align-items: center;">
                <span style="font-weight: 800; font-size: 0.95rem; color: ${isComply ? '#15803d' : '#64748b'};">
                  ${isComply ? '☑' : '☐'} Comply (مطابق)
                </span>
                <span style="font-weight: 800; font-size: 0.95rem; color: ${!isComply ? '#b91c1c' : '#64748b'};">
                  ${!isComply ? '☑' : '☐'} Not Complied (غير مطابق)
                </span>
              </div>
            </div>

            <!-- 5. اعتماد مدير المعمل والتوقيعات -->
            <table class="lab-manager-table" style="width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 0.85rem;">
              <thead>
                <tr style="background: #0f2a5c; color: #ffffff;">
                  <th colspan="2" style="border: 1px solid #0f2a5c; padding: 6px 10px; text-align: center; font-weight: 800;">
                    اعتماد مدير المعمل / الفاحص المسؤول
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style="width: 30%; font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">الاسم</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: 700;">${this.esc(report.labManagerName || report.responsibleEmployee || 'مدير المعمل')}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">التوقيع المعتمد</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px; font-family: monospace; font-size: 0.95rem;">${this.esc(report.labManagerSignature || 'معتمد')}</td>
                </tr>
                <tr>
                  <td style="font-weight: 800; background: #F1F5F9; border: 1px solid #cbd5e1; padding: 6px 8px;">التاريخ</td>
                  <td style="border: 1px solid #cbd5e1; padding: 6px 8px;">${this.esc(mDate || today)}</td>
                </tr>
              </tbody>
            </table>

          </div>

          <!-- Official Company Footer Banner -->
          <div class="lab-sheet-footer-banner" style="background: #0f2a5c; color: #ffffff; padding: 12px 18px; border-bottom-left-radius: 4px; border-bottom-right-radius: 4px; font-size: 0.72rem; line-height: 1.45;">
            <div style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px;">
              <div style="text-align: right;">
                <div>العنوان: القاهرة — النزهة الجديدة، شارع الأديب محمد السباعي</div>
                <div>الهاتف: 0226206493 | البريد الإلكتروني: m.daha@facestone-eg.com | hanaa@facestone-eg.com</div>
              </div>
              <div style="text-align: left;">
                <div style="font-weight: 900; font-size: 0.85rem; letter-spacing: 0.5px;">FACESTONE</div>
                <div>www.facestone-eg.com</div>
              </div>
            </div>
          </div>

        </div>
      `;
    }

    static openPrintModal(report) {
      if (!this.reportModal || !this.modalSheetContent) return;
      this.activePrintReport = report;
      this.modalSheetContent.innerHTML = this.generatePrintSheetHtml(report);
      this.reportModal.style.display = 'flex';
    }

    static closePrintModal() {
      if (this.reportModal) this.reportModal.style.display = 'none';
      this.activePrintReport = null;
    }

    static executePrint(report) {
      const sheetHtml = this.generatePrintSheetHtml(report);
      const printIframe = document.createElement('iframe');
      printIframe.style.position = 'fixed';
      printIframe.style.right = '0';
      printIframe.style.bottom = '0';
      printIframe.style.width = '0';
      printIframe.style.height = '0';
      printIframe.style.border = '0';
      document.body.appendChild(printIframe);

      const sampleRefCode = report.sampleCode || `SMP-${String(report.sampleNumber || 1).padStart(4, '0')}`;

      const doc = printIframe.contentWindow.document;
      doc.open();
      doc.write(`
        <!DOCTYPE html>
        <html dir="rtl" lang="ar">
        <head>
          <meta charset="utf-8">
          <title>FaceStone Lab Report - ${sampleRefCode}</title>
          <style>
            @page {
              size: A4 portrait;
              margin: 8mm 10mm;
            }
            * {
              box-sizing: border-box;
              -webkit-print-color-adjust: exact !important;
              print-color-adjust: exact !important;
            }
            body {
              margin: 0;
              padding: 0;
              background: #ffffff;
              color: #000000;
              direction: rtl;
              font-family: 'Segoe UI', Tahoma, Arial, sans-serif;
            }
            table {
              border-collapse: collapse;
              width: 100%;
              page-break-inside: avoid !important;
              break-inside: avoid !important;
            }
            tr, td, th {
              page-break-inside: avoid !important;
              break-inside: avoid !important;
            }
            .lab-report-sheet {
              page-break-inside: avoid !important;
              break-inside: avoid !important;
            }
          </style>
        </head>
        <body>
          ${sheetHtml}
        </body>
        </html>
      `);
      doc.close();

      setTimeout(() => {
        try {
          printIframe.contentWindow.focus();
          printIframe.contentWindow.print();
        } catch (e) {
          console.error('Iframe print failed:', e);
          window.print();
        } finally {
          setTimeout(() => {
            if (printIframe.parentNode) printIframe.parentNode.removeChild(printIframe);
          }, 1000);
        }
      }, 350);
    }

    // ======================================================================
    // 3. FINAL PRODUCT CONFORMITY CERTIFICATE
    // ======================================================================
    static async renderFinalCertView() {
      if (!this.finalContainer) return;
      const sectionAr = LAB_SECTION_AR[this.activeSection];
      this.finalContainer.innerHTML = `
        <div id="lab-final-alert"></div>
        <div class="lab-form-card" style="max-width: 900px; margin: 0 auto;">
          <h3 style="margin:0 0 6px;color:#0F2A5C;font-size:0.98rem;font-weight:800;">إصدار شهادة مطابقة منتج نهائي — ${this.esc(sectionAr)}</h3>
          <p class="lab-hint" style="margin:0 0 16px;">اختر الرقم المرجعي التتبعي للإنتاج لإصدار شهادة المطابقة للمنتج النهائي.</p>
          <form id="lab-final-form" novalidate>
            <div class="lab-form-grid">
              <div class="lab-field lab-field-full">
                <label class="lab-label">الرقم المرجعي التتبعي للإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-final-ref" required>
                  <option value="">— اختر رقماً مرجعياً للإنتاج —</option>
                </select>
              </div>
            </div>
            <div class="lab-form-grid" style="margin-top:12px;">
              <div class="lab-field">
                <label class="lab-label">اسم المنتج النهائي <span class="req">*</span></label>
                <input type="text" class="lab-input" id="lab-final-name" placeholder="مثال: أحجار ديكور فاخرة 40×40" required>
              </div>
              <div class="lab-field">
                <label class="lab-label">قرار المطابقة للمواصفات <span class="req">*</span></label>
                <select class="lab-select" id="lab-final-decision" required>
                  <option value="CONFORMS">مطابق للمواصفات القياسية</option>
                  <option value="NON_CONFORMING">غير مطابق للمواصفات</option>
                  <option value="PENDING_TESTING">يحتاج فحوصات إضافية</option>
                </select>
              </div>
              <div class="lab-field lab-field-full">
                <label class="lab-label">ملاحظات الفاحص والتوصيات</label>
                <textarea class="lab-textarea" id="lab-final-notes" placeholder="ملاحظات وتوصيات المعمل النهائي..."></textarea>
              </div>
            </div>
            <div class="lab-form-actions">
              <button type="submit" class="lab-btn lab-btn-primary" id="lab-final-submit">إصدار واعتماد شهادة المطابقة</button>
            </div>
          </form>
        </div>
      `;

      try {
        const rawRes = await window.ChatApi.getLaboratoryRawMaterialReports({ section: this.activeSection });
        if (rawRes && rawRes.success && Array.isArray(rawRes.data)) {
          const sel = document.getElementById('lab-final-ref');
          if (sel) {
            sel.innerHTML = '<option value="">— اختر رقماً مرجعياً للإنتاج —</option>' +
              rawRes.data.map(r => `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - ${this.esc(r.sampleCode || r.sampleName || 'عينة')}</option>`).join('');
          }
        }
      } catch (e) {
        console.warn('Failed to load raw refs for final cert:', e);
      }

      document.getElementById('lab-final-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const alertBox = document.getElementById('lab-final-alert');
        const refNo = parseInt(document.getElementById('lab-final-ref')?.value, 10);
        const name = document.getElementById('lab-final-name')?.value?.trim();
        const dec = document.getElementById('lab-final-decision')?.value;
        const notes = document.getElementById('lab-final-notes')?.value?.trim();

        if (!refNo || !name) {
          alertBox.innerHTML = '<div class="lab-alert lab-alert-error">يرجى استكمال الحقول الإلزامية واختيار الرقم المرجعي.</div>';
          return;
        }

        const fd = new FormData();
        fd.append('productionReferenceNumber', String(refNo));
        fd.append('finalProductName', name);
        fd.append('conformityDecision', dec);
        fd.append('notes', notes);
        fd.append('inspectionDateTime', new Date().toISOString());

        try {
          const res = await window.ChatApi.submitLaboratoryFinalCertificate(fd);
          if (res && res.success) {
            this.isDirty = false;
            alertBox.innerHTML = `<div class="lab-alert lab-alert-success">تم إصدار شهادة المطابقة بنجاح — رقم الشهادة: <b>${this.esc(res.data ? res.data.certificateNumber : '')}</b></div>`;
            document.getElementById('lab-final-form').reset();
          } else {
            alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل إصدار الشهادة.')}</div>`;
          }
        } catch (err) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">خطأ: ${this.esc(err.message || '')}</div>`;
        }
      });
    }

    // ======================================================================
    // 4. IN-PROCESS QC TESTING
    // ======================================================================
    static async renderInProcessView() {
      if (!this.inprocContainer) return;
      const sectionAr = LAB_SECTION_AR[this.activeSection];
      this.inprocContainer.innerHTML = `
        <div id="lab-inproc-alert"></div>
        <div class="lab-form-card" style="max-width: 900px; margin: 0 auto;">
          <h3 style="margin:0 0 6px;color:#0F2A5C;font-size:0.98rem;font-weight:800;">تسجيل فحص أثناء التشغيل (In-Process QC) — ${this.esc(sectionAr)}</h3>
          <form id="lab-inproc-form" novalidate>
            <div class="lab-form-grid">
              <div class="lab-field">
                <label class="lab-label">الرقم المرجعي التتبعي للإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-ref" required>
                  <option value="">— اختر رقماً مرجعياً —</option>
                </select>
              </div>
              <div class="lab-field">
                <label class="lab-label">اسم المنتج / الخامة قيد التشغيل <span class="req">*</span></label>
                <input type="text" class="lab-input" id="lab-inproc-name" placeholder="مثال: خليط الخرسانة للصب" required>
              </div>
              <div class="lab-field">
                <label class="lab-label">مرحلة الإنتاج <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-stage" required>
                  <option value="الخلط والتجهيز">الخلط والتجهيز</option>
                  <option value="الصب والكبس">الصب والكبس</option>
                  <option value="المعالجة الأولية">المعالجة الأولية</option>
                  <option value="التشطيب والتلوين">التشطيب والتلوين</option>
                  <option value="الفحص النهائي قبل التعبئة">الفحص النهائي قبل التعبئة</option>
                </select>
              </div>
              <div class="lab-field">
                <label class="lab-label">قرار الفحص <span class="req">*</span></label>
                <select class="lab-select" id="lab-inproc-decision" required>
                  <option value="MEETS_REQUIREMENTS">مطابق للمتطلبات الحالية</option>
                  <option value="DOES_NOT_MEET">غير مطابق للمتطلبات</option>
                  <option value="REQUIRES_TESTING">يحتاج فحوصات إضافية</option>
                </select>
              </div>
              <div class="lab-field lab-field-full">
                <label class="lab-label">نتائج الفحص والتحليل <span class="req">*</span></label>
                <textarea class="lab-textarea" id="lab-inproc-results" placeholder="سجل نتائج قياسات ومؤشرات مرحلة التشغيل..." required></textarea>
              </div>
            </div>
            <div class="lab-form-actions">
              <button type="submit" class="lab-btn lab-btn-primary" id="lab-inproc-submit">حفظ فحص التشغيل</button>
            </div>
          </form>
        </div>
      `;

      try {
        const rawRes = await window.ChatApi.getLaboratoryRawMaterialReports({ section: this.activeSection });
        if (rawRes && rawRes.success && Array.isArray(rawRes.data)) {
          const sel = document.getElementById('lab-inproc-ref');
          if (sel) {
            sel.innerHTML = '<option value="">— اختر رقماً مرجعياً —</option>' +
              rawRes.data.map(r => `<option value="${r.productionReferenceNumber}">#${r.productionReferenceNumber} - ${this.esc(r.sampleCode || r.sampleName || 'عينة')}</option>`).join('');
          }
        }
      } catch (e) {
        console.warn('Failed to load raw refs for inproc:', e);
      }

      document.getElementById('lab-inproc-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const alertBox = document.getElementById('lab-inproc-alert');
        const refNo = parseInt(document.getElementById('lab-inproc-ref')?.value, 10);
        const name = document.getElementById('lab-inproc-name')?.value?.trim();
        const stage = document.getElementById('lab-inproc-stage')?.value;
        const dec = document.getElementById('lab-inproc-decision')?.value;
        const results = document.getElementById('lab-inproc-results')?.value?.trim();

        if (!refNo || !name || !results) {
          alertBox.innerHTML = '<div class="lab-alert lab-alert-error">يرجى استكمال الحقول الإلزامية.</div>';
          return;
        }

        const fd = new FormData();
        fd.append('productionReferenceNumber', String(refNo));
        fd.append('productName', name);
        fd.append('productionStage', stage);
        fd.append('inspectionDecision', dec);
        fd.append('analysisResultsJson', results);
        fd.append('testDateTime', new Date().toISOString());

        try {
          const res = await window.ChatApi.submitLaboratoryInProcessReport(fd);
          if (res && res.success) {
            this.isDirty = false;
            alertBox.innerHTML = '<div class="lab-alert lab-alert-success">تم حفظ فحص التشغيل بنجاح!</div>';
            document.getElementById('lab-inproc-form').reset();
          } else {
            alertBox.innerHTML = `<div class="lab-alert lab-alert-error">${this.esc((res && res.message) || 'فشل حفظ الفحص.')}</div>`;
          }
        } catch (err) {
          alertBox.innerHTML = `<div class="lab-alert lab-alert-error">خطأ: ${this.esc(err.message || '')}</div>`;
        }
      });
    }

    // ======================================================================
    // 5. REPORTS HISTORY
    // ======================================================================
    static async loadHistoryList() {
      if (!this.historyTbody) return;
      this.historyTbody.innerHTML = '<tr><td colspan="10" class="lab-loading">جاري تحميل التقارير والفحوصات...</td></tr>';

      try {
        const params = {};
        const sec = this.histSectionFilter?.value;
        const comp = this.histComplyFilter?.value;
        const q = this.histSearchInput?.value?.trim();

        if (sec && sec !== 'ALL') params.section = sec;
        else params.section = this.activeSection;

        if (comp && comp !== 'ALL') params.complyStatus = comp;
        if (q) params.search = q;

        const res = await window.ChatApi.getLaboratoryRawMaterialReports(params);
        if (!res || !res.success || !Array.isArray(res.data)) {
          this.historyTbody.innerHTML = `<tr><td colspan="10" class="lab-empty">${this.esc((res && res.message) || 'تعذر تحميل السجلات.')}</td></tr>`;
          return;
        }

        this.cachedReports = res.data;
        if (this.cachedReports.length === 0) {
          this.historyTbody.innerHTML = '<tr><td colspan="10" class="lab-empty">لا توجد تقارير فحص مسجلة حتى الآن.</td></tr>';
          return;
        }

        this.historyTbody.innerHTML = this.cachedReports.map(r => {
          const isComp = (r.complyStatus || 'COMPLY') === 'COMPLY';
          const complyBadge = isComp
            ? '<span class="lab-badge lab-badge-approved">Comply (مطابق)</span>'
            : '<span class="lab-badge lab-badge-rejected">Not Complied (غير مطابق)</span>';
          const secBadge = r.laboratorySection === 'CHEMICAL'
            ? '<span class="lab-badge lab-badge-chemical">كيميائي</span>'
            : '<span class="lab-badge lab-badge-physical">فيزيائي</span>';
          const st = STATUS_AR[r.approvalStatus] || [r.approvalStatus, 'lab-badge-pending'];
          const sampleRefCode = r.sampleCode || `SMP-${String(r.sampleNumber || 1).padStart(4, '0')}`;
          const matClass = r.materialClassification === 'CHEMICAL' ? 'كيميائية' : 'ميكانيكية';

          let docLink = '';
          if (r.quantityConfirmationPath) {
            docLink = `<a href="${this.esc(r.quantityConfirmationPath)}" target="_blank" style="color: #2563EB; font-weight: 700; text-decoration: underline; font-size: 0.76rem;" title="معاينة مستند تأكيد الكمية">📄 مستند التأكيد</a>`;
          }

          return `
            <tr>
              <td><b style="color: #0F2A5C; font-size: 0.95rem; font-family: monospace;">${this.esc(sampleRefCode)}</b></td>
              <td><span class="lab-badge" style="background: #F1F5F9; color: #0F172A;">${this.esc(matClass)}</span></td>
              <td>${secBadge}</td>
              <td><b>${this.esc(r.sampleName || r.rawMaterialDescription || '-')}</b></td>
              <td>${r.testedMaterialWeight != null ? (r.testedMaterialWeight.toFixed(2) + ' طن') : (r.receivedQuantity + ' طن')}</td>
              <td>${r.reportIssueDate ? r.reportIssueDate.slice(0, 10) : '-'}</td>
              <td>${complyBadge}</td>
              <td>${this.esc(r.labManagerName || r.responsibleEmployee || '-')}</td>
              <td><span class="lab-badge ${st[1]}">${st[0]}</span> ${docLink ? '<br>' + docLink : ''}</td>
              <td>
                <button type="button" class="lab-btn lab-btn-primary lab-btn-view-doc" data-id="${r.id}" style="padding: 5px 12px; font-size: 0.74rem;">
                  🖨️ عرض / طباعة
                </button>
              </td>
            </tr>
          `;
        }).join('');

        this.historyTbody.querySelectorAll('.lab-btn-view-doc').forEach(btn => {
          btn.addEventListener('click', () => {
            const id = parseInt(btn.getAttribute('data-id'), 10);
            const r = this.cachedReports.find(x => x.id === id);
            if (r) this.openPrintModal(r);
          });
        });
      } catch (err) {
        this.historyTbody.innerHTML = `<tr><td colspan="10" class="lab-empty">تعذر تحميل السجلات: ${this.esc(err.message || '')}</td></tr>`;
      }
    }

    // ======================================================================
    // PRODUCTION ORDER WORKFLOW (إصدار ومتابعة أوامر الإنتاج)
    // ======================================================================
    static async loadOrderForm() {
      try {
        const nextRes = await window.ChatApi.getLabNextOrderNumber();
        if (nextRes && nextRes.success && nextRes.data && this.poSeqPreview) {
          this.poSeqPreview.textContent = nextRes.data;
        }
      } catch (err) {
        console.warn('Could not load next order number preview', err);
      }
      await this.loadAvailableBatches();
    }

    static async loadAvailableBatches() {
      if (!this.poBatchesTbody) return;
      this.poBatchesTbody.innerHTML = `<tr><td colspan="11" style="text-align: center; padding: 2rem; color: #94a3b8;">جاري فحص رصيد المستودع للتشغيلات المطابقة...</td></tr>`;
      try {
        const res = await window.ChatApi.getLabAvailableBatches();
        if (res && res.success && Array.isArray(res.data)) {
          this.cachedAvailableBatches = res.data;
          this.renderAvailableBatches();
        } else {
          this.poBatchesTbody.innerHTML = `<tr><td colspan="11" style="text-align: center; padding: 2rem; color: #ef4444;">تعذر تحميل تشغيلات المستودع: ${this.esc(res?.message || 'خطأ غير معروف')}</td></tr>`;
        }
      } catch (err) {
        this.poBatchesTbody.innerHTML = `<tr><td colspan="11" style="text-align: center; padding: 2rem; color: #ef4444;">فشل الاتصال بالمستودع: ${this.esc(err.message)}</td></tr>`;
      }
    }

    static renderAvailableBatches() {
      if (!this.poBatchesTbody) return;
      if (!this.cachedAvailableBatches || this.cachedAvailableBatches.length === 0) {
        this.poBatchesTbody.innerHTML = `<tr><td colspan="11" style="text-align: center; padding: 2.5rem; color: #64748b;">لا توجد مواد أولية مطابقة برصيد متوفر حالياً بالمستودع. يرجى استيراد المواد وفحصها أولاً.</td></tr>`;
        this.recalcFormula();
        return;
      }

      this.poBatchesTbody.innerHTML = this.cachedAvailableBatches.map(b => {
        const isChem = (b.materialClassification || '').toUpperCase() === 'CHEMICAL';
        const typeBadge = isChem
          ? `<span style="font-size: 0.78rem; background: #eff6ff; color: #1d4ed8; padding: 2px 6px; border-radius: 4px; font-weight: 700;">كيميائية</span>`
          : `<span style="font-size: 0.78rem; background: #ecfdf5; color: #047857; padding: 2px 6px; border-radius: 4px; font-weight: 700;">ميكانيكية</span>`;

        const refDisplay = b.sampleCode
          ? `${this.esc(b.sampleCode)}`
          : (b.productionReferenceNumber ? `#${b.productionReferenceNumber}` : '-');

        const locName = b.storageLocationName || 'المستودع الرئيسي';

        return `
        <tr data-batch-id="${b.warehouseBatchId}">
          <td style="text-align: center; padding: 8px 12px;">
            <input type="checkbox" class="lab-po-batch-check" data-batch-id="${b.warehouseBatchId}" style="width: 18px; height: 18px; cursor: pointer;">
          </td>
          <td style="padding: 8px 12px; font-weight: 700; color: #1e293b;">${this.esc(b.rawMaterialName || b.materialName)}</td>
          <td style="padding: 8px 12px;">${typeBadge}</td>
          <td style="padding: 8px 12px;"><span style="background: #e2e8f0; padding: 2px 6px; border-radius: 4px; font-weight: 700; color: #1e3a8a;">${refDisplay}</span></td>
          <td style="padding: 8px 12px; font-weight: 800; font-family: monospace; color: #0f172a; font-size: 0.95rem;">${this.esc(b.batchNumber)}</td>
          <td style="padding: 8px 12px; color: #334155; font-weight: 600;">${this.esc(locName)}</td>
          <td style="padding: 8px 12px; font-weight: 700; color: #059669;">${this.fmtQty(b.currentQuantity)}</td>
          <td style="padding: 8px 12px; color: #64748b; font-weight: 600;">طن</td>
          <td style="padding: 8px 12px;"><span style="background: #dcfce7; color: #15803d; font-size: 0.75rem; padding: 2px 8px; border-radius: 4px; font-weight: 700;">مطابق (معتمد)</span></td>
          <td style="padding: 8px 12px;">
            <div style="display: flex; align-items: center; gap: 4px;">
              <input type="number" step="0.001" min="0" max="${b.currentQuantity}" class="lab-input lab-po-qty-input" data-batch-id="${b.warehouseBatchId}" placeholder="0.000" disabled style="width: 110px; padding: 4px 8px; font-weight: 700; text-align: left;">
              <span style="font-size: 0.8rem; color: #64748b;">طن</span>
            </div>
          </td>
          <td style="padding: 8px 12px; font-weight: 600; color: #475569;" class="lab-po-rem-cell" data-batch-id="${b.warehouseBatchId}">
            ${this.fmtQty(b.currentQuantity)} طن
          </td>
        </tr>
      `;
      }).join('');

      this.poBatchesTbody.querySelectorAll('.lab-po-batch-check').forEach(chk => {
        chk.addEventListener('change', () => {
          const bId = chk.getAttribute('data-batch-id');
          const row = chk.closest('tr');
          const input = row.querySelector('.lab-po-qty-input');
          const batch = this.cachedAvailableBatches.find(x => x.warehouseBatchId === parseInt(bId, 10));
          if (chk.checked) {
            input.disabled = false;
            if (!input.value || parseFloat(input.value) <= 0) {
              input.value = batch ? batch.currentQuantity.toFixed(3) : '1.000';
            }
            row.style.background = '#f0fdf4';
          } else {
            input.disabled = true;
            input.value = '';
            row.style.background = '';
          }
          this.updateRowRemaining(bId);
          this.recalcFormula();
        });
      });

      this.poBatchesTbody.querySelectorAll('.lab-po-qty-input').forEach(inp => {
        inp.addEventListener('input', () => {
          const bId = inp.getAttribute('data-batch-id');
          this.updateRowRemaining(bId);
          this.recalcFormula();
        });
      });

      this.recalcFormula();
    }

    static updateRowRemaining(bId) {
      const row = this.poBatchesTbody.querySelector(`tr[data-batch-id="${bId}"]`);
      if (!row) return;
      const chk = row.querySelector('.lab-po-batch-check');
      const inp = row.querySelector('.lab-po-qty-input');
      const remCell = row.querySelector('.lab-po-rem-cell');
      const batch = this.cachedAvailableBatches.find(x => x.warehouseBatchId === parseInt(bId, 10));
      if (!batch || !remCell) return;

      const avail = batch.currentQuantity;
      if (!chk.checked) {
        remCell.textContent = `${this.fmtQty(avail)} طن`;
        remCell.style.color = '#475569';
        return;
      }

      const req = parseFloat(inp.value) || 0;
      const rem = avail - req;
      if (rem < 0) {
        remCell.textContent = `عجز: ${this.fmtQty(Math.abs(rem))} طن!`;
        remCell.style.color = '#dc2626';
      } else {
        remCell.textContent = `${this.fmtQty(rem)} طن`;
        remCell.style.color = rem === 0 ? '#d97706' : '#059669';
      }
    }

    static recalcFormula() {
      let totalInput = 0;
      let count = 0;

      if (this.poBatchesTbody) {
        this.poBatchesTbody.querySelectorAll('tr').forEach(row => {
          const chk = row.querySelector('.lab-po-batch-check');
          const inp = row.querySelector('.lab-po-qty-input');
          if (chk && chk.checked && inp) {
            const v = parseFloat(inp.value);
            if (!isNaN(v) && v > 0) {
              totalInput += v;
              count++;
            }
          }
        });
      }

      const targetInput = document.getElementById('lab-po-target-qty');
      const targetQty = targetInput ? (parseFloat(targetInput.value) || 0) : 0;

      if (this.poTotalInput) this.poTotalInput.textContent = `${this.fmtQty(totalInput)} طن`;
      if (this.poTargetOutput) this.poTargetOutput.textContent = `${this.fmtQty(targetQty)} طن`;
      if (this.poItemsCount) this.poItemsCount.textContent = `${count} مادة`;
    }

    static async submitProductionOrder(e) {
      e.preventDefault();
      const pName = document.getElementById('lab-po-product-name')?.value?.trim();
      const targetQty = parseFloat(document.getElementById('lab-po-target-qty')?.value);
      const pDesc = document.getElementById('lab-po-product-desc')?.value?.trim();
      const instructions = document.getElementById('lab-po-instructions')?.value?.trim();

      if (!pName) {
        alert('يرجى إدخال اسم المنتج النهائي المطلوب.');
        document.getElementById('lab-po-product-name')?.focus();
        return;
      }

      if (isNaN(targetQty) || targetQty <= 0) {
        alert('يرجى تحديد كمية مستهدفة صالحة أكبر من الصفر.');
        document.getElementById('lab-po-target-qty')?.focus();
        return;
      }

      const items = [];
      let hasStockOverflow = false;

      this.poBatchesTbody.querySelectorAll('tr').forEach(row => {
        const chk = row.querySelector('.lab-po-batch-check');
        const inp = row.querySelector('.lab-po-qty-input');
        if (chk && chk.checked && inp) {
          const bId = parseInt(chk.getAttribute('data-batch-id'), 10);
          const reqQty = parseFloat(inp.value);
          const batch = this.cachedAvailableBatches.find(x => x.warehouseBatchId === bId);
          if (isNaN(reqQty) || reqQty <= 0) {
            return;
          }
          if (batch && reqQty > batch.currentQuantity) {
            hasStockOverflow = true;
          }
          items.push({
            warehouseBatchId: bId,
            requestedQuantity: reqQty,
            materialType: batch ? batch.materialClassification : '',
            unit: 'طن'
          });
        }
      });

      if (items.length === 0) {
        alert('يجب تحديد تشغيلة ومادة خام واحدة على الأقل وتحديد الكمية المطلوبة للتركيبة.');
        return;
      }

      if (hasStockOverflow) {
        alert('إحدى الكميات المحددة تتجاوز الرصيد المتاح في تشغيلة المستودع. يرجى تعديل الكميات.');
        return;
      }

      const payload = {
        productName: pName,
        productDescription: pDesc,
        requiredQuantity: targetQty,
        unit: 'طن',
        productionInstructions: instructions,
        items: items
      };

      const btn = document.getElementById('btn-submit-lab-order');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `<span>جاري حفظ وإصدار أمر الإنتاج...</span>`;
      }

      try {
        const res = await window.ChatApi.createLabProductionOrder(payload);
        if (res && res.success && res.data) {
          alert(`تم إصدار أمر الإنتاج بنجاح برقم: ${res.data.orderNumber}\nتم إرسال الطلب إلى مستودع المواد الأولية للنقل والتأكيد.`);
          if (this.poForm) this.poForm.reset();
          this.recalcFormula();
          this.switchTab('orders');
        } else {
          alert('تعذر إصدار أمر الإنتاج: ' + (res?.message || 'خطأ غير معروف'));
        }
      } catch (err) {
        alert('حدث خطأ أثناء الاتصال بالخادم: ' + err.message);
      } finally {
        if (btn) {
          btn.disabled = false;
          btn.innerHTML = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z"/></svg><span>إصدار أمر الإنتاج وإرساله إلى المستودع</span>`;
        }
      }
    }

    static async loadOrdersList() {
      if (!this.ordersTbody) return;
      this.ordersTbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #94a3b8;">جاري تحميل أوامر الإنتاج...</td></tr>`;

      const params = {};
      const search = this.ordersSearchInput?.value?.trim();
      const status = this.ordersStatusSelect?.value;
      const from = this.ordersFromDate?.value;
      const to = this.ordersToDate?.value;

      if (search) params.search = search;
      if (status && status !== 'ALL') params.status = status;
      if (from) params.fromDate = from;
      if (to) params.toDate = to;

      try {
        const res = await window.ChatApi.getLabProductionOrders(params);
        if (res && res.success && Array.isArray(res.data)) {
          this.cachedOrders = res.data;
          this.renderOrdersTable(res.data);
        } else {
          this.ordersTbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #ef4444;">تعذر تحميل السجل: ${this.esc(res?.message || '')}</td></tr>`;
        }
      } catch (err) {
        this.ordersTbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #ef4444;">فشل الاتصال: ${this.esc(err.message)}</td></tr>`;
      }
    }

    static renderOrdersTable(orders) {
      if (!this.ordersTbody) return;
      if (!orders || orders.length === 0) {
        this.ordersTbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2.5rem; color: #64748b;">لا توجد أوامر إنتاج مطابقة لمعايير البحث الحالية.</td></tr>`;
        return;
      }

      const getStatusBadge = (status) => {
        switch (status) {
          case 'PENDING_TRANSFER':
            return '<span style="background: #fef3c7; color: #b45309; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">⏳ في انتظار نقل المستودع</span>';
          case 'PARTIALLY_TRANSFERRED':
            return '<span style="background: #e0f2fe; color: #0369a1; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">🔄 تم النقل جزئياً</span>';
          case 'TRANSFERRED_TO_PRODUCTION':
            return '<span style="background: #dbeafe; color: #1d4ed8; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">🚚 تم النقل للإنتاج</span>';
          case 'IN_PRODUCTION':
            return '<span style="background: #f3e8ff; color: #7e22ce; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">⚙️ قيد التصنيع</span>';
          case 'COMPLETED':
            return '<span style="background: #dcfce7; color: #15803d; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">✓ مكتمل التصنيع</span>';
          case 'CANCELLED':
            return '<span style="background: #fee2e2; color: #b91c1c; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">✕ ملغي</span>';
          default:
            return `<span style="background: #f1f5f9; color: #475569; padding: 3px 8px; border-radius: 9999px; font-weight: 600;">${status}</span>`;
        }
      };

      this.ordersTbody.innerHTML = orders.map(o => `
        <tr>
          <td style="padding: 10px 12px; font-weight: 800; color: #1d4ed8;">${this.esc(o.orderNumber)}</td>
          <td style="padding: 10px 12px;">
            <div style="font-weight: 700; color: #0f172a;">${this.esc(o.productName)}</div>
            <div style="font-size: 0.8rem; color: #64748b;">الكمية: ${this.fmtQty(o.requestedQuantity)} ${this.esc(o.unit)}</div>
          </td>
          <td style="padding: 10px 12px;"><span style="background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 0.8rem;">الإصدار v${o.formulaVersion} (${o.itemsCount} مادة)</span></td>
          <td style="padding: 10px 12px;">
            <div>مخطط: <strong>${this.fmtQty(o.totalPlannedRawMaterialQuantity)} طن</strong></div>
            <div style="font-size: 0.8rem; color: ${o.totalTransferredQuantity >= o.totalPlannedRawMaterialQuantity ? '#16a34a' : '#d97706'};">
              منقول: <strong>${this.fmtQty(o.totalTransferredQuantity)} طن</strong>
            </div>
          </td>
          <td style="padding: 10px 12px;">${getStatusBadge(o.status)}</td>
          <td style="padding: 10px 12px; color: #334155;">${this.esc(o.laboratoryManagerName || '-')}</td>
          <td style="padding: 10px 12px; font-size: 0.8rem; color: #64748b;">${o.createdAt ? o.createdAt.slice(0, 16).replace('T', ' ') : '-'}</td>
          <td style="padding: 10px 12px; text-align: center;">
            <div style="display: flex; gap: 6px; justify-content: center;">
              <button type="button" class="lab-btn lab-btn-primary btn-view-order-details" data-order-id="${o.id}" style="padding: 4px 10px; font-size: 0.78rem;">
                عرض التفاصيل
              </button>
              ${o.status === 'PENDING_TRANSFER' ? `
                <button type="button" class="lab-btn lab-btn-ghost btn-cancel-order" data-order-id="${o.id}" style="padding: 4px 8px; font-size: 0.78rem; color: #dc2626;">
                  إلغاء
                </button>
              ` : ''}
            </div>
          </td>
        </tr>
      `).join('');

      this.ordersTbody.querySelectorAll('.btn-view-order-details').forEach(b => {
        b.addEventListener('click', () => {
          const id = parseInt(b.getAttribute('data-order-id'), 10);
          this.openOrderDetailsModal(id);
        });
      });

      this.ordersTbody.querySelectorAll('.btn-cancel-order').forEach(b => {
        b.addEventListener('click', async () => {
          const id = parseInt(b.getAttribute('data-order-id'), 10);
          if (confirm('هل أنت متأكد من رغبتك في إلغاء أمر الإنتاج هذا؟')) {
            try {
              const res = await window.ChatApi.cancelLabProductionOrder(id);
              if (res && res.success) {
                alert('تم إلغاء أمر الإنتاج بنجاح.');
                this.loadOrdersList();
              } else {
                alert('تعذر إلغاء الأمر: ' + (res?.message || ''));
              }
            } catch (err) {
              alert('فشل الاتصال: ' + err.message);
            }
          }
        });
      });
    }

    static async openOrderDetailsModal(orderId) {
      if (!this.orderDetailsModal || !this.orderDetailsContent) return;
      this.orderDetailsModal.style.display = 'flex';
      this.orderDetailsContent.innerHTML = `<div style="text-align: center; padding: 3rem; color: #94a3b8;">جاري تحميل تفاصيل أمر الإنتاج وسجل النقل والتصنيع...</div>`;

      try {
        const res = await window.ChatApi.getLabProductionOrderById(orderId);
        if (res && res.success && res.data) {
          const o = res.data;
          document.getElementById('lab-mod-order-title').textContent = `أمر إنتاج: ${o.orderNumber}`;
          document.getElementById('lab-mod-order-status-badge').textContent = o.statusArabic || o.status;

          this.orderDetailsContent.innerHTML = `
            <!-- Top Summary Card -->
            <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1.25rem; margin-bottom: 1.5rem; display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem;">
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">المنتج المطلوب:</span>
                <strong style="font-size: 1.05rem; color: #0f172a;">${this.esc(o.productName)}</strong>
              </div>
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">الكمية المستهدفة:</span>
                <strong style="font-size: 1.05rem; color: #2563eb;">${this.fmtQty(o.requestedQuantity)} ${this.esc(o.unit)}</strong>
              </div>
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">المستودع المصدر:</span>
                <strong style="color: #334155;">${this.esc(o.storageLocationName)}</strong>
              </div>
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">مدير المختبر المصدر:</span>
                <strong style="color: #334155;">${this.esc(o.laboratoryManagerName)}</strong>
              </div>
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">تاريخ الإصدار:</span>
                <span style="font-size: 0.88rem; color: #475569;">${o.createdAt ? o.createdAt.slice(0, 16).replace('T', ' ') : '-'}</span>
              </div>
              <div>
                <span style="font-size: 0.78rem; color: #64748b; display: block;">إصدار التركيبة:</span>
                <span style="font-size: 0.88rem; font-weight: 700; color: #1e3a8a;">v${o.formulaVersion}</span>
              </div>
            </div>

            ${o.productionInstructions ? `
              <div style="background: #fffbeb; border: 1px solid #fde68a; border-radius: 8px; padding: 0.85rem 1rem; margin-bottom: 1.25rem; font-size: 0.85rem;">
                <strong style="color: #92400e; display: block; margin-bottom: 0.25rem;">تعليمات التصنيع لقسم الإنتاج:</strong>
                <p style="margin: 0; color: #78350f;">${this.esc(o.productionInstructions)}</p>
              </div>
            ` : ''}

            <!-- 1. Manufacturing Formula Breakdown -->
            <div style="margin-bottom: 1.5rem;">
              <h4 style="margin: 0 0 0.75rem 0; font-size: 1rem; font-weight: 800; color: #1e293b; display: flex; align-items: center; gap: 6px;">
                <span>🧪</span>
                <span>تركيبة المواد الأولية المحددة وحالة النقل:</span>
              </h4>
              <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 8px;">
                <table style="width: 100%; text-align: right; border-collapse: collapse; font-size: 0.85rem;">
                  <thead style="background: #f8fafc;">
                    <tr>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">المادة الخام</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">رقم الدفعة (Batch #)</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">الرقم المرجعي</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">كود العينة</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">الكمية المطلوبة (طن)</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">المنقول فعلياً</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">المتبقي للنقل</th>
                      <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">رصيد المستودع الحالي</th>
                    </tr>
                  </thead>
                  <tbody>
                    ${(o.items || []).map(it => `
                      <tr>
                        <td style="padding: 8px 12px; font-weight: 700; color: #0f172a;">${this.esc(it.materialName)}</td>
                        <td style="padding: 8px 12px; color: #1d4ed8; font-weight: 600;">${this.esc(it.batchNumber)}</td>
                        <td style="padding: 8px 12px;"><span style="background: #e2e8f0; padding: 2px 6px; border-radius: 4px;">${it.productionReferenceNumber}</span></td>
                        <td style="padding: 8px 12px; color: #64748b;">${this.esc(it.sampleCode || '-')}</td>
                        <td style="padding: 8px 12px; font-weight: 700;">${this.fmtQty(it.requestedQuantity)} طن</td>
                        <td style="padding: 8px 12px; font-weight: 700; color: #16a34a;">${this.fmtQty(it.issuedQuantity)} طن</td>
                        <td style="padding: 8px 12px; font-weight: 700; color: ${it.remainingToTransfer > 0 ? '#d97706' : '#64748b'};">${this.fmtQty(it.remainingToTransfer)} طن</td>
                        <td style="padding: 8px 12px; color: #475569;">${this.fmtQty(it.currentWarehouseBatchQuantity)} طن</td>
                      </tr>
                    `).join('')}
                  </tbody>
                </table>
              </div>
            </div>

            <!-- 2. Physical Warehouse Transfers Log -->
            <div style="margin-bottom: 1.5rem;">
              <h4 style="margin: 0 0 0.75rem 0; font-size: 1rem; font-weight: 800; color: #1e293b; display: flex; align-items: center; gap: 6px;">
                <span>🚚</span>
                <span>سجل النقل الفعلي من المستودع إلى الإنتاج:</span>
              </h4>
              ${(o.transfers && o.transfers.length > 0) ? `
                <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 8px;">
                  <table style="width: 100%; text-align: right; border-collapse: collapse; font-size: 0.85rem;">
                    <thead style="background: #f8fafc;">
                      <tr>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">رقم إذن النقل</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">تاريخ وتوقيت النقل</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">المادة الخام</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">رقم الدفعة</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">الكمية المنقولة</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">المستودع المصدر</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">أمين المستودع</th>
                        <th style="padding: 8px 12px; border-bottom: 1px solid #e2e8f0;">ملاحظات</th>
                      </tr>
                    </thead>
                    <tbody>
                      ${o.transfers.map(tr => `
                        <tr>
                          <td style="padding: 8px 12px; font-weight: 700; color: #2563eb;">${this.esc(tr.transferTransactionNumber || ('#' + tr.id))}</td>
                          <td style="padding: 8px 12px; font-size: 0.8rem; color: #64748b;">${tr.confirmedAt ? tr.confirmedAt.slice(0, 16).replace('T', ' ') : '-'}</td>
                          <td style="padding: 8px 12px; font-weight: 600;">${this.esc(tr.materialName)}</td>
                          <td style="padding: 8px 12px; color: #1e40af;">${this.esc(tr.batchNumber)}</td>
                          <td style="padding: 8px 12px; font-weight: 700; color: #059669;">${this.fmtQty(tr.transferredQuantity)} طن</td>
                          <td style="padding: 8px 12px; color: #64748b;">${this.esc(tr.storageLocationName)}</td>
                          <td style="padding: 8px 12px; color: #334155;">${this.esc(tr.confirmedBy)}</td>
                          <td style="padding: 8px 12px; font-size: 0.8rem; color: #64748b;">${this.esc(tr.notes || '-')}</td>
                        </tr>
                      `).join('')}
                    </tbody>
                  </table>
                </div>
              ` : `
                <div style="background: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 1.25rem; text-align: center; color: #64748b; font-size: 0.88rem;">
                  لم يتم تأكيد أي تحويلات مادية من المستودع حتى الآن. الطلب بانتظار اعتماد أمين المستودع.
                </div>
              `}
            </div>

            <!-- 3. Production Department Lifecycle Tracking -->
            <div>
              <h4 style="margin: 0 0 0.75rem 0; font-size: 1rem; font-weight: 800; color: #1e293b; display: flex; align-items: center; gap: 6px;">
                <span>⚙️</span>
                <span>تتبع مرحلة التشغيل والإنتاج الفعلي:</span>
              </h4>
              ${o.linkedProcess ? `
                <div style="background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 10px; padding: 1.25rem;">
                  <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #dcfce7; padding-bottom: 0.75rem; margin-bottom: 0.75rem;">
                    <div>
                      <span style="font-size: 0.8rem; color: #166534;">رقم عملية الإنتاج المرتبطة:</span>
                      <strong style="font-size: 1.1rem; color: #15803d; margin-right: 6px;">${this.esc(o.linkedProcess.processNumber)}</strong>
                    </div>
                    <div>
                      <span style="background: #bbf7d0; color: #166534; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.8rem;">${o.linkedProcess.status === 'COMPLETED' ? '✓ مكتمل التصنيع' : '⚙️ قيد التشغيل'}</span>
                    </div>
                  </div>
                  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem;">
                    <div>
                      <span style="font-size: 0.78rem; color: #166534; display: block;">إجمالي المدخلات المستهلكة:</span>
                      <strong style="font-size: 1.05rem; color: #15803d;">${this.fmtQty(o.linkedProcess.totalInputQuantity)} طن</strong>
                    </div>
                    <div>
                      <span style="font-size: 0.78rem; color: #166534; display: block;">إجمالي المنتج المصنع:</span>
                      <strong style="font-size: 1.05rem; color: #15803d;">${this.fmtQty(o.linkedProcess.totalOutputQuantity)} طن</strong>
                    </div>
                    <div>
                      <span style="font-size: 0.78rem; color: #166534; display: block;">كمية الهالك المحسوبة:</span>
                      <strong style="font-size: 1.05rem; color: #dc2626;">${this.fmtQty(o.linkedProcess.totalWasteQuantity)} طن</strong>
                    </div>
                    <div>
                      <span style="font-size: 0.78rem; color: #166534; display: block;">نسبة الهالك:</span>
                      <strong style="font-size: 1.05rem; color: #dc2626;">${o.linkedProcess.wastePercentage}%</strong>
                    </div>
                  </div>
                  ${(o.linkedProcess.finishedProducts && o.linkedProcess.finishedProducts.length > 0) ? `
                    <div style="margin-top: 1rem; border-top: 1px dashed #bbf7d0; padding-top: 0.75rem;">
                      <strong style="font-size: 0.85rem; color: #166534; display: block; margin-bottom: 0.35rem;">المنتجات المصنعة المسجلة:</strong>
                      <ul style="margin: 0; padding-right: 1.25rem; font-size: 0.85rem; color: #14532d;">
                        ${o.linkedProcess.finishedProducts.map(fp => `
                          <li>${this.esc(fp.finishedProductName)} — الكمية: <strong>${this.fmtQty(fp.manufacturedQuantity)} طن</strong> (رقم التصنيع: ${this.esc(fp.manufactureNumber)})</li>
                        `).join('')}
                      </ul>
                    </div>
                  ` : ''}
                </div>
              ` : `
                <div style="background: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 1.25rem; text-align: center; color: #64748b; font-size: 0.88rem;">
                  لم يتم ربط أمر الإنتاج بعملية تصنيع حتى الآن. ستظهر بيانات التتبع فور بدء قسم الإنتاج بتشغيل المواد المحولة.
                </div>
              `}
            </div>
          `;
        } else {
          this.orderDetailsContent.innerHTML = `<div style="text-align: center; padding: 2rem; color: #ef4444;">تعذر تحميل تفاصيل الأمر: ${this.esc(res?.message || '')}</div>`;
        }
      } catch (err) {
        this.orderDetailsContent.innerHTML = `<div style="text-align: center; padding: 2rem; color: #ef4444;">فشل الاتصال: ${this.esc(err.message)}</div>`;
      }
    }

    // ======================================================================
    // UTILITIES
    // ======================================================================
    static esc(str) {
      return String(str == null ? '' : str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    static fmtQty(v) {
      const n = Number(v);
      if (isNaN(n)) return '-';
      return n.toLocaleString('en-US', { minimumFractionDigits: 3, maximumFractionDigits: 3 });
    }

    static debounce(fn, ms) {
      return function (...args) {
        clearTimeout(fn.__labDebounce);
        fn.__labDebounce = setTimeout(() => fn.apply(null, args), ms);
      };
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    window.LaboratoryApp = LaboratoryApp;
  });
  if (!window.LaboratoryApp) window.LaboratoryApp = LaboratoryApp;
})();
