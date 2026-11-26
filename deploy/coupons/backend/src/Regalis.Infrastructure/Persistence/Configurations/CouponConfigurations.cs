using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Coupons;

namespace Regalis.Infrastructure.Persistence.Configurations;

// Coupon tables. Created by db/migrations/20261125120000_Coupons.sql (no EF migration; see the kit
// README). No navigation properties: the services join by id.

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
	public void Configure(EntityTypeBuilder<Coupon> b)
	{
		b.ToTable("MST_Coupon");
		b.HasKey(x => x.CouponId);
		b.Property(x => x.Code).HasMaxLength(30).IsRequired();
		b.Property(x => x.Description).HasMaxLength(300);
		b.Property(x => x.DiscountType).HasConversion<byte>();
		b.Property(x => x.DiscountValue).HasPrecision(18, 2);
		b.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
		b.Property(x => x.MinAmount).HasPrecision(18, 2);
		b.HasIndex(x => x.Code).HasDatabaseName("IX_Coupon_Code");
		b.HasIndex(x => new { x.IsActive, x.ValidFrom, x.ValidUntil }).HasDatabaseName("IX_Coupon_Live");
	}
}

public class CouponModuleLinkConfiguration : IEntityTypeConfiguration<CouponModuleLink>
{
	public void Configure(EntityTypeBuilder<CouponModuleLink> b)
	{
		b.ToTable("MST_CouponModule");
		b.HasKey(x => x.CouponModuleId);
		b.Property(x => x.Module).HasConversion<byte>();
		b.HasIndex(x => new { x.CouponId, x.Module }).IsUnique().HasDatabaseName("UX_CouponModule");
	}
}

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
	public void Configure(EntityTypeBuilder<CouponRedemption> b)
	{
		b.ToTable("TRN_CouponRedemption");
		b.HasKey(x => x.RedemptionId);
		b.Property(x => x.Module).HasConversion<byte>();
		b.Property(x => x.BaseAmount).HasPrecision(18, 2);
		b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
		b.Property(x => x.CouponCode).HasMaxLength(30).IsRequired();
		b.HasIndex(x => x.CouponId).HasDatabaseName("IX_CouponRedemption_Coupon");
		b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_CouponRedemption_Customer");
		b.HasIndex(x => x.BookingId).HasDatabaseName("IX_CouponRedemption_Booking");
		b.HasIndex(x => x.CustomerPassId).HasDatabaseName("IX_CouponRedemption_Pass");
		b.HasIndex(x => x.PaymentId).HasDatabaseName("IX_CouponRedemption_Payment");
	}
}
