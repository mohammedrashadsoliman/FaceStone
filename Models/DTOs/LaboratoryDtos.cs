using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class LaboratoryDashboardStatsDto
{
    public int PhysicalReportsCount { get; set; }
    public int ChemicalReportsCount { get; set; }
    public int InProcessTestsCount { get; set; }
    public int FinalCertificatesCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ConformingBatchesCount { get; set; }
    public int NonConformingBatchesCount { get; set; }
    public decimal TotalAcceptedRawMaterialTons { get; set; }
    public decimal TotalFinalProductTons { get; set; }
    public int ActiveTraceableLifecyclesCount { get; set; }

    // Aliases for frontend & API compatibility
    public int ActiveProductionLifecycles => ActiveTraceableLifecyclesCount;
    public decimal TotalAcceptedRawMaterial => TotalAcceptedRawMaterialTons;
    public decimal TotalConformingFinishedProduct => TotalFinalProductTons;
}

public class LaboratoryReferenceLookupDto
{
    public int ProductionReferenceNumber { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleCode { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string RawMaterialDescription { get; set; } = string.Empty;
    public int? RawMaterialId { get; set; }
    public string? RawMaterialName { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? TransportationTransactionNumber { get; set; }
    public string? PONumber { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public DateTime AnalysisDateTime { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsStorageReceived { get; set; }
    public int InProcessTestsCount { get; set; }
    public bool HasFinalCertificate { get; set; }
    public string? FinalProductName { get; set; }
    public decimal? FinalProductQuantity { get; set; }
    public string? ConformityDecision { get; set; }
    public string Stage { get; set; } = "RAW_MATERIAL_ACCEPTED";
}

public class NextSampleNumberDto
{
    public int SampleNumber { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string LaboratorySection { get; set; } = string.Empty;
}

public class LaboratoryBatchLookupDto
{
    public int ProductionReferenceNumber { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleCode { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public string SampleDescription { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? PONumber { get; set; }
    public string LaboratorySection { get; set; } = "PHYSICAL";
    public DateTime CreatedAt { get; set; }
    public string SourceType { get; set; } = "LABORATORY_REPORT";
}

public class CreateRawMaterialReportDto
{
    public int? ProductionReferenceNumber { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleCode { get; set; }
    public string? MaterialClassification { get; set; } = "MECHANICAL";
    public string? SampleName { get; set; } = "سيليكا باودر (Micro Silica Powder)";
    public string? SampleDescription { get; set; } = "بودرة رمادية اللون";
    public string? BatchNumber { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public DateTime? ReportIssueDate { get; set; }
    public string? LabManagerName { get; set; }
    public string? LabManagerSignature { get; set; }
    public DateTime? LabManagerDate { get; set; }
    public string? ComplyStatus { get; set; } = "COMPLY";
    public string? LaboratorySection { get; set; }
    public DateTime? AnalysisDateTime { get; set; }
    public string? RawMaterialDescription { get; set; }
    public int? RawMaterialId { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal ReceivedQuantity { get; set; }
    public decimal TestedMaterialWeight { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal? RejectedQuantity { get; set; }
    public string? AnalysisResultsJson { get; set; }
    public string InspectionDecision { get; set; } = "ACCEPTED";
    public string? Notes { get; set; }
    [JsonIgnore]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
    [JsonIgnore]
    public IFormFile? QuantityConfirmationDocument { get; set; }
    public string? QuantityConfirmationPath { get; set; }
    public string? QuantityConfirmationFileName { get; set; }
}

public class RawMaterialReportDto
{
    public int Id { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public int SampleNumber { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public string MaterialClassification { get; set; } = "MECHANICAL";
    public string SampleName { get; set; } = string.Empty;
    public string? SampleDescription { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public DateTime? ReportIssueDate { get; set; }
    public string? LabManagerName { get; set; }
    public string? LabManagerSignature { get; set; }
    public DateTime? LabManagerDate { get; set; }
    public string ComplyStatus { get; set; } = "COMPLY";
    public string LaboratorySection { get; set; } = string.Empty;
    public DateTime AnalysisDateTime { get; set; }
    public string RawMaterialDescription { get; set; } = string.Empty;
    public int? RawMaterialId { get; set; }
    public string? RawMaterialName { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? TransportationTransactionNumber { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal ReceivedQuantity { get; set; }
    public decimal TestedMaterialWeight { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? AnalysisResultsJson { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string? QuantityConfirmationPath { get; set; }
    public string? QuantityConfirmationFileName { get; set; }
    public string? QuantityConfirmationFileType { get; set; }
    public long QuantityConfirmationFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsStorageReceived { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public int? StorageOperationId { get; set; }
    public string? PhysicalInspectionStatus { get; set; }
    public string? PhysicalInspectedBy { get; set; }
    public DateTime? PhysicalInspectedAt { get; set; }
    public string? PhysicalNotes { get; set; }
    public string? ChemicalInspectionStatus { get; set; }
    public string? ChemicalInspectedBy { get; set; }
    public DateTime? ChemicalInspectedAt { get; set; }
    public string? ChemicalNotes { get; set; }
    public string? ChemicalResultsJson { get; set; }
    public string? OverallStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class PendingChemicalReferenceDto
{
    public int Id { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public int SampleNumber { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public string? SampleDescription { get; set; }
    public string MaterialClassification { get; set; } = "MECHANICAL";
    public string? SupplierName { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal TestedMaterialWeight { get; set; }
    public string Unit { get; set; } = "طن";
    public string? PhysicalNotes { get; set; }
    public string? PhysicalInspectedBy { get; set; }
    public DateTime? PhysicalInspectedAt { get; set; }
    public string? PhysicalResultsJson { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
}

public class SubmitChemicalInspectionDto
{
    public int LaboratoryReportId { get; set; }
    public string? SampleCode { get; set; }
    public DateTime? AnalysisDateTime { get; set; }
    public string ComplyStatus { get; set; } = "COMPLY"; // COMPLY | NOT_COMPLIED
    public string? Notes { get; set; }
    public string? AnalysisResultsJson { get; set; }
    public string? LabManagerName { get; set; }
    public string? LabManagerSignature { get; set; }
    public DateTime? LabManagerDate { get; set; }
    [JsonIgnore]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class CreateInProcessReportDto
{
    public int ProductionReferenceNumber { get; set; }
    public string? ProductionBatchNumber { get; set; }
    public int? ProductionWorkInProcessId { get; set; }
    public string? LaboratorySection { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime? TestDateTime { get; set; }
    public string ProductionStage { get; set; } = string.Empty;
    public string? AnalysisResultsJson { get; set; }
    public string? SpecificationCriteria { get; set; }
    public string InspectionDecision { get; set; } = "MEETS_REQUIREMENTS";
    public string? Notes { get; set; }
    [JsonIgnore]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class InProcessReportDto
{
    public int Id { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public int? RawMaterialReportId { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleCode { get; set; }
    public string ProductionBatchNumber { get; set; } = string.Empty;
    public int? ProductionWorkInProcessId { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime TestDateTime { get; set; }
    public string ProductionStage { get; set; } = string.Empty;
    public string? AnalysisResultsJson { get; set; }
    public string? SpecificationCriteria { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateFinalConformityCertificateDto
{
    public int? ProductionReferenceNumber { get; set; }
    public string? LaboratorySection { get; set; }
    public string FinalProductName { get; set; } = string.Empty;
    public decimal? ActualFinalProductQuantity { get; set; }
    public string QuantityUnit { get; set; } = "طن";
    public string? ProductionBatchNumber { get; set; }
    public int? ProductionWorkInProcessId { get; set; }
    public int? ProductionFinishedProductId { get; set; }
    public int? ProductionProcessId { get; set; }
    public string? ProductionProcessNumber { get; set; }
    public string? ManufactureNumber { get; set; }
    public int? ProductionOrderId { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public decimal? TargetQuantity { get; set; }
    [JsonConverter(typeof(StringOrArrayConverter))]
    public string? AssociatedReferenceNumbers { get; set; }
    public DateTime? ProductionCompletionDateTime { get; set; }
    public decimal? ActualRawMaterialInputQuantity { get; set; }
    public decimal? ActualMeasuredWaste { get; set; }
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";
    public string ConformityDecision { get; set; } = "CONFORMS";
    public string? NonConformityAction { get; set; }
    public string? ClassifiedProductType { get; set; }
    public string? ReworkInstructions { get; set; }
    public string? RejectionReason { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? SpecificationResultsJson { get; set; }
    public string? QualityParametersJson { get; set; }
    public string? Observations { get; set; }
    public string? FinalTestResults { get; set; }
    public string? InspectorNotes { get; set; }
    public string? ApprovalStage { get; set; } // PHYSICAL | CHEMICAL
    public string? PhysicalApprovalStatus { get; set; }
    public string? PhysicalTestResults { get; set; }
    public string? PhysicalNotes { get; set; }
    public string? ChemicalApprovalStatus { get; set; }
    public string? ChemicalTestResults { get; set; }
    public string? ChemicalNotes { get; set; }
    [JsonIgnore]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class FinalConformityCertificateDto
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public int? RawMaterialReportId { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleCode { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string FinalProductName { get; set; } = string.Empty;
    public decimal ActualFinalProductQuantity { get; set; }
    public string QuantityUnit { get; set; } = "طن";
    public string ProductionBatchNumber { get; set; } = string.Empty;
    public int? ProductionWorkInProcessId { get; set; }
    public int? ProductionProcessId { get; set; }
    public string? ProductionProcessNumber { get; set; }
    public int? ProductionFinishedProductId { get; set; }
    public string? ManufactureNumber { get; set; }
    public int? ProductionOrderId { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public decimal TargetQuantity { get; set; }
    public string? AssociatedReferenceNumbers { get; set; }
    public DateTime ProductionCompletionDateTime { get; set; }
    public decimal ActualRawMaterialInputQuantity { get; set; }
    public decimal CalculatedDifference { get; set; }
    public decimal? ActualMeasuredWaste { get; set; }
    public string WasteClassification { get; set; } = string.Empty;
    public string ConformityDecision { get; set; } = string.Empty;
    public string ConformityDecisionArabic => ConformityDecision switch
    {
        "CONFORMS" or "APPROVED" => "مطابق للمواصفات القياسية",
        "NON_CONFORMING" or "REJECTED" => "غير مطابق للمواصفات القياسية",
        "PENDING_TESTING" or "UNDER_INSPECTION" => "قيد الفحص المخبري",
        _ => "قيد انتظار الفحص"
    };
    public string? NonConformityAction { get; set; }
    public string? ClassifiedProductType { get; set; }
    public string? ReworkInstructions { get; set; }
    public string? RejectionReason { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? SpecificationResultsJson { get; set; }
    public string? QualityParametersJson { get; set; }
    public string? Observations { get; set; }
    public string? FinalTestResults { get; set; }
    public string? InspectorNotes { get; set; }
    public string PhysicalApprovalStatus { get; set; } = "PENDING";
    public string? PhysicalApprovedBy { get; set; }
    public DateTime? PhysicalApprovedAt { get; set; }
    public string? PhysicalNotes { get; set; }
    public string? PhysicalTestResults { get; set; }
    public string ChemicalApprovalStatus { get; set; } = "PENDING";
    public string? ChemicalApprovedBy { get; set; }
    public DateTime? ChemicalApprovedAt { get; set; }
    public string? ChemicalNotes { get; set; }
    public string? ChemicalTestResults { get; set; }
    public string OverallStatus { get; set; } = "PENDING_PHYSICAL";
    public string OverallStatusArabic => OverallStatus switch
    {
        "APPROVED" => "معتمد ومطابق للمواصفات",
        "REJECTED" or "PHYSICAL_REJECTED" or "CHEMICAL_REJECTED" => "مرفوض مخبرياً",
        "PENDING_CHEMICAL" => "بانتظار التحليل الكيميائي",
        "PENDING_PHYSICAL" => "بانتظار الفحص الفيزيائي",
        _ => "قيد الفحص المخبري"
    };
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsStoragePosted { get; set; }
    public DateTime? StoragePostedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class EligibleFinishedProductForLabDto
{
    public int FinishedProductId { get; set; }
    public int Id => FinishedProductId;
    public int ProductionProcessId { get; set; }
    public string ProcessNumber { get; set; } = string.Empty;
    public int? ProductionOrderId { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public string FinishedProductName { get; set; } = string.Empty;
    public string ManufactureNumber { get; set; } = string.Empty;
    public decimal ManufacturedQuantity { get; set; }
    public decimal ActualQuantity => ManufacturedQuantity;
    public string Unit { get; set; } = "طن";
    public decimal TargetQuantity { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal WastePercentage { get; set; }
    public int? DestinationStorageLocationId { get; set; }
    public string? DestinationStorageLocationName { get; set; }
    public DateTime ProductionDate { get; set; }
    public string FormattedProductionDate => ProductionDate.ToString("yyyy-MM-dd HH:mm");
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string LaboratoryConformityStatus { get; set; } = "PENDING_INSPECTION";
    public string LaboratoryConformityStatusArabic => LaboratoryConformityStatus switch
    {
        "APPROVED" or "CONFORMS" => "معتمد مخبرياً – مطابق للمواصفات",
        "REJECTED" or "NON_CONFORMING" => "مرفوض مخبرياً – غير مطابق للمواصفات",
        "PENDING_CHEMICAL" => "بانتظار التحليل الكيميائي (مرحلة 2)",
        "PENDING_PHYSICAL" => "بانتظار الفحص الفيزيائي (مرحلة 1)",
        "UNDER_INSPECTION" or "PENDING_TESTING" => "قيد الفحص المخبري",
        _ => "قيد فحص المختبر"
    };
    public int? LaboratoryCertificateId { get; set; }
    public string? LaboratoryCertificateNumber { get; set; }
    public DateTime? LaboratoryCertifiedAt { get; set; }
    public string? LaboratoryCertifiedBy { get; set; }
    public string? LaboratoryRejectionReason { get; set; }
    public string? LaboratoryCorrectiveAction { get; set; }
    public string PhysicalApprovalStatus { get; set; } = "PENDING";
    public string? PhysicalApprovedBy { get; set; }
    public DateTime? PhysicalApprovedAt { get; set; }
    public string? PhysicalNotes { get; set; }
    public string? PhysicalTestResults { get; set; }
    public string ChemicalApprovalStatus { get; set; } = "PENDING";
    public string? ChemicalApprovedBy { get; set; }
    public DateTime? ChemicalApprovedAt { get; set; }
    public string? ChemicalNotes { get; set; }
    public string? ChemicalTestResults { get; set; }
    public string OverallStatus { get; set; } = "PENDING_PHYSICAL";
    public List<int> AssociatedReferenceNumbers { get; set; } = new();
    public List<string> AssociatedBatchNumbers { get; set; } = new();
    public List<EligibleFinishedProductMaterialDto> ConsumedMaterials { get; set; } = new();
}

public class EligibleFinishedProductMaterialDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string? MaterialClassification { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string SampleCode { get; set; } = string.Empty;
    public decimal ConsumedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string? StorageLocationName { get; set; }
}

public class ProductionReferenceTraceabilityDto
{
    public int ProductionReferenceNumber { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public RawMaterialReportDto? RawMaterialReport { get; set; }
    public List<StorageOperationDto> StorageImports { get; set; } = new();
    public List<ProductionWipBatchDto> ProductionBatches { get; set; } = new();
    public List<InProcessReportDto> InProcessReports { get; set; } = new();
    public FinalConformityCertificateDto? FinalCertificate { get; set; }
    public List<FormApprovalHistoryDto> AuditTrail { get; set; } = new();
    public TransportationSummaryDto? RelatedTransportation { get; set; }
    public PurchaseOrderSummaryDto? RelatedPurchaseOrder { get; set; }

    // Aliases for compatibility
    public List<StorageOperationDto> ConnectedStorageOperations => StorageImports;
    public List<ProductionWipBatchDto> ConnectedProductionBatches => ProductionBatches;
    public FinalConformityCertificateDto? FinalConformityCertificate => FinalCertificate;
    public List<FormApprovalHistoryDto> Approvals => AuditTrail;
}

public class ProductionWipBatchDto
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public decimal MaterialQuantityIssued { get; set; }
    public string SourceStorageLocation { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FinalProductName { get; set; }
    public decimal? FinalProductQuantity { get; set; }
    public decimal? WasteQuantity { get; set; }
    public string WasteClassification { get; set; } = string.Empty;
    public string? RecoverableProductName { get; set; }
    public decimal? RecoverableQuantity { get; set; }
}

public class TransportationSummaryDto
{
    public int Id { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
}

public class PurchaseOrderSummaryDto
{
    public int Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal PricePerTon { get; set; }
    public decimal TotalValue { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public class StringOrArrayConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64().ToString();
        }
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String) list.Add(reader.GetString() ?? "");
                else if (reader.TokenType == JsonTokenType.Number) list.Add(reader.GetInt64().ToString());
            }
            return string.Join(", ", list);
        }
        return string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
