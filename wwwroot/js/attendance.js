/**
 * FaceStone Web - Attendance Department Application
 * Full Database-Driven Arabic RTL Attendance Sheet with Individual & Batch Operations,
 * Automatic Working Hours Calculation, Optional Evidence Uploads, and Realtime Statistics.
 */
(function () {
  'use strict';

  class AttendanceApp {
    constructor() {
      this.initialized = false;
      this.selectedDate = this.getTodayDateString();
      this.employees = [];
      this.recordsState = new Map(); // userId => { recordId, status, checkInTime, checkOutTime, workingHours, notes, approvalStatus, rejectionReason, reviewedBy, reviewedAt }
      this.selectedFiles = []; // Array of File objects
      this.existingAttachments = []; // Array of AttendanceAttachmentDto
      this.currentSubmissionId = null;
      this.currentStatus = 'DRAFT'; // 'DRAFT' | 'PENDING' | 'APPROVED' | 'REJECTED'
      this.rejectionReason = '';
      this.reviewedBy = '';
      this.reviewedAt = null;
      this.isSubmitting = false;
      this.isLoading = false;
      this.isEditMode = false;
      this.searchTerm = '';
    }

    getTodayDateString() {
      const today = new Date();
      const y = today.getFullYear();
      const m = String(today.getMonth() + 1).padStart(2, '0');
      const d = String(today.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    getNowDateTimeLocalString() {
      const now = new Date();
      const y = now.getFullYear();
      const m = String(now.getMonth() + 1).padStart(2, '0');
      const d = String(now.getDate()).padStart(2, '0');
      const h = String(now.getHours()).padStart(2, '0');
      const min = String(now.getMinutes()).padStart(2, '0');
      return `${y}-${m}-${d}T${h}:${min}`;
    }

    toLocalInputValue(dateStr) {
      if (!dateStr) return '';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '';
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      const h = String(d.getHours()).padStart(2, '0');
      const min = String(d.getMinutes()).padStart(2, '0');
      return `${y}-${m}-${day}T${h}:${min}`;
    }

    formatDisplayDateTime(dateStr) {
      if (!dateStr) return '-';
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '-';
      const timePart = d.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' });
      return `${timePart}`;
    }

    calcWorkingHours(checkIn, checkOut) {
      if (!checkIn || !checkOut) return null;
      const d1 = new Date(checkIn);
      const d2 = new Date(checkOut);
      if (isNaN(d1.getTime()) || isNaN(d2.getTime())) return null;
      if (d2 <= d1) return 0;
      const diffHrs = (d2.getTime() - d1.getTime()) / (1000 * 60 * 60);
      return Math.round(diffHrs * 100) / 100;
    }

    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindElements();
      this.bindEvents();
      this.setupDropzone();
      this.setupRealtime();

      if (this.dateInput) {
        this.dateInput.value = this.selectedDate;
      }
      if (this.datePicker) {
        this.datePicker.value = this.selectedDate;
      }
      if (this.dateDisplay) {
        this.dateDisplay.value = this.selectedDate;
      }

      this.loadAttendanceData(this.selectedDate);
    }

    bindElements() {
      this.root = document.getElementById('attendance-page-root');
      this.userAvatar = document.getElementById('attendance-user-avatar');
      this.userName = document.getElementById('attendance-user-name');
      this.userRole = document.getElementById('attendance-user-role');
      this.backBtn = document.getElementById('attendance-back-to-departments-btn');

      this.dateInput = document.getElementById('attendance-date-input');
      this.btnToday = document.getElementById('btn-attendance-today');
      this.dateDisplay = document.getElementById('attendance-date-display');
      this.datePicker = document.getElementById('attendance-date-picker');

      this.preparedByName = document.getElementById('attendance-prepared-by-name');
      this.preparedByRole = document.getElementById('attendance-prepared-by-role');
      this.statusSummaryText = document.getElementById('attendance-status-summary-text');

      this.statusBadge = document.getElementById('attendance-page-status-badge');
      this.statusBadgeText = document.getElementById('attendance-status-badge-text');

      this.rejectionBanner = document.getElementById('attendance-rejection-banner');
      this.rejectReviewer = document.getElementById('attendance-reject-reviewer');
      this.rejectDate = document.getElementById('attendance-reject-date');
      this.rejectReasonText = document.getElementById('attendance-reject-reason-text');
      this.btnReopenEdit = document.getElementById('btn-attendance-reopen-edit');

      this.searchInput = document.getElementById('attendance-search-employee');
      this.btnRefresh = document.getElementById('attendance-btn-refresh');

      this.statTotal = document.getElementById('attendance-stat-total');
      this.statPresent = document.getElementById('attendance-stat-present');
      this.statAbsent = document.getElementById('attendance-stat-absent');
      this.statUnmarked = document.getElementById('attendance-stat-unmarked');
      this.statHours = document.getElementById('attendance-stat-hours');
      this.statRate = document.getElementById('attendance-stat-rate');

      this.tableBody = document.getElementById('attendance-table-body');
      this.tableLoading = document.getElementById('attendance-table-loading');
      this.tableEmpty = document.getElementById('attendance-table-empty');

      this.dropzone = document.getElementById('attendance-dropzone');
      this.fileInput = document.getElementById('attendance-file-input');
      this.filesList = document.getElementById('attendance-files-list');
      this.evidenceCountPill = document.getElementById('attendance-evidence-count-pill');

      this.btnReset = document.getElementById('btn-attendance-reset');
      this.btnSubmit = document.getElementById('btn-attendance-submit');
      this.feedbackBox = document.getElementById('attendance-submit-feedback');
    }

    bindEvents() {
      if (this.backBtn) {
        this.backBtn.addEventListener('click', () => {
          if (window.ChatApp) {
            window.ChatApp.backToDepartments();
          }
        });
      }

      if (this.dateInput) {
        this.dateInput.addEventListener('change', () => {
          const val = this.dateInput.value;
          if (val) {
            this.selectedDate = val;
            if (this.datePicker) this.datePicker.value = val;
            if (this.dateDisplay) this.dateDisplay.value = val;
            this.loadAttendanceData(this.selectedDate);
          }
        });
      }

      if (this.btnToday) {
        this.btnToday.addEventListener('click', () => {
          this.selectedDate = this.getTodayDateString();
          if (this.dateInput) this.dateInput.value = this.selectedDate;
          if (this.datePicker) this.datePicker.value = this.selectedDate;
          if (this.dateDisplay) this.dateDisplay.value = this.selectedDate;
          this.loadAttendanceData(this.selectedDate);
        });
      }

      if (this.searchInput) {
        this.searchInput.addEventListener('input', (e) => {
          this.searchTerm = (e.target.value || '').trim().toLowerCase();
          this.renderTable();
        });
      }

      if (this.btnRefresh) {
        this.btnRefresh.addEventListener('click', () => {
          this.loadAttendanceData(this.selectedDate);
        });
      }

      if (this.btnReset) {
        this.btnReset.addEventListener('click', () => {
          this.selectedFiles = [];
          this.loadAttendanceData(this.selectedDate);
        });
      }

      if (this.btnSubmit) {
        this.btnSubmit.addEventListener('click', () => {
          this.handleSubmit();
        });
      }

      if (this.btnReopenEdit) {
        this.btnReopenEdit.addEventListener('click', () => {
          this.reopenForEdit();
        });
      }
    }

    setupDropzone() {
      if (!this.dropzone || !this.fileInput) return;

      this.dropzone.addEventListener('click', (e) => {
        if (e.target.closest('button')) return;
        this.fileInput.click();
      });

      this.fileInput.addEventListener('change', (e) => {
        if (e.target.files && e.target.files.length > 0) {
          this.handleFilesAdded(Array.from(e.target.files));
          this.fileInput.value = '';
        }
      });

      ['dragenter', 'dragover'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.style.borderColor = '#0284C7';
          this.dropzone.style.background = '#F0F9FF';
        });
      });

      ['dragleave', 'drop'].forEach(eventName => {
        this.dropzone.addEventListener(eventName, (e) => {
          e.preventDefault();
          e.stopPropagation();
          this.dropzone.style.borderColor = '#94A3B8';
          this.dropzone.style.background = '#F8FAFC';
        });
      });

      this.dropzone.addEventListener('drop', (e) => {
        if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
          this.handleFilesAdded(Array.from(e.dataTransfer.files));
        }
      });
    }

    handleFilesAdded(files) {
      const allowedExts = ['.pdf', '.jpg', '.jpeg', '.png'];
      const maxSizeBytes = 20 * 1024 * 1024; // 20 MB

      if (!files || files.length === 0) return;
      const file = files[0];

      const ext = '.' + file.name.split('.').pop().toLowerCase();
      if (!allowedExts.includes(ext)) {
        this.showFeedback(`الملف "${file.name}" غير مدعوم. الصيغ المدعومة هي: PDF, JPG, PNG.`, 'error');
        return;
      }

      if (file.size > maxSizeBytes) {
        this.showFeedback(`حجم الملف "${file.name}" يتجاوز الحد الأقصى (20 ميجابايت).`, 'error');
        return;
      }

      this.selectedFiles = [file];
      this.renderSelectedFiles();
    }

    renderSelectedFiles() {
      if (!this.filesList) return;
      this.filesList.innerHTML = '';

      const totalCount = this.selectedFiles.length + this.existingAttachments.length;
      if (this.evidenceCountPill) {
        this.evidenceCountPill.textContent = totalCount > 0 ? `${totalCount} مستند مختار` : '0 ملف مختار';
        if (totalCount > 0) {
          this.evidenceCountPill.style.background = '#ECFDF5';
          this.evidenceCountPill.style.color = '#065F46';
          this.evidenceCountPill.style.border = '1px solid #A7F3D0';
        } else {
          this.evidenceCountPill.style.background = '#F1F5F9';
          this.evidenceCountPill.style.color = '#0F172A';
          this.evidenceCountPill.style.border = 'none';
        }
      }

      // Existing attachments
      this.existingAttachments.forEach((att) => {
        const card = document.createElement('div');
        card.className = 'cm-attachment-card';
        card.style.cssText = 'background:#FFFFFF; border:1px solid #E2E8F0; border-radius:8px; padding:10px 14px; display:flex; align-items:center; justify-content:space-between; gap:8px; margin-bottom:6px;';
        const isPdf = (att.fileType || '').toLowerCase().includes('pdf') || att.fileName.toLowerCase().endsWith('.pdf');
        const icon = isPdf ? '📄' : '🖼️';
        const sizeStr = this.formatFileSize(att.fileSize);

        card.innerHTML = `
          <div style="display:flex; align-items:center; gap:8px; overflow:hidden;">
            <span style="font-size:1.4rem;">${icon}</span>
            <div style="overflow:hidden;">
              <div style="font-weight:700; font-size:0.85rem; color:#0F172A; text-overflow:ellipsis; overflow:hidden; white-space:nowrap;">
                ${this.escapeHtml(att.originalFileName || att.fileName)}
              </div>
              <div style="font-size:0.75rem; color:#64748B;">
                <span>${sizeStr}</span> • <span style="color:#059669; font-weight:600;">سابق</span>
              </div>
            </div>
          </div>
          <button type="button" class="btn-att-preview" style="background:#F1F5F9; border:1px solid #CBD5E1; border-radius:6px; padding:4px 8px; font-size:0.75rem; cursor:pointer;">
            👁️ معاينة
          </button>
        `;

        const btnPrev = card.querySelector('.btn-att-preview');
        if (btnPrev) {
          btnPrev.addEventListener('click', (e) => {
            e.stopPropagation();
            this.previewAttachment(att);
          });
        }
        this.filesList.appendChild(card);
      });

      // Newly selected files
      this.selectedFiles.forEach((file, idx) => {
        const card = document.createElement('div');
        card.className = 'cm-attachment-card';
        card.style.cssText = 'background:#FFFFFF; border:1px solid #0284C7; border-radius:8px; padding:10px 14px; display:flex; align-items:center; justify-content:space-between; gap:8px; margin-bottom:6px;';
        const isPdf = file.name.toLowerCase().endsWith('.pdf');
        const icon = isPdf ? '📄' : '🖼️';
        const sizeStr = this.formatFileSize(file.size);

        card.innerHTML = `
          <div style="display:flex; align-items:center; gap:8px; overflow:hidden;">
            <span style="font-size:1.4rem;">${icon}</span>
            <div style="overflow:hidden;">
              <div style="font-weight:700; font-size:0.85rem; color:#0F172A; text-overflow:ellipsis; overflow:hidden; white-space:nowrap;">
                ${this.escapeHtml(file.name)}
              </div>
              <div style="font-size:0.75rem; color:#64748B;">
                <span>${sizeStr}</span> • <span style="color:#0284C7; font-weight:600;">جديد</span>
              </div>
            </div>
          </div>
          <button type="button" class="btn-file-remove" style="background:#FEE2E2; border:1px solid #FECACA; color:#DC2626; border-radius:6px; padding:4px 8px; font-size:0.75rem; cursor:pointer;">
            ✕ إزالة
          </button>
        `;

        const btnRemove = card.querySelector('.btn-file-remove');
        if (btnRemove) {
          btnRemove.addEventListener('click', (e) => {
            e.stopPropagation();
            this.selectedFiles.splice(idx, 1);
            this.renderSelectedFiles();
          });
        }
        this.filesList.appendChild(card);
      });
    }

    setupRealtime() {
      window.addEventListener('facestone:realtime:attendance', (e) => {
        const detail = e.detail;
        if (detail && detail.formId && detail.formId === this.currentSubmissionId) {
          this.loadAttendanceData(this.selectedDate);
        }
      });

      window.addEventListener('facestone:realtime:control-monitoring', (e) => {
        const detail = e.detail;
        if (detail && (detail.department === 'attendance' || detail.formType === 'attendance')) {
          this.loadAttendanceData(this.selectedDate);
        }
      });
    }

    updateUserBadge(user) {
      if (!user) return;
      const displayName = user.fullName || user.username || 'المستخدم';
      const roleText = window.ChatApp ? window.ChatApp.formatRoleArabic(user.role) : (user.role || 'موظف');

      if (this.userName) this.userName.textContent = displayName;
      if (this.userRole) this.userRole.textContent = roleText;
      if (this.preparedByName) this.preparedByName.textContent = displayName;
      if (this.preparedByRole) this.preparedByRole.textContent = roleText;
      if (this.userAvatar) {
        const parts = displayName.split(' ').filter(Boolean);
        let initials = 'FS';
        if (parts.length >= 2) initials = (parts[0][0] + parts[1][0]).toUpperCase();
        else if (parts.length === 1 && parts[0].length >= 2) initials = parts[0].substring(0, 2).toUpperCase();
        this.userAvatar.textContent = initials;
      }
    }

    async loadAttendanceData(dateStr) {
      this.isLoading = true;
      if (this.tableLoading) this.tableLoading.style.display = 'block';
      if (this.tableEmpty) this.tableEmpty.style.display = 'none';
      if (this.tableBody) this.tableBody.innerHTML = '';
      if (this.feedbackBox) this.feedbackBox.style.display = 'none';

      try {
        const res = await window.ChatApi.getAttendanceSheet(dateStr);
        if (this.tableLoading) this.tableLoading.style.display = 'none';

        if (res && res.success && res.data) {
          const d = res.data;
          this.employees = d.employees || [];
          this.currentSubmissionId = d.submissionId || null;
          this.currentStatus = (d.approvalStatus || 'DRAFT').toUpperCase();

          const sub = d.submission;
          if (sub) {
            this.rejectionReason = sub.rejectionReason || '';
            this.reviewedBy = sub.reviewedBy || '';
            this.reviewedAt = sub.reviewedAt || null;
            this.existingAttachments = sub.attachments || [];
          } else {
            this.rejectionReason = '';
            this.reviewedBy = '';
            this.reviewedAt = null;
            this.existingAttachments = [];
          }

          // Populate recordsState
          this.recordsState.clear();
          this.employees.forEach(emp => {
            const status = (emp.status && emp.status.trim()) ? emp.status : null;
            const subRec = sub && sub.records ? sub.records.find(r => r.userId === emp.userId || r.fullName === emp.fullName) : null;
            const appStatus = (subRec && subRec.approvalStatus) ? subRec.approvalStatus : (emp.approvalStatus || 'PENDING');
            const rejReason = (subRec && subRec.rejectionReason) ? subRec.rejectionReason : (emp.rejectionReason || '');
            const revBy = (subRec && subRec.reviewedBy) ? subRec.reviewedBy : (emp.reviewedBy || '');
            const revAt = (subRec && subRec.reviewedAt) ? subRec.reviewedAt : (emp.reviewedAt || null);

            const checkIn = (subRec && (subRec.checkInTime || subRec.attendanceDateTime))
              ? (subRec.checkInTime || subRec.attendanceDateTime)
              : (emp.checkInTime || emp.attendanceDateTime || null);

            const checkOut = (subRec && subRec.checkOutTime) ? subRec.checkOutTime : (emp.checkOutTime || null);

            let hours = (subRec && subRec.workingHours != null) ? subRec.workingHours : (emp.workingHours != null ? emp.workingHours : null);
            if (hours == null && checkIn && checkOut) {
              hours = this.calcWorkingHours(checkIn, checkOut);
            }

            this.recordsState.set(emp.userId, {
              recordId: subRec ? subRec.id : (emp.recordId || null),
              status: status,
              checkInTime: checkIn,
              checkOutTime: checkOut,
              workingHours: hours,
              notes: (subRec ? subRec.notes : emp.notes) || '',
              approvalStatus: (appStatus || 'PENDING').toUpperCase(),
              rejectionReason: rejReason,
              reviewedBy: revBy,
              reviewedAt: revAt
            });
          });

          this.renderStatusBadge();
          this.renderRejectionBanner();
          this.renderSelectedFiles();
          this.renderTable();

          if (d.stats) {
            this.applyStats(d.stats);
          } else {
            this.updateStatsCounters();
          }
        } else {
          this.showFeedback(res?.message || 'تعذر تحميل بيانات كشف الحضور.', 'error');
        }
      } catch (err) {
        if (this.tableLoading) this.tableLoading.style.display = 'none';
        console.error('Error loading attendance sheet:', err);
        this.showFeedback('حدث خطأ أثناء تحميل سجلات كشف الحضور.', 'error');
      } finally {
        this.isLoading = false;
      }
    }

    applyStats(stats) {
      if (!stats) return;
      if (this.statTotal) this.statTotal.textContent = stats.totalEmployees ?? this.employees.length;
      if (this.statPresent) this.statPresent.textContent = stats.presentCount ?? 0;
      if (this.statAbsent) this.statAbsent.textContent = stats.absentCount ?? 0;
      if (this.statUnmarked) this.statUnmarked.textContent = stats.unmarkedCount ?? 0;
      if (this.statHours) this.statHours.textContent = Number(stats.totalWorkingHours ?? 0).toFixed(1);
      if (this.statRate) this.statRate.textContent = `${stats.attendanceRate ?? 0}%`;
    }

    reopenForEdit() {
      this.isEditMode = true;
      if (this.btnSubmit) {
        this.btnSubmit.disabled = false;
        this.btnSubmit.style.opacity = '1';
        this.btnSubmit.innerHTML = `
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
          <span>تعديل وإعادة تقديم / Edit & Resubmit</span>
        `;
      }
      this.renderTable();
    }

    renderStatusBadge() {
      if (!this.statusBadge || !this.statusBadgeText) return;

      const status = (this.currentStatus || 'DRAFT').toUpperCase();
      this.statusBadge.className = 'finance-badge-pill';

      if (status === 'APPROVED') {
        this.statusBadge.style.background = '#ECFDF5';
        this.statusBadge.style.color = '#065F46';
        this.statusBadge.style.border = '1px solid #A7F3D0';
        this.statusBadgeText.textContent = '✓ معتمد رسمياً (APPROVED)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '✓ معتمد رسمياً (APPROVED)';
          this.statusSummaryText.style.color = '#065F46';
        }
      } else if (status === 'REJECTED') {
        this.statusBadge.style.background = '#FEF2F2';
        this.statusBadge.style.color = '#991B1B';
        this.statusBadge.style.border = '1px solid #FECACA';
        this.statusBadgeText.textContent = '✕ مرفوض للتعديل (REJECTED)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '✕ مرفوض للتعديل (REJECTED)';
          this.statusSummaryText.style.color = '#991B1B';
        }
      } else if (status === 'PENDING') {
        this.statusBadge.style.background = '#FFFBEB';
        this.statusBadge.style.color = '#92400E';
        this.statusBadge.style.border = '1px solid #FDE68A';
        this.statusBadgeText.textContent = '⏳ قيد المراجعة والتدقيق (PENDING REVIEW)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = '⏳ قيد المراجعة والتدقيق (PENDING REVIEW)';
          this.statusSummaryText.style.color = '#92400E';
        }
      } else {
        this.statusBadge.style.background = '#F1F5F9';
        this.statusBadge.style.color = '#0F172A';
        this.statusBadge.style.border = '1px solid #E2E8F0';
        this.statusBadgeText.textContent = 'مسودة جديدة (DRAFT)';
        if (this.statusSummaryText) {
          this.statusSummaryText.textContent = 'مسودة جديدة (DRAFT)';
          this.statusSummaryText.style.color = '#0F172A';
        }
      }

      if (this.btnSubmit) {
        if (status === 'APPROVED' && !this.isEditMode) {
          this.btnSubmit.disabled = false;
          this.btnSubmit.style.opacity = '1';
          this.btnSubmit.style.cursor = 'pointer';
          this.btnSubmit.innerHTML = `
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>تحديث كشف الحضور / Update Attendance</span>
          `;
        } else if (status === 'REJECTED' || this.isEditMode) {
          this.btnSubmit.disabled = false;
          this.btnSubmit.style.opacity = '1';
          this.btnSubmit.style.cursor = 'pointer';
          this.btnSubmit.innerHTML = `
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>تعديل وإعادة تقديم / Edit & Resubmit</span>
          `;
        } else {
          this.btnSubmit.disabled = false;
          this.btnSubmit.style.opacity = '1';
          this.btnSubmit.style.cursor = 'pointer';
          this.btnSubmit.innerHTML = `
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
            <span>Submit Attendance for Review / إرسال الحضور للمراجعة</span>
          `;
        }
      }
    }

    renderRejectionBanner() {
      if (!this.rejectionBanner) return;

      if (this.currentStatus === 'REJECTED') {
        this.rejectionBanner.style.display = 'block';
        if (this.rejectReviewer) this.rejectReviewer.textContent = this.reviewedBy || 'مراجع العمليات';
        if (this.rejectDate) this.rejectDate.textContent = this.reviewedAt ? new Date(this.reviewedAt).toLocaleString('ar-EG') : '-';
        if (this.rejectReasonText) this.rejectReasonText.textContent = this.rejectionReason || 'توجد ملاحظات يلزم تعديلها وإعادة التقديم.';
      } else {
        this.rejectionBanner.style.display = 'none';
      }
    }

    renderTable() {
      if (!this.tableBody) return;
      this.tableBody.innerHTML = '';

      const filtered = this.employees.filter(emp => {
        if (!this.searchTerm) return true;
        const name = (emp.fullName || '').toLowerCase();
        const role = (emp.role || '').toLowerCase();
        return name.includes(this.searchTerm) || role.includes(this.searchTerm);
      });

      if (filtered.length === 0) {
        if (this.tableEmpty) this.tableEmpty.style.display = 'block';
        return;
      } else {
        if (this.tableEmpty) this.tableEmpty.style.display = 'none';
      }

      filtered.forEach((emp) => {
        const state = this.recordsState.get(emp.userId) || {
          recordId: null,
          status: null,
          checkInTime: null,
          checkOutTime: null,
          workingHours: null,
          notes: '',
          approvalStatus: 'APPROVED'
        };

        const isPresent = state.status && state.status.toLowerCase() === 'present';
        const isAbsent = state.status && state.status.toLowerCase() === 'absent';

        const tr = document.createElement('tr');
        tr.className = 'finance-table-row';
        tr.setAttribute('data-user-id', emp.userId);
        tr.style.transition = 'background 0.15s ease';

        // 1. Employee Name (Column 1)
        const nameHtml = `
          <div style="display:flex; align-items:center; gap:8px;">
            <div style="width:34px; height:34px; border-radius:50%; background:#0F172A; color:#FFFFFF; display:flex; align-items:center; justify-content:center; font-weight:800; font-size:0.8rem;">
              ${this.getInitials(emp.fullName || emp.username)}
            </div>
            <div>
              <div style="font-weight:700; color:#0F172A; font-size:0.92rem;">${this.escapeHtml(emp.fullName || emp.username)}</div>
              <div style="font-size:0.75rem; color:#64748B;">ID: ${emp.userId.substring(0, 8)} • @${this.escapeHtml(emp.username)}</div>
            </div>
          </div>
        `;

        // 2. System Role (Column 2)
        const roleArabic = window.ChatApp ? window.ChatApp.formatRoleArabic(emp.role) : (emp.role || 'موظف');
        const roleHtml = `
          <div>
            <span class="finance-badge-pill" style="font-size:0.75rem; font-weight:700; background:#F8FAFC; color:#334155; border:1px solid #E2E8F0;">
              ${this.escapeHtml(roleArabic)}
            </span>
          </div>
        `;

        // 3. Present Checkbox (Column 3)
        const presentHtml = `
          <div style="display:flex; justify-content:center; align-items:center;">
            <input type="checkbox" class="att-chk-present" data-user-id="${emp.userId}"
              ${isPresent ? 'checked' : ''}
              style="width:20px; height:20px; cursor:pointer; accent-color:#059669;" />
          </div>
        `;

        // 4. Absent Checkbox (Column 4)
        const absentHtml = `
          <div style="display:flex; justify-content:center; align-items:center;">
            <input type="checkbox" class="att-chk-absent" data-user-id="${emp.userId}"
              ${isAbsent ? 'checked' : ''}
              style="width:20px; height:20px; cursor:pointer; accent-color:#DC2626;" />
          </div>
        `;

        // 5. Check-In Time Input (Column 5)
        const inLocal = this.toLocalInputValue(state.checkInTime);
        const checkInHtml = `
          <div style="display:flex; align-items:center; gap:4px;">
            <input type="datetime-local" class="att-input-checkin finance-filter-input" data-user-id="${emp.userId}"
              value="${inLocal}"
              style="padding:5px 6px; font-size:0.8rem; font-weight:700; width:100%; border-radius:6px; background:#FFFFFF; border:1px solid #CBD5E1; color:#0F172A;" />
            <button type="button" class="btn-att-in-now" data-user-id="${emp.userId}"
              title="تعيين وقت الحضور الآن"
              style="padding:4px 7px; font-size:0.72rem; font-weight:700; background:#E0F2FE; color:#0369A1; border:1px solid #BAE6FD; border-radius:6px; cursor:pointer; white-space:nowrap;">
              الآن
            </button>
          </div>
        `;

        // 6. Check-Out Time Input (Column 6)
        const outLocal = this.toLocalInputValue(state.checkOutTime);
        const checkOutHtml = `
          <div style="display:flex; align-items:center; gap:4px;">
            <input type="datetime-local" class="att-input-checkout finance-filter-input" data-user-id="${emp.userId}"
              value="${outLocal}"
              style="padding:5px 6px; font-size:0.8rem; font-weight:700; width:100%; border-radius:6px; background:#FFFFFF; border:1px solid #CBD5E1; color:#0F172A;" />
            <button type="button" class="btn-att-out-now" data-user-id="${emp.userId}"
              title="تعيين وقت الانصراف الآن"
              style="padding:4px 7px; font-size:0.72rem; font-weight:700; background:#FEF3C7; color:#92400E; border:1px solid #FCD34D; border-radius:6px; cursor:pointer; white-space:nowrap;">
              الآن
            </button>
          </div>
        `;

        // 7. Working Hours (Column 7)
        const hoursDisplay = (state.workingHours != null && state.workingHours > 0)
          ? `${Number(state.workingHours).toFixed(1)} س`
          : '-';
        const hoursHtml = `
          <div style="text-align:center;">
            <span class="att-hours-badge finance-badge-pill" data-user-id="${emp.userId}"
              style="font-weight:800; font-size:0.825rem; background:#EFF6FF; color:#1D4ED8; border:1px solid #BFDBFE;">
              ${hoursDisplay}
            </span>
          </div>
        `;

        // 8. Notes Input (Column 8)
        const notesHtml = `
          <div>
            <input type="text" class="att-input-notes finance-filter-input" data-user-id="${emp.userId}"
              value="${this.escapeHtml(state.notes || '')}"
              placeholder="ملاحظات..."
              style="padding:5px 8px; font-size:0.8rem; width:100%; border-radius:6px;" />
          </div>
        `;

        // 9. Quick Actions (Column 9)
        const actionsHtml = `
          <div style="display:flex; align-items:center; justify-content:center; gap:6px;">
            <button type="button" class="btn-row-save" data-user-id="${emp.userId}"
              title="حفظ التغييرات لهذا الموظف في قاعدة البيانات مباشرة"
              style="background:#059669; color:#FFFFFF; border:none; border-radius:6px; padding:5px 10px; font-size:0.75rem; font-weight:700; cursor:pointer; display:flex; align-items:center; gap:4px;">
              💾 حفظ
            </button>
            <button type="button" class="btn-row-delete" data-user-id="${emp.userId}"
              title="حذف/إلغاء تسجيل هذا الموظف"
              style="background:#FEE2E2; color:#DC2626; border:1px solid #FECACA; border-radius:6px; padding:5px 8px; font-size:0.75rem; cursor:pointer;">
              ✕
            </button>
          </div>
        `;

        // 10. Approval Status (Column 10)
        let statusBadgeHtml = '';
        if (state.approvalStatus === 'APPROVED') {
          statusBadgeHtml = `<span class="cm-badge cm-badge-approved">✓ معتمد</span>`;
        } else if (state.approvalStatus === 'REJECTED') {
          statusBadgeHtml = `<span class="cm-badge cm-badge-rejected" title="${this.escapeHtml(state.rejectionReason || '')}">✕ مرفوض</span>`;
        } else if (state.approvalStatus === 'PENDING') {
          statusBadgeHtml = `<span class="cm-badge cm-badge-pending">⏳ قيد المراجعة</span>`;
        } else {
          statusBadgeHtml = state.status
            ? `<span class="finance-badge-pill" style="font-size:0.72rem; background:#F1F5F9; color:#0F172A;">${state.status === 'Present' ? 'حاضر' : 'غائب'}</span>`
            : `<span style="color:#94A3B8; font-size:0.75rem;">لم يسجل</span>`;
        }

        tr.innerHTML = `
          <td>${nameHtml}</td>
          <td>${roleHtml}</td>
          <td style="text-align:center;">${presentHtml}</td>
          <td style="text-align:center;">${absentHtml}</td>
          <td>${checkInHtml}</td>
          <td>${checkOutHtml}</td>
          <td>${hoursHtml}</td>
          <td>${notesHtml}</td>
          <td style="text-align:center;">${actionsHtml}</td>
          <td style="text-align:center;">${statusBadgeHtml}</td>
        `;

        if (isPresent) {
          tr.style.background = '#F0FDF4';
        } else if (isAbsent) {
          tr.style.background = '#FEF2F2';
        }

        this.bindRowEvents(tr, emp.userId);
        this.tableBody.appendChild(tr);
      });
    }

    bindRowEvents(tr, userId) {
      const chkPresent = tr.querySelector('.att-chk-present');
      const chkAbsent = tr.querySelector('.att-chk-absent');
      const inputIn = tr.querySelector('.att-input-checkin');
      const btnInNow = tr.querySelector('.btn-att-in-now');
      const inputOut = tr.querySelector('.att-input-checkout');
      const btnOutNow = tr.querySelector('.btn-att-out-now');
      const hoursBadge = tr.querySelector('.att-hours-badge');
      const inputNotes = tr.querySelector('.att-input-notes');
      const btnSave = tr.querySelector('.btn-row-save');
      const btnDelete = tr.querySelector('.btn-row-delete');

      const updateHoursBadge = () => {
        const state = this.recordsState.get(userId);
        if (!state) return;
        const hrs = this.calcWorkingHours(state.checkInTime, state.checkOutTime);
        state.workingHours = hrs;
        this.recordsState.set(userId, state);
        if (hoursBadge) {
          hoursBadge.textContent = (hrs != null && hrs > 0) ? `${Number(hrs).toFixed(1)} س` : '-';
        }
        this.updateStatsCounters();
      };

      if (chkPresent) {
        chkPresent.addEventListener('change', () => {
          const state = this.recordsState.get(userId) || {};
          if (chkPresent.checked) {
            if (chkAbsent) chkAbsent.checked = false;
            state.status = 'Present';
            if (!state.checkInTime) {
              const now = new Date().toISOString();
              state.checkInTime = now;
              if (inputIn) inputIn.value = this.getNowDateTimeLocalString();
            }
            tr.style.background = '#F0FDF4';
          } else {
            state.status = null;
            tr.style.background = 'transparent';
          }
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (chkAbsent) {
        chkAbsent.addEventListener('change', () => {
          const state = this.recordsState.get(userId) || {};
          if (chkAbsent.checked) {
            if (chkPresent) chkPresent.checked = false;
            state.status = 'Absent';
            tr.style.background = '#FEF2F2';
          } else {
            state.status = null;
            tr.style.background = 'transparent';
          }
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (btnInNow) {
        btnInNow.addEventListener('click', () => {
          const state = this.recordsState.get(userId) || {};
          state.status = 'Present';
          state.checkInTime = new Date().toISOString();
          if (chkPresent) chkPresent.checked = true;
          if (chkAbsent) chkAbsent.checked = false;
          if (inputIn) inputIn.value = this.getNowDateTimeLocalString();
          tr.style.background = '#F0FDF4';
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (btnOutNow) {
        btnOutNow.addEventListener('click', () => {
          const state = this.recordsState.get(userId) || {};
          state.checkOutTime = new Date().toISOString();
          if (inputOut) inputOut.value = this.getNowDateTimeLocalString();
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (inputIn) {
        inputIn.addEventListener('change', () => {
          const state = this.recordsState.get(userId) || {};
          if (inputIn.value) {
            state.checkInTime = new Date(inputIn.value).toISOString();
            if (!state.status) {
              state.status = 'Present';
              if (chkPresent) chkPresent.checked = true;
              tr.style.background = '#F0FDF4';
            }
          } else {
            state.checkInTime = null;
          }
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (inputOut) {
        inputOut.addEventListener('change', () => {
          const state = this.recordsState.get(userId) || {};
          state.checkOutTime = inputOut.value ? new Date(inputOut.value).toISOString() : null;
          this.recordsState.set(userId, state);
          updateHoursBadge();
        });
      }

      if (inputNotes) {
        inputNotes.addEventListener('input', (e) => {
          const state = this.recordsState.get(userId) || {};
          state.notes = e.target.value;
          this.recordsState.set(userId, state);
        });
      }

      // Quick Save for individual employee
      if (btnSave) {
        btnSave.addEventListener('click', async () => {
          const state = this.recordsState.get(userId) || {};
          btnSave.disabled = true;
          btnSave.innerHTML = '⏳ حفظ...';

          try {
            const payload = {
              id: state.recordId,
              userId: userId,
              date: this.selectedDate,
              status: state.status || 'Present',
              checkInTime: state.checkInTime,
              checkOutTime: state.checkOutTime,
              workingHours: state.workingHours,
              notes: state.notes || ''
            };

            const res = await window.ChatApi.saveAttendanceRecord(payload);
            if (res && res.success && res.data) {
              state.recordId = res.data.id;
              state.status = res.data.status;
              state.workingHours = res.data.workingHours;
              this.recordsState.set(userId, state);
              btnSave.innerHTML = '✓ تم!';
              btnSave.style.background = '#047857';
              setTimeout(() => {
                btnSave.innerHTML = '💾 حفظ';
                btnSave.style.background = '#059669';
                btnSave.disabled = false;
              }, 1500);
              this.showFeedback(`✓ تم حفظ سجل حضور ${emp.fullName || 'الموظف'} بنجاح في قاعدة البيانات.`, 'success');
              this.updateStatsCounters();
            } else {
              btnSave.innerHTML = '✕ فشل';
              btnSave.disabled = false;
              this.showFeedback(res?.message || 'تعذر حفظ السجل.', 'error');
            }
          } catch (err) {
            btnSave.innerHTML = '✕ خطأ';
            btnSave.disabled = false;
            this.showFeedback('حدث خطأ أثناء حفظ السجل.', 'error');
          }
        });
      }

      // Quick Delete for individual employee
      if (btnDelete) {
        btnDelete.addEventListener('click', async () => {
          const state = this.recordsState.get(userId);
          if (!state || !state.recordId) {
            // Not saved yet, just clear UI
            if (chkPresent) chkPresent.checked = false;
            if (chkAbsent) chkAbsent.checked = false;
            if (inputIn) inputIn.value = '';
            if (inputOut) inputOut.value = '';
            if (inputNotes) inputNotes.value = '';
            if (hoursBadge) hoursBadge.textContent = '-';
            tr.style.background = 'transparent';
            this.recordsState.set(userId, { recordId: null, status: null, checkInTime: null, checkOutTime: null, workingHours: null, notes: '' });
            this.updateStatsCounters();
            return;
          }

          if (!confirm(`هل أنت متأكد من حذف سجل حضور ${emp.fullName || 'الموظف'}؟`)) return;

          btnDelete.disabled = true;
          try {
            const res = await window.ChatApi.deleteAttendanceRecord(state.recordId);
            if (res && res.success) {
              if (chkPresent) chkPresent.checked = false;
              if (chkAbsent) chkAbsent.checked = false;
              if (inputIn) inputIn.value = '';
              if (inputOut) inputOut.value = '';
              if (inputNotes) inputNotes.value = '';
              if (hoursBadge) hoursBadge.textContent = '-';
              tr.style.background = 'transparent';
              this.recordsState.set(userId, { recordId: null, status: null, checkInTime: null, checkOutTime: null, workingHours: null, notes: '' });
              this.showFeedback('✓ تم حذف سجل الحضور من قاعدة البيانات.', 'success');
              this.updateStatsCounters();
            } else {
              this.showFeedback(res?.message || 'تعذر حذف السجل.', 'error');
            }
          } catch (err) {
            this.showFeedback('حدث خطأ أثناء حذف السجل.', 'error');
          } finally {
            btnDelete.disabled = false;
          }
        });
      }
    }

    updateStatsCounters() {
      let present = 0;
      let absent = 0;
      let unmarked = 0;
      let totalHrs = 0;

      this.employees.forEach(emp => {
        const state = this.recordsState.get(emp.userId);
        if (state && state.status) {
          const st = state.status.toLowerCase();
          if (st === 'present') {
            present++;
            if (state.workingHours != null && state.workingHours > 0) {
              totalHrs += Number(state.workingHours);
            }
          } else if (st === 'absent') {
            absent++;
          } else {
            unmarked++;
          }
        } else {
          unmarked++;
        }
      });

      const total = this.employees.length;
      const rate = total > 0 ? Math.round((present / total) * 100) : 0;

      if (this.statTotal) this.statTotal.textContent = total;
      if (this.statPresent) this.statPresent.textContent = present;
      if (this.statAbsent) this.statAbsent.textContent = absent;
      if (this.statUnmarked) this.statUnmarked.textContent = unmarked;
      if (this.statHours) this.statHours.textContent = totalHrs.toFixed(1);
      if (this.statRate) this.statRate.textContent = `${rate}%`;
    }

    async handleSubmit() {
      if (this.isSubmitting) return;

      const recordsToSubmit = [];
      for (const [userId, state] of this.recordsState.entries()) {
        if (state && state.status) {
          const emp = this.employees.find(e => e.userId === userId);
          recordsToSubmit.push({
            id: state.recordId,
            userId: userId,
            fullName: emp ? emp.fullName : 'Employee',
            role: emp ? emp.role : 'Employee',
            status: state.status,
            attendanceDateTime: state.checkInTime,
            checkInTime: state.checkInTime,
            checkOutTime: state.checkOutTime,
            workingHours: state.workingHours,
            notes: state.notes || '',
            isTestData: false
          });
        }
      }

      if (recordsToSubmit.length === 0) {
        this.showFeedback('يجب تسجيل حضور أو غياب لموظف واحد على الأقل قبل إرسال الكشف.', 'error');
        return;
      }

      this.isSubmitting = true;
      if (this.btnSubmit) {
        this.btnSubmit.disabled = true;
        this.btnSubmit.innerHTML = `
          <div class="finance-spinner" style="width:18px; height:18px; border-width:2px;"></div>
          <span>جاري إرسال كشف الحضور للمراجعة...</span>
        `;
      }

      try {
        const formData = new FormData();
        formData.append('attendanceDate', this.selectedDate);
        formData.append('notes', 'كشف الحضور اليومي المعتمد عبر البوابة.');
        formData.append('recordsJson', JSON.stringify(recordsToSubmit));

        this.selectedFiles.forEach(file => {
          formData.append('attachments', file, file.name);
        });

        let res;
        const isResubmit = this.currentSubmissionId && (this.currentStatus === 'REJECTED' || this.isEditMode);
        if (isResubmit) {
          res = await window.ChatApi.resubmitAttendance(this.currentSubmissionId, formData);
        } else {
          res = await window.ChatApi.submitAttendance(formData);
        }

        if (res && res.success) {
          this.showFeedback('✓ تم حفظ وإرسال كشف الحضور بنجاح إلى قاعدة البيانات.', 'success');
          this.selectedFiles = [];
          this.isEditMode = false;
          await this.loadAttendanceData(this.selectedDate);
        } else {
          this.showFeedback('تعذر إرسال كشف الحضور: ' + (res?.message || 'حدث خطأ في الخادم.'), 'error');
        }
      } catch (err) {
        console.error('Error submitting attendance:', err);
        this.showFeedback('حدث خطأ أثناء إرسال الكشف.', 'error');
      } finally {
        this.isSubmitting = false;
        if (this.btnSubmit) {
          this.renderStatusBadge();
        }
      }
    }

    previewAttachment(att) {
      if (!att) return;
      const overlay = document.getElementById('cm-preview-overlay');
      const body = document.getElementById('cm-preview-body');
      const title = document.getElementById('cm-preview-title');
      const popout = document.getElementById('cm-preview-popout-link');
      const download = document.getElementById('cm-preview-download-link');
      const closeBtn = document.getElementById('cm-preview-close');

      if (!overlay || !body) return;

      const viewUrl = att.viewUrl || `/api/control-monitoring/attachments/attendance/${att.fileName}`;
      const downloadUrl = att.downloadUrl || `${viewUrl}?download=true`;

      const token = window.ChatApi ? window.ChatApi.getToken() : null;
      const authedUrl = token ? `${viewUrl}${viewUrl.includes('?') ? '&' : '?'}t=${encodeURIComponent(token)}` : viewUrl;

      if (title) title.textContent = att.originalFileName || att.fileName || 'معاينة المرفق';
      if (popout) popout.href = authedUrl;
      if (download) download.href = downloadUrl;

      const isPdf = (att.fileType || '').toLowerCase().includes('pdf') || att.fileName.toLowerCase().endsWith('.pdf');

      if (isPdf) {
        body.innerHTML = `<iframe src="${this.escapeHtml(authedUrl)}" style="width:100%; height:75vh; border:none; border-radius:8px;"></iframe>`;
      } else {
        body.innerHTML = `
          <div style="text-align:center; padding:1.5rem;">
            <img src="${this.escapeHtml(authedUrl)}" alt="${this.escapeHtml(att.fileName)}" style="max-width:100%; max-height:75vh; border-radius:8px;" />
          </div>
        `;
      }

      overlay.style.display = 'flex';

      if (closeBtn) {
        closeBtn.onclick = () => {
          overlay.style.display = 'none';
          body.innerHTML = '';
        };
      }
    }

    showFeedback(message, type = 'info') {
      if (!this.feedbackBox) return;
      this.feedbackBox.style.display = 'block';
      this.feedbackBox.textContent = message;

      if (type === 'error') {
        this.feedbackBox.style.background = '#FEF2F2';
        this.feedbackBox.style.color = '#991B1B';
        this.feedbackBox.style.border = '1px solid #FECACA';
      } else if (type === 'success') {
        this.feedbackBox.style.background = '#ECFDF5';
        this.feedbackBox.style.color = '#065F46';
        this.feedbackBox.style.border = '1px solid #A7F3D0';
      } else {
        this.feedbackBox.style.background = '#F0F9FF';
        this.feedbackBox.style.color = '#0369A1';
        this.feedbackBox.style.border = '1px solid #BAE6FD';
      }

      this.feedbackBox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    formatFileSize(bytes) {
      if (!bytes || bytes <= 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    getInitials(name) {
      if (!name) return 'FS';
      const parts = name.trim().split(' ').filter(Boolean);
      if (parts.length >= 2) {
        return (parts[0][0] + parts[1][0]).toUpperCase();
      }
      return name.substring(0, 2).toUpperCase();
    }

    escapeHtml(str) {
      if (str === null || str === undefined) return '';
      return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }
  }

  window.AttendanceApp = new AttendanceApp();
})();
