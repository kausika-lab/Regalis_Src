using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Persistence.Configurations;

// Assets module (Regalis_WorkSpace_Assets_Feature_Specification, Oct 2026). Money is
// DECIMAL(18,2) like the rest of the database (exact to the paisa); quantities are
// DECIMAL(18,3) like TRN_InventoryTransaction.

public class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> builder)
    {
        builder.ToTable("MST_AssetCategory");
        builder.HasKey(x => x.AssetCategoryId);
        builder.Property(x => x.CategoryName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.CodePrefix).HasMaxLength(30).IsRequired();
        builder.Property(x => x.UsefulLifeYears).HasPrecision(5, 2);
        builder.HasIndex(x => x.CategoryName).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AssetCategory_Name");
    }
}

public class AssetLocationConfiguration : IEntityTypeConfiguration<AssetLocation>
{
    public void Configure(EntityTypeBuilder<AssetLocation> builder)
    {
        builder.ToTable("MST_AssetLocation");
        builder.HasKey(x => x.AssetLocationId);
        builder.Property(x => x.LocationName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.LocationName).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AssetLocation_Name");
        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetDetailConfiguration : IEntityTypeConfiguration<AssetDetail>
{
    public void Configure(EntityTypeBuilder<AssetDetail> builder)
    {
        builder.ToTable("MST_AssetDetail");
        builder.HasKey(x => x.InventoryItemId);
        builder.Property(x => x.InventoryItemId).ValueGeneratedNever();
        builder.Property(x => x.CodePrefix).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.RetiredReason).HasMaxLength(300);
        // One code per number per prefix, ever.
        builder.HasIndex(x => new { x.CodePrefix, x.CodeNumber }).IsUnique()
            .HasDatabaseName("UX_AssetDetail_Prefix_Number");
        builder.HasIndex(x => x.SerialNumber).HasFilter("[SerialNumber] IS NOT NULL")
            .HasDatabaseName("IX_AssetDetail_Serial");
        builder.HasIndex(x => new { x.AssetLocationId, x.Status }).HasDatabaseName("IX_AssetDetail_Location_Status");

        builder.HasOne(x => x.InventoryItem).WithOne(x => x.AssetDetail)
            .HasForeignKey<AssetDetail>(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.AssetLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoodsReceiptLine>().WithMany()
            .HasForeignKey(x => x.GoodsReceiptLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CodeSequenceConfiguration : IEntityTypeConfiguration<CodeSequence>
{
    public void Configure(EntityTypeBuilder<CodeSequence> builder)
    {
        builder.ToTable("MST_CodeSequence");
        builder.HasKey(x => x.Prefix);
        builder.Property(x => x.Prefix).HasMaxLength(40);
    }
}

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("TRN_GoodsReceipt");
        builder.HasKey(x => x.GoodsReceiptId);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.ReceiptNumber).IsUnique().HasDatabaseName("UX_GoodsReceipt_Number");
        builder.HasIndex(x => new { x.ReceiptDate, x.Status }).HasDatabaseName("IX_GoodsReceipt_Date_Status");

        builder.HasOne<Supplier>().WithMany()
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.IntoLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.GoodsReceipt)
            .HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("TRN_GoodsReceiptLine", t =>
            t.HasCheckConstraint("CK_GoodsReceiptLine_Quantity", "[Quantity] > 0"));
        builder.HasKey(x => x.GoodsReceiptLineId);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.ItemName).HasMaxLength(150);
        builder.Property(x => x.CodePrefix).HasMaxLength(30);
        builder.Property(x => x.BrandModel).HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.VendorName).HasMaxLength(150);
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.Property(x => x.FirstCode).HasMaxLength(40);
        builder.Property(x => x.LastCode).HasMaxLength(40);
        builder.HasIndex(x => x.InventoryItemId).HasDatabaseName("IX_GoodsReceiptLine_Item");

        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssetCategory>().WithMany()
            .HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.AssetLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderLine>().WithMany()
            .HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsIssueConfiguration : IEntityTypeConfiguration<GoodsIssue>
{
    public void Configure(EntityTypeBuilder<GoodsIssue> builder)
    {
        builder.ToTable("TRN_GoodsIssue");
        builder.HasKey(x => x.GoodsIssueId);
        builder.Property(x => x.IssueNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RepairVendor).HasMaxLength(150);
        builder.Property(x => x.ReportedBy).HasMaxLength(150);
        builder.Property(x => x.ReasonText).HasMaxLength(300);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.IssueNumber).IsUnique().HasDatabaseName("UX_GoodsIssue_Number");
        builder.HasIndex(x => new { x.IssueDate, x.Reason }).HasDatabaseName("IX_GoodsIssue_Date_Reason");

        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.GoodsIssue)
            .HasForeignKey(x => x.GoodsIssueId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsIssueLineConfiguration : IEntityTypeConfiguration<GoodsIssueLine>
{
    public void Configure(EntityTypeBuilder<GoodsIssueLine> builder)
    {
        builder.ToTable("TRN_GoodsIssueLine", t =>
            t.HasCheckConstraint("CK_GoodsIssueLine_Quantity", "[Quantity] > 0"));
        builder.HasKey(x => x.GoodsIssueLineId);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.HasIndex(x => x.InventoryItemId).HasDatabaseName("IX_GoodsIssueLine_Item");
        builder.HasOne(x => x.InventoryItem).WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetRepairConfiguration : IEntityTypeConfiguration<AssetRepair>
{
    public void Configure(EntityTypeBuilder<AssetRepair> builder)
    {
        builder.ToTable("TRN_AssetRepair");
        builder.HasKey(x => x.AssetRepairId);
        builder.Property(x => x.RepairVendor).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ClosedNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.InventoryItemId, x.IsOpen }).HasDatabaseName("IX_AssetRepair_Item_Open");
        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoodsIssue>().WithMany()
            .HasForeignKey(x => x.GoodsIssueId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ItemEventConfiguration : IEntityTypeConfiguration<ItemEvent>
{
    public void Configure(EntityTypeBuilder<ItemEvent> builder)
    {
        builder.ToTable("TRN_ItemEvent");
        builder.HasKey(x => x.ItemEventId);
        builder.Property(x => x.EventType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(300).IsRequired();
        builder.Property(x => x.FromText).HasMaxLength(150);
        builder.Property(x => x.ToText).HasMaxLength(150);
        builder.Property(x => x.DocumentNumber).HasMaxLength(30);
        builder.HasIndex(x => new { x.InventoryItemId, x.ItemEventId }).HasDatabaseName("IX_ItemEvent_Item");
        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ItemDocumentConfiguration : IEntityTypeConfiguration<ItemDocument>
{
    public void Configure(EntityTypeBuilder<ItemDocument> builder)
    {
        builder.ToTable("TRN_ItemDocument");
        builder.HasKey(x => x.ItemDocumentId);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.InventoryItemId).HasDatabaseName("IX_ItemDocument_Item");
        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockCountConfiguration : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> builder)
    {
        builder.ToTable("TRN_StockCount");
        builder.HasKey(x => x.StockCountId);
        builder.Property(x => x.CountNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CountedBy).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.CountNumber).IsUnique().HasDatabaseName("UX_StockCount_Number");
        builder.HasOne<AssetLocation>().WithMany()
            .HasForeignKey(x => x.AssetLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.StockCount)
            .HasForeignKey(x => x.StockCountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockCountLineConfiguration : IEntityTypeConfiguration<StockCountLine>
{
    public void Configure(EntityTypeBuilder<StockCountLine> builder)
    {
        builder.ToTable("TRN_StockCountLine");
        builder.HasKey(x => x.StockCountLineId);
        builder.Property(x => x.CountedQuantity).HasPrecision(18, 3);
        builder.Property(x => x.RecordedQuantity).HasPrecision(18, 3);
        builder.Property(x => x.Difference).HasPrecision(18, 3);
        builder.HasIndex(x => new { x.StockCountId, x.InventoryItemId }).IsUnique()
            .HasDatabaseName("UX_StockCountLine_Count_Item");
        builder.HasOne<InventoryItem>().WithMany()
            .HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
