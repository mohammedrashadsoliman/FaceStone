using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaceStone.Web.Models;

public class Invoice
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public int? ProjectCodeId { get; set; }
    [ForeignKey(nameof(ProjectCodeId))]
    public virtual ProjectCode? ProjectCode { get; set; }

    [MaxLength(50)]
    public string ProjectCodeValue { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Association Type: "PURCHASE_ORDER" or "COMPANY"
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string RelatedEntityType { get; set; } = "COMPANY";

    public int? PurchaseOrderId { get; set; }
    [ForeignKey(nameof(PurchaseOrderId))]
    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    [MaxLength(100)]
    public string? PONumber { get; set; }

    public int? SupplierId { get; set; }
    [ForeignKey(nameof(SupplierId))]
    public virtual Supplier? SupplierEntity { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(200)]
    public string? MaterialName { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? OrderedQuantity { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? InvoicedQuantity { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? PoUnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? InvoiceUnitPrice { get; set; }

    [MaxLength(150)]
    public string? PriceChangedBy { get; set; }

    public DateTime? PriceChangedAt { get; set; }

    public int? CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public virtual StorageClient? Company { get; set; }

    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? CompanyAddress { get; set; }

    [MaxLength(50)]
    public string? CompanyTaxNumber { get; set; }

    [MaxLength(50)]
    public string? CompanyPhone { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Subtotal { get; set; }

    /// <summary>
    /// تكلفة الشحن (Shipping Price)
    /// </summary>
    [Column(TypeName = "decimal(18, 3)")]
    public decimal ShippingPrice { get; set; } = 0m;

    [Column(TypeName = "decimal(5, 2)")]
    public decimal TaxRate { get; set; } = 14.00m;

    [Column(TypeName = "decimal(18, 3)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Status: "DRAFT", "ISSUED", "CANCELLED"
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "ISSUED";

    public bool IsPrinted { get; set; } = false;
    public DateTime? PrintedAt { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MaxLength(150)]
    public string CreatedBy { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

