using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Regalis.Domain.Entities.Crm;

namespace Regalis.Infrastructure.Persistence.Configurations;

// CRM tables. Created by db/migrations/20261119120000_Crm.sql (no EF migration; see the kit
// README). No navigation properties: the services join by id, which keeps the soft-delete
// query filters of the existing tables out of CRM queries.

public class CrmLeadConfiguration : IEntityTypeConfiguration<CrmLead>
{
	public void Configure(EntityTypeBuilder<CrmLead> b)
	{
		b.ToTable("TRN_CrmLead");
		b.HasKey(x => x.LeadId);
		b.Property(x => x.LeadNumber).HasMaxLength(30).IsRequired();
		b.Property(x => x.Name).HasMaxLength(150).IsRequired();
		b.Property(x => x.Company).HasMaxLength(150);
		b.Property(x => x.Mobile).HasMaxLength(20).IsRequired();
		b.Property(x => x.MobileNormalised).HasMaxLength(10).IsRequired();
		b.Property(x => x.Email).HasMaxLength(256);
		b.Property(x => x.EmailNormalised).HasMaxLength(256);
		b.Property(x => x.BudgetText).HasMaxLength(100);
		b.Property(x => x.SourceDetail).HasMaxLength(300);
		b.Property(x => x.FirstMessage).HasMaxLength(2000);
		b.Property(x => x.LostNote).HasMaxLength(500);
		b.Property(x => x.Channel).HasConversion<byte>();
		b.Property(x => x.DealValue).HasPrecision(18, 2);
		b.HasIndex(x => x.LeadNumber).IsUnique().HasDatabaseName("UX_CrmLead_Number");
		b.HasIndex(x => x.MobileNormalised).HasDatabaseName("IX_CrmLead_Mobile");
		b.HasIndex(x => x.EmailNormalised).HasDatabaseName("IX_CrmLead_Email");
		b.HasIndex(x => new { x.StatusId, x.OwnerUserId }).HasDatabaseName("IX_CrmLead_Status_Owner");
		b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_CrmLead_Customer");
	}
}

public class CrmLeadActivityConfiguration : IEntityTypeConfiguration<CrmLeadActivity>
{
	public void Configure(EntityTypeBuilder<CrmLeadActivity> b)
	{
		b.ToTable("TRN_CrmLeadActivity");
		b.HasKey(x => x.ActivityId);
		b.Property(x => x.Kind).HasConversion<byte>();
		b.Property(x => x.Detail).HasMaxLength(2000).IsRequired();
		b.HasIndex(x => new { x.LeadId, x.HappenedUtc }).HasDatabaseName("IX_CrmLeadActivity_Lead");
	}
}

public class CrmFollowUpConfiguration : IEntityTypeConfiguration<CrmFollowUp>
{
	public void Configure(EntityTypeBuilder<CrmFollowUp> b)
	{
		b.ToTable("TRN_CrmFollowUp");
		b.HasKey(x => x.FollowUpId);
		b.Property(x => x.WhatToDo).HasMaxLength(500).IsRequired();
		b.Property(x => x.Kind).HasMaxLength(10).IsRequired();
		b.Property(x => x.Outcome).HasMaxLength(500);
		b.HasIndex(x => new { x.IsDone, x.DueUtc }).HasDatabaseName("IX_CrmFollowUp_Due");
		b.HasIndex(x => x.LeadId).HasDatabaseName("IX_CrmFollowUp_Lead");
	}
}

public class CrmLeadConsentConfiguration : IEntityTypeConfiguration<CrmLeadConsent>
{
	public void Configure(EntityTypeBuilder<CrmLeadConsent> b)
	{
		b.ToTable("TRN_CrmLeadConsent");
		b.HasKey(x => x.ConsentId);
		b.Property(x => x.HowObtained).HasMaxLength(200).IsRequired();
		b.Property(x => x.ConsentTextShown).HasMaxLength(1000);
		b.Property(x => x.SourceIpHash).HasMaxLength(64);
		b.HasIndex(x => x.LeadId).HasDatabaseName("IX_CrmLeadConsent_Lead");
	}
}

public class CrmVisitorConfiguration : IEntityTypeConfiguration<CrmVisitor>
{
	public void Configure(EntityTypeBuilder<CrmVisitor> b)
	{
		b.ToTable("TRN_CrmVisitor");
		b.HasKey(x => x.VisitorId);
		b.Property(x => x.VisitorKey).HasMaxLength(64).IsRequired();
		b.Property(x => x.UtmSource).HasMaxLength(100);
		b.Property(x => x.UtmMedium).HasMaxLength(100);
		b.Property(x => x.UtmCampaign).HasMaxLength(100);
		b.Property(x => x.Referrer).HasMaxLength(300);
		b.Property(x => x.LandingPage).HasMaxLength(300);
		b.Property(x => x.City).HasMaxLength(100);
		b.Property(x => x.Country).HasMaxLength(100);
		b.Property(x => x.DeviceType).HasMaxLength(30);
		b.Property(x => x.Browser).HasMaxLength(50);
		b.HasIndex(x => x.VisitorKey).IsUnique().HasDatabaseName("UX_CrmVisitor_Key");
		b.HasIndex(x => x.FirstSeenUtc).HasDatabaseName("IX_CrmVisitor_FirstSeen");
	}
}

public class CrmPageViewConfiguration : IEntityTypeConfiguration<CrmPageView>
{
	public void Configure(EntityTypeBuilder<CrmPageView> b)
	{
		b.ToTable("TRN_CrmPageView");
		b.HasKey(x => x.PageViewId);
		b.Property(x => x.Path).HasMaxLength(300).IsRequired();
		b.HasIndex(x => new { x.VisitorId, x.ViewedUtc }).HasDatabaseName("IX_CrmPageView_Visitor");
	}
}

public class CrmWhatsAppRefConfiguration : IEntityTypeConfiguration<CrmWhatsAppRef>
{
	public void Configure(EntityTypeBuilder<CrmWhatsAppRef> b)
	{
		b.ToTable("TRN_CrmWhatsAppRef");
		b.HasKey(x => x.WhatsAppRefId);
		b.Property(x => x.Code).HasMaxLength(12).IsRequired();
		b.Property(x => x.PagePath).HasMaxLength(300);
		b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_CrmWhatsAppRef_Code");
	}
}

public class CrmNotificationConfiguration : IEntityTypeConfiguration<CrmNotification>
{
	public void Configure(EntityTypeBuilder<CrmNotification> b)
	{
		b.ToTable("TRN_CrmNotification");
		b.HasKey(x => x.NotificationId);
		b.Property(x => x.Kind).HasMaxLength(40).IsRequired();
		b.Property(x => x.RecipientAddress).HasMaxLength(256);
		b.Property(x => x.Channel).HasMaxLength(10).IsRequired();
		b.Property(x => x.Title).HasMaxLength(200).IsRequired();
		b.Property(x => x.Body).HasMaxLength(2000);
		b.Property(x => x.Error).HasMaxLength(500);
		b.HasIndex(x => new { x.RecipientUserId, x.ReadUtc }).HasDatabaseName("IX_CrmNotification_Recipient");
		b.HasIndex(x => x.LeadId).HasDatabaseName("IX_CrmNotification_Lead");
	}
}

public class CrmSourceConfiguration : IEntityTypeConfiguration<CrmSource>
{
	public void Configure(EntityTypeBuilder<CrmSource> b)
	{
		b.ToTable("MST_CrmSource");
		b.HasKey(x => x.SourceId);
		b.Property(x => x.Code).HasMaxLength(30).IsRequired();
		b.Property(x => x.Name).HasMaxLength(100).IsRequired();
		b.Property(x => x.UtmSource).HasMaxLength(200);
		b.Property(x => x.UtmMedium).HasMaxLength(100);
		b.Property(x => x.ReferrerMatch).HasMaxLength(200);
	}
}

public class CrmStatusConfiguration : IEntityTypeConfiguration<CrmStatus>
{
	public void Configure(EntityTypeBuilder<CrmStatus> b)
	{
		b.ToTable("MST_CrmStatus");
		b.HasKey(x => x.StatusId);
		b.Property(x => x.Code).HasMaxLength(30).IsRequired();
		b.Property(x => x.Name).HasMaxLength(60).IsRequired();
		b.Property(x => x.Description).HasMaxLength(200);
		b.Property(x => x.Colour).HasMaxLength(20).IsRequired();
		b.Property(x => x.Requires).HasConversion<byte>();
	}
}

public class CrmLostReasonConfiguration : IEntityTypeConfiguration<CrmLostReason>
{
	public void Configure(EntityTypeBuilder<CrmLostReason> b)
	{
		b.ToTable("MST_CrmLostReason");
		b.HasKey(x => x.LostReasonId);
		b.Property(x => x.Name).HasMaxLength(100).IsRequired();
		b.Property(x => x.Hint).HasMaxLength(200);
	}
}

public class CrmLookingForConfiguration : IEntityTypeConfiguration<CrmLookingFor>
{
	public void Configure(EntityTypeBuilder<CrmLookingFor> b)
	{
		b.ToTable("MST_CrmLookingFor");
		b.HasKey(x => x.LookingForId);
		b.Property(x => x.Name).HasMaxLength(100).IsRequired();
	}
}

public class CrmAssignmentRuleConfiguration : IEntityTypeConfiguration<CrmAssignmentRule>
{
	public void Configure(EntityTypeBuilder<CrmAssignmentRule> b)
	{
		b.ToTable("MST_CrmAssignmentRule");
		b.HasKey(x => x.RuleId);
		b.Property(x => x.Label).HasMaxLength(200).IsRequired();
		b.Property(x => x.ConditionType).HasConversion<byte>();
		b.Property(x => x.TargetType).HasConversion<byte>();
	}
}

public class CrmTeamMemberConfiguration : IEntityTypeConfiguration<CrmTeamMember>
{
	public void Configure(EntityTypeBuilder<CrmTeamMember> b)
	{
		b.ToTable("MST_CrmTeamMember");
		b.HasKey(x => x.UserId);
		b.Property(x => x.UserId).ValueGeneratedNever();
	}
}

public class CrmSettingConfiguration : IEntityTypeConfiguration<CrmSetting>
{
	public void Configure(EntityTypeBuilder<CrmSetting> b)
	{
		b.ToTable("MST_CrmSetting");
		b.HasKey(x => x.SettingId);
		b.Property(x => x.SettingKey).HasMaxLength(60).IsRequired();
		b.Property(x => x.SettingValue).HasMaxLength(2000);
		b.HasIndex(x => x.SettingKey).IsUnique().HasDatabaseName("UX_CrmSetting_Key");
	}
}
