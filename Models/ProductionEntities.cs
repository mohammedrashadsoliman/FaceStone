using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Sequence tracker for Production Orders, Batches, Outputs, and Reworks
/// </summary>
public class ProductionSequence
{
    /// <summary>
    /// ORDER | BATCH | OUTPUT | REWORK
    /// </summary>
    public string SequenceType { get; set; } = string.Empty;

    public int NextValue { get; set; } = 1;
}

/// <summary>
/// Production Order / Request (أمر وطلب إنتاج)
/// Tracks requested output, planned raw materials, storage approval, and manufacturing lifecycle.
/// </summary>
public class ProductionOrder
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Order Identifier (e.g. ORD-2026-0001)
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Shared Lifecycle Production Reference Number originated from Laboratory
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Requested Product Name or Manufacturing Process (e.g. خرسانة جاهزة رتبة C35)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    public string? ProductDescription { get; set; }
    public string? LaboratoryManagerName { get; set; }
    public Guid? LaboratoryManagerId { get; set; }
    public string? ProductionInstructions { get; set; }
    public int FormulaVersion { get; set; } = 1;
    public decimal TotalPlannedRawMaterialQuantity { get; set; }
    public decimal TotalTransferredQuantity { get; set; }
    public int? LinkedProductionProcessId { get; set; }
    public string? LinkedProductionProcessNumber { get; set; }

    /// <summary>
    /// Requested target output quantity (TON)
    /// </summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>
    /// Unit of measure (default: طن)
    /// </summary>
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// Warehouse Storage Location from which materials are requested
    /// </summary>
    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    /// <summary>
    /// Order status:
    /// DRAFT | PENDING_STORAGE_APPROVAL | PARTIALLY_ISSUED | READY_FOR_PRODUCTION | 
    /// IN_PRODUCTION | ON_HOLD_LABORATORY | PENDING_FINAL_LABORATORY_INSPECTION | 
    /// REWORK_REQUIRED | APPROVED_FOR_STORAGE | COMPLETED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "DRAFT";

    /// <summary>
    /// Current Manufacturing Stage (الخلط الأولي | الطحن والتجهيز | التشكيل والصب | المعالجة | التجفيف | المرحلة النهائية)
    /// </summary>
    public string CurrentStage { get; set; } = "الخلط الأولي";

    /// <summary>
    /// Production Manager or responsible employee
    /// </summary>
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    /// <summary>
    /// Storage Transfer Operation fulfilling this request
    /// </summary>
    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    public bool IsStorageApproved { get; set; }
    public DateTime? StorageApprovedAt { get; set; }
    public string? StorageApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Planned raw material requirements
    /// </summary>
    public ICollection<ProductionOrderItem> Items { get; set; } = new List<ProductionOrderItem>();

    /// <summary>
    /// Active and historical production batches under this order
    /// </summary>
    public ICollection<ProductionBatch> Batches { get; set; } = new List<ProductionBatch>();

    /// <summary>
    /// Recorded production outputs under this order
    /// </summary>
    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();

    public ICollection<ProductionOrderTransfer> Transfers { get; set; } = new List<ProductionOrderTransfer>();
    public ICollection<ProductionOrderFormulaVersion> FormulaVersions { get; set; } = new List<ProductionOrderFormulaVersion>();
    public ICollection<ProductionFinishedProduct> FinishedProducts { get; set; } = new List<ProductionFinishedProduct>();
}

/// <summary>
/// Planned raw material line item for a Production Order
/// </summary>
public class ProductionOrderItem
{
    public int Id { get; set; }

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public string MaterialName { get; set; } = string.Empty;
    public string? MaterialType { get; set; }

    public int? WarehouseBatchId { get; set; }
    public WarehouseBatch? WarehouseBatch { get; set; }
    public string? BatchNumber { get; set; }
    public int? ProductionReferenceNumber { get; set; }
    public string? SampleCode { get; set; }

    /// <summary>
    /// Requested quantity (TON)
    /// </summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>
    /// Actual quantity issued by Storage (TON)
    /// </summary>
    public decimal IssuedQuantity { get; set; }

    /// <summary>
    /// Actual quantity consumed in manufacturing (TON)
    /// </summary>
    public decimal ConsumedQuantity { get; set; }

    public decimal AvailableQuantityBefore { get; set; }

    public string Unit { get; set; } = "طن";

    public int? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string? StorageLocationName { get; set; }

    public int? LaboratoryRawMaterialReportId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Active Production Batch (تشغيلة الإنتاج)
/// Governs stage-by-stage execution, in-process laboratory quality gates, and holds.
/// </summary>
public class ProductionBatch
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Batch Tracking Identifier (e.g. BATCH-2026-0001 or PRD-2026-0001-B1)
    /// </summary>
    public string BatchNumber { get; set; } = string.Empty;

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    /// <summary>
    /// Shared Lifecycle Production Reference Number originated from Laboratory
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Link to existing ProductionWorkInProcess for full backward compatibility
    /// </summary>
    public int? ProductionWorkInProcessId { get; set; }
    public ProductionWorkInProcess? ProductionWorkInProcess { get; set; }

    /// <summary>
    /// Current Manufacturing Stage
    /// </summary>
    public string CurrentStage { get; set; } = "الخلط الأولي";

    /// <summary>
    /// Status: IN_PROGRESS | ON_HOLD_LABORATORY | READY_FOR_INSPECTION | COMPLETED | CANCELLED | REWORK
    /// </summary>
    public string Status { get; set; } = "IN_PROGRESS";

    /// <summary>
    /// Quality Gate Hold: TRUE if an in-process laboratory test has failed (DOES_NOT_MEET)
    /// </summary>
    public bool QualityHold { get; set; }

    /// <summary>
    /// Reason for Laboratory Hold
    /// </summary>
    public string? QualityHoldReason { get; set; }

    /// <summary>
    /// The failed In-Process Report ID that triggered the hold
    /// </summary>
    public int? QualityHoldReportId { get; set; }

    public DateTime? QualityHoldAt { get; set; }
    public DateTime? QualityHoldReleasedAt { get; set; }
    public string? QualityHoldReleasedBy { get; set; }

    /// <summary>
    /// Total actual raw material input issued to this batch (TON)
    /// </summary>
    public decimal TotalInputQuantity { get; set; }

    /// <summary>
    /// Total recorded output from this batch (TON)
    /// </summary>
    public decimal TotalOutputQuantity { get; set; }

    /// <summary>
    /// Total recorded waste from this batch (TON)
    /// </summary>
    public decimal TotalWasteQuantity { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();
}

/// <summary>
/// Production Output (تسجيل ناتج الإنتاج)
/// Classifies actual manufacturing output as Semi-Finished, Finished, or Recoverable Waste.
/// </summary>
public class ProductionOutput
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Output Identifier (e.g. OUT-2026-0001)
    /// </summary>
    public string OutputNumber { get; set; } = string.Empty;

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int ProductionBatchId { get; set; }
    public ProductionBatch? ProductionBatch { get; set; }

    public int? ProductionReferenceNumber { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Output Classification:
    /// SEMI_FINISHED (منتج نصف مصنع)
    /// FINISHED (منتج تام الصنع)
    /// RECOVERABLE_WASTE (مخلفات قابلة للاستفادة / منتج ثانوي)
    /// </summary>
    public string Classification { get; set; } = "FINISHED";

    /// <summary>
    /// Actual measured quantity produced (TON)
    /// </summary>
    public decimal Quantity { get; set; }

    public string Unit { get; set; } = "طن";

    /// <summary>
    /// Production stage where this output was produced
    /// </summary>
    public string ProductionStage { get; set; } = string.Empty;

    public DateTime OutputDate { get; set; } = DateTime.UtcNow;

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>
    /// For SEMI_FINISHED: next intended production stage or destination
    /// </summary>
    public string? NextStage { get; set; }

    /// <summary>
    /// Destination Storage Location for receiving
    /// </summary>
    public int? DestinationStorageLocationId { get; set; }
    public StorageLocation? DestinationStorageLocation { get; set; }
    public string? DestinationStorageLocationName { get; set; }

    /// <summary>
    /// Laboratory Conformity Status (for Finished products):
    /// PENDING_INSPECTION (بانتظار الفحص النهائي) | CONFORMS (مطابق للمواصفات) | 
    /// NON_CONFORMING (غير مطابق) | NOT_APPLICABLE (لا ينطبق)
    /// </summary>
    public string LaboratoryConformityStatus { get; set; } = "PENDING_INSPECTION";

    /// <summary>
    /// Linked Laboratory Final Product Conformity Certificate ID
    /// </summary>
    public int? LaboratoryCertificateId { get; set; }
    public LaboratoryFinalConformityCertificate? LaboratoryCertificate { get; set; }

    /// <summary>
    /// Has this output been received and posted to warehouse inventory by Storage?
    /// </summary>
    public bool IsStorageReceived { get; set; }
    public decimal StorageReceivedQuantity { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public string? StorageReceivedBy { get; set; }
    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    /// <summary>
    /// If NON_CONFORMING:
    /// REWORK_REMANUFACTURE | CLASSIFIED_PRODUCT | PENDING_DECISION
    /// </summary>
    public string? NonConformityAction { get; set; }

    public bool IsReworkCreated { get; set; }
    public int? ReworkOrderId { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Traceable Rework and Remanufacturing Order (أمر إعادة التشغيل والتصنيع)
/// Created when a finished product fails Laboratory conformity and is returned for rework.
/// </summary>
public class ProductionRework
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Rework Identifier (e.g. RWK-2026-0001)
    /// </summary>
    public string ReworkNumber { get; set; } = string.Empty;

    public int OriginalProductionOrderId { get; set; }
    public int OriginalProductionBatchId { get; set; }
    public int OriginalProductionOutputId { get; set; }

    public int? ProductionReferenceNumber { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// PENDING | IN_PROGRESS | COMPLETED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    public int? NewProductionOrderId { get; set; }
    public int? NewProductionBatchId { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Production Process (عملية الإنتاج)
/// Unique root operation grouping multiple Warehouse references and batches, recording raw material consumption,
/// finished goods manufacturing, automatic waste tracking, and weighbridge documentation.
/// </summary>
public class ProductionProcess
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Process Tracking Identifier (e.g. PRD-2026-000001)
    /// </summary>
    public string ProcessNumber { get; set; } = string.Empty;

    public DateTime ProductionDate { get; set; } = DateTime.UtcNow;

    public int? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string? StorageLocationName { get; set; }

    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal WastePercentage { get; set; }
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// IN_PROGRESS | COMPLETED | ON_HOLD
    /// </summary>
    public string Status { get; set; } = "IN_PROGRESS";

    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public string? ProductionOrderNumber { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Incoming Raw Materials Weighbridge attachment (PDF/JPG/PNG)
    /// </summary>
    public string? IncomingWeighbridgePath { get; set; }
    public string? IncomingWeighbridgeFileName { get; set; }
    public string? IncomingWeighbridgeFileType { get; set; }
    public long IncomingWeighbridgeFileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<ProductionProcessMaterial> Materials { get; set; } = new List<ProductionProcessMaterial>();
    public ICollection<ProductionFinishedProduct> FinishedProducts { get; set; } = new List<ProductionFinishedProduct>();
    public ICollection<ProductionAttachment> Attachments { get; set; } = new List<ProductionAttachment>();
    public ICollection<ProductionWasteRecord> WasteRecords { get; set; } = new List<ProductionWasteRecord>();
}

/// <summary>
/// Consumed Raw Material item for a Production Process, linked to a specific Warehouse Batch and Laboratory Reference
/// </summary>
public class ProductionProcessMaterial
{
    public int Id { get; set; }

    public int ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    public int WarehouseBatchId { get; set; }
    public WarehouseBatch? WarehouseBatch { get; set; }
    public string BatchNumber { get; set; } = string.Empty;

    public int? LaboratoryReportId { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public string SampleCode { get; set; } = string.Empty;

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialClassification { get; set; } = string.Empty;

    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    public decimal AvailableQuantityBefore { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal RemainingQuantityAfter { get; set; }
    public string Unit { get; set; } = "طن";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Finished Product manufactured from a Production Process (المنتج النهائي)
/// </summary>
public class ProductionFinishedProduct
{
    public int Id { get; set; }

    public int ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    /// <summary>
    /// Direct relationship to authorizing Production Order
    /// </summary>
    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public string? ProductionOrderNumber { get; set; }

    public string FinishedProductName { get; set; } = string.Empty;

    /// <summary>
    /// Distinct Manufacture Business Identifier (e.g. MFG-2026-0001)
    /// </summary>
    public string ManufactureNumber { get; set; } = string.Empty;

    public decimal ManufacturedQuantity { get; set; }
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// مكان تخزين المنتج النهائي بالمستودع المحدد
    /// </summary>
    public int? DestinationStorageLocationId { get; set; }
    public StorageLocation? DestinationStorageLocation { get; set; }
    public string? DestinationStorageLocationName { get; set; }

    public DateTime ProductionDate { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }

    /// <summary>
    /// Finished Product Weighbridge attachment (PDF/JPG/PNG)
    /// </summary>
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeFileType { get; set; }
    public long WeighbridgeFileSize { get; set; }

    /// <summary>
    /// Laboratory Certification Status:
    /// PENDING_INSPECTION (قيد فحص المختبر)
    /// UNDER_INSPECTION (قيد الفحص)
    /// APPROVED (معتمد مخبرياً – مطابق للمواصفات)
    /// REJECTED (مرفوض مخبرياً – غير مطابق للمواصفات)
    /// </summary>
    public string LaboratoryConformityStatus { get; set; } = "PENDING_INSPECTION";

    public int? LaboratoryCertificateId { get; set; }
    public LaboratoryFinalConformityCertificate? LaboratoryCertificate { get; set; }

    public string? LaboratoryCertificateNumber { get; set; }
    public DateTime? LaboratoryCertifiedAt { get; set; }
    public string? LaboratoryCertifiedBy { get; set; }
    public string? LaboratoryRejectionReason { get; set; }
    public string? LaboratoryCorrectiveAction { get; set; }
    public string? LaboratoryNotes { get; set; }
    public string? LaboratoryQualityParametersJson { get; set; }

    /// <summary>
    /// Indicates whether the certified finished product has been officially received/imported into Warehouse inventory
    /// </summary>
    public bool IsStorageReceived { get; set; } = false;
    public int? WarehouseImportOperationId { get; set; }
    public DateTime? StorageReceivedDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Physical weighbridge or quality attachment for a Production Process / Finished Product
/// </summary>
public class ProductionAttachment
{
    public int Id { get; set; }

    public int ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    public int? FinishedProductId { get; set; }
    public ProductionFinishedProduct? FinishedProduct { get; set; }

    /// <summary>
    /// IncomingWeighbridge | FinishedProductWeighbridge
    /// </summary>
    public string AttachmentType { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? StoredFileName { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }

    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Mass balance waste calculation audit record
/// </summary>
public class ProductionWasteRecord
{
    public int Id { get; set; }

    public int ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal WastePercentage { get; set; }
    public string Unit { get; set; } = "طن";

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string CalculatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Physical Warehouse Transfer event fulfilling raw materials for a Production Order
/// </summary>
public class ProductionOrderTransfer
{
    public int Id { get; set; }

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int ProductionOrderItemId { get; set; }
    public ProductionOrderItem? ProductionOrderItem { get; set; }

    public int WarehouseBatchId { get; set; }
    public WarehouseBatch? WarehouseBatch { get; set; }
    public string BatchNumber { get; set; } = string.Empty;

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int? ProductionReferenceNumber { get; set; }

    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }
    public string? TransferTransactionNumber { get; set; }

    public decimal TransferredQuantity { get; set; }
    public string Unit { get; set; } = "طن";

    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    public string ConfirmedBy { get; set; } = string.Empty;
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

/// <summary>
/// Preserved historical manufacturing formula version for a Production Order
/// </summary>
public class ProductionOrderFormulaVersion
{
    public int Id { get; set; }

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int VersionNumber { get; set; } = 1;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public decimal TargetOutputQuantity { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string FormulaJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
