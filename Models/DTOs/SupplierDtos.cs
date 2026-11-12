using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class SupplierSummaryDto
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? CommercialRegister { get; set; }
    public string? ContactPerson { get; set; }
    public string? PrimaryMaterialName { get; set; }
    public string? PrimaryMaterialType { get; set; }
    public string SourceDepartment { get; set; } = "FINANCE";
    public string? RelatedLaboratoryReferenceNumber { get; set; }
    public string? RelatedWarehouseImportNumber { get; set; }
    public DateTime RegistrationDate { get; set; }
    public decimal TotalAmountOwed { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public int TransactionsCount { get; set; }
    public int PurchaseOrdersCount { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierDetailDto
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? CommercialRegister { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
    public string? PrimaryMaterialName { get; set; }
    public string? PrimaryMaterialType { get; set; }
    public string SourceDepartment { get; set; } = "FINANCE";
    public string? RelatedLaboratoryReferenceNumber { get; set; }
    public string? RelatedWarehouseImportNumber { get; set; }
    public DateTime RegistrationDate { get; set; }
    public decimal TotalAmountOwed { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public bool IsActive { get; set; }
    public List<SupplierFinancialTransactionDto> Transactions { get; set; } = new();
    public List<SupplierPoItemDto> PurchaseOrders { get; set; } = new();
}

public class SupplierFinancialTransactionDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string TransactionType { get; set; } = "AMOUNT_OWED";
    public string TransactionTypeArabic { get; set; } = "مستحق للمورد";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public int? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SupplierPoItemDto
{
    public int Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "قيد التوريد";
}

public class CreateSupplierDto
{
    [Required(ErrorMessage = "اسم المورد مطلوب.")]
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

    [MaxLength(200)]
    public string? PrimaryMaterialName { get; set; }

    [MaxLength(100)]
    public string? PrimaryMaterialType { get; set; }

    public string? Notes { get; set; }
}

public class UpdateSupplierDto
{
    [Required(ErrorMessage = "اسم المورد مطلوب.")]
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

    [MaxLength(200)]
    public string? PrimaryMaterialName { get; set; }

    [MaxLength(100)]
    public string? PrimaryMaterialType { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateSupplierTransactionDto
{
    [Required(ErrorMessage = "نوع الحركة مطلوب.")]
    public string TransactionType { get; set; } = "PAYMENT"; // AMOUNT_OWED | PAYMENT | ADJUSTMENT | CHARGE

    [Range(0.001, 1000000000, ErrorMessage = "المبلغ يجب أن يكون أكبر من الصفر.")]
    public decimal Amount { get; set; }

    public DateTime? TransactionDate { get; set; }

    [Required(ErrorMessage = "البيان أو الوصف مطلوب.")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(100)]
    public string? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public int? InvoiceId { get; set; }
}
