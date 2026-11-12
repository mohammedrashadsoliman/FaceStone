/**
 * FaceStone Storage Department Management - Arabic RTL Application Module
 * Redesigned Storage Dashboard:
 *  - 3 Main Cards:
 *      1. وارد (Import) -> إذن إضافة مواد إلى المخزن (/storage/import)
 *      2. صادر (Export) -> إذن صرف بضاعة من المخزن (/storage/export)
 *      3. تحويل (Transfer) -> تحويل مواد من المخزن إلى الإنتاج (/storage/transfer)
 *  - Operations History Ledger & Production Work-in-Process (WIP) Tracking
 *  - Real-time stock validations, atomic sequences, and Control & Monitoring approval integration.
 */

(function () {
  'use strict';

  class StorageManager {
    constructor() {
      this.initialized = false;
      this.locations = [];
      this.clients = [];
      this.materials = [];
      this.materialStockMap = {}; // key: `${locationId}_${materialId}`, value: stockNumber
      this.activeView = 'hub'; // 'hub', 'import', 'export', 'transfer', 'item-card'
      this.isSubmitting = false;

      // Selection states for dynamic tables
      this.importSelectedMaterials = new Map(); // materialId -> { name, quantity }
      this.importHasNewMaterial = false;
      this.importNewMaterial = { name: '', quantity: '' };
      this.importMaterialType = null; // 'MECHANICAL' | 'CHEMICAL'

      this.exportSelectedMaterials = new Map(); // materialId -> { name, quantity, availableStock }
      this.transferSelectedMaterials = new Map(); // materialId -> { name, quantity, availableStock, trackingId }

      // Cached sequences
      this.nextSequences = { import: 1, export: 1, transfer: 1 };

      // Laboratory production reference traceability integration
      this.importEligibleReferences = [];
      this.selectedImportRef = null;
      this.transferEligibleReferences = [];
      this.selectedTransferRef = null;

      // State for Laboratory & Batch Division integration
      this.importBatches = [];
      this.exportSelectionMode = 'BY_REFERENCE'; // 'BY_REFERENCE' | 'BY_BATCH'
      this.exportAvailableBatches = [];
      this.exportSelectedBatches = new Map(); // batchId -> { batch, quantity, notes }
      this.exportCategoryType = 'RAW_MATERIAL'; // 'RAW_MATERIAL' | 'FINISHED_PRODUCT'
      this.exportFinSelectedProducts = new Map(); // batchId -> { batch, quantity }
      this.availableFinishedProducts = [];
      this.transferSelectionMode = 'BY_REFERENCE'; // 'BY_REFERENCE' | 'BY_BATCH'
      this.transferAvailableBatches = [];
      this.transferSelectedBatches = new Map(); // batchId -> { batch, quantity, notes }
      this.currentPrintBatches = [];
      this.currentPrintOpInfo = null;

      this.init();
    }

    init() {
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => this.bindElements());
      } else {
        this.bindElements();
      }
    }

    bindElements() {
      this.pageRoot = document.getElementById('storage-page-root');
      if (!this.pageRoot) return;

      // Header Elements
      this.btnBackToDepts = document.getElementById('storage-back-to-departments-btn');
      this.userNameEl = document.getElementById('storage-user-name');
      this.userRoleEl = document.getElementById('storage-user-role');
      this.userAvatarEl = document.getElementById('storage-user-avatar');

      // Top-level views
      this.viewHub = document.getElementById('storage-hub-cards-view');
      this.viewImport = document.getElementById('storage-import-view');
      this.viewExport = document.getElementById('storage-export-view');
      this.viewTransfer = document.getElementById('storage-transfer-view');
      this.viewItemCard = document.getElementById('storage-item-card-view');

      // The 3 Main Dashboard Cards
      this.cardImport = document.getElementById('card-nav-storage-import');
      this.cardExport = document.getElementById('card-nav-storage-export');
      this.cardTransfer = document.getElementById('card-nav-storage-transfer');

      // Hub Navigation Events
      this.cardImport?.addEventListener('click', () => this.showImportView());
      this.cardExport?.addEventListener('click', () => this.showExportView());
      this.cardTransfer?.addEventListener('click', () => this.showTransferView());

      // Pending Production Orders Transfer Events
      document.getElementById('btn-refresh-storage-pending-orders')?.addEventListener('click', () => this.loadPendingProductionOrders());
      document.getElementById('storage-confirm-transfer-form')?.addEventListener('submit', (e) => this.submitConfirmOrderTransfer(e));
      document.getElementById('btn-close-storage-trf-modal')?.addEventListener('click', () => {
        const m = document.getElementById('storage-modal-confirm-order-transfer');
        if (m) m.style.display = 'none';
      });
      document.getElementById('btn-cancel-storage-trf-modal')?.addEventListener('click', () => {
        const m = document.getElementById('storage-modal-confirm-order-transfer');
        if (m) m.style.display = 'none';
      });

      // Back Buttons
      document.getElementById('btn-import-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-export-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-transfer-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());
      document.getElementById('btn-storage-back-to-hub')?.addEventListener('click', () => this.showDashboardHub());

      // Hub Tabs & Refresh
      this.btnTabHubOps = document.getElementById('btn-tab-hub-operations');
      this.btnTabHubWip = document.getElementById('btn-tab-hub-wip');
      this.btnTabHubInventory = document.getElementById('btn-tab-hub-inventory');
      this.hubOpsSection = document.getElementById('hub-operations-section');
      this.hubWipSection = document.getElementById('hub-wip-section');
      this.hubInventorySection = document.getElementById('hub-inventory-section');

      this.btnTabHubOps?.addEventListener('click', () => this.switchHubTab('ops'));
      this.btnTabHubWip?.addEventListener('click', () => this.switchHubTab('wip'));
      this.btnTabHubInventory?.addEventListener('click', () => this.switchHubTab('inventory'));
      document.getElementById('btn-hub-refresh-ops')?.addEventListener('click', () => this.loadHubData());
      document.getElementById('btn-hub-refresh-inventory')?.addEventListener('click', () => {
        const locId = document.getElementById('filter-hub-inv-location')?.value;
        this.loadWarehouseInventory(locId);
      });

      // Hub Filters
      document.getElementById('filter-hub-op-type')?.addEventListener('change', () => this.loadOperations());
      document.getElementById('filter-hub-op-status')?.addEventListener('change', () => this.loadOperations());
      document.getElementById('filter-hub-inv-location')?.addEventListener('change', (e) => {
        this.loadWarehouseInventory(e.target.value);
      });

      // Real-Time Inventory & Order Synchronization
      window.addEventListener('facestone:warehouse-inventory-changed', () => {
        const locId = document.getElementById('filter-hub-inv-location')?.value;
        this.loadWarehouseInventory(locId || null);
        this.loadOperations();
        this.loadPendingProductionOrders();
      });
      window.addEventListener('facestone:production-order-changed', () => {
        this.loadPendingProductionOrders();
      });
      window.addEventListener('facestone:data-changed', (e) => {
        if (e.detail?.department === 'storage' || e.detail?.department === 'production') {
          const locId = document.getElementById('filter-hub-inv-location')?.value;
          this.loadWarehouseInventory(locId || null);
          this.loadOperations();
          this.loadPendingProductionOrders();
        }
      });

      // Global Toast
      this.toastEl = document.getElementById('storage-global-toast');
      this.toastMsg = document.getElementById('storage-toast-message');
      this.toastIcon = document.getElementById('storage-toast-icon');

      // Modals
      this.wipOutputModal = document.getElementById('storage-record-wip-modal');
      this.opDetailsModal = document.getElementById('storage-op-details-modal');
      this.batchPrintModal = document.getElementById('storage-batch-print-modal');

      document.getElementById('btn-close-wip-modal')?.addEventListener('click', () => this.closeWipModal());
      document.getElementById('btn-cancel-wip-modal')?.addEventListener('click', () => this.closeWipModal());
      document.getElementById('btn-close-op-details-modal')?.addEventListener('click', () => this.closeOpDetailsModal());
      document.getElementById('btn-cancel-op-details-modal')?.addEventListener('click', () => this.closeOpDetailsModal());
      document.getElementById('btn-close-batch-print-modal')?.addEventListener('click', () => this.closeBatchPrintModal());
      document.getElementById('btn-cancel-batch-print-modal')?.addEventListener('click', () => this.closeBatchPrintModal());
      document.getElementById('btn-execute-batch-print')?.addEventListener('click', () => this.executeBatchPrint());

      // WIP Output Form submission
      document.getElementById('storage-wip-output-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleWipOutputSubmit();
      });

      // WIP Output quantity input listener for mass balance calculation
      document.getElementById('wip-modal-product-qty')?.addEventListener('input', () => {
        this.calculateWipMassBalance();
      });

      // WIP Output waste classification toggle
      document.querySelectorAll('input[name="wip-waste-class"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
          const wrap = document.getElementById('wip-recoverable-wrap');
          if (wrap) {
            wrap.style.display = (e.target.value === 'ORDINARY') ? 'none' : 'block';
          }
        });
      });

      // Bind Import Form
      this.bindImportEvents();

      // Bind Export Form
      this.bindExportEvents();

      // Bind Transfer Form
      this.bindTransferEvents();

      // Register SignalR real-time updates listener
      this.bindSignalREvents();

      this.initialized = true;
    }

    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) this.userNameEl.textContent = user.fullName || user.username;
      if (this.userRoleEl) this.userRoleEl.textContent = user.role || 'مسؤول المستودع';
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'FS')
          .split(' ')
          .map(w => w[0])
          .join('')
          .substring(0, 2)
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'FS';
      }

      // Pre-fill read-only responsible employee inputs in forms
      const empName = user.fullName || user.username || 'الموظف المسؤول';
      const importEmpEl = document.getElementById('import-responsible-emp');
      const exportEmpEl = document.getElementById('export-responsible-emp');
      const transferEmpEl = document.getElementById('transfer-responsible-emp');
      if (importEmpEl) importEmpEl.textContent = empName;
      if (exportEmpEl) exportEmpEl.textContent = empName;
      if (transferEmpEl) transferEmpEl.textContent = empName;
    }

    bindSignalREvents() {
      if (window.realTimeChat?.connection) {
        window.realTimeChat.connection.on('OnDepartmentDataChanged', (data) => {
          if (data && data.formType === 'Storage') {
            this.loadHubData();
          }
        });
        window.realTimeChat.connection.on('OnFormApprovalStatusChanged', (data) => {
          if (data && data.formType === 'Storage') {
            this.loadHubData();
          }
        });
      }
    }

    // =========================================================================
    // View Routing
    // =========================================================================

    showDashboardHub() {
      this.activeView = 'hub';
      if (this.viewHub) this.viewHub.style.display = 'block';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage') {
        history.pushState({ view: 'storage', subRoute: '/storage' }, '', '/storage');
      }

      this.loadHubData();
    }

    async showImportView() {
      this.activeView = 'import';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'block';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/import') {
        history.pushState({ view: 'storage', subRoute: '/storage/import' }, '', '/storage/import');
      }

      await this.prepareImportForm();
    }

    async showExportView() {
      this.activeView = 'export';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'block';
      if (this.viewTransfer) this.viewTransfer.style.display = 'none';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/export') {
        history.pushState({ view: 'storage', subRoute: '/storage/export' }, '', '/storage/export');
      }

      await this.prepareExportForm();
    }

    async showTransferView() {
      this.activeView = 'transfer';
      if (this.viewHub) this.viewHub.style.display = 'none';
      if (this.viewImport) this.viewImport.style.display = 'none';
      if (this.viewExport) this.viewExport.style.display = 'none';
      if (this.viewTransfer) this.viewTransfer.style.display = 'block';
      if (this.viewItemCard) this.viewItemCard.style.display = 'none';

      if (window.location.pathname !== '/storage/transfer') {
        history.pushState({ view: 'storage', subRoute: '/storage/transfer' }, '', '/storage/transfer');
      }

      await this.prepareTransferForm();
      await this.loadPendingProductionOrders();
    }

    handleRoute(subRoute) {
      if (!subRoute) {
        this.showDashboardHub();
        return;
      }
      if (subRoute === '/storage/import' || subRoute.includes('import')) {
        this.showImportView();
      } else if (subRoute === '/storage/export' || subRoute.includes('export')) {
        this.showExportView();
      } else if (subRoute === '/storage/transfer' || subRoute.includes('transfer')) {
        this.showTransferView();
      } else {
        this.showDashboardHub();
      }
    }

    async loadInitialData() {
      this.bindElements();
      await this.loadHubData();
    }

    // =========================================================================
    // Data Loading Helpers
    // =========================================================================

    async loadMetadata() {
      // Fetch locations
      try {
        const resLoc = await ChatApi.getStorageLocations();
        if (resLoc.success && Array.isArray(resLoc.data)) {
          this.locations = resLoc.data;
        }
      } catch (e) {
        console.error('Failed loading storage locations:', e);
      }

      // Fetch clients
      try {
        const resCli = await ChatApi.getStorageClients();
        if (resCli.success && Array.isArray(resCli.data)) {
          this.clients = resCli.data;
        }
      } catch (e) {
        console.error('Failed loading storage clients:', e);
      }

      // Fetch upcoming sequence numbers
      try {
        const resSeq = await ChatApi.getStorageNextNumbers();
        if (resSeq.success && resSeq.data) {
          this.nextSequences = {
            import: resSeq.data.nextImport ?? resSeq.data.nextImportNumber ?? 1,
            export: resSeq.data.nextExport ?? resSeq.data.nextExportNumber ?? 1,
            transfer: resSeq.data.nextTransfer ?? resSeq.data.nextTransferNumber ?? 1
          };
        }
      } catch (e) {
        console.error('Failed loading storage sequences:', e);
      }

      // Fetch materials
      try {
        const resMat = await ChatApi.getStorageMaterialsStock();
        if (resMat.success && Array.isArray(resMat.data)) {
          this.materials = resMat.data;
        }
      } catch (e) {
        console.error('Failed loading materials stock:', e);
      }
    }

    formatLocalDateTimeForInput(date = new Date()) {
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    // =========================================================================
    // VIEW 1: HUB DATA (Operations Ledger & WIP Batches)
    // =========================================================================

    switchHubTab(tabKey) {
      const tabs = [
        { key: 'ops', btn: this.btnTabHubOps, sec: this.hubOpsSection },
        { key: 'wip', btn: this.btnTabHubWip, sec: this.hubWipSection },
        { key: 'inventory', btn: this.btnTabHubInventory, sec: this.hubInventorySection }
      ];

      tabs.forEach(t => {
        const isActive = t.key === tabKey;
        if (t.btn) {
          t.btn.style.background = isActive ? '#FFFFFF' : 'transparent';
          t.btn.style.border = isActive ? '1px solid #CBD5E1' : 'none';
          t.btn.style.fontWeight = isActive ? '700' : '600';
        }
        if (t.sec) {
          t.sec.style.display = isActive ? 'block' : 'none';
        }
      });

      if (tabKey === 'inventory') {
        const locId = document.getElementById('filter-hub-inv-location')?.value;
        this.loadWarehouseInventory(locId);
      }
    }

    async loadHubData() {
      await Promise.all([
        this.loadOperations(),
        this.loadProductionBatches(),
        this.loadWarehouseInventory()
      ]);
    }

    async loadWarehouseInventory(locationId = null) {
      const tbody = document.getElementById('hub-inventory-tbody');
      const filterSelect = document.getElementById('filter-hub-inv-location');

      // Populate filter dropdown once if needed
      if (filterSelect && filterSelect.options.length <= 1 && this.locations && this.locations.length > 0) {
        this.locations.forEach(loc => {
          const opt = document.createElement('option');
          opt.value = loc.id;
          opt.textContent = `${loc.name} (${loc.code})`;
          filterSelect.appendChild(opt);
        });
      }

      if (tbody) {
        tbody.innerHTML = `
          <tr>
            <td colspan="9" style="text-align: center; padding: 2rem; color: #94A3B8;">
              <span class="storage-btn-spinner" style="display:inline-block; border-color:#059669; border-top-color:transparent; margin-left:8px;"></span>
              جاري تحميل رصيد المخزون والدفعات...
            </td>
          </tr>
        `;
      }

      try {
        const res = await ChatApi.getStorageInventory({ locationId: locationId || undefined });
        if (!res.success || !Array.isArray(res.data)) {
          if (tbody) tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:2rem; color:#EF4444;">فشل جلب بيانات المخزون.</td></tr>`;
          return;
        }

        const items = res.data;
        if (items.length === 0) {
          if (tbody) {
            tbody.innerHTML = `
              <tr>
                <td colspan="9" style="text-align:center; padding:2.5rem; color:#64748B;">
                  <div style="font-size:1.5rem; margin-bottom:8px;">📦</div>
                  <div style="font-weight:700; font-size:1rem; color:#334155;">لا توجد مواد أو دفعات متاحة في هذا المخزن حالياً</div>
                  <div style="font-size:0.8rem; color:#94A3B8; margin-top:4px;">يمكنك إضافة دفعات جديدة من شاشة الوارد بعد اعتماد نتائج الفحص المعملي</div>
                </td>
              </tr>
            `;
          }
          return;
        }

        tbody.innerHTML = items.map(b => {
          const isChem = b.materialClassification === 'CHEMICAL';
          const classBadge = isChem
            ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
            : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

          const refDisplay = b.sampleCode 
            ? `عينة: <strong>${this.escapeHtml(b.sampleCode)}</strong>` 
            : (b.productionReferenceNumber ? `مرجع: <strong>#${b.productionReferenceNumber}</strong>` : '-');

          const dateDisplay = b.createdAt ? new Date(b.createdAt).toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';

          return `
            <tr>
              <td>
                <div style="font-weight:700; color:#0F172A;">${this.escapeHtml(b.storageLocationName || '-')}</div>
                <div style="font-size:0.75rem; color:#64748B;">${this.escapeHtml(b.storageLocationCode || '')}</div>
              </td>
              <td>
                <span class="storage-seq-badge" style="background:#F0FDF4; color:#15803D; border:1px solid #BBF7D0; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                  ${this.escapeHtml(b.batchNumber)}
                </span>
              </td>
              <td>
                <span style="font-size:0.85rem; color:#1E40AF;">${refDisplay}</span>
              </td>
              <td>
                <strong style="color:#0F172A;">${this.escapeHtml(b.materialName)}</strong>
              </td>
              <td>${classBadge}</td>
              <td style="text-align:center; font-weight:600; color:#64748B;">
                ${Number(b.initialQuantity || 0).toFixed(3)} طن
              </td>
              <td style="text-align:center; font-weight:800; color:#059669; font-size:1.05rem;">
                ${Number(b.availableQuantity || 0).toFixed(3)} طن
              </td>
              <td>
                <span class="badge" style="background:#DCFCE7; color:#15803D; font-size:0.75rem; padding:3px 8px; border-radius:4px; font-weight:700;">
                  ${this.escapeHtml(b.status || 'متاح')}
                </span>
              </td>
              <td style="font-size:0.8rem; color:#64748B;">
                ${dateDisplay}
              </td>
            </tr>
          `;
        }).join('');
      } catch (err) {
        console.error('Error loading inventory:', err);
        if (tbody) tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:2rem; color:#EF4444;">حدث خطأ أثناء تحميل بيانات المخزون.</td></tr>`;
      }
    }

    async loadOperations() {
      const typeFilter = document.getElementById('filter-hub-op-type')?.value || 'ALL';
      const statusFilter = document.getElementById('filter-hub-op-status')?.value || 'ALL';
      const tbody = document.getElementById('hub-operations-tbody');

      if (tbody) {
        tbody.innerHTML = `
          <tr>
            <td colspan="9" style="text-align: center; padding: 2rem; color: #94A3B8;">
              <span class="storage-btn-spinner" style="display:inline-block; border-color:#059669; border-top-color:transparent; margin-left:8px;"></span>
              جاري تحميل أذونات المستودع...
            </td>
          </tr>
        `;
      }

      try {
        const res = await ChatApi.getStorageOperations({ type: typeFilter, status: statusFilter, page: 1, pageSize: 50 });
        if (!res.success || !Array.isArray(res.data)) {
          if (tbody) tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; padding:2rem; color:#EF4444;">فشل جلب بيانات العمليات.</td></tr>`;
          return;
        }

        const items = res.data;

        // Calculate KPI summaries
        let importTons = 0, exportTons = 0, transferTons = 0, pendingCount = 0;
        items.forEach(op => {
          if (op.approvalStatus === 'PENDING') pendingCount++;
          if (op.approvalStatus === 'APPROVED') {
            if (op.operationType === 'IMPORT') importTons += (op.totalQuantity || 0);
            if (op.operationType === 'EXPORT') exportTons += (op.totalQuantity || 0);
            if (op.operationType === 'TRANSFER') transferTons += (op.totalQuantity || 0);
          }
        });

        const elImp = document.getElementById('kpi-hub-import-total');
        const elExp = document.getElementById('kpi-hub-export-total');
        const elTrf = document.getElementById('kpi-hub-transfer-total');
        const elPnd = document.getElementById('kpi-hub-pending-count');

        if (elImp) elImp.innerHTML = `<span>${importTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elExp) elExp.innerHTML = `<span>${exportTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elTrf) elTrf.innerHTML = `<span>${transferTons.toFixed(3)}</span> <span class="storage-kpi-unit">طن</span>`;
        if (elPnd) elPnd.innerHTML = `<span>${pendingCount}</span> <span class="storage-kpi-unit">طلب</span>`;

        if (items.length === 0) {
          if (tbody) {
            tbody.innerHTML = `
              <tr>
                <td colspan="9" style="text-align: center; padding: 2.5rem; color: #94A3B8;">
                  لا توجد حركات مسجلة مطابقة للفلاتر المختارة
                </td>
              </tr>
            `;
          }
          return;
        }

        if (tbody) {
          tbody.innerHTML = items.map(op => {
            const typeBadge = op.operationType === 'IMPORT'
              ? `<span class="badge-op-tag badge-op-import">وارد</span>`
              : op.operationType === 'EXPORT'
              ? `<span class="badge-op-tag badge-op-export">صادر</span>`
              : `<span class="badge-op-tag badge-op-transfer">تحويل</span>`;

            const statusBadge = op.approvalStatus === 'APPROVED'
              ? `<span class="badge-status-tag badge-status-approved">معتمد</span>`
              : op.approvalStatus === 'REJECTED'
              ? `<span class="badge-status-tag badge-status-rejected">مرفوض</span>`
              : `<span class="badge-status-tag badge-status-pending">قيد المراجعة</span>`;

            const secondParty = op.operationType === 'IMPORT'
              ? `المورد: ${this.escapeHtml(op.source || '-')}`
              : op.operationType === 'EXPORT'
              ? `${this.escapeHtml(op.clientName || 'عميل')} - ${this.escapeHtml(op.destinationName || '')}`
              : (op.destinationDepartment === 'مستودع' ? `مستودع: ${this.escapeHtml(op.destinationName || '-')}` : `خط الإنتاج (داخلي)`);

            let itemsSummary = '';
            if (op.items && op.items.length > 0) {
              const lines = op.items.map(i => {
                const bTag = (i.batchNumber || i.warehouseBatchNumber)
                  ? ` <span class="storage-seq-badge" style="font-size:0.75rem; padding:1px 6px; font-family:monospace;">${this.escapeHtml(i.batchNumber || i.warehouseBatchNumber)}</span>`
                  : '';
                return `<div style="margin-bottom:2px;"><strong>${this.escapeHtml(i.materialName || 'مادة')}</strong>${bTag}: <span style="color:#059669; font-weight:700;">${Number(i.quantity || 0).toFixed(3)} طن</span></div>`;
              });
              if (op.items.length > 1) {
                const calcTotal = op.items.reduce((sum, it) => sum + Number(it.quantity || 0), 0);
                const displayTotal = (op.totalQuantity && op.totalQuantity > 0) ? op.totalQuantity : calcTotal;
                lines.push(`<div style="border-top:1px dashed #CBD5E1; margin-top:3px; padding-top:2px; font-weight:800; color:#0F172A;">الإجمالي: ${Number(displayTotal).toFixed(3)} طن</div>`);
              }
              itemsSummary = lines.join('');
            } else if (op.totalQuantity && op.totalQuantity > 0) {
              itemsSummary = `<strong style="color:#059669;">${Number(op.totalQuantity).toFixed(3)} طن</strong>`;
            } else {
              itemsSummary = `<span style="color:#94a3b8;">—</span>`;
            }

            return `
              <tr>
                <td><strong style="color:#0F172A;">${this.escapeHtml(op.transactionFormattedNumber)}</strong></td>
                <td>${typeBadge}</td>
                <td style="font-size:0.8rem; color:#475569;">${this.formatDateTime(op.transactionDate)}</td>
                <td>${this.escapeHtml(op.storageLocationName || '-')}</td>
                <td style="font-size:0.85rem;">${secondParty}</td>
                <td style="font-size:0.8rem; line-height:1.4;">${itemsSummary}</td>
                <td>${this.escapeHtml(op.responsibleEmployee || '-')}</td>
                <td>${statusBadge}</td>
                <td>
                  <button type="button" class="storage-btn-secondary btn-view-op-detail" data-op-id="${op.id}" style="padding: 4px 10px; font-size: 0.775rem;">
                    تفاصيل
                  </button>
                </td>
              </tr>
            `;
          }).join('');

          // Bind view detail buttons
          tbody.querySelectorAll('.btn-view-op-detail').forEach(btn => {
            btn.addEventListener('click', (e) => {
              const id = parseInt(e.currentTarget.getAttribute('data-op-id'), 10);
              if (id) this.openOpDetailsModal(id);
            });
          });
        }
      } catch (e) {
        console.error('Error in loadOperations:', e);
      }
    }

    async loadProductionBatches() {
      const tbody = document.getElementById('hub-wip-tbody');
      if (!tbody) return;

      try {
        const res = await ChatApi.getStorageProductionBatches();
        if (!res.success || !Array.isArray(res.data)) {
          tbody.innerHTML = `<tr><td colspan="10" style="text-align:center; padding:2rem; color:#EF4444;">فشل جلب دفعات الإنتاج.</td></tr>`;
          return;
        }

        const batches = res.data;
        if (batches.length === 0) {
          tbody.innerHTML = `
            <tr>
              <td colspan="10" style="text-align: center; padding: 2.5rem; color: #94A3B8;">
                لا توجد دفعات إنتاج محولة حتى الآن. عند اعتماد أذونات التحويل للإنتاج، ستظهر هنا تلقائياً لمتابعة المخرجات وحساب الهالك.
              </td>
            </tr>
          `;
          return;
        }

        tbody.innerHTML = batches.map(b => {
          const isCompleted = b.status === 'مكتمل';
          const statusBadge = isCompleted
            ? `<span class="badge-status-tag badge-status-approved">مكتمل</span>`
            : `<span class="badge-status-tag badge-status-pending">قيد التشغيل</span>`;

          const outputAction = isCompleted
            ? `<span style="font-size:0.775rem; color:#059669; font-weight:700;">تم التسجيل</span>`
            : `<button type="button" class="storage-btn-primary btn-record-output" data-batch-id="${b.id}" style="background:#2563EB; padding:4px 10px; font-size:0.75rem;">تسجيل المخرجات</button>`;

          const wasteDisplay = isCompleted && b.wasteQuantity !== null
            ? `<strong>${Number(b.wasteQuantity).toFixed(3)} طن</strong> (${Number(b.wastePercentage).toFixed(2)}%)`
            : `<span style="color:#94A3B8;">بانتظار بيانات الإنتاج</span>`;

          return `
            <tr>
              <td><strong style="color:#2563EB;">${this.escapeHtml(b.trackingNumber)}</strong></td>
              <td style="font-size:0.8rem;">${this.formatDate(b.transferDate)}</td>
              <td>${this.escapeHtml(b.materialName)}</td>
              <td><strong style="color:#059669;">${Number(b.materialQuantityIssued).toFixed(3)}</strong> طن</td>
              <td>${this.escapeHtml(b.sourceStorageLocation || '-')}</td>
              <td>${statusBadge}</td>
              <td>${this.escapeHtml(b.finalProductName || '—')}</td>
              <td>${b.finalProductQuantity ? `${Number(b.finalProductQuantity).toFixed(3)} طن` : '—'}</td>
              <td>${wasteDisplay}</td>
              <td>${outputAction}</td>
            </tr>
          `;
        }).join('');

        // Bind output recording buttons
        tbody.querySelectorAll('.btn-record-output').forEach(btn => {
          btn.addEventListener('click', (e) => {
            const batchId = parseInt(e.currentTarget.getAttribute('data-batch-id'), 10);
            const batch = batches.find(b => b.id === batchId);
            if (batch) this.openWipModal(batch);
          });
        });

      } catch (e) {
        console.error('Error in loadProductionBatches:', e);
      }
    }

    // =========================================================================
    // VIEW 2: IMPORT FORM LOGIC
    // =========================================================================

    async prepareImportForm() {
      await this.loadMetadata();

      // Reset selection maps & batches
      this.importBatches = [];
      this.importSelectedMaterials.clear();
      this.importHasNewMaterial = false;
      this.importNewMaterial = { name: '', quantity: '' };
      this.importMaterialType = null;

      // Reset material type radios
      document.querySelectorAll('input[name="import-material-type"]').forEach(r => r.checked = false);
      const typeHint = document.getElementById('import-type-hint');
      if (typeHint) typeHint.style.display = 'none';

      // Set Next Sequence preview
      const seqEl = document.getElementById('import-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.import}`;

      // Set default datetime
      const dtInput = document.getElementById('import-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Populate locations
      const locSelect = document.getElementById('import-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      // Checkbox new material reset
      const chkNew = document.getElementById('import-chk-new-material');
      const wrapNew = document.getElementById('import-new-material-wrap');
      const inputNew = document.getElementById('import-new-material-name');
      if (chkNew) chkNew.checked = false;
      if (wrapNew) wrapNew.style.display = 'none';
      if (inputNew) inputNew.value = '';

      // Reset weighing fields
      ['import-ext-weight', 'import-loaded-weight', 'import-empty-weight', 'import-weighbridge-ref', 'import-weighing-emp', 'import-discrepancy-explanation'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
      });
      const wtEl = document.getElementById('import-weighing-time');
      if (wtEl) wtEl.value = this.formatLocalDateTimeForInput();
      const res = document.getElementById('import-weighing-result');
      if (res) res.style.display = 'none';
      const warn = document.getElementById('import-discrepancy-warning');
      if (warn) warn.style.display = 'none';

      // Reset batch count input
      const batchCountInput = document.getElementById('import-batch-count');
      if (batchCountInput) batchCountInput.value = '1';

      // Reset batch division table & balance
      const tbody = document.getElementById('import-batches-tbody');
      if (tbody) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; padding:1.5rem; color:#94A3B8;">يرجى اختيار رقم العينة / المرجع المعملي أولاً لتقسيم الدفعات</td></tr>`;
      }
      this.updateImportBatchesTotal();

      // Populate materials checklist (no type filter initially)
      this.renderImportChecklist();
      this.renderImportQuantitiesTable();
      this.clearAllErrors();

      // Reset & load Laboratory Production References
      this.selectedImportRef = null;
      const importRefInfo = document.getElementById('import-prod-ref-info');
      if (importRefInfo) importRefInfo.style.display = 'none';
      await this.loadImportProductionReferences();
    }

    renderImportChecklist() {
      const container = document.getElementById('import-materials-checklist');
      if (!container) return;

      if (this.materials.length === 0) {
        container.innerHTML = `<span style="grid-column: 1/-1; color: #94A3B8; font-size: 0.85rem;">لا توجد أصناف خامات مسجلة. يمكنك تحديد "إضافة صنف جديد" بالأسفل.</span>`;
        return;
      }

      container.innerHTML = this.materials.map(mat => {
        const isChecked = this.importSelectedMaterials.has(mat.id);
        return `
          <label class="storage-check-label ${isChecked ? 'is-checked' : ''}" data-mat-id="${mat.id}">
            <input type="checkbox" value="${mat.id}" ${isChecked ? 'checked' : ''} />
            <span>${this.escapeHtml(mat.name)}</span>
          </label>
        `;
      }).join('');

      // Add event listeners to checklist checkboxes
      container.querySelectorAll('input[type="checkbox"]').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const matId = parseInt(e.target.value, 10);
          const mat = this.materials.find(m => m.id === matId);
          if (!mat) return;

          if (e.target.checked) {
            this.importSelectedMaterials.set(matId, { name: mat.name, quantity: '' });
            e.target.closest('.storage-check-label')?.classList.add('is-checked');
          } else {
            this.importSelectedMaterials.delete(matId);
            e.target.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderImportQuantitiesTable();
        });
      });
    }

    renderImportQuantitiesTable() {
      const tbody = document.getElementById('import-quantities-tbody');
      const itemsBadge = document.getElementById('import-total-items-badge');
      const qtyBadge = document.getElementById('import-total-qty-badge');
      if (!tbody) return;

      const rows = [];
      let totalQty = 0;

      // Existing materials
      this.importSelectedMaterials.forEach((val, matId) => {
        const currentVal = val.quantity;
        const numVal = parseFloat(currentVal);
        if (!isNaN(numVal) && numVal > 0) totalQty += numVal;

        rows.push(`
          <tr data-mat-id="${matId}">
            <td><strong>${this.escapeHtml(val.name)}</strong></td>
            <td>
              <input type="number" class="storage-input import-qty-input" data-mat-id="${matId}"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(currentVal)}" required />
            </td>
            <td style="text-align: center;">
              <button type="button" class="storage-btn-secondary btn-remove-import-line" data-mat-id="${matId}" style="padding: 4px 8px; color: #DC2626;">
                ✕ حذف
              </button>
            </td>
          </tr>
        `);
      });

      // New material line
      if (this.importHasNewMaterial) {
        const newName = this.importNewMaterial.name.trim() || '(صنف جديد)';
        const currentVal = this.importNewMaterial.quantity;
        const numVal = parseFloat(currentVal);
        if (!isNaN(numVal) && numVal > 0) totalQty += numVal;

        rows.push(`
          <tr data-new-line="true" style="background: #F0FDF4;">
            <td>
              <strong id="import-new-mat-row-name" style="color: #059669;">✨ ${this.escapeHtml(newName)}</strong>
              <span style="font-size: 0.75rem; color: #64748B; display: block;">صنف جديد سيتم إنشاؤه</span>
            </td>
            <td>
              <input type="number" class="storage-input" id="import-new-mat-qty-input"
                     step="0.001" min="0.001" placeholder="0.000" value="${this.escapeHtml(currentVal)}" required />
            </td>
            <td style="text-align: center;">
              <span style="font-size: 0.8rem; color: #059669; font-weight: 700;">جديد</span>
            </td>
          </tr>
        `);
      }

      const totalItems = this.importSelectedMaterials.size + (this.importHasNewMaterial ? 1 : 0);
      if (itemsBadge) itemsBadge.textContent = totalItems;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (rows.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="3" style="text-align: center; padding: 2rem; color: #94A3B8;">
              اختر صنفاً أو أكثر من القائمة أعلاه لإدخال كمياتها
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = rows.join('');

      // Input change listeners
      tbody.querySelectorAll('.import-qty-input').forEach(input => {
        input.addEventListener('input', (e) => {
          const matId = parseInt(e.target.getAttribute('data-mat-id'), 10);
          if (this.importSelectedMaterials.has(matId)) {
            this.importSelectedMaterials.get(matId).quantity = e.target.value;
            this.updateImportTotalSummary();
          }
        });
      });

      const newQtyInput = document.getElementById('import-new-mat-qty-input');
      newQtyInput?.addEventListener('input', (e) => {
        this.importNewMaterial.quantity = e.target.value;
        this.updateImportTotalSummary();
      });

      // Remove buttons
      tbody.querySelectorAll('.btn-remove-import-line').forEach(btn => {
        btn.addEventListener('click', (e) => {
          const matId = parseInt(e.currentTarget.getAttribute('data-mat-id'), 10);
          this.importSelectedMaterials.delete(matId);
          // Uncheck corresponding checkbox
          const chk = document.querySelector(`#import-materials-checklist input[value="${matId}"]`);
          if (chk) {
            chk.checked = false;
            chk.closest('.storage-check-label')?.classList.remove('is-checked');
          }
          this.renderImportQuantitiesTable();
        });
      });
    }

    updateImportTotalSummary() {
      let totalQty = 0;
      this.importSelectedMaterials.forEach(val => {
        const num = parseFloat(val.quantity);
        if (!isNaN(num) && num > 0) totalQty += num;
      });
      if (this.importHasNewMaterial) {
        const num = parseFloat(this.importNewMaterial.quantity);
        if (!isNaN(num) && num > 0) totalQty += num;
      }
      const qtyBadge = document.getElementById('import-total-qty-badge');
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;
    }

    bindImportEvents() {
      // Material type radio buttons
      document.querySelectorAll('input[name="import-material-type"]').forEach(radio => {
        radio.addEventListener('change', async (e) => {
          this.importMaterialType = e.target.value;
          this.importSelectedMaterials.clear();

          // Update type hint text
          const typeHint = document.getElementById('import-type-hint');
          if (typeHint) {
            const typeName = this.importMaterialType === 'MECHANICAL' ? 'الميكانيكية' : 'الكيميائية';
            typeHint.textContent = `عرض أصناف الخامات ${typeName} فقط — تم تصفية القائمة`;
            typeHint.style.display = 'block';
          }

          // Highlight selected type label
          document.getElementById('import-type-mechanical-label')?.classList.toggle('is-checked', this.importMaterialType === 'MECHANICAL');
          document.getElementById('import-type-chemical-label')?.classList.toggle('is-checked', this.importMaterialType === 'CHEMICAL');

          // Fetch filtered materials from backend
          try {
            const res = await ChatApi.getRawMaterials(this.importMaterialType);
            if (res.success && Array.isArray(res.data)) {
              this.materials = res.data;
            }
          } catch (e) {
            console.warn('Could not filter materials by type:', e);
          }

          this.renderImportChecklist();
          this.renderImportQuantitiesTable();
          this.clearFieldError('import-material-type');
        });
      });

      // Weighing inputs live calculation
      ['import-loaded-weight', 'import-empty-weight', 'import-ext-weight'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', () => this.updateImportWeighingDisplay());
      });

      // Toggle new material checkbox
      const chkNew = document.getElementById('import-chk-new-material');
      const wrapNew = document.getElementById('import-new-material-wrap');
      const inputNew = document.getElementById('import-new-material-name');

      chkNew?.addEventListener('change', (e) => {
        this.importHasNewMaterial = e.target.checked;
        if (wrapNew) wrapNew.style.display = e.target.checked ? 'block' : 'none';
        if (e.target.checked && inputNew) {
          inputNew.focus();
        }
        this.renderImportQuantitiesTable();
      });

      inputNew?.addEventListener('input', (e) => {
        this.importNewMaterial.name = e.target.value;
        const rowName = document.getElementById('import-new-mat-row-name');
        if (rowName) {
          rowName.textContent = `✨ ${e.target.value.trim() || '(صنف جديد)'}`;
        }
      });

      // Reset
      document.getElementById('btn-import-reset')?.addEventListener('click', () => {
        this.prepareImportForm();
      });

      // Laboratory Production Reference Listeners
      document.getElementById('import-btn-refresh-refs')?.addEventListener('click', async () => {
        await this.loadImportProductionReferences();
      });
      document.getElementById('import-prod-ref-select')?.addEventListener('change', async (e) => {
        await this.onImportProductionReferenceChanged(e.target.value);
      });

      // Batch division count listener
      const batchCountInput = document.getElementById('import-batch-count');
      batchCountInput?.addEventListener('input', async (e) => {
        const count = parseInt(e.target.value, 10);
        if (!isNaN(count) && count >= 1 && count <= 50) {
          await this.generateImportBatches(count);
        }
      });

      // Quick batch count buttons
      document.querySelectorAll('.btn-quick-batch').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          const count = parseInt(e.currentTarget.getAttribute('data-count'), 10);
          if (batchCountInput) batchCountInput.value = count;
          await this.generateImportBatches(count);
        });
      });

      // Equal distribution button
      document.getElementById('import-btn-distribute-equal')?.addEventListener('click', () => {
        this.distributeImportBatchesEqually();
      });

      // Submit
      document.getElementById('storage-import-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleImportSubmit();
      });
    }

    updateImportWeighingDisplay() {
      const loaded = parseFloat(document.getElementById('import-loaded-weight')?.value) || 0;
      const empty = parseFloat(document.getElementById('import-empty-weight')?.value) || 0;
      const ext = parseFloat(document.getElementById('import-ext-weight')?.value) || 0;

      const net = loaded - empty;
      const resultBox = document.getElementById('import-weighing-result');
      const warnBox = document.getElementById('import-discrepancy-warning');

      if (loaded > 0 && empty > 0) {
        const diff = Math.abs(net - ext);
        const pct = ext > 0 ? (diff / ext) * 100 : 0;

        const netEl = document.getElementById('import-net-weight-display');
        const diffEl = document.getElementById('import-weight-diff-display');
        const pctEl = document.getElementById('import-weight-pct-display');

        if (netEl) netEl.textContent = `${net.toFixed(3)} طن`;
        if (diffEl) diffEl.textContent = `${diff.toFixed(3)} طن`;
        if (pctEl) pctEl.textContent = `${pct.toFixed(2)} %`;

        if (resultBox) resultBox.style.display = 'block';

        const flagged = pct > 0.5 || diff > 0.05;
        if (warnBox) warnBox.style.display = flagged ? 'block' : 'none';
      } else {
        if (resultBox) resultBox.style.display = 'none';
        if (warnBox) warnBox.style.display = 'none';
      }
    }

    async handleImportSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      if (!this.selectedImportRef) {
        this.showToast('error', 'يرجى اختيار رقم العينة / المرجع المعملي المطابق للاستلام.');
        return;
      }

      const locationId = parseInt(document.getElementById('import-location-select')?.value, 10);
      const supplier = document.getElementById('import-supplier-select')?.value?.trim();
      const phone = document.getElementById('import-phone')?.value?.trim();
      const dtVal = document.getElementById('import-datetime')?.value;
      const notes = document.getElementById('import-notes')?.value?.trim();

      let hasError = false;

      if (!dtVal) {
        this.setFieldError('import-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('import-location-select', 'مكان التخزين مطلوب.');
        hasError = true;
      }
      if (!supplier) {
        this.setFieldError('import-supplier-select', 'المورد / المصدر مطلوب.');
        hasError = true;
      }

      if (!this.importBatches || this.importBatches.length === 0) {
        this.showToast('error', 'يرجى تحديد دفعة واحدة على الأقل للاستلام.');
        return;
      }

      // Validate batches
      let totalBatchesQty = 0;
      for (let i = 0; i < this.importBatches.length; i++) {
        const b = this.importBatches[i];
        const qty = parseFloat(b.quantity);
        if (isNaN(qty) || qty <= 0) {
          this.showToast('error', `الدفعة رقم ${i + 1} (${b.batchNumber}) يجب أن تحتوي على كمية أكبر من الصفر.`);
          return;
        }
        totalBatchesQty += qty;
      }

      // Validate sum against remaining available
      const remainingAvail = Number(this.selectedImportRef.remainingAvailableQuantity) || 0;
      if (totalBatchesQty > remainingAvail + 0.0005) {
        this.showToast('error', `مجموع كميات الدفعات (${totalBatchesQty.toFixed(3)} طن) يتجاوز الكمية المتاحة المتبقية من العينة (${remainingAvail.toFixed(3)} طن).`);
        return;
      }

      // Validate discrepancy explanation if warning is shown
      const warnBox = document.getElementById('import-discrepancy-warning');
      if (warnBox && warnBox.style.display !== 'none') {
        const expl = document.getElementById('import-discrepancy-explanation')?.value?.trim();
        if (!expl) {
          this.setFieldError('import-discrepancy-explanation', 'تفسير الفرق في الوزن مطلوب عند وجود فرق يتجاوز 0.5%.');
          hasError = true;
        }
      }

      if (hasError) return;

      // Build weighing payload
      const loadedW = parseFloat(document.getElementById('import-loaded-weight')?.value) || null;
      const emptyW = parseFloat(document.getElementById('import-empty-weight')?.value) || null;
      const extW = parseFloat(document.getElementById('import-ext-weight')?.value) || null;
      const weighingTime = document.getElementById('import-weighing-time')?.value || null;
      const weighbridgeRef = document.getElementById('import-weighbridge-ref')?.value?.trim() || null;
      const weighingEmp = document.getElementById('import-weighing-emp')?.value?.trim() || null;
      const discrepancyExpl = document.getElementById('import-discrepancy-explanation')?.value?.trim() || null;

      const batchList = this.importBatches.map(b => ({
        batchNumber: b.batchNumber,
        quantity: parseFloat(b.quantity) || 0,
        notes: b.notes || null
      }));

      const payload = {
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        laboratoryReportId: this.selectedImportRef.reportId,
        sampleCode: this.selectedImportRef.sampleCode,
        productionReferenceNumber: this.selectedImportRef.productionReferenceNumber,
        source: supplier,
        sourcePhone: phone || null,
        notes: notes || null,
        materialType: this.selectedImportRef.materialClassification || this.importMaterialType || 'MECHANICAL',
        externalReportedWeight: extW,
        internalLoadedWeight: loadedW,
        internalEmptyWeight: emptyW,
        internalWeighingTime: weighingTime ? new Date(weighingTime).toISOString() : null,
        weighbridgeReference: weighbridgeRef,
        weighingResponsibleEmployee: weighingEmp,
        discrepancyExplanation: discrepancyExpl,
        numberOfBatches: batchList.length,
        batches: batchList,
        batchesJson: JSON.stringify(batchList),
        items: batchList.map(b => ({
          rawMaterialId: this.selectedImportRef.rawMaterialId,
          quantity: b.quantity,
          notes: b.notes
        }))
      };

      const btnSubmit = document.getElementById('btn-import-submit');
      const spinner = document.getElementById('import-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageImport(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          this.showToast('success', `تم حفظ إذن الإضافة بنجاح برقم ${txNum} وتم إنشاء ${batchList.length} دفعات مخزنية بنجاح.`);

          // Prompt batch printing modal if batches returned
          const createdBatches = res.data.batches || [];
          if (createdBatches.length > 0) {
            this.openBatchPrintModal(createdBatches, res.data);
          } else {
            try {
              const bRes = await ChatApi.getStorageOperationBatches(res.data.id);
              if (bRes.success && Array.isArray(bRes.data) && bRes.data.length > 0) {
                this.openBatchPrintModal(bRes.data, res.data);
              }
            } catch (e) {
              console.warn('Could not auto-fetch batches for print:', e);
            }
          }

          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن الإضافة.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    async loadImportProductionReferences() {
      const selectEl = document.getElementById('import-prod-ref-select');
      if (!selectEl) return;
      selectEl.innerHTML = `<option value="">-- جاري فحص وتحميل العينات المطابقة المعتمدة من المعمل... --</option>`;
      try {
        const res = await ChatApi.getStorageCompliantReferences();
        if (res && res.success && Array.isArray(res.data)) {
          this.importEligibleReferences = res.data;
          if (res.data.length === 0) {
            selectEl.innerHTML = `<option value="">-- لا توجد عينات معملية مطابقة جاهزة للاستلام حالياً (الكميات مستلمة بالكامل أو غير معتمدة) --</option>`;
          } else {
            selectEl.innerHTML = `<option value="">-- اختر رقم العينة / المرجع المعملي المطابق للاستلام --</option>` +
              res.data.map(r => {
                const classLabel = r.materialClassification === 'CHEMICAL' ? 'كيميائي' : 'ميكانيكي';
                const sampleLabel = r.sampleCode ? `عينة [${r.sampleCode}]` : `مرجع [#${r.productionReferenceNumber}]`;
                return `<option value="${r.sampleCode || r.productionReferenceNumber}">` +
                  `${sampleLabel} | ${this.escapeHtml(r.materialName)} (${classLabel}) - متاح: ${Number(r.remainingAvailableQuantity).toFixed(3)} طن` +
                  `</option>`;
              }).join('');
          }
        } else {
          selectEl.innerHTML = `<option value="">-- تعذر تحميل العينات المطابقة --</option>`;
        }
      } catch (e) {
        console.warn('Error loading compliant references for storage import:', e);
        selectEl.innerHTML = `<option value="">-- تعذر تحميل العينات المطابقة --</option>`;
      }
    }

    async onImportProductionReferenceChanged(val) {
      const infoEl = document.getElementById('import-prod-ref-info');
      const supplierInput = document.getElementById('import-supplier-select');
      const matRadios = document.querySelectorAll('input[name="import-material-type"]');

      if (!val) {
        this.selectedImportRef = null;
        if (infoEl) infoEl.style.display = 'none';
        if (supplierInput) supplierInput.disabled = false;
        matRadios.forEach(r => r.disabled = false);
        this.importBatches = [];
        this.renderImportBatches();
        this.updateImportBatchesTotal();
        return;
      }

      const ref = this.importEligibleReferences.find(r => 
        (r.sampleCode && r.sampleCode.toString() === val.toString()) || 
        (r.productionReferenceNumber && r.productionReferenceNumber.toString() === val.toString())
      );

      if (!ref) {
        this.selectedImportRef = null;
        if (infoEl) infoEl.style.display = 'none';
        if (supplierInput) supplierInput.disabled = false;
        matRadios.forEach(r => r.disabled = false);
        this.importBatches = [];
        this.renderImportBatches();
        this.updateImportBatchesTotal();
        return;
      }

      this.selectedImportRef = ref;
      const isChem = ref.materialClassification === 'CHEMICAL';
      const classText = isChem ? 'كيميائي' : 'ميكانيكي';
      const classBg = isChem ? '#1E40AF' : '#047857';
      const approvedQty = Number(ref.approvedQuantity ?? ref.acceptedQuantity ?? 0).toFixed(3);
      const remQty = Number(ref.remainingAvailableQuantity ?? 0).toFixed(3);

      const notesHtml = ref.notes ? `
        <div style="grid-column: 1 / -1; background: #FEF9C3; color: #854D0E; padding: 6px 12px; border-radius: 6px; border: 1px solid #FDE047; margin-top: 4px;">
          <strong>📝 ملاحظات الفحص المعملي:</strong> ${this.escapeHtml(ref.notes)}
        </div>
      ` : '';

      const attachHtml = ref.attachmentPath ? `
        <div style="grid-column: 1 / -1; margin-top: 4px; padding: 4px 8px; background: #F1F5F9; border-radius: 6px;">
          <strong>📎 مرفق تقرير المعمل:</strong> 
          <a href="${this.escapeHtml(ref.attachmentPath)}" target="_blank" rel="noopener noreferrer" style="color: #2563EB; font-weight: 700; text-decoration: underline; margin-right: 6px;">
            ${this.escapeHtml(ref.attachmentFileName || 'عرض المستند المرفق')}
          </a>
        </div>
      ` : '';

      if (infoEl) {
        infoEl.innerHTML = `
          <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px;">
            <div style="display:flex; align-items:center; gap:10px;">
              <span style="font-size:1.05rem; font-weight:800; color:#065F46;">🔬 رقم العينة: ${this.escapeHtml(ref.sampleCode || '-')}</span>
              <span style="background:#E2E8F0; color:#334155; border-radius:4px; padding:2px 8px; font-size:0.8rem; font-weight:700;">المرجع: #${ref.productionReferenceNumber}</span>
            </div>
            <span style="background:${classBg}; color:#fff; border-radius:6px; padding:3px 10px; font-size:0.75rem; font-weight:700;">
              مطابق للمواصفات (${classText}) - معتمد ومقروء آلياً
            </span>
          </div>
          <div style="margin-top:8px; display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap:8px; font-size:0.82rem;">
            <div>اسم المادة الخام: <strong style="color:#0F172A;">${this.escapeHtml(ref.materialName)}</strong></div>
            <div>تصنيف المادة: <strong>${classText}</strong></div>
            <div>المورد / المصدر: <strong>${this.escapeHtml(ref.supplierName || 'داخلي')}</strong></div>
            <div>الكمية المعتمدة بالمعمل: <strong>${approvedQty} طن</strong></div>
            <div>الرصيد المتاح المتبقي للتوريد: <strong style="color:#059669; font-size:1.05rem;">${remQty} طن</strong></div>
            ${notesHtml}
            ${attachHtml}
          </div>
        `;
        infoEl.style.display = 'block';
      }

      // Auto-fill and lock supplier (prevent tampering)
      if (supplierInput && ref.supplierName) {
        let optExists = Array.from(supplierInput.options).some(o => o.value === ref.supplierName);
        if (!optExists) {
          const opt = document.createElement('option');
          opt.value = ref.supplierName;
          opt.textContent = ref.supplierName;
          supplierInput.appendChild(opt);
        }
        supplierInput.value = ref.supplierName;
        supplierInput.disabled = true;
      }

      // Auto-fill external weight with remaining available quantity
      const extWeightInput = document.getElementById('import-ext-weight');
      if (extWeightInput && (!extWeightInput.value || extWeightInput.value === '')) {
        extWeightInput.value = remQty;
      }

      // Lock classification
      const radio = document.querySelector(`input[name="import-material-type"][value="${ref.materialClassification}"]`);
      if (radio) {
        radio.checked = true;
      }
      matRadios.forEach(r => r.disabled = true);
      this.importMaterialType = ref.materialClassification;

      // Generate batches (default: 1 batch)
      const countInput = document.getElementById('import-batch-count');
      const count = countInput ? Math.max(1, parseInt(countInput.value, 10) || 1) : 1;
      await this.generateImportBatches(count);
    }

    async generateImportBatches(count) {
      if (!this.selectedImportRef) {
        this.importBatches = [];
        this.renderImportBatches();
        this.updateImportBatchesTotal();
        return;
      }

      const clampedCount = Math.max(1, Math.min(50, count));
      
      // Get preview batch numbers from backend
      let previewNumbers = [];
      try {
        const res = await ChatApi.getStoragePreviewBatchNumbers(clampedCount);
        if (res && res.success && Array.isArray(res.data)) {
          previewNumbers = res.data;
        }
      } catch (e) {
        console.warn('Could not preview batch numbers from backend, using fallback:', e);
      }

      // Fallback sequential batch numbers
      while (previewNumbers.length < clampedCount) {
        const nextNum = previewNumbers.length + 1;
        previewNumbers.push(`B-${String(nextNum).padStart(4, '0')}`);
      }

      // Preserve existing user-entered quantities if available
      const existing = this.importBatches || [];
      const newBatches = [];

      for (let i = 0; i < clampedCount; i++) {
        const batchNum = previewNumbers[i];
        const oldQty = existing[i] ? existing[i].quantity : 0;
        const oldNotes = existing[i] ? existing[i].notes : '';
        newBatches.push({
          batchNumber: batchNum,
          quantity: oldQty,
          notes: oldNotes
        });
      }

      this.importBatches = newBatches;

      // If all quantities are 0 or count changed, distribute equally
      const allZero = this.importBatches.every(b => !b.quantity || b.quantity <= 0);
      if (allZero) {
        this.distributeImportBatchesEqually(false);
      }

      this.renderImportBatches();
      this.updateImportBatchesTotal();
    }

    distributeImportBatchesEqually(shouldRender = true) {
      if (!this.selectedImportRef || !this.importBatches || this.importBatches.length === 0) return;

      const total = Number(this.selectedImportRef.remainingAvailableQuantity) || 0;
      const count = this.importBatches.length;
      if (count === 0 || total <= 0) return;

      const baseQty = Math.floor((total / count) * 1000) / 1000;
      const distributedSoFar = baseQty * (count - 1);
      const lastQty = Math.round((total - distributedSoFar) * 1000) / 1000;

      for (let i = 0; i < count; i++) {
        this.importBatches[i].quantity = (i === count - 1) ? lastQty : baseQty;
      }

      if (shouldRender) {
        this.renderImportBatches();
        this.updateImportBatchesTotal();
      }
    }

    renderImportBatches() {
      const tbody = document.getElementById('import-batches-tbody');
      if (!tbody) return;

      if (!this.selectedImportRef) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; padding:1.5rem; color:#94A3B8;">يرجى اختيار رقم العينة / المرجع المعملي أولاً</td></tr>`;
        return;
      }

      if (this.importBatches.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد دفعات محددة. أدخل عدد الدفعات أعلاه.</td></tr>`;
        return;
      }

      const matName = this.escapeHtml(this.selectedImportRef.materialName);
      const isChem = this.selectedImportRef.materialClassification === 'CHEMICAL';
      const classBadge = isChem 
        ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
        : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

      tbody.innerHTML = this.importBatches.map((b, idx) => {
        return `
          <tr data-batch-idx="${idx}">
            <td style="text-align:center; font-weight:700; color:#64748B;">${idx + 1}</td>
            <td style="text-align:center;">
              <span class="storage-seq-badge" style="background:#ECFDF5; color:#065F46; border:1px solid #A7F3D0; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                ${b.batchNumber}
              </span>
            </td>
            <td>
              <div style="display:flex; align-items:center; gap:8px;">
                <strong>${matName}</strong>
                ${classBadge}
              </div>
            </td>
            <td>
              <div style="position:relative; max-width:140px;">
                <input type="number" step="0.001" min="0.001" 
                  class="storage-input batch-qty-input" 
                  data-idx="${idx}" 
                  value="${b.quantity > 0 ? Number(b.quantity).toFixed(3) : ''}" 
                  placeholder="0.000" 
                  style="text-align:center; font-weight:800; color:#059669; padding-right:8px;" required />
              </div>
            </td>
            <td>
              <input type="text" 
                class="storage-input batch-notes-input" 
                data-idx="${idx}" 
                value="${this.escapeHtml(b.notes || '')}" 
                placeholder="ملاحظات تشغيلية..." 
                style="font-size:0.85rem;" />
            </td>
            <td style="text-align:center;">
              <span class="badge" style="background:#F1F5F9; color:#475569; font-size:0.75rem; padding:3px 8px; border-radius:4px;">جديدة</span>
            </td>
          </tr>
        `;
      }).join('');

      // Attach event listeners to quantity & notes inputs
      tbody.querySelectorAll('.batch-qty-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const idx = parseInt(e.target.getAttribute('data-idx'), 10);
          const val = parseFloat(e.target.value) || 0;
          if (this.importBatches[idx]) {
            this.importBatches[idx].quantity = val;
          }
          this.updateImportBatchesTotal();
        });
      });

      tbody.querySelectorAll('.batch-notes-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const idx = parseInt(e.target.getAttribute('data-idx'), 10);
          if (this.importBatches[idx]) {
            this.importBatches[idx].notes = e.target.value.trim();
          }
        });
      });
    }

    updateImportBatchesTotal() {
      const sumEl = document.getElementById('import-batches-sum');
      const availEl = document.getElementById('import-batches-available');
      const badgeEl = document.getElementById('import-batches-balance-badge');
      const totalQtyBadge = document.getElementById('import-total-qty-badge');
      const totalItemsBadge = document.getElementById('import-total-items-badge');

      let sumQty = 0;
      if (this.importBatches && this.importBatches.length > 0) {
        sumQty = this.importBatches.reduce((acc, b) => acc + (parseFloat(b.quantity) || 0), 0);
      }

      const available = this.selectedImportRef ? Number(this.selectedImportRef.remainingAvailableQuantity) : 0;
      const diff = Math.round((available - sumQty) * 1000) / 1000;

      if (sumEl) sumEl.textContent = `${sumQty.toFixed(3)} طن`;
      if (availEl) availEl.textContent = `${available.toFixed(3)} طن`;
      if (totalQtyBadge) totalQtyBadge.textContent = `${sumQty.toFixed(3)} طن`;
      if (totalItemsBadge) totalItemsBadge.textContent = this.importBatches ? this.importBatches.length : 0;

      if (badgeEl) {
        if (!this.selectedImportRef) {
          badgeEl.style.background = '#E2E8F0';
          badgeEl.style.color = '#475569';
          badgeEl.textContent = 'بانتظار تحديد العينة وتوزيع الدفعات';
        } else if (Math.abs(diff) < 0.0005) {
          badgeEl.style.background = '#D1FAE5';
          badgeEl.style.color = '#065F46';
          badgeEl.innerHTML = `✅ تم توزيع كامل الكمية المتاحة بالتطابق (0.000 طن)`;
        } else if (diff > 0.0005) {
          badgeEl.style.background = '#EFF6FF';
          badgeEl.style.color = '#1E40AF';
          badgeEl.innerHTML = `ℹ️ متبقي غير موزع من العينة: <strong>${diff.toFixed(3)} طن</strong> (يمكن استلامه لاحقاً)`;
        } else {
          badgeEl.style.background = '#FEE2E2';
          badgeEl.style.color = '#991B1B';
          badgeEl.innerHTML = `⚠️ خطأ: مجموع الدفعات (${sumQty.toFixed(3)} طن) يتجاوز المتاح بمقدار <strong>${Math.abs(diff).toFixed(3)} طن</strong>!`;
        }
      }
    }

    // =========================================================================
    // VIEW 3: EXPORT FORM LOGIC (صادر - ربط برقم العينة والدفعات)
    // =========================================================================

    async prepareExportForm() {
      await this.loadMetadata();
      this.exportSelectedMaterials.clear();
      this.exportAvailableBatches = [];
      this.exportSelectedBatches.clear();

      // Sequence
      const seqEl = document.getElementById('export-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.export}`;

      // Datetime
      const dtInput = document.getElementById('export-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Locations
      const locSelect = document.getElementById('export-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين المصدر --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      // Clients
      const cliSelect = document.getElementById('export-client-select');
      if (cliSelect) {
        cliSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل المعتمد --</option>` +
          this.clients.map(c => `<option value="${c.id}">${this.escapeHtml(c.name)}</option>`).join('');
      }

      // Destinations reset
      const destSelect = document.getElementById('export-destination-select');
      if (destSelect) {
        destSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل أولاً --</option>`;
      }

      // Phone
      const phoneInput = document.getElementById('export-phone');
      if (phoneInput) phoneInput.value = '';

      // Reset selection mode to BY_REFERENCE
      const radRef = document.getElementById('export-mode-reference');
      if (radRef) radRef.checked = true;
      this.switchExportSelectionMode('BY_REFERENCE');

      // Reset ref select & batches tables
      const refSelect = document.getElementById('export-ref-select');
      if (refSelect) refSelect.innerHTML = `<option value="">-- اختر مكان التخزين المصدر أولاً --</option>`;
      const refWrap = document.getElementById('export-ref-batches-wrap');
      if (refWrap) refWrap.style.display = 'none';

      const refTbody = document.getElementById('export-ref-batches-tbody');
      if (refTbody) refTbody.innerHTML = '';
      const allTbody = document.getElementById('export-all-batches-tbody');
      if (allTbody) allTbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">اختر مكان التخزين المصدر لعرض الدفعات المتاحة</td></tr>`;

      this.updateExportBatchesTotal();
      this.clearAllErrors();
    }

    bindExportEvents() {
      // Client change listener
      const cliSelect = document.getElementById('export-client-select');
      const destSelect = document.getElementById('export-destination-select');
      const phoneInput = document.getElementById('export-phone');
      const destHint = document.getElementById('export-dest-hint');

      cliSelect?.addEventListener('change', (e) => {
        const cliId = parseInt(e.target.value, 10);
        const client = this.clients.find(c => c.id === cliId);

        if (!client) {
          if (destSelect) destSelect.innerHTML = `<option value="" disabled selected>-- اختر العميل أولاً --</option>`;
          return;
        }

        // Prefill phone if client has phone
        if (phoneInput && client.phoneNumber) {
          phoneInput.value = client.phoneNumber;
        }

        // Cascaded destinations
        if (destSelect) {
          if (client.destinations && client.destinations.length > 0) {
            destSelect.innerHTML = `<option value="" disabled selected>-- اختر الوجهة المعتمدة --</option>` +
              client.destinations.map(d => `<option value="${d.id}">${this.escapeHtml(d.destinationName || d.name)}</option>`).join('');
            if (destHint) destHint.textContent = `${client.destinations.length} وجهة معتمدة لهذا العميل`;
          } else {
            destSelect.innerHTML = `<option value="" disabled selected>-- لا توجد وجهات مسجلة لهذا العميل --</option>`;
            if (destHint) destHint.textContent = 'تنبيه: يجب إضافة وجهة للعميل من سجل العملاء';
          }
        }
      });

      // Export Category Type (Raw Material vs Finished Product)
      document.querySelectorAll('input[name="export-category-type"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
          this.switchExportCategoryType(e.target.value);
        });
      });

      // Finished Product Filter & Search
      document.getElementById('export-fin-order-filter')?.addEventListener('change', () => {
        this.renderAvailableFinishedProducts();
      });
      document.getElementById('export-fin-search')?.addEventListener('input', () => {
        this.renderAvailableFinishedProducts();
      });

      // Storage Location change -> reload materials stock, references, and batches
      const locSelect = document.getElementById('export-location-select');
      locSelect?.addEventListener('change', async (e) => {
        const locId = parseInt(e.target.value, 10);
        if (locId) {
          await this.onExportLocationChanged(locId);
        }
      });

      // Selection mode toggles (Mutually exclusive: Reference vs Batches)
      document.querySelectorAll('input[name="export-selection-mode"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
          this.switchExportSelectionMode(e.target.value);
        });
      });

      // Reference dropdown change
      document.getElementById('export-ref-select')?.addEventListener('change', (e) => {
        this.onExportReferenceChanged(e.target.value);
      });

      // Export weighing inputs live calculation
      ['export-loaded-weight', 'export-empty-weight', 'export-ext-weight'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', () => this.updateExportWeighingDisplay());
      });

      // Reset
      document.getElementById('btn-export-reset')?.addEventListener('click', () => {
        this.prepareExportForm();
      });

      // Submit
      document.getElementById('storage-export-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleExportSubmit();
      });
    }

    switchExportCategoryType(cat) {
      this.exportCategoryType = cat;
      const rawSec = document.getElementById('export-raw-material-section');
      const finSec = document.getElementById('export-finished-product-section');
      const rawLabel = document.getElementById('export-type-raw-label');
      const finLabel = document.getElementById('export-type-finished-label');

      if (cat === 'FINISHED_PRODUCT') {
        if (rawSec) rawSec.style.display = 'none';
        if (finSec) finSec.style.display = 'block';
        if (rawLabel) {
          rawLabel.classList.remove('is-checked');
          rawLabel.style.borderColor = '#CBD5E1';
        }
        if (finLabel) {
          finLabel.classList.add('is-checked');
          finLabel.style.borderColor = '#059669';
        }
        const locId = parseInt(document.getElementById('export-location-select')?.value, 10);
        if (locId) {
          this.loadAvailableFinishedProducts(locId);
        }
      } else {
        if (rawSec) rawSec.style.display = 'block';
        if (finSec) finSec.style.display = 'none';
        if (rawLabel) {
          rawLabel.classList.add('is-checked');
          rawLabel.style.borderColor = '#2563EB';
        }
        if (finLabel) {
          finLabel.classList.remove('is-checked');
          finLabel.style.borderColor = '#CBD5E1';
        }
      }
    }

    async loadAvailableFinishedProducts(locationId) {
      const tbody = document.getElementById('export-finished-products-tbody');
      const filterSelect = document.getElementById('export-fin-order-filter');
      if (!tbody) return;

      tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 2rem; color: #94A3B8;">جاري تحميل المنتجات النهائية المتاحة...</td></tr>';
      try {
        const res = await window.ChatApi.getAvailableFinishedProducts(locationId);
        if (res && res.success && Array.isArray(res.data)) {
          this.availableFinishedProducts = res.data;
          if (filterSelect) {
            const orders = [...new Set(res.data.map(p => p.productionOrderNumber).filter(Boolean))];
            filterSelect.innerHTML = '<option value="">-- كل أوامر الإنتاج --</option>' +
              orders.map(o => `<option value="${this.escapeHtml(o)}">${this.escapeHtml(o)}</option>`).join('');
          }
          this.renderAvailableFinishedProducts();
        } else {
          this.availableFinishedProducts = [];
          tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 2rem; color: #94A3B8;">لا توجد منتجات نهائية متوفرة بالمستودع المحدد.</td></tr>';
        }
      } catch (err) {
        console.error('Error loading available finished products:', err);
        tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 2rem; color: #EF4444;">فشل تحميل المنتجات النهائية.</td></tr>';
      }
    }

    renderAvailableFinishedProducts() {
      const tbody = document.getElementById('export-finished-products-tbody');
      if (!tbody) return;

      const orderFilter = document.getElementById('export-fin-order-filter')?.value || '';
      const search = (document.getElementById('export-fin-search')?.value || '').trim().toLowerCase();

      let filtered = this.availableFinishedProducts;
      if (orderFilter) {
        filtered = filtered.filter(p => p.productionOrderNumber === orderFilter);
      }
      if (search) {
        filtered = filtered.filter(p =>
          (p.productName && p.productName.toLowerCase().includes(search)) ||
          (p.manufactureNumber && p.manufactureNumber.toLowerCase().includes(search)) ||
          (p.batchNumber && p.batchNumber.toLowerCase().includes(search))
        );
      }

      if (filtered.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 2rem; color: #94A3B8;">لا توجد منتجات نهائية مطابقة للبحث أو الفلتر.</td></tr>';
        return;
      }

      tbody.innerHTML = filtered.map(p => {
        const isSel = this.exportFinSelectedProducts.has(p.warehouseBatchId);
        const selData = isSel ? this.exportFinSelectedProducts.get(p.warehouseBatchId) : null;
        const currentQty = selData ? selData.quantity : '';
        const rowBg = isSel ? '#F0FDF4' : 'transparent';

        return `
          <tr style="border-bottom: 1px solid #E2E8F0; background: ${rowBg};" data-fin-batch-id="${p.warehouseBatchId}">
            <td style="text-align: center; padding: 8px;">
              <input type="checkbox" class="export-fin-chk" data-fin-id="${p.warehouseBatchId}" ${isSel ? 'checked' : ''} style="width: 18px; height: 18px; cursor: pointer;" />
            </td>
            <td style="padding: 8px; font-weight: 700; color: #1E293B;">
              ${this.escapeHtml(p.manufactureNumber || p.batchNumber)}
            </td>
            <td style="padding: 8px; font-weight: 800; color: #15803D;">
              ${this.escapeHtml(p.productName)}
            </td>
            <td style="padding: 8px; color: #0284C7; font-weight: 600;">
              ${this.escapeHtml(p.productionOrderNumber || '—')}
            </td>
            <td style="padding: 8px; color: #7C3AED; font-weight: 600;">
              ${this.escapeHtml(p.processNumber || '—')}
            </td>
            <td style="padding: 8px; font-weight: 700; color: #0F172A;">
              ${Number(p.availableQuantity).toFixed(3)} ${this.escapeHtml(p.unit || 'طن')}
            </td>
            <td style="padding: 8px;">
              <input type="number" class="storage-input export-fin-qty-input" data-fin-id="${p.warehouseBatchId}" step="0.001" min="0.001" max="${p.availableQuantity}" placeholder="0.000" value="${currentQty}" ${isSel ? '' : 'disabled'} style="width: 100%; font-weight: 700; padding: 4px 8px; text-align: center; border: 1.5px solid ${isSel ? '#10B981' : '#CBD5E1'};" />
            </td>
          </tr>
        `;
      }).join('');

      tbody.querySelectorAll('.export-fin-chk').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const bId = parseInt(e.target.getAttribute('data-fin-id'), 10);
          const tr = tbody.querySelector(`tr[data-fin-batch-id="${bId}"]`);
          const qtyInp = tr?.querySelector('.export-fin-qty-input');
          const batch = this.availableFinishedProducts.find(x => x.warehouseBatchId === bId);
          if (!batch) return;

          if (e.target.checked) {
            if (tr) tr.style.background = '#F0FDF4';
            if (qtyInp) {
              qtyInp.disabled = false;
              if (!qtyInp.value) qtyInp.value = batch.availableQuantity;
              qtyInp.style.borderColor = '#10B981';
              qtyInp.focus();
            }
            this.exportFinSelectedProducts.set(bId, {
              batch,
              quantity: qtyInp?.value ? parseFloat(qtyInp.value) : batch.availableQuantity
            });
          } else {
            if (tr) tr.style.background = 'transparent';
            if (qtyInp) {
              qtyInp.disabled = true;
              qtyInp.value = '';
              qtyInp.style.borderColor = '#CBD5E1';
            }
            this.exportFinSelectedProducts.delete(bId);
          }
          this.updateFinishedExportTotals();
        });
      });

      tbody.querySelectorAll('.export-fin-qty-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const bId = parseInt(e.target.getAttribute('data-fin-id'), 10);
          const val = parseFloat(e.target.value) || 0;
          if (this.exportFinSelectedProducts.has(bId)) {
            this.exportFinSelectedProducts.get(bId).quantity = val;
          }
          this.updateFinishedExportTotals();
        });
      });

      this.updateFinishedExportTotals();
    }

    updateFinishedExportTotals() {
      const countEl = document.getElementById('export-fin-total-items-badge');
      const qtyEl = document.getElementById('export-fin-total-qty-badge');
      const statusEl = document.getElementById('export-fin-balance-status-badge');

      let totalQty = 0;
      this.exportFinSelectedProducts.forEach(item => {
        totalQty += (parseFloat(item.quantity) || 0);
      });

      if (countEl) countEl.textContent = this.exportFinSelectedProducts.size;
      if (qtyEl) qtyEl.textContent = `${totalQty.toFixed(3)} طن`;

      if (statusEl) {
        if (this.exportFinSelectedProducts.size > 0 && totalQty > 0) {
          statusEl.innerHTML = '<span style="font-size:0.8rem; font-weight:700; color:#15803D;">✓ تم تحديد الكميات بنجاح</span>';
        } else {
          statusEl.innerHTML = '<span style="font-size:0.8rem; color:#64748B;">لم يتم إدخال كميات</span>';
        }
      }
    }

    switchExportSelectionMode(mode) {
      this.exportSelectionMode = mode;
      const refContainer = document.getElementById('export-container-by-reference');
      const batchContainer = document.getElementById('export-container-by-batch');
      const refLabel = document.getElementById('export-mode-reference-label');
      const batchLabel = document.getElementById('export-mode-batch-label');

      if (mode === 'BY_REFERENCE') {
        if (refContainer) refContainer.style.display = 'block';
        if (batchContainer) batchContainer.style.display = 'none';
        if (refLabel) {
          refLabel.classList.add('is-checked');
          refLabel.style.borderColor = '#D97706';
        }
        if (batchLabel) {
          batchLabel.classList.remove('is-checked');
          batchLabel.style.borderColor = '#CBD5E1';
        }
      } else {
        if (refContainer) refContainer.style.display = 'none';
        if (batchContainer) batchContainer.style.display = 'block';
        if (refLabel) {
          refLabel.classList.remove('is-checked');
          refLabel.style.borderColor = '#CBD5E1';
        }
        if (batchLabel) {
          batchLabel.classList.add('is-checked');
          batchLabel.style.borderColor = '#D97706';
        }
      }

      this.exportSelectedBatches.clear();
      // Uncheck all checkboxes in tables
      document.querySelectorAll('#export-ref-batches-tbody .export-batch-chk, #export-all-batches-tbody .export-batch-chk').forEach(c => c.checked = false);
      document.querySelectorAll('#export-ref-batches-tbody .export-batch-qty-input, #export-all-batches-tbody .export-batch-qty-input').forEach(i => {
        i.disabled = true;
        i.value = '';
      });
      this.updateExportBatchesTotal();
    }

    async onExportLocationChanged(locationId) {
      if (!locationId) return;

      this.exportSelectedBatches.clear();
      this.updateExportBatchesTotal();

      if (this.exportCategoryType === 'FINISHED_PRODUCT') {
        await this.loadAvailableFinishedProducts(locationId);
      }

      const refSelect = document.getElementById('export-ref-select');
      const refWrap = document.getElementById('export-ref-batches-wrap');
      const refTbody = document.getElementById('export-ref-batches-tbody');
      const tbodyAll = document.getElementById('export-all-batches-tbody');

      if (refWrap) refWrap.style.display = 'none';
      if (refTbody) refTbody.innerHTML = '';

      if (refSelect) {
        refSelect.innerHTML = `<option value="">-- جاري تحميل المراجع المعملية المتاحة في المستودع... --</option>`;
      }
      if (tbodyAll) {
        tbodyAll.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">جاري تحميل الدفعات المتاحة...</td></tr>`;
      }

      // 1. Fetch available references in this warehouse
      let refs = [];
      try {
        const resRefs = await ChatApi.getStorageLocationAvailableReferences(locationId);
        if (resRefs && resRefs.success && Array.isArray(resRefs.data)) {
          refs = resRefs.data;
        }
      } catch (e) {
        console.warn('Error loading export available references:', e);
      }

      // 2. Fetch all available batches in this warehouse
      try {
        const resBatches = await ChatApi.getStorageLocationAvailableBatches(locationId);
        if (resBatches && resBatches.success && Array.isArray(resBatches.data)) {
          this.exportAvailableBatches = resBatches.data;
        } else {
          this.exportAvailableBatches = [];
        }
      } catch (e) {
        console.warn('Error loading export available batches:', e);
        this.exportAvailableBatches = [];
      }

      if (this.exportAvailableBatches.length === 0) {
        if (refSelect) {
          refSelect.innerHTML = `<option value="">-- لا توجد مواد متاحة في هذا المخزن حالياً --</option>`;
        }
        if (tbodyAll) {
          tbodyAll.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:2rem; color:#EF4444; font-weight:700;">⚠️ لا توجد مواد متاحة في هذا المخزن حالياً</td></tr>`;
        }
      } else {
        if (refSelect) {
          if (refs.length === 0) {
            refSelect.innerHTML = `<option value="">-- لا توجد مراجع معملية متاحة برصيد في هذا المخزن --</option>`;
          } else {
            refSelect.innerHTML = `<option value="">-- اختر رقم العينة / المرجع المعملي للصرف --</option>` +
              refs.map(r => `
                <option value="${r.productionReferenceNumber}" data-lab-id="${r.reportId || r.laboratoryReportId}" data-sample-code="${this.escapeHtml(r.sampleCode || '')}">
                  مرجع: #${r.productionReferenceNumber} | عينة: [${this.escapeHtml(r.sampleCode || '-')}] - ${this.escapeHtml(r.materialName)} (رصيد: ${Number(r.remainingAvailableQuantity).toFixed(3)} طن)
                </option>
              `).join('');
          }
        }
        this.renderExportDirectBatchesTable();
      }

      this.updateExportBatchesTotal();
    }

    onExportReferenceChanged(refNo) {
      const wrap = document.getElementById('export-ref-batches-wrap');
      const tbody = document.getElementById('export-ref-batches-tbody');
      if (!refNo) {
        if (wrap) wrap.style.display = 'none';
        if (tbody) tbody.innerHTML = '';
        this.exportSelectedBatches.clear();
        this.updateExportBatchesTotal();
        return;
      }

      const matchingBatches = this.exportAvailableBatches.filter(b => 
        b.productionReferenceNumber === parseInt(refNo, 10)
      );

      if (wrap) wrap.style.display = 'block';
      if (!tbody) return;

      if (matchingBatches.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد دفعات نشطة متاحة لهذا المرجع في هذا المستودع.</td></tr>`;
        return;
      }

      tbody.innerHTML = matchingBatches.map(b => {
        const batchId = b.id || b.batchId;
        const curQty = b.currentQuantity ?? b.availableQuantity ?? 0;
        const isChecked = this.exportSelectedBatches.has(batchId);
        const selQty = isChecked ? this.exportSelectedBatches.get(batchId).quantity : '';
        const classBadge = b.materialClassification === 'CHEMICAL'
          ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
          : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

        return `
          <tr data-batch-id="${batchId}">
            <td style="text-align:center;">
              <input type="checkbox" class="export-batch-chk" value="${batchId}" ${isChecked ? 'checked' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="storage-seq-badge" style="background:#FEF3C7; color:#B45309; border:1px solid #FCD34D; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                ${this.escapeHtml(b.batchNumber)}
              </span>
            </td>
            <td>
              <div style="display:flex; align-items:center; gap:8px;">
                <strong>${this.escapeHtml(b.materialName)}</strong>
                ${classBadge}
              </div>
            </td>
            <td style="text-align:center; font-weight:700; color:#059669;">
              ${Number(curQty).toFixed(3)} طن
            </td>
            <td>
              <input type="number" step="0.001" min="0.001" max="${curQty}" 
                class="storage-input export-batch-qty-input" 
                data-batch-id="${batchId}" 
                value="${selQty}" 
                placeholder="0.000" 
                style="text-align:center; font-weight:800; color:#B45309;" 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td>
              <input type="text" 
                class="storage-input export-batch-notes-input" 
                data-batch-id="${batchId}" 
                placeholder="ملاحظات..." 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="badge" style="background:#DCFCE7; color:#15803D; font-size:0.75rem; padding:3px 8px; border-radius:4px;">متاح</span>
            </td>
          </tr>
        `;
      }).join('');

      this.bindExportBatchRowEvents(tbody);
    }

    renderExportDirectBatchesTable() {
      const tbody = document.getElementById('export-all-batches-tbody');
      if (!tbody) return;

      if (!this.exportAvailableBatches || this.exportAvailableBatches.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:2rem; color:#EF4444; font-weight:700;">⚠️ لا توجد مواد متاحة في هذا المخزن حالياً</td></tr>`;
        return;
      }

      tbody.innerHTML = this.exportAvailableBatches.map(b => {
        const batchId = b.id || b.batchId;
        const curQty = b.currentQuantity ?? b.availableQuantity ?? 0;
        const isChecked = this.exportSelectedBatches.has(batchId);
        const selQty = isChecked ? this.exportSelectedBatches.get(batchId).quantity : '';
        const classBadge = b.materialClassification === 'CHEMICAL'
          ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
          : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

        return `
          <tr data-batch-id="${batchId}">
            <td style="text-align:center;">
              <input type="checkbox" class="export-batch-chk" value="${batchId}" ${isChecked ? 'checked' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="storage-seq-badge" style="background:#FEF3C7; color:#B45309; border:1px solid #FCD34D; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                ${this.escapeHtml(b.batchNumber)}
              </span>
            </td>
            <td style="font-size:0.85rem;">
              <div style="font-weight:700; color:#0F172A;">مرجع: #${b.productionReferenceNumber}</div>
              <div style="color:#64748B;">عينة: [${this.escapeHtml(b.sampleCode || '-')}]</div>
            </td>
            <td>
              <div style="display:flex; align-items:center; gap:8px;">
                <strong>${this.escapeHtml(b.materialName)}</strong>
                ${classBadge}
              </div>
            </td>
            <td style="text-align:center; font-weight:700; color:#059669;">
              ${Number(curQty).toFixed(3)} طن
            </td>
            <td>
              <input type="number" step="0.001" min="0.001" max="${curQty}" 
                class="storage-input export-batch-qty-input" 
                data-batch-id="${batchId}" 
                value="${selQty}" 
                placeholder="0.000" 
                style="text-align:center; font-weight:800; color:#B45309;" 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td>
              <input type="text" 
                class="storage-input export-batch-notes-input" 
                data-batch-id="${batchId}" 
                placeholder="ملاحظات..." 
                ${!isChecked ? 'disabled' : ''} />
            </td>
          </tr>
        `;
      }).join('');

      this.bindExportBatchRowEvents(tbody);
    }

    bindExportBatchRowEvents(container) {
      container.querySelectorAll('.export-batch-chk').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const batchId = parseInt(e.target.value, 10);
          const row = e.target.closest('tr');
          const qtyInp = row?.querySelector('.export-batch-qty-input');
          const notesInp = row?.querySelector('.export-batch-notes-input');
          const batch = this.exportAvailableBatches.find(b => (b.id === batchId || b.batchId === batchId));

          if (!batch) return;
          const curQty = batch.currentQuantity ?? batch.availableQuantity ?? 0;

          if (e.target.checked) {
            if (qtyInp) {
              qtyInp.disabled = false;
              if (!qtyInp.value || parseFloat(qtyInp.value) <= 0) {
                qtyInp.value = Number(curQty).toFixed(3);
              }
              qtyInp.focus();
            }
            if (notesInp) notesInp.disabled = false;
            this.exportSelectedBatches.set(batchId, {
              batch: batch,
              quantity: parseFloat(qtyInp?.value) || Number(curQty),
              notes: notesInp?.value || ''
            });
          } else {
            if (qtyInp) qtyInp.disabled = true;
            if (notesInp) notesInp.disabled = true;
            this.exportSelectedBatches.delete(batchId);
          }
          this.updateExportBatchesTotal();
        });
      });

      container.querySelectorAll('.export-batch-qty-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const batchId = parseInt(e.target.getAttribute('data-batch-id'), 10);
          const batch = this.exportAvailableBatches.find(b => (b.id === batchId || b.batchId === batchId));
          if (!batch) return;
          const curQty = batch.currentQuantity ?? batch.availableQuantity ?? 0;

          let val = parseFloat(e.target.value) || 0;
          if (val > curQty) {
            e.target.style.borderColor = '#DC2626';
          } else {
            e.target.style.borderColor = '';
          }

          if (this.exportSelectedBatches.has(batchId)) {
            this.exportSelectedBatches.get(batchId).quantity = val;
          }
          this.updateExportBatchesTotal();
        });
      });

      container.querySelectorAll('.export-batch-notes-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const batchId = parseInt(e.target.getAttribute('data-batch-id'), 10);
          if (this.exportSelectedBatches.has(batchId)) {
            this.exportSelectedBatches.get(batchId).notes = e.target.value.trim();
          }
        });
      });
    }

    updateExportBatchesTotal() {
      const itemsBadge = document.getElementById('export-total-items-badge');
      const qtyBadge = document.getElementById('export-total-qty-badge');
      const statusBadge = document.getElementById('export-balance-status-badge');

      let totalQty = 0;
      let hasOverQty = false;

      this.exportSelectedBatches.forEach(sel => {
        totalQty += (sel.quantity || 0);
        if (sel.quantity > sel.batch.currentQuantity) {
          hasOverQty = true;
        }
      });

      if (itemsBadge) itemsBadge.textContent = this.exportSelectedBatches.size;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (statusBadge) {
        if (this.exportSelectedBatches.size === 0) {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#E2E8F0; color:#475569;">بانتظار تحديد الدفعات</span>`;
        } else if (hasOverQty) {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#FEE2E2; color:#991B1B;">⚠️ خطأ: توجد كمية مدخلة تتجاوز رصيد الدفعة!</span>`;
        } else {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#FEF3C7; color:#B45309;">جاهز للصرف (${this.exportSelectedBatches.size} دفعات)</span>`;
        }
      }
    }

    updateExportWeighingDisplay() {
      const loaded = parseFloat(document.getElementById('export-loaded-weight')?.value) || 0;
      const empty = parseFloat(document.getElementById('export-empty-weight')?.value) || 0;
      const ext = parseFloat(document.getElementById('export-ext-weight')?.value) || 0;

      const net = loaded - empty;
      const resultBox = document.getElementById('export-weighing-result');
      const warnBox = document.getElementById('export-discrepancy-warning');

      if (loaded > 0 && empty > 0) {
        const diff = Math.abs(net - ext);
        const pct = ext > 0 ? (diff / ext) * 100 : 0;

        const netEl = document.getElementById('export-net-weight-display');
        const diffEl = document.getElementById('export-weight-diff-display');
        const pctEl = document.getElementById('export-weight-pct-display');

        if (netEl) netEl.textContent = `${net.toFixed(3)} طن`;
        if (diffEl) diffEl.textContent = `${diff.toFixed(3)} طن`;
        if (pctEl) pctEl.textContent = `${pct.toFixed(2)} %`;

        if (resultBox) resultBox.style.display = 'block';

        const flagged = pct > 0.5 || diff > 0.05;
        if (warnBox) warnBox.style.display = flagged ? 'block' : 'none';
      } else {
        if (resultBox) resultBox.style.display = 'none';
        if (warnBox) warnBox.style.display = 'none';
      }
    }

    async reloadMaterialStockForLocation(locationId) {
      try {
        const res = await ChatApi.getStorageMaterialsStock(locationId);
        if (res.success && Array.isArray(res.data)) {
          this.materials = res.data;
        }
      } catch (e) {
        console.error('Failed reloading stock for location:', e);
      }
    }

    renderExportChecklist() {
      // Preserved for backward compatibility
    }

    renderExportQuantitiesTable() {
      // Preserved for backward compatibility
    }

    async handleExportSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      const locationId = parseInt(document.getElementById('export-location-select')?.value, 10);
      const clientId = parseInt(document.getElementById('export-client-select')?.value, 10);
      const destinationId = parseInt(document.getElementById('export-destination-select')?.value, 10);
      const phone = document.getElementById('export-phone')?.value?.trim();
      const dtVal = document.getElementById('export-datetime')?.value;
      const notes = document.getElementById('export-notes')?.value?.trim();

      let hasError = false;

      if (!dtVal) {
        this.setFieldError('export-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('export-location-select', 'مكان التخزين مطلوب.');
        hasError = true;
      }
      if (!clientId || isNaN(clientId)) {
        this.setFieldError('export-client-select', 'العميل مطلوب.');
        hasError = true;
      }
      if (!destinationId || isNaN(destinationId)) {
        this.setFieldError('export-destination-select', 'الوجهة المعتمدة مطلوبة.');
        hasError = true;
      }

      const isFinished = this.exportCategoryType === 'FINISHED_PRODUCT';
      const items = [];

      if (isFinished) {
        if (this.exportFinSelectedProducts.size === 0) {
          this.showToast('error', 'يرجى تحديد منتج نهائي واحد على الأقل وتحديد الكمية المراد صرفها.');
          return;
        }

        for (const [batchId, sel] of this.exportFinSelectedProducts) {
          const qty = parseFloat(sel.quantity);
          if (isNaN(qty) || qty <= 0) {
            this.showToast('error', `المنتج النهائي '${sel.batch.materialName}' يجب أن تكون كميته أكبر من الصفر.`);
            return;
          }
          if (qty > sel.batch.availableQuantity) {
            this.showToast('error', `الكمية المصروفة للمنتج '${sel.batch.materialName}' (${qty.toFixed(3)} طن) تتجاوز الرصيد المتاح بالمخزن (${Number(sel.batch.availableQuantity).toFixed(3)} طن).`);
            return;
          }
          items.push({
            warehouseBatchId: batchId,
            rawMaterialId: sel.batch.rawMaterialId,
            quantity: qty,
            productionOrderId: sel.batch.productionOrderId || null,
            productionProcessId: sel.batch.productionProcessId || null,
            manufactureNumber: sel.batch.manufactureNumber || sel.batch.batchNumber || null,
            notes: sel.notes || null
          });
        }
      } else {
        if (this.exportSelectedBatches.size === 0) {
          this.showToast('error', 'يرجى تحديد دفعة واحدة على الأقل للصرف.');
          return;
        }

        for (const [batchId, sel] of this.exportSelectedBatches) {
          const qty = parseFloat(sel.quantity);
          if (isNaN(qty) || qty <= 0) {
            this.showToast('error', `الدفعة '${sel.batch.batchNumber}' يجب أن تكون كميتها أكبر من الصفر.`);
            return;
          }
          if (qty > sel.batch.currentQuantity) {
            this.showToast('error', `الكمية المصروفة للدفعة '${sel.batch.batchNumber}' (${qty.toFixed(3)} طن) تتجاوز رصيدها المتاح (${Number(sel.batch.currentQuantity).toFixed(3)} طن).`);
            return;
          }
          items.push({
            warehouseBatchId: batchId,
            rawMaterialId: sel.batch.rawMaterialId,
            quantity: qty,
            notes: sel.notes || null
          });
        }
      }

      // Validate discrepancy explanation if warning is shown
      const warnBox = document.getElementById('export-discrepancy-warning');
      if (warnBox && warnBox.style.display !== 'none') {
        const expl = document.getElementById('export-discrepancy-explanation')?.value?.trim();
        if (!expl) {
          this.setFieldError('export-discrepancy-explanation', 'تفسير الفرق في الوزن مطلوب عند وجود فرق يتجاوز 0.5%.');
          hasError = true;
        }
      }

      if (hasError) return;

      // Build weighing payload
      const loadedW = parseFloat(document.getElementById('export-loaded-weight')?.value) || null;
      const emptyW = parseFloat(document.getElementById('export-empty-weight')?.value) || null;
      const extW = parseFloat(document.getElementById('export-ext-weight')?.value) || null;
      const weighingTime = document.getElementById('export-weighing-time')?.value || null;
      const weighbridgeRef = document.getElementById('export-weighbridge-ref')?.value?.trim() || null;
      const weighingEmp = document.getElementById('export-weighing-emp')?.value?.trim() || null;
      const discrepancyExpl = document.getElementById('export-discrepancy-explanation')?.value?.trim() || null;

      // Primary reference if in reference mode
      let refNo = null;
      let labReportId = null;
      let sampleCd = null;
      if (!isFinished && this.exportSelectionMode === 'BY_REFERENCE') {
        const refSelect = document.getElementById('export-ref-select');
        const selectedVal = refSelect?.value;
        if (selectedVal) {
          refNo = parseInt(selectedVal, 10);
          const opt = refSelect.options[refSelect.selectedIndex];
          if (opt) {
            const labIdAttr = opt.getAttribute('data-lab-id');
            if (labIdAttr) labReportId = parseInt(labIdAttr, 10);
            sampleCd = opt.getAttribute('data-sample-code') || null;
          }
          const firstBatch = this.exportSelectedBatches.values().next().value?.batch;
          if (firstBatch) {
            labReportId = labReportId || firstBatch.laboratoryReportId || firstBatch.reportId;
            sampleCd = sampleCd || firstBatch.sampleCode;
          }
        }
      }

      let poId = null;
      let procId = null;
      let mfgNo = null;
      if (isFinished && items.length > 0) {
        poId = items[0].productionOrderId;
        procId = items[0].productionProcessId;
        mfgNo = items[0].manufactureNumber;
      }

      const payload = {
        exportType: isFinished ? 'FINISHED_PRODUCT' : 'RAW_MATERIAL',
        productionOrderId: poId,
        productionProcessId: procId,
        manufactureNumber: mfgNo,
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        clientId: clientId,
        destinationId: destinationId,
        clientPhone: phone || null,
        notes: notes || null,
        selectionMode: isFinished ? 'BY_BATCH' : this.exportSelectionMode,
        productionReferenceNumber: isFinished ? null : refNo,
        laboratoryReportId: isFinished ? null : labReportId,
        sampleCode: isFinished ? null : sampleCd,
        externalReportedWeight: extW,
        internalLoadedWeight: loadedW,
        internalEmptyWeight: emptyW,
        internalWeighingTime: weighingTime ? new Date(weighingTime).toISOString() : null,
        weighbridgeReference: weighbridgeRef,
        weighingResponsibleEmployee: weighingEmp,
        discrepancyExplanation: discrepancyExpl,
        items: items,
        itemsJson: JSON.stringify(items)
      };

      const btnSubmit = document.getElementById('btn-export-submit');
      const spinner = document.getElementById('export-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageExport(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          this.showToast('success', `تم حفظ إذن الصرف برقم ${txNum} بنجاح، بانتظار اعتماد قسم التحكم والمراقبة.`);
          this.exportSelectedBatches.clear();
          this.exportFinSelectedProducts.clear();
          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن الصرف.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    // =========================================================================
    // VIEW 4: TRANSFER FORM LOGIC (تحويل للإنتاج - ربط برقم العينة والدفعات)
    // =========================================================================

async prepareTransferForm() {
      await this.loadMetadata();
      this.transferSelectedMaterials.clear();
      this.transferAvailableBatches = [];
      this.transferSelectedBatches.clear();

      // Sequence
      const seqEl = document.getElementById('transfer-seq-preview');
      if (seqEl) seqEl.textContent = `#${this.nextSequences.transfer}`;

      // Datetime
      const dtInput = document.getElementById('transfer-datetime');
      if (dtInput) dtInput.value = this.formatLocalDateTimeForInput();

      // Locations
      const locSelect = document.getElementById('transfer-location-select');
      if (locSelect) {
        locSelect.innerHTML = `<option value="" disabled selected>-- اختر مكان التخزين المصدر --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      // Reset Destination Type to PRODUCTION
      const destTypeSelect = document.getElementById('transfer-destination-type');
      if (destTypeSelect) destTypeSelect.value = 'PRODUCTION';
      const destWrap = document.getElementById('transfer-dest-warehouse-wrap');
      if (destWrap) destWrap.style.display = 'none';
      this.updateTransferDestWarehouseOptions();

      // Reset mode to BY_REFERENCE
      const radRef = document.getElementById('transfer-mode-reference');
      if (radRef) radRef.checked = true;
      this.switchTransferSelectionMode('BY_REFERENCE');

      // Reset tables
      const refSelect = document.getElementById('transfer-ref-select');
      if (refSelect) refSelect.innerHTML = `<option value="">-- اختر مكان التخزين المصدر أولاً --</option>`;
      const refWrap = document.getElementById('transfer-ref-batches-wrap');
      if (refWrap) refWrap.style.display = 'none';

      const refTbody = document.getElementById('transfer-ref-batches-tbody');
      if (refTbody) refTbody.innerHTML = '';
      const allTbody = document.getElementById('transfer-all-batches-tbody');
      if (allTbody) allTbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">اختر مكان التخزين المصدر لعرض الدفعات المتاحة للتحويل</td></tr>`;

      this.updateTransferBatchesTotal();
      this.clearAllErrors();
    }

    bindTransferEvents() {
      // Source Location change
      const locSelect = document.getElementById('transfer-location-select');
      locSelect?.addEventListener('change', async (e) => {
        const locId = parseInt(e.target.value, 10);
        if (locId) {
          await this.onTransferLocationChanged(locId);
        }
      });

      // Destination Type change (Production vs Warehouse)
      const destTypeSelect = document.getElementById('transfer-destination-type');
      destTypeSelect?.addEventListener('change', (e) => {
        const isWh = e.target.value === 'WAREHOUSE';
        const wrap = document.getElementById('transfer-dest-warehouse-wrap');
        if (wrap) wrap.style.display = isWh ? 'block' : 'none';
        this.updateTransferDestWarehouseOptions();
      });

      // Selection mode toggles
      document.querySelectorAll('input[name="transfer-selection-mode"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
          this.switchTransferSelectionMode(e.target.value);
        });
      });

      // Reference dropdown change
      document.getElementById('transfer-ref-select')?.addEventListener('change', (e) => {
        this.onTransferReferenceChanged(e.target.value);
      });

      document.getElementById('btn-transfer-reset')?.addEventListener('click', () => {
        this.prepareTransferForm();
      });

      document.getElementById('storage-transfer-form')?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleTransferSubmit();
      });
    }

    updateTransferDestWarehouseOptions() {
      const destSelect = document.getElementById('transfer-dest-warehouse-select');
      const sourceLocId = parseInt(document.getElementById('transfer-location-select')?.value, 10);
      if (!destSelect) return;

      const curVal = destSelect.value;
      destSelect.innerHTML = '<option value="" disabled selected>-- اختر مستودع الوجهة --</option>' +
        this.locations
          .filter(loc => !sourceLocId || loc.id !== sourceLocId)
          .map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`)
          .join('');

      if (curVal && parseInt(curVal, 10) !== sourceLocId) {
        destSelect.value = curVal;
      }
    }

    switchTransferSelectionMode(mode) {
      this.transferSelectionMode = mode;
      const refContainer = document.getElementById('transfer-container-by-reference');
      const batchContainer = document.getElementById('transfer-container-by-batch');
      const refLabel = document.getElementById('transfer-mode-reference-label');
      const batchLabel = document.getElementById('transfer-mode-batch-label');

      if (mode === 'BY_REFERENCE') {
        if (refContainer) refContainer.style.display = 'block';
        if (batchContainer) batchContainer.style.display = 'none';
        if (refLabel) {
          refLabel.classList.add('is-checked');
          refLabel.style.borderColor = '#2563EB';
        }
        if (batchLabel) {
          batchLabel.classList.remove('is-checked');
          batchLabel.style.borderColor = '#CBD5E1';
        }
      } else {
        if (refContainer) refContainer.style.display = 'none';
        if (batchContainer) batchContainer.style.display = 'block';
        if (refLabel) {
          refLabel.classList.remove('is-checked');
          refLabel.style.borderColor = '#CBD5E1';
        }
        if (batchLabel) {
          batchLabel.classList.add('is-checked');
          batchLabel.style.borderColor = '#2563EB';
        }
      }

      this.transferSelectedBatches.clear();
      document.querySelectorAll('#transfer-ref-batches-tbody .transfer-batch-chk, #transfer-all-batches-tbody .transfer-batch-chk').forEach(c => c.checked = false);
      document.querySelectorAll('#transfer-ref-batches-tbody .transfer-batch-qty-input, #transfer-all-batches-tbody .transfer-batch-qty-input').forEach(i => {
        i.disabled = true;
        i.value = '';
      });
      this.updateTransferBatchesTotal();
    }

    async onTransferLocationChanged(locationId) {
      if (!locationId) return;

      this.transferSelectedBatches.clear();
      this.updateTransferBatchesTotal();
      this.updateTransferDestWarehouseOptions();

      const refSelect = document.getElementById('transfer-ref-select');
      const refWrap = document.getElementById('transfer-ref-batches-wrap');
      const refTbody = document.getElementById('transfer-ref-batches-tbody');
      const tbodyAll = document.getElementById('transfer-all-batches-tbody');

      if (refWrap) refWrap.style.display = 'none';
      if (refTbody) refTbody.innerHTML = '';

      if (refSelect) {
        refSelect.innerHTML = `<option value="">-- جاري تحميل المراجع المعملية المتاحة في المستودع... --</option>`;
      }
      if (tbodyAll) {
        tbodyAll.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">جاري تحميل الدفعات المتاحة للتحويل...</td></tr>`;
      }

      // 1. Fetch available references in this warehouse
      let refs = [];
      try {
        const resRefs = await ChatApi.getStorageLocationAvailableReferences(locationId);
        if (resRefs && resRefs.success && Array.isArray(resRefs.data)) {
          refs = resRefs.data;
        }
      } catch (e) {
        console.warn('Error loading transfer available references:', e);
      }

      // 2. Fetch all available batches in this warehouse
      try {
        const resBatches = await ChatApi.getStorageLocationAvailableBatches(locationId);
        if (resBatches && resBatches.success && Array.isArray(resBatches.data)) {
          this.transferAvailableBatches = resBatches.data;
        } else {
          this.transferAvailableBatches = [];
        }
      } catch (e) {
        console.warn('Error loading transfer available batches:', e);
        this.transferAvailableBatches = [];
      }

      if (this.transferAvailableBatches.length === 0) {
        if (refSelect) {
          refSelect.innerHTML = `<option value="">-- لا توجد مواد متاحة في هذا المخزن حالياً --</option>`;
        }
        if (tbodyAll) {
          tbodyAll.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:2rem; color:#EF4444; font-weight:700;">⚠️ لا توجد مواد متاحة في هذا المخزن حالياً</td></tr>`;
        }
      } else {
        if (refSelect) {
          if (refs.length === 0) {
            refSelect.innerHTML = `<option value="">-- لا توجد مراجع معملية متاحة برصيد في هذا المستودع --</option>`;
          } else {
            refSelect.innerHTML = `<option value="">-- اختر رقم العينة / المرجع المعملي للتحويل --</option>` +
              refs.map(r => `
                <option value="${r.productionReferenceNumber}" data-lab-id="${r.reportId || r.laboratoryReportId}" data-sample-code="${this.escapeHtml(r.sampleCode || '')}">
                  مرجع: #${r.productionReferenceNumber} | عينة: [${this.escapeHtml(r.sampleCode || '-')}] - ${this.escapeHtml(r.materialName)} (رصيد: ${Number(r.remainingAvailableQuantity).toFixed(3)} طن)
                </option>
              `).join('');
          }
        }
        this.renderTransferDirectBatchesTable();
      }

      this.updateTransferBatchesTotal();
    }

    onTransferReferenceChanged(refNo) {
      const wrap = document.getElementById('transfer-ref-batches-wrap');
      const tbody = document.getElementById('transfer-ref-batches-tbody');
      if (!refNo) {
        if (wrap) wrap.style.display = 'none';
        if (tbody) tbody.innerHTML = '';
        this.transferSelectedBatches.clear();
        this.updateTransferBatchesTotal();
        return;
      }

      const matchingBatches = this.transferAvailableBatches.filter(b => 
        b.productionReferenceNumber === parseInt(refNo, 10)
      );

      if (wrap) wrap.style.display = 'block';
      if (!tbody) return;

      if (matchingBatches.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:1.5rem; color:#94A3B8;">لا توجد دفعات نشطة متاحة لهذا المرجع في هذا المستودع.</td></tr>`;
        return;
      }

      tbody.innerHTML = matchingBatches.map(b => {
        const batchId = b.id || b.batchId;
        const curQty = b.currentQuantity ?? b.availableQuantity ?? 0;
        const isChecked = this.transferSelectedBatches.has(batchId);
        const selQty = isChecked ? this.transferSelectedBatches.get(batchId).quantity : '';
        const classBadge = b.materialClassification === 'CHEMICAL'
          ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
          : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

        return `
          <tr data-batch-id="${batchId}">
            <td style="text-align:center;">
              <input type="checkbox" class="transfer-batch-chk" value="${batchId}" ${isChecked ? 'checked' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="storage-seq-badge" style="background:#EFF6FF; color:#1D4ED8; border:1px solid #BFDBFE; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                ${this.escapeHtml(b.batchNumber)}
              </span>
            </td>
            <td>
              <div style="display:flex; align-items:center; gap:8px;">
                <strong>${this.escapeHtml(b.materialName)}</strong>
                ${classBadge}
              </div>
            </td>
            <td style="text-align:center; font-weight:700; color:#059669;">
              ${Number(curQty).toFixed(3)} طن
            </td>
            <td>
              <input type="number" step="0.001" min="0.001" max="${curQty}" 
                class="storage-input transfer-batch-qty-input" 
                data-batch-id="${batchId}" 
                value="${selQty}" 
                placeholder="0.000" 
                style="text-align:center; font-weight:800; color:#1D4ED8;" 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td>
              <input type="text" 
                class="storage-input transfer-batch-notes-input" 
                data-batch-id="${batchId}" 
                placeholder="خط الإنتاج / الوردية / ملاحظات..." 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="badge" style="background:#DCFCE7; color:#15803D; font-size:0.75rem; padding:3px 8px; border-radius:4px;">متاح</span>
            </td>
          </tr>
        `;
      }).join('');

      this.bindTransferBatchRowEvents(tbody);
    }

    renderTransferDirectBatchesTable() {
      const tbody = document.getElementById('transfer-all-batches-tbody');
      if (!tbody) return;

      if (!this.transferAvailableBatches || this.transferAvailableBatches.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:2rem; color:#EF4444; font-weight:700;">⚠️ لا توجد مواد متاحة في هذا المخزن حالياً</td></tr>`;
        return;
      }

      tbody.innerHTML = this.transferAvailableBatches.map(b => {
        const batchId = b.id || b.batchId;
        const curQty = b.currentQuantity ?? b.availableQuantity ?? 0;
        const isChecked = this.transferSelectedBatches.has(batchId);
        const selQty = isChecked ? this.transferSelectedBatches.get(batchId).quantity : '';
        const classBadge = b.materialClassification === 'CHEMICAL'
          ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.75rem; padding:2px 8px; border-radius:4px;">كيميائي</span>`
          : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.75rem; padding:2px 8px; border-radius:4px;">ميكانيكي</span>`;

        return `
          <tr data-batch-id="${batchId}">
            <td style="text-align:center;">
              <input type="checkbox" class="transfer-batch-chk" value="${batchId}" ${isChecked ? 'checked' : ''} />
            </td>
            <td style="text-align:center;">
              <span class="storage-seq-badge" style="background:#EFF6FF; color:#1D4ED8; border:1px solid #BFDBFE; font-weight:800; font-family:monospace; font-size:0.95rem; padding:3px 10px; border-radius:6px; display:inline-block;">
                ${this.escapeHtml(b.batchNumber)}
              </span>
            </td>
            <td style="font-size:0.85rem;">
              <div style="font-weight:700; color:#0F172A;">مرجع: #${b.productionReferenceNumber}</div>
              <div style="color:#64748B;">عينة: [${this.escapeHtml(b.sampleCode || '-')}]</div>
            </td>
            <td>
              <div style="display:flex; align-items:center; gap:8px;">
                <strong>${this.escapeHtml(b.materialName)}</strong>
                ${classBadge}
              </div>
            </td>
            <td style="text-align:center; font-weight:700; color:#059669;">
              ${Number(curQty).toFixed(3)} طن
            </td>
            <td>
              <input type="number" step="0.001" min="0.001" max="${curQty}" 
                class="storage-input transfer-batch-qty-input" 
                data-batch-id="${batchId}" 
                value="${selQty}" 
                placeholder="0.000" 
                style="text-align:center; font-weight:800; color:#1D4ED8;" 
                ${!isChecked ? 'disabled' : ''} />
            </td>
            <td>
              <input type="text" 
                class="storage-input transfer-batch-notes-input" 
                data-batch-id="${batchId}" 
                placeholder="خط الإنتاج / ملاحظات..." 
                ${!isChecked ? 'disabled' : ''} />
            </td>
          </tr>
        `;
      }).join('');

      this.bindTransferBatchRowEvents(tbody);
    }

    bindTransferBatchRowEvents(container) {
      container.querySelectorAll('.transfer-batch-chk').forEach(chk => {
        chk.addEventListener('change', (e) => {
          const batchId = parseInt(e.target.value, 10);
          const row = e.target.closest('tr');
          const qtyInp = row?.querySelector('.transfer-batch-qty-input');
          const notesInp = row?.querySelector('.transfer-batch-notes-input');
          const batch = this.transferAvailableBatches.find(b => (b.id === batchId || b.batchId === batchId));

          if (!batch) return;
          const curQty = batch.currentQuantity ?? batch.availableQuantity ?? 0;

          if (e.target.checked) {
            if (qtyInp) {
              qtyInp.disabled = false;
              if (!qtyInp.value || parseFloat(qtyInp.value) <= 0) {
                qtyInp.value = Number(curQty).toFixed(3);
              }
              qtyInp.focus();
            }
            if (notesInp) notesInp.disabled = false;
            this.transferSelectedBatches.set(batchId, {
              batch: batch,
              quantity: parseFloat(qtyInp?.value) || Number(curQty),
              notes: notesInp?.value || ''
            });
          } else {
            if (qtyInp) qtyInp.disabled = true;
            if (notesInp) notesInp.disabled = true;
            this.transferSelectedBatches.delete(batchId);
          }
          this.updateTransferBatchesTotal();
        });
      });

      container.querySelectorAll('.transfer-batch-qty-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const batchId = parseInt(e.target.getAttribute('data-batch-id'), 10);
          const batch = this.transferAvailableBatches.find(b => (b.id === batchId || b.batchId === batchId));
          if (!batch) return;
          const curQty = batch.currentQuantity ?? batch.availableQuantity ?? 0;

          let val = parseFloat(e.target.value) || 0;
          if (val > curQty) {
            e.target.style.borderColor = '#DC2626';
          } else {
            e.target.style.borderColor = '';
          }

          if (this.transferSelectedBatches.has(batchId)) {
            this.transferSelectedBatches.get(batchId).quantity = val;
          }
          this.updateTransferBatchesTotal();
        });
      });

      container.querySelectorAll('.transfer-batch-notes-input').forEach(inp => {
        inp.addEventListener('input', (e) => {
          const batchId = parseInt(e.target.getAttribute('data-batch-id'), 10);
          if (this.transferSelectedBatches.has(batchId)) {
            this.transferSelectedBatches.get(batchId).notes = e.target.value.trim();
          }
        });
      });
    }

    updateTransferBatchesTotal() {
      const itemsBadge = document.getElementById('transfer-total-items-badge');
      const qtyBadge = document.getElementById('transfer-total-qty-badge');
      const statusBadge = document.getElementById('transfer-balance-status-badge');

      let totalQty = 0;
      let hasOverQty = false;

      this.transferSelectedBatches.forEach(sel => {
        totalQty += (sel.quantity || 0);
        const curBatchMax = sel.batch.currentQuantity ?? sel.batch.availableQuantity ?? 0;
        if (sel.quantity > curBatchMax) {
          hasOverQty = true;
        }
      });

      if (itemsBadge) itemsBadge.textContent = this.transferSelectedBatches.size;
      if (qtyBadge) qtyBadge.textContent = `${totalQty.toFixed(3)} طن`;

      if (statusBadge) {
        if (this.transferSelectedBatches.size === 0) {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#E2E8F0; color:#475569;">بانتظار تحديد الدفعات</span>`;
        } else if (hasOverQty) {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#FEE2E2; color:#991B1B;">⚠️ خطأ: توجد كمية تتجاوز رصيد الدفعة!</span>`;
        } else {
          statusBadge.innerHTML = `<span style="display:inline-block; padding:4px 12px; border-radius:6px; font-size:0.8rem; font-weight:700; background:#DBEAFE; color:#1E40AF;">جاهز للتحويل (${this.transferSelectedBatches.size} دفعات)</span>`;
        }
      }
    }

    renderTransferChecklist() {
      // Preserved for backward compatibility
    }

    renderTransferQuantitiesTable() {
      // Preserved for backward compatibility
    }

    async handleTransferSubmit() {
      if (this.isSubmitting) return;
      this.clearAllErrors();

      const locationId = parseInt(document.getElementById('transfer-location-select')?.value, 10);
      const dtVal = document.getElementById('transfer-datetime')?.value;
      const notes = document.getElementById('transfer-notes')?.value?.trim();
      const destType = document.getElementById('transfer-destination-type')?.value || 'PRODUCTION';

      let hasError = false;

      if (!dtVal) {
        this.setFieldError('transfer-datetime', 'التاريخ والوقت مطلوب.');
        hasError = true;
      }
      if (!locationId || isNaN(locationId)) {
        this.setFieldError('transfer-location-select', 'مكان التخزين المصدر مطلوب.');
        hasError = true;
      }

      let destStorageLocationId = null;
      if (destType === 'WAREHOUSE') {
        destStorageLocationId = parseInt(document.getElementById('transfer-dest-warehouse-select')?.value, 10);
        if (!destStorageLocationId || isNaN(destStorageLocationId)) {
          this.setFieldError('transfer-dest-warehouse-select', 'يرجى اختيار مستودع الوجهة المحول إليه.');
          hasError = true;
        } else if (destStorageLocationId === locationId) {
          this.setFieldError('transfer-dest-warehouse-select', 'لا يمكن التحويل لنفس مستودع المصدر. يرجى اختيار مستودع مختلف.');
          hasError = true;
        }
      }

      if (this.transferSelectedBatches.size === 0) {
        this.showToast('error', 'يرجى تحديد دفعة واحدة على الأقل لتحويلها.');
        return;
      }

      const items = [];
      for (const [batchId, sel] of this.transferSelectedBatches) {
        const qty = parseFloat(sel.quantity);
        const curBatchMax = sel.batch.currentQuantity ?? sel.batch.availableQuantity ?? 0;
        if (isNaN(qty) || qty <= 0) {
          this.showToast('error', `الدفعة '${sel.batch.batchNumber}' يجب أن تكون كميتها أكبر من الصفر.`);
          return;
        }
        if (qty > curBatchMax) {
          this.showToast('error', `الكمية المراد تحويلها للدفعة '${sel.batch.batchNumber}' (${qty.toFixed(3)} طن) تتجاوز رصيدها المتاح بالمخزن (${Number(curBatchMax).toFixed(3)} طن).`);
          return;
        }
        items.push({
          warehouseBatchId: batchId,
          rawMaterialId: sel.batch.rawMaterialId,
          quantity: qty,
          notes: sel.notes || null
        });
      }

      if (hasError) return;

      // Primary reference if in reference mode
      let refNo = null;
      let labReportId = null;
      let sampleCd = null;
      if (this.transferSelectionMode === 'BY_REFERENCE') {
        const refSelect = document.getElementById('transfer-ref-select');
        const selectedVal = refSelect?.value;
        if (selectedVal) {
          refNo = parseInt(selectedVal, 10);
          const firstBatch = this.transferSelectedBatches.values().next().value?.batch;
          if (firstBatch) {
            labReportId = firstBatch.laboratoryReportId || firstBatch.reportId;
            sampleCd = firstBatch.sampleCode;
          }
        }
      }

      const payload = {
        transactionDate: new Date(dtVal).toISOString(),
        storageLocationId: locationId,
        destinationType: destType,
        destinationStorageLocationId: destStorageLocationId,
        productionLine: destType === 'PRODUCTION' ? "خط الإنتاج الرئيسي" : null,
        selectionMode: this.transferSelectionMode,
        productionReferenceNumber: refNo,
        laboratoryReportId: labReportId,
        sampleCode: sampleCd,
        notes: notes || null,
        items: items,
        itemsJson: JSON.stringify(items)
      };

      const btnSubmit = document.getElementById('btn-transfer-submit');
      const spinner = document.getElementById('transfer-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.submitStorageTransfer(payload);
        if (res.success && res.data) {
          const txNum = res.data.transactionFormattedNumber || `#${res.data.transactionNumber}`;
          const destName = destType === 'WAREHOUSE' ? 'المستودع المستلم' : 'خطوط الإنتاج والتصنيع';
          this.showToast('success', `تم تنفيذ إذن التحويل برقم ${txNum} بنجاح وتحديث رصيد ${destName} لحظياً.`);
          await this.showDashboardHub();
        } else {
          this.showToast('error', res.message || 'فشل حفظ إذن التحويل.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    // =========================================================================
    // PRODUCTION ORDER TRANSFER INTEGRATION (تأكيد نقل المواد لأوامر الإنتاج)
    // =========================================================================

    async loadPendingProductionOrders() {
      const container = document.getElementById('storage-pending-orders-list-container');
      const badge = document.getElementById('storage-pending-orders-count-badge');
      if (!container) return;

      container.innerHTML = '<div style="text-align: center; padding: 2rem; color: #94a3b8; font-size: 0.9rem;">جاري فحص طلبات أوامر الإنتاج المعلقة...</div>';

      try {
        const res = await ChatApi.getPendingProductionOrderTransfers();
        if (res && res.success && Array.isArray(res.data)) {
          this.cachedPendingOrders = res.data;
          if (badge) {
            badge.textContent = `${res.data.length} طلب قيد النقل`;
            badge.style.background = res.data.length > 0 ? '#2563eb' : '#64748b';
          }

          if (res.data.length === 0) {
            container.innerHTML = '<div style="text-align: center; padding: 2rem; color: #64748b; font-size: 0.9rem;">لا توجد طلبات أوامر إنتاج معلقة بانتظار النقل حالياً.</div>';
            return;
          }

          container.innerHTML = `
            <div style="overflow-x: auto; border: 1px solid #e2e8f0; border-radius: 8px;">
              <table style="width: 100%; text-align: right; border-collapse: collapse; font-size: 0.85rem;">
                <thead style="background: #f8fafc;">
                  <tr>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">رقم أمر الإنتاج</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">المنتج النهائي</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">الكمية المستهدفة</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">المطلوب بالخلطة</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">المنقول سابقاً</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">المتبقي</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0;">الحالة</th>
                    <th style="padding: 10px 12px; border-bottom: 1px solid #e2e8f0; text-align: center;">الإجراء</th>
                  </tr>
                </thead>
                <tbody>
                  ${res.data.map(o => {
                    const planned = Number(o.totalPlannedRawMaterialQuantity || 0);
                    const transferred = Number(o.totalTransferredQuantity || 0);
                    const rem = Math.max(0, planned - transferred);
                    const isPart = o.status === 'PARTIALLY_TRANSFERRED';
                    return `
                      <tr>
                        <td style="padding: 10px 12px; font-weight: 800; color: #1d4ed8;">${this.escapeHtml(o.orderNumber)}</td>
                        <td style="padding: 10px 12px; font-weight: 700;">${this.escapeHtml(o.productName)}</td>
                        <td style="padding: 10px 12px;">${Number(o.requestedQuantity ?? o.requiredQuantity ?? 0).toFixed(3)} طن</td>
                        <td style="padding: 10px 12px;">${planned.toFixed(3)} طن</td>
                        <td style="padding: 10px 12px; color: #059669; font-weight: 600;">${transferred.toFixed(3)} طن</td>
                        <td style="padding: 10px 12px; color: #d97706; font-weight: 700;">${rem.toFixed(3)} طن</td>
                        <td style="padding: 10px 12px;">
                          <span style="background: ${isPart ? '#e0f2fe' : '#fef3c7'}; color: ${isPart ? '#0369a1' : '#b45309'}; padding: 3px 8px; border-radius: 9999px; font-weight: 700; font-size: 0.78rem;">
                            ${isPart ? '🔄 منقول جزئياً' : '⏳ بانتظار النقل'}
                          </span>
                        </td>
                        <td style="padding: 10px 12px; text-align: center;">
                          <button type="button" class="storage-btn-primary" onclick="window.StorageApp.openConfirmTransferModal(${o.id})" style="padding: 6px 14px; font-size: 0.825rem; font-weight: 700; display: inline-flex; align-items: center; gap: 5px;">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
                            <span>تأكيد النقل للإنتاج</span>
                          </button>
                        </td>
                      </tr>
                    `;
                  }).join('')}
                </tbody>
              </table>
            </div>
          `;
        } else {
          container.innerHTML = `<div style="text-align: center; padding: 2rem; color: #ef4444; font-size: 0.9rem;">فشل جلب طلبات النقل: ${this.escapeHtml(res?.message || '')}</div>`;
        }
      } catch (err) {
        container.innerHTML = `<div style="text-align: center; padding: 2rem; color: #ef4444; font-size: 0.9rem;">حدث خطأ أثناء فحص طلبات النقل: ${this.escapeHtml(err.message)}</div>`;
      }
    }

    async openConfirmTransferModal(orderId) {
      const modal = document.getElementById('storage-modal-confirm-order-transfer');
      const orderNumEl = document.getElementById('stg-trf-modal-order-number');
      const prodNameEl = document.getElementById('stg-trf-modal-product-name');
      const hiddenId = document.getElementById('stg-trf-order-id');
      const tbody = document.getElementById('storage-transfer-items-tbody');
      const notesInp = document.getElementById('stg-trf-modal-notes');

      if (!modal || !tbody) return;

      tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #94a3b8;">جاري تحميل بنود أمر الإنتاج...</td></tr>';
      modal.style.display = 'flex';

      try {
        let order = this.cachedPendingOrders?.find(x => x.id === orderId);
        const res = await ChatApi.getLabProductionOrderById(orderId);
        if (res && res.success && res.data) {
          order = res.data;
        }

        if (!order) {
          alert('تعذر العثور على بيانات أمر الإنتاج المحدد.');
          modal.style.display = 'none';
          return;
        }

        if (hiddenId) hiddenId.value = order.id;
        const targetQty = Number(order.requestedQuantity ?? order.requiredQuantity ?? 0);
        if (orderNumEl) orderNumEl.textContent = `تأكيد نقل المواد لأمر الإنتاج ${order.orderNumber}`;
        if (prodNameEl) prodNameEl.textContent = `المنتج المستهدف: ${order.productName} (${targetQty.toFixed(3)} طن)`;
        if (notesInp) notesInp.value = '';

        const items = order.items || [];
        tbody.innerHTML = items.map(it => {
          const requested = Number(it.requestedQuantity ?? it.requiredQuantity ?? 0);
          const prevTransferred = Number(it.issuedQuantity ?? it.transferredQuantity ?? 0);
          const pending = Math.max(0, requested - prevTransferred);
          const avail = Number(it.currentWarehouseBatchQuantity ?? it.availableWarehouseQuantity ?? it.availableQuantity ?? 0);
          const defaultTransfer = Math.min(pending, avail);

          return `
            <tr data-item-id="${it.id}" data-batch-id="${it.warehouseBatchId}">
              <td style="padding: 10px 12px; font-weight: 700; color: #1e293b;">${this.escapeHtml(it.materialName || 'خامة')}</td>
              <td style="padding: 10px 12px; font-family: monospace; color: #2563eb; font-weight: 700;">${this.escapeHtml(it.batchNumber || '-')}</td>
              <td style="padding: 10px 12px; font-family: monospace; color: #475569;">REF-${it.productionReferenceNumber || '-'}</td>
              <td style="padding: 10px 12px; font-weight: 600;">${requested.toFixed(3)} طن</td>
              <td style="padding: 10px 12px; color: #059669;">${prevTransferred.toFixed(3)} طن</td>
              <td style="padding: 10px 12px; color: #d97706; font-weight: 700;">${pending.toFixed(3)} طن</td>
              <td style="padding: 10px 12px; color: ${avail < pending ? '#dc2626' : '#1e293b'}; font-weight: 700;">
                ${avail.toFixed(3)} طن
                ${avail < pending ? '<span style="font-size: 0.72rem; color: #dc2626; display: block;">رصيد غير كافٍ للكامل</span>' : ''}
              </td>
              <td style="padding: 8px 12px;">
                <input type="number" class="stg-confirm-item-input storage-input" 
                  data-item-id="${it.id}" 
                  data-batch-id="${it.warehouseBatchId}"
                  data-pending="${pending}"
                  data-avail="${avail}"
                  step="0.001" min="0" max="${pending}"
                  value="${defaultTransfer.toFixed(3)}" 
                  style="width: 100%; padding: 5px 8px; font-size: 0.85rem; font-weight: 700;" 
                  ${pending <= 0 ? 'disabled' : ''} />
              </td>
            </tr>
          `;
        }).join('');

      } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #ef4444;">حدث خطأ أثناء تحميل البنود: ${this.escapeHtml(err.message)}</td></tr>`;
      }
    }

    async submitConfirmOrderTransfer(e) {
      e.preventDefault();
      const hiddenId = document.getElementById('stg-trf-order-id');
      const notesInp = document.getElementById('stg-trf-modal-notes');
      const submitBtn = document.getElementById('btn-confirm-storage-trf-submit');
      const modal = document.getElementById('storage-modal-confirm-order-transfer');

      const orderId = parseInt(hiddenId?.value || '0', 10);
      if (!orderId) {
        alert('لم يتم تحديد أمر الإنتاج المراد تأكيد نقله.');
        return;
      }

      const items = [];
      let totalToTransfer = 0;
      let hasValidationError = false;

      const rows = document.querySelectorAll('#storage-transfer-items-tbody tr');
      rows.forEach(row => {
        const inp = row.querySelector('.stg-confirm-item-input');
        if (!inp || inp.disabled) return;

        const itemId = parseInt(inp.getAttribute('data-item-id') || '0', 10);
        const batchId = parseInt(inp.getAttribute('data-batch-id') || '0', 10);
        const pending = parseFloat(inp.getAttribute('data-pending') || '0');
        const avail = parseFloat(inp.getAttribute('data-avail') || '0');
        const val = parseFloat(inp.value);

        if (isNaN(val) || val < 0) {
          alert('يرجى إدخال كمية منقولة صحيحة أكبر من أو تساوي الصفر لجميع البنود.');
          hasValidationError = true;
          return;
        }

        if (val > pending + 0.0001) {
          alert(`الكمية المحددة للدفعة (${val} طن) تتجاوز الكمية المتبقية للنقل (${pending} طن).`);
          hasValidationError = true;
          return;
        }

        if (val > avail + 0.0001) {
          alert(`الكمية المحددة (${val} طن) تتجاوز الرصيد الفعلي المتوفر في المستودع (${avail} طن).`);
          hasValidationError = true;
          return;
        }

        if (val > 0) {
          items.push({
            productionOrderItemId: itemId,
            warehouseBatchId: batchId,
            transferQuantity: val,
            transferredQuantity: val
          });
          totalToTransfer += val;
        }
      });

      if (hasValidationError) return;

      if (items.length === 0 || totalToTransfer <= 0) {
        alert('يجب تحديد كمية منقولة أكبر من الصفر لمادة خام واحدة على الأقل لتأكيد النقل.');
        return;
      }

      const payload = {
        notes: notesInp?.value?.trim() || null,
        items: items
      };

      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span>جاري تأكيد النقل وخصم المخزون...</span>';
      }

      try {
        const res = await ChatApi.confirmProductionOrderTransfer(orderId, payload);
        if (res && res.success && res.data) {
          const data = res.data;
          this.showToast('success', `تم تأكيد النقل برقم ${data.transferTransactionNumber} بنجاح. تم خصم ${data.totalTransferredQuantity} طن من رصيد المستودع (حالة الأمر الآن: ${data.statusArabic}).`);
          if (modal) modal.style.display = 'none';
          await this.loadPendingProductionOrders();
          await this.loadHubData();
        } else {
          this.showToast('error', res?.message || 'فشل تأكيد النقل من المستودع.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.innerHTML = '<span>تأكيد النقل والخصم من المستودع</span>';
        }
      }
    }

    // =========================================================================
    // MODALS: Production Output & Operation Details
    // =========================================================================

    openWipModal(batch) {
      if (!this.wipOutputModal) return;
      this.currentWipBatch = batch;

      document.getElementById('wip-modal-batch-id').value = batch.id;
      document.getElementById('wip-modal-tracking-num').textContent = batch.trackingNumber;
      document.getElementById('wip-modal-material-name').textContent = batch.materialName;
      document.getElementById('wip-modal-raw-qty').textContent = `${Number(batch.materialQuantityIssued).toFixed(3)} طن`;

      const prodName = document.getElementById('wip-modal-product-name');
      const prodQty = document.getElementById('wip-modal-product-qty');
      const notes = document.getElementById('wip-modal-notes');

      if (prodName) prodName.value = '';
      if (prodQty) prodQty.value = '';
      if (notes) notes.value = '';

      // Reset waste classification
      const ordinaryRadio = document.getElementById('wip-waste-ordinary');
      if (ordinaryRadio) ordinaryRadio.checked = true;
      const recoverableWrap = document.getElementById('wip-recoverable-wrap');
      if (recoverableWrap) recoverableWrap.style.display = 'none';
      const recoverableName = document.getElementById('wip-recoverable-name');
      if (recoverableName) recoverableName.value = '';
      const recoverableQty = document.getElementById('wip-recoverable-qty');
      if (recoverableQty) recoverableQty.value = '';

      // Populate destination location dropdown
      const destSelect = document.getElementById('wip-recoverable-dest');
      if (destSelect && this.locations && this.locations.length > 0) {
        destSelect.innerHTML = `<option value="" disabled selected>-- اختر المستودع --</option>` +
          this.locations.map(loc => `<option value="${loc.id}">${this.escapeHtml(loc.name)} (${this.escapeHtml(loc.code)})</option>`).join('');
      }

      this.calculateWipMassBalance();
      this.wipOutputModal.style.display = 'flex';
    }


    closeWipModal() {
      if (this.wipOutputModal) this.wipOutputModal.style.display = 'none';
      this.currentWipBatch = null;
    }

    calculateWipMassBalance() {
      if (!this.currentWipBatch) return;
      const rawQty = parseFloat(this.currentWipBatch.materialQuantityIssued) || 0;
      const outputVal = parseFloat(document.getElementById('wip-modal-product-qty')?.value);

      const elWasteQty = document.getElementById('wip-modal-waste-qty');
      const elWastePct = document.getElementById('wip-modal-waste-percent');

      if (isNaN(outputVal) || outputVal < 0) {
        if (elWasteQty) elWasteQty.textContent = '0.000 طن';
        if (elWastePct) elWastePct.textContent = '0.00 %';
        return;
      }

      const wasteQty = Math.max(0, rawQty - outputVal);
      const wastePct = rawQty > 0 ? (wasteQty / rawQty) * 100 : 0;

      if (elWasteQty) elWasteQty.textContent = `${wasteQty.toFixed(3)} طن`;
      if (elWastePct) elWastePct.textContent = `${wastePct.toFixed(2)} %`;
    }

    async handleWipOutputSubmit() {
      if (this.isSubmitting) return;

      const batchId = this.currentWipBatch?.id || parseInt(document.getElementById('wip-modal-batch-id')?.value, 10);
      const prodName = document.getElementById('wip-modal-product-name')?.value?.trim();
      const prodQty = parseFloat(document.getElementById('wip-modal-product-qty')?.value);
      const notes = document.getElementById('wip-modal-notes')?.value?.trim();
      const wasteClass = document.querySelector('input[name="wip-waste-class"]:checked')?.value || 'ORDINARY';

      if (!prodName) {
        this.showToast('error', 'اسم المنتج النهائي مطلوب.');
        return;
      }
      if (isNaN(prodQty) || prodQty <= 0) {
        this.setFieldError('wip-modal-product-qty', 'كمية المنتج النهائي يجب أن تكون أكبر من الصفر.');
        return;
      }
      if (this.currentWipBatch && prodQty > this.currentWipBatch.materialQuantityIssued) {
        this.setFieldError('wip-modal-product-qty', `كمية المنتج النهائي لا يمكن أن تتجاوز كمية المادة الخام المستلمة (${Number(this.currentWipBatch.materialQuantityIssued).toFixed(3)} طن).`);
        return;
      }

      let recoverableQty = null;
      let recoverableName = null;
      let recoverableDestId = null;

      if (wasteClass !== 'ORDINARY') {
        recoverableName = document.getElementById('wip-recoverable-name')?.value?.trim();
        recoverableQty = parseFloat(document.getElementById('wip-recoverable-qty')?.value);
        recoverableDestId = parseInt(document.getElementById('wip-recoverable-dest')?.value, 10) || null;

        if (!recoverableName) {
          this.setFieldError('wip-recoverable-name', 'اسم المادة المستردة مطلوب.');
          return;
        }
        if (isNaN(recoverableQty) || recoverableQty <= 0) {
          this.setFieldError('wip-recoverable-qty', 'كمية المادة المستردة يجب أن تكون أكبر من الصفر.');
          return;
        }
        if (!recoverableDestId) {
          this.setFieldError('wip-recoverable-dest', 'موقع التخزين المستهدف مطلوب.');
          return;
        }
      }

      const payload = {
        finalProductName: prodName,
        finalProductQuantity: prodQty,
        notes: notes || null,
        wasteClassification: wasteClass,
        recoverableProductName: recoverableName,
        recoverableQuantity: recoverableQty,
        recoverableUnit: 'طن',
        destinationStorageLocationId: recoverableDestId
      };

      const btnSubmit = document.getElementById('btn-wip-modal-submit');
      const spinner = document.getElementById('wip-modal-spinner');
      this.isSubmitting = true;
      if (btnSubmit) btnSubmit.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await ChatApi.recordStorageProductionOutput(batchId, payload);
        if (res.success) {
          const wasteQty = res.data?.wasteQuantity ?? 0;
          const wastePct = res.data?.wastePercentage ?? 0;
          this.showToast('success', `تم تسجيل مخرجات الإنتاج. الهالك: ${wasteQty.toFixed(3)} طن (${wastePct.toFixed(2)}%).`);
          this.closeWipModal();
          await this.loadProductionBatches();
        } else {
          this.showToast('error', res.message || 'فشل تسجيل مخرجات الإنتاج.');
        }
      } catch (err) {
        this.showToast('error', err.message || 'حدث خطأ في الاتصال بالسيرفر.');
      } finally {
        this.isSubmitting = false;
        if (btnSubmit) btnSubmit.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }


    async openOpDetailsModal(operationId) {
      if (!this.opDetailsModal) return;
      const modalBody = document.getElementById('op-details-modal-body');
      const modalTitle = document.getElementById('op-details-modal-title');
      const modalSub = document.getElementById('op-details-modal-sub');

      if (modalBody) {
        modalBody.innerHTML = `<div style="text-align:center; padding:2rem; color:#94A3B8;">جاري تحميل تفاصيل العملية...</div>`;
      }
      this.opDetailsModal.style.display = 'flex';

      try {
        const res = await ChatApi.getStorageOperationDetails(operationId);
        if (!res.success || !res.data) {
          if (modalBody) modalBody.innerHTML = `<div style="color:#DC2626; padding:1.5rem;">فشل جلب تفاصيل العملية.</div>`;
          return;
        }

        const op = res.data;
        if (modalTitle) modalTitle.textContent = `تفاصيل العملية ${op.transactionFormattedNumber}`;
        if (modalSub) modalSub.textContent = `نوع الحركة: ${op.operationType} | تاريخ: ${this.formatDateTime(op.transactionDate)}`;

        // Fetch batches if available
        let batches = [];
        try {
          const bRes = await ChatApi.getStorageOperationBatches(operationId);
          if (bRes && bRes.success && Array.isArray(bRes.data)) {
            batches = bRes.data;
          }
        } catch (e) {
          console.warn('Could not fetch batches for op details:', e);
        }

        const itemsHtml = op.items && op.items.length > 0
          ? `
            <table class="storage-qty-table" style="margin-top:10px;">
              <thead>
                <tr>
                  <th>اسم الصنف</th>
                  <th>الكمية بالطن</th>
                  <th>رقم التتبع / الدفعة</th>
                </tr>
              </thead>
              <tbody>
                ${op.items.map(i => {
                  const tag = i.batchNumber || i.warehouseBatchNumber || i.trackingNumber || '—';
                  return `
                    <tr>
                      <td><strong>${this.escapeHtml(i.materialName)}</strong></td>
                      <td style="font-weight:700; color:#059669;">${Number(i.quantity).toFixed(3)} طن</td>
                      <td><span class="storage-seq-badge" style="font-family:monospace;">${this.escapeHtml(tag)}</span></td>
                    </tr>
                  `;
                }).join('')}
              </tbody>
            </table>
          `
          : '<p style="color:#94A3B8;">لا توجد بنود مسجلة.</p>';

        const batchesHtml = batches.length > 0
          ? `
            <div style="margin-top:1.25rem; border-top:1px solid #E2E8F0; padding-top:1rem;">
              <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px; margin-bottom:8px;">
                <h4 style="margin:0; font-size:0.95rem; font-weight:800; color:#0F172A;">
                  الدفعات المخزنية المرتبطة بالعملية (${batches.length} دفعات):
                </h4>
                <button type="button" class="storage-btn" id="btn-details-print-batches" style="background:#059669; color:#fff; border-color:#047857; padding:4px 14px; font-size:0.8rem; display:inline-flex; align-items:center; gap:6px; cursor:pointer;">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
                  <span>طباعة بطاقات أرقام الدفعات</span>
                </button>
              </div>
              <table class="storage-qty-table" style="margin-top:6px;">
                <thead>
                  <tr>
                    <th style="text-align:center;">رقم الدفعة</th>
                    <th>المادة</th>
                    <th style="text-align:center;">الكمية الحالية / الأولية</th>
                    <th style="text-align:center;">رقم العينة / المرجع</th>
                    <th style="text-align:center;">الحالة</th>
                  </tr>
                </thead>
                <tbody>
                  ${batches.map(b => `
                    <tr>
                      <td style="text-align:center;">
                        <span class="storage-seq-badge" style="background:#ECFDF5; color:#065F46; border:1px solid #A7F3D0; font-family:monospace; font-weight:800;">
                          ${b.batchNumber}
                        </span>
                      </td>
                      <td><strong>${this.escapeHtml(b.materialName)}</strong></td>
                      <td style="text-align:center; font-weight:700; color:#059669;">
                        ${Number(b.currentQuantity).toFixed(3)} / ${Number(b.initialQuantity).toFixed(3)} طن
                      </td>
                      <td style="text-align:center; font-size:0.85rem;">
                        ${this.escapeHtml(b.sampleCode ? `عينة [${b.sampleCode}]` : `#${b.productionReferenceNumber}`)}
                      </td>
                      <td style="text-align:center;">
                        <span class="badge" style="background:#F1F5F9; color:#475569; font-size:0.75rem; padding:2px 6px; border-radius:4px;">
                          ${this.escapeHtml(b.status)}
                        </span>
                      </td>
                    </tr>
                  `).join('')}
                </tbody>
              </table>
            </div>
          `
          : '';

        if (modalBody) {
          modalBody.innerHTML = `
            <div style="background:#F8FAFC; border:1px solid #E2E8F0; border-radius:10px; padding:1rem; margin-bottom:1rem;">
              <div style="display:grid; grid-template-columns:repeat(2,1fr); gap:10px; font-size:0.85rem;">
                <div><span style="color:#64748B;">مكان التخزين:</span> <strong>${this.escapeHtml(op.storageLocationName || '-')}</strong></div>
                <div><span style="color:#64748B;">الموظف المسؤول:</span> <strong>${this.escapeHtml(op.responsibleEmployee || '-')}</strong></div>
                <div><span style="color:#64748B;">حالة الاعتماد:</span> <strong>${this.escapeHtml(op.approvalStatus)}</strong></div>
                <div><span style="color:#64748B;">المراجع:</span> <strong>${this.escapeHtml(op.reviewedBy || 'بانتظار المراجعة')}</strong></div>
                ${op.source ? `<div><span style="color:#64748B;">المورد / المصدر:</span> <strong>${this.escapeHtml(op.source)}</strong> (${this.escapeHtml(op.sourcePhone || 'بدون هاتف')})</div>` : ''}
                ${op.clientName ? `<div><span style="color:#64748B;">العميل:</span> <strong>${this.escapeHtml(op.clientName)}</strong> - وجهة: ${this.escapeHtml(op.destinationName || '')}</div>` : ''}
              </div>
              ${op.notes ? `<div style="margin-top:10px; font-size:0.8rem; color:#475569; border-top:1px dashed #CBD5E1; padding-top:6px;"><strong>ملاحظات:</strong> ${this.escapeHtml(op.notes)}</div>` : ''}
              ${op.rejectionReason ? `<div style="margin-top:10px; font-size:0.8rem; color:#DC2626; background:#FEE2E2; border:1px solid #FCA5A5; padding:6px 10px; border-radius:6px;"><strong>سبب الرفض:</strong> ${this.escapeHtml(op.rejectionReason)}</div>` : ''}
            </div>

            <h4 style="margin:1rem 0 0.5rem 0; font-size:0.95rem; font-weight:800; color:#0F172A;">بنود الأصناف المسجلة:</h4>
            ${itemsHtml}
            ${batchesHtml}
          `;

          // Attach listener to print batches button
          const btnPrint = document.getElementById('btn-details-print-batches');
          btnPrint?.addEventListener('click', () => {
            this.openBatchPrintModal(batches, op);
          });
        }
      } catch (err) {
        if (modalBody) modalBody.innerHTML = `<div style="color:#DC2626; padding:1.5rem;">خطأ: ${this.escapeHtml(err.message)}</div>`;
      }
    }

    closeOpDetailsModal() {
      if (this.opDetailsModal) this.opDetailsModal.style.display = 'none';
    }

    // =========================================================================
    // MODALS: Batch Printing (طباعة بطاقات أرقام الدفعات A4 RTL)
    // =========================================================================

    openBatchPrintModal(batches, opInfo) {
      if (!this.batchPrintModal) return;
      if (!batches || batches.length === 0) {
        this.showToast('info', 'لا توجد دفعات متاحة للطباعة في هذه العملية.');
        return;
      }

      this.currentPrintBatches = batches;
      this.currentPrintOpInfo = opInfo;

      const modalBody = document.getElementById('batch-print-modal-body');
      if (!modalBody) return;

      const opNum = opInfo?.transactionFormattedNumber || opInfo?.operationNumber || `#${opInfo?.id || ''}`;
      const labRef = opInfo?.sampleCode 
        ? `عينة [${opInfo.sampleCode}]` 
        : (opInfo?.productionReferenceNumber ? `مرجع [#${opInfo.productionReferenceNumber}]` : (batches[0]?.sampleCode ? `عينة [${batches[0].sampleCode}]` : '-'));
      const supplier = opInfo?.source || opInfo?.supplierName || batches[0]?.supplierName || '-';
      const opDate = this.formatDateTime(opInfo?.transactionDate || opInfo?.operationDate || new Date());
      const locName = opInfo?.storageLocationName || batches[0]?.storageLocationName || '-';

      modalBody.innerHTML = `
        <div style="background:#F8FAFC; border:1px solid #E2E8F0; border-radius:8px; padding:12px; margin-bottom:12px; font-size:0.85rem;">
          <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap:8px;">
            <div><span style="color:#64748B;">رقم إذن العملية:</span> <strong>${this.escapeHtml(opNum)}</strong></div>
            <div><span style="color:#64748B;">المرجع المعملي:</span> <strong style="color:#059669;">${this.escapeHtml(labRef)}</strong></div>
            <div><span style="color:#64748B;">المورد / المصدر:</span> <strong>${this.escapeHtml(supplier)}</strong></div>
            <div><span style="color:#64748B;">مستودع التخزين:</span> <strong>${this.escapeHtml(locName)}</strong></div>
            <div><span style="color:#64748B;">تاريخ العملية:</span> <strong>${this.escapeHtml(opDate)}</strong></div>
          </div>
        </div>

        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:10px; padding:0 4px;">
          <label style="display:inline-flex; align-items:center; gap:8px; font-weight:700; cursor:pointer; font-size:0.88rem; color:#0F172A;">
            <input type="checkbox" id="batch-print-select-all" checked style="width:16px; height:16px;" />
            <span>تحديد جميع الدفعات للطباعة</span>
          </label>
          <span id="batch-print-selected-count" style="background:#ECFDF5; color:#065F46; border:1px solid #A7F3D0; font-size:0.8rem; font-weight:700; padding:2px 10px; border-radius:12px;">
            تم اختيار ${batches.length} من ${batches.length} دفعة
          </span>
        </div>

        <div style="max-height:360px; overflow-y:auto; border:1px solid #E2E8F0; border-radius:8px;">
          <table class="storage-qty-table" style="margin:0;">
            <thead style="position:sticky; top:0; background:#F8FAFC; z-index:1;">
              <tr>
                <th style="width:45px; text-align:center;">اختيار</th>
                <th style="text-align:center;">رقم الدفعة</th>
                <th>اسم المادة والتصنيف</th>
                <th style="text-align:center;">الكمية</th>
                <th style="text-align:center;">الحالة</th>
              </tr>
            </thead>
            <tbody>
              ${batches.map((b, idx) => {
                const bQty = Number(b.initialQuantity ?? b.currentQuantity ?? b.quantity ?? 0).toFixed(3);
                const classBadge = b.materialClassification === 'CHEMICAL'
                  ? `<span class="badge" style="background:#DBEAFE; color:#1E40AF; font-size:0.72rem; padding:1px 6px; border-radius:4px;">كيميائي</span>`
                  : `<span class="badge" style="background:#D1FAE5; color:#065F46; font-size:0.72rem; padding:1px 6px; border-radius:4px;">ميكانيكي</span>`;

                return `
                  <tr>
                    <td style="text-align:center;">
                      <input type="checkbox" class="batch-print-item-chk" data-idx="${idx}" checked style="width:16px; height:16px; cursor:pointer;" />
                    </td>
                    <td style="text-align:center;">
                      <span class="storage-seq-badge" style="background:#ECFDF5; color:#065F46; border:1px solid #A7F3D0; font-weight:800; font-family:monospace; font-size:0.95rem; padding:2px 8px; border-radius:4px;">
                        ${b.batchNumber}
                      </span>
                    </td>
                    <td>
                      <div style="display:flex; align-items:center; gap:6px;">
                        <strong>${this.escapeHtml(b.materialName || 'مادة خام')}</strong>
                        ${classBadge}
                      </div>
                    </td>
                    <td style="text-align:center; font-weight:800; color:#059669;">
                      ${bQty} طن
                    </td>
                    <td style="text-align:center;">
                      <span class="badge" style="background:#F1F5F9; color:#475569; font-size:0.75rem; padding:2px 6px; border-radius:4px;">
                        ${this.escapeHtml(b.status || 'نشطة')}
                      </span>
                    </td>
                  </tr>
                `;
              }).join('')}
            </tbody>
          </table>
        </div>
      `;

      // Event listener for select-all
      const chkAll = document.getElementById('batch-print-select-all');
      const itemChks = modalBody.querySelectorAll('.batch-print-item-chk');
      const countEl = document.getElementById('batch-print-selected-count');

      const updateCount = () => {
        const checkedCount = modalBody.querySelectorAll('.batch-print-item-chk:checked').length;
        if (countEl) countEl.textContent = `تم اختيار ${checkedCount} من ${batches.length} دفعة`;
        if (chkAll) chkAll.checked = (checkedCount === batches.length);
      };

      chkAll?.addEventListener('change', (e) => {
        itemChks.forEach(c => c.checked = e.target.checked);
        updateCount();
      });

      itemChks.forEach(c => {
        c.addEventListener('change', updateCount);
      });

      this.batchPrintModal.style.display = 'flex';
    }

    closeBatchPrintModal() {
      if (this.batchPrintModal) {
        this.batchPrintModal.style.display = 'none';
      }
    }

    executeBatchPrint() {
      if (!this.currentPrintBatches || this.currentPrintBatches.length === 0) {
        this.showToast('error', 'لا توجد دفعات محددة للطباعة.');
        return;
      }

      const checkedIndices = [];
      document.querySelectorAll('#batch-print-modal-body .batch-print-item-chk:checked').forEach(c => {
        checkedIndices.push(parseInt(c.getAttribute('data-idx'), 10));
      });

      if (checkedIndices.length === 0) {
        this.showToast('error', 'يرجى تحديد دفعة واحدة على الأقل للطباعة.');
        return;
      }

      const selected = checkedIndices.map(idx => this.currentPrintBatches[idx]).filter(Boolean);
      const op = this.currentPrintOpInfo || {};
      const opNum = op.transactionFormattedNumber || op.operationNumber || `#${op.id || ''}`;
      const supplier = op.source || op.supplierName || selected[0]?.supplierName || '-';
      const locName = op.storageLocationName || selected[0]?.storageLocationName || 'المستودع الرئيسي';
      const printDate = new Date().toLocaleString('ar-EG', { dateStyle: 'full', timeStyle: 'short' });

      // Generate printable HTML
      const html = `
        <!DOCTYPE html>
        <html dir="rtl" lang="ar">
        <head>
          <meta charset="utf-8" />
          <title>بطاقات أرقام الدفعات المخزنية - ${opNum}</title>
          <style>
            @page {
              size: A4 portrait;
              margin: 12mm 10mm 12mm 10mm;
            }
            * {
              box-sizing: border-box;
              font-family: 'Segoe UI', Tahoma, Arial, sans-serif;
            }
            body {
              margin: 0;
              padding: 0;
              color: #0F172A;
              background: #fff;
              font-size: 11pt;
              line-height: 1.4;
            }
            .header-table {
              width: 100%;
              border-collapse: collapse;
              margin-bottom: 12px;
              border-bottom: 3px double #0F172A;
              padding-bottom: 8px;
            }
            .header-table td {
              vertical-align: middle;
            }
            .company-title {
              font-size: 16pt;
              font-weight: 900;
              color: #047857;
              margin: 0;
            }
            .dept-title {
              font-size: 13pt;
              font-weight: 800;
              color: #1E293B;
              margin: 2px 0 0 0;
            }
            .doc-subtitle {
              font-size: 10pt;
              color: #64748B;
              margin: 2px 0 0 0;
            }
            .meta-box {
              background: #F8FAFC;
              border: 1px solid #CBD5E1;
              border-radius: 6px;
              padding: 8px 12px;
              margin-bottom: 14px;
            }
            .meta-grid {
              display: grid;
              grid-template-columns: repeat(3, 1fr);
              gap: 6px;
              font-size: 9.5pt;
            }
            .meta-item span {
              color: #64748B;
            }
            .meta-item strong {
              color: #0F172A;
            }
            .cards-grid {
              display: grid;
              grid-template-columns: repeat(2, 1fr);
              gap: 12px;
              margin-bottom: 16px;
            }
            .batch-card {
              border: 2px solid #059669;
              border-radius: 8px;
              padding: 10px 12px;
              background: #fff;
              page-break-inside: avoid;
              position: relative;
            }
            .card-header {
              display: flex;
              justify-content: space-between;
              align-items: center;
              border-bottom: 1px dashed #A7F3D0;
              padding-bottom: 6px;
              margin-bottom: 8px;
            }
            .card-header .tag-badge {
              background: #059669;
              color: #fff;
              font-size: 8.5pt;
              font-weight: 700;
              padding: 2px 8px;
              border-radius: 4px;
            }
            .card-header .loc-text {
              font-size: 8.5pt;
              color: #64748B;
            }
            .batch-number-box {
              text-align: center;
              margin: 6px 0;
              padding: 4px 0;
              background: #F0FDF4;
              border: 1px solid #BBF7D0;
              border-radius: 6px;
            }
            .batch-number-box .b-num {
              font-family: 'Courier New', Courier, monospace;
              font-size: 20pt;
              font-weight: 900;
              color: #065F46;
              letter-spacing: 2px;
            }
            .barcode-lines {
              display: flex;
              justify-content: center;
              align-items: flex-end;
              gap: 2px;
              height: 22px;
              margin: 4px 0 2px 0;
            }
            .barcode-lines div {
              background: #0F172A;
              height: 100%;
            }
            .card-details {
              display: grid;
              grid-template-columns: 1fr 1fr;
              gap: 4px 8px;
              font-size: 9pt;
              margin-top: 6px;
            }
            .card-details .full-row {
              grid-column: 1 / -1;
            }
            .card-details span {
              color: #64748B;
            }
            .card-details strong {
              color: #0F172A;
            }
            .signatures-box {
              margin-top: 20px;
              padding-top: 10px;
              border-top: 1px solid #CBD5E1;
              display: grid;
              grid-template-columns: repeat(3, 1fr);
              text-align: center;
              font-size: 9.5pt;
              page-break-inside: avoid;
            }
            .sig-title {
              font-weight: 700;
              color: #334155;
              margin-bottom: 30px;
            }
            .sig-line {
              border-top: 1px dotted #94A3B8;
              width: 70%;
              margin: 0 auto;
            }
            @media print {
              body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            }
          </style>
        </head>
        <body>
          <table class="header-table">
            <tr>
              <td style="width: 70%;">
                <h1 class="company-title">شركة فيس ستون للمنتجات الأسمنتية والخرسانية</h1>
                <h2 class="dept-title">إدارة المستودعات والمخازن — بطاقات الدفعات المخزنية</h2>
                <p class="doc-subtitle">وثيقة تتبع مخزني معتمدة مرتبطة بالمعمل والرقابة والمتابعة</p>
              </td>
              <td style="width: 30%; text-align: left; font-size: 9pt; color: #475569;">
                <div><strong>تاريخ الطباعة:</strong> ${printDate}</div>
                <div><strong>رقم إذن الوارد:</strong> ${this.escapeHtml(opNum)}</div>
                <div><strong>عدد الدفعات المطبوعة:</strong> ${selected.length}</div>
              </td>
            </tr>
          </table>

          <div class="meta-box">
            <div class="meta-grid">
              <div class="meta-item"><span>رقم العينة المعملية:</span> <strong>${this.escapeHtml(op.sampleCode || selected[0]?.sampleCode || '-')}</strong></div>
              <div class="meta-item"><span>المرجع المعملي:</span> <strong>#${this.escapeHtml(op.productionReferenceNumber || selected[0]?.productionReferenceNumber || '-')}</strong></div>
              <div class="meta-item"><span>المورد / المصدر:</span> <strong>${this.escapeHtml(supplier)}</strong></div>
              <div class="meta-item"><span>مستودع التخزين:</span> <strong>${this.escapeHtml(locName)}</strong></div>
              <div class="meta-item"><span>إجمالي الكمية:</span> <strong>${selected.reduce((sum, b) => sum + Number(b.initialQuantity ?? b.currentQuantity ?? b.quantity ?? 0), 0).toFixed(3)} طن</strong></div>
              <div class="meta-item"><span>حالة المواد:</span> <strong>مطابق ومعتمد بالمعمل</strong></div>
            </div>
          </div>

          <div class="cards-grid">
            ${selected.map((b, idx) => {
              const bQty = Number(b.initialQuantity ?? b.currentQuantity ?? b.quantity ?? 0).toFixed(3);
              const classLabel = b.materialClassification === 'CHEMICAL' ? 'كيميائي' : 'ميكانيكي';
              const bNum = b.batchNumber || `B-${String(idx + 1).padStart(4, '0')}`;
              const sCode = b.sampleCode || op.sampleCode || '-';
              const pRef = b.productionReferenceNumber || op.productionReferenceNumber || '-';

              return `
                <div class="batch-card">
                  <div class="card-header">
                    <span class="tag-badge">بطاقة دفعة مخزنية معتمدة</span>
                    <span class="loc-text">${this.escapeHtml(locName)}</span>
                  </div>
                  <div class="batch-number-box">
                    <div class="b-num">${bNum}</div>
                    <div class="barcode-lines">
                      <div style="width:2px;"></div><div style="width:1px;"></div><div style="width:3px;"></div>
                      <div style="width:1px;"></div><div style="width:2px;"></div><div style="width:4px;"></div>
                      <div style="width:1px;"></div><div style="width:2px;"></div><div style="width:3px;"></div>
                      <div style="width:2px;"></div><div style="width:1px;"></div><div style="width:2px;"></div>
                      <div style="width:3px;"></div><div style="width:1px;"></div><div style="width:2px;"></div>
                      <div style="width:4px;"></div><div style="width:1px;"></div><div style="width:2px;"></div>
                    </div>
                  </div>
                  <div class="card-details">
                    <div class="full-row"><span>المادة:</span> <strong>${this.escapeHtml(b.materialName || 'مادة خام')} (${classLabel})</strong></div>
                    <div><span>الوزن الصافي:</span> <strong style="font-size:11pt; color:#059669;">${bQty} طن</strong></div>
                    <div><span>رقم العينة:</span> <strong>${this.escapeHtml(sCode)}</strong></div>
                    <div><span>المرجع المعملي:</span> <strong>#${this.escapeHtml(pRef)}</strong></div>
                    <div><span>المورد:</span> <strong>${this.escapeHtml(supplier)}</strong></div>
                    <div class="full-row" style="border-top:1px dashed #E2E8F0; padding-top:4px; margin-top:2px; font-size:8pt; color:#64748B;">
                      <span>تاريخ الورود: ${this.escapeHtml(this.formatDate(b.importDate || op.transactionDate || new Date()))}</span>
                      ${b.notes ? ` | <span>ملاحظات: ${this.escapeHtml(b.notes)}</span>` : ''}
                    </div>
                  </div>
                </div>
              `;
            }).join('')}
          </div>

          <div class="signatures-box">
            <div>
              <div class="sig-title">أمين المستودع المستلم</div>
              <div class="sig-line"></div>
            </div>
            <div>
              <div class="sig-title">مسؤول فحص الجودة والمعمل</div>
              <div class="sig-line"></div>
            </div>
            <div>
              <div class="sig-title">مدير إدارة الرقابة والمتابعة</div>
              <div class="sig-line"></div>
            </div>
          </div>
        </body>
        </html>
      `;

      // Trigger print via hidden iframe
      const iframe = document.createElement('iframe');
      iframe.style.position = 'fixed';
      iframe.style.right = '0';
      iframe.style.bottom = '0';
      iframe.style.width = '0';
      iframe.style.height = '0';
      iframe.style.border = '0';
      document.body.appendChild(iframe);

      iframe.contentDocument.open();
      iframe.contentDocument.write(html);
      iframe.contentDocument.close();

      setTimeout(() => {
        iframe.contentWindow.focus();
        iframe.contentWindow.print();
        setTimeout(() => {
          document.body.removeChild(iframe);
        }, 3000);
      }, 500);

      this.closeBatchPrintModal();
      this.showToast('success', `تم تجهيز بطاقات ${selected.length} دفعة للطباعة بنجاح.`);
    }

    // =========================================================================
    // Utilities & Toast
    // =========================================================================

    setFieldError(fieldId, errorMsg) {
      const errEl = document.getElementById(`err-${fieldId}`);
      if (errEl) {
        errEl.textContent = errorMsg;
        errEl.classList.add('visible');
      }
      const inputEl = document.getElementById(fieldId);
      inputEl?.classList.add('is-invalid');
    }

    clearFieldError(fieldId) {
      const errEl = document.getElementById(`err-${fieldId}`);
      if (errEl) {
        errEl.textContent = '';
        errEl.classList.remove('visible');
      }
      const inputEl = document.getElementById(fieldId);
      inputEl?.classList.remove('is-invalid');
    }

    clearAllErrors() {
      const errs = this.pageRoot?.querySelectorAll('.storage-field-error');
      errs?.forEach(e => {
        e.textContent = '';
        e.classList.remove('visible');
      });
      const inputs = this.pageRoot?.querySelectorAll('.is-invalid');
      inputs?.forEach(i => i.classList.remove('is-invalid'));
    }

    showToast(type, message) {
      if (!this.toastEl) return;
      this.toastEl.className = `storage-alert-toast ${type}`;
      if (this.toastMsg) this.toastMsg.textContent = message;
      if (this.toastIcon) {
        this.toastIcon.textContent = (type === 'success') ? '✓' : (type === 'error' ? '✕' : 'ℹ');
      }
      this.toastEl.style.display = 'flex';
      setTimeout(() => {
        if (this.toastEl) this.toastEl.style.display = 'none';
      }, 5000);
    }

    escapeHtml(str) {
      if (!str) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
    }

    formatDate(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        return d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'short', day: 'numeric' });
      } catch {
        return dateStr;
      }
    }

    formatDateTime(dateStr) {
      if (!dateStr) return '-';
      try {
        const d = new Date(dateStr);
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'numeric',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });
      } catch {
        return dateStr;
      }
    }
  }

  // Export Singleton
  window.StorageApp = new StorageManager();

})();
