using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// تقرير نتائج تحليل المواد الخام (Card 1 in Laboratory)
/// Originates the shared Production Reference Number for traceability across departments.
/// </summary>
public class LaboratoryRawMaterialReport
{
    public int Id { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج (يبدأ من 1 ويتصاعد تسلسلياً وفريد على مستوى النظام)
    /// </summary>
    public int ProductionReferenceNumber { get; set; }

    /// <summary>
    /// رقم العينة — يبدأ من 1 ويتصاعد تسلسلياً وفريد على مستوى النظام والمعمل
    /// </summary>
    public int SampleNumber { get; set; }

    /// <summary>
    /// كود ورقم العينة الرسمي المشترك (مثال: SMP-0001) المرجع المركزي الموحد
    /// </summary>
    public string? SampleCode { get; set; }

    /// <summary>
    /// تصنيف المادة: MECHANICAL (ميكانيكية) | CHEMICAL (كيميائية)
    /// </summary>
    public string MaterialClassification { get; set; } = "MECHANICAL";

    /// <summary>
    /// مسمى العينة (مثال: سيليكا باودر Micro Silica Powder)
    /// </summary>
    public string SampleName { get; set; } = "سيليكا باودر (Micro Silica Powder)";

    /// <summary>
    /// وصف العينة (مثال: بودرة رمادية اللون)
    /// </summary>
    public string? SampleDescription { get; set; } = "بودرة رمادية اللون";

    /// <summary>
    /// رقم الدفعة (.Batch no) — الرقم المرجعي التتبعي المشترك للتشغيلة
    /// </summary>
    public string? BatchNumber { get; set; }

    /// <summary>
    /// تاريخ استلام العينة
    /// </summary>
    public DateTime? SampleReceivedDate { get; set; }

    /// <summary>
    /// تاريخ إصدار التقرير
    /// </summary>
    public DateTime? ReportIssueDate { get; set; }

    /// <summary>
    /// مدير المعمل — الاسم
    /// </summary>
    public string? LabManagerName { get; set; }

    /// <summary>
    /// مدير المعمل — التوقيع
    /// </summary>
    public string? LabManagerSignature { get; set; }

    /// <summary>
    /// مدير المعمل — التاريخ
    /// </summary>
    public DateTime? LabManagerDate { get; set; }

    /// <summary>
    /// قرار المطابقة النهائي للمواصفة: COMPLY (مطابق) | NOT_COMPLIED (غير مطابق)
    /// </summary>
    public string ComplyStatus { get; set; } = "COMPLY";

    /// <summary>
    /// قسم المعمل: PHYSICAL (المعمل الفيزيائي) | CHEMICAL (المعمل الكيميائي)
    /// </summary>
    public string LaboratorySection { get; set; } = "PHYSICAL";

    /// <summary>
    /// تاريخ ووقت الفحص والتحليل
    /// </summary>
    public DateTime AnalysisDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// بيان ووصف المادة الخام
    /// </summary>
    public string RawMaterialDescription { get; set; } = string.Empty;

    /// <summary>
    /// ربط بكارت الصنف في المستودعات (إن وجد)
    /// </summary>
    public int? RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// اسم المورد / المصدر
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// ربط بحركة النقل الواردة (Transportation) إن وجدت
    /// </summary>
    public int? SourceTransportationId { get; set; }
    public Transportation? SourceTransportation { get; set; }
    public int? TransportationTransactionNumber { get; set; }

    /// <summary>
    /// ربط بأمر الشراء إن وجد
    /// </summary>
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public string? PONumber { get; set; }

    /// <summary>
    /// الكمية المستلمة (بالطن)
    /// </summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>
    /// إجمالي وزن المواد التي تم اختبارها (طن)
    /// </summary>
    public decimal TestedMaterialWeight { get; set; }

    /// <summary>
    /// مستند تأكيد الكمية المستلمة (مسار الملف)
    /// </summary>
    public string? QuantityConfirmationPath { get; set; }
    public string? QuantityConfirmationFileName { get; set; }
    public string? QuantityConfirmationFileType { get; set; }
    public long QuantityConfirmationFileSize { get; set; }

    /// <summary>
    /// الكمية المقبولة (بالطن) - >= 0 وتتاح للمستودعات بعد الاعتماد
    /// </summary>
    public decimal AcceptedQuantity { get; set; }

    /// <summary>
    /// الكمية المرفوضة (بالطن) = المستلمة - المقبولة
    /// </summary>
    public decimal RejectedQuantity { get; set; }

    /// <summary>
    /// وحدة القياس (طن / كجم)
    /// </summary>
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// نتائج الفحوصات والتحاليل المعملية (JSON مخزن للقيم الرقمية والوحدات)
    /// </summary>
    public string? AnalysisResultsJson { get; set; }

    /// <summary>
    /// حالة فحص المعمل الفيزيائي: PENDING | APPROVED | REJECTED
    /// </summary>
    public string? PhysicalInspectionStatus { get; set; }
    public string? PhysicalInspectedBy { get; set; }
    public DateTime? PhysicalInspectedAt { get; set; }
    public string? PhysicalNotes { get; set; }

    /// <summary>
    /// حالة فحص المعمل الكيميائي: PENDING | COMPLIANT | NON_COMPLIANT | REJECTED
    /// </summary>
    public string? ChemicalInspectionStatus { get; set; }
    public string? ChemicalInspectedBy { get; set; }
    public DateTime? ChemicalInspectedAt { get; set; }
    public string? ChemicalNotes { get; set; }
    public string? ChemicalResultsJson { get; set; }

    /// <summary>
    /// الحالة الإجمالية للبوابة المعملية المزدوجة:
    /// PENDING_PHYSICAL | PENDING_CHEMICAL | COMPLIANT_APPROVED | NON_COMPLIANT | REJECTED
    /// </summary>
    public string? OverallStatus { get; set; }

    /// <summary>
    /// قرار الفحص المعملي:
    /// ACCEPTED (مقبول) | PARTIALLY_ACCEPTED (مقبول جزئياً) | REJECTED (مرفوض)
    /// </summary>
    public string InspectionDecision { get; set; } = "ACCEPTED";

    /// <summary>
    /// ملاحظات الفحص والتقرير
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// مستند التقرير الفني والمرفقات
    /// </summary>
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    /// <summary>
    /// الموظف المسؤول الفاحص (مشتق من الحساب المصادق عليه)
    /// </summary>
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>
    /// حالة المراجعة والاعتماد بقسم التحكم والمراقبة: PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// هل تم استلام الكمية المقبولة بالمستودع؟
    /// </summary>
    public bool IsStorageReceived { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public int? StorageOperationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// فحوصات التشغيل المرتبطة بنفس الرقم المرجعي
    /// </summary>
    public ICollection<LaboratoryInProcessReport> InProcessReports { get; set; } = new List<LaboratoryInProcessReport>();

    /// <summary>
    /// شهادات مطابقة المنتج النهائي المرتبطة
    /// </summary>
    public ICollection<LaboratoryFinalConformityCertificate> FinalCertificates { get; set; } = new List<LaboratoryFinalConformityCertificate>();

    /// <summary>
    /// الدفعات المخزنية المقسمة من هذا التقرير والمرجع المعملي
    /// </summary>
    public ICollection<WarehouseBatch> Batches { get; set; } = new List<WarehouseBatch>();
}
