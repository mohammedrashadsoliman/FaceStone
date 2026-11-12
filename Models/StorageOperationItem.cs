namespace FaceStone.Web.Models;

/// <summary>
/// Detail line for a material within a Storage Operation (Import, Export, Transfer)
/// </summary>
public class StorageOperationItem
{
    public int Id { get; set; }

    public int StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// Foreign key to linked WarehouseBatch (for Import batch creation or Export/Transfer batch consumption)
    /// </summary>
    public int? WarehouseBatchId { get; set; }
    public WarehouseBatch? WarehouseBatch { get; set; }

    /// <summary>
    /// Batch Number (رقم الدفعة, e.g. B-0001)
    /// </summary>
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Sample Code / Reference Number from Laboratory (رقم العينة, e.g. SMP-0001)
    /// </summary>
    public string? SampleCode { get; set; }

    /// <summary>
    /// Shared Production Reference Number
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// أمر الإنتاج المرتبط (عند صرف منتج نهائي)
    /// </summary>
    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    /// <summary>
    /// عملية الإنتاج المرتبطة
    /// </summary>
    public int? ProductionProcessId { get; set; }
    public ProductionProcess? ProductionProcess { get; set; }

    /// <summary>
    /// رقم التصنيع (Manufacture Number)
    /// </summary>
    public string? ManufactureNumber { get; set; }

    /// <summary>
    /// اسم الصنف
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// الكمية بالطن (decimal 18,3)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// رقم تتبع التحويل / البند (Generated tracking code, e.g. TRF-2026-0001-L1)
    /// </summary>
    public string TrackingNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
