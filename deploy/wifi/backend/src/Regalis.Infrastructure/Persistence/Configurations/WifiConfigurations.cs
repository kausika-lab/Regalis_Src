using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Wifi;

namespace Regalis.Infrastructure.Persistence.Configurations;

// Wi-Fi tables. Created by db/migrations/20261119180000_Wifi.sql (no EF migration; see the kit
// README). No navigation properties: the services join by id.

public class WifiAccessConfiguration : IEntityTypeConfiguration<WifiAccess>
{
	public void Configure(EntityTypeBuilder<WifiAccess> b)
	{
		b.ToTable("TRN_WifiAccess");
		b.HasKey(x => x.AccessId);
		b.Property(x => x.AccessNo).HasMaxLength(30).IsRequired();
		b.Property(x => x.Kind).HasConversion<byte>();
		b.Property(x => x.Profile).HasMaxLength(20).IsRequired();
		b.Property(x => x.HolderName).HasMaxLength(150).IsRequired();
		b.Property(x => x.HolderEmail).HasMaxLength(256);
		b.Property(x => x.HolderMobile).HasMaxLength(20);
		b.Property(x => x.Ssid).HasMaxLength(64).IsRequired();
		b.Property(x => x.Username).HasMaxLength(64).IsRequired();
		b.Property(x => x.PasswordEnc).HasMaxLength(400).IsRequired();
		b.Property(x => x.Status).HasConversion<byte>();
		b.Property(x => x.ControllerRef).HasMaxLength(100);
		b.Property(x => x.ControllerState).HasConversion<byte>();
		b.Property(x => x.ControllerError).HasMaxLength(500);
		b.Property(x => x.EmailStatus).HasMaxLength(10);
		b.Property(x => x.EmailError).HasMaxLength(500);
		b.Property(x => x.PauseReason).HasMaxLength(300);
		b.Property(x => x.RevokedReason).HasMaxLength(500);
		b.Property(x => x.Note).HasMaxLength(500);
		b.HasIndex(x => x.AccessNo).IsUnique().HasDatabaseName("UX_WifiAccess_No");
		b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_WifiAccess_Username");
		b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_WifiAccess_Customer");
		b.HasIndex(x => new { x.Status, x.ValidUntilUtc }).HasDatabaseName("IX_WifiAccess_Status");
		b.HasIndex(x => x.ControllerRef).HasDatabaseName("IX_WifiAccess_Ref");
		b.HasIndex(x => x.ParentAccessId).HasDatabaseName("IX_WifiAccess_Parent");
	}
}

public class WifiAccessSourceConfiguration : IEntityTypeConfiguration<WifiAccessSource>
{
	public void Configure(EntityTypeBuilder<WifiAccessSource> b)
	{
		b.ToTable("TRN_WifiAccessSource");
		b.HasKey(x => x.SourceId);
		b.Property(x => x.RefNumber).HasMaxLength(40).IsRequired();
		b.HasIndex(x => new { x.SourceType, x.RefId }).IsUnique().HasDatabaseName("UX_WifiAccessSource_Ref");
		b.HasIndex(x => x.AccessId).HasDatabaseName("IX_WifiAccessSource_Access");
	}
}

public class WifiDeviceConfiguration : IEntityTypeConfiguration<WifiDevice>
{
	public void Configure(EntityTypeBuilder<WifiDevice> b)
	{
		b.ToTable("TRN_WifiDevice");
		b.HasKey(x => x.DeviceId);
		b.Property(x => x.MacAddress).HasMaxLength(17).IsRequired();
		b.Property(x => x.DeviceName).HasMaxLength(120);
		b.Property(x => x.RemovedBy).HasMaxLength(20);
		b.HasIndex(x => new { x.AccessId, x.MacAddress }).HasDatabaseName("IX_WifiDevice_Access_Mac");
	}
}

public class WifiSessionConfiguration : IEntityTypeConfiguration<WifiSession>
{
	public void Configure(EntityTypeBuilder<WifiSession> b)
	{
		b.ToTable("TRN_WifiSession");
		b.HasKey(x => x.SessionId);
		b.Property(x => x.MacAddress).HasMaxLength(17).IsRequired();
		b.Property(x => x.DeviceName).HasMaxLength(120);
		b.Property(x => x.Ssid).HasMaxLength(64);
		b.Property(x => x.AccessPoint).HasMaxLength(120);
		b.Property(x => x.IpAddress).HasMaxLength(45);
		b.Property(x => x.Result).HasConversion<byte>();
		b.HasIndex(x => new { x.EndedUtc, x.MacAddress }).HasDatabaseName("IX_WifiSession_Open");
		b.HasIndex(x => x.StartedUtc).HasDatabaseName("IX_WifiSession_Started");
		b.HasIndex(x => x.AccessId).HasDatabaseName("IX_WifiSession_Access");
	}
}

public class WifiEventConfiguration : IEntityTypeConfiguration<WifiEvent>
{
	public void Configure(EntityTypeBuilder<WifiEvent> b)
	{
		b.ToTable("TRN_WifiEvent");
		b.HasKey(x => x.EventId);
		b.Property(x => x.Event).HasMaxLength(30).IsRequired();
		b.Property(x => x.Detail).HasMaxLength(1000).IsRequired();
		b.HasIndex(x => new { x.AccessId, x.CreatedUtc }).HasDatabaseName("IX_WifiEvent_Access");
	}
}

public class WifiBlockConfiguration : IEntityTypeConfiguration<WifiBlock>
{
	public void Configure(EntityTypeBuilder<WifiBlock> b)
	{
		b.ToTable("TRN_WifiBlock");
		b.HasKey(x => x.BlockId);
		b.Property(x => x.MacAddress).HasMaxLength(17).IsRequired();
		b.Property(x => x.Reason).HasMaxLength(300);
		b.HasIndex(x => x.MacAddress).HasDatabaseName("IX_WifiBlock_Mac");
	}
}

public class WifiSettingConfiguration : IEntityTypeConfiguration<WifiSetting>
{
	public void Configure(EntityTypeBuilder<WifiSetting> b)
	{
		b.ToTable("MST_WifiSetting");
		b.HasKey(x => x.SettingKey);
		b.Property(x => x.SettingKey).HasMaxLength(60);
	}
}

public class WifiSimKeyConfiguration : IEntityTypeConfiguration<WifiSimKey>
{
	public void Configure(EntityTypeBuilder<WifiSimKey> b)
	{
		b.ToTable("TRN_WifiSimKey");
		b.HasKey(x => x.ControllerRef);
		b.Property(x => x.ControllerRef).HasMaxLength(100);
		b.Property(x => x.Ssid).HasMaxLength(64).IsRequired();
		b.Property(x => x.Username).HasMaxLength(64).IsRequired();
		b.Property(x => x.PasswordEnc).HasMaxLength(400).IsRequired();
	}
}

public class WifiSimClientConfiguration : IEntityTypeConfiguration<WifiSimClient>
{
	public void Configure(EntityTypeBuilder<WifiSimClient> b)
	{
		b.ToTable("TRN_WifiSimClient");
		b.HasKey(x => x.MacAddress);
		b.Property(x => x.MacAddress).HasMaxLength(17);
		b.Property(x => x.ControllerRef).HasMaxLength(100);
		b.Property(x => x.DeviceName).HasMaxLength(120);
		b.Property(x => x.Ssid).HasMaxLength(64);
		b.Property(x => x.AccessPoint).HasMaxLength(120);
		b.Property(x => x.IpAddress).HasMaxLength(45);
	}
}

public class WifiSimEventConfiguration : IEntityTypeConfiguration<WifiSimEvent>
{
	public void Configure(EntityTypeBuilder<WifiSimEvent> b)
	{
		b.ToTable("TRN_WifiSimEvent");
		b.HasKey(x => x.SimEventId);
		b.Property(x => x.MacAddress).HasMaxLength(17).IsRequired();
		b.Property(x => x.DeviceName).HasMaxLength(120);
		b.Property(x => x.Ssid).HasMaxLength(64);
		b.Property(x => x.AccessPoint).HasMaxLength(120);
		b.Property(x => x.ControllerRef).HasMaxLength(100);
	}
}
