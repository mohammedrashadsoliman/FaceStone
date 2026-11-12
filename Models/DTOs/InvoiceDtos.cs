using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class InvoiceSummaryDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string InvoiceDateFormatted => InvoiceDate.ToString("yyyy-MM-dd HH:mm");
    public int? ProjectCodeId { get; set; }
    public string ProjectCodeValue { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = "COMPANY";
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? MaterialName { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string EntityDisplay => !string.IsNullOrWhiteSpace(PONumber) ? $"أمر شراء: {PONumber} ({SupplierName ?? CompanyName})" : CompanyName;
    public int TransactionCount { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "ISSUED";
    public string StatusArabic => Status switch
    {
        "DRAFT" => "مسودة",
        "ISSUED" => "معتمدة ومصدرة",
        "CANCELLED" => "ملغاة",
        _ => Status
    };
    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class InvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string InvoiceDateFormatted => InvoiceDate.ToString("yyyy-MM-dd HH:mm");
    public int? ProjectCodeId { get; set; }
    public string ProjectCodeValue { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = "COMPANY";
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? MaterialName { get; set; }
    public decimal? OrderedQuantity { get; set; }
    public decimal? InvoicedQuantity { get; set; }
    public decimal? PoUnitPrice { get; set; }
    public decimal? InvoiceUnitPrice { get; set; }
    public string? PriceChangedBy { get; set; }
    public DateTime? PriceChangedAt { get; set; }
    public int? CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyAddress { get; set; }
    public string? CompanyTaxNumber { get; set; }
    public string? CompanyPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "ISSUED";
    public string StatusArabic => Status switch
    {
        "DRAFT" => "مسودة",
        "ISSUED" => "معتمدة ومصدرة",
        "CANCELLED" => "ملغاة",
        _ => Status
    };
    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? TransportationId { get; set; }
    public int TransportationTransactionNumber { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public string? WarehouseExportNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Destination { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string? TransactionDateFormatted => TransactionDate?.ToString("yyyy-MM-dd HH:mm");
}

public class CreateInvoiceDto
{
    public int? ProjectCodeId { get; set; }

    [Required(ErrorMessage = "نوع الارتباط بالفاتورة مطلوب.")]
    public string RelatedEntityType { get; set; } = "COMPANY"; // "PURCHASE_ORDER" | "COMPANY"

    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? MaterialName { get; set; }
    public decimal? InvoicedQuantity { get; set; }
    public decimal? PoUnitPrice { get; set; }
    public decimal? InvoiceUnitPrice { get; set; }

    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyTaxNumber { get; set; }
    public string? CompanyPhone { get; set; }

    [Range(0, 1000000000, ErrorMessage = "تكلفة الشحن يجب أن تكون رقماً موجباً.")]
    public decimal ShippingPrice { get; set; } = 0m;

    [Range(0, 100, ErrorMessage = "نسبة الضريبة يجب أن تكون بين 0 و 100.")]
    public decimal TaxRate { get; set; } = 14.00m;

    [Range(0, 1000000000, ErrorMessage = "قيمة الخصم يجب أن تكون رقم موجب.")]
    public decimal DiscountAmount { get; set; } = 0m;

    public string Status { get; set; } = "ISSUED"; // "DRAFT" | "ISSUED"

    public string? Notes { get; set; }

    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

public class ProjectInvoiceDataDto
{
    public int ProjectCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? Description { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyAddress { get; set; }
    public bool HasEligibleRecords { get; set; }
    public string? Message { get; set; }
    public List<EligibleTransportationTransactionDto> EligibleTransportations { get; set; } = new();
}

public class CreateInvoiceItemDto
{
    public int? TransportationId { get; set; }

    public int? PurchaseOrderId { get; set; }

    [MaxLength(200)]
    public string? ProductName { get; set; }

    [Required(ErrorMessage = "سعر الوحدة مطلوب.")]
    [Range(0.001, 10000000.0, ErrorMessage = "سعر الوحدة يجب أن يكون رقماً موجباً أكبر من الصفر.")]
    public decimal UnitPrice { get; set; }

    [Range(0.001, 1000000.0, ErrorMessage = "الكمية يجب أن تكون رقماً موجباً.")]
    public decimal? Quantity { get; set; }

    public string Unit { get; set; } = "طن";
}

public class UpdateInvoiceDto : CreateInvoiceDto
{
    public int Id { get; set; }
}

public class EligibleTransportationTransactionDto
{
    public int TransportationId { get; set; }
    public int TransactionNumber { get; set; }
    public string TransactionNumberDisplay => $"#{TransactionNumber}";
    public string? WarehouseExportNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public decimal TotalTransportedQuantity { get; set; }
    public decimal PreviouslyInvoicedQuantity { get; set; }
    public decimal RemainingInvoicableQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public DateTime TransactionDate { get; set; }
    public string TransactionDateFormatted => TransactionDate.ToString("yyyy-MM-dd HH:mm");
    public string Destination { get; set; } = string.Empty;
    public int? ProjectCodeId { get; set; }
    public string ProjectCodeValue { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
}

public class EligiblePurchaseOrderDto
{
    public int Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal PreviouslyInvoicedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime OrderDate { get; set; }
    public string OrderDateFormatted => OrderDate.ToString("yyyy-MM-dd");
    public string? Destination { get; set; }
}

public class CreatePurchaseOrderDto
{
    [MaxLength(100)]
    public string? PONumber { get; set; }

    [Required(ErrorMessage = "المورد مطلوب.")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "اسم المادة أو الصنف مطلوب.")]
    [MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;

    [Range(0.001, 100000000, ErrorMessage = "الكمية المطلوبة يجب أن تكون أكبر من الصفر.")]
    public decimal Quantity { get; set; }

    [MaxLength(50)]
    public string Unit { get; set; } = "طن";

    [Range(0.01, 100000000, ErrorMessage = "سعر الوحدة يجب أن يكون رقماً موجباً.")]
    public decimal UnitPrice { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    public string? Notes { get; set; }
}

public class UpdatePurchaseOrderDto
{
    [Required(ErrorMessage = "المورد مطلوب.")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "اسم المادة أو الصنف مطلوب.")]
    [MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;

    [Range(0.001, 100000000, ErrorMessage = "الكمية المطلوبة يجب أن تكون أكبر من الصفر.")]
    public decimal Quantity { get; set; }

    [MaxLength(50)]
    public string Unit { get; set; } = "طن";

    [Range(0.01, 100000000, ErrorMessage = "سعر الوحدة يجب أن يكون رقماً موجباً.")]
    public decimal UnitPrice { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    public string? Notes { get; set; }
}

public class InvoiceStatisticsDto
{
    public int TotalInvoices { get; set; }
    public int DraftInvoices { get; set; }
    public int IssuedInvoices { get; set; }
    public int PrintedInvoices { get; set; }
    public decimal TotalInvoicedAmount { get; set; }
}

public class InvoiceQueryParameters
{
    public string? Search { get; set; }
    public string? Status { get; set; } // ALL, DRAFT, ISSUED, CANCELLED, PRINTED
    public int? ProjectCodeId { get; set; }
    public string? PONumber { get; set; }
    public int? CompanyId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
