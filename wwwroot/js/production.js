/* ==========================================================================
   FACESTONE PRODUCTION DEPARTMENT CONTROLLER (قسم الإنتاج)
   Redesigned Daily Production Tracking System
   - Strict server-enforced shift and employee mapping
   - Dynamic multi-product quantity inputs
   - Real-time arithmetic: Duration, Gross, Net, Product & Overall Hourly Rates, Waste %
   - Real database summaries and management answers without double-counting
   ========================================================================== */

(function () {
  'use strict';

  class ProductionApp {
    static init() {
      this.root = document.getElementById('production-page-root');
      if (!this.root) return;

      if (this.isInitialized) {
        this.loadShiftInfo();
        this.loadCatalog();
        this.loadStats();
        this.loadSubmissions();
        this.setDefaultDateAndTimes();
        this.showSection(this.currentSection || 'process');
        return;
      }
      this.isInitialized = true;

      this.currentSection = 'process';
      this.shiftInfo = null;
      this.catalog = [];
      this.selectedProducts = new Map(); // productCode -> { id, code, name, category, quantity, unit }
      this.editSelectedProducts = new Map();

      // Production Department Flow State
      this.availableBatches = [];
      this.selectedBatches = new Map(); // batchId -> { batch, consumedQuantity }
      this.currentProcessForFinished = null;
      this.currentOrderForFinished = null;
      this.eligibleOrdersForFinished = [];

      this.bindUiEvents();
      this.initProcessSection();
      this.initFinishedSection();
      this.initTraceabilitySection();

      this.loadShiftInfo();
      this.loadCatalog();
      this.loadStats();
      this.loadSubmissions();
      this.setDefaultDateAndTimes();
      this.showSection('process');
    }

    static updateUserBadge(user) {
      if (!user) return;
      const nameEl = document.getElementById('prd-user-name');
      const roleEl = document.getElementById('prd-user-role');
      const avatarEl = document.getElementById('prd-user-avatar');

      if (nameEl) nameEl.textContent = user.fullName || user.username || 'المستخدم';
      if (roleEl) {
        roleEl.textContent = (window.ChatApp && window.ChatApp.formatRoleArabic)
          ? window.ChatApp.formatRoleArabic(user.role)
          : user.role;
      }
      if (avatarEl) {
        const initials = (user.fullName || 'FS').split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
        avatarEl.textContent = initials || 'FS';
      }
    }

    static setDefaultDateAndTimes() {
      const now = new Date();
      const todayStr = this.formatDate(now);

      const dateInput = document.getElementById('prd-input-date');
      if (dateInput && !dateInput.value) {
        dateInput.value = todayStr;
      }

      // Default start and end times (e.g., 08:00 to 16:00 today)
      const startInput = document.getElementById('prd-input-start-time');
      const endInput = document.getElementById('prd-input-end-time');

      if (startInput && !startInput.value) {
        startInput.value = `${todayStr}T08:00`;
      }
      if (endInput && !endInput.value) {
        endInput.value = `${todayStr}T16:00`;
      }

      this.updateLiveCalculations();
    }

    static bindUiEvents() {
      // Tab switcher buttons
      const tabBtns = this.root.querySelectorAll('.prd-tab-btn');
      tabBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
          e.preventDefault();
          const target = btn.dataset.section;
          if (target) this.showSection(target);
        });
      });

      // Refresh button
      const refreshBtn = document.getElementById('prd-btn-refresh');
      refreshBtn?.addEventListener('click', () => {
        this.loadShiftInfo();
        this.loadStats();
        this.loadSubmissions();
      });

      // Add product button and select
      const addProdBtn = document.getElementById('prd-btn-add-product');
      addProdBtn?.addEventListener('click', () => this.handleAddProductClick());

      const catalogSelect = document.getElementById('prd-catalog-select');
      catalogSelect?.addEventListener('change', () => {
        if (catalogSelect.value) {
          this.addProduct(catalogSelect.value);
          catalogSelect.value = '';
        }
      });

      // Shift times change
      const startInput = document.getElementById('prd-input-start-time');
      const endInput = document.getElementById('prd-input-end-time');
      startInput?.addEventListener('input', () => this.updateLiveCalculations());
      endInput?.addEventListener('input', () => this.updateLiveCalculations());

      // Waste change
      const wasteInput = document.getElementById('prd-input-waste');
      wasteInput?.addEventListener('input', () => this.updateLiveCalculations());

      // Daily Form Submit
      const dailyForm = document.getElementById('prd-daily-form');
      dailyForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleDailySubmission();
      });

      // Reset form
      const resetBtn = document.getElementById('prd-btn-reset-form');
      resetBtn?.addEventListener('click', () => this.resetDailyForm());

      // Summary Filter buttons
      const applyFiltersBtn = document.getElementById('prd-btn-apply-filters');
      applyFiltersBtn?.addEventListener('click', () => this.applySummaryFilters());

      const resetFiltersBtn = document.getElementById('prd-btn-reset-filters');
      resetFiltersBtn?.addEventListener('click', () => this.resetSummaryFilters());

      const exportSummaryBtn = document.getElementById('prd-btn-export-summary');
      exportSummaryBtn?.addEventListener('click', () => {
        this.loadStats();
        this.loadSubmissions();
      });

      // Edit Modal Events
      const editCatalogSelect = document.getElementById('prd-edit-catalog-select');
      const editAddProdBtn = document.getElementById('prd-edit-btn-add-product');
      editAddProdBtn?.addEventListener('click', () => {
        if (editCatalogSelect && editCatalogSelect.value) {
          this.addEditProduct(editCatalogSelect.value);
          editCatalogSelect.value = '';
        }
      });

      const editStartTime = document.getElementById('prd-edit-start-time');
      const editEndTime = document.getElementById('prd-edit-end-time');
      const editWaste = document.getElementById('prd-edit-waste');
      editStartTime?.addEventListener('input', () => this.updateEditLiveCalculations());
      editEndTime?.addEventListener('input', () => this.updateEditLiveCalculations());
      editWaste?.addEventListener('input', () => this.updateEditLiveCalculations());

      const editForm = document.getElementById('prd-edit-submission-form');
      editForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleEditSubmissionSubmit();
      });
    }

    static showSection(section) {
      if (section === 'inventory' || section === 'orders' || section === 'batches') {
        section = 'process';
      }
      if (!section) section = 'process';
      this.currentSection = section;

      const tabBtns = this.root.querySelectorAll('.prd-tab-btn');
      tabBtns.forEach(btn => {
        btn.classList.toggle('active', btn.dataset.section === section);
      });

      const secProcess = document.getElementById('prd-sec-process');
      const secFinished = document.getElementById('prd-sec-finished');
      const secHistory = document.getElementById('prd-sec-history');
      const secForm = document.getElementById('prd-sec-form');
      const secSummary = document.getElementById('prd-sec-summary');

      if (secProcess) secProcess.style.display = (section === 'process') ? 'block' : 'none';
      if (secFinished) secFinished.style.display = (section === 'finished') ? 'block' : 'none';
      if (secHistory) secHistory.style.display = (section === 'history') ? 'block' : 'none';
      if (secForm) secForm.style.display = (section === 'form') ? 'block' : 'none';
      if (secSummary) secSummary.style.display = (section === 'summary') ? 'block' : 'none';

      if (section === 'process') {
        this.loadNextNumbers();
        this.loadAvailableBatches();
      } else if (section === 'finished') {
        this.loadOrdersForFinished();
        this.loadProcessesForFinished();
        this.loadNextNumbers();
      } else if (section === 'history') {
        this.loadTraceabilityHistory();
      } else if (section === 'summary') {
        this.loadSubmissions();
      }
    }

    static handleRoute(subRoute) {
      if (subRoute === '/production/summary' || subRoute === 'summary') {
        this.showSection('summary');
      } else if (subRoute === '/production/finished' || subRoute === 'finished') {
        this.showSection('finished');
      } else if (subRoute === '/production/history' || subRoute === 'history') {
        this.showSection('history');
      } else if (subRoute === '/production/form' || subRoute === 'form') {
        this.showSection('form');
      } else {
        this.showSection('process');
      }
    }

    /* ----------------------------------------------------------------------
       Shift & Current Employee Info
       ---------------------------------------------------------------------- */
    static async loadShiftInfo() {
      try {
        const res = await window.ChatApi.getCurrentShiftInfo();
        if (res && res.success && res.data) {
          this.shiftInfo = res.data;
          this.renderShiftInfo(this.shiftInfo);
        } else {
          this.renderShiftError(res?.message || 'تعذر تحميل بيانات الوردية من الخادم');
        }
      } catch (err) {
        console.error('Error loading shift info:', err);
        this.renderShiftError('خطأ أثناء الاتصال بالخادم للتحقق من الوردية');
      }
    }

    static renderShiftInfo(info) {
      const shiftTextEl = document.getElementById('prd-current-shift-text');
      const formEmpNameEl = document.getElementById('prd-form-employee-name');
      const formShiftTagEl = document.getElementById('prd-form-shift-tag');
      const warningBanner = document.getElementById('prd-shift-warning-banner');
      const warningText = document.getElementById('prd-shift-warning-text');
      const submitBtn = document.getElementById('prd-btn-submit-daily');

      const shiftName = info.shift || info.shiftName || '';
      const empEmail = info.employeeEmail || info.email || '';

      if (info.canSubmit) {
        if (warningBanner) warningBanner.style.display = 'none';
        if (submitBtn) submitBtn.disabled = false;

        const shiftBadgeText = `${info.employeeName} — ${shiftName}`;
        if (shiftTextEl) shiftTextEl.textContent = shiftBadgeText;
        if (formEmpNameEl) formEmpNameEl.textContent = `${info.employeeName} (${empEmail})`;
        if (formShiftTagEl) {
          formShiftTagEl.textContent = shiftName;
          formShiftTagEl.style.background = shiftName.includes('الأولى') ? '#dbeafe' : '#fef3c7';
          formShiftTagEl.style.color = shiftName.includes('الأولى') ? '#1e40af' : '#92400e';
        }

        const now = new Date();
        const todayStr = this.formatDate(now);
        const startInput = document.getElementById('prd-input-start-time');
        const endInput = document.getElementById('prd-input-end-time');

        if (shiftName.includes('الثانية') || shiftName.includes('المسائية')) {
          if (startInput && (!startInput.value || startInput.value.endsWith('08:00'))) {
            startInput.value = `${todayStr}T16:00`;
          }
          if (endInput && (!endInput.value || endInput.value.endsWith('16:00'))) {
            const tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
            const tomorrowStr = this.formatDate(tomorrow);
            endInput.value = `${tomorrowStr}T00:00`;
          }
        } else {
          if (startInput && (!startInput.value || startInput.value.endsWith('16:00'))) {
            startInput.value = `${todayStr}T08:00`;
          }
          if (endInput && (!endInput.value || endInput.value.endsWith('00:00'))) {
            endInput.value = `${todayStr}T16:00`;
          }
        }
        this.updateLiveCalculations();
      } else {
        if (warningBanner) warningBanner.style.display = 'block';
        if (warningText) warningText.textContent = info.errorMessage || info.message || 'حسابك غير مقترن بوردية إنتاج معتمدة';
        if (shiftTextEl) shiftTextEl.textContent = 'غير مصرح بتسجيل الإنتاج';
        if (formEmpNameEl) formEmpNameEl.textContent = info.employeeName || 'غير مسجل';
        if (formShiftTagEl) {
          formShiftTagEl.textContent = 'غير محدد';
          formShiftTagEl.style.background = '#fee2e2';
          formShiftTagEl.style.color = '#991b1b';
        }
        if (submitBtn) {
          submitBtn.disabled = true;
          submitBtn.title = info.errorMessage || 'غير مصرح';
        }
      }
    }

    static renderShiftError(msg) {
      const warningBanner = document.getElementById('prd-shift-warning-banner');
      const warningText = document.getElementById('prd-shift-warning-text');
      const submitBtn = document.getElementById('prd-btn-submit-daily');

      if (warningBanner) warningBanner.style.display = 'block';
      if (warningText) warningText.textContent = msg;
      if (submitBtn) submitBtn.disabled = true;
    }

    /* ----------------------------------------------------------------------
       Product Catalog
       ---------------------------------------------------------------------- */
    static async loadCatalog() {
      try {
        const res = await window.ChatApi.getProductionCatalog();
        if (res && res.success && Array.isArray(res.data)) {
          this.catalog = res.data;
          this.populateCatalogDropdowns(this.catalog);
        }
      } catch (err) {
        console.error('Error loading product catalog:', err);
      }
    }

    static populateCatalogDropdowns(products) {
      const mainSelect = document.getElementById('prd-catalog-select');
      const editSelect = document.getElementById('prd-edit-catalog-select');
      const filterSelect = document.getElementById('prd-filter-product');

      if (mainSelect) {
        mainSelect.innerHTML = '<option value="">-- اختر منتجاً لإضافته للقائمة --</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.code || p.productCode;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr} (${p.category || 'عام'})`;
          mainSelect.appendChild(opt);
        });
      }

      if (editSelect) {
        editSelect.innerHTML = '<option value="">-- إضافة منتج --</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.code || p.productCode;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr}`;
          editSelect.appendChild(opt);
        });
      }

      if (filterSelect) {
        filterSelect.innerHTML = '<option value="">جميع المنتجات</option>';
        products.forEach(p => {
          const opt = document.createElement('option');
          opt.value = p.name || p.code;
          opt.textContent = `${p.code || p.productCode} — ${p.name || p.productNameAr}`;
          filterSelect.appendChild(opt);
        });
      }
    }

    /* ----------------------------------------------------------------------
       Dynamic Products Selection (Daily Form)
       ---------------------------------------------------------------------- */
    static handleAddProductClick() {
      const select = document.getElementById('prd-catalog-select');
      if (select && select.value) {
        this.addProduct(select.value);
        select.value = '';
      }
    }

    static addProduct(productCode, initialQty = null) {
      if (!productCode) return;

      const prod = this.catalog.find(p => (p.code === productCode || p.productCode === productCode)) || {
        id: 0,
        code: productCode,
        name: productCode,
        category: 'عام',
        unit: 'طن'
      };

      const code = prod.code || prod.productCode || productCode;

      // Prevent duplicate product entry within the same submission
      if (this.selectedProducts.has(code)) {
        const existingInput = document.getElementById(`prd-qty-${code}`);
        if (existingInput) {
          existingInput.focus();
          existingInput.style.borderColor = '#f59e0b';
          setTimeout(() => { existingInput.style.borderColor = '#0284c7'; }, 1000);
        }
        return;
      }

      this.selectedProducts.set(code, {
        id: prod.id,
        code: code,
        name: prod.name || prod.productNameAr || code,
        category: prod.category || 'عام',
        unit: prod.unit || 'طن',
        quantity: initialQty
      });

      this.renderSelectedProducts();
      this.updateLiveCalculations();
    }

    static removeProduct(productCode) {
      this.selectedProducts.delete(productCode);
      this.renderSelectedProducts();
      this.updateLiveCalculations();
    }

    static renderSelectedProducts() {
      const container = document.getElementById('prd-selected-products-container');
      if (!container) return;

      if (this.selectedProducts.size === 0) {
        container.innerHTML = `
          <div id="prd-no-products-placeholder" style="text-align: center; padding: 2rem 1rem; background: #ffffff; border: 2px dashed #cbd5e1; border-radius: 8px; color: var(--prd-text-muted);">
            <div style="font-size: 1.8rem; margin-bottom: 0.35rem;">📦</div>
            <div style="font-weight: 700; font-size: 0.95rem; color: var(--prd-navy-dark);">لم يتم اختيار أي منتج بعد</div>
            <div style="font-size: 0.82rem; margin-top: 0.25rem;">يرجى اختيار المنتجات المنتجة من القائمة المنسدلة أعلاه لإدخال كمياتها</div>
          </div>
        `;
        return;
      }

      container.innerHTML = '';
      this.selectedProducts.forEach((item, code) => {
        const card = document.createElement('div');
        card.className = 'prd-selected-product-card';
        card.id = `prd-row-${code}`;

        const qtyVal = (item.quantity !== null && item.quantity !== undefined && item.quantity !== '')
          ? item.quantity
          : '';

        card.innerHTML = `
          <div style="display: flex; align-items: center; gap: 0.75rem; flex: 1;">
            <div style="width: 36px; height: 36px; border-radius: 8px; background: #e0f2fe; color: #0284c7; display: flex; align-items: center; justify-content: center; font-weight: 800; font-size: 0.85rem;">
              FS
            </div>
            <div>
              <div style="font-weight: 800; color: var(--prd-navy-dark); font-size: 0.92rem;">
                ${this.escape(item.name)}
              </div>
              <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 0.15rem;">
                <span style="font-size: 0.75rem; color: #64748b; font-family: monospace; font-weight: 700;">${this.escape(item.code)}</span>
                <span class="prd-badge" style="background: #f1f5f9; color: #475569; font-size: 0.7rem; padding: 0.1rem 0.4rem; border-radius: 4px;">${this.escape(item.category)}</span>
              </div>
            </div>
          </div>

          <div style="display: flex; align-items: center; gap: 1rem;">
            <!-- Live Product Rate Display -->
            <div style="text-align: right; min-width: 130px;">
              <span style="font-size: 0.72rem; color: var(--prd-text-muted); display: block;">معدل إنتاج المنتج:</span>
              <span id="prd-item-rate-${code}" style="font-weight: 800; font-size: 0.88rem; color: var(--prd-yellow-hover);">—</span>
            </div>

            <!-- Product Quantity Input -->
            <div style="display: flex; align-items: center; gap: 0.4rem;">
              <label for="prd-qty-${code}" style="font-size: 0.82rem; font-weight: 700; color: var(--prd-navy-dark); white-space: nowrap;">
                الكمية المنتجة (طن):
              </label>
              <input type="number" 
                     step="0.001" 
                     min="0.001" 
                     id="prd-qty-${code}" 
                     class="prd-product-qty-input" 
                     placeholder="0.000" 
                     value="${qtyVal}"
                     required>
            </div>

            <!-- Remove Product Button -->
            <button type="button" 
                    class="prd-btn-remove-prod" 
                    title="حذف المنتج من التسجيل"
                    onclick="ProductionApp.removeProduct('${code}')">
              &times; حذف
            </button>
          </div>
        `;

        container.appendChild(card);

        // Bind input event for quantity without re-rendering other inputs
        const input = card.querySelector(`#prd-qty-${code}`);
        input?.addEventListener('input', (e) => {
          const val = parseFloat(e.target.value);
          const currentItem = this.selectedProducts.get(code);
          if (currentItem) {
            currentItem.quantity = isNaN(val) ? null : val;
          }
          this.updateLiveCalculations();
        });
      });
    }

    /* ----------------------------------------------------------------------
       Live Automatic Calculations
       ---------------------------------------------------------------------- */
    static calculateDurationHours(startVal, endVal) {
      if (!startVal || !endVal) return null;

      const start = new Date(startVal);
      const end = new Date(endVal);

      if (isNaN(start.getTime()) || isNaN(end.getTime())) return null;

      const diffMs = end.getTime() - start.getTime();
      if (diffMs <= 0) return null;

      return diffMs / (1000 * 60 * 60);
    }

    static updateLiveCalculations() {
      const startVal = document.getElementById('prd-input-start-time')?.value;
      const endVal = document.getElementById('prd-input-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-input-waste')?.value || 0);

      // 1. Duration Calculation
      const durationHours = this.calculateDurationHours(startVal, endVal);
      const durationBox = document.getElementById('prd-display-duration-box');
      const durationText = document.getElementById('prd-display-duration-text');
      const durationBadge = document.getElementById('prd-display-duration-badge');
      const durationHint = document.getElementById('prd-duration-hint');

      if (durationHours !== null && durationHours > 0) {
        const totalMinutes = Math.round(durationHours * 60);
        const hrs = Math.floor(totalMinutes / 60);
        const mins = totalMinutes % 60;

        if (durationText) durationText.textContent = `${durationHours.toFixed(2)} ساعة`;
        if (durationBadge) durationBadge.textContent = `${hrs} س ${mins} د`;
        if (durationHint) {
          durationHint.textContent = `فترة وردية سليمة (${durationHours.toFixed(2)} ساعة عمل فعلية)`;
          durationHint.style.color = '#10b981';
        }
        if (durationBox) durationBox.style.borderColor = '#10b981';
      } else {
        if (durationText) durationText.textContent = '—';
        if (durationBadge) durationBadge.textContent = 'غير صالح';
        if (durationHint) {
          if (startVal && endVal && new Date(endVal) <= new Date(startVal)) {
            durationHint.textContent = 'خطأ: نهاية الوردية يجب أن تكون بعد بداية الوردية';
            durationHint.style.color = '#ef4444';
          } else {
            durationHint.textContent = 'يرجى تحديد بداية ونهاية الوردية';
            durationHint.style.color = 'var(--prd-text-muted)';
          }
        }
        if (durationBox) durationBox.style.borderColor = '#cbd5e1';
      }

      // 2. Gross Production (Sum of Product Quantities)
      let totalGross = 0;
      this.selectedProducts.forEach((item, code) => {
        const qty = parseFloat(item.quantity);
        if (!isNaN(qty) && qty > 0) {
          totalGross += qty;
        }

        // Product Rate = Product Quantity / Duration Hours
        const itemRateEl = document.getElementById(`prd-item-rate-${code}`);
        if (itemRateEl) {
          if (durationHours && durationHours > 0 && !isNaN(qty) && qty > 0) {
            const prodRate = qty / durationHours;
            itemRateEl.textContent = `${prodRate.toFixed(3)} طن/س`;
          } else {
            itemRateEl.textContent = '—';
          }
        }
      });

      // 3. Net Production = Gross - Daily Waste
      const waste = isNaN(wasteVal) ? 0 : Math.max(0, wasteVal);
      const netProduction = Math.max(0, totalGross - waste);

      // 4. Overall Production Rate = Gross / Duration Hours
      let overallRate = null;
      if (durationHours && durationHours > 0 && totalGross > 0) {
        overallRate = totalGross / durationHours;
      }

      // 5. Waste Percentage = (Daily Waste / Total Gross) * 100
      let wastePct = null;
      if (totalGross > 0) {
        wastePct = (waste / totalGross) * 100;
      }

      // Update Live Arithmetic Displays
      const grossDisp = document.getElementById('prd-calc-gross-display');
      const netDisp = document.getElementById('prd-calc-net-display');
      const rateDisp = document.getElementById('prd-calc-rate-display');
      const wastePctDisp = document.getElementById('prd-calc-waste-pct-display');

      if (grossDisp) grossDisp.textContent = `${totalGross.toFixed(3)} طن`;
      if (netDisp) netDisp.textContent = `${netProduction.toFixed(3)} طن`;
      if (rateDisp) {
        rateDisp.textContent = overallRate !== null ? `${overallRate.toFixed(3)} طن/س` : '—';
      }
      if (wastePctDisp) {
        wastePctDisp.textContent = wastePct !== null ? `${wastePct.toFixed(3)}%` : '—';
      }
    }

    /* ----------------------------------------------------------------------
       Form Submission & Validation
       ---------------------------------------------------------------------- */
    static async handleDailySubmission() {
      const submitBtn = document.getElementById('prd-btn-submit-daily');
      const spinner = document.getElementById('prd-submit-spinner');

      const dateVal = document.getElementById('prd-input-date')?.value;
      const startVal = document.getElementById('prd-input-start-time')?.value;
      const endVal = document.getElementById('prd-input-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-input-waste')?.value || 0);
      const notesVal = document.getElementById('prd-input-notes')?.value || '';

      if (!dateVal) {
        this.notify('يرجى تحديد تاريخ الإنتاج', 'error');
        return;
      }

      if (!startVal || !endVal) {
        this.notify('يرجى تحديد بداية ونهاية الوردية بدقة', 'error');
        return;
      }

      const durationHours = this.calculateDurationHours(startVal, endVal);
      if (!durationHours || durationHours <= 0) {
        this.notify('توقيت الوردية غير صالح: يجب أن يكون وقت النهاية بعد وقت البداية', 'error');
        return;
      }

      if (this.selectedProducts.size === 0) {
        this.notify('يرجى اختيار منتج واحد على الأقل وإدخال كميته المنتجة', 'error');
        return;
      }

      const productsPayload = [];
      let missingQty = false;

      this.selectedProducts.forEach((item, code) => {
        const qty = parseFloat(item.quantity);
        if (isNaN(qty) || qty <= 0) {
          missingQty = true;
        } else {
          productsPayload.push({
            productId: item.id || null,
            productName: item.name,
            quantity: qty,
            unit: item.unit || 'طن'
          });
        }
      });

      if (missingQty || productsPayload.length === 0) {
        this.notify('يرجى إدخال كمية منتجة موجبة (> 0) لكل منتج محدد', 'error');
        return;
      }

      if (isNaN(wasteVal) || wasteVal < 0) {
        this.notify('كمية الهالك يجب أن تكون رقماً موجباً أو صفراً', 'error');
        return;
      }

      const payload = {
        productionDate: dateVal,
        shiftStart: startVal,
        shiftEnd: endVal,
        wasteQuantity: wasteVal,
        notes: notesVal,
        products: productsPayload
      };

      if (submitBtn) submitBtn.disabled = true;
      if (spinner) spinner.style.display = 'inline-block';

      try {
        const res = await window.ChatApi.createDailyProductionSubmission(payload);
        if (res && res.success) {
          const refNum = res.data?.submissionNumber || res.data?.referenceNumber || '';
          this.notify(`تم حفظ تسجيل الإنتاج اليومي بنجاح برقم: ${refNum}`, 'success');
          this.resetDailyForm();
          this.loadStats();
          this.loadSubmissions();
          this.showSection('summary');
        } else {
          this.notify(res?.message || 'فشل حفظ تسجيل الإنتاج', 'error');
        }
      } catch (err) {
        console.error('Error submitting daily production:', err);
        this.notify('حدث خطأ غير متوقع أثناء حفظ تسجيل الإنتاج', 'error');
      } finally {
        if (submitBtn) submitBtn.disabled = false;
        if (spinner) spinner.style.display = 'none';
      }
    }

    static resetDailyForm() {
      this.selectedProducts.clear();
      this.renderSelectedProducts();

      const wasteInput = document.getElementById('prd-input-waste');
      if (wasteInput) wasteInput.value = '0.000';

      const notesInput = document.getElementById('prd-input-notes');
      if (notesInput) notesInput.value = '';

      this.setDefaultDateAndTimes();
      this.updateLiveCalculations();
    }

    /* ----------------------------------------------------------------------
       Today's Stats & Summary Dashboard
       ---------------------------------------------------------------------- */
    static async loadStats() {
      try {
        const res = await window.ChatApi.getDailyProductionSummaryStats();
        if (res && res.success && res.data) {
          const stats = res.data;
          const grossEl = document.getElementById('prd-stat-today-gross');
          const wasteEl = document.getElementById('prd-stat-today-waste');
          const netEl = document.getElementById('prd-stat-today-net');
          const rateEl = document.getElementById('prd-stat-today-rate');
          const wastePctEl = document.getElementById('prd-stat-today-waste-pct');
          const subCountEl = document.getElementById('prd-stat-today-submissions');

          const gross = stats.totalGrossToday ?? stats.totalGrossProduction ?? 0;
          const waste = stats.totalWasteToday ?? stats.totalWasteQuantity ?? 0;
          const net = stats.netProductionToday ?? stats.totalNetProduction ?? 0;
          const rate = stats.overallProductionRate ?? stats.overallHourlyRate ?? 0;
          const wastePct = stats.averageWastePercentage ?? stats.wastePercentage ?? 0;
          const count = stats.totalSubmissionsCount ?? stats.shiftSubmissionsCount ?? 0;

          if (grossEl) grossEl.textContent = `${Number(gross).toFixed(3)} طن`;
          if (wasteEl) wasteEl.textContent = `${Number(waste).toFixed(3)} طن`;
          if (netEl) netEl.textContent = `${Number(net).toFixed(3)} طن`;
          if (rateEl) rateEl.textContent = `${Number(rate).toFixed(3)} طن/ساعة`;
          if (wastePctEl) wastePctEl.textContent = `${Number(wastePct).toFixed(2)}%`;
          if (subCountEl) subCountEl.textContent = `${count}`;
        }
      } catch (err) {
        console.error('Error loading production stats:', err);
      }
    }

    static async loadSubmissions(filters = {}) {
      const tbody = document.getElementById('prd-daily-submissions-tbody');
      if (tbody) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: var(--prd-text-muted);">جاري تحميل سجلات الإنتاج اليومية...</td></tr>';
      }

      try {
        const res = await window.ChatApi.getDailyProductionSubmissions(filters);
        if (res && res.success && Array.isArray(res.data)) {
          const submissions = res.data;
          this.renderSubmissionsTable(submissions);
          this.renderManagementSummary(submissions);

          const badgeCount = document.getElementById('prd-badge-records-count');
          if (badgeCount) badgeCount.textContent = `${submissions.length}`;

          this.populateEmployeeFilter(submissions);
        } else {
          if (tbody) {
            tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">تعذر تحميل السجلات</td></tr>';
          }
        }
      } catch (err) {
        console.error('Error loading submissions:', err);
        if (tbody) {
          tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">خطأ أثناء الاتصال بالخادم</td></tr>';
        }
      }
    }

    static populateEmployeeFilter(submissions) {
      const empSelect = document.getElementById('prd-filter-employee');
      if (!empSelect) return;

      const currentVal = empSelect.value;
      const emps = new Set();
      submissions.forEach(s => {
        if (s.employeeName) emps.add(s.employeeName);
      });

      empSelect.innerHTML = '<option value="">جميع مدراء الإنتاج</option>';
      emps.forEach(emp => {
        const opt = document.createElement('option');
        opt.value = emp;
        opt.textContent = emp;
        if (emp === currentVal) opt.selected = true;
        empSelect.appendChild(opt);
      });
    }

    static renderSubmissionsTable(submissions) {
      const tbody = document.getElementById('prd-daily-submissions-tbody');
      if (!tbody) return;

      if (submissions.length === 0) {
        tbody.innerHTML = `
          <tr>
            <td colspan="9" style="text-align: center; padding: 3rem 1.5rem; color: var(--prd-text-muted);">
              <div style="font-size: 2rem; margin-bottom: 0.5rem;">📋</div>
              <div style="font-weight: 700; font-size: 1rem; color: var(--prd-navy-dark);">لا توجد سجلات إنتاج مطابقة للبحث</div>
              <div style="font-size: 0.85rem; margin-top: 0.25rem;">استخدم نموذج "تسجيل الإنتاج اليومي" لتسجيل إنتاج الوردية</div>
            </td>
          </tr>
        `;
        return;
      }

      tbody.innerHTML = '';
      submissions.forEach(s => {
        const tr = document.createElement('tr');
        tr.style.borderBottom = '1px solid var(--prd-border)';

        const items = s.details || s.products || [];
        const productsHtml = items.map(p => `
          <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; background: #f8fafc; padding: 0.2rem 0.5rem; border-radius: 4px; margin-bottom: 0.2rem; font-size: 0.78rem;">
            <span style="font-weight: 700; color: var(--prd-navy-dark);">${this.escape(p.productName || p.productNameAr)}:</span>
            <span style="font-weight: 800; color: #0284c7; direction: ltr;">${Number(p.quantity || p.producedQuantity).toFixed(3)} طن</span>
          </div>
        `).join('');

        const shiftVal = s.shift || s.shiftName || '';
        const shiftBadgeClass = shiftVal.includes('الأولى') ? 'background: #dbeafe; color: #1e40af;' : 'background: #fef3c7; color: #92400e;';

        const startTimeStr = this.formatDateTime(s.shiftStart);
        const endTimeStr = this.formatDateTime(s.shiftEnd);

        const refNum = s.submissionNumber || s.referenceNumber || '';
        const gross = s.totalGrossQuantity ?? s.totalGrossProduction ?? 0;
        const waste = s.wasteQuantity ?? 0;
        const net = s.netProductionQuantity ?? s.totalNetProduction ?? 0;
        const duration = s.durationHours ?? s.productionDurationHours ?? 0;
        const rate = s.productionRatePerHour ?? s.overallHourlyRate ?? 0;
        const wastePct = s.wastePercentage ?? 0;

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';

        tr.innerHTML = `
          <td style="padding: 0.85rem 0.75rem; font-weight: 800; color: #0284c7; font-family: monospace;">
            ${this.escape(refNum)}
          </td>
          <td style="padding: 0.85rem 0.75rem; font-weight: 700; white-space: nowrap;">
            ${prodDate}
          </td>
          <td style="padding: 0.85rem 0.75rem;">
            <div style="font-weight: 700; color: var(--prd-navy-dark); font-size: 0.88rem;">${this.escape(s.employeeName)}</div>
            <span class="prd-badge" style="display: inline-block; margin-top: 0.2rem; padding: 0.15rem 0.45rem; border-radius: 4px; font-size: 0.72rem; font-weight: 700; ${shiftBadgeClass}">
              ${this.escape(shiftVal)}
            </span>
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.78rem;">
            <div><span style="color: var(--prd-text-muted);">من:</span> ${startTimeStr}</div>
            <div><span style="color: var(--prd-text-muted);">إلى:</span> ${endTimeStr}</div>
            <div style="font-weight: 800; color: #0369a1; margin-top: 0.2rem;">⏱️ ${Number(duration).toFixed(2)} س</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; min-width: 200px;">
            ${productsHtml}
          </td>
          <td style="padding: 0.85rem 0.75rem; font-weight: 800; color: #0284c7; font-size: 0.95rem; white-space: nowrap;">
            ${Number(gross).toFixed(3)} طن
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.82rem; white-space: nowrap;">
            <div style="color: #ef4444; font-weight: 700;">هالك: ${Number(waste).toFixed(3)} طن</div>
            <div style="color: #10b981; font-weight: 800; margin-top: 0.15rem;">صافي: ${Number(net).toFixed(3)} طن</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; font-size: 0.82rem; white-space: nowrap;">
            <div style="color: var(--prd-yellow-hover); font-weight: 800;">${Number(rate).toFixed(3)} طن/س</div>
            <div style="color: #8b5cf6; font-weight: 700; margin-top: 0.15rem;">هالك: ${Number(wastePct).toFixed(2)}%</div>
          </td>
          <td style="padding: 0.85rem 0.75rem; text-align: center; white-space: nowrap;">
            <div style="display: flex; gap: 0.4rem; justify-content: center;">
              <button type="button" class="prd-btn prd-btn-outline" style="padding: 0.3rem 0.6rem; font-size: 0.75rem;" onclick="ProductionApp.showSubmissionDetails(${s.id})">
                عرض
              </button>
              <button type="button" class="prd-btn prd-btn-navy" style="padding: 0.3rem 0.6rem; font-size: 0.75rem; background: #0284c7;" onclick="ProductionApp.openEditSubmissionModal(${s.id})">
                تعديل
              </button>
            </div>
          </td>
        `;

        tbody.appendChild(tr);
      });
    }

    /* ----------------------------------------------------------------------
       Management Q&A Summary Aggregation (No Double Counting)
       ---------------------------------------------------------------------- */
    static renderManagementSummary(submissions) {
      let totalGross = 0;
      let totalWaste = 0;
      let totalDurationHours = 0;
      const productMap = new Map();

      submissions.forEach(s => {
        totalGross += Number(s.totalGrossQuantity ?? s.totalGrossProduction ?? 0);
        totalWaste += Number(s.wasteQuantity ?? 0);
        totalDurationHours += Number(s.durationHours ?? s.productionDurationHours ?? 0);

        const items = s.details || s.products || [];
        items.forEach(p => {
          const name = p.productName || p.productNameAr;
          const qty = Number(p.quantity || p.producedQuantity || 0);
          if (!productMap.has(name)) {
            productMap.set(name, { name: name, total: 0 });
          }
          productMap.get(name).total += qty;
        });
      });

      const totalNet = Math.max(0, totalGross - totalWaste);
      const avgRate = totalDurationHours > 0 ? (totalGross / totalDurationHours) : 0;
      const wastePct = totalGross > 0 ? ((totalWaste / totalGross) * 100) : 0;

      const grossEl = document.getElementById('prd-summary-total-gross');
      const wasteEl = document.getElementById('prd-summary-total-waste');
      const netEl = document.getElementById('prd-summary-total-net');
      const rateEl = document.getElementById('prd-summary-avg-rate');
      const wastePctEl = document.getElementById('prd-summary-waste-pct');
      const breakdownEl = document.getElementById('prd-summary-products-breakdown');

      if (grossEl) grossEl.textContent = `${totalGross.toFixed(3)} طن`;
      if (wasteEl) wasteEl.textContent = `${totalWaste.toFixed(3)} طن`;
      if (netEl) netEl.textContent = `${totalNet.toFixed(3)} طن`;
      if (rateEl) rateEl.textContent = `${avgRate.toFixed(3)} طن/س`;
      if (wastePctEl) wastePctEl.textContent = `${wastePct.toFixed(2)}%`;

      if (breakdownEl) {
        if (productMap.size === 0) {
          breakdownEl.innerHTML = '<span style="font-size: 0.8rem; color: #cbd5e1;">لا توجد سجلات مطابقة للشروط المحددة</span>';
        } else {
          let chipsHtml = '';
          productMap.forEach(item => {
            chipsHtml += `
              <div style="background: rgba(255, 255, 255, 0.12); border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 6px; padding: 0.35rem 0.75rem; display: flex; align-items: center; gap: 0.5rem;">
                <span style="font-size: 0.82rem; font-weight: 700; color: #ffffff;">${this.escape(item.name)}:</span>
                <span style="font-size: 0.85rem; font-weight: 800; color: #38bdf8; direction: ltr;">${item.total.toFixed(3)} طن</span>
              </div>
            `;
          });
          breakdownEl.innerHTML = chipsHtml;
        }
      }
    }

    /* ----------------------------------------------------------------------
       Summary Filter Actions
       ---------------------------------------------------------------------- */
    static applySummaryFilters() {
      const startDate = document.getElementById('prd-filter-start-date')?.value || '';
      const endDate = document.getElementById('prd-filter-end-date')?.value || '';
      const shift = document.getElementById('prd-filter-shift')?.value || '';
      const employee = document.getElementById('prd-filter-employee')?.value || '';
      const product = document.getElementById('prd-filter-product')?.value || '';

      const filters = {};
      if (startDate) filters.startDate = startDate;
      if (endDate) filters.endDate = endDate;
      if (shift) filters.shift = shift;
      if (employee) filters.employee = employee;
      if (product) filters.product = product;

      this.loadSubmissions(filters);
    }

    static resetSummaryFilters() {
      const startDateInput = document.getElementById('prd-filter-start-date');
      const endDateInput = document.getElementById('prd-filter-end-date');
      const shiftSelect = document.getElementById('prd-filter-shift');
      const empSelect = document.getElementById('prd-filter-employee');
      const prodSelect = document.getElementById('prd-filter-product');

      if (startDateInput) startDateInput.value = '';
      if (endDateInput) endDateInput.value = '';
      if (shiftSelect) shiftSelect.value = '';
      if (empSelect) empSelect.value = '';
      if (prodSelect) prodSelect.value = '';

      this.loadSubmissions();
    }

    /* ----------------------------------------------------------------------
       Details Modal
       ---------------------------------------------------------------------- */
    static async showSubmissionDetails(id) {
      try {
        const res = await window.ChatApi.getDailyProductionSubmission(id);
        if (!res || !res.success || !res.data) {
          this.notify('تعذر استرجاع تفاصيل السجل', 'error');
          return;
        }

        const s = res.data;
        const refNum = s.submissionNumber || s.referenceNumber || '';
        const shiftVal = s.shift || s.shiftName || '';
        const gross = s.totalGrossQuantity ?? s.totalGrossProduction ?? 0;
        const waste = s.wasteQuantity ?? 0;
        const net = s.netProductionQuantity ?? s.totalNetProduction ?? 0;
        const duration = s.durationHours ?? s.productionDurationHours ?? 0;
        const rate = s.productionRatePerHour ?? s.overallHourlyRate ?? 0;
        const wastePct = s.wastePercentage ?? 0;

        const titleEl = document.getElementById('prd-detail-modal-title');
        const contentEl = document.getElementById('prd-detail-modal-content');

        if (titleEl) titleEl.textContent = `تفاصيل تسجيل الإنتاج: ${refNum}`;

        const items = s.details || s.products || [];
        const itemsHtml = items.map(p => `
          <tr style="border-bottom: 1px solid var(--prd-border);">
            <td style="padding: 0.65rem 0.75rem; font-weight: 700;">${this.escape(p.productName || p.productNameAr)}</td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 800; color: #0284c7; direction: ltr;">${Number(p.quantity || p.producedQuantity).toFixed(3)} طن</td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 700; color: var(--prd-yellow-hover); direction: ltr;">${Number(p.productionRatePerHour || 0).toFixed(3)} طن/س</td>
          </tr>
        `).join('');

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';

        if (contentEl) {
          contentEl.innerHTML = `
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; background: #f8fafc; padding: 1rem; border-radius: 8px; margin-bottom: 1.25rem;">
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">رقم التسجيل:</span>
                <span style="font-weight: 800; color: #0284c7; font-family: monospace; font-size: 1rem;">${this.escape(refNum)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">تاريخ الإنتاج:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${prodDate}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">الوردية المعينة:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${this.escape(shiftVal)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">مدير الإنتاج المسؤول:</span>
                <span style="font-weight: 800; color: var(--prd-navy-dark);">${this.escape(s.employeeName)} (${this.escape(s.employeeEmail)})</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">بداية ونهاية الوردية:</span>
                <span style="font-weight: 700; font-size: 0.85rem;">${this.formatDateTime(s.shiftStart)} إلى ${this.formatDateTime(s.shiftEnd)}</span>
              </div>
              <div>
                <span style="font-size: 0.75rem; color: var(--prd-text-muted); display: block;">مدة الإنتاج الفعلية:</span>
                <span style="font-weight: 800; color: #0369a1;">⏱️ ${Number(duration).toFixed(2)} ساعة</span>
              </div>
            </div>

            <div style="margin-bottom: 1.25rem;">
              <h4 style="margin: 0 0 0.5rem; font-size: 0.95rem; font-weight: 800; color: var(--prd-navy-dark);">المنتجات المسجلة ومعدلات الإنتاج:</h4>
              <table style="width: 100%; border-collapse: collapse; text-align: right; font-size: 0.85rem;">
                <thead>
                  <tr style="background: #f1f5f9; border-bottom: 2px solid var(--prd-border); color: var(--prd-text-muted);">
                    <th style="padding: 0.65rem 0.75rem;">اسم المنتج</th>
                    <th style="padding: 0.65rem 0.75rem;">الكمية المنتجة (طن)</th>
                    <th style="padding: 0.65rem 0.75rem;">معدل إنتاج المنتج</th>
                  </tr>
                </thead>
                <tbody>
                  ${itemsHtml}
                </tbody>
              </table>
            </div>

            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 0.75rem; background: #e0f2fe; border: 1px solid #bae6fd; border-radius: 8px; padding: 1rem; margin-bottom: 1.25rem;">
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">إجمالي الإنتاج</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #0284c7;">${Number(gross).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">كمية الهالك</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #ef4444;">${Number(waste).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">صافي الإنتاج</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #10b981;">${Number(net).toFixed(3)} طن</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">معدل الإنتاج الكلي</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: var(--prd-yellow-hover);">${Number(rate).toFixed(3)} طن/س</div>
              </div>
              <div>
                <div style="font-size: 0.72rem; color: #0369a1;">نسبة الهالك</div>
                <div style="font-size: 1.15rem; font-weight: 800; color: #8b5cf6;">${Number(wastePct).toFixed(2)}%</div>
              </div>
            </div>

            <div style="margin-bottom: 1rem;">
              <span style="font-size: 0.78rem; font-weight: 700; color: var(--prd-text-muted); display: block; margin-bottom: 0.25rem;">ملاحظات الإنتاج:</span>
              <div style="background: #ffffff; border: 1px solid var(--prd-border); border-radius: 6px; padding: 0.75rem; font-size: 0.88rem; color: var(--prd-text);">
                ${s.notes ? this.escape(s.notes) : '<span style="color: var(--prd-text-muted);">لا توجد ملاحظات مسجلة</span>'}
              </div>
            </div>

            <div style="font-size: 0.72rem; color: var(--prd-text-muted); border-top: 1px solid var(--prd-border); padding-top: 0.75rem; display: flex; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem;">
              <span>تم الإدخال بواسطة: <b>${this.escape(s.createdBy || s.employeeEmail)}</b> بتاريخ: ${this.formatDateTime(s.createdAt)}</span>
              ${s.updatedAt ? `<span>آخر تعديل بواسطة: <b>${this.escape(s.updatedBy || '')}</b> بتاريخ: ${this.formatDateTime(s.updatedAt)}</span>` : ''}
            </div>
          `;
        }

        this.openModal('prd-modal-submission-details');
      } catch (err) {
        console.error('Error viewing submission details:', err);
        this.notify('حدث خطأ أثناء تحميل تفاصيل السجل', 'error');
      }
    }

    /* ----------------------------------------------------------------------
       Edit Modal
       ---------------------------------------------------------------------- */
    static async openEditSubmissionModal(id) {
      try {
        const res = await window.ChatApi.getDailyProductionSubmission(id);
        if (!res || !res.success || !res.data) {
          this.notify('تعذر تحميل بيانات السجل للتعديل', 'error');
          return;
        }

        const s = res.data;
        document.getElementById('prd-edit-id').value = s.id;
        document.getElementById('prd-edit-ref-display').textContent = s.submissionNumber || s.referenceNumber;
        document.getElementById('prd-edit-shift-display').textContent = s.shift || s.shiftName;
        document.getElementById('prd-edit-employee-display').textContent = s.employeeName;

        const prodDate = s.productionDate ? (typeof s.productionDate === 'string' ? s.productionDate.split('T')[0] : this.formatDate(new Date(s.productionDate))) : '';
        document.getElementById('prd-edit-date').value = prodDate;
        document.getElementById('prd-edit-start-time').value = s.shiftStart.substring(0, 16);
        document.getElementById('prd-edit-end-time').value = s.shiftEnd.substring(0, 16);
        document.getElementById('prd-edit-waste').value = Number(s.wasteQuantity || 0).toFixed(3);
        document.getElementById('prd-edit-notes').value = s.notes || '';

        this.editSelectedProducts.clear();
        const items = s.details || s.products || [];
        items.forEach(p => {
          const name = p.productName || p.productNameAr;
          this.editSelectedProducts.set(name, {
            id: p.productId || p.productCatalogId,
            name: name,
            quantity: Number(p.quantity || p.producedQuantity)
          });
        });

        this.renderEditProducts();
        this.updateEditLiveCalculations();
        this.openModal('prd-modal-edit-submission');
      } catch (err) {
        console.error('Error opening edit modal:', err);
        this.notify('حدث خطأ أثناء فتح نافذة التعديل', 'error');
      }
    }

    static addEditProduct(productCode) {
      if (!productCode) return;

      const prod = this.catalog.find(p => (p.code === productCode || p.productCode === productCode)) || {
        id: 0,
        name: productCode
      };

      const name = prod.name || prod.productNameAr || productCode;
      if (this.editSelectedProducts.has(name)) return;

      this.editSelectedProducts.set(name, {
        id: prod.id,
        name: name,
        quantity: null
      });

      this.renderEditProducts();
      this.updateEditLiveCalculations();
    }

    static removeEditProduct(productName) {
      this.editSelectedProducts.delete(productName);
      this.renderEditProducts();
      this.updateEditLiveCalculations();
    }

    static renderEditProducts() {
      const container = document.getElementById('prd-edit-products-container');
      if (!container) return;

      container.innerHTML = '';
      if (this.editSelectedProducts.size === 0) {
        container.innerHTML = '<div style="color: #ef4444; font-size: 0.85rem; padding: 0.5rem;">يجب اختيار منتج واحد على الأقل</div>';
        return;
      }

      this.editSelectedProducts.forEach((item, name) => {
        const row = document.createElement('div');
        row.style.cssText = 'display: flex; align-items: center; justify-content: space-between; background: #ffffff; padding: 0.5rem 0.75rem; border: 1px solid var(--prd-border); border-radius: 6px;';

        const qtyVal = (item.quantity !== null && item.quantity !== undefined && item.quantity !== '') ? item.quantity : '';

        row.innerHTML = `
          <div style="font-weight: 700; font-size: 0.85rem;">
            ${this.escape(item.name)}
          </div>
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <input type="number" 
                   step="0.001" 
                   min="0.001" 
                   id="prd-edit-qty-${this.escape(item.name)}" 
                   style="width: 110px; padding: 0.35rem 0.55rem; font-weight: 700; border: 1px solid #0284c7; border-radius: 4px; text-align: left; direction: ltr;" 
                   value="${qtyVal}" 
                   placeholder="0.000" 
                   required>
            <button type="button" class="prd-btn-remove-prod" style="padding: 0.25rem 0.5rem;" onclick="ProductionApp.removeEditProduct('${this.escape(item.name)}')">&times;</button>
          </div>
        `;

        container.appendChild(row);

        const input = row.querySelector(`input[type="number"]`);
        input?.addEventListener('input', (e) => {
          const val = parseFloat(e.target.value);
          const currentItem = this.editSelectedProducts.get(name);
          if (currentItem) {
            currentItem.quantity = isNaN(val) ? null : val;
          }
          this.updateEditLiveCalculations();
        });
      });
    }

    static updateEditLiveCalculations() {
      const startVal = document.getElementById('prd-edit-start-time')?.value;
      const endVal = document.getElementById('prd-edit-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-edit-waste')?.value || 0);

      const durationHours = this.calculateDurationHours(startVal, endVal);

      let totalGross = 0;
      this.editSelectedProducts.forEach(item => {
        const qty = parseFloat(item.quantity);
        if (!isNaN(qty) && qty > 0) totalGross += qty;
      });

      const waste = isNaN(wasteVal) ? 0 : Math.max(0, wasteVal);
      const net = Math.max(0, totalGross - waste);
      const rate = (durationHours && durationHours > 0 && totalGross > 0) ? (totalGross / durationHours) : 0;
      const wastePct = totalGross > 0 ? ((waste / totalGross) * 100) : 0;

      document.getElementById('prd-edit-calc-gross').textContent = totalGross.toFixed(3);
      document.getElementById('prd-edit-calc-net').textContent = net.toFixed(3);
      document.getElementById('prd-edit-calc-rate').textContent = rate.toFixed(3);
      document.getElementById('prd-edit-calc-waste').textContent = `${wastePct.toFixed(2)}%`;
    }

    static async handleEditSubmissionSubmit() {
      const id = parseInt(document.getElementById('prd-edit-id')?.value, 10);
      const saveBtn = document.getElementById('prd-edit-btn-save');

      const dateVal = document.getElementById('prd-edit-date')?.value;
      const startVal = document.getElementById('prd-edit-start-time')?.value;
      const endVal = document.getElementById('prd-edit-end-time')?.value;
      const wasteVal = parseFloat(document.getElementById('prd-edit-waste')?.value || 0);
      const notesVal = document.getElementById('prd-edit-notes')?.value || '';

      if (!dateVal || !startVal || !endVal) {
        this.notify('يرجى ملء جميع الحقول المطلوبة', 'error');
        return;
      }

      const durationHours = this.calculateDurationHours(startVal, endVal);
      if (!durationHours || durationHours <= 0) {
        this.notify('توقيت الوردية غير صالح: وقت النهاية يجب أن يكون بعد وقت البداية', 'error');
        return;
      }

      const productsPayload = [];
      let missingQty = false;

      this.editSelectedProducts.forEach((item, name) => {
        const qty = parseFloat(item.quantity);
        if (isNaN(qty) || qty <= 0) {
          missingQty = true;
        } else {
          productsPayload.push({
            productId: item.id || null,
            productName: item.name,
            quantity: qty,
            unit: 'طن'
          });
        }
      });

      if (missingQty || productsPayload.length === 0) {
        this.notify('يرجى إدخال كمية منتجة موجبة لكل منتج محدد', 'error');
        return;
      }

      const payload = {
        productionDate: dateVal,
        shiftStart: startVal,
        shiftEnd: endVal,
        wasteQuantity: isNaN(wasteVal) ? 0 : wasteVal,
        notes: notesVal,
        products: productsPayload
      };

      if (saveBtn) saveBtn.disabled = true;

      try {
        const res = await window.ChatApi.updateDailyProductionSubmission(id, payload);
        if (res && res.success) {
          this.notify('تم تحديث تسجيل الإنتاج بنجاح', 'success');
          this.closeModal('prd-modal-edit-submission');
          this.loadStats();
          this.loadSubmissions();
        } else {
          this.notify(res?.message || 'فشل تحديث تسجيل الإنتاج', 'error');
        }
      } catch (err) {
        console.error('Error updating submission:', err);
        this.notify('حدث خطأ أثناء حفظ التعديلات', 'error');
      } finally {
        if (saveBtn) saveBtn.disabled = false;
      }
    }

    /* ----------------------------------------------------------------------
       Modal Helpers & Notifications
       ---------------------------------------------------------------------- */
    static openModal(modalId) {
      const el = document.getElementById(modalId);
      if (el) el.style.display = 'flex';
    }

    static closeModal(modalId) {
      const el = document.getElementById(modalId);
      if (el) el.style.display = 'none';
    }

    static notify(message, type = 'info') {
      if (window.ChatApp && typeof window.ChatApp.showToast === 'function') {
        window.ChatApp.showToast(message, type);
      } else {
        alert(message);
      }
    }

    static formatDate(d) {
      const year = d.getFullYear();
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    }

    static formatDateTime(dtStr) {
      if (!dtStr) return '—';
      try {
        const d = new Date(dtStr);
        if (isNaN(d.getTime())) return dtStr;
        return d.toLocaleString('ar-EG', {
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
          hour: '2-digit',
          minute: '2-digit',
          hour12: true
        });
      } catch {
        return dtStr;
      }
    }

    static escape(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }

    /* ----------------------------------------------------------------------
       1. PRODUCTION PROCESS SECTION (عملية الإنتاج - سحب المواد الخام)
       ---------------------------------------------------------------------- */
    static initProcessSection() {
      const procDate = document.getElementById('prd-proc-date');
      if (procDate && !procDate.value) {
        const now = new Date();
        const tzOffset = now.getTimezoneOffset() * 60000;
        procDate.value = (new Date(now.getTime() - tzOffset)).toISOString().slice(0, 16);
      }

      const locFilter = document.getElementById('prd-proc-location-filter');
      locFilter?.addEventListener('change', () => this.filterAndRenderBatches());

      const searchInput = document.getElementById('prd-proc-search');
      searchInput?.addEventListener('input', () => this.filterAndRenderBatches());

      const refreshBatchesBtn = document.getElementById('prd-proc-btn-refresh-batches');
      refreshBatchesBtn?.addEventListener('click', () => {
        this.loadAvailableBatches();
        this.loadNextNumbers();
      });

      const resetBtn = document.getElementById('prd-proc-btn-reset');
      resetBtn?.addEventListener('click', () => this.resetProcessForm());

      const procForm = document.getElementById('prd-process-form');
      procForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleProcessSubmit();
      });

      // Ready Orders Selection Event
      const orderSelect = document.getElementById('prd-proc-order-select');
      orderSelect?.addEventListener('change', () => {
        const oId = parseInt(orderSelect.value, 10);
        const badge = document.getElementById('prd-proc-order-info-badge');
        if (!oId) {
          if (badge) badge.style.display = 'none';
          return;
        }
        if (badge) badge.style.display = 'block';
        const order = this.cachedReadyOrders ? this.cachedReadyOrders.find(x => x.id === oId) : null;
        if (order) {
          const notesInput = document.getElementById('prd-proc-notes');
          if (notesInput && !notesInput.value) {
            notesInput.value = `أمر إنتاج المختبر: ${order.orderNumber} - المنتج: ${order.productName} - ${order.productionInstructions || ''}`;
          }
          (order.items || []).forEach(it => {
            if (it.issuedQuantity > 0) {
              const b = this.availableBatches.find(x => x.id === it.warehouseBatchId);
              if (b) {
                this.selectedBatches.set(b.id, {
                  batch: b,
                  consumedQuantity: it.issuedQuantity
                });
              } else {
                const virtualBatch = {
                  id: it.warehouseBatchId,
                  batchNumber: it.batchNumber,
                  materialName: it.materialName,
                  materialClassification: it.materialType,
                  currentQuantity: it.issuedQuantity,
                  unit: it.unit || 'طن',
                  storageLocationName: it.storageLocationName,
                  sampleCode: it.sampleCode,
                  productionReferenceNumber: it.productionReferenceNumber
                };
                this.selectedBatches.set(it.warehouseBatchId, {
                  batch: virtualBatch,
                  consumedQuantity: it.issuedQuantity
                });
              }
            }
          });
          this.renderSelectedBatches();
          this.filterAndRenderBatches();
        }
      });
    }

    static async loadReadyOrdersForProcess() {
      const select = document.getElementById('prd-proc-order-select');
      if (!select) return;
      try {
        const res = await window.ChatApi.getProductionReadyOrders();
        if (res && res.success && Array.isArray(res.data)) {
          this.cachedReadyOrders = res.data;
          select.innerHTML = '<option value="">-- تشغيل مباشر بدون أمر إنتاج مسبق --</option>' +
            res.data.map(o => '<option value="' + o.id + '">' +
              o.orderNumber + ' — ' + o.productName + ' (المنقول: ' + Number(o.totalTransferredQuantity).toFixed(3) + ' طن - ' + (o.statusArabic || o.status) + ')' +
            '</option>').join('');
        }
      } catch (err) {
        console.warn('Could not load ready production orders', err);
      }
    }

    static async loadNextNumbers() {
      try {
        const res = await window.ChatApi.getProductionNextNumbers();
        if (res && res.success && res.data) {
          const badge = document.getElementById('prd-process-expected-id');
          if (badge && res.data.nextProcessNumber) {
            badge.textContent = res.data.nextProcessNumber;
          }
          const mfgInput = document.getElementById('prd-fin-mfg-number');
          if (mfgInput && !mfgInput.value && res.data.nextManufactureNumber) {
            mfgInput.value = res.data.nextManufactureNumber;
          }
        }
      } catch (err) {
        console.warn('Failed to load production next numbers:', err);
      }
    }

    static async loadAvailableBatches() {
      const tbody = document.getElementById('prd-proc-batches-tbody');
      if (tbody) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; padding: 2rem; color: var(--prd-text-muted);">جاري تحميل تشغيلات المستودع المتاحة...</td></tr>';
      }
      try {
        const res = await window.ChatApi.getAvailableProductionBatches();
        if (res && res.success && Array.isArray(res.data)) {
          this.availableBatches = res.data;
          this.populateLocationFilter();
          this.filterAndRenderBatches();
          this.loadReadyOrdersForProcess();
        } else {
          if (tbody) {
            tbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #ef4444;">${this.escape(res?.message || 'فشل تحميل تشغيلات المستودع')}</td></tr>`;
          }
        }
      } catch (err) {
        console.error('Error loading available batches:', err);
        if (tbody) {
          tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; padding: 2rem; color: #ef4444;">حدث خطأ أثناء تحميل تشغيلات المستودع</td></tr>';
        }
      }
    }

    static populateLocationFilter() {
      const locFilter = document.getElementById('prd-proc-location-filter');
      if (!locFilter) return;
      const currentVal = locFilter.value;
      const locations = new Set();
      (this.availableBatches || []).forEach(b => {
        if (b.warehouseLocation && b.warehouseLocation.trim()) {
          locations.add(b.warehouseLocation.trim());
        }
      });

      let opts = '<option value="">جميع المستودعات والمواقع</option>';
      locations.forEach(loc => {
        opts += `<option value="${this.escape(loc)}">${this.escape(loc)}</option>`;
      });
      locFilter.innerHTML = opts;
      if (locations.has(currentVal)) {
        locFilter.value = currentVal;
      }
    }

    static filterAndRenderBatches() {
      const tbody = document.getElementById('prd-proc-batches-tbody');
      if (!tbody) return;

      const locFilter = document.getElementById('prd-proc-location-filter');
      const searchInput = document.getElementById('prd-proc-search');

      const selectedLoc = locFilter ? locFilter.value.trim().toLowerCase() : '';
      const search = searchInput ? searchInput.value.trim().toLowerCase() : '';

      const filtered = (this.availableBatches || []).filter(b => {
        if (selectedLoc && (b.warehouseLocation || '').toLowerCase() !== selectedLoc) {
          return false;
        }
        if (search) {
          const matchBatch = (b.batchNumber || '').toLowerCase().includes(search);
          const matchRef = (b.laboratoryReferenceNumber || '').toLowerCase().includes(search);
          const matchSample = (b.sampleCode || '').toLowerCase().includes(search);
          const matchMat = (b.materialName || '').toLowerCase().includes(search);
          const matchLoc = (b.warehouseLocation || '').toLowerCase().includes(search);
          if (!matchBatch && !matchRef && !matchSample && !matchMat && !matchLoc) {
            return false;
          }
        }
        return true;
      });

      if (filtered.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; padding: 2rem; color: var(--prd-text-muted);">لا توجد تشغيلات مطابقة للمستودع المحدد أو معايير البحث</td></tr>';
        this.updateProcessSummary();
        return;
      }

      let html = '';
      filtered.forEach(b => {
        const isSelected = this.selectedBatches.has(b.id);
        const selData = isSelected ? this.selectedBatches.get(b.id) : null;
        const currentQtyVal = isSelected ? selData.consumedQuantity : b.currentQuantity;
        const rowBg = isSelected ? '#f0fdf4' : 'transparent';

        html += `
          <tr style="border-bottom: 1px solid #e2e8f0; background: ${rowBg};" data-row-batch-id="${b.id}">
            <td style="padding: 0.65rem 0.5rem; text-align: center;">
              <input type="checkbox" class="prd-proc-batch-chk" data-id="${b.id}" ${isSelected ? 'checked' : ''} style="width: 18px; height: 18px; cursor: pointer;">
            </td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 700; color: var(--prd-navy-dark);">
              ${this.escape(b.batchNumber)}
            </td>
            <td style="padding: 0.65rem 0.75rem;">
              <span class="prd-badge" style="background: #e0f2fe; color: #0369a1; font-weight: 700;">
                ${this.escape(b.laboratoryReferenceNumber || '—')}
              </span>
            </td>
            <td style="padding: 0.65rem 0.75rem; font-size: 0.85rem; color: #64748b;">
              ${this.escape(b.sampleCode || '—')}
            </td>
            <td style="padding: 0.65rem 0.75rem; font-weight: 600;">
              ${this.escape(b.materialName || '—')}
              ${b.materialType ? `<span style="font-size: 0.75rem; color: #64748b; display: block;">${this.escape(b.materialType)}</span>` : ''}
            </td>
            <td style="padding: 0.65rem 0.75rem; font-size: 0.85rem;">
              ${this.escape(b.warehouseLocation || 'المستودع الرئيسي')}
            </td>
            <td style="padding: 0.65rem 0.75rem;">
              <strong style="color: #0284c7;">${parseFloat(b.currentQuantity).toFixed(3)}</strong>
              <span style="font-size: 0.75rem; color: #64748b;">${this.escape(b.unit || 'طن')}</span>
            </td>
            <td style="padding: 0.65rem 0.75rem;">
              <input type="number" class="prd-input prd-proc-batch-qty" data-id="${b.id}" step="0.001" min="0.001" max="${b.currentQuantity}" value="${parseFloat(currentQtyVal).toFixed(3)}" ${isSelected ? '' : 'disabled'} style="width: 140px; padding: 0.4rem 0.6rem; font-weight: 700;">
            </td>
          </tr>
        `;
      });

      tbody.innerHTML = html;

      // Bind checkboxes & inputs
      const chks = tbody.querySelectorAll('.prd-proc-batch-chk');
      chks.forEach(chk => {
        chk.addEventListener('change', () => {
          const bId = parseInt(chk.dataset.id, 10);
          const batchObj = this.availableBatches.find(x => x.id === bId);
          if (!batchObj) return;

          const row = tbody.querySelector(`tr[data-row-batch-id="${bId}"]`);
          const qtyInput = row?.querySelector('.prd-proc-batch-qty');

          if (chk.checked) {
            if (row) row.style.background = '#f0fdf4';
            if (qtyInput) {
              qtyInput.disabled = false;
              let qty = parseFloat(qtyInput.value);
              if (isNaN(qty) || qty <= 0 || qty > batchObj.currentQuantity) {
                qty = batchObj.currentQuantity;
                qtyInput.value = qty.toFixed(3);
              }
              this.selectedBatches.set(bId, {
                batch: batchObj,
                consumedQuantity: qty
              });
            }
          } else {
            if (row) row.style.background = 'transparent';
            if (qtyInput) qtyInput.disabled = true;
            this.selectedBatches.delete(bId);
          }

          this.updateProcessSummary();
        });
      });

      const qtyInputs = tbody.querySelectorAll('.prd-proc-batch-qty');
      qtyInputs.forEach(input => {
        input.addEventListener('input', () => {
          const bId = parseInt(input.dataset.id, 10);
          const batchObj = this.availableBatches.find(x => x.id === bId);
          if (!batchObj) return;

          let val = parseFloat(input.value);
          if (!isNaN(val) && val > 0) {
            if (val > batchObj.currentQuantity) {
              this.notify(`الكمية المدخلة (${val}) تتجاوز المتاح بالتشغيلة (${batchObj.currentQuantity})`, 'warning');
              val = batchObj.currentQuantity;
              input.value = val.toFixed(3);
            }
            if (this.selectedBatches.has(bId)) {
              this.selectedBatches.get(bId).consumedQuantity = val;
            }
          }
          this.updateProcessSummary();
        });
      });

      this.updateProcessSummary();
    }

    static updateProcessSummary() {
      const countEl = document.getElementById('prd-proc-selected-batches-count');
      const totalEl = document.getElementById('prd-proc-total-consumed-qty');

      let totalQty = 0;
      this.selectedBatches.forEach(item => {
        totalQty += (parseFloat(item.consumedQuantity) || 0);
      });

      if (countEl) countEl.textContent = this.selectedBatches.size;
      if (totalEl) totalEl.textContent = totalQty.toFixed(3);
    }

    static resetProcessForm() {
      this.selectedBatches.clear();
      const searchInput = document.getElementById('prd-proc-search');
      if (searchInput) searchInput.value = '';
      const locFilter = document.getElementById('prd-proc-location-filter');
      if (locFilter) locFilter.value = '';
      const fileInput = document.getElementById('prd-proc-weighbridge-file');
      if (fileInput) fileInput.value = '';
      const notesInput = document.getElementById('prd-proc-notes');
      if (notesInput) notesInput.value = '';
      const procDate = document.getElementById('prd-proc-date');
      if (procDate) {
        const now = new Date();
        const tzOffset = now.getTimezoneOffset() * 60000;
        procDate.value = (new Date(now.getTime() - tzOffset)).toISOString().slice(0, 16);
      }
      this.filterAndRenderBatches();
          this.loadReadyOrdersForProcess();
    }

    static async handleProcessSubmit() {
      if (this.isSubmittingProcess) return;
      this.isSubmittingProcess = true;
      if (this.selectedBatches.size === 0) {
        this.notify('يرجى تحديد تشغيلة أو مادة أولية واحدة على الأقل للاستهلاك في عملية الإنتاج', 'error');
        return;
      }

      const materialsPayload = [];
      let hasInvalidQty = false;

      const orderSelect = document.getElementById('prd-proc-order-select');
      const selectedOrderId = orderSelect ? orderSelect.value : '';
      const selectedOrder = selectedOrderId && this.cachedReadyOrders ? this.cachedReadyOrders.find(o => o.id === parseInt(selectedOrderId, 10)) : null;

      this.selectedBatches.forEach((item, bId) => {
        const qty = parseFloat(item.consumedQuantity);
        const orderItem = selectedOrder ? (selectedOrder.items || []).find(it => it.warehouseBatchId === bId) : null;
        const maxAllowed = orderItem ? orderItem.issuedQuantity : item.batch.currentQuantity;

        if (isNaN(qty) || qty <= 0 || (maxAllowed > 0 && qty > maxAllowed)) {
          hasInvalidQty = true;
        } else {
          materialsPayload.push({
            warehouseBatchId: bId,
            consumedQuantity: qty,
            unit: item.batch.unit || 'طن'
          });
        }
      });

      if (hasInvalidQty || materialsPayload.length === 0) {
        this.notify('يرجى التأكد من أن جميع الكميات المستهلكة المحددة موجبة ولا تتجاوز الكميات المنقولة أو الرصيد المتاح', 'error');
        return;
      }

      const fileInput = document.getElementById('prd-proc-weighbridge-file');
      if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        this.notify('يرجى إرفاق تذكرة ميزان البسكول للمواد الخام الواردة للعملية (PDF, JPG, PNG)', 'error');
        return;
      }

      const file = fileInput.files[0];
      const validExts = ['.pdf', '.jpg', '.jpeg', '.png'];
      const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
      if (!validExts.includes(ext)) {
        this.notify('نوع ملف ميزان البسكول غير مدعوم. الصيغ المسموحة: PDF, JPG, PNG', 'error');
        return;
      }
      if (file.size > 25 * 1024 * 1024) {
        this.notify('حجم ملف ميزان البسكول يتجاوز الحد الأقصى المسموح (25 ميجابايت)', 'error');
        return;
      }

      const procDate = document.getElementById('prd-proc-date')?.value || new Date().toISOString();
      const notes = document.getElementById('prd-proc-notes')?.value || '';

      const formData = new FormData();
      formData.append('ProductionDate', procDate);
      formData.append('Notes', notes);
      if (selectedOrderId) {
        formData.append('ProductionOrderId', selectedOrderId);
      }
      formData.append('MaterialsJson', JSON.stringify(materialsPayload));
      formData.append('incomingWeighbridge', file);
      formData.append('weighbridgeFile', file);

      const submitBtn = document.getElementById('prd-proc-btn-submit');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري حفظ وتأكيد العملية وخصم المخزون...';
      }

      try {
        const res = await window.ChatApi.submitProductionProcess(formData);
        if (res && res.success && res.data) {
          const p = res.data;
          this.notify(`تم إنشاء عملية الإنتاج بنجاح برقم: ${p.processNumber}. تم خصم ${p.totalInputQuantity} طن من المستودع وحفظ تذكرة البسكول.`, 'success');
          this.resetProcessForm();
          this.loadAvailableBatches();
          this.loadNextNumbers();

          // Switch to Finished Product tab and pre-select new process
          this.showSection('finished');
          setTimeout(() => {
            this.loadProcessesForFinished(p.id);
          }, 300);
        } else {
          this.notify(res?.message || 'فشل حفظ وتأكيد عملية الإنتاج', 'error');
        }
      } catch (err) {
        console.error('Error submitting production process:', err);
        this.notify('حدث خطأ أثناء حفظ وتأكيد عملية الإنتاج', 'error');
      } finally {
        this.isSubmittingProcess = false;
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ وتأكيد عملية الإنتاج';
        }
      }
    }

    /* ----------------------------------------------------------------------
       2. FINISHED PRODUCT & WASTE CALCULATION SECTION (المنتج النهائي وحساب الهالك)
       ---------------------------------------------------------------------- */
    static initFinishedSection() {
      // Production Process Selection
      const procSelect = document.getElementById('prd-fin-process-select');
      procSelect?.addEventListener('change', () => {
        const val = procSelect.value ? parseInt(procSelect.value, 10) : null;
        this.onProcessSelectedForFinished(val);
      });

      // Production Order Selection
      const orderSelect = document.getElementById('prd-fin-order-select');
      orderSelect?.addEventListener('change', () => {
        const val = orderSelect.value ? parseInt(orderSelect.value, 10) : null;
        this.onOrderSelectedForFinished(val);
      });

      const refreshOrdersBtn = document.getElementById('btn-refresh-finished-orders');
      refreshOrdersBtn?.addEventListener('click', () => {
        this.loadEligibleProcesses();
        this.loadOrdersForFinished();
        this.loadStorageLocationsForFinished();
      });

      const qtyInput = document.getElementById('prd-fin-quantity');
      qtyInput?.addEventListener('input', () => this.updateWasteCalculation());

      const resetBtn = document.getElementById('prd-fin-btn-reset');
      resetBtn?.addEventListener('click', () => this.resetFinishedForm());

      const finishedForm = document.getElementById('prd-finished-form');
      finishedForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        this.handleFinishedSubmit();
      });

      // Load initial lists
      this.loadEligibleProcesses();
      this.loadOrdersForFinished();
      this.loadStorageLocationsForFinished();

      // Real-time synchronization
      window.addEventListener('facestone:production-order-changed', () => {
        if (this.currentSection === 'finished') {
          this.loadEligibleProcesses();
          this.loadOrdersForFinished();
        }
      });
      window.addEventListener('facestone:warehouse-inventory-changed', () => {
        if (this.currentSection === 'finished') {
          this.loadStorageLocationsForFinished();
        }
      });
      window.addEventListener('facestone:data-changed', (e) => {
        if (['storage', 'production', 'laboratory'].includes(e.detail?.department) && this.currentSection === 'finished') {
          this.loadEligibleProcesses();
          this.loadOrdersForFinished();
          this.loadStorageLocationsForFinished();
        }
      });
    }

    static async loadStorageLocationsForFinished() {
      const select = document.getElementById('prd-fin-destination-warehouse');
      if (!select) return;

      try {
        const res = await window.ChatApi.getStorageLocations();
        let opts = '<option value="">-- اختر مستودع تخزين المنتج النهائي --</option>';
        if (res && res.success && Array.isArray(res.data)) {
          this.storageLocationsForFinished = res.data;
          res.data.forEach(loc => {
            opts += `<option value="${loc.id}">${this.escape(loc.name)} (${this.escape(loc.code || 'WH')})</option>`;
          });
        }
        select.innerHTML = opts;
      } catch (err) {
        console.error('Error loading storage locations for finished product:', err);
      }
    }

    static async loadProcessesForFinished(selectedId = null) {
      return this.loadEligibleProcesses(selectedId);
    }

    static renderSelectedBatches() {
      this.updateProcessSummary();
    }

    static async loadEligibleProcesses(selectedId = null) {
      const select = document.getElementById('prd-fin-process-select');
      if (!select) return;

      try {
        const res = await window.ChatApi.getEligibleProductionProcesses();
        let opts = '<option value="">-- اختر رقم عملية الإنتاج من قاعدة البيانات --</option>';

        if (res && res.success && Array.isArray(res.data)) {
          this.eligibleProcessesForFinished = res.data;
          res.data.forEach(p => {
            const isSel = selectedId && (p.id === selectedId || p.processNumber === selectedId);
            const statusBadge = p.status === 'COMPLETED' ? 'مكتملة' : 'قيد التشغيل';
            const orderTxt = p.productionOrderNumber ? ` [أمر: ${p.productionOrderNumber}]` : '';
            opts += `<option value="${p.id}" ${isSel ? 'selected' : ''}>${this.escape(p.processNumber)}${this.escape(orderTxt)} — ${this.escape(p.targetProductName || 'منتج')} (المدخلات: ${parseFloat(p.totalInputQuantity).toFixed(2)} طن) [${statusBadge}]</option>`;
          });
        } else {
          this.eligibleProcessesForFinished = [];
        }

        select.innerHTML = opts;

        if (selectedId) {
          const matchOpt = Array.from(select.options).find(o => o.value == selectedId);
          if (matchOpt) {
            select.value = selectedId;
            this.onProcessSelectedForFinished(parseInt(selectedId, 10));
          }
        } else if (select.value) {
          this.onProcessSelectedForFinished(parseInt(select.value, 10));
        }
      } catch (err) {
        console.error('Error loading eligible production processes:', err);
      }
    }

    static async onProcessSelectedForFinished(processId) {
      const card = document.getElementById('prd-fin-order-details-card');
      if (!processId) {
        this.currentProcessForFinished = null;
        if (card && !this.currentOrderForFinished) card.style.display = 'none';
        this.updateWasteCalculation();
        return;
      }

      const proc = (this.eligibleProcessesForFinished || []).find(p => p.id === processId);
      if (!proc) {
        this.currentProcessForFinished = null;
        if (card && !this.currentOrderForFinished) card.style.display = 'none';
        this.updateWasteCalculation();
        return;
      }

      this.currentProcessForFinished = proc;
      if (card) card.style.display = 'block';

      // Sync Order Select if linked
      if (proc.productionOrderId) {
        const orderSelect = document.getElementById('prd-fin-order-select');
        if (orderSelect) {
          orderSelect.value = proc.productionOrderId;
        }
      }

      const numEl = document.getElementById('prd-fin-disp-order-number');
      const prodEl = document.getElementById('prd-fin-disp-order-product');
      const targetEl = document.getElementById('prd-fin-disp-target-qty');
      const transEl = document.getElementById('prd-fin-disp-transferred-qty');
      const procEl = document.getElementById('prd-fin-disp-proc-number');
      const statusEl = document.getElementById('prd-fin-disp-order-status');

      if (numEl) numEl.textContent = proc.productionOrderNumber || '—';
      if (prodEl) prodEl.textContent = proc.targetProductName || '—';
      if (targetEl) targetEl.textContent = `${parseFloat(proc.totalInputQuantity).toFixed(3)} ${proc.unit || 'طن'}`;
      if (transEl) transEl.textContent = `${parseFloat(proc.totalInputQuantity).toFixed(3)} ${proc.unit || 'طن'}`;
      if (procEl) procEl.textContent = proc.processNumber;
      if (statusEl) {
        statusEl.textContent = proc.status === 'COMPLETED' ? 'مكتملة' : 'قيد المعالجة (IN_PROGRESS)';
        statusEl.style.background = proc.status === 'COMPLETED' ? '#dcfce7' : '#fef3c7';
        statusEl.style.color = proc.status === 'COMPLETED' ? '#15803d' : '#92400e';
      }

      // Auto-fill finished product name as READ-ONLY
      const prodNameInp = document.getElementById('prd-fin-product-name');
      if (prodNameInp) {
        prodNameInp.value = proc.targetProductName || '';
        prodNameInp.readOnly = true;
      }

      // Auto-fill target quantity as READ-ONLY
      const targetQtyInp = document.getElementById('prd-fin-target-quantity');
      if (targetQtyInp) {
        targetQtyInp.value = `${parseFloat(proc.targetQuantity || proc.totalInputQuantity || 0).toFixed(3)} طن`;
      }

      // Render consumed materials formula table
      const matTbody = document.getElementById('prd-fin-consumed-materials-tbody');
      if (matTbody) {
        let rows = '';
        (proc.consumedMaterials || []).forEach(m => {
          rows += `
            <tr style="border-bottom: 1px solid #e2e8f0;">
              <td style="padding: 6px 10px; font-weight: 700; color: #1e293b;">${this.escape(m.materialName)}</td>
              <td style="padding: 6px 10px;">${this.escape(m.materialClassification || '—')}</td>
              <td style="padding: 6px 10px;"><span class="prd-badge" style="background: #e0f2fe; color: #0369a1;">${this.escape(m.productionReferenceNumber || '—')}</span></td>
              <td style="padding: 6px 10px; font-weight: 600;">${this.escape(m.batchNumber || '—')}</td>
              <td style="padding: 6px 10px; color: #64748b;">${this.escape(m.sampleCode || '—')}</td>
              <td style="padding: 6px 10px; font-weight: 600;">${parseFloat(m.consumedQuantity).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
              <td style="padding: 6px 10px; font-weight: 700; color: #0284c7;">${parseFloat(m.consumedQuantity).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
              <td style="padding: 6px 10px; font-weight: 700; color: #059669;">${parseFloat(m.consumedQuantity).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
            </tr>
          `;
        });
        matTbody.innerHTML = rows || '<tr><td colspan="8" style="text-align: center; color: var(--prd-text-muted); padding: 1rem;">لا توجد مواد مسجلة بالعملية</td></tr>';
      }

      // Next manufacture number auto-suggest
      const mfgInput = document.getElementById('prd-fin-mfg-number');
      if (mfgInput && !mfgInput.value) {
        this.loadNextNumbers();
      }

      this.updateWasteCalculation();
    }

    static async loadOrdersForFinished(selectedId = null) {
      const select = document.getElementById('prd-fin-order-select');
      if (!select) return;

      try {
        const res = await window.ChatApi.getProductionEligibleOrdersForFinished();
        let opts = '<option value="">-- اختر أمر الإنتاج المسؤول عن تصنيع المنتج النهائي --</option>';

        if (res && res.success && Array.isArray(res.data)) {
          this.eligibleOrdersForFinished = res.data;
          res.data.forEach(o => {
            const isSel = selectedId && o.id === selectedId;
            const statusBadge = o.statusArabic || o.status;
            opts += `<option value="${o.id}" ${isSel ? 'selected' : ''}>${this.escape(o.orderNumber)} – ${this.escape(o.productName)} (${this.escape(statusBadge)}) - منقول للإنتاج: ${parseFloat(o.totalTransferredQuantity).toFixed(2)} طن</option>`;
          });
        } else {
          this.eligibleOrdersForFinished = [];
        }

        select.innerHTML = opts;

        if (selectedId) {
          const matchOpt = Array.from(select.options).find(o => o.value == selectedId);
          if (matchOpt) {
            select.value = selectedId;
            this.onOrderSelectedForFinished(parseInt(selectedId, 10));
          }
        }
      } catch (err) {
        console.error('Error loading eligible production orders for finished product:', err);
      }
    }

    static onOrderSelectedForFinished(orderId) {
      if (!orderId) {
        this.currentOrderForFinished = null;
        if (!this.currentProcessForFinished) {
          const card = document.getElementById('prd-fin-order-details-card');
          if (card) card.style.display = 'none';
        }
        this.updateWasteCalculation();
        return;
      }

      const order = (this.eligibleOrdersForFinished || []).find(o => o.id === orderId);
      if (!order) return;

      this.currentOrderForFinished = order;
      const card = document.getElementById('prd-fin-order-details-card');
      if (card) card.style.display = 'block';

      // If order has linked process, select it in process dropdown
      if (order.linkedProductionProcessId) {
        const procSelect = document.getElementById('prd-fin-process-select');
        if (procSelect) {
          procSelect.value = order.linkedProductionProcessId;
          const matchProc = (this.eligibleProcessesForFinished || []).find(p => p.id === order.linkedProductionProcessId);
          if (matchProc) this.currentProcessForFinished = matchProc;
        }
      }

      const numEl = document.getElementById('prd-fin-disp-order-number');
      const prodEl = document.getElementById('prd-fin-disp-order-product');
      const targetEl = document.getElementById('prd-fin-disp-target-qty');
      const transEl = document.getElementById('prd-fin-disp-transferred-qty');
      const procEl = document.getElementById('prd-fin-disp-proc-number');
      const statusEl = document.getElementById('prd-fin-disp-order-status');

      if (numEl) numEl.textContent = order.orderNumber;
      if (prodEl) prodEl.textContent = order.productName;
      if (targetEl) targetEl.textContent = `${parseFloat(order.targetQuantity).toFixed(3)} ${order.unit || 'طن'}`;
      if (transEl) transEl.textContent = `${parseFloat(order.totalTransferredQuantity).toFixed(3)} ${order.unit || 'طن'}`;
      if (procEl) procEl.textContent = order.linkedProductionProcessNumber || 'سيتم الربط/الإنشاء التلقائي';
      if (statusEl) {
        statusEl.textContent = order.statusArabic || order.status;
        statusEl.style.background = '#dcfce7';
        statusEl.style.color = '#15803d';
      }

      const prodNameInp = document.getElementById('prd-fin-product-name');
      if (prodNameInp && order.productName) {
        prodNameInp.value = order.productName;
        prodNameInp.readOnly = true;
      }
      const targetQtyInp = document.getElementById('prd-fin-target-quantity');
      if (targetQtyInp) {
        targetQtyInp.value = `${parseFloat(order.targetQuantity || 0).toFixed(3)} ${order.unit || 'طن'}`;
      }

      // Render consumed materials formula table
      const matTbody = document.getElementById('prd-fin-consumed-materials-tbody');
      if (matTbody && order.materials) {
        let rows = '';
        order.materials.forEach(m => {
          rows += `
            <tr style="border-bottom: 1px solid #e2e8f0;">
              <td style="padding: 6px 10px; font-weight: 700; color: #1e293b;">${this.escape(m.materialName)}</td>
              <td style="padding: 6px 10px;">${this.escape(m.materialClassification || '—')}</td>
              <td style="padding: 6px 10px;"><span class="prd-badge" style="background: #e0f2fe; color: #0369a1;">${this.escape(m.laboratoryReferenceNumber || '—')}</span></td>
              <td style="padding: 6px 10px; font-weight: 600;">${this.escape(m.batchNumber || '—')}</td>
              <td style="padding: 6px 10px; color: #64748b;">${this.escape(m.sampleCode || '—')}</td>
              <td style="padding: 6px 10px; font-weight: 600;">${parseFloat(m.plannedQuantity).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
              <td style="padding: 6px 10px; font-weight: 700; color: #0284c7;">${parseFloat(m.transferredQuantity).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
              <td style="padding: 6px 10px; font-weight: 700; color: #059669;">${parseFloat(m.availableToProduction).toFixed(3)} ${this.escape(m.unit || 'طن')}</td>
            </tr>
          `;
        });
        matTbody.innerHTML = rows || '<tr><td colspan="8" style="text-align: center; color: var(--prd-text-muted); padding: 1rem;">لا توجد مواد مسجلة بأمر الإنتاج</td></tr>';
      }

      this.updateWasteCalculation();
    }

    static updateWasteCalculation() {
      const inputEl = document.getElementById('prd-fin-calc-input');
      const outputEl = document.getElementById('prd-fin-calc-output');
      const wasteEl = document.getElementById('prd-fin-calc-waste');
      const wastePctEl = document.getElementById('prd-fin-calc-waste-pct');
      const warningEl = document.getElementById('prd-fin-waste-warning');
      const qtyInput = document.getElementById('prd-fin-quantity');

      let inputQty = 0;
      if (this.currentProcessForFinished) {
        inputQty = parseFloat(this.currentProcessForFinished.totalInputQuantity) || 0;
      } else if (this.currentOrderForFinished) {
        inputQty = parseFloat(this.currentOrderForFinished.totalTransferredQuantity) || 0;
      }

      const outputQty = qtyInput ? parseFloat(qtyInput.value) || 0 : 0;
      const wasteQty = inputQty - outputQty;
      const wastePct = inputQty > 0 ? (wasteQty / inputQty) * 100 : 0;

      if (inputEl) inputEl.textContent = `${inputQty.toFixed(3)} طن`;
      if (outputEl) outputEl.textContent = `${outputQty.toFixed(3)} طن`;
      if (wasteEl) wasteEl.textContent = `${wasteQty.toFixed(3)} طن`;
      if (wastePctEl) wastePctEl.textContent = `${wastePct.toFixed(2)}%`;

      if (outputQty > inputQty && inputQty > 0) {
        if (warningEl) warningEl.style.display = 'block';
        if (wasteEl) wasteEl.style.color = '#dc2626';
        if (wastePctEl) wastePctEl.style.color = '#dc2626';
      } else {
        if (warningEl) warningEl.style.display = 'none';
        if (wasteEl) wasteEl.style.color = '#ef4444';
        if (wastePctEl) wastePctEl.style.color = '#8b5cf6';
      }

      // Live difference calculation between actual manufactured quantity and target quantity
      const diffEl = document.getElementById('prd-fin-quantity-difference');
      const targetInput = document.getElementById('prd-fin-target-quantity');
      let targetQty = 0;
      if (this.currentOrderForFinished && this.currentOrderForFinished.targetQuantity) {
        targetQty = parseFloat(this.currentOrderForFinished.targetQuantity) || 0;
      } else if (this.currentProcessForFinished && this.currentProcessForFinished.targetQuantity) {
        targetQty = parseFloat(this.currentProcessForFinished.targetQuantity) || 0;
      } else {
        targetQty = inputQty;
      }

      if (targetInput && targetQty > 0) {
        targetInput.value = `${targetQty.toFixed(3)} طن`;
      }

      if (diffEl) {
        const diff = outputQty - targetQty;
        if (outputQty === 0 && targetQty === 0) {
          diffEl.textContent = '0.000 طن (مطابق)';
          diffEl.style.color = '#64748b';
        } else if (Math.abs(diff) < 0.0001) {
          diffEl.textContent = '0.000 طن (مطابق تماماً للمستهدف)';
          diffEl.style.color = '#15803d';
        } else if (diff > 0) {
          diffEl.textContent = `+${diff.toFixed(3)} طن (فائض إنتاج عن المستهدف)`;
          diffEl.style.color = '#0284c7';
        } else {
          diffEl.textContent = `${diff.toFixed(3)} طن (عجز عن المستهدف)`;
          diffEl.style.color = '#d97706';
        }
      }
    }

    static resetFinishedForm() {
      const form = document.getElementById('prd-finished-form');
      if (form) form.reset();
      this.currentOrderForFinished = null;
      this.currentProcessForFinished = null;
      const orderCard = document.getElementById('prd-fin-order-details-card');
      if (orderCard) orderCard.style.display = 'none';
      this.updateWasteCalculation();
      this.loadNextNumbers();
    }

    static async handleFinishedSubmit() {
      if (this.isSubmittingFinished) return;
      this.isSubmittingFinished = true;
      const procSelect = document.getElementById('prd-fin-process-select');
      const procIdVal = procSelect?.value ? parseInt(procSelect.value, 10) : null;

      const orderSelect = document.getElementById('prd-fin-order-select');
      const orderIdVal = orderSelect?.value ? parseInt(orderSelect.value, 10) : null;

      if (!procIdVal && !orderIdVal && !this.currentProcessForFinished && !this.currentOrderForFinished) {
        this.notify('يرجى اختيار رقم عملية الإنتاج (Process Number) أو أمر الإنتاج المسؤول عن تصنيع المنتج النهائي', 'error');
        return;
      }

      const productName = document.getElementById('prd-fin-product-name')?.value?.trim();
      if (!productName) {
        this.notify('يرجى اختيار أو إدخال اسم المنتج النهائي المصنع', 'error');
        return;
      }

      const mfgNumber = document.getElementById('prd-fin-mfg-number')?.value?.trim();
      if (!mfgNumber) {
        this.notify('يرجى إدخال رقم التصنيع (Manufacture Number)', 'error');
        return;
      }

      const qty = parseFloat(document.getElementById('prd-fin-quantity')?.value);
      if (isNaN(qty) || qty <= 0) {
        this.notify('يرجى إدخال كمية مصنعة موجبة أكبر من الصفر', 'error');
        return;
      }

      let inputQty = 0;
      if (this.currentProcessForFinished) {
        inputQty = parseFloat(this.currentProcessForFinished.totalInputQuantity) || 0;
      } else if (this.currentOrderForFinished) {
        inputQty = parseFloat(this.currentOrderForFinished.totalTransferredQuantity) || 0;
      }

      if (inputQty > 0 && qty > inputQty) {
        this.notify(`خطأ في تدقيق الكتلة: كمية الناتج المصنع (${qty} طن) أكبر من إجمالي المواد المستلمة للإنتاج (${inputQty} طن). الهالك لا يمكن أن يكون سالباً!`, 'error');
        return;
      }

      const destWarehouseSelect = document.getElementById('prd-fin-destination-warehouse');
      const destLocationId = destWarehouseSelect?.value ? parseInt(destWarehouseSelect.value, 10) : null;
      if (!destLocationId || destLocationId <= 0) {
        this.notify('يرجى اختيار مستودع وجهة تخزين المنتج النهائي (Destination Warehouse)', 'error');
        destWarehouseSelect?.focus();
        return;
      }

      const fileInput = document.getElementById('prd-fin-weighbridge-file');
      if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        this.notify('يرجى إرفاق تذكرة ميزان البسكول للمنتج التام النهائي (PDF, JPG, PNG) — المرفق إلزامي', 'error');
        fileInput?.focus();
        return;
      }

      const file = fileInput.files[0];
      const validExts = ['.pdf', '.jpg', '.jpeg', '.png'];
      const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
      if (!validExts.includes(ext)) {
        this.notify('نوع ملف ميزان البسكول غير مدعوم. الصيغ المسموحة: PDF, JPG, PNG', 'error');
        return;
      }
      if (file.size > 25 * 1024 * 1024) {
        this.notify('حجم ملف ميزان البسكول يتجاوز الحد الأقصى المسموح (25 ميجابايت)', 'error');
        return;
      }

      const notes = document.getElementById('prd-fin-notes')?.value || '';

      const formData = new FormData();
      if (procIdVal) {
        formData.append('ProductionProcessId', procIdVal);
      } else if (this.currentProcessForFinished?.id) {
        formData.append('ProductionProcessId', this.currentProcessForFinished.id);
      }
      if (orderIdVal) {
        formData.append('ProductionOrderId', orderIdVal);
      } else if (this.currentOrderForFinished?.id) {
        formData.append('ProductionOrderId', this.currentOrderForFinished.id);
      }

      formData.append('FinishedProductName', productName);
      formData.append('ManufactureNumber', mfgNumber);
      formData.append('ManufacturedQuantity', qty);
      formData.append('Unit', 'طن');
      const compDate = document.getElementById('prd-fin-completion-date')?.value;
      if (compDate) {
        formData.append('ProductionDate', compDate);
      }
      formData.append('DestinationStorageLocationId', destLocationId);
      formData.append('Notes', notes);
      formData.append('weighbridgeFile', file);

      const submitBtn = document.getElementById('prd-fin-btn-submit');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = 'جاري اعتماد المنتج النهائي وإيداعه بالمستودع...';
      }

      try {
        const res = await window.ChatApi.submitFinishedProduct(formData);
        if (res && res.success && res.data) {
          const p = res.data;
          this.notify(`تم تسجيل واعتماد المنتج النهائي برقم تصنيع: ${mfgNumber} بنجاح وإيداعه في المستودع! كمية الهالك: ${p.totalWasteQuantity} طن (${parseFloat(p.wastePercentage).toFixed(2)}%).`, 'success');
          this.resetFinishedForm();
          this.loadEligibleProcesses();
          this.loadOrdersForFinished();
          this.showSection('history');
          setTimeout(() => {
            this.loadTraceabilityHistory({ query: mfgNumber });
          }, 300);
        } else {
          this.notify(res?.message || 'فشل اعتماد المنتج النهائي', 'error');
        }
      } catch (err) {
        console.error('Error submitting finished product:', err);
        this.notify('حدث خطأ أثناء اعتماد المنتج النهائي: ' + (err.message || 'خطأ غير معروف'), 'error');
      } finally {
        this.isSubmittingFinished = false;
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = 'حفظ واعتماد المنتج النهائي';
        }
      }
    }
    /* ----------------------------------------------------------------------
       3. TRACEABILITY & HISTORY SECTION (سجل وعمليات التتبع)
       ---------------------------------------------------------------------- */
    static initTraceabilitySection() {
      const searchBtn = document.getElementById('prd-btn-search-traceability');
      searchBtn?.addEventListener('click', () => this.loadTraceabilityHistory());

      const resetBtn = document.getElementById('prd-btn-reset-traceability');
      resetBtn?.addEventListener('click', () => {
        const searchInput = document.getElementById('prd-trace-search-input');
        const dateFrom = document.getElementById('prd-trace-date-from');
        const dateTo = document.getElementById('prd-trace-date-to');
        const statusFilter = document.getElementById('prd-trace-status-filter');
        if (searchInput) searchInput.value = '';
        if (dateFrom) dateFrom.value = '';
        if (dateTo) dateTo.value = '';
        if (statusFilter) statusFilter.value = '';
        this.loadTraceabilityHistory();
      });

      const searchInput = document.getElementById('prd-trace-search-input');
      searchInput?.addEventListener('keyup', (e) => {
        if (e.key === 'Enter') {
          this.loadTraceabilityHistory();
        }
      });
    }

    static async loadTraceabilityHistory(overrideParams = {}) {
      const tbody = document.getElementById('prd-trace-table-tbody');
      const countEl = document.getElementById('prd-trace-records-count');
      if (tbody) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: var(--prd-text-muted);">جاري تحميل سجل عمليات الإنتاج والتتبع...</td></tr>';
      }

      const search = overrideParams.query ?? document.getElementById('prd-trace-search-input')?.value?.trim();
      const dateFrom = overrideParams.dateFrom ?? document.getElementById('prd-trace-date-from')?.value;
      const dateTo = overrideParams.dateTo ?? document.getElementById('prd-trace-date-to')?.value;
      const status = overrideParams.status ?? document.getElementById('prd-trace-status-filter')?.value;

      const params = {};
      if (search) params.query = search;

      try {
        const res = await window.ChatApi.searchProductionTraceability(params);
        if (res && res.success && Array.isArray(res.data)) {
          let list = res.data;

          if (dateFrom) {
            list = list.filter(x => (x.productionDate || '').slice(0, 10) >= dateFrom);
          }
          if (dateTo) {
            list = list.filter(x => (x.productionDate || '').slice(0, 10) <= dateTo);
          }
          if (status) {
            list = list.filter(x => x.status === status);
          }

          if (countEl) countEl.textContent = list.length;

          if (list.length === 0) {
            if (tbody) {
              tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2.5rem; color: var(--prd-text-muted);">لا توجد عمليات إنتاج تطابق معايير البحث الحالية</td></tr>';
            }
            return;
          }

          let html = '';
          list.forEach(item => {
            const p = item.process || item;
            const statusBadge = p.status === 'COMPLETED'
              ? '<span class="prd-badge" style="background: #dcfce7; color: #166534; font-weight: 700;">مكتملة معتمد</span>'
              : '<span class="prd-badge" style="background: #fef3c7; color: #92400e; font-weight: 700;">قيد المعالجة</span>';

            let matsHtml = '';
            (p.materials || []).forEach(m => {
              const refNum = m.productionReferenceNumber || m.laboratoryReferenceNumber || '—';
              matsHtml += `<div style="margin-bottom: 0.25rem;">
                <strong style="color: var(--prd-navy-dark);">${this.escape(m.materialName)}</strong>
                <span style="font-size: 0.75rem; color: #64748b;">(مرجع: <b>${this.escape(refNum)}</b> | تشغيلة: <b>${this.escape(m.batchNumber)}</b>)</span>
                <span style="font-size: 0.8rem; font-weight: 700; color: #0284c7; margin-right: 0.35rem;">${parseFloat(m.consumedQuantity).toFixed(3)} طن</span>
              </div>`;
            });

            let finsHtml = '';
            if (p.finishedProducts && p.finishedProducts.length > 0) {
              p.finishedProducts.forEach(f => {
                finsHtml += `<div style="margin-bottom: 0.25rem;">
                  <strong style="color: #10b981;">${this.escape(f.finishedProductName)}</strong>
                  <span style="font-size: 0.75rem; color: #64748b; display: block;">رقم التصنيع: <b style="color: var(--prd-navy-dark);">${this.escape(f.manufactureNumber)}</b></span>
                </div>`;
              });
            } else {
              finsHtml = '<span style="color: var(--prd-text-muted); font-size: 0.8rem;">بانتظار تسجيل المنتج النهائي</span>';
            }

            let attHtml = '';
            if (p.attachments && p.attachments.length > 0) {
              p.attachments.forEach(att => {
                const isRaw = att.attachmentType === 'IncomingWeighbridge';
                const label = isRaw ? 'بسكول مواد خام' : 'بسكول منتج تام';
                const bg = isRaw ? '#e0f2fe' : '#dcfce7';
                const color = isRaw ? '#0369a1' : '#166534';
                attHtml += `<a href="${att.filePath}" target="_blank" class="prd-badge" style="background: ${bg}; color: ${color}; text-decoration: none; display: inline-flex; align-items: center; gap: 0.25rem; margin: 0.15rem; font-size: 0.75rem;" title="${this.escape(att.originalFileName)}">
                  <span>📄</span> ${label}
                </a>`;
              });
            } else {
              attHtml = '<span style="color: var(--prd-text-muted); font-size: 0.8rem;">—</span>';
            }

            html += `
              <tr style="border-bottom: 1px solid #e2e8f0;">
                <td style="padding: 0.75rem 0.65rem;">
                  <span class="prd-badge" style="background: #f1f5f9; color: var(--prd-navy-dark); font-weight: 800; font-size: 0.88rem; padding: 0.35rem 0.65rem; border: 1px solid #cbd5e1;">
                    ${this.escape(p.processNumber)}
                  </span>
                  <div style="margin-top: 0.3rem;">${statusBadge}</div>
                </td>
                <td style="padding: 0.75rem 0.65rem; font-size: 0.85rem;">
                  <div>${this.escape(p.formattedDate || this.formatDateTime(p.productionDate))}</div>
                  <small style="color: var(--prd-text-muted);">${this.escape(p.productionManagerName || 'مسؤول الإنتاج')}</small>
                </td>
                <td style="padding: 0.75rem 0.65rem; font-size: 0.82rem;">
                  ${matsHtml || '—'}
                </td>
                <td style="padding: 0.75rem 0.65rem; font-weight: 800; color: #0284c7; font-size: 0.95rem;">
                  ${parseFloat(p.totalInputQuantity).toFixed(3)} طن
                </td>
                <td style="padding: 0.75rem 0.65rem; font-size: 0.82rem;">
                  ${finsHtml}
                </td>
                <td style="padding: 0.75rem 0.65rem; font-weight: 800; color: #10b981; font-size: 0.95rem;">
                  ${parseFloat(p.totalOutputQuantity).toFixed(3)} طن
                </td>
                <td style="padding: 0.75rem 0.65rem; font-size: 0.85rem;">
                  ${p.status === 'COMPLETED' ? `
                    <div style="font-weight: 800; color: #ef4444;">${parseFloat(p.totalWasteQuantity).toFixed(3)} طن</div>
                    <small style="color: #8b5cf6; font-weight: 700;">${parseFloat(p.wastePercentage).toFixed(2)}%</small>
                  ` : '<span style="color: var(--prd-text-muted);">—</span>'}
                </td>
                <td style="padding: 0.75rem 0.65rem;">
                  ${attHtml}
                </td>
                <td style="padding: 0.75rem 0.65rem; text-align: center;">
                  <button type="button" class="prd-btn prd-btn-outline" onclick="ProductionApp.showTraceabilityModal(${p.id})" style="padding: 0.4rem 0.75rem; font-size: 0.8rem; font-weight: 700;">
                    سلسلة التتبع الكاملة
                  </button>
                </td>
              </tr>
            `;
          });

          if (tbody) tbody.innerHTML = html;
        } else {
          if (tbody) {
            tbody.innerHTML = `<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">${this.escape(res?.message || 'فشل تحميل سجل التتبع')}</td></tr>`;
          }
        }
      } catch (err) {
        console.error('Error loading traceability history:', err);
        if (tbody) {
          tbody.innerHTML = '<tr><td colspan="9" style="text-align: center; padding: 2rem; color: #ef4444;">حدث خطأ أثناء تحميل سجل التتبع</td></tr>';
        }
      }
    }

    static async showTraceabilityModal(processId) {
      const contentEl = document.getElementById('prd-trace-modal-content');
      const titleEl = document.getElementById('prd-trace-modal-title');
      if (contentEl) {
        contentEl.innerHTML = '<div style="text-align: center; padding: 3rem; color: var(--prd-text-muted);">جاري تحميل سلسلة التتبع الكاملة...</div>';
      }
      this.openModal('prd-modal-traceability-details');

      try {
        const res = await window.ChatApi.getProductionProcessById(processId);
        if (res && res.success && res.data) {
          const p = res.data;
          if (titleEl) {
            titleEl.textContent = `سلسلة التتبع الكاملة لعملية الإنتاج: ${p.processNumber}`;
          }

          let matsRows = '';
          (p.materials || []).forEach(m => {
            const refNum = m.productionReferenceNumber || m.laboratoryReferenceNumber || '—';
            const locName = m.storageLocationName || m.warehouseLocation || '—';
            matsRows += `
              <tr style="border-bottom: 1px solid #e2e8f0;">
                <td style="padding: 0.5rem 0.75rem; font-weight: 700;">${this.escape(m.materialName)}</td>
                <td style="padding: 0.5rem 0.75rem;"><span class="prd-badge" style="background: #e0f2fe; color: #0369a1; font-weight: 700;">${this.escape(refNum)}</span></td>
                <td style="padding: 0.5rem 0.75rem; font-weight: 600;">${this.escape(m.batchNumber)}</td>
                <td style="padding: 0.5rem 0.75rem;">${this.escape(m.sampleCode || '—')}</td>
                <td style="padding: 0.5rem 0.75rem;">${this.escape(locName)}</td>
                <td style="padding: 0.5rem 0.75rem; font-weight: 800; color: #0284c7;">${parseFloat(m.consumedQuantity).toFixed(3)} طن</td>
              </tr>
            `;
          });

          let finRows = '';
          (p.finishedProducts || []).forEach(f => {
            finRows += `
              <tr style="border-bottom: 1px solid #e2e8f0;">
                <td style="padding: 0.5rem 0.75rem; font-weight: 700; color: #10b981;">${this.escape(f.finishedProductName)}</td>
                <td style="padding: 0.5rem 0.75rem;"><strong style="color: var(--prd-navy-dark);">${this.escape(f.manufactureNumber)}</strong></td>
                <td style="padding: 0.5rem 0.75rem; font-weight: 800; color: #10b981;">${parseFloat(f.manufacturedQuantity).toFixed(3)} ${this.escape(f.unit || 'طن')}</td>
                <td style="padding: 0.5rem 0.75rem;">${this.escape(this.formatDateTime(f.productionDate))}</td>
                <td style="padding: 0.5rem 0.75rem;">
                  ${f.weighbridgeAttachmentPath ? `<a href="${f.weighbridgeAttachmentPath}" target="_blank" class="prd-badge" style="background: #dcfce7; color: #166534; text-decoration: none;">📄 تذكرة وزن المنتج</a>` : '—'}
                </td>
              </tr>
            `;
          });

          let attRows = '';
          (p.attachments || []).forEach(att => {
            const isRaw = att.attachmentType === 'IncomingWeighbridge';
            attRows += `
              <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.75rem 1rem; display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
                <div>
                  <strong style="color: var(--prd-navy-dark); display: block;">${isRaw ? '⚖️ ميزان بسكول المواد الخام الواردة للعملية' : '⚖️ ميزان بسكول المنتج النهائي المصنع'}</strong>
                  <small style="color: var(--prd-text-muted);">${this.escape(att.originalFileName)} (${(att.fileSize / 1024).toFixed(1)} KB) - تم الرفع بواسطة: ${this.escape(att.uploadedBy)}</small>
                </div>
                <a href="${att.filePath}" target="_blank" class="prd-btn prd-btn-outline" style="padding: 0.35rem 0.85rem; font-size: 0.8rem; text-decoration: none;">
                  معاينة / تحميل 📥
                </a>
              </div>
            `;
          });

          contentEl.innerHTML = `
            <div style="display: flex; flex-direction: column; gap: 1.5rem;">
              <!-- Header Banner -->
              <div style="background: #f1f5f9; border-radius: 10px; padding: 1.25rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;">
                <div>
                  <span style="font-size: 0.82rem; color: var(--prd-text-muted); display: block;">معرف عملية الإنتاج الموحد</span>
                  <h2 style="margin: 0.2rem 0; font-size: 1.4rem; font-weight: 800; color: var(--prd-navy-dark);">${this.escape(p.processNumber)}</h2>
                  <span style="font-size: 0.85rem; color: #64748b;">تاريخ العملية: ${this.escape(p.formattedDate || this.formatDateTime(p.productionDate))} | مسؤول الإنتاج: <strong>${this.escape(p.productionManagerName || '—')}</strong></span>
                </div>
                <div>
                  <span class="prd-badge" style="background: ${p.status === 'COMPLETED' ? '#dcfce7' : '#fef3c7'}; color: ${p.status === 'COMPLETED' ? '#166534' : '#92400e'}; font-size: 0.95rem; font-weight: 800; padding: 0.4rem 0.9rem; border-radius: 8px;">
                    ${p.status === 'COMPLETED' ? 'مكتملة معتمدة (COMPLETED)' : 'قيد المعالجة (IN_PROGRESS)'}
                  </span>
                </div>
              </div>

              <!-- Stage 1: Warehouse & Raw Materials Consumed -->
              <div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1.25rem;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.85rem;">
                  <h4 style="margin: 0; font-size: 1rem; font-weight: 800; color: var(--prd-navy-dark); display: flex; align-items: center; gap: 0.5rem;">
                    <span style="color: #0284c7;">①</span> مصادر المستودع والمواد الأولية المستهلكة (Warehouse & Raw Materials)
                  </h4>
                  <span style="font-size: 0.85rem; color: #0284c7; font-weight: 800;">إجمالي الاستهلاك: ${parseFloat(p.totalInputQuantity).toFixed(3)} طن</span>
                </div>
                <div class="table-responsive" style="overflow-x: auto;">
                  <table class="prd-table" style="width: 100%; border-collapse: collapse; font-size: 0.82rem; text-align: right;">
                    <thead>
                      <tr style="background: #f8fafc; color: var(--prd-navy-dark); border-bottom: 2px solid #e2e8f0;">
                        <th style="padding: 0.5rem 0.75rem;">اسم الخامة</th>
                        <th style="padding: 0.5rem 0.75rem;">الرقم المرجعي (المعمل)</th>
                        <th style="padding: 0.5rem 0.75rem;">رقم التشغيلة</th>
                        <th style="padding: 0.5rem 0.75rem;">كود العينة</th>
                        <th style="padding: 0.5rem 0.75rem;">المستودع المصدر</th>
                        <th style="padding: 0.5rem 0.75rem;">الكمية المستهلكة</th>
                      </tr>
                    </thead>
                    <tbody>
                      ${matsRows || '<tr><td colspan="6" style="text-align: center; color: var(--prd-text-muted);">لا توجد مواد مسجلة</td></tr>'}
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- Stage 2: Finished Products Manufactured -->
              <div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1.25rem;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.85rem;">
                  <h4 style="margin: 0; font-size: 1rem; font-weight: 800; color: var(--prd-navy-dark); display: flex; align-items: center; gap: 0.5rem;">
                    <span style="color: #10b981;">②</span> المنتجات النهائية المصنعة وأرقام التصنيع (Finished Products & MFG)
                  </h4>
                  <span style="font-size: 0.85rem; color: #10b981; font-weight: 800;">إجمالي الناتج: ${parseFloat(p.totalOutputQuantity).toFixed(3)} طن</span>
                </div>
                <div class="table-responsive" style="overflow-x: auto;">
                  <table class="prd-table" style="width: 100%; border-collapse: collapse; font-size: 0.82rem; text-align: right;">
                    <thead>
                      <tr style="background: #f8fafc; color: var(--prd-navy-dark); border-bottom: 2px solid #e2e8f0;">
                        <th style="padding: 0.5rem 0.75rem;">اسم المنتج النهائي</th>
                        <th style="padding: 0.5rem 0.75rem;">رقم التصنيع المستقل (MFG #)</th>
                        <th style="padding: 0.5rem 0.75rem;">الكمية المصنعة</th>
                        <th style="padding: 0.5rem 0.75rem;">تاريخ الإنتاج</th>
                        <th style="padding: 0.5rem 0.75rem;">تذكرة البسكول</th>
                      </tr>
                    </thead>
                    <tbody>
                      ${finRows || '<tr><td colspan="5" style="text-align: center; color: var(--prd-text-muted);">بانتظار تسجيل المنتج النهائي</td></tr>'}
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- Stage 3: Mass Balance & Waste Audit -->
              <div style="background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 10px; padding: 1.25rem;">
                <h4 style="margin: 0 0 1rem; font-size: 1rem; font-weight: 800; color: var(--prd-navy-dark); display: flex; align-items: center; gap: 0.5rem;">
                  <span style="color: #8b5cf6;">③</span> تدقيق ميزان الكتلة وحساب الهالك (Mass Balance & Waste Calculation)
                </h4>
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem;">
                  <div style="background: #ffffff; padding: 0.85rem 1rem; border-radius: 8px; border-top: 3px solid #0284c7;">
                    <span style="font-size: 0.78rem; color: var(--prd-text-muted); display: block;">إجمالي المدخلات (Raw Input)</span>
                    <strong style="font-size: 1.25rem; color: #0284c7;">${parseFloat(p.totalInputQuantity).toFixed(3)} طن</strong>
                  </div>
                  <div style="background: #ffffff; padding: 0.85rem 1rem; border-radius: 8px; border-top: 3px solid #10b981;">
                    <span style="font-size: 0.78rem; color: var(--prd-text-muted); display: block;">إجمالي المخرجات (Finished Output)</span>
                    <strong style="font-size: 1.25rem; color: #10b981;">${parseFloat(p.totalOutputQuantity).toFixed(3)} طن</strong>
                  </div>
                  <div style="background: #ffffff; padding: 0.85rem 1rem; border-radius: 8px; border-top: 3px solid #ef4444;">
                    <span style="font-size: 0.78rem; color: var(--prd-text-muted); display: block;">كمية الهالك (Waste Quantity)</span>
                    <strong style="font-size: 1.25rem; color: #ef4444;">${parseFloat(p.totalWasteQuantity).toFixed(3)} طن</strong>
                  </div>
                  <div style="background: #ffffff; padding: 0.85rem 1rem; border-radius: 8px; border-top: 3px solid #8b5cf6;">
                    <span style="font-size: 0.78rem; color: var(--prd-text-muted); display: block;">نسبة الهالك (Waste Percentage)</span>
                    <strong style="font-size: 1.25rem; color: #8b5cf6;">${parseFloat(p.wastePercentage).toFixed(2)}%</strong>
                  </div>
                </div>
              </div>

              <!-- Stage 4: Weighbridge Documents -->
              <div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1.25rem;">
                <h4 style="margin: 0 0 0.85rem; font-size: 1rem; font-weight: 800; color: var(--prd-navy-dark); display: flex; align-items: center; gap: 0.5rem;">
                  <span style="color: #3b82f6;">④</span> مستندات ميزان البسكول المحفوظة (Weighbridge Documents)
                </h4>
                ${attRows || '<p style="color: var(--prd-text-muted); font-size: 0.85rem; margin: 0;">لا توجد مرفقات مسجلة لهذه العملية</p>'}
              </div>

              <!-- Stage 5: Transportation Integration Information -->
              <div style="background: #f0fdf4; border: 1px solid #86efac; border-radius: 10px; padding: 1rem 1.25rem;">
                <div style="display: flex; gap: 0.75rem; align-items: flex-start;">
                  <span style="font-size: 1.25rem;">🚚</span>
                  <div>
                    <strong style="color: #166534; font-size: 0.92rem; display: block;">جاهزية الشحن والنقل (Transportation Department Integration)</strong>
                    <p style="margin: 0.25rem 0 0; font-size: 0.82rem; color: #15803d; line-height: 1.5;">
                      تم ربط المنتجات النهائية المصنعة برقم عملية الإنتاج <strong>${this.escape(p.processNumber)}</strong> وأرقام التصنيع الفريدة. قسم النقل يستطيع التحقق والتحميل مباشرة بناءً على هذه الأرقام المرجعية الثابتة دون الحاجة لأي تعديل على نماذج أو عمليات النقل.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          `;
        } else {
          if (contentEl) {
            contentEl.innerHTML = `<div style="text-align: center; padding: 3rem; color: #ef4444;">${this.escape(res?.message || 'فشل تحميل بيانات العملية')}</div>`;
          }
        }
      } catch (err) {
        console.error('Error fetching process for modal:', err);
        if (contentEl) {
          contentEl.innerHTML = '<div style="text-align: center; padding: 3rem; color: #ef4444;">حدث خطأ أثناء تحميل تفاصيل العملية</div>';
        }
      }
    }

  }

  // Expose to window for app navigation and cross-module calls
  window.ProductionApp = ProductionApp;

  // Auto-init on DOMContentLoaded if element exists
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ProductionApp.init());
  } else {
    ProductionApp.init();
  }
})();
