using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Offers;

namespace Regalis.Infrastructure.Persistence.Configurations;

// Offer tables. Created by db/migrations/20261123120000_Offers.sql (no EF migration; see the kit
// README). No navigation properties: the services join by id.

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
	public void Configure(EntityTypeBuilder<Offer> b)
	{
		b.ToTable("MST_Offer");
		b.HasKey(x => x.OfferId);
		b.Property(x => x.OfferName).HasMaxLength(120).IsRequired();
		b.Property(x => x.Title).HasMaxLength(150).IsRequired();
		b.Property(x => x.Description).HasMaxLength(1000);
		b.Property(x => x.Terms).HasMaxLength(4000);
		b.Property(x => x.Code).HasMaxLength(30);
		b.Property(x => x.DiscountType).HasConversion<byte>();
		b.Property(x => x.DiscountValue).HasPrecision(18, 2);
		b.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
		b.Property(x => x.MinAmount).HasPrecision(18, 2);
		b.HasIndex(x => x.Code).HasDatabaseName("IX_Offer_Code");
		b.HasIndex(x => new { x.IsActive, x.StartDate, x.EndDate }).HasDatabaseName("IX_Offer_Live");
	}
}

public class OfferModuleLinkConfiguration : IEntityTypeConfiguration<OfferModuleLink>
{
	public void Configure(EntityTypeBuilder<OfferModuleLink> b)
	{
		b.ToTable("MST_OfferModule");
		b.HasKey(x => x.OfferModuleId);
		b.Property(x => x.Module).HasConversion<byte>();
		b.HasIndex(x => new { x.OfferId, x.Module }).IsUnique().HasDatabaseName("UX_OfferModule");
	}
}

public class OfferScopeConfiguration : IEntityTypeConfiguration<OfferScope>
{
	public void Configure(EntityTypeBuilder<OfferScope> b)
	{
		b.ToTable("MST_OfferScope");
		b.HasKey(x => x.OfferScopeId);
		b.Property(x => x.Module).HasConversion<byte>();
		b.HasIndex(x => new { x.OfferId, x.Module, x.RefId }).IsUnique().HasDatabaseName("UX_OfferScope");
	}
}

public class OfferImageConfiguration : IEntityTypeConfiguration<OfferImage>
{
	public void Configure(EntityTypeBuilder<OfferImage> b)
	{
		b.ToTable("TRN_OfferImage");
		b.HasKey(x => x.OfferId);
		b.Property(x => x.OfferId).ValueGeneratedNever();
		b.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
		b.Property(x => x.Data).IsRequired();
	}
}

public class OfferRedemptionConfiguration : IEntityTypeConfiguration<OfferRedemption>
{
	public void Configure(EntityTypeBuilder<OfferRedemption> b)
	{
		b.ToTable("TRN_OfferRedemption");
		b.HasKey(x => x.RedemptionId);
		b.Property(x => x.Module).HasConversion<byte>();
		b.Property(x => x.BaseAmount).HasPrecision(18, 2);
		b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
		b.Property(x => x.OfferLabel).HasMaxLength(160).IsRequired();
		b.HasIndex(x => x.OfferId).HasDatabaseName("IX_OfferRedemption_Offer");
		b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_OfferRedemption_Customer");
		b.HasIndex(x => x.BookingId).HasDatabaseName("IX_OfferRedemption_Booking");
		b.HasIndex(x => x.CustomerPassId).HasDatabaseName("IX_OfferRedemption_Pass");
		b.HasIndex(x => x.PaymentId).HasDatabaseName("IX_OfferRedemption_Payment");
	}
}
