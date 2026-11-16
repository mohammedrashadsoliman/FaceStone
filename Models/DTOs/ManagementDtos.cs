using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

// ========================================================================
// 1. Existing Legacy DTOs (Preserved for backward compatibility)
// ========================================================================

public class ManagementCarMovementDto
{
    public string CarNumber { get; set; } = string.Empty;
    public int TransactionNumber { get; set; }
    public decimal NetWeight { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FormattedMovement => $"{CarNumber} - حركة {TransactionNumber}";
}

public class ManagementRecordDto
{
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? PONumber { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public int NumberOfTransactions { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public string CarPlatesNumber { get; set; } = string.Empty;
    public List<ManagementCarMovementDto> CarMovements { get; set; } = new();
    public decimal AcceptedQuantity { get; set; }
    public decimal UnacceptedPendingQuantity { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ManagementSummaryDto
{
    public int TotalMovements { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalAcceptedQuantity { get; set; }
    public decimal TotalUnacceptedPendingQuantity { get; set; }
}

public class ManagementFilterOptionsDto
{
    public List<string> MaterialTypes { get; set; } = new();
    public List<string> ReceivingStatuses { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
}

public class ManagementPageResponseDto
{
    public List<ManagementRecordDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ManagementSummaryDto Summary { get; set; } = new();
    public ManagementFilterOptionsDto FilterOptions { get; set; } = new();
}

public class ManagementQueryParameters
{
    public string? Search { get; set; }
    public string? MaterialType { get; set; }
    public string? ReceivingStatus { get; set; }
    public string? Destination { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; } = "date";
    public string? SortOrder { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ========================================================================
// 2. Management Department Landing Page Overview (Cards Real-time Summary)
// ========================================================================

public class ManagementOverviewCardTransportationDto
{
    public int TripsCount { get; set; }
    public decimal TotalReceivedQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
}

public class ManagementOverviewCardStorageDto
{
    public decimal TotalDeposit { get; set; }
    public decimal TotalIssue { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class ManagementOverviewCardFinanceDto
{
    public decimal TotalExpenses { get; set; }
    public decimal TotalPoValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal PerformancePercentage { get; set; }
}

public class ManagementOverviewCardEmployeesDto
{
    public int TotalEmployees { get; set; }
    public int SalariedEmployeesCount { get; set; }
    public decimal TotalSalariesRecorded { get; set; }
    public decimal TotalRemainingLending { get; set; }
    public int ActiveLendingsCount { get; set; }
}

public class ManagementOverviewCardSuppliersDto
{
    public int TotalSuppliers { get; set; }
    public int TotalPos { get; set; }
    public decimal TotalRemainingQuantity { get; set; }
}

public class ManagementOverviewCardLaboratoryDto
{
    public int TotalLifecycles { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal FinalProductQuantity { get; set; }
}

public class ManagementOverviewCardProductionDto
{
    public int ActiveBatches { get; set; }
    public int HoldBatches { get; set; }
    public decimal TotalOutputQuantity { get; set; }
}

public class ManagementOverviewDto
{
    public ManagementOverviewCardTransportationDto Transportation { get; set; } = new();
    public ManagementOverviewCardStorageDto Storage { get; set; } = new();
    public ManagementOverviewCardFinanceDto Finance { get; set; } = new();
    public ManagementOverviewCardEmployeesDto Employees { get; set; } = new();
    public ManagementOverviewCardSuppliersDto Suppliers { get; set; } = new();
    public ManagementOverviewCardLaboratoryDto Laboratory { get; set; } = new();
    public ManagementOverviewCardProductionDto Production { get; set; } = new();
}

// ========================================================================
// 3. Global Search DTOs (Categorized Real-time Search)
// ========================================================================

public class ManagementSearchItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // purchaseOrders | transportation | storage | finance | employees | suppliers
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "عرض التفاصيل";
    public DateTime? Date { get; set; }
}

public class ManagementSearchResponseDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<ManagementSearchItemDto> PurchaseOrders { get; set; } = new();
    public List<ManagementSearchItemDto> Transportation { get; set; } = new();
    public List<ManagementSearchItemDto> Storage { get; set; } = new();
    public List<ManagementSearchItemDto> Finance { get; set; } = new();
    public List<ManagementSearchItemDto> Employees { get; set; } = new();
    public List<ManagementSearchItemDto> Suppliers { get; set; } = new();
}

// ========================================================================
// 4. Transportation Monitoring Overview DTOs
// ========================================================================

public class ManagementTransportationItemDto
{
    public int Id { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal WasteQuantity { get; set; }
    public string ReceivingStatus { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? InternalWeighbridgeCode { get; set; }
    public string? ExternalWeighbridgeCode { get; set; }
    public decimal InternalWeight { get; set; }
    public decimal ExternalNetWeight { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? ShippingDurationMinutes { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public bool HasAttachments { get; set; }
    public string? InvoiceFileName { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? InvoiceAttachmentPath { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "APPROVED";
    public string? RejectionReason { get; set; }
}

public class ManagementTransportationOverviewDto
{
    public int TotalTrips { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal TotalWaste { get; set; }
    public int CarsCount { get; set; }
    public int DriversCount { get; set; }
    public int CompletedShipmentsCount { get; set; }
    public int CurrentShipmentsCount { get; set; }
    public double? AvgShippingDurationMinutes { get; set; }
    public int? LastMovementNumber { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public List<ManagementTransportationItemDto> Items { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterPo { get; set; }
    public string? FilterDriver { get; set; }
    public string? FilterMaterial { get; set; }
    public string? FilterDestination { get; set; }
}

// ========================================================================
// 5. Storage Monitoring Overview DTOs
// ========================================================================

public class ManagementStorageMaterialBreakdownDto
{
    public string Material { get; set; } = string.Empty;
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public int TransactionsCount { get; set; }
}

public class ManagementStorageItemDto
{
    public int Id { get; set; }
    public int MovementNumber { get; set; }
    public int ReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Action { get; set; } = string.Empty; // Import / Export
    public string ServiceType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string? PONumber { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public decimal ClosingBalance { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string ReceivingEmployee { get; set; } = string.Empty;
    public bool IsExportRelatedToPO { get; set; }
    public string? InternalWeighbridgeReference { get; set; }
    public string? ExternalWeighbridgeReference { get; set; }
    public decimal InternalNetWeight { get; set; }
    public decimal? ExternalNetWeight { get; set; }
    public bool HasAttachments { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "APPROVED";
    public string? RejectionReason { get; set; }
}

public class ManagementStorageOverviewDto
{
    public decimal TotalDeposit { get; set; }
    public decimal TotalIssue { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal TotalWaste { get; set; }
    public int PoRelatedExportsCount { get; set; }
    public decimal PoRelatedExportsQuantity { get; set; }
    public int NonPoImportsCount { get; set; }
    public decimal NonPoImportsQuantity { get; set; }
    public int TransactionsCount { get; set; }
    public List<ManagementStorageMaterialBreakdownDto> MaterialBreakdown { get; set; } = new();
    public List<ManagementStorageItemDto> Items { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterMaterial { get; set; }
    public string? FilterAction { get; set; }
    public string? FilterPo { get; set; }
}

// ========================================================================
// 6. Finance Monitoring Overview DTOs
// ========================================================================

public class ManagementExpenseItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SpentBy { get; set; } = string.Empty;
}

public class ManagementBankTransactionItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Direction { get; set; } = string.Empty; // In / Out
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
}

public class ManagementFinancePoItemDto
{
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal TotalValue { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal WasteValue { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public decimal ProgressPercentage { get; set; }
}

public class ManagementFinanceOverviewDto
{
    public decimal TotalPoValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal WasteValue { get; set; }
    public decimal ImportedQuantity { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal BankBalance { get; set; }
    public Dictionary<string, int> PaymentStatusCounts { get; set; } = new();
    public decimal PerformancePercentage { get; set; }
    public List<ManagementFinancePoItemDto> Items { get; set; } = new();
    public List<ManagementExpenseItemDto> RecentExpenses { get; set; } = new();
    public List<ManagementBankTransactionItemDto> RecentBankTransactions { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterPo { get; set; }
    public string? FilterPaymentStatus { get; set; }
}

// ========================================================================
// 7. Employees Monitoring DTOs
// ========================================================================

public class ManagementEmployeeDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Rate { get; set; } = "التقييم غير مدرج حالياً";
    public bool HasRatingSystem { get; set; } = false;
    public decimal BasicSalary { get; set; }
    public decimal NetSalary { get; set; }
    public bool HasSalaryRecord { get; set; }
    public string SalaryStatusMessage { get; set; } = "غير مسجل بالنظام المالي";
    public decimal TotalLending { get; set; }
    public decimal PaidLending { get; set; }
    public decimal RemainingLending { get; set; }
    public DateTime? LastLendingDate { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal? AttendancePercentage { get; set; }
    public string FinancialStatus { get; set; } = "منتظم";
}

public class ManagementEmployeeDetailDto
{
    public ManagementEmployeeDto Employee { get; set; } = new();
    public List<EmployeeLendingDto> Lendings { get; set; } = new();
    public List<AttendanceRecordDto> RecentAttendance { get; set; } = new();
}

// ========================================================================
// 8. Suppliers Monitoring DTOs
// ========================================================================

public class ManagementSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public int PosCount { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal WastedQuantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal ExpectedValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string Materials { get; set; } = string.Empty;
    public int ActivePosCount { get; set; }
    public int CompletedPosCount { get; set; }
}

public class ManagementSupplierDetailDto
{
    public ManagementSupplierDto Summary { get; set; } = new();
    public List<ManagementFinancePoItemDto> PurchaseOrders { get; set; } = new();
    public List<ManagementTransportationItemDto> TransportationTransactions { get; set; } = new();
    public List<ManagementStorageItemDto> StorageTransactions { get; set; } = new();
}

// ========================================================================
// 9. Centralized Executive Monitoring & Cross-Department Oversight DTOs
// ========================================================================

public class ManagementDepartmentStatusDto
{
    public string DepartmentKey { get; set; } = string.Empty;
    public string DepartmentNameAr { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int TodayTransactions { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public string OperationalStatus { get; set; } = "NORMAL"; // NORMAL | WARNING | CRITICAL
    public string PrimaryMetric { get; set; } = string.Empty;
    public string PrimaryMetricLabel { get; set; } = string.Empty;
    public string SecondaryMetric { get; set; } = string.Empty;
    public string SecondaryMetricLabel { get; set; } = string.Empty;
    public DateTime? LastActivityAt { get; set; }
}

public class ManagementTodayProductionDto
{
    public int TodayProcessesCount { get; set; }
    public decimal TodayInputQuantity { get; set; }
    public decimal TodayOutputQuantity { get; set; }
    public decimal TodayWasteQuantity { get; set; }
    public decimal TodayWastePercentage { get; set; }
    public int ActiveProcessesCount { get; set; }
    public int ActiveOrdersCount { get; set; }
}

public class ManagementWarehouseOverviewDto
{
    public decimal TotalRawMaterialStock { get; set; }
    public int ActiveBatchesCount { get; set; }
    public int StorageLocationsCount { get; set; }
    public int LowStockItemsCount { get; set; }
    public decimal TodayDepositQuantity { get; set; }
    public decimal TodayIssueQuantity { get; set; }
}

public class ManagementTodayTransportationDto
{
    public int TodayTripsCount { get; set; }
    public decimal TodayReceivedQuantity { get; set; }
    public decimal TodayWasteQuantity { get; set; }
    public int ActiveCarsToday { get; set; }
    public int PendingApprovalsCount { get; set; }
}

public class ManagementAttendanceSummaryDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int UnmarkedToday { get; set; }
    public decimal AttendanceRateToday { get; set; }
    public decimal TotalWorkingHoursToday { get; set; }
    public DateTime Date { get; set; }
}

public class ManagementFinancialOverviewDto
{
    public decimal TotalExpenses { get; set; }
    public decimal TotalPoContractValue { get; set; }
    public decimal TotalDeliveredPoValue { get; set; }
    public decimal TotalRemainingPoValue { get; set; }
    public decimal ContractPerformancePercentage { get; set; }
    public int PendingFinancialApprovals { get; set; }
    public int ActivePosCount { get; set; }
}

public class ManagementExecutiveDashboardDto
{
    public int TotalCompanyTransactions { get; set; }
    public int TotalPendingApprovals { get; set; }
    public int TotalApprovedTransactions { get; set; }
    public int TotalRejectedTransactions { get; set; }
    public int ActiveAlertsCount { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public ManagementTodayProductionDto TodayProduction { get; set; } = new();
    public ManagementWarehouseOverviewDto WarehouseInventory { get; set; } = new();
    public ManagementTodayTransportationDto TodayTransportation { get; set; } = new();
    public ManagementAttendanceSummaryDto AttendanceSummary { get; set; } = new();
    public ManagementFinancialOverviewDto FinancialOverview { get; set; } = new();

    public List<ManagementDepartmentStatusDto> DepartmentStatuses { get; set; } = new();
    public List<ManagementAlertDto> TopAlerts { get; set; } = new();
    public List<ManagementActivityItemDto> RecentActivities { get; set; } = new();
}

public class ManagementActivityItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DepartmentAr { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionAr { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "COMPLETED"; // PENDING | APPROVED | REJECTED | COMPLETED | IN_PROGRESS
    public string StatusAr { get; set; } = "مكتمل";
    public decimal? AmountOrQuantity { get; set; }
    public string? Unit { get; set; }
}

public class ManagementAlertDto
{
    public string Id { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DepartmentAr { get; set; } = string.Empty;
    public string Severity { get; set; } = "WARNING"; // CRITICAL | WARNING | INFO
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ========================================================================
// 10. Laboratory Monitoring DTOs
// ========================================================================

public class ManagementLabReportItemDto
{
    public int Id { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public int SampleNumber { get; set; }
    public string LaboratorySection { get; set; } = string.Empty; // PHYSICAL | CHEMICAL
    public string SampleName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string ComplyStatus { get; set; } = "COMPLY";
    public string InspectionDecision { get; set; } = "ACCEPTED";
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? RejectionReason { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime ReportIssueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManagementLabCertificateItemDto
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public string FinalProductName { get; set; } = string.Empty;
    public decimal ActualFinalProductQuantity { get; set; }
    public decimal ActualRawMaterialInputQuantity { get; set; }
    public decimal CalculatedDifference { get; set; }
    public string ConformityDecision { get; set; } = "CONFORMING";
    public string ApprovalStatus { get; set; } = "PENDING";
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime ProductionCompletionDateTime { get; set; }
}

public class ManagementLaboratoryOverviewDto
{
    public int TotalRawMaterialReports { get; set; }
    public int AcceptedRawMaterialReports { get; set; }
    public int RejectedRawMaterialReports { get; set; }
    public int PendingRawMaterialReports { get; set; }
    public decimal ComplianceRate { get; set; }
    public int PhysicalSectionReportsCount { get; set; }
    public int ChemicalSectionReportsCount { get; set; }
    public int TotalInProcessReports { get; set; }
    public int TotalFinalCertificates { get; set; }
    public decimal TotalRawMaterialAcceptedQuantity { get; set; }
    public decimal TotalRawMaterialRejectedQuantity { get; set; }
    public List<ManagementLabReportItemDto> RecentReports { get; set; } = new();
    public List<ManagementLabCertificateItemDto> RecentCertificates { get; set; } = new();
}

// ========================================================================
// 11. Production Monitoring DTOs
// ========================================================================

public class ManagementProductionOrderItemDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal TargetQuantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal TotalPlannedRawMaterialQuantity { get; set; }
    public decimal TotalTransferredQuantity { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string Status { get; set; } = "IN_PROGRESS";
    public DateTime TargetStartDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
}

public class ManagementProductionProcessItemDto
{
    public int Id { get; set; }
    public string ProcessNumber { get; set; } = string.Empty;
    public string? ProductionOrderNumber { get; set; }
    public DateTime ProductionDate { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal WastePercentage { get; set; }
    public string Unit { get; set; } = "طن";
    public string Status { get; set; } = "COMPLETED";
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ManagementProductionOverviewDto
{
    public int TotalProductionOrders { get; set; }
    public int ActiveProductionOrders { get; set; }
    public int CompletedProductionOrders { get; set; }
    public int TotalProcessesCount { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal OverallWastePercentage { get; set; }
    public int FinishedProductsCount { get; set; }
    public decimal FinishedProductsQuantity { get; set; }
    public List<ManagementProductionOrderItemDto> RecentOrders { get; set; } = new();
    public List<ManagementProductionProcessItemDto> RecentProcesses { get; set; } = new();
}

// ========================================================================
// 12. Attendance Monitoring DTOs
// ========================================================================

public class ManagementAttendanceEmployeeItemDto
{
    public Guid UserId { get; set; }
    public int? RecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Status { get; set; } = "Unmarked"; // Present | Absent | Unmarked
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal? WorkingHours { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "APPROVED";
}

public class ManagementAttendanceOverviewDto
{
    public DateTime Date { get; set; }
    public int TotalEmployees { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int UnmarkedCount { get; set; }
    public decimal AttendanceRate { get; set; }
    public decimal TotalWorkingHours { get; set; }
    public string SubmissionStatus { get; set; } = "DRAFT";
    public List<ManagementAttendanceEmployeeItemDto> Employees { get; set; } = new();
}

