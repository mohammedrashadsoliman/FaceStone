using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaceStone.Web.Models;

/// <summary>
/// Independent Supplier Entity (المورد)
/// Discovered and imported from previous departments (Laboratory, Warehouse Import)
/// or created and managed directly by the Finance Manager.
/// Exists completely independently of Purchase Orders.
/// </summary>
public class Supplier
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unique Supplier Code (كود المورد, e.g. SUP-0001)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SupplierCode { get; set; } = string.Empty;

    /// <summary>
    /// Supplier Trade / Official Name (اسم المورد)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [MaxLength(50)]
    public string? CommercialRegister { get; set; }

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    public string? Notes { get; set; }

    // =========================================================================
    // ORIGINATING DEPARTMENT INTEGRATION (الربط بالأقسام السابقة)
    // =========================================================================
    /// <summary>
    /// Material Name discovered from Laboratory or Warehouse
    /// </summary>
    [MaxLength(200)]
    public string? PrimaryMaterialName { get; set; }

    /// <summary>
    /// Material Type / Classification (e.g. MECHANICAL, CHEMICAL, مواد خام خرسانية)
    /// </summary>
    [MaxLength(100)]
    public string? PrimaryMaterialType { get; set; }

    /// <summary>
    /// Source Department: LABORATORY | WAREHOUSE_IMPORT | FINANCE | COMBINED
    /// </summary>
    [MaxLength(50)]
    public string SourceDepartment { get; set; } = "FINANCE";

    /// <summary>
    /// Reference Number from Laboratory Report (e.g. LAB-REF-2026-008903, SMP-0001)
    /// </summary>
    [MaxLength(100)]
    public string? RelatedLaboratoryReferenceNumber { get; set; }

    /// <summary>
    /// Warehouse Import Number (e.g. #1001, #3)
    /// </summary>
    [MaxLength(100)]
    public string? RelatedWarehouseImportNumber { get; set; }

    /// <summary>
    /// Initial registration date when supplier first appeared in system
    /// </summary>
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

    // =========================================================================
    // FINANCIAL SUMMARY TOTALS (الأرصدة المالية المستمرة)
    // =========================================================================
    [Column(TypeName = "decimal(18, 3)")]
    public decimal TotalAmountOwed { get; set; } = 0m;

    [Column(TypeName = "decimal(18, 3)")]
    public decimal TotalAmountPaid { get; set; } = 0m;

    [Column(TypeName = "decimal(18, 3)")]
    public decimal RemainingBalance { get; set; } = 0m; // TotalAmountOwed - TotalAmountPaid

    public DateTime? LastPaymentDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(150)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<SupplierFinancialTransaction> FinancialTransactions { get; set; } = new List<SupplierFinancialTransaction>();
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}

/// <summary>
/// Supplier Financial Transaction (سجل الحركات المالية للمورد)
/// Records amounts owed, payments, adjustments, and charges.
/// </summary>
public class SupplierFinancialTransaction
{
    [Key]
    public int Id { get; set; }

    public int SupplierId { get; set; }
    [ForeignKey(nameof(SupplierId))]
    public virtual Supplier Supplier { get; set; } = null!;

    /// <summary>
    /// Transaction Type:
    /// AMOUNT_OWED (مستحق للمورد / فاتورة توريد)
    /// PAYMENT (دفعة مسددة للمورد)
    /// ADJUSTMENT (تسوية مالية)
    /// CHARGE (خصم / رسوم إضافية)
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string TransactionType { get; set; } = "AMOUNT_OWED";

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Amount { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public int? InvoiceId { get; set; }
    [ForeignKey(nameof(InvoiceId))]
    public virtual Invoice? Invoice { get; set; }

    [Required]
    [MaxLength(150)]
    public string CreatedBy { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
