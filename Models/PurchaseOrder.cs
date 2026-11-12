using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

public class PurchaseOrder
{
    public int Id { get; set; }

    /// <summary>
    /// رقم أمر الشراء (PO) - Unique Purchase Order Number (e.g. PO-2026-9810)
    /// </summary>
    public string PONumber { get; set; } = string.Empty;

    /// <summary>
    /// المورد - Supplier Name
    /// </summary>
    public string Supplier { get; set; } = string.Empty;

    /// <summary>
    /// Foreign Key to independent Supplier entity
    /// </summary>
    public int? SupplierId { get; set; }
    public virtual Supplier? SupplierEntity { get; set; }

    /// <summary>
    /// اسم المادة أو الصنف (Material Name)
    /// </summary>
    public string MaterialName { get; set; } = "مواد خرسانية / ركام";

    /// <summary>
    /// وحدة القياس (Unit)
    /// </summary>
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// الكمية المسلمة المسترجعة آلياً من الفواتير المعتمدة فقط
    /// </summary>
    public decimal DeliveredQuantity { get; set; } = 0m;

    /// <summary>
    /// تاريخ التوريد المتوقع
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>
    /// تاريخ أمر الشراء - Purchase Order Date
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// كمية أمر الشراء (بالطن) - Total ordered quantity in tons
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// سعر الطن (ج.م) - Agreed ton price in Egyptian Pounds
    /// </summary>
    public decimal PricePerTon { get; set; }

    /// <summary>
    /// سعر الطن (ج.م) - Canonical PO Ton Price
    /// </summary>
    public decimal TonPrice { get; set; }

    /// <summary>
    /// قيمة أمر الشراء = كمية أمر الشراء × سعر الطن
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// رقم الفاتورة - Official Tax Invoice Number if available
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// ملاحظات - Optional notes
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// الوجهة المحددة لأمر الشراء - Destination (Determined automatically and strictly read-only for employees)
    /// </summary>
    public string Destination { get; set; } = "Cairo Factory";

    /// <summary>
    /// الكمية المتبقية من أمر الشراء (بالطن) - PO Remaining Quantity
    /// Subtracted automatically when transactions are submitted with actual weight.
    /// </summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>
    /// حالة أمر الشراء ("نشط" | "مكتمل")
    /// </summary>
    public string Status { get; set; } = "نشط";

    /// <summary>
    /// حالة السداد - Payment Status ("غير مدفوع" | "مدفوع جزئياً" | "مدفوع بالكامل")
    /// </summary>
    public string PaymentStatus { get; set; } = "غير مدفوع";

    /// <summary>
    /// تاريخ ووقت آخر تعديل لحالة السداد
    /// </summary>
    public DateTime? PaymentStatusUpdatedAt { get; set; }

    /// <summary>
    /// الموظف/المسؤول الذي عدّل حالة السداد (مدير المالية)
    /// </summary>
    public string? PaymentStatusUpdatedBy { get; set; }

    /// <summary>
    /// Payments recorded for this PO (CEO Dashboard Architecture)
    /// </summary>
    public ICollection<FinancePayment> Payments { get; set; } = new List<FinancePayment>();

    /// <summary>
    /// Transportation transactions recorded against this PO
    /// </summary>
    public ICollection<Transportation> TransportationTransactions { get; set; } = new List<Transportation>();

    /// <summary>
    /// Storage transactions recorded against this PO
    /// </summary>
    public ICollection<StorageTransaction> StorageTransactions { get; set; } = new List<StorageTransaction>();

    /// <summary>
    /// سجل تدقيق تغييرات حالة السداد
    /// </summary>
    public ICollection<PurchaseOrderPaymentAudit> PaymentAudits { get; set; } = new List<PurchaseOrderPaymentAudit>();

    /// <summary>
    /// الفواتير المرتبطة بأمر الشراء
    /// </summary>
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
