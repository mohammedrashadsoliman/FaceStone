/**
 * FaceStone Web - Finance Management & Statistics Application
 * Professional Arabic RTL Financial Dashboard with Live Database Aggregation,
 * Date-Range Filtering, PO History & Details, and Transparent Performance Mathematics
 */
(function () {
  'use strict';

  class FinanceManagementApp {
    constructor() {
      this.initialized = false;
      this.currentPage = 1;
      this.pageSize = 10;
      this.totalPages = 1;
      this.totalRecords = 0;
      this.isLoading = false;
      this.isLoadingStatistics = false;

      // Primary Date-Range Filter State (Default to Today)
      this.fromDate = '';
      this.toDate = '';
      this.selectedPo = 'all';
      this.availablePos = [];
      this.statisticsData = null;
      this.currentUser = null;
      this.isFinanceDirector = false;

      // Table filter state
      this.filters = {
        search: '',
        po: '',
        supplier: '',
        paymentStatus: 'all',
        invoiceNumber: '',
        dateFrom: '',
        dateTo: ''
      };

      this.currentRecords = [];

      // Active Subview ('dashboard' | 'po-details' | 'performance')
      this.currentView = 'dashboard';
      this.currentPoNumber = null;

      // Performance View Range
      this.perfFromDate = '';
      this.perfToDate = '';
    }

    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindDOMElements();
      this.initDefaultDates();
      this.bindEvents();
      this.bindNewHubAndSubviewsEvents();
      this.bindInvoicesEvents();
      this.loadPoDropdown();
      this.loadStatistics();
      this.loadInvoiceStatistics();
      this.loadPurchaseOrdersHistory();
      this.loadData();
    }

    initDefaultDates() {
      // Default behavior: fromDate = today's date, toDate = today's date
      const today = new Date();
      const y = today.getFullYear();
      const m = String(today.getMonth() + 1).padStart(2, '0');
      const d = String(today.getDate()).padStart(2, '0');
      const todayStr = `${y}-${m}-${d}`;

      this.fromDate = todayStr;
      this.toDate = todayStr;

      if (this.filterDateFrom) this.filterDateFrom.value = this.fromDate;
      if (this.filterDateTo) this.filterDateTo.value = this.toDate;

      this.perfFromDate = this.fromDate;
      this.perfToDate = this.toDate;
      if (this.perfDateFrom) this.perfDateFrom.value = this.perfFromDate;
      if (this.perfDateTo) this.perfDateTo.value = this.perfToDate;
    }

    bindDOMElements() {
      // Root & Subviews
      this.root = document.getElementById('finance-management-page-root');
      this.hubCardsView = document.getElementById('finance-hub-cards-view');
      this.historyView = document.getElementById('finance-history-view');
      this.suppliersView = document.getElementById('finance-suppliers-view');
      this.supplierDetailsView = document.getElementById('finance-supplier-details-view');
      this.posView = document.getElementById('finance-pos-view');
      this.bankView = document.getElementById('finance-bank-view');
      this.expensesView = document.getElementById('finance-expenses-view');
      this.employeesView = document.getElementById('finance-employees-view');
      this.employeeDetailsView = document.getElementById('finance-employee-details-view');
      this.dashboardView = document.getElementById('finance-dashboard-view');
      this.poDetailsView = document.getElementById('finance-po-details-view');
      this.performanceView = document.getElementById('finance-performance-view');
      this.invoicesView = document.getElementById('finance-invoices-view');

      // Invoices Module State
      this.invoicesPage = 1;
      this.invoicesPageSize = 20;
      this.invoicesFilters = {
        search: '',
        status: 'ALL',
        projectCodeId: '',
        fromDate: '',
        toDate: ''
      };
      this.invWizardStep = 1;
      this.invSelectedProjectId = null;
      this.invSelectedProjectObj = null;
      this.invEligibleTransList = [];
      this.invSelectedTransMap = new Map();
      this.invPricingList = [];
      this.invAssociationType = 'COMPANY';
      this.invSelectedCompanyId = null;
      this.invSelectedPoId = null;
      this.invCachedProjects = [];
      this.invCachedCompanies = [];
      this.invCachedPos = [];
      this.invoiceCreationMode = 'PO';
      this.invEligiblePosList = [];
      this.invSelectedDirectPo = null;

      // User Profile Header
      this.userNameEl = document.getElementById('finance-mgmt-user-name');
      this.userRoleEl = document.getElementById('finance-mgmt-user-role');
      this.userAvatarEl = document.getElementById('finance-mgmt-user-avatar');

      // 1. Date Range & Search Filters
      this.filterDateFrom = document.getElementById('finance-filter-date-from');
      this.filterDateTo = document.getElementById('finance-filter-date-to');
      this.btnApplyFilters = document.getElementById('finance-btn-apply-filters');
      this.mainPoSelect = document.getElementById('finance-filter-main-po');
      this.btnClearPo = document.getElementById('finance-btn-clear-po');
      this.resetFiltersBtn = document.getElementById('finance-reset-filters-btn');
      this.refreshBtn = document.getElementById('finance-refresh-btn');

      this.searchInput = document.getElementById('finance-search-input');
      this.searchClearBtn = document.getElementById('finance-search-clear-btn');
      this.filterSupplierSelect = document.getElementById('finance-filter-supplier');
      this.filterPaymentStatusSelect = document.getElementById('finance-filter-payment-status');
      this.filterInvoiceInput = document.getElementById('finance-filter-invoice');

      // 2. Daily / Range Activity Cards
      this.dailyDateBadge = document.getElementById('finance-daily-date-badge');
      this.dailyTransCount = document.getElementById('finance-daily-trans-count');
      this.dailyTransNet = document.getElementById('finance-daily-trans-net');
      this.dailyTransWaste = document.getElementById('finance-daily-trans-waste');
      this.dailyTransWastePrice = document.getElementById('finance-daily-trans-waste-price');
      this.dailyStorageImportQty = document.getElementById('finance-daily-storage-import-qty');
      this.dailyStorageImportPrice = document.getElementById('finance-daily-storage-import-price');
      this.dailyStorageWaste = document.getElementById('finance-daily-storage-waste');
      this.dailyStorageWastePrice = document.getElementById('finance-daily-storage-waste-price');
      this.dailyTotalWaste = document.getElementById('finance-daily-total-waste');
      this.dailyTotalWastePrice = document.getElementById('finance-daily-total-waste-price');

      // 3. PO Historical Summary Table
      this.posCountBadge = document.getElementById('finance-pos-count-badge');
      this.posHistoryTable = document.getElementById('finance-pos-history-table');
      this.posHistoryBody = document.getElementById('finance-pos-history-body');

      // 4. Factory Performance Card
      this.btnPerfBannerDetails = document.getElementById('finance-btn-perf-banner-details');
      this.btnPerfDetails = document.getElementById('finance-btn-perf-details');
      this.perfPercentage = document.getElementById('finance-perf-percentage');
      this.perfBadge = document.getElementById('finance-perf-badge');
      this.perfBadgeText = document.getElementById('finance-perf-badge-text');
      this.perfExpectedQty = document.getElementById('finance-perf-expected-qty');
      this.perfExpectedPrice = document.getElementById('finance-perf-expected-price');
      this.perfActualQty = document.getElementById('finance-perf-actual-qty');
      this.perfActualPrice = document.getElementById('finance-perf-actual-price');
      this.perfWasteQty = document.getElementById('finance-perf-waste-qty');
      this.perfWastePrice = document.getElementById('finance-perf-waste-price');
      this.perfRemainingQty = document.getElementById('finance-perf-remaining-qty');
      this.perfRemainingPrice = document.getElementById('finance-perf-remaining-price');

      // 5. Detailed Transactions Table
      this.tableBody = document.getElementById('finance-table-body');
      this.tableContainer = document.getElementById('finance-table-scroll');
      this.loadingEl = document.getElementById('finance-table-loading');
      this.emptyEl = document.getElementById('finance-table-empty');
      this.errorEl = document.getElementById('finance-table-error');

      this.paginationInfo = document.getElementById('finance-pagination-info');
      this.pageSizeSelect = document.getElementById('finance-page-size-select');
      this.btnPrevPage = document.getElementById('finance-btn-prev-page');
      this.btnNextPage = document.getElementById('finance-btn-next-page');
      this.pageNumbersContainer = document.getElementById('finance-page-numbers');

      // 6. Dedicated PO Details View Elements
      this.poDetailsBackBtn = document.getElementById('finance-po-details-back-btn');
      this.poDetailTitlePo = document.getElementById('po-detail-title-po-number');
      this.poDetailBadgePo = document.getElementById('po-detail-po-number');
      this.poDetailCompany = document.getElementById('po-detail-company');
      this.poDetailDestination = document.getElementById('po-detail-destination');
      this.poDetailMaterial = document.getElementById('po-detail-material');
      this.poDetailOrderDate = document.getElementById('po-detail-order-date');
      this.poDetailStatus = document.getElementById('po-detail-status');
      this.poDetailPaymentStatus = document.getElementById('po-detail-payment-status');

      this.poDetailExpectedQty = document.getElementById('po-detail-expected-qty');
      this.poDetailExpectedVal = document.getElementById('po-detail-expected-val');
      this.poDetailDeliveredQty = document.getElementById('po-detail-delivered-qty');
      this.poDetailDeliveredVal = document.getElementById('po-detail-delivered-val');
      this.poDetailRemainingQty = document.getElementById('po-detail-remaining-qty');
      this.poDetailRemainingVal = document.getElementById('po-detail-remaining-val');
      this.poDetailTotalWaste = document.getElementById('po-detail-total-waste');
      this.poDetailWasteVal = document.getElementById('po-detail-waste-val');
      this.poDetailTransWaste = document.getElementById('po-detail-trans-waste');
      this.poDetailTransMoves = document.getElementById('po-detail-trans-moves');
      this.poDetailStorageWaste = document.getElementById('po-detail-storage-waste');
      this.poDetailStorageQty = document.getElementById('po-detail-storage-qty');
      this.poDetailTonPrice = document.getElementById('po-detail-ton-price');
      this.poDetailPerfPct = document.getElementById('po-detail-perf-pct');

      // Payment controls on PO Details View
      this.poDetailPaidAmt = document.getElementById('po-detail-paid-amt');
      this.poDetailOutstandingAmt = document.getElementById('po-detail-outstanding-amt');
      this.poDetailCurrentPaymentBadge = document.getElementById('po-detail-current-payment-badge');
      this.poDetailDirectorControls = document.getElementById('po-detail-director-controls');
      this.poDetailPaymentSelect = document.getElementById('po-detail-payment-select');
      this.poDetailPaymentNotes = document.getElementById('po-detail-payment-notes');
      this.poDetailBtnSavePayment = document.getElementById('po-detail-btn-save-payment');
      this.poDetailReadonlyControls = document.getElementById('po-detail-readonly-controls');
      this.poDetailPaymentAlert = document.getElementById('po-detail-payment-alert');
      this.poDetailAuditTbody = document.getElementById('po-detail-audit-tbody');

      this.poDetailTimelineList = document.getElementById('po-detail-timeline-list');
      this.poDetailTransCountBadge = document.getElementById('po-detail-trans-count-badge');
      this.poDetailTransTbody = document.getElementById('po-detail-trans-tbody');
      this.poDetailStorageCountBadge = document.getElementById('po-detail-storage-count-badge');
      this.poDetailStorageTbody = document.getElementById('po-detail-storage-tbody');

      // 7. Dedicated Performance Details View Elements
      this.perfBackBtn = document.getElementById('finance-perf-back-btn');
      this.perfDateFrom = document.getElementById('perf-date-from');
      this.perfDateTo = document.getElementById('perf-date-to');
      this.perfBtnApplyDate = document.getElementById('perf-btn-apply-date');
      this.perfRangeLabel = document.getElementById('perf-range-label');

      this.perfDetailPercentage = document.getElementById('perf-detail-percentage');
      this.perfDetailBadge = document.getElementById('perf-detail-badge');
      this.perfDetailBadgeText = document.getElementById('perf-detail-badge-text');
      this.perfDetailFormulaSub = document.getElementById('perf-detail-formula-sub');
      this.perfDetailExpectedQty = document.getElementById('perf-detail-expected-qty');
      this.perfDetailExpectedPrice = document.getElementById('perf-detail-expected-price');
      this.perfDetailActualQty = document.getElementById('perf-detail-actual-qty');
      this.perfDetailActualPrice = document.getElementById('perf-detail-actual-price');
      this.perfDetailTotalWaste = document.getElementById('perf-detail-total-waste');
      this.perfDetailWastePrice = document.getElementById('perf-detail-waste-price');
      this.perfDetailRemainingQty = document.getElementById('perf-detail-remaining-qty');
      this.perfDetailRemainingPrice = document.getElementById('perf-detail-remaining-price');

      this.perfCalcQtySteps = document.getElementById('perf-calc-qty-steps');
      this.perfCalcFinSteps = document.getElementById('perf-calc-fin-steps');
      this.perfCalcWasteSteps = document.getElementById('perf-calc-waste-steps');
      this.perfCalcComparisonSteps = document.getElementById('perf-calc-comparison-steps');
      this.perfPosTbody = document.getElementById('perf-pos-tbody');

      // Legacy Payments Modal (CEO modal)
      this.paymentModal = document.getElementById('finance-payment-modal');
      this.modalPoNumber = document.getElementById('finance-modal-po-number');
      this.modalPaymentsList = document.getElementById('finance-modal-payments-list');
      this.modalCloseBtn = document.getElementById('finance-modal-close-btn');
      this.modalBackdrop = document.getElementById('finance-modal-backdrop');
    }

    bindEvents() {
      // Apply filters button
      this.btnApplyFilters?.addEventListener('click', () => {
        this.applyDateRangeFilter();
      });

      // Date changes
      this.filterDateFrom?.addEventListener('change', () => {
        this.fromDate = this.filterDateFrom.value || '';
      });

      this.filterDateTo?.addEventListener('change', () => {
        this.toDate = this.filterDateTo.value || '';
      });

      // PO Select
      this.mainPoSelect?.addEventListener('change', (e) => {
        this.selectedPo = e.target.value;
        this.filters.po = (this.selectedPo === 'all' ? '' : this.selectedPo);
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // Clear PO
      this.btnClearPo?.addEventListener('click', () => {
        this.selectedPo = 'all';
        if (this.mainPoSelect) this.mainPoSelect.value = 'all';
        this.filters.po = '';
        this.loadStatistics();
        this.currentPage = 1;
        this.loadData();
      });

      // Reset
      this.resetFiltersBtn?.addEventListener('click', () => this.resetFilters());

      // Refresh
      this.refreshBtn?.addEventListener('click', () => {
        this.loadPoDropdown();
        this.loadStatistics();
        this.loadPurchaseOrdersHistory();
        this.loadData();
      });

      // View details buttons on Performance Card
      this.btnPerfBannerDetails?.addEventListener('click', () => {
        this.navigateTo('/finance/performance');
      });

      this.btnPerfDetails?.addEventListener('click', () => {
        this.navigateTo('/finance/performance');
      });

      // Back buttons on subviews
      this.poDetailsBackBtn?.addEventListener('click', () => {
        this.navigateTo('/finance');
      });

      this.perfBackBtn?.addEventListener('click', () => {
        this.navigateTo('/finance');
      });

      // Save Payment Status button (Finance Director)
      this.poDetailBtnSavePayment?.addEventListener('click', () => {
        this.handleSavePaymentStatus();
      });

      // Performance View Date Range Apply
      this.perfBtnApplyDate?.addEventListener('click', () => {
        this.perfFromDate = this.perfDateFrom?.value || '';
        this.perfToDate = this.perfDateTo?.value || '';
        this.loadPerformanceDetails();
      });

      // Quick search in table
      let searchTimeout = null;
      this.searchInput?.addEventListener('input', (e) => {
        const val = e.target.value.trim();
        if (this.searchClearBtn) this.searchClearBtn.style.display = val ? 'flex' : 'none';
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
          this.filters.search = val;
          this.currentPage = 1;
          this.loadData();
        }, 350);
      });

      this.searchClearBtn?.addEventListener('click', () => {
        if (this.searchInput) this.searchInput.value = '';
        if (this.searchClearBtn) this.searchClearBtn.style.display = 'none';
        this.filters.search = '';
        this.currentPage = 1;
        this.loadData();
      });

      // Pagination
      this.pageSizeSelect?.addEventListener('change', (e) => {
        this.pageSize = parseInt(e.target.value, 10) || 10;
        this.currentPage = 1;
        this.loadData();
      });

      this.btnPrevPage?.addEventListener('click', () => {
        if (this.currentPage > 1) {
          this.currentPage--;
          this.loadData();
        }
      });

      this.btnNextPage?.addEventListener('click', () => {
        if (this.currentPage < this.totalPages) {
          this.currentPage++;
          this.loadData();
        }
      });

      // Modal close handlers
      this.modalCloseBtn?.addEventListener('click', () => this.closePaymentModal());
      this.modalBackdrop?.addEventListener('click', () => this.closePaymentModal());
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && this.paymentModal?.style.display === 'flex') {
          this.closePaymentModal();
        }
      });
    }

    applyDateRangeFilter() {
      this.fromDate = this.filterDateFrom?.value || '';
      this.toDate = this.filterDateTo?.value || '';
      this.filters.dateFrom = this.fromDate;
      this.filters.dateTo = this.toDate;

      this.loadStatistics();
      this.currentPage = 1;
      this.loadData();
    }

    updateUserBadge(user) {
      if (!user) return;
      this.currentUser = user;
      const fullName = user.fullName || 'المستخدم';
      const role = window.ChatApp?.formatRoleArabic ? window.ChatApp.formatRoleArabic(user.role) : (user.role || 'محاسب مالي');
      const initials = fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() || 'FS';

      if (this.userNameEl) this.userNameEl.textContent = fullName;
      if (this.userRoleEl) this.userRoleEl.textContent = role;
      if (this.userAvatarEl) this.userAvatarEl.textContent = initials;

      const normRole = (user.role || '').toUpperCase();
      this.isFinanceDirector = [
        'FINANCE_DIRECTOR', 'FINANCE DIRECTOR', 'DIRECTOR_FINANCE', 'FINANCE_MANAGER',
        'FINANCE MANAGER', 'CFO', 'CEO', 'IT'
      ].includes(normRole);

      const addBankBtn = document.getElementById('finance-btn-add-bank-trans');
      if (addBankBtn) addBankBtn.style.display = this.isFinanceDirector ? 'inline-flex' : 'none';
      const addExpBtn = document.getElementById('finance-btn-add-expense');
      if (addExpBtn) addExpBtn.style.display = this.isFinanceDirector ? 'inline-flex' : 'none';
    }

    /* ------------------------------------------------------------------------
       Routing & View Switching (7 Finance Sections & Main Hub)
       ------------------------------------------------------------------------ */
    navigateTo(urlPath) {
      if (window.location.pathname !== urlPath) {
        history.pushState({ view: 'finance-management', subRoute: urlPath }, '', urlPath);
      }
      this.handleRoute(urlPath);
    }

    hideAllSubViews() {
      const views = [
        this.hubCardsView,
        this.historyView,
        this.suppliersView,
        this.supplierDetailsView,
        this.posView,
        this.poDetailsView,
        this.performanceView,
        this.bankView,
        this.expensesView,
        this.employeesView,
        this.employeeDetailsView,
        this.invoicesView,
        this.dashboardView
      ];
      views.forEach(v => {
        if (v) v.style.display = 'none';
      });
    }

    handleRoute(path) {
      const cleanPath = (path || window.location.pathname || '').trim().toLowerCase();
      if (cleanPath === '/finance/history' || cleanPath.endsWith('/history')) {
        this.showHistoryView();
      } else if (cleanPath.startsWith('/finance/suppliers/')) {
        const supId = decodeURIComponent(cleanPath.replace('/finance/suppliers/', '').trim());
        if (supId) {
          this.showSupplierDetailsView(supId);
        } else {
          this.showSuppliersView();
        }
      } else if (cleanPath === '/finance/suppliers' || cleanPath.endsWith('/suppliers')) {
        this.showSuppliersView();
      } else if (cleanPath.startsWith('/finance/purchase-orders/')) {
        const poNum = decodeURIComponent(cleanPath.replace('/finance/purchase-orders/', '').trim());
        if (poNum) {
          this.showPoDetailsView(poNum);
        } else {
          this.showPurchaseOrdersView();
        }
      } else if (cleanPath === '/finance/purchase-orders' || cleanPath.endsWith('/purchase-orders')) {
        this.showPurchaseOrdersView();
      } else if (cleanPath === '/finance/performance' || cleanPath.endsWith('/performance')) {
        this.showPerformanceView();
      } else if (cleanPath === '/finance/bank' || cleanPath.endsWith('/bank')) {
        this.showBankView();
      } else if (cleanPath === '/finance/expenses' || cleanPath.endsWith('/expenses')) {
        this.showExpensesView();
      } else if (cleanPath.startsWith('/finance/employees/')) {
        const empId = decodeURIComponent(cleanPath.replace('/finance/employees/', '').trim());
        if (empId) {
          this.showEmployeeDetailsView(empId);
        } else {
          this.showEmployeesView();
        }
      } else if (cleanPath === '/finance/employees' || cleanPath.endsWith('/employees')) {
        this.showEmployeesView();
      } else if (cleanPath === '/finance/invoices' || cleanPath.endsWith('/invoices')) {
        this.showInvoicesView();
      } else {
        this.showHubCardsView();
      }
    }

    showHubCardsView() {
      this.currentView = 'hub';
      this.hideAllSubViews();
      if (this.hubCardsView) this.hubCardsView.style.display = 'block';
      this.loadInvoiceStatistics();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    showDashboardView() {
      this.showHubCardsView();
    }

    showHistoryView() {
      this.currentView = 'history';
      this.hideAllSubViews();
      if (this.historyView) this.historyView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadHistoryData();
    }

    showSuppliersView() {
      this.currentView = 'suppliers';
      this.hideAllSubViews();
      if (this.suppliersView) this.suppliersView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadSuppliersData();
    }

    showSupplierDetailsView(supplierId) {
      this.currentView = 'supplier-detail';
      this.hideAllSubViews();
      if (this.supplierDetailsView) this.supplierDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadSupplierDetail(supplierId);
    }

    showPurchaseOrdersView() {
      this.currentView = 'purchase-orders';
      this.hideAllSubViews();
      if (this.posView) this.posView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadAllPurchaseOrdersData();
    }

    showPoDetailsView(poNumber) {
      this.currentView = 'po-details';
      this.currentPoNumber = poNumber;
      this.hideAllSubViews();
      if (this.poDetailsView) this.poDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadPoDetails(poNumber);
    }

    showPerformanceView() {
      this.currentView = 'performance';
      this.hideAllSubViews();
      if (this.performanceView) this.performanceView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });

      this.perfFromDate = this.fromDate;
      this.perfToDate = this.toDate;
      if (this.perfDateFrom) this.perfDateFrom.value = this.perfFromDate;
      if (this.perfDateTo) this.perfDateTo.value = this.perfToDate;

      this.loadPerformanceDetails();
    }

    showBankView() {
      this.currentView = 'bank';
      this.hideAllSubViews();
      if (this.bankView) this.bankView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadBankData();
    }

    showExpensesView() {
      this.currentView = 'expenses';
      this.hideAllSubViews();
      if (this.expensesView) this.expensesView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadExpensesData();
    }

    showEmployeesView() {
      this.currentView = 'employees';
      this.hideAllSubViews();
      if (this.employeesView) this.employeesView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadEmployeesData();
    }

    showEmployeeDetailsView(employeeId) {
      this.currentView = 'employee-detail';
      this.hideAllSubViews();
      if (this.employeeDetailsView) this.employeeDetailsView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.loadEmployeeDetail(employeeId);
    }

    /* ------------------------------------------------------------------------
       1. Dropdowns & Statistics Aggregation
       ------------------------------------------------------------------------ */
    async loadPoDropdown() {
      try {
        const res = await window.ChatApi.getFinancePos();
        if (res && res.success && Array.isArray(res.data)) {
          this.availablePos = res.data;
          if (this.mainPoSelect) {
            const currentVal = this.selectedPo || 'all';
            this.mainPoSelect.innerHTML = '<option value="all">-- جميع أوامر الشراء (كافة العقود) --</option>';
            this.availablePos.forEach(p => {
              const opt = document.createElement('option');
              opt.value = p.poNumber;
              const qtyFormatted = Number(p.quantity || 0).toLocaleString('en-US');
              const priceFormatted = Number(p.tonPrice || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
              opt.textContent = `${p.poNumber} — ${p.supplier || 'مورد'} (${qtyFormatted} طن @ ${priceFormatted} ج.م)`;
              this.mainPoSelect.appendChild(opt);
            });
            this.mainPoSelect.value = currentVal;
          }
        }
      } catch (err) {
        console.warn('Could not load PO dropdown options:', err);
      }
    }

    async loadStatistics() {
      this.isLoadingStatistics = true;
      try {
        const poParam = (this.selectedPo && this.selectedPo !== 'all') ? this.selectedPo : '';
        const res = await window.ChatApi.getFinanceStatistics(this.fromDate, this.toDate, poParam);
        if (res && res.success && res.data) {
          this.statisticsData = res.data;
          if (res.data.isCurrentUserFinanceDirector !== undefined) {
            this.isFinanceDirector = res.data.isCurrentUserFinanceDirector;
          }
          this.renderStatistics(res.data);
        }
      } catch (err) {
        console.error('Failed to load finance statistics:', err);
      } finally {
        this.isLoadingStatistics = false;
      }
    }

    renderStatistics(stats) {
      if (!stats) return;

      const daily = stats.dailyActivity || {};
      const perf = stats.factoryPerformance || {};

      // 1. Daily/Range Badge
      if (this.dailyDateBadge) {
        if (stats.filterFromDate === stats.filterToDate) {
          this.dailyDateBadge.textContent = stats.filterFromDate || this.fromDate || 'اليوم';
        } else {
          this.dailyDateBadge.textContent = `من: ${stats.filterFromDate} إلى: ${stats.filterToDate}`;
        }
      }

      // 2. Daily / Range Activity Cards (Live Aggregated Real DB Data)
      if (this.dailyTransCount) {
        this.dailyTransCount.textContent = Number(daily.transportationTransactionsCount || 0).toLocaleString('ar-EG');
      }
      if (this.dailyTransNet) {
        this.dailyTransNet.textContent = this.formatWeight(daily.transportationNetWeight) + ' طن صافي';
      }
      if (this.dailyTransWaste) {
        this.dailyTransWaste.textContent = this.formatWeight(daily.transportationWasteQuantity) + ' طن';
      }
      if (this.dailyTransWastePrice) {
        this.dailyTransWastePrice.textContent = this.formatCurrency(daily.transportationWastePrice);
      }
      if (this.dailyStorageImportQty) {
        this.dailyStorageImportQty.textContent = this.formatWeight(daily.storageImportedQuantity) + ' طن';
      }
      if (this.dailyStorageImportPrice) {
        if (daily.storageImportedPrice !== null && daily.storageImportedPrice !== undefined) {
          this.dailyStorageImportPrice.textContent = this.formatCurrency(daily.storageImportedPrice);
        } else {
          this.dailyStorageImportPrice.textContent = 'غير مسعر';
        }
      }
      if (this.dailyStorageWaste) {
        this.dailyStorageWaste.textContent = this.formatWeight(daily.storageWasteQuantity) + ' طن';
      }
      if (this.dailyStorageWastePrice) {
        this.dailyStorageWastePrice.textContent = this.formatCurrency(daily.storageWastePrice);
      }
      if (this.dailyTotalWaste) {
        this.dailyTotalWaste.textContent = this.formatWeight(daily.totalWasteQuantity) + ' طن';
      }
      if (this.dailyTotalWastePrice) {
        this.dailyTotalWastePrice.textContent = this.formatCurrency(daily.totalWastePrice);
      }

      // 3. Factory Performance Highlight Card
      const perfRate = Number(perf.performancePercentage || 0);
      if (this.perfPercentage) {
        this.perfPercentage.textContent = perfRate.toFixed(2) + '%';
      }

      if (this.perfBadge && this.perfBadgeText) {
        this.perfBadge.className = 'finance-perf-badge';
        if (perfRate >= 95) {
          this.perfBadge.classList.add('perf-positive');
          this.perfBadgeText.textContent = 'أداء ممتاز';
        } else if (perfRate >= 75) {
          this.perfBadge.classList.add('medium');
          this.perfBadgeText.textContent = 'أداء متوسط / قيد التوريد';
        } else {
          this.perfBadge.classList.add('low');
          this.perfBadgeText.textContent = 'أداء منخفض / يتطلب متابعة';
        }
      }

      if (this.perfExpectedQty) this.perfExpectedQty.textContent = this.formatWeight(perf.expectedQuantity) + ' طن';
      if (this.perfExpectedPrice) this.perfExpectedPrice.textContent = this.formatCurrency(perf.expectedPrice);
      if (this.perfActualQty) this.perfActualQty.textContent = this.formatWeight(perf.actualDeliveredQuantity) + ' طن';
      if (this.perfActualPrice) this.perfActualPrice.textContent = this.formatCurrency(perf.actualDeliveredPrice);
      if (this.perfWasteQty) this.perfWasteQty.textContent = this.formatWeight(perf.totalWasteQuantity) + ' طن';
      if (this.perfWastePrice) this.perfWastePrice.textContent = this.formatCurrency(perf.totalWastePrice);
      if (this.perfRemainingQty) this.perfRemainingQty.textContent = this.formatWeight(perf.remainingQuantity) + ' طن';
      if (this.perfRemainingPrice) this.perfRemainingPrice.textContent = this.formatCurrency(perf.remainingPrice);
    }

    /* ------------------------------------------------------------------------
       2. PO Historical List (Requirements 7 & 8)
       ------------------------------------------------------------------------ */
    async loadPurchaseOrdersHistory() {
      try {
        const res = await window.ChatApi.getFinancePurchaseOrders();
        if (res && res.success && Array.isArray(res.data)) {
          this.renderPosHistoryTable(res.data);
          if (this.posCountBadge) {
            this.posCountBadge.textContent = `${res.data.length} أوامر شراء مسجلة`;
          }
        }
      } catch (err) {
        console.error('Failed to load purchase orders history:', err);
      }
    }

    renderPosHistoryTable(pos) {
      if (!this.posHistoryBody) return;
      this.posHistoryBody.innerHTML = '';

      if (pos.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="11" style="text-align: center; color: #64748B; padding: 2rem;">لا توجد أوامر شراء مسجلة في قاعدة البيانات.</td>';
        this.posHistoryBody.appendChild(tr);
        return;
      }

      pos.forEach((p, idx) => {
        const tr = document.createElement('tr');
        tr.className = idx % 2 === 0 ? 'finance-row-even' : 'finance-row-odd';

        // Payment status badge
        const payStatus = p.paymentStatus || 'غير مدفوع';
        let payBadgeClass = 'badge-unpaid';
        if (payStatus === 'مدفوع بالكامل') payBadgeClass = 'badge-paid';
        else if (payStatus === 'مدفوع جزئياً') payBadgeClass = 'badge-partial';

        tr.innerHTML = `
          <!-- 1. PO -->
          <td>
            <strong style="font-family: monospace; color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(p.poNumber)}</strong>
          </td>
          <!-- 2. تاريخ الإنشاء -->
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">
            ${this.formatDate(p.orderDate)}
          </td>
          <!-- 3. المورد -->
          <td style="font-weight: 700; color: #1E293B;">
            ${this.escapeHtml(p.supplier || '-')}
          </td>
          <!-- 4. الكمية المتعاقدة -->
          <td style="font-weight: 700; color: #0F172A;">
            ${this.formatWeight(p.quantity)} طن
          </td>
          <!-- 5. سعر الطن -->
          <td style="color: #D97706; font-weight: 700;">
            ${this.formatCurrency(p.tonPrice)}
          </td>
          <!-- 6. الكمية المسلمة -->
          <td style="color: #059669; font-weight: 700;">
            ${this.formatWeight(p.deliveredQuantity)} طن
          </td>
          <!-- 7. الكمية المتبقية -->
          <td style="color: ${p.remainingQuantity > 0 ? '#B45309' : '#64748B'}; font-weight: 700;">
            ${this.formatWeight(p.remainingQuantity)} طن
          </td>
          <!-- 8. إجمالي الهالك -->
          <td style="color: ${p.totalWasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">
            ${this.formatWeight(p.totalWasteQuantity)} طن
          </td>
          <!-- 9. حالة السداد -->
          <td>
            <span class="finance-status-badge ${payBadgeClass}">${this.escapeHtml(payStatus)}</span>
          </td>
          <!-- 10. حالة العقد -->
          <td>
            <span class="finance-status-badge badge-neutral">${this.escapeHtml(p.status || 'نشط')}</span>
          </td>
          <!-- 11. زر عرض التفاصيل (Requirement 8) -->
          <td style="text-align: center;">
            <button type="button" class="finance-btn-table-details" data-po="${this.escapeHtml(p.poNumber)}" title="عرض تفاصيل أمر الشراء">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              <span>عرض التفاصيل</span>
            </button>
          </td>
        `;

        // Click handler for details button
        const btn = tr.querySelector('.finance-btn-table-details');
        btn?.addEventListener('click', (e) => {
          e.stopPropagation();
          const po = btn.getAttribute('data-po');
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(po)}`);
        });

        this.posHistoryBody.appendChild(tr);
      });
    }

    /* ------------------------------------------------------------------------
       3. Dedicated PO Details View Logic (Requirements 8, 9, 14-18, 26)
       ------------------------------------------------------------------------ */
    async loadPoDetails(poNumber) {
      if (!poNumber) return;
      try {
        const res = await window.ChatApi.getFinancePurchaseOrderDetails(poNumber);
        if (res && res.success && res.data) {
          this.renderPoDetails(res.data);
        } else {
          alert('تعذر تحميل تفاصيل أمر الشراء: ' + (res?.message || 'غير موجود'));
          this.showDashboardView();
        }
      } catch (err) {
        console.error('Failed to load PO details:', err);
        alert('حدث خطأ أثناء جلب تفاصيل أمر الشراء.');
      }
    }

    renderPoDetails(data) {
      // 1. Identity
      if (this.poDetailTitlePo) this.poDetailTitlePo.textContent = data.poNumber;
      if (this.poDetailBadgePo) this.poDetailBadgePo.textContent = data.poNumber;
      if (this.poDetailCompany) this.poDetailCompany.textContent = data.company || data.supplier || 'المورد';
      if (this.poDetailDestination) this.poDetailDestination.textContent = `الوجهة: ${data.destination || 'غير محدد'}`;
      if (this.poDetailMaterial) this.poDetailMaterial.textContent = `المادة: ${data.material || 'مواد تعاقدية'}`;
      if (this.poDetailOrderDate) this.poDetailOrderDate.textContent = this.formatDate(data.orderDate);

      // Badges
      if (this.poDetailStatus) this.poDetailStatus.textContent = data.status || 'نشط';
      if (this.poDetailPaymentStatus) {
        this.poDetailPaymentStatus.textContent = data.paymentStatus || 'غير مدفوع';
        let cls = 'badge-unpaid';
        if (data.paymentStatus === 'مدفوع بالكامل') cls = 'badge-paid';
        else if (data.paymentStatus === 'مدفوع جزئياً') cls = 'badge-partial';
        this.poDetailPaymentStatus.className = `finance-status-badge ${cls}`;
      }

      // 2. Quantitative & Financial KPI Tiles
      if (this.poDetailExpectedQty) this.poDetailExpectedQty.textContent = this.formatWeight(data.originalQuantity) + ' طن';
      if (this.poDetailExpectedVal) this.poDetailExpectedVal.textContent = 'القيمة المتوقعة: ' + this.formatCurrency(data.expectedPrice);
      if (this.poDetailDeliveredQty) this.poDetailDeliveredQty.textContent = this.formatWeight(data.deliveredQuantity) + ' طن';
      if (this.poDetailDeliveredVal) this.poDetailDeliveredVal.textContent = 'القيمة المسلمة: ' + this.formatCurrency(data.deliveredValue);
      if (this.poDetailRemainingQty) this.poDetailRemainingQty.textContent = this.formatWeight(data.remainingQuantity) + ' طن';
      if (this.poDetailRemainingVal) this.poDetailRemainingVal.textContent = 'القيمة المتبقية: ' + this.formatCurrency(data.remainingValue);
      if (this.poDetailTotalWaste) this.poDetailTotalWaste.textContent = this.formatWeight(data.totalWasteQuantity) + ' طن';
      if (this.poDetailWasteVal) this.poDetailWasteVal.textContent = 'قيمة الهالك: ' + this.formatCurrency(data.totalWasteValue);
      if (this.poDetailTransWaste) this.poDetailTransWaste.textContent = this.formatWeight(data.transportationWasteQuantity) + ' طن';
      if (this.poDetailTransMoves) this.poDetailTransMoves.textContent = `عدد النقلات: ${Number(data.transportationTransactionsCount || 0).toLocaleString('ar-EG')}`;
      if (this.poDetailStorageWaste) this.poDetailStorageWaste.textContent = this.formatWeight(data.storageWasteQuantity) + ' طن';
      if (this.poDetailStorageQty) this.poDetailStorageQty.textContent = `إجمالي التخزين: ${this.formatWeight(data.storageQuantity)} طن`;
      if (this.poDetailTonPrice) this.poDetailTonPrice.textContent = this.formatCurrency(data.tonPrice);
      if (this.poDetailPerfPct) this.poDetailPerfPct.textContent = Number(data.performancePercentage || 0).toFixed(2) + '%';

      // 3. Payment Status Controls & Authorization (Requirements 14-18)
      if (this.poDetailPaidAmt) this.poDetailPaidAmt.textContent = this.formatCurrency(data.paidAmount);
      if (this.poDetailOutstandingAmt) this.poDetailOutstandingAmt.textContent = this.formatCurrency(data.outstandingAmount);
      if (this.poDetailCurrentPaymentBadge) {
        this.poDetailCurrentPaymentBadge.textContent = data.paymentStatus || 'غير مدفوع';
        let cls = 'badge-unpaid';
        if (data.paymentStatus === 'مدفوع بالكامل') cls = 'badge-paid';
        else if (data.paymentStatus === 'مدفوع جزئياً') cls = 'badge-partial';
        this.poDetailCurrentPaymentBadge.className = `finance-status-badge ${cls}`;
      }

      // Check if current user is authorized Finance Director
      const canEditPayment = (data.isCurrentUserFinanceDirector === true) || this.isFinanceDirector;
      if (this.poDetailDirectorControls) {
        this.poDetailDirectorControls.style.display = canEditPayment ? 'flex' : 'none';
      }
      if (this.poDetailReadonlyControls) {
        this.poDetailReadonlyControls.style.display = canEditPayment ? 'none' : 'inline-flex';
      }
      if (this.poDetailPaymentSelect) {
        this.poDetailPaymentSelect.value = data.paymentStatus || 'غير مدفوع';
      }
      if (this.poDetailPaymentAlert) {
        this.poDetailPaymentAlert.style.display = 'none';
      }

      // Payment Audit Trail Table
      this.renderPaymentAudits(data.paymentAudits || []);

      // 4. Chronological Timeline (Requirement 9)
      this.renderChronologicalTimeline(data.timeline || []);

      // 5. Transportation Records Table
      this.renderPoTransportationTable(data.transportationTransactions || []);
      if (this.poDetailTransCountBadge) {
        this.poDetailTransCountBadge.textContent = `${(data.transportationTransactions || []).length} نقلات`;
      }

      // 6. Storage Records Table
      this.renderPoStorageTable(data.storageTransactions || []);
      if (this.poDetailStorageCountBadge) {
        this.poDetailStorageCountBadge.textContent = `${(data.storageTransactions || []).length} حركات`;
      }
    }

    renderPaymentAudits(audits) {
      if (!this.poDetailAuditTbody) return;
      this.poDetailAuditTbody.innerHTML = '';

      if (audits.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="5" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد تغييرات سابقة مسجلة في سجل تدقيق الدفع.</td>';
        this.poDetailAuditTbody.appendChild(tr);
        return;
      }

      audits.forEach(a => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(a.previousStatus || '-')}</span></td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(a.newStatus || '-')}</span></td>
          <td style="font-weight: 700; color: #0F172A;">${this.escapeHtml(a.changedBy || 'مدير مالي')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDateTime(a.changedAt)}</td>
          <td style="color: #475569;">${this.escapeHtml(a.notes || '-')}</td>
        `;
        this.poDetailAuditTbody.appendChild(tr);
      });
    }

    renderChronologicalTimeline(events) {
      if (!this.poDetailTimelineList) return;
      this.poDetailTimelineList.innerHTML = '';

      if (events.length === 0) {
        this.poDetailTimelineList.innerHTML = '<div style="color: #64748B; padding: 1rem;">لا توجد حركات مسجلة لهذا العقد.</div>';
        return;
      }

      events.forEach(ev => {
        const item = document.createElement('div');
        item.className = 'finance-timeline-item';

        let dotClass = 'status';
        if (ev.eventType === 'PoCreated') dotClass = 'created';
        else if (ev.eventType === 'Transportation') dotClass = 'transport';
        else if (ev.eventType === 'Storage') dotClass = 'storage';
        else if (ev.eventType === 'PaymentStatusChange') dotClass = 'payment';

        item.innerHTML = `
          <div class="finance-timeline-dot ${dotClass}"></div>
          <div class="finance-timeline-box">
            <div class="finance-timeline-header">
              <span class="finance-timeline-title">${this.escapeHtml(ev.title)}</span>
              <span class="finance-timeline-date">${this.formatDateTime(ev.eventDate)}</span>
            </div>
            <div class="finance-timeline-desc">${this.escapeHtml(ev.description || '')}</div>
          </div>
        `;
        this.poDetailTimelineList.appendChild(item);
      });
    }

    renderPoTransportationTable(transactions) {
      if (!this.poDetailTransTbody) return;
      this.poDetailTransTbody.innerHTML = '';

      if (transactions.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="7" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد حركات نقل مسجلة لهذا العقد.</td>';
        this.poDetailTransTbody.appendChild(tr);
        return;
      }

      transactions.forEach(t => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(t.ticketNumber || '-')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDate(t.transactionDate)}</td>
          <td style="font-weight: 700; color: #1E293B;">${this.escapeHtml(t.truckNumber || '-')}</td>
          <td>${this.escapeHtml(t.driverName || '-')}</td>
          <td style="font-weight: 700; color: #0F172A;">${this.formatWeight(t.netWeight)} طن</td>
          <td style="color: ${t.wasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">${this.formatWeight(t.wasteQuantity)} طن</td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.status || 'مكتمل')}</span></td>
        `;
        this.poDetailTransTbody.appendChild(tr);
      });
    }

    renderPoStorageTable(transactions) {
      if (!this.poDetailStorageTbody) return;
      this.poDetailStorageTbody.innerHTML = '';

      if (transactions.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="8" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد حركات تخزين مسجلة لهذا العقد.</td>';
        this.poDetailStorageTbody.appendChild(tr);
        return;
      }

      transactions.forEach(s => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(s.transactionCode || '-')}</td>
          <td dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.formatDate(s.transactionDate)}</td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(s.action || s.serviceType || 'إيداع')}</span></td>
          <td style="font-weight: 700;">${this.escapeHtml(s.material || '-')}</td>
          <td style="font-weight: 700; color: #0F172A;">${this.formatWeight(s.internalWeight)} طن</td>
          <td>${s.externalWeight !== null && s.externalWeight !== undefined ? (this.formatWeight(s.externalWeight) + ' طن') : '<span style="color: #94A3B8;">-</span>'}</td>
          <td style="color: ${s.wasteQuantity > 0 ? '#DC2626' : '#64748B'}; font-weight: 700;">${this.formatWeight(s.wasteQuantity)} طن</td>
          <td style="color: #64748B;">${this.escapeHtml(s.warehouseName || '-')}</td>
        `;
        this.poDetailStorageTbody.appendChild(tr);
      });
    }

    async handleSavePaymentStatus() {
      if (!this.currentPoNumber) return;
      const newStatus = this.poDetailPaymentSelect?.value;
      const notes = this.poDetailPaymentNotes?.value?.trim() || '';

      if (!newStatus) return;

      if (this.poDetailBtnSavePayment) {
        this.poDetailBtnSavePayment.disabled = true;
        this.poDetailBtnSavePayment.textContent = 'جارٍ الحفظ...';
      }

      try {
        const res = await window.ChatApi.updateFinancePaymentStatus(this.currentPoNumber, newStatus, notes);
        if (res && res.success) {
          if (this.poDetailPaymentAlert) {
            this.poDetailPaymentAlert.className = 'finance-alert success';
            this.poDetailPaymentAlert.textContent = 'تم حفظ وتحديث حالة السداد بنجاح في قاعدة البيانات.';
            this.poDetailPaymentAlert.style.display = 'flex';
          }
          if (this.poDetailPaymentNotes) this.poDetailPaymentNotes.value = '';

          // Reload PO Details & History
          await this.loadPoDetails(this.currentPoNumber);
          this.loadPurchaseOrdersHistory();
          this.loadStatistics();
        } else {
          if (this.poDetailPaymentAlert) {
            this.poDetailPaymentAlert.className = 'finance-alert error';
            this.poDetailPaymentAlert.textContent = 'فشل في تحديث حالة السداد: ' + (res?.message || 'غير مصرح لك.');
            this.poDetailPaymentAlert.style.display = 'flex';
          }
        }
      } catch (err) {
        console.error('Error saving payment status:', err);
        if (this.poDetailPaymentAlert) {
          this.poDetailPaymentAlert.className = 'finance-alert error';
          this.poDetailPaymentAlert.textContent = 'حدث خطأ أثناء حفظ حالة السداد: صلاحيات غير كافية أو فشل في الاتصال.';
          this.poDetailPaymentAlert.style.display = 'flex';
        }
      } finally {
        if (this.poDetailBtnSavePayment) {
          this.poDetailBtnSavePayment.disabled = false;
          this.poDetailBtnSavePayment.innerHTML = `
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>حفظ وتحديث الحالة</span>
          `;
        }
      }
    }

    /* ------------------------------------------------------------------------
       4. Dedicated Performance Details View Logic (Requirements 10-13, 27)
       ------------------------------------------------------------------------ */
    async loadPerformanceDetails() {
      try {
        const poParam = (this.selectedPo && this.selectedPo !== 'all') ? this.selectedPo : '';
        const res = await window.ChatApi.getFinancePerformanceDetails(this.perfFromDate, this.perfToDate, poParam);
        if (res && res.success && res.data) {
          this.renderPerformanceDetails(res.data);
        }
      } catch (err) {
        console.error('Failed to load performance details:', err);
      }
    }

    renderPerformanceDetails(data) {
      if (!data) return;

      // Date range label
      if (this.perfRangeLabel) {
        if (data.filterFromDate === data.filterToDate) {
          this.perfRangeLabel.textContent = data.filterFromDate || 'اليوم';
        } else {
          this.perfRangeLabel.textContent = `من ${data.filterFromDate} إلى ${data.filterToDate}`;
        }
      }

      // Overall dial
      const perfPct = Number(data.overallPerformancePercentage || 0);
      if (this.perfDetailPercentage) this.perfDetailPercentage.textContent = perfPct.toFixed(2) + '%';
      if (this.perfDetailBadge && this.perfDetailBadgeText) {
        this.perfDetailBadge.className = 'finance-perf-badge';
        if (perfPct >= 95) {
          this.perfDetailBadge.classList.add('perf-positive');
          this.perfDetailBadgeText.textContent = 'أداء ممتاز';
        } else if (perfPct >= 75) {
          this.perfDetailBadge.classList.add('medium');
          this.perfDetailBadgeText.textContent = 'أداء متوسط / قيد التوريد';
        } else {
          this.perfDetailBadge.classList.add('low');
          this.perfDetailBadgeText.textContent = 'أداء منخفض / يتطلب متابعة';
        }
      }

      // Expected vs Actual grid
      if (this.perfDetailExpectedQty) this.perfDetailExpectedQty.textContent = this.formatWeight(data.cumulativeExpectedQuantity) + ' طن';
      if (this.perfDetailExpectedPrice) this.perfDetailExpectedPrice.textContent = this.formatCurrency(data.cumulativeExpectedPrice);
      if (this.perfDetailActualQty) this.perfDetailActualQty.textContent = this.formatWeight(data.cumulativeDeliveredQuantity) + ' طن';
      if (this.perfDetailActualPrice) this.perfDetailActualPrice.textContent = this.formatCurrency(data.cumulativeDeliveredPrice);
      if (this.perfDetailTotalWaste) this.perfDetailTotalWaste.textContent = this.formatWeight(data.cumulativeTotalWaste) + ' طن';
      if (this.perfDetailWastePrice) this.perfDetailWastePrice.textContent = this.formatCurrency(data.cumulativeWasteFinancialValue);
      if (this.perfDetailRemainingQty) this.perfDetailRemainingQty.textContent = this.formatWeight(data.cumulativeRemainingQuantity) + ' طن';
      if (this.perfDetailRemainingPrice) this.perfDetailRemainingPrice.textContent = this.formatCurrency(data.cumulativeRemainingPrice);

      // Requirements 11 & 12: Arithmetic Step-by-Step Transparent Formulas
      // 1. Quantitative Calculation Steps
      if (this.perfCalcQtySteps) {
        this.perfCalcQtySteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. إجمالي الكمية التعاقدية المتوقعة (Expected Quantity):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeExpectedQuantity)} طن</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. إجمالي الهالك الفعلي (نقل + تخزين):</span>
            <div class="calc-math-formula">هالك النقل (${this.formatWeight(data.cumulativeTransportationWaste)} طن) + هالك التخزين (${this.formatWeight(data.cumulativeStorageWaste)} طن) = <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong></div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. حساب الكمية الفعلية المسلمة (Actual Delivered Quantity):</span>
            <div class="calc-math-formula">${data.quantitativeCalculationFormula || `${this.formatWeight(data.cumulativeExpectedQuantity)} - ${this.formatWeight(data.cumulativeTotalWaste)} = ${this.formatWeight(data.cumulativeDeliveredQuantity)} طن`}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">4. نسبة الإنجاز والأداء الكمي (Performance %):</span>
            <div class="calc-math-formula">${data.quantitativePerformancePercentageFormula || `(${this.formatWeight(data.cumulativeDeliveredQuantity)} / ${this.formatWeight(data.cumulativeExpectedQuantity)}) × 100 = ${perfPct.toFixed(2)}%`}</div>
          </div>
        `;
      }

      // 2. Financial Calculation Steps
      if (this.perfCalcFinSteps) {
        const finPct = Number(data.financialPerformancePercentage || 0);
        this.perfCalcFinSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. القيمة المالية المتوقعة لكافة العقود (Expected Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeExpectedPrice)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. القيمة المالية الفعلية المسلمة (Delivered Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeDeliveredPrice)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. القيمة المالية المفقودة في الهالك (Waste Financial Value):</span>
            <div class="calc-math-formula">${this.formatCurrency(data.cumulativeWasteFinancialValue)}</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">4. نسبة الأداء المالي (Financial Performance %):</span>
            <div class="calc-math-formula">${data.financialPerformancePercentageFormula || `(${this.formatCurrency(data.cumulativeDeliveredPrice)} / ${this.formatCurrency(data.cumulativeExpectedPrice)}) × 100 = ${finPct.toFixed(2)}%`}</div>
          </div>
        `;
      }

      // 3. Waste Breakdown Steps
      if (this.perfCalcWasteSteps) {
        this.perfCalcWasteSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">1. هالك النقل التراكمي (Transportation Waste):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeTransportationWaste)} طن (فارق ميزان الشاحنات المسجل فعلياً)</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">2. هالك التخزين التراكمي (Storage Waste):</span>
            <div class="calc-math-formula">${this.formatWeight(data.cumulativeStorageWaste)} طن (فارق ميزان البسكول الخارجي عن الداخلي في المخازن)</div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">3. قاعدة احتساب إجمالي الهالك:</span>
            <div class="calc-math-formula">إجمالي الهالك = هالك النقل (${this.formatWeight(data.cumulativeTransportationWaste)} طن) + هالك التخزين (${this.formatWeight(data.cumulativeStorageWaste)} طن) = <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong></div>
          </div>
        `;
      }

      // 4. Range vs Cumulative Comparison (Requirement 13)
      if (this.perfCalcComparisonSteps) {
        this.perfCalcComparisonSteps.innerHTML = `
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">حركات النطاق الزمني المحدد (${data.filterFromDate} إلى ${data.filterToDate}):</span>
            <div class="calc-math-formula">
              عدد النقلات: <strong>${Number(data.rangeTransportationCount || 0).toLocaleString('ar-EG')}</strong> | 
              الصافي المنقول: <strong>${this.formatWeight(data.rangeTransportationNetWeight)} طن</strong> | 
              الوارد المخزني المستقل: <strong>${this.formatWeight(data.rangeStorageImportedQuantity)} طن</strong> | 
              هالك الفترة: <strong>${this.formatWeight(data.rangeTotalWaste)} طن</strong>
            </div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">الموقف التراكمي لعقود أوامر الشراء (Cumulative PO Status):</span>
            <div class="calc-math-formula">
              إجمالي التعاقد: <strong>${this.formatWeight(data.cumulativeExpectedQuantity)} طن</strong> | 
              إجمالي المسلم: <strong>${this.formatWeight(data.cumulativeDeliveredQuantity)} طن</strong> | 
              إجمالي الهالك الكلي: <strong>${this.formatWeight(data.cumulativeTotalWaste)} طن</strong> | 
              المتبقي الكلي: <strong>${this.formatWeight(data.cumulativeRemainingQuantity)} طن</strong>
            </div>
          </div>
          <div class="finance-calc-step">
            <span class="finance-calc-step-label">التوضيح المحاسبي:</span>
            <div style="font-size: 0.8rem; color: #475569;">
              يتم احتساب نسبة الأداء الكلية بناءً على الموقف التراكمي للتعاقد لضمان عدم معاملة حركة اليوم كإجمالي للعقد كاملاً.
            </div>
          </div>
        `;
      }

      // PO Breakdown Table
      this.renderPerformancePoTable(data.poBreakdowns || []);
    }

    renderPerformancePoTable(pos) {
      if (!this.perfPosTbody) return;
      this.perfPosTbody.innerHTML = '';

      if (pos.length === 0) {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td colspan="10" style="text-align: center; color: #64748B; padding: 1rem;">لا توجد أوامر شراء للمقارنة.</td>';
        this.perfPosTbody.appendChild(tr);
        return;
      }

      pos.forEach(p => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td><strong style="font-family: monospace; color: #0F172A;">${this.escapeHtml(p.poNumber)}</strong></td>
          <td style="font-weight: 700;">${this.escapeHtml(p.supplier || '-')}</td>
          <td style="font-weight: 700;">${this.formatWeight(p.expectedQuantity)} طن</td>
          <td style="color: #059669; font-weight: 700;">${this.formatWeight(p.deliveredQuantity)} طن</td>
          <td style="color: #DC2626; font-weight: 700;">${this.formatWeight(p.totalWasteQuantity)} طن</td>
          <td style="color: #B45309; font-weight: 700;">${this.formatWeight(p.remainingQuantity)} طن</td>
          <td><strong style="color: #1D4ED8;">${Number(p.performancePercentage || 0).toFixed(2)}%</strong></td>
          <td>${this.formatCurrency(p.expectedPrice)}</td>
          <td>${this.formatCurrency(p.deliveredPrice)}</td>
          <td style="text-align: center;">
            <button type="button" class="finance-btn-table-details" data-po="${this.escapeHtml(p.poNumber)}" title="عرض تفاصيل أمر الشراء">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              <span>التفاصيل</span>
            </button>
          </td>
        `;

        const btn = tr.querySelector('.finance-btn-table-details');
        btn?.addEventListener('click', () => {
          const po = btn.getAttribute('data-po');
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(po)}`);
        });

        this.perfPosTbody.appendChild(tr);
      });

      // Real Forecasting Section
      if (data.forecasting) {
        const f = data.forecasting;
        const bannerTextEl = document.getElementById('perf-forecast-summary-text');
        if (bannerTextEl) {
          bannerTextEl.textContent = f.forecastingSummaryArabic || 'لا تتوفر توقعات حالية.';
        }
        const remQtyEl = document.getElementById('perf-forecast-rem-qty');
        if (remQtyEl) remQtyEl.textContent = this.formatWeight(f.expectedRemainingQuantity) + ' طن';

        const remValEl = document.getElementById('perf-forecast-rem-val');
        if (remValEl) remValEl.textContent = this.formatCurrency(f.expectedRemainingValue);

        const dailyPaceEl = document.getElementById('perf-forecast-daily-pace');
        if (dailyPaceEl) dailyPaceEl.textContent = this.formatWeight(f.averageDailyDeliveredQuantity) + ' طن/يوم';

        const daysEl = document.getElementById('perf-forecast-days');
        if (daysEl) daysEl.textContent = (f.estimatedDaysToCompletion || 0) + ' يوم';

        const dateEl = document.getElementById('perf-forecast-date');
        if (dateEl) dateEl.textContent = f.projectedCompletionDate ? this.formatDate(f.projectedCompletionDate) : 'غير محدد';

        const yieldEl = document.getElementById('perf-forecast-yield');
        if (yieldEl) yieldEl.textContent = this.formatCurrency(f.expectedFinalFinancialYield);
      }
    }

    /* ------------------------------------------------------------------------
       5. Detailed Management Table (Bottom Section)
       ------------------------------------------------------------------------ */
    async loadData() {
      this.setLoadingState(true);
      try {
        const params = {
          search: this.filters.search,
          po: this.filters.po,
          supplier: this.filters.supplier,
          paymentStatus: this.filters.paymentStatus,
          invoiceNumber: this.filters.invoiceNumber,
          dateFrom: this.fromDate,
          dateTo: this.toDate,
          page: this.currentPage,
          pageSize: this.pageSize
        };

        const res = await window.ChatApi.getFinanceManagementData(params);
        if (res && res.success && res.data) {
          const data = res.data;
          this.currentRecords = data.records || [];
          this.totalRecords = data.totalCount || 0;
          this.currentPage = data.page || 1;
          this.totalPages = data.totalPages || 1;

          this.updateFilterDropdowns(data.filterOptions);
          this.renderTable(this.currentRecords);
          this.renderPagination();
          this.setEmptyState(this.currentRecords.length === 0);
          this.setErrorState(false);
        } else {
          this.setErrorState(true, res?.message || 'فشل في استرجاع البيانات المالية');
        }
      } catch (err) {
        console.error('Error loading finance data:', err);
        this.setErrorState(true, 'حدث خطأ في الاتصال بالخادم.');
      } finally {
        this.setLoadingState(false);
      }
    }

    updateFilterDropdowns(opts) {
      if (!opts) return;
      if (this.filterSupplierSelect && opts.suppliers) {
        const currentVal = this.filterSupplierSelect.value;
        const currentOptions = Array.from(this.filterSupplierSelect.options).map(o => o.value);
        opts.suppliers.forEach(s => {
          if (!currentOptions.includes(s)) {
            const opt = document.createElement('option');
            opt.value = s;
            opt.textContent = s;
            this.filterSupplierSelect.appendChild(opt);
          }
        });
        if (currentVal) this.filterSupplierSelect.value = currentVal;
      }
    }

    renderTable(records) {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      records.forEach((r, idx) => {
        const tr = document.createElement('tr');
        tr.className = idx % 2 === 0 ? 'finance-row-even' : 'finance-row-odd';
        tr.setAttribute('data-po', r.poNumber || '');

        let badgeClass = 'badge-unpaid';
        if (r.paymentStatus === 'مدفوع بالكامل') {
          badgeClass = 'badge-paid';
        } else if (r.paymentStatus === 'مدفوع جزئياً') {
          badgeClass = 'badge-partial';
        }

        const paidFormatted = this.formatCurrency(r.paidAmount);
        const paidHtml = r.paidAmount > 0 
          ? `<span class="finance-paid-link" data-po="${this.escapeHtml(r.poNumber)}" title="عرض تفاصيل الدفعات">${paidFormatted} <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg></span>`
          : `<span>0.00 ج.م</span>`;

        tr.innerHTML = `
          <td class="cell-po">
            <span class="finance-po-badge">${this.escapeHtml(r.poNumber || '-')}</span>
          </td>
          <td class="cell-supplier font-medium">
            ${this.escapeHtml(r.supplier || '-')}
          </td>
          <td class="cell-date font-numeric" dir="ltr">
            ${this.formatDate(r.orderDate)}
          </td>
          <td class="cell-qty font-numeric font-semibold">
            ${this.formatWeight(r.poQuantity)} طن
          </td>
          <td class="cell-val font-numeric font-semibold color-primary">
            ${this.formatCurrency(r.poValue)}
          </td>
          <td class="cell-price font-numeric">
            ${this.formatCurrency(r.tonPrice)}
          </td>
          <td class="cell-supplied-qty font-numeric font-semibold color-info">
            ${this.formatWeight(r.suppliedQuantity)} طن
          </td>
          <td class="cell-supplied-val font-numeric font-semibold">
            ${this.formatCurrency(r.suppliedValue)}
          </td>
          <td class="cell-remaining-qty font-numeric font-semibold ${r.remainingQuantity > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatWeight(r.remainingQuantity)} طن
          </td>
          <td class="cell-remaining-val font-numeric ${r.remainingValue > 0 ? 'color-warning' : 'color-muted'}">
            ${this.formatCurrency(r.remainingValue)}
          </td>
          <td class="cell-movements font-numeric">
            <span class="finance-count-badge">${Number(r.movementsCount || 0).toLocaleString('ar-EG')} نقلة</span>
          </td>
          <td class="cell-approved font-numeric font-semibold color-success">
            ${this.formatWeight(r.approvedQuantity)} طن
          </td>
          <td class="cell-unapproved font-numeric ${r.unapprovedQuantity > 0 ? 'color-danger font-semibold' : 'color-muted'}">
            ${this.formatWeight(r.unapprovedQuantity)} طن
          </td>
          <td class="cell-paid font-numeric font-semibold color-success">
            ${paidHtml}
          </td>
          <td class="cell-outstanding font-numeric font-semibold ${r.outstandingAmount > 0 ? 'color-danger' : 'color-success'}">
            ${this.formatCurrency(r.outstandingAmount)}
          </td>
          <td class="cell-status">
            <span class="finance-status-badge ${badgeClass}">${this.escapeHtml(r.paymentStatus)}</span>
          </td>
          <td class="cell-invoice font-numeric">
            ${r.invoiceNumber ? `<span class="finance-invoice-chip" dir="ltr">${this.escapeHtml(r.invoiceNumber)}</span>` : '<span class="color-muted">غير محدد</span>'}
          </td>
        `;

        tr.addEventListener('click', (e) => {
          if (e.target.closest('.finance-paid-link')) return;
          const poNum = tr.getAttribute('data-po');
          if (!poNum || poNum === '-') return;
          this.navigateTo(`/finance/purchase-orders/${encodeURIComponent(poNum)}`);
        });

        const paidLink = tr.querySelector('.finance-paid-link');
        if (paidLink) {
          paidLink.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            const po = paidLink.getAttribute('data-po');
            if (po) this.showPaymentDetailsModal(po);
          });
        }

        this.tableBody.appendChild(tr);
      });
    }

    renderPagination() {
      if (!this.paginationInfo) return;
      const start = this.totalRecords === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
      const end = Math.min(this.currentPage * this.pageSize, this.totalRecords);
      this.paginationInfo.textContent = `عرض ${start} إلى ${end} من إجمالي ${this.totalRecords} أمر شراء`;

      if (this.btnPrevPage) this.btnPrevPage.disabled = (this.currentPage <= 1);
      if (this.btnNextPage) this.btnNextPage.disabled = (this.currentPage >= this.totalPages);

      if (this.pageNumbersContainer) {
        this.pageNumbersContainer.innerHTML = '';
        for (let i = 1; i <= this.totalPages; i++) {
          if (i === 1 || i === this.totalPages || (i >= this.currentPage - 1 && i <= this.currentPage + 1)) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `finance-page-btn ${i === this.currentPage ? 'active' : ''}`;
            btn.textContent = i;
            btn.addEventListener('click', () => {
              this.currentPage = i;
              this.loadData();
            });
            this.pageNumbersContainer.appendChild(btn);
          }
        }
      }
    }

    /* ------------------------------------------------------------------------
       CEO Payment Modal
       ------------------------------------------------------------------------ */
    async showPaymentDetailsModal(poNumber) {
      if (!this.paymentModal) return;
      if (this.modalPoNumber) this.modalPoNumber.textContent = poNumber;
      if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="text-align: center; padding: 2rem;">جارٍ تحميل الدفعات...</div>';
      if (this.modalBackdrop) this.modalBackdrop.style.display = 'flex';
      this.paymentModal.style.display = 'flex';

      try {
        const res = await window.ChatApi.getFinancePayments(poNumber);
        if (res && res.success && Array.isArray(res.data)) {
          this.renderModalPayments(res.data);
        } else {
          if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="color: #64748B; padding: 2rem; text-align: center;">لا توجد دفعات مسجلة لهذا العقد.</div>';
        }
      } catch (err) {
        console.error('Error fetching payments:', err);
        if (this.modalPaymentsList) this.modalPaymentsList.innerHTML = '<div style="color: #DC2626; padding: 2rem; text-align: center;">فشل في تحميل سجل الدفعات.</div>';
      }
    }

    renderModalPayments(payments) {
      if (!this.modalPaymentsList) return;
      if (payments.length === 0) {
        this.modalPaymentsList.innerHTML = '<div style="color: #64748B; padding: 2rem; text-align: center;">لا توجد دفعات مالية مسجلة لهذا العقد.</div>';
        return;
      }

      let html = '<div class="finance-table-scroll"><table class="finance-table"><thead><tr><th>رقم الدفعة</th><th>المبلغ</th><th>التاريخ</th><th>طريقة السداد</th><th>الحالة</th><th>ملاحظات</th></tr></thead><tbody>';
      payments.forEach(p => {
        html += `
          <tr>
            <td style="font-family: monospace; font-weight: 700;">${this.escapeHtml(p.paymentNumber || p.id || '-')}</td>
            <td style="color: #059669; font-weight: 700;">${this.formatCurrency(p.amount)}</td>
            <td dir="ltr" style="font-family: monospace;">${this.formatDate(p.paymentDate || p.createdAt)}</td>
            <td>${this.escapeHtml(p.paymentMethod || 'تحويل بنكي')}</td>
            <td><span class="finance-status-badge badge-paid">${this.escapeHtml(p.status || 'معتمد')}</span></td>
            <td>${this.escapeHtml(p.notes || '-')}</td>
          </tr>
        `;
      });
      html += '</tbody></table></div>';
      this.modalPaymentsList.innerHTML = html;
    }

    closePaymentModal() {
      if (this.paymentModal) this.paymentModal.style.display = 'none';
      if (this.modalBackdrop) this.modalBackdrop.style.display = 'none';
    }

    resetFilters() {
      this.initDefaultDates();
      this.selectedPo = 'all';
      if (this.mainPoSelect) this.mainPoSelect.value = 'all';

      this.filters = {
        search: '',
        po: '',
        supplier: '',
        paymentStatus: 'all',
        invoiceNumber: '',
        dateFrom: '',
        dateTo: ''
      };

      if (this.searchInput) this.searchInput.value = '';
      if (this.searchClearBtn) this.searchClearBtn.style.display = 'none';
      if (this.filterSupplierSelect) this.filterSupplierSelect.value = '';
      if (this.filterPaymentStatusSelect) this.filterPaymentStatusSelect.value = 'all';
      if (this.filterInvoiceInput) this.filterInvoiceInput.value = '';

      this.currentPage = 1;
      this.loadStatistics();
      this.loadPurchaseOrdersHistory();
      this.loadData();
    }

    setLoadingState(loading) {
      this.isLoading = loading;
      if (this.loadingEl) this.loadingEl.style.display = loading ? 'flex' : 'none';
      if (loading) {
        if (this.emptyEl) this.emptyEl.style.display = 'none';
        if (this.errorEl) this.errorEl.style.display = 'none';
      }
    }

    setEmptyState(empty) {
      if (this.emptyEl) this.emptyEl.style.display = empty && !this.isLoading ? 'flex' : 'none';
    }

    setErrorState(show, msg = '') {
      if (this.errorEl) {
        this.errorEl.style.display = show ? 'flex' : 'none';
        const msgEl = this.errorEl.querySelector('.finance-error-msg');
        if (msgEl && msg) msgEl.textContent = msg;
      }
    }

    formatCurrency(val) {
      const num = Number(val) || 0;
      return num.toLocaleString('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }) + ' ج.م';
    }

    formatWeight(val) {
      const num = Number(val) || 0;
      return num.toLocaleString('en-US', {
        minimumFractionDigits: 3,
        maximumFractionDigits: 3
      });
    }

    formatDate(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
      } catch {
        return dateStr;
      }
    }

    formatDateTime(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        const hours = String(d.getHours()).padStart(2, '0');
        const minutes = String(d.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day} ${hours}:${minutes}`;
      } catch {
        return dateStr;
      }
    }

    escapeHtml(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
    }

    bindNewHubAndSubviewsEvents() {
      // 8 Hub Cards click & keyboard navigation
      const cards = [
        { id: 'card-nav-finance-history', route: '/finance/history' },
        { id: 'card-nav-finance-suppliers', route: '/finance/suppliers' },
        { id: 'card-nav-finance-pos', route: '/finance/purchase-orders' },
        { id: 'card-nav-finance-performance', route: '/finance/performance' },
        { id: 'card-nav-finance-bank', route: '/finance/bank' },
        { id: 'card-nav-finance-expenses', route: '/finance/expenses' },
        { id: 'card-nav-finance-employees', route: '/finance/employees' },
        { id: 'card-nav-finance-invoices', route: '/finance/invoices' }
      ];
      cards.forEach(c => {
        const el = document.getElementById(c.id);
        if (el) {
          el.addEventListener('click', (e) => {
            e.preventDefault();
            this.navigateTo(c.route);
          });
          el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              this.navigateTo(c.route);
            }
          });
        }
      });

      // Subview Back Buttons (Return to Finance Main Hub or Parents)
      const backButtons = [
        { id: 'finance-history-back-btn', route: '/finance' },
        { id: 'finance-suppliers-back-btn', route: '/finance' },
        { id: 'finance-supplier-detail-back-btn', route: '/finance/suppliers' },
        { id: 'finance-pos-back-btn', route: '/finance' },
        { id: 'finance-po-details-back-btn', route: '/finance/purchase-orders' },
        { id: 'finance-perf-back-btn', route: '/finance' },
        { id: 'finance-bank-back-btn', route: '/finance' },
        { id: 'finance-expenses-back-btn', route: '/finance' },
        { id: 'finance-employees-back-btn', route: '/finance' },
        { id: 'finance-emp-detail-back-btn', route: '/finance/employees' },
        { id: 'finance-invoices-back-btn', route: '/finance' }
      ];
      backButtons.forEach(b => {
        const btn = document.getElementById(b.id);
        if (btn) {
          btn.addEventListener('click', (e) => {
            e.preventDefault();
            this.navigateTo(b.route);
          });
        }
      });

      // 1. History Filters & Pagination
      const histApplyBtn = document.getElementById('finance-hist-apply-btn');
      if (histApplyBtn) {
        histApplyBtn.addEventListener('click', () => {
          this.histPage = 1;
          this.loadHistoryData();
        });
      }
      const histResetBtn = document.getElementById('finance-hist-reset-btn');
      if (histResetBtn) {
        histResetBtn.addEventListener('click', () => {
          const df = document.getElementById('finance-hist-date-from');
          const dt = document.getElementById('finance-hist-date-to');
          const ty = document.getElementById('finance-hist-type-select');
          const po = document.getElementById('finance-hist-po-select');
          const sp = document.getElementById('finance-hist-supplier-select');
          const sr = document.getElementById('finance-hist-search-input');
          if (df) df.value = '';
          if (dt) dt.value = '';
          if (ty) ty.value = 'all';
          if (po) po.value = 'all';
          if (sp) sp.value = 'all';
          if (sr) sr.value = '';
          this.histPage = 1;
          this.loadHistoryData();
        });
      }
      const histRefreshBtn = document.getElementById('finance-hist-refresh-btn');
      if (histRefreshBtn) {
        histRefreshBtn.addEventListener('click', () => this.loadHistoryData());
      }
      const histSearch = document.getElementById('finance-hist-search-input');
      if (histSearch) {
        let t = null;
        histSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => {
            this.histPage = 1;
            this.loadHistoryData();
          }, 350);
        });
      }
      const histPrev = document.getElementById('finance-hist-prev-page');
      if (histPrev) {
        histPrev.addEventListener('click', () => {
          if (this.histPage > 1) {
            this.histPage--;
            this.loadHistoryData();
          }
        });
      }
      const histNext = document.getElementById('finance-hist-next-page');
      if (histNext) {
        histNext.addEventListener('click', () => {
          if (this.histPage < (this.histTotalPages || 1)) {
            this.histPage++;
            this.loadHistoryData();
          }
        });
      }

      // 2. Suppliers Search, Refresh, Add & Sync
      const supSearch = document.getElementById('finance-suppliers-search-input');
      if (supSearch) {
        let t = null;
        supSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterSuppliersClientSide(), 250);
        });
      }
      const supDeptFilter = document.getElementById('finance-suppliers-dept-filter');
      if (supDeptFilter) {
        supDeptFilter.addEventListener('change', () => this.filterSuppliersClientSide());
      }
      const supRefresh = document.getElementById('finance-suppliers-refresh-btn');
      if (supRefresh) {
        supRefresh.addEventListener('click', () => this.loadSuppliersData());
      }
      const supAddBtn = document.getElementById('fin-supplier-btn-add');
      if (supAddBtn) {
        supAddBtn.addEventListener('click', () => this.openAddSupplierModal());
      }
      const supSyncBtn = document.getElementById('fin-supplier-btn-sync');
      if (supSyncBtn) {
        supSyncBtn.addEventListener('click', () => this.syncSuppliers());
      }
      const supModalClose = document.getElementById('fin-supplier-modal-close-btn');
      const supFormCancel = document.getElementById('fin-supplier-form-cancel-btn');
      [supModalClose, supFormCancel].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeSupplierModal());
      });
      const supForm = document.getElementById('finance-supplier-form');
      if (supForm) {
        supForm.addEventListener('submit', (e) => this.submitSupplierForm(e));
      }
      const supTxAddBtn = document.getElementById('supplier-detail-add-tx-btn');
      if (supTxAddBtn) {
        supTxAddBtn.addEventListener('click', () => this.openAddSupplierTransactionModal(this.currentSupplierId, this.currentSupplierName));
      }
      const supDetailEditBtn = document.getElementById('supplier-detail-edit-btn');
      if (supDetailEditBtn) {
        supDetailEditBtn.addEventListener('click', () => this.openEditSupplierModal(this.currentSupplierId));
      }
      const supTxModalClose = document.getElementById('fin-supplier-tx-modal-close-btn');
      const supTxFormCancel = document.getElementById('fin-supplier-tx-cancel-btn');
      [supTxModalClose, supTxFormCancel].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeSupplierTransactionModal());
      });
      const supTxForm = document.getElementById('finance-supplier-tx-form');
      if (supTxForm) {
        supTxForm.addEventListener('submit', (e) => this.submitSupplierTransaction(e));
      }

      // 3. Purchase Orders Filters, Refresh & Add
      const posSearch = document.getElementById('finance-pos-search-input');
      if (posSearch) {
        let t = null;
        posSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterPurchaseOrdersClientSide(), 250);
        });
      }
      const posStatus = document.getElementById('finance-pos-status-select');
      if (posStatus) {
        posStatus.addEventListener('change', () => this.filterPurchaseOrdersClientSide());
      }
      const posRefresh = document.getElementById('finance-pos-refresh-btn');
      if (posRefresh) {
        posRefresh.addEventListener('click', () => this.loadAllPurchaseOrdersData());
      }
      const poAddBtn = document.getElementById('fin-po-btn-add');
      if (poAddBtn) {
        poAddBtn.addEventListener('click', () => this.openAddPurchaseOrderModal());
      }
      const poModalClose = document.getElementById('fin-po-modal-close-btn');
      const poFormCancel = document.getElementById('fin-po-form-cancel-btn');
      [poModalClose, poFormCancel].forEach(b => {
        if (b) b.addEventListener('click', () => this.closePurchaseOrderModal());
      });
      const poForm = document.getElementById('finance-po-form');
      if (poForm) {
        poForm.addEventListener('submit', (e) => this.submitPurchaseOrderForm(e));
      }
      const poQtyInput = document.getElementById('po-form-quantity');
      const poPriceInput = document.getElementById('po-form-unit-price');
      [poQtyInput, poPriceInput].forEach(inp => {
        if (inp) inp.addEventListener('input', () => this.calculatePoTotalValue());
      });

      // 5. Bank Add Modal & Form
      const addBankBtn = document.getElementById('finance-btn-add-bank-trans');
      if (addBankBtn) {
        addBankBtn.addEventListener('click', () => this.openAddBankModal());
      }
      const bankCloseBtn = document.getElementById('bank-modal-close-btn');
      const bankCancelBtn = document.getElementById('bank-modal-cancel-btn');
      [bankCloseBtn, bankCancelBtn].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeAddBankModal());
      });
      const bankForm = document.getElementById('finance-bank-form');
      if (bankForm) {
        bankForm.addEventListener('submit', (e) => this.submitBankTransaction(e));
      }
      const bankRefresh = document.getElementById('finance-bank-refresh-btn');
      if (bankRefresh) {
        bankRefresh.addEventListener('click', () => this.loadBankData());
      }

      // 6. Expenses Add Modal & Form
      const addExpBtn = document.getElementById('finance-btn-add-expense');
      if (addExpBtn) {
        addExpBtn.addEventListener('click', () => this.openAddExpenseModal());
      }
      const expCloseBtn = document.getElementById('exp-modal-close-btn');
      const expCancelBtn = document.getElementById('exp-modal-cancel-btn');
      [expCloseBtn, expCancelBtn].forEach(b => {
        if (b) b.addEventListener('click', () => this.closeAddExpenseModal());
      });
      const expForm = document.getElementById('finance-expense-form');
      if (expForm) {
        expForm.addEventListener('submit', (e) => this.submitExpense(e));
      }
      const expApplyBtn = document.getElementById('finance-exp-apply-btn');
      if (expApplyBtn) {
        expApplyBtn.addEventListener('click', () => this.loadExpensesData());
      }
      const expResetBtn = document.getElementById('finance-exp-reset-btn');
      if (expResetBtn) {
        expResetBtn.addEventListener('click', () => {
          const df = document.getElementById('finance-exp-date-from');
          const dt = document.getElementById('finance-exp-date-to');
          const cat = document.getElementById('finance-exp-category-filter');
          if (df) df.value = '';
          if (dt) dt.value = '';
          if (cat) cat.value = 'all';
          this.loadExpensesData();
        });
      }
      const expRefresh = document.getElementById('finance-expenses-refresh-btn');
      if (expRefresh) {
        expRefresh.addEventListener('click', () => this.loadExpensesData());
      }

      // 7. Employees Search & Refresh
      const empSearch = document.getElementById('finance-emp-search-input');
      if (empSearch) {
        let t = null;
        empSearch.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => this.filterEmployeesClientSide(), 250);
        });
      }
      const empRefresh = document.getElementById('finance-emp-refresh-btn');
      if (empRefresh) {
        empRefresh.addEventListener('click', () => this.loadEmployeesData());
      }
    }

    /* ------------------------------------------------------------------------
       1. HISTORY VIEW LOGIC (/finance/history)
       ------------------------------------------------------------------------ */
    async loadHistoryData() {
      const search = document.getElementById('finance-hist-search-input')?.value?.trim() || '';
      const type = document.getElementById('finance-hist-type-select')?.value || 'all';
      const po = document.getElementById('finance-hist-po-select')?.value || 'all';
      const supplier = document.getElementById('finance-hist-supplier-select')?.value || 'all';
      const fromDate = document.getElementById('finance-hist-date-from')?.value || '';
      const toDate = document.getElementById('finance-hist-date-to')?.value || '';

      const loadingEl = document.getElementById('finance-hist-loading');
      const emptyEl = document.getElementById('finance-hist-empty');
      const tbody = document.getElementById('finance-hist-tbody');
      const badge = document.getElementById('finance-hist-count-badge');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceHistory({
          search,
          type: type === 'all' ? '' : type,
          po: po === 'all' ? '' : po,
          supplier: supplier === 'all' ? '' : supplier,
          fromDate,
          toDate,
          page: this.histPage || 1,
          pageSize: this.histPageSize || 15
        });

        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const items = res.data.items || [];
          this.histTotalRecords = res.data.totalRecords || 0;
          this.histTotalPages = res.data.totalPages || 1;
          this.histPage = res.data.currentPage || 1;

          if (badge) badge.textContent = `${this.histTotalRecords.toLocaleString()} معاملة`;
          this.renderHistoryTable(items);

          if (res.data.availablePos && this.histPoPopulated !== true) {
            this.populateHistPoDropdown(res.data.availablePos);
          }
          if (res.data.availableSuppliers && this.histSuppliersPopulated !== true) {
            this.populateHistSupplierDropdown(res.data.availableSuppliers);
          }
          this.updateHistPagination();
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load finance history:', err);
      }
    }

    populateHistPoDropdown(pos) {
      const select = document.getElementById('finance-hist-po-select');
      if (!select || !Array.isArray(pos)) return;
      this.histPoPopulated = true;
      const currentVal = select.value || 'all';
      select.innerHTML = '<option value="all">-- كافة أوامر الشراء --</option>';
      pos.forEach(p => {
        const opt = document.createElement('option');
        opt.value = p;
        opt.textContent = p;
        select.appendChild(opt);
      });
      select.value = currentVal;
    }

    populateHistSupplierDropdown(suppliers) {
      const select = document.getElementById('finance-hist-supplier-select');
      if (!select || !Array.isArray(suppliers)) return;
      this.histSuppliersPopulated = true;
      const currentVal = select.value || 'all';
      select.innerHTML = '<option value="all">-- كافة الموردين --</option>';
      suppliers.forEach(s => {
        const opt = document.createElement('option');
        opt.value = s;
        opt.textContent = s;
        select.appendChild(opt);
      });
      select.value = currentVal;
    }

    renderHistoryTable(items) {
      const tbody = document.getElementById('finance-hist-tbody');
      const emptyEl = document.getElementById('finance-hist-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(t => {
        const isReceived = (t.direction === 'إيداع' || t.direction === 'مستلم' || t.direction === 'وارد');
        const dirBadge = isReceived
          ? `<span class="finance-status-badge badge-positive">${this.escapeHtml(t.direction)}</span>`
          : `<span class="finance-status-badge badge-waste">${this.escapeHtml(t.direction)}</span>`;

        return `
          <tr>
            <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.transactionTypeArabic || t.transactionType)}</span></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(t.transactionNumber || '-')}</span></td>
            <td>${t.poNumber ? `<span dir="ltr" style="font-weight: 700; color: #D97706;">${this.escapeHtml(t.poNumber)}</span>` : '<span style="color: #94A3B8;">-</span>'}</td>
            <td><strong>${this.escapeHtml(t.supplierOrEntity || '-')}</strong></td>
            <td><strong dir="ltr" style="font-family: monospace; color: ${isReceived ? '#059669' : '#DC2626'};">${this.formatCurrency(t.amount)}</strong></td>
            <td>${dirBadge}</td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.status || 'معتمد')}</span></td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(t.createdByUser || '-')}</span></td>
            <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(t.description || '-')}</span></td>
          </tr>
        `;
      }).join('');
    }

    updateHistPagination() {
      const info = document.getElementById('finance-hist-pagination-info');
      const prevBtn = document.getElementById('finance-hist-prev-page');
      const nextBtn = document.getElementById('finance-hist-next-page');
      const pagesContainer = document.getElementById('finance-hist-page-numbers');

      const p = this.histPage || 1;
      const tp = this.histTotalPages || 1;

      if (info) info.textContent = `صفحة ${p} من ${tp} (إجمالي ${this.histTotalRecords} معاملة)`;
      if (prevBtn) prevBtn.disabled = (p <= 1);
      if (nextBtn) nextBtn.disabled = (p >= tp);

      if (pagesContainer) {
        pagesContainer.innerHTML = '';
        for (let i = 1; i <= Math.min(tp, 7); i++) {
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = `finance-page-btn ${i === p ? 'active' : ''}`;
          btn.textContent = i;
          btn.addEventListener('click', () => {
            this.histPage = i;
            this.loadHistoryData();
          });
          pagesContainer.appendChild(btn);
        }
      }
    }

    /* ------------------------------------------------------------------------
       2. SUPPLIERS VIEW LOGIC (/finance/suppliers)
       ------------------------------------------------------------------------ */
    async loadSuppliersData() {
      const loadingEl = document.getElementById('finance-suppliers-loading');
      const emptyEl = document.getElementById('finance-suppliers-empty');
      const tbody = document.getElementById('finance-suppliers-tbody');
      const countBadge = document.getElementById('finance-suppliers-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceSuppliers();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.suppliersRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} مورد`;
          this.renderSuppliersTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load suppliers:', err);
      }
    }

    filterSuppliersClientSide() {
      if (!this.suppliersRawList) return;
      const term = document.getElementById('finance-suppliers-search-input')?.value?.trim().toLowerCase() || '';
      const dept = document.getElementById('finance-suppliers-dept-filter')?.value || 'all';

      const filtered = this.suppliersRawList.filter(s => {
        const matchesTerm = !term ||
          (s.name && s.name.toLowerCase().includes(term)) ||
          (s.supplierCode && s.supplierCode.toLowerCase().includes(term)) ||
          (s.primaryMaterialName && s.primaryMaterialName.toLowerCase().includes(term));

        let matchesDept = true;
        if (dept !== 'all') {
          matchesDept = (s.sourceDepartment && s.sourceDepartment.toLowerCase().includes(dept.toLowerCase()));
        }

        return matchesTerm && matchesDept;
      });

      this.renderSuppliersTable(filtered);
    }

    renderSuppliersTable(items) {
      const tbody = document.getElementById('finance-suppliers-tbody');
      const emptyEl = document.getElementById('finance-suppliers-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(s => {
        const deptBadgeClass = s.sourceDepartment.includes('LAB') ? 'badge-highlight' : (s.sourceDepartment.includes('WAREHOUSE') ? 'badge-positive' : 'badge-neutral');
        const deptDisplay = s.sourceDepartment === 'COMBINED' || s.sourceDepartment === 'LABORATORY & WAREHOUSE' 
          ? 'معمل ومستودع' 
          : (s.sourceDepartment === 'LABORATORY' ? 'المعمل' : (s.sourceDepartment === 'WAREHOUSE_IMPORT' ? 'المستودع' : 'المالية'));
        const lastPay = s.lastPaymentDate ? this.formatDate(s.lastPaymentDate) : '-';

        return `
          <tr>
            <td><strong dir="ltr" style="font-family: monospace; color: #0284C7; font-size: 0.95rem;">${this.escapeHtml(s.supplierCode || '-')}</strong></td>
            <td><strong style="color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(s.name)}</strong></td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(s.primaryMaterialName || '-')}</span></td>
            <td><span class="finance-status-badge ${deptBadgeClass}">${this.escapeHtml(deptDisplay)}</span></td>
            <td><span dir="ltr" style="font-family: monospace; font-size: 0.8rem; color: #64748B;">${this.escapeHtml(s.relatedLaboratoryReferenceNumber || '-')}</span></td>
            <td><span dir="ltr" style="font-family: monospace; font-size: 0.85rem; color: #059669; font-weight: 700;">${this.escapeHtml(s.relatedWarehouseImportNumber || '-')}</span></td>
            <td><strong dir="ltr" style="color: #0F172A;">${this.formatCurrency(s.totalAmountOwed)}</strong></td>
            <td><strong dir="ltr" style="color: #059669;">${this.formatCurrency(s.totalAmountPaid)}</strong></td>
            <td><strong dir="ltr" style="color: ${s.remainingBalance > 0 ? '#DC2626' : '#059669'}; font-weight: 800;">${this.formatCurrency(s.remainingBalance)}</strong></td>
            <td><span dir="ltr" style="font-size: 0.85rem;">${lastPay}</span></td>
            <td style="white-space: nowrap;">
              <div style="display: flex; gap: 6px;">
                <button type="button" class="finance-btn-table-action" title="عرض الملف التفصيلي" onclick="window.FinanceManagementApp.navigateTo('/finance/suppliers/' + ${s.id})">
                  <span>عرض</span>
                </button>
                <button type="button" class="finance-btn-table-action" title="تعديل بيانات المورد" onclick="window.FinanceManagementApp.openEditSupplierModal(${s.id})" style="background: #E2E8F0; color: #1E293B;">
                  <span>تعديل</span>
                </button>
                <button type="button" class="finance-btn-table-action" title="إضافة حركة مالية / سداد" onclick="window.FinanceManagementApp.openAddSupplierTransactionModal(${s.id}, '${this.escapeHtml(s.name)}')" style="background: #DCFCE7; color: #15803D;">
                  <span>+ حركة</span>
                </button>
              </div>
            </td>
          </tr>
        `;
      }).join('');
    }

    async loadSupplierDetail(id) {
      if (!id) return;
      try {
        const res = await window.ChatApi.getFinanceSupplierDetails(id);
        if (res && res.success && res.data) {
          const d = res.data;
          this.currentSupplierId = d.id;
          this.currentSupplierName = d.name;

          const nameEl = document.getElementById('supplier-detail-name');
          const codeBadge = document.getElementById('supplier-detail-code-badge');
          if (nameEl) nameEl.textContent = d.name;
          if (codeBadge) codeBadge.textContent = d.supplierCode || `SUP-${d.id}`;

          const matSub = document.getElementById('supplier-detail-materials-sub');
          if (matSub) matSub.textContent = `المادة: ${d.primaryMaterialName || '-'}`;
          const txSub = document.getElementById('supplier-detail-tx-count-sub');
          if (txSub) txSub.textContent = `عدد المعاملات: ${d.transactions ? d.transactions.length : 0}`;
          const deptSub = document.getElementById('supplier-detail-dept-sub');
          if (deptSub) deptSub.textContent = `القسم المصدر: ${d.sourceDepartment || 'FINANCE'}`;

          const owedEl = document.getElementById('supplier-detail-owed-amt');
          if (owedEl) owedEl.textContent = this.formatCurrency(d.totalAmountOwed);
          const paidEl = document.getElementById('supplier-detail-paid-amt');
          if (paidEl) paidEl.textContent = this.formatCurrency(d.totalAmountPaid);
          const remEl = document.getElementById('supplier-detail-remaining-balance');
          if (remEl) remEl.textContent = this.formatCurrency(d.remainingBalance);
          const lastDateEl = document.getElementById('supplier-detail-last-payment-date');
          if (lastDateEl) lastDateEl.textContent = d.lastPaymentDate ? this.formatDate(d.lastPaymentDate) : 'لا يوجد دفعات مسجلة';

          // Metadata Card
          const phoneEl = document.getElementById('supplier-detail-phone');
          if (phoneEl) phoneEl.textContent = d.phoneNumber || '-';
          const contactEl = document.getElementById('supplier-detail-contact');
          if (contactEl) contactEl.textContent = d.contactPerson || '-';
          const taxEl = document.getElementById('supplier-detail-tax');
          if (taxEl) taxEl.textContent = d.taxNumber || '-';
          const labRefEl = document.getElementById('supplier-detail-lab-ref');
          if (labRefEl) labRefEl.textContent = d.relatedLaboratoryReferenceNumber || '-';
          const impEl = document.getElementById('supplier-detail-import-num');
          if (impEl) impEl.textContent = d.relatedWarehouseImportNumber || '-';
          const regEl = document.getElementById('supplier-detail-reg-date');
          if (regEl) regEl.textContent = this.formatDate(d.registrationDate);

          // Transactions Table
          const txTbody = document.getElementById('supplier-detail-tx-tbody');
          const txBadge = document.getElementById('supplier-detail-tx-badge');
          const txList = d.transactions || [];
          if (txBadge) txBadge.textContent = `${txList.length} حركة`;
          if (txTbody) {
            if (txList.length === 0) {
              txTbody.innerHTML = '<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد معاملات مالية أو مدفوعات مسجلة لهذا المورد بعد. اضغط على "+ إضافة معاملة مالية" لتسجيل دفعة أو مستحق.</td></tr>';
            } else {
              txTbody.innerHTML = txList.map(t => {
                const isPaid = t.transactionType === 'PAYMENT';
                const isOwed = t.transactionType === 'AMOUNT_OWED';
                const color = isPaid ? '#059669' : (isOwed ? '#DC2626' : '#D97706');
                const badgeClass = isPaid ? 'badge-positive' : (isOwed ? 'badge-waste' : 'badge-neutral');
                return `
                  <tr>
                    <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
                    <td><span class="finance-status-badge ${badgeClass}">${this.escapeHtml(t.transactionTypeArabic || t.transactionType)}</span></td>
                    <td><strong dir="ltr" style="color: ${color}; font-size: 0.95rem;">${isPaid ? '-' : '+'}${this.formatCurrency(t.amount)}</strong></td>
                    <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(t.paymentMethod || '-')}</span></td>
                    <td><span dir="ltr" style="font-family: monospace; font-size: 0.85rem;">${this.escapeHtml(t.referenceNumber || '-')}</span></td>
                    <td>${this.escapeHtml(t.description || t.notes || '-')}</td>
                    <td><span style="font-size: 0.85rem; color: #64748B;">${this.escapeHtml(t.createdBy || '-')}</span></td>
                  </tr>
                `;
              }).join('');
            }
          }

          // POs Table
          const posTbody = document.getElementById('supplier-detail-pos-tbody');
          if (posTbody) {
            const pos = d.purchaseOrders || [];
            if (pos.length === 0) {
              posTbody.innerHTML = '<tr><td colspan="10" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد أوامر شراء مرتبطة بهذا المورد (المورد يعمل ومسجل بشكل مستقل).</td></tr>';
            } else {
              posTbody.innerHTML = pos.map(p => `
                <tr>
                  <td><strong dir="ltr" style="font-family: monospace; color: #D97706;">${this.escapeHtml(p.poNumber)}</strong></td>
                  <td>${this.escapeHtml(p.materialName || 'مواد خام')}</td>
                  <td><strong dir="ltr">${this.formatWeight(p.orderedQuantity)} طن</strong></td>
                  <td><strong dir="ltr">${this.formatCurrency(p.tonPrice)}</strong></td>
                  <td><strong dir="ltr">${this.formatCurrency(p.totalValue)}</strong></td>
                  <td><strong dir="ltr" style="color: #059669;">${this.formatWeight(p.deliveredQuantity)} طن</strong></td>
                  <td><strong dir="ltr" style="color: #D97706;">${this.formatWeight(p.remainingQuantity)} طن</strong></td>
                  <td><span class="finance-status-badge ${p.status === 'مكتمل' ? 'badge-positive' : 'badge-neutral'}">${this.escapeHtml(p.status || 'نشط')}</span></td>
                  <td><span dir="ltr">${this.formatDate(p.orderDate)}</span></td>
                  <td>
                    <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/purchase-orders/' + encodeURIComponent('${this.escapeHtml(p.poNumber)}'))">
                      <span>عرض</span>
                    </button>
                  </td>
                </tr>
              `).join('');
            }
          }
        }
      } catch (err) {
        console.error('Failed to load supplier detail:', err);
      }
    }

    async syncSuppliers() {
      try {
        const syncBtn = document.getElementById('fin-supplier-btn-sync');
        if (syncBtn) {
          syncBtn.disabled = true;
          syncBtn.innerHTML = '<span>جاري المزامنة...</span>';
        }
        const res = await window.ChatApi.syncFinanceSuppliers();
        if (syncBtn) {
          syncBtn.disabled = false;
          syncBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/></svg><span>مزامنة الموردين من الأقسام</span>';
        }
        if (res && res.success) {
          alert(res.message || 'تمت مزامنة وتوحيد الموردين من المعمل والمستودع بنجاح!');
          await this.loadSuppliersData();
        } else {
          alert(res?.message || 'تعذر إتمام المزامنة.');
        }
      } catch (err) {
        console.error('Failed to sync suppliers:', err);
        alert('حدث خطأ أثناء مزامنة الموردين.');
      }
    }

    openAddSupplierModal() {
      const modal = document.getElementById('finance-supplier-modal-backdrop');
      const title = document.getElementById('fin-supplier-modal-title');
      const idInp = document.getElementById('supplier-form-id');
      const nameInp = document.getElementById('supplier-form-name');
      const codeInp = document.getElementById('supplier-form-code');
      const matInp = document.getElementById('supplier-form-material');
      const deptInp = document.getElementById('supplier-form-dept');
      const phoneInp = document.getElementById('supplier-form-phone');
      const taxInp = document.getElementById('supplier-form-tax');
      const contactInp = document.getElementById('supplier-form-contact');
      const labRefInp = document.getElementById('supplier-form-lab-ref');
      const impInp = document.getElementById('supplier-form-import-num');
      const addrInp = document.getElementById('supplier-form-address');
      const notesInp = document.getElementById('supplier-form-notes');

      if (title) title.textContent = 'إضافة مورد مستقل جديد — الحسابات المالية';
      if (idInp) idInp.value = '';
      if (nameInp) nameInp.value = '';
      if (codeInp) {
        const rnd = Math.floor(1000 + Math.random() * 9000);
        codeInp.value = `SUP-${rnd}`;
      }
      if (matInp) matInp.value = '';
      if (deptInp) deptInp.value = 'FINANCE';
      if (phoneInp) phoneInp.value = '';
      if (taxInp) taxInp.value = '';
      if (contactInp) contactInp.value = '';
      if (labRefInp) labRefInp.value = '';
      if (impInp) impInp.value = '';
      if (addrInp) addrInp.value = '';
      if (notesInp) notesInp.value = '';

      if (modal) modal.style.display = 'flex';
    }

    async openEditSupplierModal(id) {
      if (!id) return;
      try {
        const res = await window.ChatApi.getFinanceSupplierDetails(id);
        if (res && res.success && res.data) {
          const d = res.data;
          const modal = document.getElementById('finance-supplier-modal-backdrop');
          const title = document.getElementById('fin-supplier-modal-title');
          if (title) title.textContent = `تعديل بيانات المورد: ${d.name}`;

          const idInp = document.getElementById('supplier-form-id');
          if (idInp) idInp.value = d.id;
          const nameInp = document.getElementById('supplier-form-name');
          if (nameInp) nameInp.value = d.name || '';
          const codeInp = document.getElementById('supplier-form-code');
          if (codeInp) codeInp.value = d.supplierCode || '';
          const matInp = document.getElementById('supplier-form-material');
          if (matInp) matInp.value = d.primaryMaterialName || '';
          const typeInp = document.getElementById('supplier-form-type');
          if (typeInp && d.primaryMaterialType) typeInp.value = d.primaryMaterialType;
          const deptInp = document.getElementById('supplier-form-dept');
          if (deptInp && d.sourceDepartment) deptInp.value = d.sourceDepartment;
          const phoneInp = document.getElementById('supplier-form-phone');
          if (phoneInp) phoneInp.value = d.phoneNumber || '';
          const taxInp = document.getElementById('supplier-form-tax');
          if (taxInp) taxInp.value = d.taxNumber || '';
          const contactInp = document.getElementById('supplier-form-contact');
          if (contactInp) contactInp.value = d.contactPerson || '';
          const labRefInp = document.getElementById('supplier-form-lab-ref');
          if (labRefInp) labRefInp.value = d.relatedLaboratoryReferenceNumber || '';
          const impInp = document.getElementById('supplier-form-import-num');
          if (impInp) impInp.value = d.relatedWarehouseImportNumber || '';
          const addrInp = document.getElementById('supplier-form-address');
          if (addrInp) addrInp.value = d.address || '';
          const notesInp = document.getElementById('supplier-form-notes');
          if (notesInp) notesInp.value = d.notes || '';

          if (modal) modal.style.display = 'flex';
        }
      } catch (err) {
        console.error('Failed to load supplier for editing:', err);
      }
    }

    closeSupplierModal() {
      const modal = document.getElementById('finance-supplier-modal-backdrop');
      if (modal) modal.style.display = 'none';
    }

    async submitSupplierForm(e) {
      e.preventDefault();
      const id = document.getElementById('supplier-form-id')?.value;
      const payload = {
        name: document.getElementById('supplier-form-name')?.value?.trim(),
        supplierCode: document.getElementById('supplier-form-code')?.value?.trim(),
        primaryMaterialName: document.getElementById('supplier-form-material')?.value?.trim(),
        primaryMaterialType: document.getElementById('supplier-form-type')?.value,
        sourceDepartment: document.getElementById('supplier-form-dept')?.value || 'FINANCE',
        phoneNumber: document.getElementById('supplier-form-phone')?.value?.trim(),
        taxNumber: document.getElementById('supplier-form-tax')?.value?.trim(),
        contactPerson: document.getElementById('supplier-form-contact')?.value?.trim(),
        relatedLaboratoryReferenceNumber: document.getElementById('supplier-form-lab-ref')?.value?.trim(),
        relatedWarehouseImportNumber: document.getElementById('supplier-form-import-num')?.value?.trim(),
        address: document.getElementById('supplier-form-address')?.value?.trim(),
        notes: document.getElementById('supplier-form-notes')?.value?.trim()
      };

      if (!payload.name) {
        alert('اسم المورد مطلوب.');
        return;
      }

      try {
        let res;
        if (id) {
          res = await window.ChatApi.updateFinanceSupplier(id, payload);
        } else {
          res = await window.ChatApi.createFinanceSupplier(payload);
        }

        if (res && res.success) {
          alert(id ? 'تم تعديل بيانات المورد بنجاح!' : 'تم إضافة المورد الجديد بنجاح!');
          this.closeSupplierModal();
          await this.loadSuppliersData();
          if (id && this.currentView === 'supplier-detail') {
            await this.loadSupplierDetail(id);
          }
        } else {
          alert(res?.message || 'تعذر حفظ بيانات المورد.');
        }
      } catch (err) {
        console.error('Failed to save supplier:', err);
        alert('حدث خطأ أثناء حفظ بيانات المورد.');
      }
    }

    openAddSupplierTransactionModal(id, name) {
      if (!id) return;
      const modal = document.getElementById('finance-supplier-tx-modal-backdrop');
      const supIdInp = document.getElementById('supplier-tx-sup-id');
      const supNameDisp = document.getElementById('supplier-tx-sup-name-disp');
      const dateInp = document.getElementById('supplier-tx-date');
      const amountInp = document.getElementById('supplier-tx-amount');
      const refInp = document.getElementById('supplier-tx-ref');
      const descInp = document.getElementById('supplier-tx-desc');

      if (supIdInp) supIdInp.value = id;
      if (supNameDisp) supNameDisp.textContent = name || `المورد #${id}`;
      if (dateInp) dateInp.value = new Date().toISOString().slice(0, 10);
      if (amountInp) amountInp.value = '';
      if (refInp) refInp.value = '';
      if (descInp) descInp.value = '';

      if (modal) modal.style.display = 'flex';
    }

    closeSupplierTransactionModal() {
      const modal = document.getElementById('finance-supplier-tx-modal-backdrop');
      if (modal) modal.style.display = 'none';
    }

    async submitSupplierTransaction(e) {
      e.preventDefault();
      const supId = document.getElementById('supplier-tx-sup-id')?.value;
      if (!supId) return;

      const amount = parseFloat(document.getElementById('supplier-tx-amount')?.value);
      if (isNaN(amount) || amount <= 0) {
        alert('يرجى إدخال مبلغ صحيح أكبر من الصفر.');
        return;
      }

      const payload = {
        transactionType: document.getElementById('supplier-tx-type')?.value,
        amount: amount,
        transactionDate: document.getElementById('supplier-tx-date')?.value || new Date().toISOString(),
        paymentMethod: document.getElementById('supplier-tx-method')?.value,
        referenceNumber: document.getElementById('supplier-tx-ref')?.value?.trim(),
        description: document.getElementById('supplier-tx-desc')?.value?.trim(),
        notes: document.getElementById('supplier-tx-desc')?.value?.trim()
      };

      if (!payload.description) {
        alert('البيان والوصف مطلوب.');
        return;
      }

      try {
        const res = await window.ChatApi.addFinanceSupplierTransaction(supId, payload);
        if (res && res.success) {
          alert('تم تسجيل المعاملة المالية وتحديث الرصيد بنجاح!');
          this.closeSupplierTransactionModal();
          await this.loadSupplierDetail(supId);
          await this.loadSuppliersData();
        } else {
          alert(res?.message || 'تعذر تسجيل المعاملة المالية.');
        }
      } catch (err) {
        console.error('Failed to add supplier transaction:', err);
        alert('حدث خطأ أثناء حفظ المعاملة المالية.');
      }
    }

    /* ------------------------------------------------------------------------
       3. PURCHASE ORDERS VIEW LOGIC (/finance/purchase-orders)
       ------------------------------------------------------------------------ */
    async loadAllPurchaseOrdersData() {
      const loadingEl = document.getElementById('finance-pos-all-loading');
      const emptyEl = document.getElementById('finance-pos-all-empty');
      const tbody = document.getElementById('finance-pos-all-tbody');
      const countBadge = document.getElementById('finance-pos-all-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinancePurchaseOrders();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.posRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} أمر شراء`;
          this.renderAllPurchaseOrdersTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load all purchase orders:', err);
      }
    }

    filterPurchaseOrdersClientSide() {
      if (!this.posRawList) return;
      const term = document.getElementById('finance-pos-search-input')?.value?.trim().toLowerCase() || '';
      const statusFilter = document.getElementById('finance-pos-status-select')?.value || 'all';

      const filtered = this.posRawList.filter(p => {
        const matchesTerm = !term ||
          (p.poNumber && p.poNumber.toLowerCase().includes(term)) ||
          (p.supplier && p.supplier.toLowerCase().includes(term)) ||
          (p.materialName && p.materialName.toLowerCase().includes(term)) ||
          (p.material && p.material.toLowerCase().includes(term));

        let matchesStatus = true;
        if (statusFilter === 'active') matchesStatus = (p.remainingQuantity > 0.001);
        else if (statusFilter === 'completed') matchesStatus = (p.remainingQuantity <= 0.001);
        else if (statusFilter === 'unpaid') matchesStatus = (p.paymentStatus === 'غير مدفوع' || !p.paymentStatus);
        else if (statusFilter === 'partial') matchesStatus = (p.paymentStatus === 'مدفوع جزئياً');
        else if (statusFilter === 'paid') matchesStatus = (p.paymentStatus === 'مدفوع بالكامل');

        return matchesTerm && matchesStatus;
      });

      this.renderAllPurchaseOrdersTable(filtered);
    }

    renderAllPurchaseOrdersTable(items) {
      const tbody = document.getElementById('finance-pos-all-tbody');
      const emptyEl = document.getElementById('finance-pos-all-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(p => {
        const isCompleted = p.remainingQuantity <= 0.001;
        const statusArabic = isCompleted ? 'مكتمل' : 'نشط';
        const statusBadgeClass = isCompleted ? 'badge-positive' : 'badge-neutral';
        const unit = p.unit || 'طن';
        const mat = p.materialName || p.material || 'مواد خام';

        return `
          <tr>
            <td><strong dir="ltr" style="font-family: monospace; color: #D97706; font-size: 0.95rem;">${this.escapeHtml(p.poNumber)}</strong></td>
            <td><strong>${this.escapeHtml(p.supplier || '-')}</strong></td>
            <td>${this.escapeHtml(mat)}</td>
            <td><strong dir="ltr">${this.formatWeight(p.quantity)} ${unit}</strong></td>
            <td><strong dir="ltr" style="color: #059669;">${this.formatWeight(p.deliveredQuantity)} ${unit}</strong></td>
            <td><strong dir="ltr" style="color: ${p.remainingQuantity > 0 ? '#D97706' : '#059669'};">${this.formatWeight(p.remainingQuantity)} ${unit}</strong></td>
            <td>${this.escapeHtml(unit)}</td>
            <td><strong dir="ltr">${this.formatCurrency(p.tonPrice)}</strong></td>
            <td><strong dir="ltr" style="color: #0284C7; font-weight: 800;">${this.formatCurrency(p.totalValue)}</strong></td>
            <td><span dir="ltr">${this.formatDate(p.orderDate)}</span></td>
            <td><span class="finance-status-badge ${statusBadgeClass}">${this.escapeHtml(statusArabic)}</span></td>
            <td><span class="finance-status-badge ${p.paymentStatus === 'مدفوع بالكامل' ? 'badge-positive' : (p.paymentStatus === 'مدفوع جزئياً' ? 'badge-neutral' : 'badge-waste')}">${this.escapeHtml(p.paymentStatus || 'غير مدفوع')}</span></td>
            <td style="white-space: nowrap;">
              <div style="display: flex; gap: 6px;">
                <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/purchase-orders/' + encodeURIComponent('${this.escapeHtml(p.poNumber)}'))">
                  <span>عرض</span>
                </button>
                <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.openEditPurchaseOrderModal(${p.id})" style="background: #E2E8F0; color: #1E293B;">
                  <span>تعديل</span>
                </button>
              </div>
            </td>
          </tr>
        `;
      }).join('');
    }

    async openAddPurchaseOrderModal() {
      const modal = document.getElementById('finance-po-modal-backdrop');
      const title = document.getElementById('fin-po-modal-title');
      const idInp = document.getElementById('po-form-id');
      const numInp = document.getElementById('po-form-number');
      const supSelect = document.getElementById('po-form-supplier-select');
      const matInp = document.getElementById('po-form-material');
      const qtyInp = document.getElementById('po-form-quantity');
      const unitInp = document.getElementById('po-form-unit');
      const priceInp = document.getElementById('po-form-unit-price');
      const totalInp = document.getElementById('po-form-total-value');
      const dateInp = document.getElementById('po-form-order-date');
      const delivDateInp = document.getElementById('po-form-delivery-date');
      const destInp = document.getElementById('po-form-destination');
      const notesInp = document.getElementById('po-form-notes');
      const warnEl = document.getElementById('po-form-qty-warning');

      if (title) title.textContent = 'إضافة أمر شراء جديد — إدارة المشتريات والتعاقدات';
      if (idInp) idInp.value = '';
      if (warnEl) warnEl.style.display = 'none';

      // Auto-generate PO Number: PO-YYYY-XXXX
      const year = new Date().getFullYear();
      const rnd = Math.floor(1000 + Math.random() * 9000);
      if (numInp) numInp.value = `PO-${year}-${rnd}`;

      // Populate independent suppliers
      if (supSelect) {
        try {
          const res = await window.ChatApi.getFinanceSuppliers();
          if (res && res.success && Array.isArray(res.data)) {
            supSelect.innerHTML = '<option value="">-- اختر المورد من القائمة --</option>' +
              res.data.map(s => `<option value="${s.id}" data-material="${this.escapeHtml(s.primaryMaterialName || '')}">${this.escapeHtml(s.name)} (${s.supplierCode})</option>`).join('');
          }
        } catch (err) {
          console.error('Failed to load suppliers in PO modal:', err);
        }

        supSelect.onchange = () => {
          const opt = supSelect.options[supSelect.selectedIndex];
          const mat = opt?.getAttribute('data-material');
          if (mat && matInp && !matInp.value) matInp.value = mat;
        };
      }

      if (matInp) matInp.value = 'مواد خام خرسانية';
      if (qtyInp) qtyInp.value = '';
      if (unitInp) unitInp.value = 'طن';
      if (priceInp) priceInp.value = '';
      if (totalInp) totalInp.value = '0.00 ج.م';
      if (dateInp) dateInp.value = new Date().toISOString().slice(0, 10);
      if (delivDateInp) delivDateInp.value = '';
      if (destInp) destInp.value = 'Cairo Factory';
      if (notesInp) notesInp.value = '';

      if (modal) modal.style.display = 'flex';
    }

    async openEditPurchaseOrderModal(id) {
      if (!id) return;
      try {
        const po = this.posRawList?.find(p => p.id === id);
        if (!po) return;

        const modal = document.getElementById('finance-po-modal-backdrop');
        const title = document.getElementById('fin-po-modal-title');
        const idInp = document.getElementById('po-form-id');
        const numInp = document.getElementById('po-form-number');
        const supSelect = document.getElementById('po-form-supplier-select');
        const matInp = document.getElementById('po-form-material');
        const qtyInp = document.getElementById('po-form-quantity');
        const deliveredHidden = document.getElementById('po-form-delivered-qty');
        const unitInp = document.getElementById('po-form-unit');
        const priceInp = document.getElementById('po-form-unit-price');
        const dateInp = document.getElementById('po-form-order-date');
        const delivDateInp = document.getElementById('po-form-delivery-date');
        const destInp = document.getElementById('po-form-destination');
        const notesInp = document.getElementById('po-form-notes');
        const warnEl = document.getElementById('po-form-qty-warning');

        if (title) title.textContent = `تعديل أمر الشراء: ${po.poNumber}`;
        if (idInp) idInp.value = po.id;
        if (numInp) numInp.value = po.poNumber;
        if (deliveredHidden) deliveredHidden.value = po.deliveredQuantity || 0;

        if (supSelect) {
          try {
            const res = await window.ChatApi.getFinanceSuppliers();
            if (res && res.success && Array.isArray(res.data)) {
              supSelect.innerHTML = '<option value="">-- اختر المورد من القائمة --</option>' +
                res.data.map(s => `<option value="${s.id}">${this.escapeHtml(s.name)} (${s.supplierCode})</option>`).join('');
              if (po.supplierId) supSelect.value = po.supplierId;
            }
          } catch (err) {
            console.error('Failed to load suppliers:', err);
          }
        }

        if (matInp) matInp.value = po.materialName || po.material || '';
        if (qtyInp) qtyInp.value = po.quantity;
        if (unitInp) unitInp.value = po.unit || 'طن';
        if (priceInp) priceInp.value = po.tonPrice;
        if (dateInp) dateInp.value = po.orderDate ? po.orderDate.slice(0, 10) : '';
        if (delivDateInp) delivDateInp.value = po.expectedDeliveryDate ? po.expectedDeliveryDate.slice(0, 10) : '';
        if (destInp) destInp.value = po.destination || 'Cairo Factory';
        if (notesInp) notesInp.value = po.notes || '';

        this.calculatePoTotalValue();
        if (warnEl) {
          if (po.deliveredQuantity > 0) {
            warnEl.style.display = 'block';
            warnEl.textContent = `تم تسليم ${this.formatWeight(po.deliveredQuantity)} طن بالفعل من هذا الأمر. لا يمكن تقليل الكمية المتعاقدة عن ذلك.`;
          } else {
            warnEl.style.display = 'none';
          }
        }

        if (modal) modal.style.display = 'flex';
      } catch (err) {
        console.error('Failed to open edit PO modal:', err);
      }
    }

    closePurchaseOrderModal() {
      const modal = document.getElementById('finance-po-modal-backdrop');
      if (modal) modal.style.display = 'none';
    }

    calculatePoTotalValue() {
      const qty = parseFloat(document.getElementById('po-form-quantity')?.value) || 0;
      const price = parseFloat(document.getElementById('po-form-unit-price')?.value) || 0;
      const total = Math.round(qty * price * 100) / 100;
      const totalInp = document.getElementById('po-form-total-value');
      if (totalInp) totalInp.value = `${this.formatCurrency(total)}`;
    }

    async submitPurchaseOrderForm(e) {
      e.preventDefault();
      const id = document.getElementById('po-form-id')?.value;
      const supSelect = document.getElementById('po-form-supplier-select');
      const supplierId = supSelect?.value ? parseInt(supSelect.value, 10) : null;
      const supplierName = supSelect?.options[supSelect.selectedIndex]?.text?.split('(')[0]?.trim();

      const quantity = parseFloat(document.getElementById('po-form-quantity')?.value);
      const unitPrice = parseFloat(document.getElementById('po-form-unit-price')?.value);
      const deliveredQty = parseFloat(document.getElementById('po-form-delivered-qty')?.value) || 0;

      if (!supplierId) {
        alert('يرجى اختيار المورد.');
        return;
      }
      if (isNaN(quantity) || quantity <= 0) {
        alert('يرجى إدخال كمية صحيحة أكبر من الصفر.');
        return;
      }
      if (id && quantity < deliveredQty) {
        alert(`لا يمكن تقليل كمية أمر الشراء عن الكمية المسلمة بالفعل (${this.formatWeight(deliveredQty)} طن).`);
        return;
      }
      if (isNaN(unitPrice) || unitPrice <= 0) {
        alert('يرجى إدخال سعر وحدة صحيح أكبر من الصفر.');
        return;
      }

      const payload = {
        poNumber: document.getElementById('po-form-number')?.value?.trim(),
        supplierId: supplierId,
        supplier: supplierName,
        materialName: document.getElementById('po-form-material')?.value?.trim(),
        quantity: quantity,
        unit: document.getElementById('po-form-unit')?.value || 'طن',
        unitPrice: unitPrice,
        orderDate: document.getElementById('po-form-order-date')?.value || new Date().toISOString(),
        expectedDeliveryDate: document.getElementById('po-form-delivery-date')?.value || null,
        destination: document.getElementById('po-form-destination')?.value?.trim() || 'Cairo Factory',
        notes: document.getElementById('po-form-notes')?.value?.trim()
      };

      try {
        let res;
        if (id) {
          res = await window.ChatApi.updateFinancePurchaseOrder(id, payload);
        } else {
          res = await window.ChatApi.createFinancePurchaseOrder(payload);
        }

        if (res && res.success) {
          alert(id ? 'تم تعديل أمر الشراء بنجاح!' : 'تم إضافة أمر الشراء بنجاح وحفظه في قاعدة البيانات!');
          this.closePurchaseOrderModal();
          await this.loadAllPurchaseOrdersData();
          await this.loadPoDropdown();
          await this.loadPurchaseOrdersHistory();
        } else {
          alert(res?.message || 'تعذر حفظ أمر الشراء.');
        }
      } catch (err) {
        console.error('Failed to save purchase order:', err);
        alert('حدث خطأ أثناء حفظ أمر الشراء.');
      }
    }

    /* ------------------------------------------------------------------------
       5. BANK VIEW LOGIC (/finance/bank)
       ------------------------------------------------------------------------ */
    async loadBankData() {
      const loadingEl = document.getElementById('finance-bank-loading');
      const emptyEl = document.getElementById('finance-bank-empty');
      const tbody = document.getElementById('finance-bank-tbody');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceBankSummary();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          const balEl = document.getElementById('finance-bank-balance-val');
          if (balEl) balEl.textContent = this.formatCurrency(d.currentBalance);

          const depEl = document.getElementById('finance-bank-deposits-val');
          if (depEl) depEl.textContent = this.formatCurrency(d.totalReceived);

          const withEl = document.getElementById('finance-bank-withdrawals-val');
          if (withEl) withEl.textContent = this.formatCurrency(d.totalSent);

          const cntEl = document.getElementById('finance-bank-count-val');
          if (cntEl) cntEl.textContent = `${d.totalTransactionsCount} معاملة`;

          this.renderBankTable(d.transactions || []);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load bank summary:', err);
      }
    }

    renderBankTable(items) {
      const tbody = document.getElementById('finance-bank-tbody');
      const emptyEl = document.getElementById('finance-bank-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(t => {
        const isDep = (t.direction === 'إيداع');
        return `
          <tr>
            <td><strong dir="ltr">${this.formatDate(t.transactionDate)}</strong></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(t.transactionType)}</span></td>
            <td><strong dir="ltr" style="font-family: monospace; color: ${isDep ? '#059669' : '#DC2626'};">${this.formatCurrency(t.amount)}</strong></td>
            <td><span class="finance-status-badge ${isDep ? 'badge-positive' : 'badge-waste'}">${isDep ? 'إيداع / Received' : 'صرف / Sent'}</span></td>
            <td><strong>${this.escapeHtml(t.bankName)}</strong></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(t.referenceNumber || '-')}</span></td>
            <td>${this.escapeHtml(t.description)}</td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(t.createdBy || '-')}</span></td>
            <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(t.notes || '-')}</span></td>
          </tr>
        `;
      }).join('');
    }

    openAddBankModal() {
      const backdrop = document.getElementById('finance-add-bank-modal-backdrop');
      if (backdrop) backdrop.style.display = 'flex';
      const dateInp = document.getElementById('bank-trans-date');
      if (dateInp && !dateInp.value) {
        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, '0');
        const d = String(now.getDate()).padStart(2, '0');
        dateInp.value = `${y}-${m}-${d}`;
      }
    }

    closeAddBankModal() {
      const backdrop = document.getElementById('finance-add-bank-modal-backdrop');
      if (backdrop) backdrop.style.display = 'none';
      const form = document.getElementById('finance-bank-form');
      if (form) form.reset();
    }

    async submitBankTransaction(e) {
      e.preventDefault();
      const dateVal = document.getElementById('bank-trans-date')?.value;
      const bankVal = document.getElementById('bank-trans-bank-name')?.value?.trim();
      const typeVal = document.getElementById('bank-trans-type')?.value;
      const dirVal = document.getElementById('bank-trans-direction')?.value;
      const amtVal = parseFloat(document.getElementById('bank-trans-amount')?.value);
      const refVal = document.getElementById('bank-trans-ref')?.value?.trim();
      const descVal = document.getElementById('bank-trans-desc')?.value?.trim();
      const notesVal = document.getElementById('bank-trans-notes')?.value?.trim();

      if (!dateVal || !bankVal || !typeVal || !dirVal || isNaN(amtVal) || amtVal <= 0 || !descVal) {
        alert('يرجى ملء جميع الحقول الإلزامية والتأكد من إدخال مبلغ صحيح.');
        return;
      }

      const submitBtn = document.getElementById('bank-modal-submit-btn');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري الحفظ...';
      }

      try {
        const payload = {
          transactionDate: dateVal,
          bankName: bankVal,
          transactionType: typeVal,
          direction: dirVal,
          amount: amtVal,
          referenceNumber: refVal,
          description: descVal,
          notes: notesVal
        };

        const res = await window.ChatApi.createFinanceBankTransaction(payload);
        if (res && res.success) {
          this.closeAddBankModal();
          this.loadBankData();
        } else {
          alert(res?.message || 'تعذر حفظ المعاملة البنكية.');
        }
      } catch (err) {
        console.error('Failed to submit bank transaction:', err);
        alert('حدث خطأ أثناء الاتصال بالخادم.');
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ المعاملة البنكية';
        }
      }
    }

    /* ------------------------------------------------------------------------
       6. EXPENSES VIEW LOGIC (/finance/expenses)
       ------------------------------------------------------------------------ */
    async loadExpensesData() {
      const loadingEl = document.getElementById('finance-expenses-loading');
      const emptyEl = document.getElementById('finance-expenses-empty');
      const tbody = document.getElementById('finance-expenses-tbody');

      const catVal = document.getElementById('finance-exp-category-filter')?.value || 'all';
      const fromVal = document.getElementById('finance-exp-date-from')?.value || '';
      const toVal = document.getElementById('finance-exp-date-to')?.value || '';

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceExpenses({
          category: catVal === 'all' ? '' : catVal,
          fromDate: fromVal,
          toDate: toVal
        });
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          const totEl = document.getElementById('finance-exp-total-val');
          if (totEl) totEl.textContent = this.formatCurrency(d.totalExpenses);

          const todEl = document.getElementById('finance-exp-today-val');
          if (todEl) todEl.textContent = this.formatCurrency(d.todayExpenses);

          const rngEl = document.getElementById('finance-exp-range-val');
          if (rngEl) rngEl.textContent = this.formatCurrency(d.rangeExpenses);

          this.renderExpenseCategories(d.categoryBreakdown || []);
          this.renderExpensesTable(d.expenses || []);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load expenses:', err);
      }
    }

    renderExpenseCategories(categories) {
      const container = document.getElementById('finance-exp-categories-grid');
      if (!container) return;

      if (!categories || categories.length === 0) {
        container.innerHTML = '<div style="color: #94A3B8; padding: 1rem;">لا توجد مصاريف مصنفة حتى الآن.</div>';
        return;
      }

      container.innerHTML = categories.map(c => `
        <div class="finance-forecast-tile">
          <span class="finance-forecast-label">${this.escapeHtml(c.category)}</span>
          <span class="finance-forecast-val" style="color: #EA580C;">${this.formatCurrency(c.totalAmount)}</span>
          <span class="finance-forecast-sub">${c.count} معاملة (${Number(c.percentage || 0).toFixed(1)}%)</span>
        </div>
      `).join('');
    }

    renderExpensesTable(items) {
      const tbody = document.getElementById('finance-expenses-tbody');
      const emptyEl = document.getElementById('finance-expenses-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(x => `
        <tr>
          <td><strong dir="ltr">${this.formatDate(x.expenseDate)}</strong></td>
          <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(x.category)}</span></td>
          <td><strong dir="ltr" style="font-family: monospace; color: #EA580C;">${this.formatCurrency(x.amount)}</strong></td>
          <td><strong>${this.escapeHtml(x.title)}</strong></td>
          <td>${this.escapeHtml(x.beneficiary || '-')}</td>
          <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(x.paymentMethod || 'نقدي')}</span></td>
          <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(x.referenceNumber || '-')}</span></td>
          <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(x.createdBy || '-')}</span></td>
          <td><span style="font-size: 0.825rem; color: #64748B;">${this.escapeHtml(x.notes || '-')}</span></td>
        </tr>
      `).join('');
    }

    openAddExpenseModal() {
      const backdrop = document.getElementById('finance-add-expense-modal-backdrop');
      if (backdrop) backdrop.style.display = 'flex';
      const dateInp = document.getElementById('exp-date');
      if (dateInp && !dateInp.value) {
        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, '0');
        const d = String(now.getDate()).padStart(2, '0');
        dateInp.value = `${y}-${m}-${d}`;
      }
    }

    closeAddExpenseModal() {
      const backdrop = document.getElementById('finance-add-expense-modal-backdrop');
      if (backdrop) backdrop.style.display = 'none';
      const form = document.getElementById('finance-expense-form');
      if (form) form.reset();
    }

    async submitExpense(e) {
      e.preventDefault();
      const dateVal = document.getElementById('exp-date')?.value;
      const catVal = document.getElementById('exp-category')?.value;
      const amtVal = parseFloat(document.getElementById('exp-amount')?.value);
      const titleVal = document.getElementById('exp-title')?.value?.trim();
      const benVal = document.getElementById('exp-beneficiary')?.value?.trim();
      const payVal = document.getElementById('exp-pay-method')?.value;
      const refVal = document.getElementById('exp-ref')?.value?.trim();
      const notesVal = document.getElementById('exp-notes')?.value?.trim();

      if (!dateVal || !catVal || isNaN(amtVal) || amtVal <= 0 || !titleVal) {
        alert('يرجى ملء جميع الحقول الإلزامية والتأكد من إدخال مبلغ صحيح.');
        return;
      }

      const submitBtn = document.getElementById('exp-modal-submit-btn');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري الحفظ...';
      }

      try {
        const payload = {
          expenseDate: dateVal,
          category: catVal,
          amount: amtVal,
          title: titleVal,
          beneficiary: benVal,
          paymentMethod: payVal,
          referenceNumber: refVal,
          notes: notesVal
        };

        const res = await window.ChatApi.createFinanceExpense(payload);
        if (res && res.success) {
          this.closeAddExpenseModal();
          this.loadExpensesData();
        } else {
          alert(res?.message || 'تعذر حفظ المصروف.');
        }
      } catch (err) {
        console.error('Failed to submit expense:', err);
        alert('حدث خطأ أثناء الاتصال بالخادم.');
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ المصروف';
        }
      }
    }

    /* ------------------------------------------------------------------------
       7. EMPLOYEES VIEW LOGIC (/finance/employees)
       ------------------------------------------------------------------------ */
    async loadEmployeesData() {
      const loadingEl = document.getElementById('finance-emp-loading');
      const emptyEl = document.getElementById('finance-emp-empty');
      const tbody = document.getElementById('finance-employees-tbody');
      const countBadge = document.getElementById('finance-emp-count');

      if (loadingEl) loadingEl.style.display = 'flex';
      if (emptyEl) emptyEl.style.display = 'none';

      try {
        const res = await window.ChatApi.getFinanceEmployees();
        if (loadingEl) loadingEl.style.display = 'none';

        if (res && res.success && Array.isArray(res.data)) {
          this.employeesRawList = res.data;
          if (countBadge) countBadge.textContent = `${res.data.length} موظف`;
          this.renderEmployeesTable(res.data);
        } else {
          if (emptyEl) emptyEl.style.display = 'flex';
          if (tbody) tbody.innerHTML = '';
        }
      } catch (err) {
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'flex';
        console.error('Failed to load employees:', err);
      }
    }

    filterEmployeesClientSide() {
      if (!this.employeesRawList) return;
      const term = document.getElementById('finance-emp-search-input')?.value?.trim().toLowerCase() || '';
      if (!term) {
        this.renderEmployeesTable(this.employeesRawList);
        return;
      }
      const filtered = this.employeesRawList.filter(e =>
        (e.fullName && e.fullName.toLowerCase().includes(term)) ||
        (e.username && e.username.toLowerCase().includes(term)) ||
        (e.department && e.department.toLowerCase().includes(term)) ||
        (e.position && e.position.toLowerCase().includes(term))
      );
      this.renderEmployeesTable(filtered);
    }

    renderEmployeesTable(items) {
      const tbody = document.getElementById('finance-employees-tbody');
      const emptyEl = document.getElementById('finance-emp-empty');
      if (!tbody) return;

      if (!items || items.length === 0) {
        tbody.innerHTML = '';
        if (emptyEl) emptyEl.style.display = 'flex';
        return;
      }
      if (emptyEl) emptyEl.style.display = 'none';

      tbody.innerHTML = items.map(emp => {
        const salDisplay = emp.hasSalaryRecord
          ? `<strong dir="ltr" style="color:#059669;">${this.formatCurrency(emp.netSalary || emp.basicSalary)}</strong>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">غير مسجل</span>';

        const attDisplay = emp.hasAttendanceRecord
          ? `<span class="finance-status-badge badge-positive">حضور ${Number(emp.attendanceRate || 0).toFixed(0)}% (${emp.daysPresent} يوم)</span>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">جاهز للربط</span>';

        const lendDisplay = emp.hasLendingRecord
          ? `<strong dir="ltr" style="color:#DC2626;">${this.formatCurrency(emp.remainingLending)}</strong>`
          : '<span style="color:#94A3B8; font-size:0.8rem;">لا توجد سلف</span>';

        return `
          <tr>
            <td><strong style="color: #0F172A; font-size: 0.95rem;">${this.escapeHtml(emp.fullName)}</strong></td>
            <td><span dir="ltr" style="font-family: monospace;">${this.escapeHtml(emp.username)}</span></td>
            <td><span class="finance-status-badge badge-neutral">${this.escapeHtml(emp.department || 'عام')}</span></td>
            <td><span style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(emp.position || emp.role)}</span></td>
            <td>${salDisplay}</td>
            <td>${attDisplay}</td>
            <td>${lendDisplay}</td>
            <td><span class="finance-status-badge badge-positive">${this.escapeHtml(emp.financialStatus || 'نشط')}</span></td>
            <td>
              <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.navigateTo('/finance/employees/' + encodeURIComponent('${emp.id}'))">
                <span>عرض التفاصيل</span>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"/></svg>
              </button>
            </td>
          </tr>
        `;
      }).join('');
    }

    async loadEmployeeDetail(id) {
      if (!id) return;
      try {
        const res = await window.ChatApi.getFinanceEmployeeDetail(id);
        if (res && res.success && res.data) {
          const emp = res.data;
          const nameEl = document.getElementById('emp-detail-name');
          const fnEl = document.getElementById('emp-detail-fullname');
          const unEl = document.getElementById('emp-detail-username');
          const dpEl = document.getElementById('emp-detail-dept');
          const roEl = document.getElementById('emp-detail-role');
          const stEl = document.getElementById('emp-detail-status');

          if (nameEl) nameEl.textContent = emp.fullName;
          if (fnEl) fnEl.textContent = emp.fullName;
          if (unEl) unEl.textContent = emp.username;
          if (dpEl) dpEl.textContent = `القسم: ${emp.department || '-'}`;
          if (roEl) roEl.textContent = `الوظيفة: ${emp.position || emp.role}`;
          if (stEl) stEl.textContent = `الحالة: ${emp.financialStatus || 'نشط'}`;

          const bsEl = document.getElementById('emp-detail-basic-salary');
          if (bsEl) bsEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.basicSalary) : 'غير محدد بالبيانات';

          const alEl = document.getElementById('emp-detail-allowances');
          if (alEl) alEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.allowances) : '0.00 ج.م';

          const deEl = document.getElementById('emp-detail-deductions');
          if (deEl) deEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.insuranceDeductions) : '0.00 ج.م';

          const nsEl = document.getElementById('emp-detail-net-salary');
          if (nsEl) nsEl.textContent = emp.hasSalaryRecord ? this.formatCurrency(emp.netSalary) : 'غير محدد بالبيانات';

          const pmSub = document.getElementById('emp-detail-pay-method-sub');
          if (pmSub) pmSub.textContent = `طريقة الدفع: ${emp.paymentMethod || 'غير محدد'}`;

          // Attendance
          const prEl = document.getElementById('emp-detail-att-present-badge');
          if (prEl) prEl.textContent = `حضور: ${emp.daysPresent} يوم`;
          const abEl = document.getElementById('emp-detail-att-absent-badge');
          if (abEl) abEl.textContent = `غياب: ${emp.daysAbsent} يوم`;
          const rtEl = document.getElementById('emp-detail-att-rate-badge');
          if (rtEl) rtEl.textContent = `النسبة: ${Number(emp.attendanceRate || 100).toFixed(0)}%`;

          const attTbody = document.getElementById('emp-detail-att-tbody');
          const attEmpty = document.getElementById('emp-detail-att-empty');
          const atts = emp.attendanceRecords || [];
          if (attTbody) {
            if (atts.length === 0) {
              attTbody.innerHTML = '';
              if (attEmpty) attEmpty.style.display = 'flex';
            } else {
              if (attEmpty) attEmpty.style.display = 'none';
              attTbody.innerHTML = atts.map(a => `
                <tr>
                  <td><strong dir="ltr">${this.formatDate(a.date)}</strong></td>
                  <td><span class="finance-status-badge ${a.status === 'حاضر' ? 'badge-positive' : 'badge-waste'}">${this.escapeHtml(a.status)}</span></td>
                  <td><span dir="ltr">${this.escapeHtml(a.checkInTime || '-')}</span></td>
                  <td><span dir="ltr">${this.escapeHtml(a.checkOutTime || '-')}</span></td>
                  <td>${this.escapeHtml(a.notes || '-')}</td>
                </tr>
              `).join('');
            }
          }

          // Lending
          const ltEl = document.getElementById('emp-detail-lend-total-badge');
          if (ltEl) ltEl.textContent = `إجمالي السلف: ${this.formatCurrency(emp.totalLending)}`;
          const lpEl = document.getElementById('emp-detail-lend-paid-badge');
          if (lpEl) lpEl.textContent = `المسدد: ${this.formatCurrency(emp.paidLending)}`;
          const lrEl = document.getElementById('emp-detail-lend-rem-badge');
          if (lrEl) lrEl.textContent = `المتبقي: ${this.formatCurrency(emp.remainingLending)}`;

          const lendTbody = document.getElementById('emp-detail-lend-tbody');
          const lendEmpty = document.getElementById('emp-detail-lend-empty');
          const lends = emp.lendings || [];
          if (lendTbody) {
            if (lends.length === 0) {
              lendTbody.innerHTML = '';
              if (lendEmpty) lendEmpty.style.display = 'flex';
            } else {
              if (lendEmpty) lendEmpty.style.display = 'none';
              lendTbody.innerHTML = lends.map(l => `
                <tr>
                  <td><strong dir="ltr">${this.formatDate(l.requestDate)}</strong></td>
                  <td><strong dir="ltr" style="font-family: monospace;">${this.formatCurrency(l.lendingAmount)}</strong></td>
                  <td><strong dir="ltr" style="color: #059669;">${this.formatCurrency(l.paidAmount)}</strong></td>
                  <td><strong dir="ltr" style="color: #DC2626;">${this.formatCurrency(l.remainingAmount)}</strong></td>
                  <td><span class="finance-status-badge badge-positive">${this.escapeHtml(l.approvalStatus)}</span></td>
                  <td>${this.escapeHtml(l.approvedBy || '-')}</td>
                  <td>${this.escapeHtml(l.notes || '-')}</td>
                </tr>
              `).join('');
            }
          }
        }
      } catch (err) {
        console.error('Failed to load employee detail:', err);
      }
    }

    /* ------------------------------------------------------------------------
       8. INVOICES MODULE LOGIC (/finance/invoices)
       ------------------------------------------------------------------------ */
    bindInvoicesEvents() {
      // Hub Card Quick Action Pills
      const hubNewInv = document.getElementById('hub-pill-new-invoice');
      if (hubNewInv) {
        hubNewInv.addEventListener('click', (e) => {
          e.stopPropagation();
          this.navigateTo('/finance/invoices');
          this.openCreateInvoiceModal();
        });
      }

      const hubViewInv = document.getElementById('hub-pill-view-invoices');
      if (hubViewInv) {
        hubViewInv.addEventListener('click', (e) => {
          e.stopPropagation();
          this.invoicesFilters.status = 'ALL';
          this.navigateTo('/finance/invoices');
        });
      }

      const hubIssuedInv = document.getElementById('hub-pill-issued-invoices');
      if (hubIssuedInv) {
        hubIssuedInv.addEventListener('click', (e) => {
          e.stopPropagation();
          this.invoicesFilters.status = 'ISSUED';
          this.navigateTo('/finance/invoices');
        });
      }

      const hubDraftInv = document.getElementById('hub-pill-draft-invoices');
      if (hubDraftInv) {
        hubDraftInv.addEventListener('click', (e) => {
          e.stopPropagation();
          this.invoicesFilters.status = 'DRAFT';
          this.navigateTo('/finance/invoices');
        });
      }

      const hubPrintedInv = document.getElementById('hub-pill-printed-invoices');
      if (hubPrintedInv) {
        hubPrintedInv.addEventListener('click', (e) => {
          e.stopPropagation();
          this.invoicesFilters.status = 'PRINTED';
          this.navigateTo('/finance/invoices');
        });
      }

      // Create New Invoice Button (In Invoices View Banner)
      const btnNewInv = document.getElementById('fin-inv-btn-create-new');
      if (btnNewInv) {
        btnNewInv.addEventListener('click', () => {
          this.openCreateInvoiceModal();
        });
      }

      // Invoices Filters
      const filterApplyBtn = document.getElementById('fin-inv-filter-apply-btn');
      if (filterApplyBtn) {
        filterApplyBtn.addEventListener('click', () => {
          this.invoicesPage = 1;
          this.loadInvoicesData();
        });
      }

      const filterResetBtn = document.getElementById('fin-inv-filter-reset-btn');
      if (filterResetBtn) {
        filterResetBtn.addEventListener('click', () => {
          const s = document.getElementById('fin-inv-search-input');
          const st = document.getElementById('fin-inv-filter-status-select');
          const prj = document.getElementById('fin-inv-filter-project-select');
          const df = document.getElementById('fin-inv-filter-date-from');
          const dt = document.getElementById('fin-inv-filter-date-to');
          if (s) s.value = '';
          if (st) st.value = 'ALL';
          if (prj) prj.value = '';
          if (df) df.value = '';
          if (dt) dt.value = '';
          this.invoicesFilters = { search: '', status: 'ALL', projectCodeId: '', fromDate: '', toDate: '' };
          this.invoicesPage = 1;
          this.loadInvoicesData();
        });
      }

      let searchInvTimeout = null;
      const searchInvInput = document.getElementById('fin-inv-search-input');
      if (searchInvInput) {
        searchInvInput.addEventListener('input', (e) => {
          clearTimeout(searchInvTimeout);
          searchInvTimeout = setTimeout(() => {
            this.invoicesFilters.search = e.target.value.trim();
            this.invoicesPage = 1;
            this.loadInvoicesData();
          }, 350);
        });
      }

      const statusSelect = document.getElementById('fin-inv-filter-status-select');
      if (statusSelect) {
        statusSelect.addEventListener('change', (e) => {
          this.invoicesFilters.status = e.target.value;
          this.invoicesPage = 1;
          this.loadInvoicesData();
        });
      }

      const prjSelect = document.getElementById('fin-inv-filter-project-select');
      if (prjSelect) {
        prjSelect.addEventListener('change', (e) => {
          this.invoicesFilters.projectCodeId = e.target.value;
          this.invoicesPage = 1;
          this.loadInvoicesData();
        });
      }

      // Modal Close Buttons
      const modalCloseBtn = document.getElementById('fin-invoice-modal-close-btn');
      if (modalCloseBtn) modalCloseBtn.addEventListener('click', () => this.closeCreateInvoiceModal());

      const viewCloseBtn = document.getElementById('fin-view-modal-close-btn');
      if (viewCloseBtn) viewCloseBtn.addEventListener('click', () => this.closeViewInvoiceModal());

      const viewBtnClose = document.getElementById('fin-view-btn-close');
      if (viewBtnClose) viewBtnClose.addEventListener('click', () => this.closeViewInvoiceModal());

      const viewBtnPrint = document.getElementById('fin-view-btn-print');
      if (viewBtnPrint) {
        viewBtnPrint.addEventListener('click', () => {
          if (this.currentViewingInvoiceId) {
            this.closeViewInvoiceModal();
            this.printInvoice(this.currentViewingInvoiceId);
          }
        });
      }

      // Print Modal Buttons
      const printDismissBtn = document.getElementById('fin-btn-dismiss-print');
      if (printDismissBtn) printDismissBtn.addEventListener('click', () => this.closePrintModal());

      const printExecBtn = document.getElementById('fin-btn-execute-print');
      if (printExecBtn) printExecBtn.addEventListener('click', () => this.executePrintInvoice());

      // Searchable Project Code Enum-Style Dropdown
      const comboTrigger = document.getElementById('fin-project-combo-trigger');
      if (comboTrigger) {
        comboTrigger.addEventListener('click', (e) => {
          e.stopPropagation();
          if (comboTrigger.disabled) return;
          this.toggleProjectCombo();
        });
      }

      const comboSearch = document.getElementById('fin-project-combo-search');
      if (comboSearch) {
        comboSearch.addEventListener('click', (e) => e.stopPropagation());
        comboSearch.addEventListener('input', (e) => this.renderProjectComboOptions(e.target.value));
      }

      document.addEventListener('click', (e) => {
        const combo = document.getElementById('fin-project-combo');
        if (combo && !combo.contains(e.target)) {
          this.toggleProjectCombo(false);
        }
      });

      // Apply uniform unit price across all auto-loaded project items
      const applyUniformBtn = document.getElementById('fin-btn-apply-uniform-price');
      if (applyUniformBtn) {
        applyUniformBtn.addEventListener('click', () => {
          const p = parseFloat(document.getElementById('fin-uniform-price-input')?.value);
          if (!isNaN(p) && p > 0) {
            document.querySelectorAll('#fin-invoice-items-tbody .fin-item-price-input').forEach(inp => {
              inp.value = p.toFixed(2);
            });
            this.recalculateProjectInvoiceTotals();
          } else {
            alert('يرجى إدخال سعر موحد صحيح أكبر من الصفر.');
          }
        });
      }

      // Live calculations on financial inputs
      ['fin-invoice-shipping-input', 'fin-invoice-tax-rate', 'fin-invoice-discount'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', () => this.recalculateProjectInvoiceTotals());
      });

      // Modal Footer Action Buttons
      const invoiceDraftBtn = document.getElementById('fin-invoice-btn-draft');
      if (invoiceDraftBtn) invoiceDraftBtn.addEventListener('click', () => this.saveProjectInvoice(false, false));

      const invoiceIssueBtn = document.getElementById('fin-invoice-btn-issue');
      if (invoiceIssueBtn) invoiceIssueBtn.addEventListener('click', () => this.saveProjectInvoice(true, false));

      const invoiceSavePrintBtn = document.getElementById('fin-invoice-btn-save-print');
      if (invoiceSavePrintBtn) invoiceSavePrintBtn.addEventListener('click', () => this.saveProjectInvoice(true, true));

      const invoiceCancelBtn = document.getElementById('fin-invoice-btn-cancel');
      if (invoiceCancelBtn) invoiceCancelBtn.addEventListener('click', () => this.closeCreateInvoiceModal());
    }

    async showInvoicesView() {
      this.currentView = 'invoices';
      this.hideAllSubViews();
      if (this.invoicesView) this.invoicesView.style.display = 'block';
      window.scrollTo({ top: 0, behavior: 'smooth' });

      // Update active nav state if needed
      await this.loadInvoiceStatistics();
      await this.loadInvoiceFilterProjectCodes();
      await this.loadInvoicesData();
    }

    async loadInvoiceStatistics() {
      try {
        const res = await window.ChatApi.getInvoiceStatistics();
        if (res && res.success && res.data) {
          const s = res.data;
          const totalEl = document.getElementById('fin-inv-stat-total');
          const issuedEl = document.getElementById('fin-inv-stat-issued');
          const draftsEl = document.getElementById('fin-inv-stat-drafts');
          const printedEl = document.getElementById('fin-inv-stat-printed');
          const amountEl = document.getElementById('fin-inv-stat-amount');
          const hubBadge = document.getElementById('finance-hub-invoices-badge');

          if (totalEl) totalEl.textContent = s.totalInvoices;
          if (issuedEl) issuedEl.textContent = s.issuedInvoices;
          if (draftsEl) draftsEl.textContent = s.draftInvoices;
          if (printedEl) printedEl.textContent = s.printedInvoices;
          if (amountEl) amountEl.textContent = `${this.formatCurrency(s.totalInvoicedAmount)}`;
          if (hubBadge) hubBadge.textContent = `${s.totalInvoices} فاتورة`;
        }
      } catch (err) {
        console.error('Failed to load invoice statistics:', err);
      }
    }

    async loadInvoiceFilterProjectCodes() {
      const select = document.getElementById('fin-inv-filter-project-select');
      if (!select || select.options.length > 1) return;

      try {
        const res = await window.ChatApi.getInvoiceProjectCodes();
        if (res && res.success && res.data) {
          this.invCachedProjects = res.data;
          select.innerHTML = '<option value="">جميع المشاريع</option>' +
            res.data.map(p => `<option value="${p.id}">${this.escapeHtml(p.code)} - ${this.escapeHtml(p.projectName)}</option>`).join('');
        }
      } catch (err) {
        console.error('Failed to load project codes:', err);
      }
    }

    async loadInvoicesData() {
      const loading = document.getElementById('fin-invoices-loading');
      const empty = document.getElementById('fin-invoices-empty');
      const tbody = document.getElementById('fin-invoices-tbody');
      const countBadge = document.getElementById('fin-inv-table-count-badge');

      if (loading) loading.style.display = 'block';
      if (empty) empty.style.display = 'none';

      try {
        const res = await window.ChatApi.getInvoices({
          search: this.invoicesFilters.search,
          status: this.invoicesFilters.status,
          projectCodeId: this.invoicesFilters.projectCodeId,
          fromDate: document.getElementById('fin-inv-filter-date-from')?.value || '',
          toDate: document.getElementById('fin-inv-filter-date-to')?.value || '',
          page: this.invoicesPage,
          pageSize: this.invoicesPageSize
        });

        if (loading) loading.style.display = 'none';

        if (res && res.success && res.data) {
          const invoices = res.data;
          this.invoicesTotalCount = res.totalCount || invoices.length;
          if (countBadge) countBadge.textContent = `${this.invoicesTotalCount} فاتورة`;

          if (invoices.length === 0) {
            if (tbody) tbody.innerHTML = '';
            if (empty) empty.style.display = 'block';
            return;
          }

          if (tbody) {
            tbody.innerHTML = invoices.map(inv => {
              const statusClass = inv.status === 'ISSUED' ? 'badge-positive' : (inv.status === 'DRAFT' ? 'badge-neutral' : 'badge-negative');
              const printBadge = inv.isPrinted
                ? '<span class="finance-status-badge badge-positive" style="background: #E0E7FF; color: #3730A3;">مطبوعة ✓</span>'
                : '<span class="finance-status-badge badge-neutral" style="color: #94A3B8;">غير مطبوعة</span>';

              const actionsHtml = `
                <div style="display: flex; gap: 6px; align-items: center;">
                  <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.viewInvoice(${inv.id})" title="عرض التفاصيل">
                    <span>عرض</span>
                  </button>
                  <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.printInvoice(${inv.id})" style="background: #E0F2FE; color: #0369A1;" title="طباعة">
                    <span>طباعة</span>
                  </button>
                  ${inv.status === 'DRAFT' ? `
                    <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.editInvoice(${inv.id})" style="background: #FEF3C7; color: #92400E;" title="تعديل المسودة">
                      <span>تعديل</span>
                    </button>
                    <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.issueDraftInvoice(${inv.id})" style="background: #D1FAE5; color: #065F46;" title="اعتماد وإصدار">
                      <span>إصدار</span>
                    </button>
                  ` : ''}
                  ${inv.status !== 'CANCELLED' ? `
                    <button type="button" class="finance-btn-table-action" onclick="window.FinanceManagementApp.cancelInvoice(${inv.id})" style="background: #FEE2E2; color: #991B1B;" title="إلغاء الفاتورة">
                      <span>إلغاء</span>
                    </button>
                  ` : ''}
                </div>
              `;

              return `
                <tr>
                  <td><strong dir="ltr" style="font-family: monospace; color: #0284C7;">${this.escapeHtml(inv.invoiceNumber)}</strong></td>
                  <td>${this.escapeHtml(inv.invoiceDateFormatted)}</td>
                  <td>
                    <span style="font-weight: 700; color: #1E3A8A; background: #DBEAFE; padding: 2px 8px; border-radius: 6px; font-size: 0.8rem;">${this.escapeHtml(inv.projectCodeValue || '-')}</span>
                    <div style="font-size: 0.75rem; color: #64748B;">${this.escapeHtml(inv.projectName || '')}</div>
                  </td>
                  <td>
                    <div style="font-weight: 700;">${this.escapeHtml(inv.entityDisplay)}</div>
                  </td>
                  <td><strong>${inv.poNumber ? (inv.invoicedQuantity ? `${this.formatWeight(inv.invoicedQuantity)} طن` : 'أمر شراء') : `${inv.transactionCount} شحنات`}</strong></td>
                  <td dir="ltr">${this.formatCurrency(inv.subtotal)}${inv.shippingPrice > 0 ? `<br><span style="font-size: 0.75rem; color: #059669; font-weight: 600;">+ شحن: ${this.formatCurrency(inv.shippingPrice)}</span>` : ''}</td>
                  <td dir="ltr" style="font-size: 0.8rem; color: #64748B;">
                    ضريبة (${inv.taxRate}%): ${this.formatCurrency(inv.taxAmount)}
                    ${inv.discountAmount > 0 ? `<br>خصم: -${this.formatCurrency(inv.discountAmount)}` : ''}
                  </td>
                  <td><strong dir="ltr" style="color: #0284C7; font-size: 0.95rem;">${this.formatCurrency(inv.totalAmount)}</strong></td>
                  <td><span class="finance-status-badge ${statusClass}">${this.escapeHtml(inv.statusArabic)}</span></td>
                  <td>${printBadge}</td>
                  <td>${this.escapeHtml(inv.createdBy || '-')}</td>
                  <td>${actionsHtml}</td>
                </tr>
              `;
            }).join('');
          }
        }
      } catch (err) {
        console.error('Failed to load invoices data:', err);
        if (loading) loading.style.display = 'none';
      }
    }

    setInvoiceActionButtonsVisible(visible) {
      const displayVal = visible ? 'inline-flex' : 'none';
      ['fin-invoice-btn-draft', 'fin-invoice-btn-issue', 'fin-invoice-btn-save-print'].forEach(id => {
        const btn = document.getElementById(id);
        if (btn) btn.style.display = displayVal;
      });
    }

    resetProjectInvoiceModalState() {
      this.invEditingInvoiceId = null;
      this.invSelectedProjectId = null;
      this.invSelectedProjectObj = null;
      this.invSelectedCompanyId = null;
      this.invProjectItems = [];

      const form = document.getElementById('fin-project-invoice-form');
      if (form) form.reset();

      const editIdEl = document.getElementById('fin-invoice-edit-id');
      const projIdEl = document.getElementById('fin-invoice-project-id');
      const compIdEl = document.getElementById('fin-invoice-company-id');
      if (editIdEl) editIdEl.value = '';
      if (projIdEl) projIdEl.value = '';
      if (compIdEl) compIdEl.value = '';

      const titleEl = document.getElementById('fin-modal-title-text');
      if (titleEl) titleEl.textContent = 'إنشاء فاتورة مشروع جديدة — منظومة الفواتير';

      const triggerBtn = document.getElementById('fin-project-combo-trigger');
      const comboLabel = document.getElementById('fin-project-combo-label');
      const comboSearch = document.getElementById('fin-project-combo-search');
      if (triggerBtn) {
        triggerBtn.disabled = false;
        triggerBtn.style.background = '#FFFFFF';
        triggerBtn.style.cursor = 'pointer';
      }
      if (comboLabel) {
        comboLabel.textContent = '-- اختر كود المشروع أو ابحث بالكود / الاسم --';
        comboLabel.style.color = '#64748B';
      }
      if (comboSearch) comboSearch.value = '';

      this.toggleProjectCombo(false);

      const loadingEl = document.getElementById('fin-invoice-project-loading');
      const emptyEl = document.getElementById('fin-invoice-empty-state');
      const bodyEl = document.getElementById('fin-invoice-project-form-body');
      const tbodyEl = document.getElementById('fin-invoice-items-tbody');

      if (loadingEl) loadingEl.style.display = 'none';
      if (emptyEl) emptyEl.style.display = 'none';
      if (bodyEl) bodyEl.style.display = 'none';
      if (tbodyEl) tbodyEl.innerHTML = '';

      this.setInvoiceActionButtonsVisible(false);
    }

    async openCreateInvoiceModal() {
      this.resetProjectInvoiceModalState();

      const modalBackdrop = document.getElementById('finance-invoice-modal-backdrop');
      if (modalBackdrop) modalBackdrop.style.display = 'flex';

      const listEl = document.getElementById('fin-project-combo-list');
      if (listEl) {
        listEl.innerHTML = '<div style="padding: 14px; text-align: center; color: #64748B; font-size: 0.85rem;">جاري تحميل أكواد المشاريع...</div>';
      }

      try {
        const res = await window.ChatApi.getInvoiceProjectCodes();
        if (res && res.success && Array.isArray(res.data)) {
          this.invCachedProjects = res.data;
        } else {
          this.invCachedProjects = [];
        }
        this.renderProjectComboOptions('');
      } catch (err) {
        console.error('Failed to load project codes for invoice modal:', err);
        this.invCachedProjects = [];
        if (listEl) {
          listEl.innerHTML = '<div style="padding: 14px; text-align: center; color: #DC2626; font-size: 0.85rem;">تعذر تحميل أكواد المشاريع من الخادم.</div>';
        }
      }
    }

    async editInvoice(id) {
      this.resetProjectInvoiceModalState();
      this.invEditingInvoiceId = id;

      const modalBackdrop = document.getElementById('finance-invoice-modal-backdrop');
      const loadingEl = document.getElementById('fin-invoice-project-loading');
      if (modalBackdrop) modalBackdrop.style.display = 'flex';
      if (loadingEl) loadingEl.style.display = 'block';

      try {
        const res = await window.ChatApi.getInvoiceById(id);
        if (loadingEl) loadingEl.style.display = 'none';

        if (!res || !res.success || !res.data) {
          alert(res?.message || 'تعذر تحميل بيانات الفاتورة للتعديل.');
          this.closeCreateInvoiceModal();
          return;
        }

        const inv = res.data;
        if (inv.status !== 'DRAFT') {
          alert('لا يمكن تعديل إلا الفواتير التي في حالة مسودة.');
          this.closeCreateInvoiceModal();
          return;
        }

        const titleEl = document.getElementById('fin-modal-title-text');
        if (titleEl) titleEl.textContent = `تعديل مسودة الفاتورة: ${inv.invoiceNumber}`;

        const editIdEl = document.getElementById('fin-invoice-edit-id');
        const projIdEl = document.getElementById('fin-invoice-project-id');
        const compIdEl = document.getElementById('fin-invoice-company-id');
        if (editIdEl) editIdEl.value = String(inv.id);
        if (projIdEl) projIdEl.value = inv.projectCodeId ? String(inv.projectCodeId) : '';
        if (compIdEl) compIdEl.value = inv.companyId ? String(inv.companyId) : '';

        this.invSelectedProjectId = inv.projectCodeId || null;
        this.invSelectedCompanyId = inv.companyId || null;

        const triggerBtn = document.getElementById('fin-project-combo-trigger');
        const comboLabel = document.getElementById('fin-project-combo-label');
        if (triggerBtn) {
          triggerBtn.disabled = true;
          triggerBtn.style.background = '#F1F5F9';
          triggerBtn.style.cursor = 'not-allowed';
        }
        if (comboLabel) {
          comboLabel.textContent = `${inv.projectCodeValue || '-'} — ${inv.projectName || 'مشروع'}`;
          comboLabel.style.color = '#0F172A';
        }

        const codeEl = document.getElementById('fin-disp-proj-code');
        const nameEl = document.getElementById('fin-disp-proj-name');
        const clientEl = document.getElementById('fin-disp-proj-client');
        const summaryEl = document.getElementById('fin-disp-proj-summary');

        if (codeEl) codeEl.textContent = inv.projectCodeValue || '-';
        if (nameEl) nameEl.textContent = inv.projectName || '-';
        if (clientEl) clientEl.textContent = inv.companyName || '-';

        const compNameInp = document.getElementById('fin-company-name-input');
        const compPhoneInp = document.getElementById('fin-company-phone-input');
        const compTaxInp = document.getElementById('fin-company-tax-input');
        const compAddrInp = document.getElementById('fin-company-address-input');
        if (compNameInp) compNameInp.value = inv.companyName || '';
        if (compPhoneInp) compPhoneInp.value = inv.companyPhone || '';
        if (compTaxInp) compTaxInp.value = inv.companyTaxNumber || '';
        if (compAddrInp) compAddrInp.value = inv.companyAddress || '';

        const shippingInp = document.getElementById('fin-invoice-shipping-input');
        const taxInp = document.getElementById('fin-invoice-tax-rate');
        const discInp = document.getElementById('fin-invoice-discount');
        const notesInp = document.getElementById('fin-invoice-notes-input');
        if (shippingInp) shippingInp.value = (inv.shippingPrice ?? 0).toFixed(2);
        if (taxInp) taxInp.value = inv.taxRate ?? 14;
        if (discInp) discInp.value = (inv.discountAmount ?? 0).toFixed(2);
        if (notesInp) notesInp.value = inv.notes || '';

        this.invProjectItems = (inv.items || []).map(it => ({
          transportationId: it.transportationId,
          transactionNumber: it.transportationTransactionNumber,
          warehouseExportNumber: it.warehouseExportNumber || '-',
          productName: it.productName || 'مواد',
          transactionDateFormatted: it.transactionDateFormatted || '-',
          remainingInvoicableQuantity: it.quantity,
          unit: it.unit || 'طن',
          unitPrice: it.unitPrice
        }));

        const totalQty = this.invProjectItems.reduce((acc, x) => acc + (parseFloat(x.remainingInvoicableQuantity) || 0), 0);
        if (summaryEl) {
          summaryEl.textContent = `${this.invProjectItems.length} بنود — ${this.formatWeight(totalQty)} طن`;
        }

        this.renderProjectInvoiceItems();
        const bodyEl = document.getElementById('fin-invoice-project-form-body');
        if (bodyEl) bodyEl.style.display = 'flex';
        this.setInvoiceActionButtonsVisible(true);
      } catch (err) {
        console.error('Failed to open invoice for edit:', err);
        if (loadingEl) loadingEl.style.display = 'none';
        alert('حدث خطأ أثناء تحميل بيانات المسودة.');
        this.closeCreateInvoiceModal();
      }
    }

    closeCreateInvoiceModal() {
      const modalBackdrop = document.getElementById('finance-invoice-modal-backdrop');
      if (modalBackdrop) modalBackdrop.style.display = 'none';
      this.toggleProjectCombo(false);
    }

    toggleProjectCombo(forceOpen) {
      const panel = document.getElementById('fin-project-combo-panel');
      if (!panel) return;
      const shouldOpen = typeof forceOpen === 'boolean' ? forceOpen : (panel.style.display === 'none' || !panel.style.display);
      panel.style.display = shouldOpen ? 'block' : 'none';
      if (shouldOpen) {
        const searchEl = document.getElementById('fin-project-combo-search');
        if (searchEl) {
          searchEl.focus();
          this.renderProjectComboOptions(searchEl.value || '');
        }
      }
    }

    renderProjectComboOptions(searchQuery) {
      const listEl = document.getElementById('fin-project-combo-list');
      if (!listEl) return;

      const q = (searchQuery || '').trim().toLowerCase();
      const projects = (this.invCachedProjects || []).filter(p => {
        if (!q) return true;
        return (p.code || '').toLowerCase().includes(q)
          || (p.projectName || '').toLowerCase().includes(q)
          || (p.clientName || '').toLowerCase().includes(q);
      });

      if (projects.length === 0) {
        listEl.innerHTML = '<div style="padding: 12px; text-align: center; color: #64748B; font-size: 0.85rem;">لا توجد أكواد مشاريع مطابقة لنتيجة البحث.</div>';
        return;
      }

      listEl.innerHTML = projects.map(p => {
        const isSelected = this.invSelectedProjectId === p.id;
        return `
          <button type="button" class="fin-project-combo-option" data-id="${p.id}" style="width: 100%; text-align: right; background: ${isSelected ? '#EFF6FF' : 'transparent'}; border: none; border-bottom: 1px solid #F1F5F9; padding: 9px 12px; cursor: pointer; display: flex; justify-content: space-between; align-items: center; gap: 10px; border-radius: 6px;">
            <div>
              <span dir="ltr" style="font-family: monospace; font-weight: 800; color: #0284C7; background: #E0F2FE; padding: 2px 8px; border-radius: 5px; font-size: 0.85rem;">${this.escapeHtml(p.code)}</span>
              <span style="font-weight: 700; color: #0F172A; margin-right: 6px;">${this.escapeHtml(p.projectName || '')}</span>
            </div>
            <span style="font-size: 0.78rem; color: #64748B;">${this.escapeHtml(p.clientName || '')}</span>
          </button>
        `;
      }).join('');

      listEl.querySelectorAll('.fin-project-combo-option').forEach(btn => {
        btn.addEventListener('click', (e) => {
          e.stopPropagation();
          const pid = parseInt(btn.getAttribute('data-id'), 10);
          if (pid) {
            this.selectProjectCodeForInvoice(pid);
          }
        });
      });
    }

    async selectProjectCodeForInvoice(projectCodeId) {
      const prj = (this.invCachedProjects || []).find(p => p.id === projectCodeId);
      this.invSelectedProjectId = projectCodeId;
      this.invSelectedProjectObj = prj || null;
      this.toggleProjectCombo(false);

      const projIdEl = document.getElementById('fin-invoice-project-id');
      const comboLabel = document.getElementById('fin-project-combo-label');
      if (projIdEl) projIdEl.value = String(projectCodeId);
      if (comboLabel && prj) {
        comboLabel.textContent = `${prj.code} — ${prj.projectName}`;
        comboLabel.style.color = '#0F172A';
      }

      const loadingEl = document.getElementById('fin-invoice-project-loading');
      const emptyEl = document.getElementById('fin-invoice-empty-state');
      const emptyMsgEl = document.getElementById('fin-invoice-empty-message');
      const bodyEl = document.getElementById('fin-invoice-project-form-body');

      if (loadingEl) loadingEl.style.display = 'block';
      if (emptyEl) emptyEl.style.display = 'none';
      if (bodyEl) bodyEl.style.display = 'none';
      this.setInvoiceActionButtonsVisible(false);

      try {
        const res = await window.ChatApi.getProjectInvoiceData(projectCodeId);
        if (loadingEl) loadingEl.style.display = 'none';

        if (!res || !res.success || !res.data) {
          if (emptyMsgEl) emptyMsgEl.textContent = res?.message || 'تعذر استرجاع بيانات المشروع من قاعدة البيانات.';
          if (emptyEl) emptyEl.style.display = 'block';
          return;
        }

        const data = res.data;
        const items = Array.isArray(data.eligibleTransportations) ? data.eligibleTransportations : [];

        if (!data.hasEligibleRecords || items.length === 0) {
          if (emptyMsgEl) {
            emptyMsgEl.textContent = data.message || `لا توجد معاملات نقل مؤهلة أو متبقية للفوترة لكود المشروع (${data.code}) حالياً.`;
          }
          if (emptyEl) emptyEl.style.display = 'block';
          return;
        }

        this.invSelectedCompanyId = data.companyId || null;
        const compIdEl = document.getElementById('fin-invoice-company-id');
        if (compIdEl) compIdEl.value = data.companyId ? String(data.companyId) : '';

        const codeEl = document.getElementById('fin-disp-proj-code');
        const nameEl = document.getElementById('fin-disp-proj-name');
        const clientEl = document.getElementById('fin-disp-proj-client');
        const summaryEl = document.getElementById('fin-disp-proj-summary');

        const totalQty = items.reduce((sum, x) => sum + (parseFloat(x.remainingInvoicableQuantity) || 0), 0);
        if (codeEl) codeEl.textContent = data.code || '-';
        if (nameEl) nameEl.textContent = data.projectName || '-';
        if (clientEl) clientEl.textContent = data.clientName || data.companyName || '-';
        if (summaryEl) summaryEl.textContent = `${items.length} شحنات — ${this.formatWeight(totalQty)} طن`;

        const compNameInp = document.getElementById('fin-company-name-input');
        const compPhoneInp = document.getElementById('fin-company-phone-input');
        const compTaxInp = document.getElementById('fin-company-tax-input');
        const compAddrInp = document.getElementById('fin-company-address-input');
        if (compNameInp) compNameInp.value = data.companyName || data.clientName || '';
        if (compPhoneInp) compPhoneInp.value = data.companyPhone || '';
        if (compTaxInp) compTaxInp.value = '';
        if (compAddrInp) compAddrInp.value = data.companyAddress || '';

        const shippingInp = document.getElementById('fin-invoice-shipping-input');
        const taxInp = document.getElementById('fin-invoice-tax-rate');
        const discInp = document.getElementById('fin-invoice-discount');
        const notesInp = document.getElementById('fin-invoice-notes-input');
        const uniformInp = document.getElementById('fin-uniform-price-input');
        if (shippingInp) shippingInp.value = '0.00';
        if (taxInp) taxInp.value = '14';
        if (discInp) discInp.value = '0';
        if (notesInp) notesInp.value = '';
        if (uniformInp) uniformInp.value = '';

        this.invProjectItems = items.map(it => ({ ...it, unitPrice: 0 }));
        this.renderProjectInvoiceItems();

        if (bodyEl) bodyEl.style.display = 'flex';
        this.setInvoiceActionButtonsVisible(true);
      } catch (err) {
        console.error('Failed to retrieve project invoice data:', err);
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyMsgEl) emptyMsgEl.textContent = 'حدث خطأ أثناء استرجاع بيانات المشروع من الخادم.';
        if (emptyEl) emptyEl.style.display = 'block';
      }
    }

    renderProjectInvoiceItems() {
      const tbody = document.getElementById('fin-invoice-items-tbody');
      if (!tbody) return;

      const items = this.invProjectItems || [];
      tbody.innerHTML = items.map(it => {
        const priceVal = it.unitPrice > 0 ? Number(it.unitPrice).toFixed(2) : '';
        return `
          <tr data-trans-id="${it.transportationId}" data-qty="${it.remainingInvoicableQuantity}" data-product="${this.escapeHtml(it.productName || '')}">
            <td><strong dir="ltr" style="font-family: monospace; color: #0284C7;">${it.transactionNumber ? '#' + it.transactionNumber : '-'}</strong></td>
            <td><strong style="color: #059669;">${this.escapeHtml(it.warehouseExportNumber || '-')}</strong></td>
            <td><strong>${this.escapeHtml(it.productName || 'مواد')}</strong></td>
            <td>${this.escapeHtml(it.transactionDateFormatted || '-')}</td>
            <td><strong dir="ltr">${this.formatWeight(it.remainingInvoicableQuantity)} ${this.escapeHtml(it.unit || 'طن')}</strong></td>
            <td>
              <input type="number" class="finance-input fin-item-price-input" data-trans-id="${it.transportationId}" value="${priceVal}" min="0.01" step="0.01" placeholder="أدخل السعر..." style="width: 135px; font-weight: 700; text-align: left;" />
            </td>
            <td><strong dir="ltr" class="fin-row-line-total" style="color: #0284C7;">0.00 ج.م</strong></td>
          </tr>
        `;
      }).join('');

      tbody.querySelectorAll('.fin-item-price-input').forEach(inp => {
        inp.addEventListener('input', () => this.recalculateProjectInvoiceTotals());
      });

      this.recalculateProjectInvoiceTotals();
    }

    recalculateProjectInvoiceTotals() {
      const rows = document.querySelectorAll('#fin-invoice-items-tbody tr');
      let subtotal = 0;

      rows.forEach(r => {
        const qty = parseFloat(r.getAttribute('data-qty')) || 0;
        const priceInp = r.querySelector('.fin-item-price-input');
        const price = parseFloat(priceInp?.value) || 0;
        const lineTotal = Math.round(qty * price * 1000) / 1000;
        subtotal += lineTotal;

        const totalCell = r.querySelector('.fin-row-line-total');
        if (totalCell) totalCell.textContent = `${this.formatCurrency(lineTotal)}`;
      });

      const shipping = Math.max(0, parseFloat(document.getElementById('fin-invoice-shipping-input')?.value) || 0);
      const taxRate = Math.max(0, parseFloat(document.getElementById('fin-invoice-tax-rate')?.value) || 0);
      const discount = Math.max(0, parseFloat(document.getElementById('fin-invoice-discount')?.value) || 0);

      const taxAmount = Math.round(subtotal * (taxRate / 100) * 1000) / 1000;
      const totalAmount = Math.max(0, subtotal + shipping + taxAmount - discount);

      const subtotalEl = document.getElementById('fin-invoice-subtotal-disp');
      const taxEl = document.getElementById('fin-invoice-tax-disp');
      const totalEl = document.getElementById('fin-invoice-total-disp');

      if (subtotalEl) subtotalEl.textContent = `${this.formatCurrency(subtotal)}`;
      if (taxEl) taxEl.textContent = `${this.formatCurrency(taxAmount)}`;
      if (totalEl) totalEl.textContent = `${this.formatCurrency(totalAmount)}`;
    }

    async saveProjectInvoice(isIssued, openPrintAfter) {
      if (!this.invSelectedProjectId) {
        alert('يرجى اختيار كود المشروع أولاً.');
        return;
      }

      const rows = document.querySelectorAll('#fin-invoice-items-tbody tr');
      if (!rows || rows.length === 0) {
        alert('لا توجد بنود مؤهلة للفوترة لهذا المشروع.');
        return;
      }

      const itemsPayload = [];
      let hasInvalidPrice = false;

      rows.forEach(r => {
        const transId = parseInt(r.getAttribute('data-trans-id'), 10);
        const qty = parseFloat(r.getAttribute('data-qty')) || 0;
        const productName = r.getAttribute('data-product') || '';
        const price = parseFloat(r.querySelector('.fin-item-price-input')?.value);

        if (isNaN(price) || price <= 0) {
          hasInvalidPrice = true;
        }

        itemsPayload.push({
          transportationId: isNaN(transId) ? null : transId,
          productName: productName,
          quantity: qty,
          unitPrice: isNaN(price) ? 0 : price
        });
      });

      if (hasInvalidPrice) {
        alert('يرجى إدخال سعر وحدة صحيح (أكبر من الصفر) لجميع البنود.');
        return;
      }

      const companyName = document.getElementById('fin-company-name-input')?.value?.trim() || '';
      if (!companyName) {
        alert('يرجى إدخال اسم الشركة / العميل.');
        return;
      }

      const payload = {
        id: this.invEditingInvoiceId || 0,
        projectCodeId: this.invSelectedProjectId,
        relatedEntityType: 'COMPANY',
        companyId: this.invSelectedCompanyId || null,
        companyName: companyName,
        companyPhone: document.getElementById('fin-company-phone-input')?.value?.trim() || null,
        companyTaxNumber: document.getElementById('fin-company-tax-input')?.value?.trim() || null,
        companyAddress: document.getElementById('fin-company-address-input')?.value?.trim() || null,
        shippingPrice: Math.max(0, parseFloat(document.getElementById('fin-invoice-shipping-input')?.value) || 0),
        taxRate: Math.max(0, parseFloat(document.getElementById('fin-invoice-tax-rate')?.value) || 0),
        discountAmount: Math.max(0, parseFloat(document.getElementById('fin-invoice-discount')?.value) || 0),
        status: isIssued ? 'ISSUED' : 'DRAFT',
        notes: document.getElementById('fin-invoice-notes-input')?.value?.trim() || null,
        items: itemsPayload
      };

      try {
        const res = this.invEditingInvoiceId
          ? await window.ChatApi.updateInvoice(this.invEditingInvoiceId, payload)
          : await window.ChatApi.createInvoice(payload);

        if (res && res.success) {
          const msg = this.invEditingInvoiceId
            ? (isIssued ? 'تم تحديث المسودة وإصدار الفاتورة بنجاح!' : 'تم تحديث مسودة الفاتورة بنجاح!')
            : (isIssued ? 'تم إنشاء وإصدار الفاتورة بنجاح!' : 'تم حفظ مسودة الفاتورة بنجاح!');
          alert(msg);

          this.closeCreateInvoiceModal();
          await this.loadInvoiceStatistics();
          await this.loadInvoicesData();

          const savedId = res.data?.id || this.invEditingInvoiceId;
          if ((openPrintAfter || isIssued) && savedId) {
            this.printInvoice(savedId);
          }
        } else {
          alert(res?.message || 'تعذر حفظ الفاتورة.');
        }
      } catch (err) {
        console.error('Failed to save project invoice:', err);
        alert('حدث خطأ أثناء الاتصال بالخادم لحفظ الفاتورة.');
      }
    }

    async viewInvoice(id) {
      this.currentViewingInvoiceId = id;
      const modalBackdrop = document.getElementById('finance-view-invoice-modal-backdrop');
      const content = document.getElementById('fin-view-modal-content');
      const numEl = document.getElementById('fin-view-inv-number');
      const statusBadge = document.getElementById('fin-view-modal-status-badge');

      if (modalBackdrop) modalBackdrop.style.display = 'flex';
      if (content) content.innerHTML = '<div style="padding: 2rem; text-align: center;"><div class="spinner"></div></div>';

      try {
        const res = await window.ChatApi.getInvoiceById(id);
        if (res && res.success && res.data) {
          const inv = res.data;
          if (numEl) numEl.textContent = inv.invoiceNumber;
          if (statusBadge) {
            const badgeClass = inv.status === 'ISSUED' ? 'badge-positive' : (inv.status === 'DRAFT' ? 'badge-neutral' : 'badge-negative');
            statusBadge.innerHTML = `<span class="finance-status-badge ${badgeClass}" style="font-size: 0.9rem; padding: 6px 16px;">${this.escapeHtml(inv.statusArabic)}</span>`;
          }

          if (content) {
            content.innerHTML = `
              <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; background: #F8FAFC; padding: 1rem; border-radius: 10px; margin-bottom: 1.25rem; border: 1px solid #E2E8F0;">
                <div>
                  <span style="font-size: 0.8rem; color: #64748B;">تاريخ الإصدار:</span>
                  <div><strong dir="ltr">${this.escapeHtml(inv.invoiceDateFormatted)}</strong></div>
                </div>
                <div>
                  <span style="font-size: 0.8rem; color: #64748B;">كود المشروع:</span>
                  <div><strong style="color: #1E3A8A;">${this.escapeHtml(inv.projectCodeValue || '-')}</strong> ${inv.projectName ? `- ${this.escapeHtml(inv.projectName)}` : ''}</div>
                </div>
                <div>
                  <span style="font-size: 0.8rem; color: #64748B;">الجهة / المورد:</span>
                  <div><strong>${this.escapeHtml(inv.poNumber ? `أمر شراء: ${inv.poNumber} (${inv.supplierName || inv.companyName || 'مورد'})` : (inv.companyName || '-'))}</strong></div>
                </div>
                <div>
                  <span style="font-size: 0.8rem; color: #64748B;">المسؤول:</span>
                  <div>${this.escapeHtml(inv.createdBy || '-')}</div>
                </div>
              </div>

              ${inv.priceChangedBy ? `
                <div style="background: #FFFBEB; border: 1px solid #FDE68A; border-radius: 8px; padding: 10px 14px; margin-bottom: 1.25rem; font-size: 0.85rem; color: #92400E;">
                  <strong>تعديل سعر الوحدة:</strong> تم تعديل سعر الوحدة بهذه الفاتورة عن سعر أمر الشراء الأصلي بواسطة: <strong>${this.escapeHtml(inv.priceChangedBy)}</strong> ${inv.priceChangedAt ? `بتاريخ ${new Date(inv.priceChangedAt).toLocaleDateString('ar-EG')}` : ''}
                </div>
              ` : ''}

              <h4 style="margin: 0 0 0.75rem 0; color: #0F172A;">بنود الفاتورة والتسعير المعتمد:</h4>
              <div class="finance-table-wrapper" style="overflow-x: auto;">
                <table class="finance-table">
                  <thead>
                    <tr>
                      <th>معاملة النقل / المرجع</th>
                      <th>إذن الصرف</th>
                      <th>الصنف / المادة</th>
                      <th>التشغيلة / المرجع</th>
                      <th>الكمية</th>
                      <th>سعر الوحدة</th>
                      <th>إجمالي البند</th>
                    </tr>
                  </thead>
                  <tbody>
                    ${(inv.items && inv.items.length > 0) ? inv.items.map(it => `
                      <tr>
                        <td><strong dir="ltr" style="font-family: monospace; color: #0284C7;">${it.transportationTransactionNumber ? '#' + it.transportationTransactionNumber : (inv.poNumber ? `أمر شراء ${inv.poNumber}` : '-')}</strong></td>
                        <td><strong style="color: #059669;">${this.escapeHtml(it.warehouseExportNumber || '-')}</strong></td>
                        <td>${this.escapeHtml(it.productName || inv.materialName || 'مواد')}</td>
                        <td><span style="font-size: 0.8rem; color: #64748B;">${this.escapeHtml(it.batchNumber || it.referenceNumber || (inv.poNumber ? inv.poNumber : '-'))}</span></td>
                        <td><strong dir="ltr">${this.formatWeight(it.quantity)} ${this.escapeHtml(it.unit || 'طن')}</strong></td>
                        <td dir="ltr">${this.formatCurrency(it.unitPrice)}</td>
                        <td><strong dir="ltr" style="color: #0284C7;">${this.formatCurrency(it.lineTotal)}</strong></td>
                      </tr>
                    `).join('') : `
                      <tr>
                        <td><strong dir="ltr" style="font-family: monospace; color: #0284C7;">${inv.poNumber ? `أمر شراء ${inv.poNumber}` : '-'}</strong></td>
                        <td>-</td>
                        <td>${this.escapeHtml(inv.materialName || 'مواد')}</td>
                        <td>${this.escapeHtml(inv.poNumber || '-')}</td>
                        <td><strong dir="ltr">${this.formatWeight(inv.invoicedQuantity || 0)} طن</strong></td>
                        <td dir="ltr">${this.formatCurrency(inv.invoiceUnitPrice || 0)}</td>
                        <td><strong dir="ltr" style="color: #0284C7;">${this.formatCurrency(inv.subtotal)}</strong></td>
                      </tr>
                    `}
                  </tbody>
                </table>
              </div>

              <div style="margin-top: 1.5rem; display: flex; justify-content: flex-end;">
                <div style="width: 100%; max-width: 400px; background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 10px; padding: 1rem;">
                  <div style="display: flex; justify-content: space-between; padding: 4px 0;">
                    <span>المجموع الفرعي:</span>
                    <strong dir="ltr">${this.formatCurrency(inv.subtotal)}</strong>
                  </div>
                  ${inv.shippingPrice > 0 ? `
                    <div style="display: flex; justify-content: space-between; padding: 4px 0;">
                      <span>تكلفة الشحن (Shipping Price):</span>
                      <strong dir="ltr">${this.formatCurrency(inv.shippingPrice)}</strong>
                    </div>
                  ` : ''}
                  <div style="display: flex; justify-content: space-between; padding: 4px 0;">
                    <span>ضريبة القيمة المضافة (${inv.taxRate}%):</span>
                    <strong dir="ltr">${this.formatCurrency(inv.taxAmount)}</strong>
                  </div>
                  ${inv.discountAmount > 0 ? `
                    <div style="display: flex; justify-content: space-between; padding: 4px 0; color: #DC2626;">
                      <span>قيمة الخصم:</span>
                      <strong dir="ltr">-${this.formatCurrency(inv.discountAmount)}</strong>
                    </div>
                  ` : ''}
                  <div style="display: flex; justify-content: space-between; padding: 8px 0 0 0; border-top: 2px solid #0F172A; font-size: 1.15rem; font-weight: 800; color: #0284C7;">
                    <span>الإجمالي النهائي:</span>
                    <strong dir="ltr">${this.formatCurrency(inv.totalAmount)}</strong>
                  </div>
                </div>
              </div>

              ${inv.notes ? `
                <div style="margin-top: 1rem; background: #FFFBEB; border: 1px solid #FDE68A; padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.85rem;">
                  <strong>ملاحظات:</strong> ${this.escapeHtml(inv.notes)}
                </div>
              ` : ''}
            `;
          }
        }
      } catch (err) {
        console.error('Failed to view invoice:', err);
      }
    }

    closeViewInvoiceModal() {
      const modalBackdrop = document.getElementById('finance-view-invoice-modal-backdrop');
      if (modalBackdrop) modalBackdrop.style.display = 'none';
    }

    async printInvoice(id) {
      this.currentPrintingInvoiceId = id;
      const modalBackdrop = document.getElementById('finance-print-invoice-modal-backdrop');
      const container = document.getElementById('invoice-printable-container');

      if (modalBackdrop) modalBackdrop.style.display = 'flex';
      if (container) container.innerHTML = '<div style="padding: 3rem; text-align: center;"><div class="spinner"></div><p style="margin-top: 1rem;">جاري إعداد قالب الطباعة الرسمي...</p></div>';

      try {
        const res = await window.ChatApi.getInvoiceById(id);
        if (res && res.success && res.data) {
          const inv = res.data;
          if (container) {
            container.innerHTML = `
              <!-- Print Header -->
              <div class="inv-print-header">
                <div class="inv-print-logo-area">
                  <div class="inv-print-brand">فيس ستون للحلول الخرسانية المتكاملة</div>
                  <div class="inv-print-brand-sub">FaceStone for Full Concrete Solutions S.A.E</div>
                  <div style="font-size: 0.8rem; color: #64748B; margin-top: 4px;">السجل التجاري: 184920 | البطاقة الضريبية: 492-381-902</div>
                </div>
                <div class="inv-print-meta">
                  <div style="font-size: 1.1rem; font-weight: 800; color: #0F172A;">فاتورة توريد رسمية (INVOICE)</div>
                  <div class="inv-print-number" dir="ltr">${this.escapeHtml(inv.invoiceNumber)}</div>
                  <div style="font-size: 0.85rem; color: #475569; margin-top: 4px;">التاريخ: <strong dir="ltr">${this.escapeHtml(inv.invoiceDateFormatted)}</strong></div>
                </div>
              </div>

              <!-- Customer & Project Info Grid -->
              <div class="inv-print-info-grid">
                <div class="inv-print-info-block">
                  <h5>بيانات المورد / العميل / الجهة الطالبة:</h5>
                  <p><strong>${this.escapeHtml(inv.supplierName || inv.companyName || 'عميل فيس ستون')}</strong></p>
                  ${inv.poNumber ? `<div style="font-size: 0.9rem; color: #0284C7; font-weight: 700; margin-top: 4px;">مرجع أمر الشراء: ${this.escapeHtml(inv.poNumber)}</div>` : ''}
                  ${inv.companyAddress ? `<div style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(inv.companyAddress)}</div>` : ''}
                  ${inv.companyTaxNumber ? `<div style="font-size: 0.8rem; color: #64748B;">البطاقة الضريبية: ${this.escapeHtml(inv.companyTaxNumber)}</div>` : ''}
                  ${inv.companyPhone ? `<div style="font-size: 0.8rem; color: #64748B;">الهاتف: ${this.escapeHtml(inv.companyPhone)}</div>` : ''}
                </div>
                <div class="inv-print-info-block">
                  <h5>بيانات التوريد والمشروع:</h5>
                  ${inv.projectCodeValue ? `<p>كود المشروع: <span dir="ltr"><strong>${this.escapeHtml(inv.projectCodeValue)}</strong></span></p>` : ''}
                  ${inv.projectName ? `<div style="font-size: 0.85rem; color: #475569;">${this.escapeHtml(inv.projectName)}</div>` : ''}
                  ${inv.materialName ? `<div style="font-size: 0.85rem; color: #0F172A; margin-top: 4px;">بيان المادة: <strong>${this.escapeHtml(inv.materialName)}</strong></div>` : ''}
                  ${inv.orderedQuantity ? `<div style="font-size: 0.85rem; color: #64748B;">الكمية المتعاقد عليها بأمر الشراء: ${this.formatWeight(inv.orderedQuantity)} طن</div>` : ''}
                  ${inv.priceChangedBy ? `<div style="font-size: 0.8rem; color: #B45309; margin-top: 4px;">تم اعتماد سعر خاص للفاتورة بواسطة: ${this.escapeHtml(inv.priceChangedBy)}</div>` : ''}
                </div>
              </div>

              <!-- Itemized Table -->
              <table class="inv-print-table">
                <thead>
                  <tr>
                    <th style="width: 35px; text-align: center;">م</th>
                    <th>رقم شحنة النقل / أمر الشراء</th>
                    <th>إذن صرف المستودع</th>
                    <th>بيان الصنف والمادة</th>
                    <th>التشغيلة / المرجع</th>
                    <th style="text-align: center;">الكمية</th>
                    <th style="text-align: center;">الوحدة</th>
                    <th style="text-align: left;">سعر الوحدة</th>
                    <th style="text-align: left;">إجمالي البند</th>
                  </tr>
                </thead>
                <tbody>
                  ${(inv.items && inv.items.length > 0) ? inv.items.map((it, idx) => `
                    <tr>
                      <td style="text-align: center; font-weight: 700;">${idx + 1}</td>
                      <td><strong dir="ltr" style="font-family: monospace;">${it.transportationTransactionNumber ? '#' + it.transportationTransactionNumber : (inv.poNumber ? inv.poNumber : '-')}</strong></td>
                      <td><strong>${this.escapeHtml(it.warehouseExportNumber || '-')}</strong></td>
                      <td><strong>${this.escapeHtml(it.productName || inv.materialName || 'مواد')}</strong></td>
                      <td><span style="font-size: 0.8rem; color: #64748B;">${this.escapeHtml(it.batchNumber || it.referenceNumber || (inv.poNumber ? inv.poNumber : '-'))}</span></td>
                      <td style="text-align: center;"><strong dir="ltr">${this.formatWeight(it.quantity)}</strong></td>
                      <td style="text-align: center;">${this.escapeHtml(it.unit || 'طن')}</td>
                      <td style="text-align: left;" dir="ltr">${this.formatCurrency(it.unitPrice)}</td>
                      <td style="text-align: left;"><strong dir="ltr">${this.formatCurrency(it.lineTotal)}</strong></td>
                    </tr>
                  `).join('') : `
                    <tr>
                      <td style="text-align: center; font-weight: 700;">1</td>
                      <td><strong dir="ltr" style="font-family: monospace;">${inv.poNumber || '-'}</strong></td>
                      <td>-</td>
                      <td><strong>${this.escapeHtml(inv.materialName || 'مواد خرسانية')}</strong></td>
                      <td><span style="font-size: 0.8rem; color: #64748B;">${inv.poNumber || '-'}</span></td>
                      <td style="text-align: center;"><strong dir="ltr">${this.formatWeight(inv.invoicedQuantity || 0)}</strong></td>
                      <td style="text-align: center;">طن</td>
                      <td style="text-align: left;" dir="ltr">${this.formatCurrency(inv.invoiceUnitPrice || 0)}</td>
                      <td style="text-align: left;"><strong dir="ltr">${this.formatCurrency(inv.subtotal)}</strong></td>
                    </tr>
                  `}
                </tbody>
              </table>

              <!-- Totals Box -->
              <div class="inv-print-totals-box">
                <table class="inv-print-totals-table">
                  <tr>
                    <td style="color: #64748B;">المجموع الفرعي (Subtotal):</td>
                    <td style="text-align: left;" dir="ltr"><strong>${this.formatCurrency(inv.subtotal)}</strong></td>
                  </tr>
                  ${inv.shippingPrice > 0 ? `
                    <tr>
                      <td style="color: #64748B;">تكلفة الشحن (Shipping Price):</td>
                      <td style="text-align: left;" dir="ltr"><strong>${this.formatCurrency(inv.shippingPrice)}</strong></td>
                    </tr>
                  ` : ''}
                  <tr>
                    <td style="color: #64748B;">ضريبة القيمة المضافة (${inv.taxRate}%):</td>
                    <td style="text-align: left;" dir="ltr"><strong>${this.formatCurrency(inv.taxAmount)}</strong></td>
                  </tr>
                  ${inv.discountAmount > 0 ? `
                    <tr>
                      <td style="color: #DC2626;">الخصم الممنوح:</td>
                      <td style="text-align: left; color: #DC2626;" dir="ltr"><strong>-${this.formatCurrency(inv.discountAmount)}</strong></td>
                    </tr>
                  ` : ''}
                  <tr class="total-row">
                    <td>الإجمالي المستحق:</td>
                    <td style="text-align: left;" dir="ltr">${this.formatCurrency(inv.totalAmount)}</td>
                  </tr>
                </table>
              </div>

              ${inv.notes ? `
                <div style="border-top: 1px dashed #CBD5E1; padding-top: 8px; font-size: 0.85rem; color: #475569;">
                  <strong>ملاحظات:</strong> ${this.escapeHtml(inv.notes)}
                </div>
              ` : ''}

              <!-- Official Signatures -->
              <div class="inv-print-signatures">
                <div>
                  <div class="inv-print-sig-title">إعداد ومراجعة الحسابات</div>
                  <div class="inv-print-sig-line">${this.escapeHtml(inv.createdBy || 'المدير المالي')}</div>
                </div>
                <div>
                  <div class="inv-print-sig-title">اعتماد الإدارة العامة</div>
                  <div class="inv-print-sig-line">الرئيس التنفيذي / المدير العام</div>
                </div>
                <div>
                  <div class="inv-print-sig-title">توقيع واستلام العميل</div>
                  <div class="inv-print-sig-line">الاسم والتوقيع والخاتم</div>
                </div>
              </div>
            `;
          }
        }
      } catch (err) {
        console.error('Failed to load invoice for printing:', err);
      }
    }

    async executePrintInvoice() {
      if (this.currentPrintingInvoiceId) {
        try {
          await window.ChatApi.markInvoicePrinted(this.currentPrintingInvoiceId);
          await this.loadInvoiceStatistics();
          await this.loadInvoicesData();
        } catch (err) {
          console.error('Failed to mark invoice as printed:', err);
        }
      }
      window.print();
    }

    closePrintModal() {
      const modalBackdrop = document.getElementById('finance-print-invoice-modal-backdrop');
      if (modalBackdrop) modalBackdrop.style.display = 'none';
    }

    async issueDraftInvoice(id) {
      if (!confirm('هل أنت متأكد من رغبتك في اعتماد وإصدار هذه الفاتورة رسمياً؟ لن يمكن تعديلها بعد الاعتماد.')) return;
      try {
        const res = await window.ChatApi.issueDraftInvoice(id);
        if (res && res.success) {
          alert('تم اعتماد وإصدار الفاتورة بنجاح!');
          await this.loadInvoiceStatistics();
          await this.loadInvoicesData();
        } else {
          alert(res?.message || 'تعذر اعتماد الفاتورة.');
        }
      } catch (err) {
        console.error('Failed to issue draft invoice:', err);
      }
    }

    async cancelInvoice(id) {
      if (!confirm('تحذير: هل أنت متأكد من رغبتك في إلغاء هذه الفاتورة؟ سيتم تحرير كميات معاملات النقل المرتبطة بها لتصبح متاحة للفوترة مجدداً.')) return;
      try {
        const res = await window.ChatApi.cancelInvoice(id);
        if (res && res.success) {
          alert('تم إلغاء الفاتورة بنجاح وتحرير كميات النقل.');
          await this.loadInvoiceStatistics();
          await this.loadInvoicesData();
        } else {
          alert(res?.message || 'تعذر إلغاء الفاتورة.');
        }
      } catch (err) {
        console.error('Failed to cancel invoice:', err);
      }
    }
  }

  // Register instance globally
  window.FinanceManagementApp = new FinanceManagementApp();
})();
