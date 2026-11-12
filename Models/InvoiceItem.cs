using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaceStone.Web.Models;

public class InvoiceItem
{
    [Key]
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public virtual Invoice Invoice { get; set; } = null!;

    public int? TransportationId { get; set; }
    [ForeignKey(nameof(TransportationId))]
    public virtual Transportation? Transportation { get; set; }

    public int TransportationTransactionNumber { get; set; } = 0;

    public int? PurchaseOrderId { get; set; }
    [MaxLength(100)]
    public string? PONumber { get; set; }

    [MaxLength(100)]
    public string? WarehouseExportNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    [MaxLength(100)]
    public string? ProductionOrderNumber { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Required]
    [MaxLength(50)]
    public string Unit { get; set; } = "طن";

    [Column(TypeName = "decimal(18, 3)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal LineTotal { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    public DateTime? TransactionDate { get; set; }
}

