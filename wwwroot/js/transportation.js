/**
 * FaceStone Transportation Department Management - 4-Section Ordered Workflow
 * Strictly adheres to FaceStone business requirements:
 * 1. الميزان (Internal & External Weighbridge, Departure/Arrival times entered ONLY here, Shipping Time calculation box, mandatory Attachment)
 * 2. يومية التوريد (Sequential Transaction Number per PO, Shipment Date, read-only weights from Section 1, Driver Phone, read-only Departure & Arrival times, Source, PO, mandatory Attachment)
 * 3. بوليصة الشحن (Company, Destination, Shipping Date, Quantity = Net Weight, Material Type powder/slurry, Supervisors, Plate, Driver, Recipient Name & Title, Delivery Status Checklist, Receiving Date, read-only Shipping Time, mandatory Attachment)
 * 4. اكرامية / ملاحظات (Decimal Ikramiya, Notes, Submit & Clear)
 * 
 * NOTE: Total Quantity and Remaining Quantity are NEVER displayed on this page.
 */
(function () {
  'use strict';

  class TransportationManager {
    constructor() {
      this.isSubmitting = false;
      this.config = null;
      this.currentPo = null;

      // Section file attachments
      this.wbAttachmentFile = null;
      this.dsAttachmentFile = null;
      this.bolAttachmentFile = null;

      // Edit / Resubmit State
      this.editingTxId = null;
      this.editingTx = null;

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
      this.pageRoot = document.getElementById('transportation-page-root');
      if (!this.pageRoot) return;

      // Steps
      this.stepPoEntry = document.getElementById('trans-step-po-entry');
      this.stepForm = document.getElementById('trans-step-form');
      this.stepSuccess = document.getElementById('trans-step-success');

      // User pill & Nav
      this.btnBackToDepts = document.getElementById('trans-back-to-departments-btn');
      this.userNameEl = document.getElementById('trans-user-name');
      this.userRoleEl = document.getElementById('trans-user-role');
      this.userAvatarEl = document.getElementById('trans-user-avatar');

      // Step 1: Warehouse Export Selection (The ONLY source option)
      this.transportSourceMode = 'EXPORT';
      this.exportSearchFilter = document.getElementById('trans-export-search-filter');
      this.exportInputWrap = document.getElementById('trans-export-input-wrap');
      this.exportSelect = document.getElementById('trans-export-select');
      this.exportDetailsCard = document.getElementById('trans-export-details-card');
      this.cardExpNum = document.getElementById('trans-card-exp-num');
      this.cardExpType = document.getElementById('trans-card-exp-type');
      this.cardExpTypeBadge = document.getElementById('trans-card-exp-type-badge');
      this.cardMaterial = document.getElementById('trans-card-material');
      this.cardClient = document.getElementById('trans-card-client');
      this.cardDest = document.getElementById('trans-card-dest');
      this.cardLocation = document.getElementById('trans-card-location');
      this.cardTotalQty = document.getElementById('trans-card-total-qty');
      this.cardAllocQty = document.getElementById('trans-card-alloc-qty');
      this.cardRemQty = document.getElementById('trans-card-rem-qty');
      this.cardRef = document.getElementById('trans-card-ref');
      this.cardBatch = document.getElementById('trans-card-batch');
      this.cardPoInfo = document.getElementById('trans-card-po-info');
      this.cardDate = document.getElementById('trans-card-date');
      this.eligibleExports = [];
      this.selectedExport = null;
      this.selectedExportItem = null;
      this.currentExport = null;

      this.poLookupForm = document.getElementById('trans-po-lookup-form');
      this.poContinueBtn = document.getElementById('trans-po-continue-btn');
      this.poSpinner = document.getElementById('trans-po-spinner');
      this.poContinueText = document.getElementById('trans-po-continue-text');
      this.poErrorAlert = document.getElementById('trans-po-error-alert');
      this.poErrorTitle = document.getElementById('trans-po-error-title');
      this.poErrorDesc = document.getElementById('trans-po-error-desc');
      this.poErrorClose = document.getElementById('trans-po-error-close');

      // Step 2: Form Header & Summary Bar (Section 1: Warehouse Export Information)
      this.btnChangePo = document.getElementById('trans-btn-change-po');
      this.summaryPo = document.getElementById('trans-summary-po');
      this.summaryExportType = document.getElementById('trans-summary-export-type');
      this.summaryMaterial = document.getElementById('trans-summary-material');
      this.summaryRefBatch = document.getElementById('trans-summary-ref-batch');
      this.summaryTotalQty = document.getElementById('trans-summary-total-qty');
      this.summaryAllocQty = document.getElementById('trans-summary-alloc-qty');
      this.summaryRemQty = document.getElementById('trans-summary-rem-qty');
      this.summarySupplier = document.getElementById('trans-summary-supplier');
      this.summaryDestination = document.getElementById('trans-summary-destination');
      this.summarySource = document.getElementById('trans-summary-source');
      this.exportItemsSection = document.getElementById('trans-export-items-section');
      this.exportItemsTbody = document.getElementById('trans-export-items-tbody');
      this.remainingAfterDisplay = document.getElementById('trans-remaining-after-display');
      this.quantityLimitWarning = document.getElementById('trans-quantity-limit-warning');

      // Section 2: Project Code (Optional)
      this.projectCodeSelect = document.getElementById('trans-project-code-select');

      // Form Container & Alert
      this.form = document.getElementById('trans-entry-form');
      this.alertBanner = document.getElementById('trans-form-alert');
      this.alertIcon = document.getElementById('trans-alert-icon');
      this.alertTitle = document.getElementById('trans-alert-title');
      this.alertDesc = document.getElementById('trans-alert-desc');
      this.alertCloseBtn = document.getElementById('trans-alert-close');

      // ==============================================================
      // 1. SECTION 1: الميزان
      // ==============================================================
      // 1.1 Internal Weighbridge
      this.wbInternalCodeInput = document.getElementById('trans-wb-internal-code');
      this.wbIntLoaded = document.getElementById('trans-wb-int-loaded');
      this.wbIntEmpty = document.getElementById('trans-wb-int-empty');
      this.wbIntNet = document.getElementById('trans-wb-int-net');
      this.wbIntEmptyTime = document.getElementById('trans-wb-int-empty-time');
      this.wbIntLoadedTime = document.getElementById('trans-wb-int-loaded-time');

      // 1.2 External Weighbridge
      this.wbExternalCodeInput = document.getElementById('trans-wb-external-code');
      this.wbExtLoaded = document.getElementById('trans-wb-ext-loaded');
      this.wbExtEmpty = document.getElementById('trans-wb-ext-empty');
      this.wbExtNet = document.getElementById('trans-wb-ext-net');
      this.wbExtEmptyTime = document.getElementById('trans-wb-ext-empty-time');
      this.wbExtLoadedTime = document.getElementById('trans-wb-ext-loaded-time');

      // Movement & Shipment Dates (Derived automatically from Weighbridge Timestamps)
      this.txDateInput = document.getElementById('trans-date-input');
      this.departureDisplay = document.getElementById('trans-departure-display');
      this.arrivalDisplay = document.getElementById('trans-arrival-display');

      // Shipping Time Box
      this.shippingTimeDisplay = document.getElementById('trans-shipping-time-display');
      this.shippingTimeError = document.getElementById('trans-shipping-time-error');

      // Section 1 Attachment: Weighbridge
      this.wbAttachmentInput = document.getElementById('trans-wb-attachment-input');
      this.wbDropzone = document.getElementById('trans-wb-dropzone');
      this.wbPlaceholder = document.getElementById('trans-wb-placeholder');
      this.wbFilePreview = document.getElementById('trans-wb-file-preview');
      this.wbFileName = document.getElementById('trans-wb-file-name');
      this.wbFileSize = document.getElementById('trans-wb-file-size');
      this.wbRemoveBtn = document.getElementById('trans-wb-remove-btn');
      this.wbAttachmentError = document.getElementById('trans-wb-attachment-error');

      // ==============================================================
      // 2. SECTION 2: يومية التوريد
      // ==============================================================
      this.txNumberInput = document.getElementById('trans-number-input');
      this.dailyShipDateDisplay = document.getElementById('trans-daily-ship-date-display');
      this.internalNetDisplay = document.getElementById('trans-internal-net-display');
      this.externalNetDisplay = document.getElementById('trans-external-net-display');
      this.netWeightDisplay = document.getElementById('trans-net-weight-display');
      this.wasteDisplay = document.getElementById('trans-waste-display');
      this.driverPhoneInput = document.getElementById('trans-driver-phone-input');
      this.dailyDepartureDisplay = document.getElementById('trans-daily-departure-display');
      this.dailyArrivalDisplay = document.getElementById('trans-daily-arrival-display');
      this.sourceDisplay = document.getElementById('trans-source-display');
      this.dailyPoDisplay = document.getElementById('trans-daily-po-display');

      // Section 2 Attachment: Daily Supply
      this.dsAttachmentInput = document.getElementById('trans-ds-attachment-input');
      this.dsDropzone = document.getElementById('trans-ds-dropzone');
      this.dsPlaceholder = document.getElementById('trans-ds-placeholder');
      this.dsFilePreview = document.getElementById('trans-ds-file-preview');
      this.dsFileName = document.getElementById('trans-ds-file-name');
      this.dsFileSize = document.getElementById('trans-ds-file-size');
      this.dsRemoveBtn = document.getElementById('trans-ds-remove-btn');
      this.dsAttachmentError = document.getElementById('trans-ds-attachment-error');

      // ==============================================================
      // 3. SECTION 3: بوليصة الشحن
      // ==============================================================
      this.companyInput = document.getElementById('trans-company-input');
      this.destinationInput = document.getElementById('trans-destination-input');
      this.shipDateDisplay = document.getElementById('trans-ship-date-display');
      this.quantityDisplay = document.getElementById('trans-quantity-display');
      this.materialSelect = document.getElementById('trans-material-select');
      this.shipSupSelect = document.getElementById('trans-ship-sup-select');
      this.deptSupSelect = document.getElementById('trans-dept-sup-select');
      this.carInput = document.getElementById('trans-car-input');
      this.driverSelect = document.getElementById('trans-driver-select');
      this.recNameInput = document.getElementById('trans-rec-name-input');
      this.recTitleSelect = document.getElementById('trans-rec-title-select');
      this.statusSafeCheckbox = document.getElementById('trans-status-safe');
      this.statusIntactCheckbox = document.getElementById('trans-status-intact');
      this.recStatusError = document.getElementById('trans-rec-status-error');
      this.recDateInput = document.getElementById('trans-rec-date-input');
      this.bolShippingTimeDisplay = document.getElementById('trans-bol-shipping-time-display');

      // Section 3 Attachment: Bill of Lading
      this.bolAttachmentInput = document.getElementById('trans-bol-attachment-input');
      this.bolDropzone = document.getElementById('trans-bol-dropzone');
      this.bolPlaceholder = document.getElementById('trans-bol-placeholder');
      this.bolFilePreview = document.getElementById('trans-bol-file-preview');
      this.bolFileName = document.getElementById('trans-bol-file-name');
      this.bolFileSize = document.getElementById('trans-bol-file-size');
      this.bolRemoveBtn = document.getElementById('trans-bol-remove-btn');
      this.bolAttachmentError = document.getElementById('trans-bol-attachment-error');

      // ==============================================================
      // 4. SECTION 4: اكرامية / ملاحظات
      // ==============================================================
      this.custodyInput = document.getElementById('trans-custody-input');
      this.ikramiyaInput = document.getElementById('trans-ikramiya-input');
      this.notesInput = document.getElementById('trans-notes-input');

      // Actions
      this.submitBtn = document.getElementById('trans-submit-btn');
      this.submitIcon = document.getElementById('trans-submit-icon');
      this.submitSpinner = document.getElementById('trans-submit-spinner');
      this.submitBtnText = document.getElementById('trans-submit-btn-text');
      this.clearBtn = document.getElementById('trans-clear-btn');
      this.refreshBtn = document.getElementById('trans-refresh-btn');

      // Confirmation Modal
      this.confirmModal = document.getElementById('trans-confirm-modal');
      this.confirmCancelBtn = document.getElementById('trans-confirm-cancel-btn');
      this.confirmProceedBtn = document.getElementById('trans-confirm-proceed-btn');

      // Success Step Elements
      this.successTxNumber = document.getElementById('trans-success-tx-number');
      this.successPoNumber = document.getElementById('trans-success-po-number');
      this.successCompany = document.getElementById('trans-success-company');
      this.successDestination = document.getElementById('trans-success-destination');
      this.successMaterial = document.getElementById('trans-success-material');
      this.successIntWeight = document.getElementById('trans-success-int-weight');
      this.successExtWeight = document.getElementById('trans-success-ext-weight');
      this.successNetWeight = document.getElementById('trans-success-net-weight');
      this.successWaste = document.getElementById('trans-success-waste');
      this.successShippingTime = document.getElementById('trans-success-shipping-time');
      this.btnNewPo = document.getElementById('trans-btn-new-po');
      this.btnViewRecentFromSuccess = document.getElementById('trans-btn-view-recent-from-success');

      // Recent Transactions Table & Filter Controls
      this.recentTableBody = document.getElementById('trans-recent-tbody');
      this.tableSearchInput = document.getElementById('trans-table-search-input');
      this.filterExportType = document.getElementById('trans-filter-export-type');
      this.filterProjectCode = document.getElementById('trans-filter-project-code');
      this.filterStatus = document.getElementById('trans-filter-status');
      this.allRecentRecords = [];

      this.bindEvents();
      this.setupAttachmentHandlers();
      this.loadConfig();
      this.loadEligibleExports();
    }

    bindEvents() {
      // Step 1: Export Search & Selection (Warehouse Export is the ONLY source)
      this.exportSearchFilter?.addEventListener('input', () => {
        this.filterExportOptions();
      });

      this.exportSelect?.addEventListener('change', () => {
        this.onExportSelected(this.exportSelect.value);
      });

      // Step 1: Form Submit (Continue to Step 2)
      this.poLookupForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handlePoLookup();
      });

      this.poErrorClose?.addEventListener('click', () => {
        if (this.poErrorAlert) this.poErrorAlert.style.display = 'none';
      });

      // Filter bar listeners for recent table
      this.tableSearchInput?.addEventListener('input', () => this.applyTableFilters());
      this.filterExportType?.addEventListener('change', () => this.applyTableFilters());
      this.filterProjectCode?.addEventListener('change', () => this.applyTableFilters());
      this.filterStatus?.addEventListener('change', () => this.applyTableFilters());

      // Change PO button
      this.btnChangePo?.addEventListener('click', () => {
        this.showPoEntryStep();
      });

      // Navigation back to departments
      this.btnBackToDepts?.addEventListener('click', () => {
        if (window.ChatApp?.showView) {
          window.ChatApp.showView('departments');
        }
      });

      // Weight inputs in Section 1 (ONLY location for weight input)
      [this.wbIntLoaded, this.wbIntEmpty, this.wbExtLoaded, this.wbExtEmpty].forEach(input => {
        input?.addEventListener('input', () => this.recalculateAllWeights());
      });

      // Movement & Shipment Dates: Populating Sections 2 & 3
      this.txDateInput?.addEventListener('input', () => {
        const val = this.formatDateTimeDisplay(this.txDateInput.value);
        if (this.dailyShipDateDisplay) this.dailyShipDateDisplay.value = val;
        if (this.shipDateDisplay) this.shipDateDisplay.value = val;
      });

      // Weighbridge Timestamps auto-deriving Departure, Arrival, and Shipping Time
      const onIntLoadedChange = () => {
        const val = this.formatDateTimeDisplay(this.wbIntLoadedTime.value);
        if (this.departureDisplay) this.departureDisplay.value = val;
        if (this.dailyDepartureDisplay) this.dailyDepartureDisplay.value = val;
        this.recalculateShippingTime();
      };
      this.wbIntLoadedTime?.addEventListener('input', onIntLoadedChange);
      this.wbIntLoadedTime?.addEventListener('change', onIntLoadedChange);

      const onExtLoadedChange = () => {
        const val = this.formatDateTimeDisplay(this.wbExtLoadedTime.value);
        if (this.arrivalDisplay) this.arrivalDisplay.value = val;
        if (this.dailyArrivalDisplay) this.dailyArrivalDisplay.value = val;
        this.recalculateShippingTime();
      };
      this.wbExtLoadedTime?.addEventListener('input', onExtLoadedChange);
      this.wbExtLoadedTime?.addEventListener('change', onExtLoadedChange);

      // Form Submission
      this.form?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleFormSubmit();
      });

      // Form Clear
      this.clearBtn?.addEventListener('click', () => {
        if (this.confirmModal) {
          this.confirmModal.style.display = 'flex';
        } else {
          this.resetFormKeepPo();
        }
      });
      this.confirmCancelBtn?.addEventListener('click', () => {
        if (this.confirmModal) this.confirmModal.style.display = 'none';
      });
      this.confirmProceedBtn?.addEventListener('click', () => {
        if (this.confirmModal) this.confirmModal.style.display = 'none';
        this.resetFormKeepPo();
      });

      // Alert close
      this.alertCloseBtn?.addEventListener('click', () => {
        this.hideAlert();
      });

      // Refresh recent table
      this.refreshBtn?.addEventListener('click', () => {
        this.loadRecentTransactions();
      });

      // Success Actions
      this.btnNewPo?.addEventListener('click', () => {
        this.showPoEntryStep();
      });
      this.btnViewRecentFromSuccess?.addEventListener('click', () => {
        const recentSec = document.getElementById('trans-recent-section');
        if (recentSec) {
          recentSec.scrollIntoView({ behavior: 'smooth' });
        }
      });
    }

    setupAttachmentHandlers() {
      // 1. Weighbridge Attachment
      this.setupSingleAttachment(
        this.wbAttachmentInput,
        this.wbDropzone,
        this.wbPlaceholder,
        this.wbFilePreview,
        this.wbFileName,
        this.wbFileSize,
        this.wbRemoveBtn,
        this.wbAttachmentError,
        (file) => { this.wbAttachmentFile = file; }
      );

      // 2. Daily Supply Attachment
      this.setupSingleAttachment(
        this.dsAttachmentInput,
        this.dsDropzone,
        this.dsPlaceholder,
        this.dsFilePreview,
        this.dsFileName,
        this.dsFileSize,
        this.dsRemoveBtn,
        this.dsAttachmentError,
        (file) => { this.dsAttachmentFile = file; }
      );

      // 3. Bill of Lading Attachment
      this.setupSingleAttachment(
        this.bolAttachmentInput,
        this.bolDropzone,
        this.bolPlaceholder,
        this.bolFilePreview,
        this.bolFileName,
        this.bolFileSize,
        this.bolRemoveBtn,
        this.bolAttachmentError,
        (file) => { this.bolAttachmentFile = file; }
      );
    }

    setupSingleAttachment(inputEl, dropzoneEl, placeholderEl, previewEl, nameEl, sizeEl, removeBtnEl, errorEl, setFileCallback) {
      if (!inputEl) return;

      const allowedExts = ['.pdf', '.jpg', '.jpeg'];
      const maxSizeBytes = 25 * 1024 * 1024; // 25 MB

      const handleSelectedFile = (file) => {
        if (!file) return;

        if (errorEl) {
          errorEl.textContent = '';
          errorEl.style.display = 'none';
        }

        const ext = '.' + file.name.split('.').pop().toLowerCase();
        if (!allowedExts.includes(ext)) {
          if (errorEl) {
            errorEl.textContent = 'نوع الملف غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG.';
            errorEl.style.display = 'block';
          }
          return;
        }

        if (file.size > maxSizeBytes) {
          if (errorEl) {
            errorEl.textContent = 'حجم الملف يتجاوز الحد الأقصى المسموح (25 ميجابايت).';
            errorEl.style.display = 'block';
          }
          return;
        }

        setFileCallback(file);

        if (nameEl) nameEl.textContent = file.name;
        if (sizeEl) sizeEl.textContent = this.formatFileSize(file.size);
        if (placeholderEl) placeholderEl.style.display = 'none';
        if (previewEl) previewEl.style.display = 'flex';
      };

      inputEl.addEventListener('change', () => {
        if (inputEl.files && inputEl.files[0]) {
          handleSelectedFile(inputEl.files[0]);
        }
      });

      removeBtnEl?.addEventListener('click', (e) => {
        e.stopPropagation();
        inputEl.value = '';
        setFileCallback(null);
        if (placeholderEl) placeholderEl.style.display = 'flex';
        if (previewEl) previewEl.style.display = 'none';
        if (errorEl) {
          errorEl.textContent = '';
          errorEl.style.display = 'none';
        }
      });

      // Drag & Drop
      if (dropzoneEl) {
        ['dragenter', 'dragover'].forEach(eventName => {
          dropzoneEl.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropzoneEl.classList.add('dragover');
          });
        });

        ['dragleave', 'drop'].forEach(eventName => {
          dropzoneEl.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropzoneEl.classList.remove('dragover');
          });
        });

        dropzoneEl.addEventListener('drop', (e) => {
          if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0]) {
            inputEl.files = e.dataTransfer.files;
            handleSelectedFile(e.dataTransfer.files[0]);
          }
        });
      }
    }

    formatFileSize(bytes) {
      if (bytes < 1024) return `${bytes} B`;
      if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
      return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    }

    async loadConfig() {
      try {
        const res = await window.ChatApi?.getTransportationConfig();
        if (res && res.success && res.data) {
          this.config = res.data;
          this.populateDropdowns(res.data);
          this.populateProjectCodes(res.data.projectCodes || []);
        }
      } catch (err) {
        console.error('Failed to load transportation config:', err);
      }
    }

    populateDropdowns(cfg) {
      if (this.driverSelect) {
        const currentVal = this.driverSelect.value;
        this.driverSelect.innerHTML = '<option value="" disabled selected>-- اختر السائق من السجلات المعتمدة --</option>';
        if (cfg.drivers && cfg.drivers.length > 0) {
          cfg.drivers.forEach(d => {
            const opt = document.createElement('option');
            opt.value = d;
            opt.textContent = d;
            this.driverSelect.appendChild(opt);
          });
        }
        if (currentVal) this.driverSelect.value = currentVal;
      }
    }

    populateProjectCodes(codes) {
      if (!Array.isArray(codes)) return;

      // 1. Populate form select (Section 2)
      if (this.projectCodeSelect) {
        const currentVal = this.projectCodeSelect.value;
        this.projectCodeSelect.innerHTML = '<option value="">-- غير محدد (بدون كود مشروع - شحنة عامة) --</option>';
        codes.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.id;
          opt.setAttribute('data-code', p.code);
          const clientPart = p.clientName ? ` (${p.clientName})` : '';
          opt.textContent = `[${p.code}] ${p.projectName}${clientPart}`;
          this.projectCodeSelect.appendChild(opt);
        });
        if (currentVal) this.projectCodeSelect.value = currentVal;
      }

      // 2. Populate filter dropdown in recent records table
      if (this.filterProjectCode) {
        const currentVal = this.filterProjectCode.value;
        this.filterProjectCode.innerHTML = [
          '<option value="all">كل أكواد المشاريع</option>',
          '<option value="none">غير محدد (بدون كود)</option>',
          ...codes.map(p => `<option value="${p.code}">[${p.code}] ${p.projectName}</option>`)
        ].join('');
        if (currentVal) this.filterProjectCode.value = currentVal;
      }
    }

    filterExportOptions() {
      if (!this.exportSelect) return;
      const term = (this.exportSearchFilter?.value || '').trim().toLowerCase();
      const currentSelectedVal = this.exportSelect.value;

      const filtered = !term ? this.eligibleExports : this.eligibleExports.filter(exp => {
        const num = (exp.exportTransactionNumber || `${exp.transactionNumber}`).toLowerCase();
        const client = (exp.clientName || '').toLowerCase();
        const mat = (exp.materialType || exp.materialName || '').toLowerCase();
        const dest = (exp.destinationName || '').toLowerCase();
        const ref = (exp.sampleCode || exp.batchNumber || exp.manufactureNumber || '').toLowerCase();
        return num.includes(term) || client.includes(term) || mat.includes(term) || dest.includes(term) || ref.includes(term);
      });

      if (filtered.length === 0) {
        this.exportSelect.innerHTML = '<option value="">-- لا توجد أذونات صرف مطابقة لبحثك --</option>';
        if (this.exportDetailsCard) this.exportDetailsCard.style.display = 'none';
        this.selectedExport = null;
        return;
      }

      this.exportSelect.innerHTML = [
        '<option value="">-- اختر إذن صرف المستودع المعتمد --</option>',
        ...filtered.map(exp => {
          const typeLabel = exp.exportType === 'FINISHED_PRODUCT' ? 'منتج نهائي' : 'مواد خام';
          const rem = Number(exp.remainingTransportationQuantity !== undefined ? exp.remainingTransportationQuantity : (exp.remainingQuantity || 0)).toFixed(3);
          const client = this.escapeHtml(exp.clientName || 'عميل');
          const mat = this.escapeHtml(exp.materialType || exp.materialName || (exp.items && exp.items[0]?.materialName) || 'بضاعة');
          return `<option value="${exp.exportTransactionNumber}">${exp.exportTransactionNumber} | [${typeLabel}] ${client} - ${mat} (رصيد: ${rem} طن)</option>`;
        })
      ].join('');

      if (currentSelectedVal && filtered.some(e => e.exportTransactionNumber === currentSelectedVal)) {
        this.exportSelect.value = currentSelectedVal;
        this.onExportSelected(currentSelectedVal);
      }
    }

    async loadEligibleExports() {
      if (!this.exportSelect) return;
      try {
        const res = await window.ChatApi?.getEligibleTransportationExports();
        if (res && res.success && Array.isArray(res.data)) {
          this.eligibleExports = res.data;
          this.filterExportOptions();
        }
      } catch (err) {
        console.error('Failed to load eligible exports for transportation:', err);
      }
    }

    onExportSelected(txNumber) {
      if (!txNumber) {
        this.selectedExport = null;
        if (this.exportDetailsCard) this.exportDetailsCard.style.display = 'none';
        return;
      }

      const exp = this.eligibleExports.find(e => 
        e.exportTransactionNumber === txNumber || 
        e.transactionNumber == txNumber || 
        `#${e.transactionNumber}` === txNumber
      );
      if (!exp) return;
      this.selectedExport = exp;

      const isFinished = exp.exportType === 'FINISHED_PRODUCT';
      const typeLabel = isFinished ? 'منتج نهائي' : 'مواد خام';
      const matName = exp.materialType || exp.materialName || (exp.items && exp.items[0]?.materialName) || 'بضاعة';
      const total = Number(exp.totalExportQuantity || exp.originalExportQuantity || 0);
      const alloc = Number(exp.transportedQuantity !== undefined ? exp.transportedQuantity : (exp.previouslyAllocatedQuantity || 0));
      const rem = Number(exp.remainingTransportationQuantity !== undefined ? exp.remainingTransportationQuantity : (exp.remainingQuantity || 0));

      if (this.exportDetailsCard) this.exportDetailsCard.style.display = 'block';
      if (this.cardExpNum) this.cardExpNum.textContent = exp.exportTransactionNumber;
      if (this.cardExpType) this.cardExpType.textContent = typeLabel;
      if (this.cardExpTypeBadge) {
        this.cardExpTypeBadge.textContent = typeLabel;
        this.cardExpTypeBadge.style.background = isFinished ? '#2563EB' : '#059669';
      }
      if (this.cardMaterial) this.cardMaterial.textContent = matName;
      if (this.cardClient) this.cardClient.textContent = exp.clientName || 'عميل';
      if (this.cardDest) this.cardDest.textContent = exp.destinationName || '-';
      if (this.cardLocation) this.cardLocation.textContent = exp.storageLocationName || '-';
      if (this.cardTotalQty) this.cardTotalQty.textContent = `${total.toFixed(3)} طن`;
      if (this.cardAllocQty) this.cardAllocQty.textContent = `${alloc.toFixed(3)} طن`;
      if (this.cardRemQty) this.cardRemQty.textContent = `${rem.toFixed(3)} طن`;

      const refNum = exp.sampleCode || (exp.productionReferenceNumber ? `#${exp.productionReferenceNumber}` : null) || (exp.items && exp.items[0]?.sampleCode) || '—';
      if (this.cardRef) this.cardRef.textContent = refNum;

      const batch = exp.batchNumber || (exp.items && exp.items.map(i => i.batchNumber).filter(Boolean).join(', ')) || '—';
      if (this.cardBatch) this.cardBatch.textContent = batch;

      const poInfo = exp.manufactureNumber || exp.productionOrderNumber || (exp.productionOrderId ? `PO-${exp.productionOrderId}` : '—');
      if (this.cardPoInfo) this.cardPoInfo.textContent = poInfo;

      if (this.cardDate) {
        this.cardDate.textContent = this.formatDateTimeDisplay(exp.exportDate || exp.transactionDate);
      }
    }

    showFormStepForExport(exp) {
      this.currentExport = exp;
      this.currentPo = null;

      if (this.stepPoEntry) this.stepPoEntry.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'none';
      if (this.stepForm) this.stepForm.style.display = 'block';

      const companyName = exp.clientName || 'عميل';
      const destination = exp.destinationName || 'وجهة العميل';
      const isFinished = exp.exportType === 'FINISHED_PRODUCT';
      const typeLabel = isFinished ? 'منتج نهائي' : 'مواد خام';
      const matName = exp.materialType || exp.materialName || (exp.items && exp.items[0]?.materialName) || 'بضاعة';
      const totalQty = Number(exp.totalExportQuantity || exp.originalExportQuantity || 0).toFixed(3);
      const allocQty = Number(exp.transportedQuantity !== undefined ? exp.transportedQuantity : (exp.previouslyAllocatedQuantity || 0)).toFixed(3);
      const rem = exp.remainingTransportationQuantity !== undefined ? exp.remainingTransportationQuantity : (exp.remainingQuantity || 0);
      const remQty = Number(rem).toFixed(3);

      const refOrSample = exp.sampleCode || (exp.items && exp.items[0]?.sampleCode);
      const batchOrMfg = exp.manufactureNumber || exp.batchNumber || (exp.items && exp.items[0]?.batchNumber);
      const refBatchText = [refOrSample, batchOrMfg].filter(Boolean).join(' / ') || '—';

      // Section 1: Warehouse Export Info Box
      if (this.summaryPo) this.summaryPo.textContent = exp.exportTransactionNumber;
      if (this.summaryExportType) this.summaryExportType.textContent = typeLabel;
      if (this.summaryMaterial) this.summaryMaterial.textContent = matName;
      if (this.summaryRefBatch) this.summaryRefBatch.textContent = refBatchText;
      if (this.summaryTotalQty) this.summaryTotalQty.textContent = `${totalQty} طن`;
      if (this.summaryAllocQty) this.summaryAllocQty.textContent = `${allocQty} طن`;
      if (this.summaryRemQty) this.summaryRemQty.textContent = `${remQty} طن`;
      if (this.summarySupplier) this.summarySupplier.textContent = `${companyName} (${destination})`;
      if (this.summaryDestination) this.summaryDestination.textContent = destination;
      if (this.summarySource) this.summarySource.textContent = exp.storageLocationName ? `مستودع: ${exp.storageLocationName}` : 'Face stone (المستودع الرئيسي)';

      // Render Multi-line Items Table if present
      this.renderExportItemsTable(exp.items || []);

      // Section 2: Project Code (Optional) - auto-select if project matches destination
      if (this.projectCodeSelect) {
        let matched = '';
        if (destination) {
          for (let i = 0; i < this.projectCodeSelect.options.length; i++) {
            const opt = this.projectCodeSelect.options[i];
            if (opt.value && opt.textContent.includes(destination)) {
              matched = opt.value;
              break;
            }
          }
        }
        this.projectCodeSelect.value = matched;
      }

      // Section 2: Daily Supply PO display
      if (this.dailyPoDisplay) this.dailyPoDisplay.value = exp.exportTransactionNumber;

      // Section 3: Prepopulated & Read-only fields
      if (this.companyInput) {
        this.companyInput.value = companyName;
        this.companyInput.readOnly = true;
      }
      if (this.destinationInput) {
        this.destinationInput.value = destination;
        this.destinationInput.readOnly = true;
      }

      // Material Type
      if (this.materialSelect) {
        const matLower = (matName || '').toLowerCase();
        if (matLower.includes('slurry')) {
          this.materialSelect.value = 'slurry';
        } else {
          this.materialSelect.value = 'powder';
        }
      }

      // Default transaction number & clear weighbridge codes
      if (this.txNumberInput) {
        this.txNumberInput.value = (exp.transactionNumber || 1);
      }
      if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = '';
      if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = '';

      // Set default current timestamps
      this.setDefaultTimestamps();

      // Recalculate weights & initialize preview
      this.recalculateAllWeights();
      this.hideAlert();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    renderExportItemsTable(items) {
      if (!this.exportItemsSection || !this.exportItemsTbody) return;

      if (!Array.isArray(items) || items.length === 0) {
        this.exportItemsSection.style.display = 'none';
        this.selectedExportItem = null;
        return;
      }

      // Show table
      this.exportItemsSection.style.display = 'block';
      this.exportItemsTbody.innerHTML = '';

      // Default selected item is the first one with remaining > 0, or items[0]
      const defaultItem = items.find(i => Number(i.remainingQuantity !== undefined ? i.remainingQuantity : (i.remainingTransportableQuantity || 0)) > 0) || items[0];
      this.selectedExportItem = defaultItem;

      items.forEach((item, idx) => {
        const isSelected = this.selectedExportItem && this.selectedExportItem.id === item.id;
        const exportedQty = Number(item.quantity || item.originalExportQuantity || 0).toFixed(3);
        const allocQty = Number(item.previouslyAllocatedQuantity !== undefined ? item.previouslyAllocatedQuantity : (item.transportedQuantity || 0)).toFixed(3);
        const remQty = Number(item.remainingQuantity !== undefined ? item.remainingQuantity : (item.remainingTransportableQuantity || 0)).toFixed(3);
        const refOrBatch = [item.sampleCode, item.batchNumber || item.manufactureNumber].filter(Boolean).join(' / ') || '—';

        const tr = document.createElement('tr');
        tr.id = `trans-item-row-${item.id}`;
        tr.style.cursor = 'pointer';
        tr.style.transition = 'background 0.2s';
        if (isSelected) {
          tr.style.background = '#EFF6FF';
          tr.style.fontWeight = 'bold';
        }

        tr.innerHTML = `
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0;">
            <input type="radio" name="trans-selected-item-radio" value="${item.id}" ${isSelected ? 'checked' : ''} style="cursor: pointer; width: 18px; height: 18px;" />
          </td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0;">${idx + 1}</td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0; font-weight: 700; color: #1E3A8A;">${this.escapeHtml(item.materialName || 'خامة')}</td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0; font-family: monospace;">${this.escapeHtml(refOrBatch)}</td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0; font-family: monospace;">${exportedQty} طن</td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0; font-family: monospace; color: #D97706;">${allocQty} طن</td>
          <td style="padding: 8px 10px; border: 1px solid #E2E8F0; font-family: monospace; font-weight: 800; color: #059669;">${remQty} طن</td>
        `;

        tr.addEventListener('click', () => {
          const radio = tr.querySelector('input[type="radio"]');
          if (radio) radio.checked = true;
          this.onExportItemSelected(item.id);
        });

        this.exportItemsTbody.appendChild(tr);
      });
    }

    onExportItemSelected(itemId) {
      if (!this.currentExport || !Array.isArray(this.currentExport.items)) return;
      const item = this.currentExport.items.find(i => i.id === itemId);
      if (!item) return;

      this.selectedExportItem = item;

      // Highlight selected row in table
      if (this.exportItemsTbody) {
        const rows = this.exportItemsTbody.querySelectorAll('tr');
        rows.forEach(r => {
          r.style.background = '';
          r.style.fontWeight = 'normal';
        });
        const activeRow = document.getElementById(`trans-item-row-${item.id}`);
        if (activeRow) {
          activeRow.style.background = '#EFF6FF';
          activeRow.style.fontWeight = 'bold';
        }
      }

      // Update summary card with this specific item's metadata and balances
      if (this.summaryMaterial) this.summaryMaterial.textContent = item.materialName || 'بضاعة';
      const refOrSample = item.sampleCode || this.currentExport.sampleCode;
      const batchOrMfg = item.manufactureNumber || item.batchNumber;
      if (this.summaryRefBatch) this.summaryRefBatch.textContent = [refOrSample, batchOrMfg].filter(Boolean).join(' / ') || '—';

      const exported = Number(item.quantity || item.originalExportQuantity || 0).toFixed(3);
      const allocated = Number(item.previouslyAllocatedQuantity !== undefined ? item.previouslyAllocatedQuantity : (item.transportedQuantity || 0)).toFixed(3);
      const remaining = Number(item.remainingQuantity !== undefined ? item.remainingQuantity : (item.remainingTransportableQuantity || 0)).toFixed(3);

      if (this.summaryTotalQty) this.summaryTotalQty.textContent = `${exported} طن`;
      if (this.summaryAllocQty) this.summaryAllocQty.textContent = `${allocated} طن`;
      if (this.summaryRemQty) this.summaryRemQty.textContent = `${remaining} طن`;

      // Update remaining balance preview
      const currentNet = Number(this.netWeightDisplay?.value || 0);
      this.updateRemainingPreview(currentNet);
    }

    updateRemainingPreview(currentNet) {
      const availableRem = this.selectedExportItem 
        ? Number(this.selectedExportItem.remainingQuantity !== undefined ? this.selectedExportItem.remainingQuantity : (this.selectedExportItem.remainingTransportableQuantity || 0))
        : Number(this.currentExport ? (this.currentExport.remainingTransportationQuantity !== undefined ? this.currentExport.remainingTransportationQuantity : (this.currentExport.remainingQuantity || 0)) : 0);

      const net = Math.max(0, Number(currentNet) || 0);
      const remAfter = Math.max(0, availableRem - net);

      if (this.remainingAfterDisplay) {
        this.remainingAfterDisplay.textContent = remAfter.toFixed(3);
      }

      if (this.quantityLimitWarning) {
        if (net > availableRem && availableRem > 0) {
          this.quantityLimitWarning.style.display = 'block';
          this.quantityLimitWarning.textContent = `⚠️ تنبيه: الوزن الصافي للشحنة (${net.toFixed(3)} طن) يتجاوز الرصيد المتاح للنقل (${availableRem.toFixed(3)} طن)! يرجى تصحيح أوزان الميزان.`;
        } else if (availableRem <= 0) {
          this.quantityLimitWarning.style.display = 'block';
          this.quantityLimitWarning.textContent = `⚠️ تنبيه: تم استنفاد كامل الرصيد المتاح للنقل لهذا البند مسبقاً.`;
        } else {
          this.quantityLimitWarning.style.display = 'none';
        }
      }
    }

    showPoEntryStep() {
      if (this.stepPoEntry) this.stepPoEntry.style.display = 'block';
      if (this.stepForm) this.stepForm.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'none';
      if (this.poErrorAlert) this.poErrorAlert.style.display = 'none';
      if (this.exportSearchFilter) this.exportSearchFilter.value = '';
      this.loadEligibleExports();
    }

    assignNextTransactionAndCodes(poData) {
      const nextNum = poData?.nextTransactionNumber || 1;
      if (this.txNumberInput) {
        this.txNumberInput.value = nextNum;
      }

      // Requirements 1, 1A, 1B: Weighbridge Reference numbers must be entered MANUALLY by employee.
      // Must NOT be generated or driven automatically by the system.
      if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = '';
      if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = '';
    }

    setDefaultTimestamps() {
      const now = new Date();
      const pad = (n) => String(n).padStart(2, '0');
      const nowLocal = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;

      if (this.txDateInput && !this.txDateInput.value) {
        this.txDateInput.value = nowLocal;
      }

      const formatted = this.formatDateTimeDisplay(this.txDateInput?.value || nowLocal);
      if (this.dailyShipDateDisplay) this.dailyShipDateDisplay.value = formatted;
      if (this.shipDateDisplay) this.shipDateDisplay.value = formatted;

      // Default weighbridge times
      if (this.wbIntEmptyTime && !this.wbIntEmptyTime.value) {
        this.wbIntEmptyTime.value = nowLocal;
      }
      if (this.wbIntLoadedTime && !this.wbIntLoadedTime.value) {
        this.wbIntLoadedTime.value = nowLocal;
      }
      if (this.wbExtLoadedTime && !this.wbExtLoadedTime.value) {
        this.wbExtLoadedTime.value = nowLocal;
      }

      // Derive initial Departure & Arrival times from Loaded Timestamps
      const depStr = this.formatDateTimeDisplay(this.wbIntLoadedTime?.value || nowLocal);
      if (this.departureDisplay) this.departureDisplay.value = depStr;
      if (this.dailyDepartureDisplay) this.dailyDepartureDisplay.value = depStr;

      const arrStr = this.formatDateTimeDisplay(this.wbExtLoadedTime?.value || nowLocal);
      if (this.arrivalDisplay) this.arrivalDisplay.value = arrStr;
      if (this.dailyArrivalDisplay) this.dailyArrivalDisplay.value = arrStr;
    }

    formatDateTimeDisplay(dtString) {
      if (!dtString) return '—';
      try {
        const d = new Date(dtString);
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        });
      } catch {
        return dtString;
      }
    }

    async handlePoLookup() {
      // Warehouse Export is the ONLY source for transportation operations
      if (!this.selectedExport) {
        const val = this.exportSelect?.value;
        if (val) {
          this.onExportSelected(val);
        }
      }

      if (!this.selectedExport) {
        this.showPoError('إذن الصرف مطلوب', 'يرجى اختيار إذن صرف مستودع معتمد ومتاح للنقل للمتابعة.');
        return;
      }

      this.showFormStepForExport(this.selectedExport);
    }

    showPoError(title, desc) {
      if (this.poErrorTitle) this.poErrorTitle.textContent = title;
      if (this.poErrorDesc) this.poErrorDesc.textContent = desc;
      if (this.poErrorAlert) this.poErrorAlert.style.display = 'flex';
      this.exportSelect?.focus();
    }

    setPoLookupLoading(isLoading) {
      if (this.poContinueBtn) this.poContinueBtn.disabled = isLoading;
      if (this.poSpinner) this.poSpinner.style.display = isLoading ? 'inline-block' : 'none';
      if (this.poContinueText) this.poContinueText.textContent = isLoading ? 'جاري التحقق...' : 'Continue';
    }

    /**
     * Weight Calculations (Driven strictly and exclusively from Section 1: الميزان)
     * Internal Net Weight = Internal Loaded - Internal Empty
     * External Net Weight = External Loaded - External Empty
     * Net Weight (الوزن الصافي) = MIN(Internal Net, External Net)
     * Waste (كمية هالك) = ABS(Internal Net - External Net)
     * Quantity in Bill of Lading (الكمية) = Net Weight
     */
    recalculateAllWeights() {
      const intLoaded = parseFloat(this.wbIntLoaded?.value);
      const intEmpty = parseFloat(this.wbIntEmpty?.value);
      const extLoaded = parseFloat(this.wbExtLoaded?.value);
      const extEmpty = parseFloat(this.wbExtEmpty?.value);

      const hasIntLoaded = !isNaN(intLoaded) && intLoaded > 0;
      const hasIntEmpty = !isNaN(intEmpty) && intEmpty > 0;
      const hasExtLoaded = !isNaN(extLoaded) && extLoaded > 0;
      const hasExtEmpty = !isNaN(extEmpty) && extEmpty > 0;

      // 1. Internal Net Weight
      let intNet = 0;
      if (hasIntLoaded && hasIntEmpty) {
        intNet = intLoaded - intEmpty;
        const valStr = intNet.toFixed(3);
        if (this.wbIntNet) this.wbIntNet.value = valStr;
        if (this.internalNetDisplay) this.internalNetDisplay.value = valStr;
      } else {
        if (this.wbIntNet) this.wbIntNet.value = '0.000';
        if (this.internalNetDisplay) this.internalNetDisplay.value = '0.000';
      }

      // 2. External Net Weight
      let extNet = 0;
      if (hasExtLoaded && hasExtEmpty) {
        extNet = extLoaded - extEmpty;
        const valStr = extNet.toFixed(3);
        if (this.wbExtNet) this.wbExtNet.value = valStr;
        if (this.externalNetDisplay) this.externalNetDisplay.value = valStr;
      } else {
        if (this.wbExtNet) this.wbExtNet.value = '0.000';
        if (this.externalNetDisplay) this.externalNetDisplay.value = '0.000';
      }

      // 3. Net Weight and Waste Quantity
      if (hasIntLoaded && hasIntEmpty && hasExtLoaded && hasExtEmpty) {
        let waste = 0;
        let netWeight = 0;

        if (intNet === extNet) {
          waste = 0;
          netWeight = intNet;
        } else {
          waste = Math.abs(intNet - extNet);
          netWeight = Math.min(intNet, extNet);
        }

        if (this.wasteDisplay) this.wasteDisplay.value = waste.toFixed(3);
        if (this.netWeightDisplay) this.netWeightDisplay.value = netWeight.toFixed(3);
        if (this.quantityDisplay) this.quantityDisplay.value = netWeight.toFixed(3);
        this.updateRemainingPreview(netWeight);
      } else {
        if (this.wasteDisplay) this.wasteDisplay.value = '0.000';
        if (this.netWeightDisplay) this.netWeightDisplay.value = '0.000';
        if (this.quantityDisplay) {
          const availableRem = this.selectedExportItem 
            ? Number(this.selectedExportItem.remainingQuantity !== undefined ? this.selectedExportItem.remainingQuantity : (this.selectedExportItem.remainingTransportableQuantity || 0))
            : Number(this.currentExport ? (this.currentExport.remainingTransportationQuantity !== undefined ? this.currentExport.remainingTransportationQuantity : (this.currentExport.remainingQuantity || 0)) : 0);
          this.quantityDisplay.value = availableRem > 0 ? availableRem.toFixed(3) : '0.000';
        }
        this.updateRemainingPreview(0);
      }
    }

    /**
     * Shipping Time Calculation:
     * Shipping Time = External Loaded Weighing Date/Time − Internal Loaded Weighing Date/Time
     * Format: HH:MM
     */
    recalculateShippingTime() {
      const startTimeVal = this.wbIntLoadedTime?.value;
      const endTimeVal = this.wbExtLoadedTime?.value;

      if (!startTimeVal || !endTimeVal) {
        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = '—';
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = '—';
        if (this.shippingTimeError) this.shippingTimeError.style.display = 'none';
        return;
      }

      const start = new Date(startTimeVal).getTime();
      const end = new Date(endTimeVal).getTime();

      if (end < start) {
        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = 'خطأ في التوقيت';
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = 'خطأ في التوقيت';
        if (this.shippingTimeError) {
          this.shippingTimeError.textContent = 'وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم.';
          this.shippingTimeError.style.display = 'block';
        }
      } else {
        if (this.shippingTimeError) this.shippingTimeError.style.display = 'none';
        const diffMs = end - start;
        const totalMinutes = Math.floor(diffMs / (1000 * 60));
        const hours = Math.floor(totalMinutes / 60);
        const mins = totalMinutes % 60;

        const pad = (n) => String(n).padStart(2, '0');
        const formattedTime = `${pad(hours)}:${pad(mins)}`;

        if (this.shippingTimeDisplay) this.shippingTimeDisplay.textContent = formattedTime;
        if (this.bolShippingTimeDisplay) this.bolShippingTimeDisplay.value = formattedTime;
      }
    }

    validateForm() {
      let isValid = true;
      this.clearAllFieldErrors();

      // SECTION 1 VALIDATIONS:
      // Manual Reference Codes (Requirements 1, 1A, 1B, 15)
      if (!this.wbInternalCodeInput?.value?.trim()) {
        this.setFieldError(this.wbInternalCodeInput, 'رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً.');
        isValid = false;
      }
      if (!this.wbExternalCodeInput?.value?.trim()) {
        this.setFieldError(this.wbExternalCodeInput, 'رقم مرجع الميزان الخارجي مطلوب ويجب إدخاله يدوياً.');
        isValid = false;
      }

      // Internal Weights
      const intLoaded = parseFloat(this.wbIntLoaded?.value);
      const intEmpty = parseFloat(this.wbIntEmpty?.value);
      if (isNaN(intLoaded) || intLoaded <= 0) {
        this.setFieldError(this.wbIntLoaded, 'الوزن الداخلي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (isNaN(intEmpty) || intEmpty <= 0) {
        this.setFieldError(this.wbIntEmpty, 'الوزن الداخلي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (!isNaN(intLoaded) && !isNaN(intEmpty) && intLoaded <= intEmpty) {
        this.setFieldError(this.wbIntLoaded, 'الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ.');
        isValid = false;
      }

      // External Weights
      const extLoaded = parseFloat(this.wbExtLoaded?.value);
      const extEmpty = parseFloat(this.wbExtEmpty?.value);
      if (isNaN(extLoaded) || extLoaded <= 0) {
        this.setFieldError(this.wbExtLoaded, 'الوزن الخارجي قائم مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (isNaN(extEmpty) || extEmpty <= 0) {
        this.setFieldError(this.wbExtEmpty, 'الوزن الخارجي فارغ مطلوب ويجب أن يكون أكبر من الصفر.');
        isValid = false;
      }
      if (!isNaN(extLoaded) && !isNaN(extEmpty) && extLoaded <= extEmpty) {
        this.setFieldError(this.wbExtLoaded, 'الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ.');
        isValid = false;
      }

      // Shipment Date
      if (!this.txDateInput?.value) {
        this.setFieldError(this.txDateInput, 'تاريخ الشحنة مطلوب.');
        isValid = false;
      }

      // Loaded Timestamps (Single Source of Truth for Departure & Arrival)
      if (!this.wbIntLoadedTime?.value) {
        this.setFieldError(this.wbIntLoadedTime, 'تاريخ ووقت الوزن القائم الداخلي مطلوب لتحديد تاريخ المغادرة.');
        isValid = false;
      }
      if (!this.wbExtLoadedTime?.value) {
        this.setFieldError(this.wbExtLoadedTime, 'تاريخ ووقت الوزن القائم الخارجي مطلوب لتحديد تاريخ الوصول.');
        isValid = false;
      }

      // Shipping Time check: External Loaded must be >= Internal Loaded
      const startTimeVal = this.wbIntLoadedTime?.value;
      const endTimeVal = this.wbExtLoadedTime?.value;
      if (startTimeVal && endTimeVal) {
        const start = new Date(startTimeVal).getTime();
        const end = new Date(endTimeVal).getTime();
        if (end < start) {
          if (this.shippingTimeError) {
            this.shippingTimeError.textContent = 'وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم.';
            this.shippingTimeError.style.display = 'block';
          }
          isValid = false;
        }
      }

      // Section 1 Mandatory Attachment Check
      if (!this.wbAttachmentFile) {
        const hasExisting = this.editingTx && (this.editingTx.weighbridgeFileName || this.editingTx.weighbridgeAttachmentPath);
        if (!hasExisting) {
          this.setAttachmentError(this.wbAttachmentError, 'Attachment is required for this section. (مرفق قسم الميزان مطلوب)');
          isValid = false;
        }
      }

      // SECTION 2 VALIDATIONS:
      // Section 2 Mandatory Attachment Check
      if (!this.dsAttachmentFile) {
        const hasExisting = this.editingTx && (this.editingTx.dailySupplyFileName || this.editingTx.dailySupplyAttachmentPath);
        if (!hasExisting) {
          this.setAttachmentError(this.dsAttachmentError, 'Attachment is required for this section. (مرفق قسم يومية التوريد مطلوب)');
          isValid = false;
        }
      }

      // SECTION 3 VALIDATIONS:
      // Material Type
      const mat = this.materialSelect?.value;
      if (!mat || (mat !== 'powder' && mat !== 'slurry')) {
        this.setFieldError(this.materialSelect, 'يرجى اختيار نوع الخامة (powder أو slurry فقط).');
        isValid = false;
      }

      // Car Number
      if (!this.carInput?.value?.trim()) {
        this.setFieldError(this.carInput, 'رقم السيارة مطلوب.');
        isValid = false;
      }

      // Driver
      if (!this.driverSelect?.value?.trim()) {
        this.setFieldError(this.driverSelect, 'يرجى اختيار السائق من القائمة المعتمدة.');
        isValid = false;
      }

      // Delivery Status Checklist (Requirement 37)
      const isSafe = this.statusSafeCheckbox?.checked;
      const isIntact = this.statusIntactCheckbox?.checked;
      if (!isSafe && !isIntact) {
        if (this.recStatusError) {
          this.recStatusError.textContent = 'حالة الاستلام مطلوبة. يجب تحديد (safe أو intact أو كلاهما).';
          this.recStatusError.style.display = 'block';
        }
        isValid = false;
      }

      // Section 3 Mandatory Attachment Check
      if (!this.bolAttachmentFile) {
        const hasExisting = this.editingTx && (this.editingTx.billOfLadingFileName || this.editingTx.billOfLadingAttachmentPath);
        if (!hasExisting) {
          this.setAttachmentError(this.bolAttachmentError, 'Attachment is required for this section. (مرفق قسم بوليصة الشحن مطلوب)');
          isValid = false;
        }
      }

      // SECTION 4 VALIDATIONS:
      // Custody (العهدة - Required decimal >= 0, Requirements 6, 7, 15)
      const custodyVal = parseFloat(this.custodyInput?.value);
      if (isNaN(custodyVal) || custodyVal < 0) {
        this.setFieldError(this.custodyInput, 'العهدة مطلوبة ويجب إدخال قيمة رقمية صحيحة (صفر أو أكثر).');
        isValid = false;
      }

      // Warehouse Export Balance Check: Net Weight <= Remaining Balance
      const netWeight = parseFloat(this.netWeightDisplay?.value) || 0;
      if (this.currentExport) {
        const itemRem = this.selectedExportItem 
          ? (parseFloat(this.selectedExportItem.remainingQuantity !== undefined ? this.selectedExportItem.remainingQuantity : this.selectedExportItem.remainingTransportableQuantity) || 0)
          : null;
        const totalRemaining = parseFloat(this.currentExport.remainingTransportationQuantity !== undefined ? this.currentExport.remainingTransportationQuantity : this.currentExport.remainingQuantity) || 0;

        if (itemRem !== null && netWeight > itemRem) {
          this.showAlert(`الوزن الصافي للشحنة (${netWeight} طن) يتجاوز الرصيد المتبقي للبند المحدد '${this.selectedExportItem.materialName}' (${itemRem} طن).`, 'error');
          isValid = false;
        } else if (netWeight > totalRemaining) {
          this.showAlert(`الوزن الصافي للشحنة (${netWeight} طن) يتجاوز الرصيد المتبقي المتاح للنقل في إذن الصرف (${totalRemaining} طن).`, 'error');
          isValid = false;
        }
      }

      return isValid;
    }

    setAttachmentError(el, msg) {
      if (el) {
        el.textContent = msg;
        el.style.display = 'block';
      }
    }

    async handleFormSubmit() {
      if (this.isSubmitting) return;

      if (!this.validateForm()) {
        this.showAlert('يرجى تصحيح الأخطاء الموضحة وتوفير المرفقات الإلزامية لكل قسم قبل الحفظ.', 'error');
        return;
      }

      this.setSubmitLoading(true);
      this.hideAlert();

      try {
        const formData = new FormData();

        const txNum = parseInt(this.txNumberInput?.value, 10) || 0;
        formData.append('transactionNumber', txNum);
        formData.append('transactionDate', this.txDateInput.value);
        formData.append('carNumber', this.carInput.value.trim());
        formData.append('driverName', this.driverSelect.value.trim());
        formData.append('source', 'Face stone (Full Concrete Solutions)');
        
        if (this.currentExport) {
          formData.append('storageOperationId', this.currentExport.storageOperationId || this.currentExport.id);
          if (this.selectedExportItem?.id) {
            formData.append('storageOperationItemId', this.selectedExportItem.id);
          }
          formData.append('exportTransactionNumber', this.currentExport.exportTransactionNumber);
          formData.append('exportType', this.currentExport.exportType || 'RAW_MATERIAL');
          formData.append('poNumber', this.currentExport.exportTransactionNumber);
          if (this.currentExport.productionOrderId) formData.append('productionOrderId', this.currentExport.productionOrderId);
          if (this.currentExport.productionProcessId) formData.append('productionProcessId', this.currentExport.productionProcessId);
          const mfg = this.selectedExportItem?.manufactureNumber || this.currentExport.manufactureNumber;
          if (mfg) formData.append('manufactureNumber', mfg);
          const batch = this.selectedExportItem?.batchNumber || this.currentExport.batchNumber || (this.currentExport.items && this.currentExport.items[0]?.batchNumber);
          if (batch) formData.append('batchNumber', batch);
          const sample = this.selectedExportItem?.sampleCode || this.currentExport.sampleCode || (this.currentExport.items && this.currentExport.items[0]?.sampleCode);
          if (sample) formData.append('sampleCode', sample);
          if (this.currentExport.storageLocationId) formData.append('sourceStorageLocationId', this.currentExport.storageLocationId);
          if (this.currentExport.storageLocationName) formData.append('sourceStorageLocationName', this.currentExport.storageLocationName);
        } else if (this.editingTx) {
          if (this.editingTx.storageOperationId) formData.append('storageOperationId', this.editingTx.storageOperationId);
          formData.append('exportTransactionNumber', this.editingTx.exportTransactionNumber || this.editingTx.poNumber);
          formData.append('exportType', this.editingTx.exportType || 'RAW_MATERIAL');
          formData.append('poNumber', this.editingTx.exportTransactionNumber || this.editingTx.poNumber);
        }

        // Section 2: Project Code (Optional)
        if (this.projectCodeSelect && this.projectCodeSelect.value) {
          const selectedOpt = this.projectCodeSelect.options[this.projectCodeSelect.selectedIndex];
          formData.append('projectCodeId', this.projectCodeSelect.value);
          const codeVal = selectedOpt?.getAttribute('data-code') || '';
          if (codeVal) formData.append('projectCode', codeVal);
        }

        formData.append('destination', this.destinationInput?.value || this.currentExport?.destinationName || 'Cairo Factory');
        formData.append('materialType', this.materialSelect.value.trim());

        // Weights (Entered ONLY in Section 1)
        formData.append('weightBeforeLoading', this.wbIntEmpty.value);
        formData.append('weightWhenExitingFactory', this.wbIntLoaded.value);
        formData.append('externalLoadedWeight', this.wbExtLoaded.value);
        formData.append('externalEmptyWeight', this.wbExtEmpty.value);

        // Movement Dates
        if (this.departureInput?.value) {
          formData.append('departureTime', this.departureInput.value);
        }
        if (this.arrivalInput?.value) {
          formData.append('arrivalTime', this.arrivalInput.value);
        }

        // Driver Phone
        if (this.driverPhoneInput?.value?.trim()) {
          formData.append('driverPhone', this.driverPhoneInput.value.trim());
        }

        // Supervisors & Recipient
        formData.append('shippingSupervisor', 'محمد ناجح');
        formData.append('departmentSupervisor', 'ازهر محمد');

        // Checklist Delivery Status (Requirement 37)
        const checklist = [];
        if (this.statusSafeCheckbox?.checked) checklist.push('safe');
        if (this.statusIntactCheckbox?.checked) checklist.push('intact');
        formData.append('receivingStatus', checklist.join(', '));

        if (this.recNameInput?.value?.trim()) {
          formData.append('recipientName', this.recNameInput.value.trim());
        }
        if (this.recTitleSelect?.value?.trim()) {
          formData.append('recipientTitle', this.recTitleSelect.value.trim());
        }
        if (this.recDateInput?.value) {
          formData.append('receivedDate', this.recDateInput.value);
        }

        // Weighbridge codes (Entered manually by employee - Requirements 1A, 1B)
        formData.append('internalWeighbridgeCode', this.wbInternalCodeInput.value.trim());
        formData.append('externalWeighbridgeCode', this.wbExternalCodeInput.value.trim());

        if (this.wbIntEmptyTime?.value) formData.append('internalEmptyWeightTime', this.wbIntEmptyTime.value);
        if (this.wbIntLoadedTime?.value) formData.append('internalLoadedWeightTime', this.wbIntLoadedTime.value);
        if (this.wbExtEmptyTime?.value) formData.append('externalEmptyWeightTime', this.wbExtEmptyTime.value);
        if (this.wbExtLoadedTime?.value) formData.append('externalLoadedWeightTime', this.wbExtLoadedTime.value);

        // Section 4: Custody (العهدة), Ikramiya & Notes
        const custodyAmount = parseFloat(this.custodyInput?.value) || 0;
        formData.append('custodyAmount', custodyAmount);

        if (this.ikramiyaInput?.value) {
          formData.append('ikramiya', parseFloat(this.ikramiyaInput.value) || 0);
        }
        if (this.notesInput?.value?.trim()) {
          formData.append('notes', this.notesInput.value.trim());
        }

        // 3 Separate Mandatory Section Attachments
        if (this.wbAttachmentFile) {
          formData.append('weighbridgeAttachment', this.wbAttachmentFile);
        }
        if (this.dsAttachmentFile) {
          formData.append('dailySupplyAttachment', this.dsAttachmentFile);
        }
        if (this.bolAttachmentFile) {
          formData.append('billOfLadingAttachment', this.bolAttachmentFile);
          formData.append('invoiceFile', this.bolAttachmentFile); // Backwards compatibility
        }

        let res;
        const isResubmit = !!this.editingTxId;
        if (isResubmit) {
          res = await window.ChatApi?.resubmitTransportation(this.editingTxId, formData);
        } else {
          res = await window.ChatApi?.submitTransportationTransaction(formData);
        }

        if (res && res.success && res.data) {
          this.editingTxId = null;
          this.editingTx = null;
          if (this.submitBtnText) this.submitBtnText.textContent = 'تسجيل واعتماد الحركة';
          this.showSuccessStep(res.data, isResubmit);
          this.loadRecentTransactions();
          this.loadConfig();
        } else {
          const msg = res?.message || 'حدث خطأ أثناء حفظ حركة النقل.';
          this.showAlert(msg, 'error');
        }
      } catch (err) {
        console.error('Error submitting transportation transaction:', err);
        this.showAlert('حدث خطأ في الاتصال بالخادم أثناء حفظ الحركة.', 'error');
      } finally {
        this.setSubmitLoading(false);
      }
    }

    showSuccessStep(txData, isResubmit = false) {
      if (this.stepPoEntry) this.stepPoEntry.style.display = 'none';
      if (this.stepForm) this.stepForm.style.display = 'none';
      if (this.stepSuccess) this.stepSuccess.style.display = 'block';

      if (this.successTxNumber) this.successTxNumber.textContent = `#${txData.transactionNumber}`;
      if (this.successPoNumber) this.successPoNumber.textContent = txData.poNumber;
      if (this.successCompany) this.successCompany.textContent = txData.companyName || this.currentPo?.supplier || 'FaceStone Partner';
      if (this.successDestination) this.successDestination.textContent = txData.destination;
      if (this.successMaterial) this.successMaterial.textContent = txData.materialType;
      if (this.successIntWeight) this.successIntWeight.textContent = `${Number(txData.internalWeight || 0).toFixed(3)} طن`;
      if (this.successExtWeight) this.successExtWeight.textContent = `${Number(txData.externalNetWeight || 0).toFixed(3)} طن`;
      if (this.successNetWeight) this.successNetWeight.textContent = `${Number(txData.netWeight || txData.quantity || 0).toFixed(3)} طن`;
      if (this.successWaste) this.successWaste.textContent = `${Number(txData.wasteQuantity || 0).toFixed(3)} طن`;

      if (this.successShippingTime) {
        const mins = txData.shippingDurationMinutes;
        if (mins !== null && mins !== undefined) {
          const h = Math.floor(mins / 60);
          const m = mins % 60;
          this.successShippingTime.textContent = h > 0 ? `${h} ساعة و ${m} دقيقة` : `${m} دقيقة`;
        } else {
          this.successShippingTime.textContent = '—';
        }
      }

      if (this.stepSuccess) {
        let cmNotice = this.stepSuccess.querySelector('.cm-success-notice');
        if (!cmNotice) {
          cmNotice = document.createElement('div');
          cmNotice.className = 'cm-success-notice';
          cmNotice.style.cssText = 'background: #eff6ff; border: 1px solid #bfdbfe; color: #1e40af; border-radius: 8px; padding: 12px 16px; margin: 16px 0; text-align: center; font-weight: 700;';
          const sub = this.stepSuccess.querySelector('.trans-success-sub');
          if (sub && sub.parentNode) {
            sub.parentNode.insertBefore(cmNotice, sub.nextSibling);
          }
        }
        if (isResubmit) {
          cmNotice.innerHTML = '🔄 <strong>تمت إعادة تقديم الطلب بنجاح</strong> — عاد الطلب إلى قائمة انتظار التحكم والمراقبة (PENDING) للمراجعة والتدقيق.';
        } else {
          cmNotice.innerHTML = '✅ <strong>تم اعتماد حركة النقل تلقائياً بنجاح</strong> — تم تحديث رصيد إذن الصرف وترحيل السجل مباشرة.';
        }
      }

      this.stepSuccess.scrollIntoView({ behavior: 'smooth' });
    }

    async loadRecentTransactions() {
      if (!this.recentTableBody) return;
      this.recentTableBody.innerHTML = '<tr><td colspan="14" class="trans-empty-row">جاري تحميل سجل الحركات...</td></tr>';

      try {
        const res = await window.ChatApi?.getRecentTransportationTransactions(50);
        if (res && res.success && res.data) {
          this.allRecentRecords = res.data;
          this.applyTableFilters();
        } else {
          this.allRecentRecords = [];
          this.recentTableBody.innerHTML = '<tr><td colspan="14" class="trans-empty-row">لا توجد حركات مسجلة حالياً.</td></tr>';
        }
      } catch (err) {
        console.error('Failed to load recent transportation records:', err);
        this.recentTableBody.innerHTML = '<tr><td colspan="14" class="trans-empty-row error">تعذر تحميل السجل. يرجى إعادة المحاولة.</td></tr>';
      }
    }

    applyTableFilters() {
      if (!this.allRecentRecords) return;
      const search = (this.tableSearchInput?.value || '').trim().toLowerCase();
      const exportType = this.filterExportType?.value || 'all';
      const projectCode = this.filterProjectCode?.value || 'all';
      const status = this.filterStatus?.value || 'all';

      const filtered = this.allRecentRecords.filter(tx => {
        // Search
        if (search) {
          const matchTx = `#${tx.transactionNumber}`.toLowerCase().includes(search) || `${tx.transactionNumber}`.includes(search);
          const matchExp = (tx.exportTransactionNumber || tx.poNumber || '').toLowerCase().includes(search);
          const matchCar = (tx.carNumber || '').toLowerCase().includes(search);
          const matchDriver = (tx.driverName || '').toLowerCase().includes(search);
          const matchPrj = (tx.projectCode || '').toLowerCase().includes(search) || (tx.projectName || '').toLowerCase().includes(search);
          const matchMat = (tx.materialType || '').toLowerCase().includes(search);
          const matchDest = (tx.destination || '').toLowerCase().includes(search);
          if (!matchTx && !matchExp && !matchCar && !matchDriver && !matchPrj && !matchMat && !matchDest) {
            return false;
          }
        }

        // Export Type
        if (exportType !== 'all') {
          if (tx.exportType !== exportType) return false;
        }

        // Project Code
        if (projectCode !== 'all') {
          if (projectCode === 'none') {
            if (tx.projectCode && tx.projectCode !== 'Not Assigned' && tx.projectCode !== 'غير محدد') return false;
          } else {
            if (tx.projectCode !== projectCode) return false;
          }
        }

        // Status
        if (status !== 'all') {
          const st = (tx.approvalStatus || 'APPROVED').toUpperCase();
          if (st !== status) return false;
        }

        return true;
      });

      this.renderRecentTable(filtered);
    }

    renderRecentTable(records) {
      if (!this.recentTableBody) return;
      if (!records || records.length === 0) {
        this.recentTableBody.innerHTML = '<tr><td colspan="14" class="trans-empty-row">لا توجد حركات مطابقة لشروط البحث والفلترة.</td></tr>';
        return;
      }

      const rowsHtml = records.map(tx => {
        const netWeight = Number(tx.netWeight || tx.quantity || 0).toFixed(3);
        const dateStr = this.formatDateTimeDisplay(tx.transactionDate);
        const isFinished = tx.exportType === 'FINISHED_PRODUCT';
        const typeBadge = isFinished 
          ? '<span class="trans-tag-badge" style="background:#EFF6FF; color:#1D4ED8; font-weight:700;">منتج نهائي</span>'
          : '<span class="trans-tag-badge" style="background:#ECFDF5; color:#047857; font-weight:700;">مواد خام</span>';

        const prjCode = tx.projectCode && tx.projectCode !== 'Not Assigned' && tx.projectCode !== 'غير محدد'
          ? `<span class="trans-tag-badge" style="background:#EEF2FF; color:#4338CA; font-weight:700;" title="${this.escapeHtml(tx.projectName || '')}">${this.escapeHtml(tx.projectCode)}</span>`
          : '<span style="color:#94A3B8; font-size:0.85rem;">غير محدد</span>';

        const refOrBatch = tx.manufactureNumber || tx.batchNumber || tx.sampleCode || '—';

        let approvalBadge = '';
        const st = (tx.approvalStatus || 'APPROVED').toUpperCase();
        if (st === 'APPROVED') {
          approvalBadge = '<span class="cm-badge cm-badge-approved">✓ معتمد</span>';
        } else if (st === 'REJECTED') {
          const reasonText = tx.rejectionReason ? `<div style="font-size:0.75rem; color:#dc2626; margin-top:2px;">السبب: ${this.escapeHtml(tx.rejectionReason)}</div>` : '';
          approvalBadge = `
            <div>
              <span class="cm-badge cm-badge-rejected">✕ مرفوض</span>
              ${reasonText}
            </div>
          `;
        } else {
          approvalBadge = '<span class="cm-badge cm-badge-pending">⏳ قيد مراجعة الرقابة</span>';
        }

        let actions = '';
        if (st === 'REJECTED') {
          actions = `<button type="button" class="trans-edit-request-btn" data-id="${tx.id}" style="display:inline-flex; align-items:center; gap:4px; padding:3px 8px; font-size:0.75rem; font-weight:700; background:#2563eb; color:#fff; border:none; border-radius:4px; cursor:pointer;" title="تعديل بيانات الطلب الأصلي وإعادة تقديمه">✏️ تعديل الطلب</button>`;
        } else {
          actions = `<span style="color:#64748B; font-size:0.82rem;">—</span>`;
        }

        return `
          <tr>
            <td class="font-numeric" style="font-weight: 800; color: #2563EB;">#${tx.transactionNumber}</td>
            <td class="font-numeric" dir="ltr" style="font-weight: 700; color: #1E40AF;">${this.escapeHtml(tx.exportTransactionNumber || tx.poNumber)}</td>
            <td>${typeBadge}</td>
            <td><strong style="color:#0F172A;">${this.escapeHtml(tx.materialType || '—')}</strong></td>
            <td class="font-numeric">${this.escapeHtml(refOrBatch)}</td>
            <td class="font-numeric" style="font-weight: 800; color: #16A34A;">${netWeight}</td>
            <td style="color:#64748B;">طن</td>
            <td>${prjCode}</td>
            <td class="font-numeric">${this.escapeHtml(tx.carNumber)}</td>
            <td>${this.escapeHtml(tx.driverName)}</td>
            <td>${this.escapeHtml(tx.destination)}</td>
            <td class="font-numeric">${dateStr}</td>
            <td>${approvalBadge}</td>
            <td>${actions}</td>
          </tr>
        `;
      }).join('');

      this.recentTableBody.innerHTML = rowsHtml;

      this.recentTableBody.querySelectorAll('.trans-edit-request-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
          const txId = e.currentTarget.getAttribute('data-id');
          this.loadForEdit(txId);
        });
      });
    }

    async loadForEdit(txId) {
      if (!txId) return;
      try {
        this.showAlert('جاري تحميل بيانات الطلب للتعديل...', 'info');
        const res = await window.ChatApi?.getTransportationById(txId);
        if (!res || !res.success || !res.data) {
          this.showAlert(res?.message || 'تعذر تحميل بيانات الطلب المرفوض.', 'error');
          return;
        }

        const tx = res.data;
        this.editingTxId = tx.id;
        this.editingTx = tx;

        // Switch steps
        if (this.stepPoEntry) this.stepPoEntry.style.display = 'none';
        if (this.stepSuccess) this.stepSuccess.style.display = 'none';
        if (this.stepForm) this.stepForm.style.display = 'block';

        // Section 1: Summary Bar
        const isFinished = tx.exportType === 'FINISHED_PRODUCT';
        const typeLabel = isFinished ? 'منتج نهائي' : 'مواد خام';
        if (this.summaryPo) this.summaryPo.textContent = tx.exportTransactionNumber || tx.poNumber;
        if (this.summaryExportType) this.summaryExportType.textContent = typeLabel;
        if (this.summaryMaterial) this.summaryMaterial.textContent = tx.materialType || 'بضاعة';
        if (this.summaryRefBatch) {
          const ref = tx.sampleCode;
          const batch = tx.batchNumber || tx.manufactureNumber;
          this.summaryRefBatch.textContent = [ref, batch].filter(Boolean).join(' / ') || '—';
        }
        if (this.summaryTotalQty) this.summaryTotalQty.textContent = `${Number(tx.netWeight || 0).toFixed(3)} طن`;
        if (this.summaryRemQty) this.summaryRemQty.textContent = '—';
        if (this.summarySupplier) this.summarySupplier.textContent = `${tx.companyName || 'عميل'} (${tx.destination})`;
        if (this.summaryDestination) this.summaryDestination.textContent = tx.destination;
        if (this.summarySource) this.summarySource.textContent = tx.sourceStorageLocationName ? `مستودع: ${tx.sourceStorageLocationName}` : (tx.source || 'Face stone');

        // Section 2: Project Code (Optional)
        if (this.projectCodeSelect) {
          if (tx.projectCodeId) {
            this.projectCodeSelect.value = tx.projectCodeId;
          } else if (tx.projectCode) {
            const foundOpt = Array.from(this.projectCodeSelect.options).find(o => o.getAttribute('data-code') === tx.projectCode || o.value == tx.projectCode);
            if (foundOpt) {
              this.projectCodeSelect.value = foundOpt.value;
            } else {
              this.projectCodeSelect.value = '';
            }
          } else {
            this.projectCodeSelect.value = '';
          }
        }

        // Daily Supply PO display
        if (this.dailyPoDisplay) this.dailyPoDisplay.value = tx.exportTransactionNumber || tx.poNumber;

        // Section 3: Prepopulated fields
        if (this.companyInput) this.companyInput.value = tx.companyName || '';
        if (this.destinationInput) this.destinationInput.value = tx.destination || '';
        if (this.sourceDisplay) this.sourceDisplay.value = tx.source || 'Face stone (Full Concrete Solutions)';

        // Populate fields
        if (this.txNumberInput) this.txNumberInput.value = tx.transactionNumber;
        if (this.txDateInput && tx.transactionDate) {
          this.txDateInput.value = tx.transactionDate.substring(0, 16);
          const formatted = this.formatDateTimeDisplay(this.txDateInput.value);
          if (this.dailyShipDateDisplay) this.dailyShipDateDisplay.value = formatted;
          if (this.shipDateDisplay) this.shipDateDisplay.value = formatted;
        }

        // Weighbridge codes
        if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = tx.internalWeighbridgeCode || '';
        if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = tx.externalWeighbridgeCode || '';

        // Weights
        if (this.wbIntEmpty) this.wbIntEmpty.value = tx.weightBeforeLoading ?? '';
        if (this.wbIntLoaded) this.wbIntLoaded.value = tx.weightWhenExitingFactory ?? '';
        if (this.wbExtLoaded) this.wbExtLoaded.value = tx.externalLoadedWeight ?? '';
        if (this.wbExtEmpty) this.wbExtEmpty.value = tx.externalEmptyWeight ?? '';

        // Weighbridge timestamps
        if (this.wbIntLoadedTime) {
          this.wbIntLoadedTime.value = tx.internalLoadedWeightTime ? tx.internalLoadedWeightTime.substring(0, 16) : '';
          const val = this.formatDateTimeDisplay(this.wbIntLoadedTime.value);
          if (this.departureDisplay) this.departureDisplay.value = val;
          if (this.dailyDepartureDisplay) this.dailyDepartureDisplay.value = val;
        }
        if (this.wbIntEmptyTime) this.wbIntEmptyTime.value = tx.internalEmptyWeightTime ? tx.internalEmptyWeightTime.substring(0, 16) : '';
        if (this.wbExtLoadedTime) {
          this.wbExtLoadedTime.value = tx.externalLoadedWeightTime ? tx.externalLoadedWeightTime.substring(0, 16) : '';
          const val = this.formatDateTimeDisplay(this.wbExtLoadedTime.value);
          if (this.arrivalDisplay) this.arrivalDisplay.value = val;
          if (this.dailyArrivalDisplay) this.dailyArrivalDisplay.value = val;
        }
        if (this.wbExtEmptyTime) this.wbExtEmptyTime.value = tx.externalEmptyWeightTime ? tx.externalEmptyWeightTime.substring(0, 16) : '';

        // Car & Driver
        if (this.carInput) this.carInput.value = tx.carNumber || '';
        if (this.driverSelect) {
          if (tx.driverName && !Array.from(this.driverSelect.options).some(o => o.value === tx.driverName)) {
            const opt = document.createElement('option');
            opt.value = tx.driverName;
            opt.textContent = tx.driverName;
            this.driverSelect.appendChild(opt);
          }
          this.driverSelect.value = tx.driverName || '';
        }
        if (this.driverPhoneInput) this.driverPhoneInput.value = tx.driverPhone || '';
        if (this.materialSelect) this.materialSelect.value = tx.materialType || 'powder';

        // Recipient & Checklist
        if (this.recNameInput) this.recNameInput.value = tx.recipientName || '';
        if (this.recTitleSelect) this.recTitleSelect.value = tx.recipientTitle || '';
        if (this.recDateInput) this.recDateInput.value = tx.receivedDate ? tx.receivedDate.substring(0, 16) : '';

        const recSt = (tx.receivingStatus || '').toLowerCase();
        if (this.statusSafeCheckbox) this.statusSafeCheckbox.checked = recSt.includes('safe');
        if (this.statusIntactCheckbox) this.statusIntactCheckbox.checked = recSt.includes('intact');

        // Custody, Ikramiya, Notes
        if (this.custodyInput) this.custodyInput.value = tx.custodyAmount ?? '0';
        if (this.ikramiyaInput) this.ikramiyaInput.value = tx.ikramiya ?? '0.00';
        if (this.notesInput) this.notesInput.value = tx.notes || '';

        // Attachments display
        this.displayExistingAttachment(this.wbPlaceholder, this.wbFilePreview, this.wbFileName, this.wbFileSize, tx.weighbridgeFileName, tx.weighbridgeFileSize);
        this.displayExistingAttachment(this.dsPlaceholder, this.dsFilePreview, this.dsFileName, this.dsFileSize, tx.dailySupplyFileName, tx.dailySupplyFileSize);
        this.displayExistingAttachment(this.bolPlaceholder, this.bolFilePreview, this.bolFileName, this.bolFileSize, tx.billOfLadingFileName, tx.billOfLadingFileSize);

        // Recalculate weights & shipping times
        this.recalculateAllWeights();
        this.recalculateShippingTime();

        // Change submit button text
        if (this.submitBtnText) this.submitBtnText.textContent = 'إعادة تقديم الطلب بعد التعديل (Resubmit Request)';

        // Show edit alert banner
        const reasonPart = tx.rejectionReason ? ` | سبب الرفض: "${this.escapeHtml(tx.rejectionReason)}"` : '';
        this.showAlert(`✏️ أنت الآن تقوم بتعديل الطلب المرفوض #${tx.transactionNumber} (أمر الشراء: ${tx.poNumber})${reasonPart}. قم بتصحيح البيانات المطلوبة ثم اضغط على "إعادة تقديم الطلب بعد التعديل".`, 'error');

        // Scroll to form
        if (this.stepForm) {
          this.stepForm.scrollIntoView({ behavior: 'smooth' });
        }
      } catch (err) {
        console.error('Failed to load transaction for edit:', err);
        this.showAlert('حدث خطأ أثناء تحميل بيانات الطلب للتعديل.', 'error');
      }
    }

    displayExistingAttachment(placeholderEl, previewEl, nameEl, sizeEl, fileName, fileSize) {
      if (fileName && previewEl && nameEl) {
        nameEl.textContent = `${fileName} (المرفق الحالي محفوظ)`;
        if (sizeEl) sizeEl.textContent = fileSize ? this.formatFileSize(fileSize) : '';
        if (placeholderEl) placeholderEl.style.display = 'none';
        previewEl.style.display = 'flex';
      }
    }

    resetFormKeepPo() {
      // Clear edit state
      this.editingTxId = null;
      this.editingTx = null;
      if (this.submitBtnText) this.submitBtnText.textContent = 'تسجيل واعتماد الحركة';

      // Clear file attachments
      this.wbAttachmentFile = null;
      this.dsAttachmentFile = null;
      this.bolAttachmentFile = null;

      [
        { input: this.wbAttachmentInput, placeholder: this.wbPlaceholder, preview: this.wbFilePreview, err: this.wbAttachmentError },
        { input: this.dsAttachmentInput, placeholder: this.dsPlaceholder, preview: this.dsFilePreview, err: this.dsAttachmentError },
        { input: this.bolAttachmentInput, placeholder: this.bolPlaceholder, preview: this.bolFilePreview, err: this.bolAttachmentError }
      ].forEach(item => {
        if (item.input) item.input.value = '';
        if (item.placeholder) item.placeholder.style.display = 'flex';
        if (item.preview) item.preview.style.display = 'none';
        if (item.err) {
          item.err.textContent = '';
          item.err.style.display = 'none';
        }
      });

      // Clear weights
      if (this.wbIntLoaded) this.wbIntLoaded.value = '';
      if (this.wbIntEmpty) this.wbIntEmpty.value = '';
      if (this.wbExtLoaded) this.wbExtLoaded.value = '';
      if (this.wbExtEmpty) this.wbExtEmpty.value = '';

      // Clear operational inputs
      if (this.wbInternalCodeInput) this.wbInternalCodeInput.value = '';
      if (this.wbExternalCodeInput) this.wbExternalCodeInput.value = '';
      if (this.driverPhoneInput) this.driverPhoneInput.value = '';
      if (this.carInput) this.carInput.value = '';
      if (this.driverSelect) this.driverSelect.selectedIndex = 0;
      if (this.materialSelect) this.materialSelect.selectedIndex = 0;
      if (this.recNameInput) this.recNameInput.value = '';
      if (this.recTitleSelect) this.recTitleSelect.selectedIndex = 0;
      if (this.recDateInput) this.recDateInput.value = '';
      if (this.custodyInput) this.custodyInput.value = '';
      if (this.ikramiyaInput) this.ikramiyaInput.value = '0.00';
      if (this.notesInput) this.notesInput.value = '';

      if (this.statusSafeCheckbox) this.statusSafeCheckbox.checked = true;
      if (this.statusIntactCheckbox) this.statusIntactCheckbox.checked = true;

      this.recalculateAllWeights();
      this.recalculateShippingTime();
      this.clearAllFieldErrors();
      this.hideAlert();
    }

    setFieldError(inputEl, msg) {
      if (!inputEl) return;
      inputEl.classList.add('error');
      const group = inputEl.closest('.trans-form-group');
      if (group) {
        let errEl = group.querySelector('.trans-field-error');
        if (errEl) {
          errEl.textContent = msg;
          errEl.style.display = 'block';
        }
      }
    }

    clearAllFieldErrors() {
      document.querySelectorAll('.trans-input.error').forEach(el => el.classList.remove('error'));
      document.querySelectorAll('.trans-field-error').forEach(el => {
        el.textContent = '';
        el.style.display = 'none';
      });
      if (this.shippingTimeError) {
        this.shippingTimeError.style.display = 'none';
      }
      if (this.recStatusError) {
        this.recStatusError.style.display = 'none';
      }
      if (this.wbAttachmentError) this.wbAttachmentError.style.display = 'none';
      if (this.dsAttachmentError) this.dsAttachmentError.style.display = 'none';
      if (this.bolAttachmentError) this.bolAttachmentError.style.display = 'none';
    }

    showAlert(msg, type = 'success') {
      if (!this.alertBanner) return;
      this.alertBanner.className = `trans-alert-banner ${type}`;
      if (this.alertIcon) this.alertIcon.textContent = type === 'error' ? '⚠️' : '✓';
      if (this.alertTitle) this.alertTitle.textContent = type === 'error' ? 'تنبيه' : 'نجاح';
      if (this.alertDesc) this.alertDesc.textContent = msg;
      this.alertBanner.style.display = 'flex';
      this.alertBanner.scrollIntoView({ behavior: 'smooth' });
    }

    hideAlert() {
      if (this.alertBanner) this.alertBanner.style.display = 'none';
    }

    setSubmitLoading(isLoading) {
      this.isSubmitting = isLoading;
      if (this.submitBtn) this.submitBtn.disabled = isLoading;
      if (this.submitSpinner) this.submitSpinner.style.display = isLoading ? 'inline-block' : 'none';
      if (this.submitIcon) this.submitIcon.style.display = isLoading ? 'none' : 'inline-block';
      if (this.submitBtnText) this.submitBtnText.textContent = isLoading ? 'جاري الحفظ...' : 'حفظ';
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

    updateUserBadge(user) {
      if (!user) return;
      if (this.userNameEl) {
        this.userNameEl.textContent = user.fullName || user.username || 'مستخدم مصرح له';
      }
      if (this.userRoleEl) {
        let roleArabic = user.role || 'إدارة النقل';
        if (user.role === 'CEO') roleArabic = 'الرئيس التنفيذي';
        else if (user.role === 'IT') roleArabic = 'الدعم الفني / IT';
        else if (user.role === 'Transportation Manager' || user.role === 'TRANSPORTATION_MANAGER') roleArabic = 'مدير إدارة النقل';
        this.userRoleEl.textContent = roleArabic;
      }
      if (this.userAvatarEl) {
        const initials = (user.fullName || user.username || 'FS')
          .split(' ')
          .filter(Boolean)
          .map(w => w[0])
          .slice(0, 2)
          .join('')
          .toUpperCase();
        this.userAvatarEl.textContent = initials || 'FS';
      }
    }
  }

  // Create global instance
  window.TransportationApp = new TransportationManager();

  // Hook into ChatApp view transitions
  document.addEventListener('chat:view:changed', (e) => {
    if (e.detail && e.detail.view === 'transportation') {
      const user = window.ChatApi?.getCurrentUser();
      window.TransportationApp.updateUserBadge(user);
      window.TransportationApp.showPoEntryStep();
      window.TransportationApp.loadRecentTransactions();
    }
  });

})();
