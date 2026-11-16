using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Models;

namespace FaceStone.Web.Data;

public class FaceStoneDbContext : DbContext
{
    public FaceStoneDbContext(DbContextOptions<FaceStoneDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
    public DbSet<LiveLocationSession> LiveLocationSessions => Set<LiveLocationSession>();
    public DbSet<Transportation> Transportations => Set<Transportation>();
    public DbSet<Management> Management => Set<Management>();
    public DbSet<StorageTransaction> StorageTransactions => Set<StorageTransaction>();
    public DbSet<StorageTransactionMovement> StorageTransactionMovements => Set<StorageTransactionMovement>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<StorageItemTransaction> StorageItemTransactions => Set<StorageItemTransaction>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<FinancePayment> FinancePayments => Set<FinancePayment>();
    public DbSet<PurchaseOrderPaymentAudit> PurchaseOrderPaymentAudits => Set<PurchaseOrderPaymentAudit>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<EmployeeFinancialRecord> EmployeeFinancialRecords => Set<EmployeeFinancialRecord>();
    public DbSet<EmployeeLending> EmployeeLendings => Set<EmployeeLending>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceSubmission> AttendanceSubmissions => Set<AttendanceSubmission>();
    public DbSet<AttendanceAttachment> AttendanceAttachments => Set<AttendanceAttachment>();
    public DbSet<FormApprovalHistory> FormApprovalHistories => Set<FormApprovalHistory>();
    public DbSet<ProjectCode> ProjectCodes => Set<ProjectCode>();

    // New 3-Card Storage Department Redesign Entities
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<StorageClient> StorageClients => Set<StorageClient>();
    public DbSet<StorageClientDestination> StorageClientDestinations => Set<StorageClientDestination>();
    public DbSet<StorageSequence> StorageSequences => Set<StorageSequence>();
    public DbSet<StorageOperation> StorageOperations => Set<StorageOperation>();
    public DbSet<StorageOperationItem> StorageOperationItems => Set<StorageOperationItem>();
    public DbSet<StorageInventoryLedger> StorageInventoryLedger => Set<StorageInventoryLedger>();
    public DbSet<WarehouseBatch> WarehouseBatches => Set<WarehouseBatch>();
    public DbSet<ProductionWorkInProcess> ProductionWorkInProcess => Set<ProductionWorkInProcess>();

    // Laboratory Department Entities
    public DbSet<LaboratoryProductionSequence> LaboratoryProductionSequences => Set<LaboratoryProductionSequence>();
    public DbSet<LaboratoryUserSampleSequence> LaboratoryUserSampleSequences => Set<LaboratoryUserSampleSequence>();
    public DbSet<LaboratoryRawMaterialReport> LaboratoryRawMaterialReports => Set<LaboratoryRawMaterialReport>();
    public DbSet<LaboratoryInProcessReport> LaboratoryInProcessReports => Set<LaboratoryInProcessReport>();
    public DbSet<LaboratoryFinalConformityCertificate> LaboratoryFinalConformityCertificates => Set<LaboratoryFinalConformityCertificate>();

    // Production Department Entities
    public DbSet<ProductionSequence> ProductionSequences => Set<ProductionSequence>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderItem> ProductionOrderItems => Set<ProductionOrderItem>();
    public DbSet<ProductionOrderTransfer> ProductionOrderTransfers => Set<ProductionOrderTransfer>();
    public DbSet<ProductionOrderFormulaVersion> ProductionOrderFormulaVersions => Set<ProductionOrderFormulaVersion>();
    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();
    public DbSet<ProductionOutput> ProductionOutputs => Set<ProductionOutput>();
    public DbSet<ProductionRework> ProductionReworks => Set<ProductionRework>();
    public DbSet<DailyProductionSubmission> DailyProductionSubmissions => Set<DailyProductionSubmission>();
    public DbSet<DailyProductionDetail> DailyProductionDetails => Set<DailyProductionDetail>();
    public DbSet<ProductionProcess> ProductionProcesses => Set<ProductionProcess>();
    public DbSet<ProductionProcessMaterial> ProductionProcessMaterials => Set<ProductionProcessMaterial>();
    public DbSet<ProductionFinishedProduct> ProductionFinishedProducts => Set<ProductionFinishedProduct>();
    public DbSet<ProductionAttachment> ProductionAttachments => Set<ProductionAttachment>();
    public DbSet<ProductionWasteRecord> ProductionWasteRecords => Set<ProductionWasteRecord>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierFinancialTransaction> SupplierFinancialTransactions => Set<SupplierFinancialTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.FullName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Username)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Email)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.CountryCode)
                  .HasMaxLength(10)
                  .HasDefaultValue("+20");

            entity.Property(e => e.PhoneNumber)
                  .HasMaxLength(30)
                  .IsRequired();

            entity.Property(e => e.PasswordHash)
                  .HasMaxLength(256)
                  .IsRequired();

            entity.Property(e => e.Role)
                  .HasMaxLength(50)
                  .HasDefaultValue("Employee");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.SenderUsername)
                  .HasMaxLength(50);

            entity.Property(e => e.SenderRole)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.RecipientUsername)
                  .HasMaxLength(50);

            entity.Property(e => e.RecipientRole)
                  .HasMaxLength(50);

            entity.Property(e => e.MessageText)
                  .IsUnicode(true); // Plain text, unencrypted

            entity.Property(e => e.ClientMessageId)
                  .HasMaxLength(64);

            entity.HasIndex(e => new { e.SenderId, e.ClientMessageId });
            entity.HasIndex(e => new { e.SenderId, e.SentAt });
            entity.HasIndex(e => new { e.RecipientId, e.SentAt });
            entity.HasIndex(e => new { e.SenderId, e.IsFromAccountant, e.IsReadByAccountant });
            entity.HasIndex(e => new { e.RecipientId, e.IsFromAccountant, e.IsReadByUser });
            entity.HasIndex(e => e.LiveSessionId);

            entity.Property(e => e.SentAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.MessageType)
                  .HasMaxLength(50)
                  .HasDefaultValue("Text");

            entity.Property(e => e.Latitude);
            entity.Property(e => e.Longitude);
            entity.Property(e => e.LiveSessionId);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.Message)
                  .HasForeignKey(a => a.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiveLocationSession>(entity =>
        {
            entity.ToTable("LiveLocationSessions");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsActive });

            entity.Property(e => e.StartedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.ToTable("ChatAttachments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.FileName)
                  .HasMaxLength(260)
                  .IsRequired();

            entity.Property(e => e.FilePath)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.FileType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.UploadedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.ToTable("Transportation");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.TransactionNumber)
                  .IsRequired();

            entity.HasOne(e => e.ProjectCode)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectCodeId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.ProjectCodeValue)
                  .HasMaxLength(50);

            entity.HasIndex(e => new { e.PONumber, e.TransactionNumber })
                  .IsUnique();

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.CarNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.DriverName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Source)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.MaterialType)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.WeightBeforeLoading)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.WeightWhenEnteringFactory)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.WeightWhenExitingFactory)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.ExpectedWeight)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.NetWeight)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.CompanyName)
                  .HasMaxLength(200)
                  .HasDefaultValue(string.Empty)
                  .IsRequired();

            entity.Property(e => e.InternalWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalNetWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.WasteQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.DriverPhone)
                  .HasMaxLength(30)
                  .IsRequired(false);

            entity.Property(e => e.DepartureTime)
                  .IsRequired(false);

            entity.Property(e => e.ArrivalTime)
                  .IsRequired(false);

            entity.Property(e => e.ShippingSupervisor)
                  .HasMaxLength(100)
                  .HasDefaultValue("محمد ناجح")
                  .IsRequired();

            entity.Property(e => e.DepartmentSupervisor)
                  .HasMaxLength(100)
                  .HasDefaultValue("ازهر محمد")
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.RecipientTitle)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ReceivedDate)
                  .IsRequired(false);

            entity.Property(e => e.InternalWeighbridgeCode)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.InternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.InternalLoadedWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalWeighbridgeCode)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ShippingDurationMinutes)
                  .IsRequired(false);

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.TransactionStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ReceivingStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.Ikramiya)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.CustodyAmount)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InvoiceAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 1: Weighbridge Attachment
            entity.Property(e => e.WeighbridgeAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 2: Daily Supply Attachment
            entity.Property(e => e.DailySupplyAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 3: Bill of Lading Attachment
            entity.Property(e => e.BillOfLadingAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            entity.Property(e => e.SubmittedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.TransportationTransactions)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.StorageOperationItem)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationItemId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.DateOfSubmit)
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("PENDING")
                  .IsRequired();

            entity.Property(e => e.ReviewedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedByUserId)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedAt)
                  .IsRequired(false);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(1000)
                  .IsRequired(false);
        });

        modelBuilder.Entity<ProjectCode>(entity =>
        {
            entity.ToTable("ProjectCodes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProjectName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ClientName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.StorageClientDestination)
                  .WithMany()
                  .HasForeignKey(e => e.StorageClientDestinationId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Management>(entity =>
        {
            entity.ToTable("Management");

            entity.HasKey(e => e.TransactionNumber);

            entity.Property(e => e.TransactionNumber)
                  .HasMaxLength(100)
                  .ValueGeneratedNever()
                  .IsRequired();

            entity.Property(e => e.Date)
                  .IsRequired();

            entity.Property(e => e.PO)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.MaterialType)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.NumberOfTransactions)
                  .IsRequired();

            entity.Property(e => e.TotalReceivedAmount)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.CarPlatesNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.AcceptedQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.UnacceptedPendingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.DeliveryStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<StorageTransaction>(entity =>
        {
            entity.ToTable("StorageTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MovementNumber)
                  .IsRequired();

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.Material)
                  .HasMaxLength(100)
                  .HasDefaultValue("powder")
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasMaxLength(50)
                  .HasDefaultValue("Import")
                  .IsRequired();

            entity.Property(e => e.ServiceType)
                  .HasMaxLength(50)
                  .HasDefaultValue("إيداع")
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .IsRequired();

            entity.HasIndex(e => e.ReferenceNumber);

            entity.Property(e => e.IsExportRelatedToPO)
                  .HasDefaultValue(false)
                  .IsRequired();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PurchaseOrderId)
                  .IsRequired(false);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.StorageTransactions)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.IncomingQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.OutgoingQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.OpeningBalance)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ClosingBalance)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.StorageLocation)
                  .HasMaxLength(200)
                  .HasDefaultValue("مستودع الخامات الرئيسي")
                  .IsRequired();

            entity.Property(e => e.DestinationStorageLocation)
                  .HasMaxLength(200)
                  .IsRequired(false);

            // Section 3: الميزان الداخلي
            entity.Property(e => e.InternalWeighbridgeReference)
                  .HasMaxLength(100)
                  .HasDefaultValue(string.Empty)
                  .IsRequired();

            entity.Property(e => e.InternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalNetWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.InternalLoadedWeightTime)
                  .IsRequired(false);

            // Section 3: الميزان الخارجي
            entity.Property(e => e.HasExternalWeighbridge)
                  .HasDefaultValue(false)
                  .IsRequired();

            entity.Property(e => e.ExternalWeighbridgeReference)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalNetWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeightTime)
                  .IsRequired(false);

            // Section 3: إيصال الميزان
            entity.Property(e => e.WeighbridgeAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 4: الموظف المسؤول
            entity.Property(e => e.ReceivingEmployee)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.CreatedByUserId)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("PENDING")
                  .IsRequired();

            entity.Property(e => e.ReviewedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedByUserId)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedAt)
                  .IsRequired(false);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.HasMany(e => e.TransportationMovements)
                  .WithOne(m => m.StorageTransaction)
                  .HasForeignKey(m => m.StorageTransactionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.MovementNumber);
            entity.HasIndex(e => e.StorageLocation);
            entity.HasIndex(e => e.Material);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.PONumber);
        });

        modelBuilder.Entity<StorageTransactionMovement>(entity =>
        {
            entity.ToTable("StorageTransactionMovements");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransportationMovementNumber)
                  .IsRequired();

            entity.HasIndex(e => e.StorageTransactionId);
            entity.HasIndex(e => e.TransportationMovementNumber);
        });

        modelBuilder.Entity<RawMaterial>(entity =>
        {
            entity.ToTable("RawMaterials");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.HasIndex(e => e.Name)
                  .IsUnique();

            entity.Property(e => e.InitialQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.MaterialType)
                  .HasMaxLength(50)
                  .HasDefaultValue("MECHANICAL")
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150);

            entity.HasMany(e => e.Transactions)
                  .WithOne(t => t.RawMaterial)
                  .HasForeignKey(t => t.RawMaterialId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StorageItemTransaction>(entity =>
        {
            entity.ToTable("StorageItemTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.TransactionType)
                  .HasMaxLength(50)
                  .HasDefaultValue("Incoming")
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Quantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.UnitPrice)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.TotalValue)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.DestinationType)
                  .HasMaxLength(50);

            entity.Property(e => e.Destination)
                  .HasMaxLength(200);

            entity.Property(e => e.RunningQuantityBalance)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.RunningValueBalance)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(500);

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.SourceTransportation)
                  .WithMany()
                  .HasForeignKey(e => e.SourceTransportationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.SourceTransportationId)
                  .IsUnique()
                  .HasFilter("[SourceTransportationId] IS NOT NULL");

            entity.HasIndex(e => e.RawMaterialId);
            entity.HasIndex(e => e.TransactionDate);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasIndex(e => e.PONumber)
                  .IsUnique();

            entity.Property(e => e.Supplier)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.OrderDate)
                  .IsRequired();

            entity.Property(e => e.Quantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.PricePerTon)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.TonPrice)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.TotalValue)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.InvoiceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .HasDefaultValue("Cairo Factory")
                  .IsRequired();

            entity.Property(e => e.RemainingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.DeliveredQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m);

            entity.Property(e => e.PaymentStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("غير مدفوع")
                  .IsRequired();

            entity.Property(e => e.PaymentStatusUpdatedAt)
                  .IsRequired(false);

            entity.Property(e => e.PaymentStatusUpdatedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.PurchaseOrder)
                  .HasForeignKey(p => p.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.PaymentAudits)
                  .WithOne(a => a.PurchaseOrder)
                  .HasForeignKey(a => a.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.Supplier);
            entity.HasIndex(e => e.InvoiceNumber);
            entity.HasIndex(e => e.PaymentStatus);
        });

        modelBuilder.Entity<FinancePayment>(entity =>
        {
            entity.ToTable("FinancePayments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.PaymentDate)
                  .IsRequired();

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer");

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PaidBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.PONumber);
            entity.HasIndex(e => e.PaymentDate);
        });

        modelBuilder.Entity<PurchaseOrderPaymentAudit>(entity =>
        {
            entity.ToTable("PurchaseOrderPaymentAudits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.PreviousStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.NewStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ChangedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.ChangedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.PONumber);
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.HasIndex(e => e.ChangedAt);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.ToTable("BankTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Direction)
                  .HasMaxLength(20)
                  .HasDefaultValue("In")
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.BankName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Description)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.BankName);
            entity.HasIndex(e => e.Direction);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Category)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.Title)
                  .HasMaxLength(300)
                  .IsRequired();

            entity.Property(e => e.Beneficiary)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer")
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ExpenseDate);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<EmployeeFinancialRecord>(entity =>
        {
            entity.ToTable("EmployeeFinancialRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BasicSalary)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.Allowances)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.InsuranceDeductions)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.NetSalary)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer");

            entity.Property(e => e.BankAccountNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<EmployeeLending>(entity =>
        {
            entity.ToTable("EmployeeLendings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LendingAmount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.PaidAmount)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.RemainingAmount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("معتمد")
                  .IsRequired();

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RequestDate);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .HasDefaultValue("Present")
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("PENDING");

            entity.Property(e => e.EmployeeName)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.EmployeeRole)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.IsTestData)
                  .HasDefaultValue(false);

            entity.Property(e => e.WorkingHours)
                  .HasPrecision(5, 2)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedByUserId)
                  .IsRequired(false);

            entity.Property(e => e.ReviewedAt)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AttendanceSubmission)
                  .WithMany(s => s.Records)
                  .HasForeignKey(e => e.AttendanceSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.AttendanceSubmissionId);
        });

        modelBuilder.Entity<AttendanceSubmission>(entity =>
        {
            entity.ToTable("AttendanceSubmissions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SubmittedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("PENDING")
                  .IsRequired();

            entity.Property(e => e.ReviewedBy)
                  .HasMaxLength(150);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(1000);

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000);

            entity.Property(e => e.SubmittedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Records)
                  .WithOne(r => r.AttendanceSubmission)
                  .HasForeignKey(r => r.AttendanceSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.AttendanceSubmission)
                  .HasForeignKey(a => a.AttendanceSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AttendanceDate);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.SubmittedAt);
        });

        modelBuilder.Entity<AttendanceAttachment>(entity =>
        {
            entity.ToTable("AttendanceAttachments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileName)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.OriginalFileName)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.FilePath)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.FileType)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UploadedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.AttendanceSubmissionId);
        });

        modelBuilder.Entity<FormApprovalHistory>(entity =>
        {
            entity.ToTable("FormApprovalHistories");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FormType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.FormId)
                  .IsRequired();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.TransactionOrMovementNumber)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.PreviousStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.NewStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ActorName)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.ActorRole)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.Comment)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.Timestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.FormType, e.FormId });
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.ToTable("StorageLocations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<StorageClient>(entity =>
        {
            entity.ToTable("StorageClients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.PhoneNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasMany(e => e.Destinations)
                  .WithOne(d => d.StorageClient)
                  .HasForeignKey(d => d.StorageClientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StorageClientDestination>(entity =>
        {
            entity.ToTable("StorageClientDestinations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DestinationName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.StorageClientId);
        });

        modelBuilder.Entity<StorageSequence>(entity =>
        {
            entity.ToTable("StorageSequences");
            entity.HasKey(e => e.SequenceType);
            entity.Property(e => e.SequenceType).HasMaxLength(50);
        });

        modelBuilder.Entity<StorageOperation>(entity =>
        {
            entity.ToTable("StorageOperations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OperationType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TransactionNumber).IsRequired();
            entity.HasIndex(e => new { e.OperationType, e.TransactionNumber }).IsUnique();
            entity.Property(e => e.StorageLocationName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Client)
                  .WithMany()
                  .HasForeignKey(e => e.ClientId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Destination)
                  .WithMany()
                  .HasForeignKey(e => e.DestinationId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.StorageOperation)
                  .HasForeignKey(i => i.StorageOperationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.LedgerMovements)
                  .WithOne(l => l.StorageOperation)
                  .HasForeignKey(l => l.StorageOperationId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.MaterialType).HasMaxLength(50);
            entity.Property(e => e.ExternalReportedWeight).HasPrecision(18, 3);
            entity.Property(e => e.InternalLoadedWeight).HasPrecision(18, 3);
            entity.Property(e => e.InternalEmptyWeight).HasPrecision(18, 3);
            entity.Property(e => e.InternalNetWeight).HasPrecision(18, 3);
            entity.Property(e => e.WeightDifference).HasPrecision(18, 3);
            entity.Property(e => e.WeightDifferencePercentage).HasPrecision(18, 2);
            entity.Property(e => e.WeighbridgeReference).HasMaxLength(100);
            entity.Property(e => e.WeighingResponsibleEmployee).HasMaxLength(150);

            entity.HasOne(e => e.Transportation)
                  .WithMany()
                  .HasForeignKey(e => e.TransportationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.ProductionBatches)
                  .WithOne(p => p.StorageOperation)
                  .HasForeignKey(p => p.StorageOperationId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.SampleCode).HasMaxLength(50);
            entity.Property(e => e.SelectionMode).HasMaxLength(50);
            entity.HasIndex(e => e.SampleCode);
            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.StorageLocationId);
        });

        modelBuilder.Entity<WarehouseBatch>(entity =>
        {
            entity.ToTable("WarehouseBatches");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.BatchNumber).IsUnique();
            entity.HasIndex(e => e.SampleCode);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.LaboratoryReportId);
            entity.HasIndex(e => e.StorageLocationId);
            entity.HasIndex(e => e.RawMaterialId);
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.BatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SampleCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MaterialName).HasMaxLength(250).IsRequired();
            entity.Property(e => e.MaterialClassification).HasMaxLength(50).HasDefaultValue("MECHANICAL").IsRequired();
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.InitialQuantity).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.CurrentQuantity).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.LaboratoryReport)
                  .WithMany(r => r.Batches)
                  .HasForeignKey(e => e.LaboratoryReportId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany(o => o.CreatedBatches)
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageOperationItem)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StorageOperationItem>(entity =>
        {
            entity.ToTable("StorageOperationItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MaterialName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.TrackingNumber).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.SampleCode).HasMaxLength(50);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany(b => b.OperationItems)
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.StorageOperationId);
            entity.HasIndex(e => e.RawMaterialId);
            entity.HasIndex(e => e.WarehouseBatchId);
            entity.HasIndex(e => e.BatchNumber);
        });

        modelBuilder.Entity<StorageInventoryLedger>(entity =>
        {
            entity.ToTable("StorageInventoryLedger");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MovementType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.RunningBalance).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany(b => b.LedgerMovements)
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany(o => o.LedgerMovements)
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.StorageOperationItem)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.StorageLocationId, e.RawMaterialId });
            entity.HasIndex(e => e.WarehouseBatchId);
            entity.HasIndex(e => e.MovementDate);
        });

        modelBuilder.Entity<ProductionWorkInProcess>(entity =>
        {
            entity.ToTable("ProductionWorkInProcess");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrackingNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.TrackingNumber).IsUnique();
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.SampleCode).HasMaxLength(50);
            entity.Property(e => e.MaterialQuantityIssued).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.FinalProductQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WasteQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WastePercentage).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("قيد التشغيل").IsRequired();
            entity.Property(e => e.SourceStorageLocation).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.StorageOperation)
                  .WithMany(o => o.ProductionBatches)
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.StorageOperationItem)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany(b => b.ProductionWipTransfers)
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.WarehouseBatchId);

            entity.Property(e => e.WasteClassification).HasMaxLength(50).HasDefaultValue("ORDINARY_WASTE").IsRequired();
            entity.Property(e => e.RecoverableProductName).HasMaxLength(200);
            entity.Property(e => e.RecoverableQuantity).HasPrecision(18, 3);
            entity.Property(e => e.RecoverableUnit).HasMaxLength(50).HasDefaultValue("طن");
            entity.Property(e => e.DestinationStorageLocationName).HasMaxLength(200);
            entity.Property(e => e.OutputResponsibleEmployee).HasMaxLength(150);

            entity.HasOne(e => e.RecoverableMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RecoverableMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DestinationStorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.DestinationStorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.StorageOperationId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ProductionReferenceNumber);
        });

        modelBuilder.Entity<LaboratoryProductionSequence>(entity =>
        {
            entity.ToTable("LaboratoryProductionSequences");
            entity.HasKey(e => e.SequenceName);
            entity.Property(e => e.SequenceName).HasMaxLength(50);
            entity.Property(e => e.NextValue).HasDefaultValue(1);
        });

        modelBuilder.Entity<LaboratoryUserSampleSequence>(entity =>
        {
            entity.ToTable("LaboratoryUserSampleSequences");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.NextSampleNumber).HasDefaultValue(1);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<LaboratoryRawMaterialReport>(entity =>
        {
            entity.ToTable("LaboratoryRawMaterialReports");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.SampleCode)
                  .IsUnique()
                  .HasFilter("[SampleCode] IS NOT NULL");
            entity.HasIndex(e => new { e.ResponsibleUserId, e.SampleNumber })
                  .IsUnique()
                  .HasFilter("[ResponsibleUserId] IS NOT NULL AND [SampleNumber] > 0");
            entity.HasIndex(e => e.SampleNumber);
            entity.HasIndex(e => e.MaterialClassification);
            entity.HasIndex(e => e.BatchNumber);
            entity.HasIndex(e => e.ComplyStatus);
            entity.HasIndex(e => e.LaboratorySection);
            entity.HasIndex(e => e.AnalysisDateTime);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.InspectionDecision);
            entity.HasIndex(e => e.SourceTransportationId);
            entity.HasIndex(e => e.PurchaseOrderId);

            entity.Property(e => e.SampleName).HasMaxLength(250).HasDefaultValue("سيليكا باودر (Micro Silica Powder)").IsRequired();
            entity.Property(e => e.SampleCode).HasMaxLength(50);
            entity.Property(e => e.MaterialClassification).HasMaxLength(50).HasDefaultValue("MECHANICAL").IsRequired();
            entity.Property(e => e.SampleDescription).HasMaxLength(500);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ComplyStatus).HasMaxLength(50).HasDefaultValue("COMPLY").IsRequired();
            entity.Property(e => e.LabManagerName).HasMaxLength(150);
            entity.Property(e => e.LabManagerSignature).HasMaxLength(150);
            entity.Property(e => e.LaboratorySection).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RawMaterialDescription).HasMaxLength(250).IsRequired();
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.PONumber).HasMaxLength(100);
            entity.Property(e => e.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TestedMaterialWeight).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(e => e.QuantityConfirmationPath).HasMaxLength(500);
            entity.Property(e => e.QuantityConfirmationFileName).HasMaxLength(255);
            entity.Property(e => e.QuantityConfirmationFileType).HasMaxLength(100);
            entity.Property(e => e.QuantityConfirmationFileSize).HasDefaultValue(0L);
            entity.Property(e => e.AttachmentFileSize).HasDefaultValue(0L);
            entity.Property(e => e.AcceptedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.RejectedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.InspectionDecision).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.ReviewedBy).HasMaxLength(150);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SourceTransportation)
                  .WithMany()
                  .HasForeignKey(e => e.SourceTransportationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LaboratoryInProcessReport>(entity =>
        {
            entity.ToTable("LaboratoryInProcessReports");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.RawMaterialReportId);
            entity.HasIndex(e => e.ProductionWorkInProcessId);
            entity.HasIndex(e => e.LaboratorySection);
            entity.HasIndex(e => e.TestDateTime);
            entity.HasIndex(e => e.ProductionStage);

            entity.Property(e => e.LaboratorySection).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SampleCode).HasMaxLength(50);
            entity.Property(e => e.ProductionBatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProductionStage).HasMaxLength(100).IsRequired();
            entity.Property(e => e.InspectionDecision).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.RawMaterialReport)
                  .WithMany(r => r.InProcessReports)
                  .HasForeignKey(e => e.RawMaterialReportId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionWorkInProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionWorkInProcessId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LaboratoryFinalConformityCertificate>(entity =>
        {
            entity.ToTable("LaboratoryFinalConformityCertificates");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.CertificateNumber).IsUnique();
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.RawMaterialReportId);
            entity.HasIndex(e => e.ProductionWorkInProcessId);
            entity.HasIndex(e => e.LaboratorySection);
            entity.HasIndex(e => e.ConformityDecision);
            entity.HasIndex(e => e.ApprovalStatus);

            entity.Property(e => e.CertificateNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SampleCode).HasMaxLength(50);
            entity.Property(e => e.LaboratorySection).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FinalProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.QuantityUnit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.ActualFinalProductQuantity).HasPrecision(18, 3);
            entity.Property(e => e.ActualRawMaterialInputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.CalculatedDifference).HasPrecision(18, 3);
            entity.Property(e => e.ActualMeasuredWaste).HasPrecision(18, 3);
            entity.Property(e => e.WasteClassification).HasMaxLength(50).HasDefaultValue("ORDINARY_WASTE").IsRequired();
            entity.Property(e => e.ConformityDecision).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NonConformityAction).HasMaxLength(50);
            entity.Property(e => e.ClassifiedProductType).HasMaxLength(100);
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.ReviewedBy).HasMaxLength(150);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.RawMaterialReport)
                  .WithMany(r => r.FinalCertificates)
                  .HasForeignKey(e => e.RawMaterialReportId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductionWorkInProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionWorkInProcessId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StorageOperation>(entity =>
        {
            entity.HasIndex(e => e.ProductionReferenceNumber);
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.HasIndex(e => e.ProductionReferenceNumber);
        });

        // Production Department Entities Configuration
        modelBuilder.Entity<ProductionSequence>(entity =>
        {
            entity.ToTable("ProductionSequences");
            entity.HasKey(e => e.SequenceType);
            entity.Property(e => e.SequenceType).HasMaxLength(50);
            entity.Property(e => e.NextValue).HasDefaultValue(1);
        });

        modelBuilder.Entity<ProductionOrder>(entity =>
        {
            entity.ToTable("ProductionOrders");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StorageLocationId);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ProductDescription).HasMaxLength(1000);
            entity.Property(e => e.LaboratoryManagerName).HasMaxLength(255);
            entity.Property(e => e.ProductionInstructions);
            entity.Property(e => e.LinkedProductionProcessNumber).HasMaxLength(100);
            entity.Property(e => e.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalPlannedRawMaterialQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalTransferredQuantity).HasPrecision(18, 3);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.StorageLocationName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("DRAFT").IsRequired();
            entity.Property(e => e.CurrentStage).HasMaxLength(100).HasDefaultValue("الخلط الأولي").IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Transfers)
                  .WithOne(t => t.ProductionOrder)
                  .HasForeignKey(t => t.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.FormulaVersions)
                  .WithOne(f => f.ProductionOrder)
                  .HasForeignKey(f => f.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionOrderItem>(entity =>
        {
            entity.ToTable("ProductionOrderItems");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => e.RawMaterialId);
            entity.HasIndex(e => e.WarehouseBatchId);
            entity.HasIndex(e => e.BatchNumber);
            entity.HasIndex(e => e.ProductionReferenceNumber);

            entity.Property(e => e.MaterialName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.MaterialType).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ProductionReferenceNumber);
            entity.Property(e => e.SampleCode).HasMaxLength(100);
            entity.Property(e => e.StorageLocationName).HasMaxLength(255);
            entity.Property(e => e.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.IssuedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.ConsumedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.AvailableQuantityBefore).HasPrecision(18, 3);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany()
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionOrderTransfer>(entity =>
        {
            entity.ToTable("ProductionOrderTransfers");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => e.ProductionOrderItemId);
            entity.HasIndex(e => e.WarehouseBatchId);
            entity.HasIndex(e => e.RawMaterialId);
            entity.HasIndex(e => e.StorageOperationId);
            entity.HasIndex(e => e.TransferTransactionNumber);

            entity.Property(e => e.BatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.MaterialName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.TransferTransactionNumber).HasMaxLength(100);
            entity.Property(e => e.TransferredQuantity).HasPrecision(18, 3);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.StorageLocationName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ConfirmedBy).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.Transfers)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProductionOrderItem)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionOrderItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany()
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionOrderFormulaVersion>(entity =>
        {
            entity.ToTable("ProductionOrderFormulaVersions");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => new { e.ProductionOrderId, e.VersionNumber }).IsUnique();

            entity.Property(e => e.ProductName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ProductDescription).HasMaxLength(1000);
            entity.Property(e => e.TargetOutputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalInputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(255).IsRequired();

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.FormulaVersions)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionBatch>(entity =>
        {
            entity.ToTable("ProductionBatches");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.BatchNumber).IsUnique();
            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QualityHold);

            entity.Property(e => e.BatchNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CurrentStage).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("IN_PROGRESS").IsRequired();
            entity.Property(e => e.TotalInputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalOutputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalWasteQuantity).HasPrecision(18, 3);
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.Batches)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProductionWorkInProcess)
                  .WithMany()
                  .HasForeignKey(e => e.ProductionWorkInProcessId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionOutput>(entity =>
        {
            entity.ToTable("ProductionOutputs");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.OutputNumber).IsUnique();
            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => e.ProductionBatchId);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.Classification);
            entity.HasIndex(e => e.LaboratoryConformityStatus);
            entity.HasIndex(e => e.IsStorageReceived);

            entity.Property(e => e.OutputNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Classification).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.ProductionStage).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.LaboratoryConformityStatus).HasMaxLength(50).HasDefaultValue("PENDING_INSPECTION").IsRequired();
            entity.Property(e => e.StorageReceivedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.Outputs)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductionBatch)
                  .WithMany(b => b.Outputs)
                  .HasForeignKey(e => e.ProductionBatchId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LaboratoryCertificate)
                  .WithMany()
                  .HasForeignKey(e => e.LaboratoryCertificateId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.StorageOperation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageOperationId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionRework>(entity =>
        {
            entity.ToTable("ProductionReworks");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ReworkNumber).IsUnique();
            entity.HasIndex(e => e.OriginalProductionOrderId);
            entity.HasIndex(e => e.OriginalProductionBatchId);
            entity.HasIndex(e => e.OriginalProductionOutputId);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.ReworkNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();
        });

        modelBuilder.Entity<DailyProductionSubmission>(entity =>
        {
            entity.ToTable("DailyProductionSubmissions");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.SubmissionNumber).IsUnique();
            entity.HasIndex(e => e.ProductionDate);
            entity.HasIndex(e => e.EmployeeEmail);
            entity.HasIndex(e => e.Shift);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.SubmissionNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EmployeeName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.EmployeeEmail).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Shift).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("SUBMITTED").IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(150);

            entity.Property(e => e.DurationHours).HasPrecision(18, 3);
            entity.Property(e => e.TotalGrossQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WasteQuantity).HasPrecision(18, 3);
            entity.Property(e => e.NetProductionQuantity).HasPrecision(18, 3);
            entity.Property(e => e.ProductionRatePerHour).HasPrecision(18, 3);
            entity.Property(e => e.WastePercentage).HasPrecision(18, 3);

            entity.HasMany(e => e.Details)
                  .WithOne(d => d.DailyProductionSubmission)
                  .HasForeignKey(d => d.DailyProductionSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyProductionDetail>(entity =>
        {
            entity.ToTable("DailyProductionDetails");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.DailyProductionSubmissionId);
            entity.HasIndex(e => e.ProductName);

            entity.Property(e => e.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();

            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.ProductionRatePerHour).HasPrecision(18, 3);
            entity.Property(e => e.ProductWaste).HasPrecision(18, 3);
            entity.Property(e => e.NetQuantity).HasPrecision(18, 3);
        });

        // Production Department: Process & Traceability Entities Configuration
        modelBuilder.Entity<ProductionProcess>(entity =>
        {
            entity.ToTable("ProductionProcesses");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProcessNumber).IsUnique();
            entity.HasIndex(e => e.ProductionDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ProductionOrderId);

            entity.Property(e => e.ProcessNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProductionOrderNumber).HasMaxLength(100);
            entity.Property(e => e.StorageLocationName).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("IN_PROGRESS").IsRequired();
            entity.Property(e => e.ResponsibleEmployee).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();

            entity.Property(e => e.TotalInputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalOutputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalWasteQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WastePercentage).HasPrecision(18, 2);

            entity.Property(e => e.IncomingWeighbridgePath).HasMaxLength(1000);
            entity.Property(e => e.IncomingWeighbridgeFileName).HasMaxLength(255);
            entity.Property(e => e.IncomingWeighbridgeFileType).HasMaxLength(100);

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Materials)
                  .WithOne(m => m.ProductionProcess)
                  .HasForeignKey(m => m.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.FinishedProducts)
                  .WithOne(f => f.ProductionProcess)
                  .HasForeignKey(f => f.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.ProductionProcess)
                  .HasForeignKey(a => a.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.WasteRecords)
                  .WithOne(w => w.ProductionProcess)
                  .HasForeignKey(w => w.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductionProcessMaterial>(entity =>
        {
            entity.ToTable("ProductionProcessMaterials");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionProcessId);
            entity.HasIndex(e => e.WarehouseBatchId);
            entity.HasIndex(e => e.ProductionReferenceNumber);
            entity.HasIndex(e => e.BatchNumber);

            entity.Property(e => e.BatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SampleCode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.MaterialName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MaterialClassification).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StorageLocationName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();

            entity.Property(e => e.AvailableQuantityBefore).HasPrecision(18, 3);
            entity.Property(e => e.ConsumedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.RemainingQuantityAfter).HasPrecision(18, 3);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany(p => p.Materials)
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WarehouseBatch)
                  .WithMany()
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RawMaterial)
                  .WithMany()
                  .HasForeignKey(e => e.RawMaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StorageLocation)
                  .WithMany()
                  .HasForeignKey(e => e.StorageLocationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionFinishedProduct>(entity =>
        {
            entity.ToTable("ProductionFinishedProducts");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionProcessId);
            entity.HasIndex(e => e.ProductionOrderId);
            entity.HasIndex(e => e.ManufactureNumber);

            entity.Property(e => e.FinishedProductName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ManufactureNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProductionOrderNumber).HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(255).IsRequired();

            entity.Property(e => e.ManufacturedQuantity).HasPrecision(18, 3);

            entity.Property(e => e.WeighbridgeAttachmentPath).HasMaxLength(1000);
            entity.Property(e => e.WeighbridgeFileName).HasMaxLength(255);
            entity.Property(e => e.WeighbridgeFileType).HasMaxLength(100);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany(p => p.FinishedProducts)
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProductionOrder)
                  .WithMany(o => o.FinishedProducts)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionAttachment>(entity =>
        {
            entity.ToTable("ProductionAttachments");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionProcessId);
            entity.HasIndex(e => e.FinishedProductId);

            entity.Property(e => e.AttachmentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.StoredFileName).HasMaxLength(255);
            entity.Property(e => e.FileType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.UploadedBy).HasMaxLength(255).IsRequired();

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany(p => p.Attachments)
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.FinishedProduct)
                  .WithMany()
                  .HasForeignKey(e => e.FinishedProductId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionWasteRecord>(entity =>
        {
            entity.ToTable("ProductionWasteRecords");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.ProductionProcessId);

            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.CalculatedBy).HasMaxLength(255).IsRequired();

            entity.Property(e => e.TotalInputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.TotalOutputQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WasteQuantity).HasPrecision(18, 3);
            entity.Property(e => e.WastePercentage).HasPrecision(18, 2);

            entity.HasOne(e => e.ProductionProcess)
                  .WithMany(p => p.WasteRecords)
                  .HasForeignKey(e => e.ProductionProcessId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.ProjectCodeId);
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.InvoiceDate);

            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProjectCodeValue).HasMaxLength(50);
            entity.Property(e => e.ProjectName).HasMaxLength(200);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(30).HasDefaultValue("COMPANY").IsRequired();
            entity.Property(e => e.PONumber).HasMaxLength(100);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyAddress).HasMaxLength(300);
            entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
            entity.Property(e => e.CompanyPhone).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("ISSUED").IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(150).IsRequired();

            entity.Property(e => e.Subtotal).HasPrecision(18, 3);
            entity.Property(e => e.ShippingPrice).HasPrecision(18, 3).HasDefaultValue(0);
            entity.Property(e => e.TaxRate).HasPrecision(5, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 3);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 3);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 3);
            entity.Property(e => e.OrderedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.InvoicedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.PoUnitPrice).HasPrecision(18, 3);
            entity.Property(e => e.InvoiceUnitPrice).HasPrecision(18, 3);

            entity.HasIndex(e => e.SupplierId);

            entity.HasOne(e => e.SupplierEntity)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProjectCode)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectCodeId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.Invoices)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Company)
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Invoice)
                  .HasForeignKey(i => i.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.ToTable("InvoiceItems");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.TransportationId);
            entity.HasIndex(e => e.PurchaseOrderId);

            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WarehouseExportNumber).HasMaxLength(100);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ProductionOrderNumber).HasMaxLength(100);
            entity.Property(e => e.PONumber).HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(50).HasDefaultValue("طن").IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(200);

            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 3);
            entity.Property(e => e.LineTotal).HasPrecision(18, 3);

            entity.HasOne(e => e.Transportation)
                  .WithMany()
                  .HasForeignKey(e => e.TransportationId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.SupplierCode);
            entity.HasIndex(e => e.Name);

            entity.Property(e => e.SupplierCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.TaxNumber).HasMaxLength(50);
            entity.Property(e => e.CommercialRegister).HasMaxLength(50);
            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.PrimaryMaterialName).HasMaxLength(200);
            entity.Property(e => e.PrimaryMaterialType).HasMaxLength(100);
            entity.Property(e => e.SourceDepartment).HasMaxLength(50).HasDefaultValue("FINANCE");
            entity.Property(e => e.RelatedLaboratoryReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.RelatedWarehouseImportNumber).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(150);

            entity.Property(e => e.TotalAmountOwed).HasPrecision(18, 3).HasDefaultValue(0);
            entity.Property(e => e.TotalAmountPaid).HasPrecision(18, 3).HasDefaultValue(0);
            entity.Property(e => e.RemainingBalance).HasPrecision(18, 3).HasDefaultValue(0);

            entity.HasMany(e => e.FinancialTransactions)
                  .WithOne(t => t.Supplier)
                  .HasForeignKey(t => t.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PurchaseOrders)
                  .WithOne(p => p.SupplierEntity)
                  .HasForeignKey(p => p.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SupplierFinancialTransaction>(entity =>
        {
            entity.ToTable("SupplierFinancialTransactions");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.TransactionType);
            entity.HasIndex(e => e.TransactionDate);

            entity.Property(e => e.TransactionType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(150);
            entity.Property(e => e.Amount).HasPrecision(18, 3);

            entity.HasOne(e => e.Invoice)
                  .WithMany()
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}


