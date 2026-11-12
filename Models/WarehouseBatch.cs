using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Warehouse Batch (دفعة المستودع) representing a divided batch of an approved, compliant Laboratory reference.
/// Relationship: Laboratory Reference (1) -> (Many) Warehouse Batches
/// </summary>
public class WarehouseBatch
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to originating LaboratoryRawMaterialReport (Parent reference)
    /// </summary>
    public int LaboratoryReportId { get; set; }
    public LaboratoryRawMaterialReport? LaboratoryReport { get; set; }

    /// <summary>
    /// Production Reference Number (الرقم المرجعي التتبعي للإنتاج)
    /// </summary>
    public int ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Sample Code / Reference Number from Laboratory (رقم العينة, e.g. SMP-0001 or LAB-2026-00125)
    /// </summary>
    public string SampleCode { get; set; } = string.Empty;

    /// <summary>
    /// Unique Batch Number (رقم الدفعة, e.g. B-0001)
    /// </summary>
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to RawMaterials catalogue
    /// </summary>
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// Material Name (اسم الخامة / الصنف)
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// Material Classification: MECHANICAL (ميكانيكية) | CHEMICAL (كيميائية)
    /// </summary>
    public string MaterialClassification { get; set; } = "MECHANICAL";

    /// <summary>
    /// Original Supplier Name preserved from the Laboratory reference
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// Warehouse Storage Location
    /// </summary>
    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }

    /// <summary>
    /// Initial allocated batch quantity (TON)
    /// </summary>
    public decimal InitialQuantity { get; set; }

    /// <summary>
    /// Current available unconsumed batch quantity (TON)
    /// </summary>
    public decimal CurrentQuantity { get; set; }

    /// <summary>
    /// Originating Import Storage Operation
    /// </summary>
    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    /// <summary>
    /// Originating Import Storage Operation Item Line
    /// </summary>
    public int? StorageOperationItemId { get; set; }
    public StorageOperationItem? StorageOperationItem { get; set; }

    /// <summary>
    /// أمر الإنتاج المرتبط (للمنتجات المصنعة)
    /// </summary>
    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    /// <summary>
    /// عملية الإنتاج المرتبطة
    /// </summary>
    public int? ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    /// <summary>
    /// Import receipt date
    /// </summary>
    public DateTime ImportDate { get; set; }

    /// <summary>
    /// Status: PENDING | APPROVED | CONSUMED | REJECTED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public string? Notes { get; set; }

    // Navigation collections
    public ICollection<StorageOperationItem> OperationItems { get; set; } = new List<StorageOperationItem>();
    public ICollection<StorageInventoryLedger> LedgerMovements { get; set; } = new List<StorageInventoryLedger>();
    public ICollection<ProductionWorkInProcess> ProductionWipTransfers { get; set; } = new List<ProductionWorkInProcess>();
}

