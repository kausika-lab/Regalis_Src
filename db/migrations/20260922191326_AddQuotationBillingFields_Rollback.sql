BEGIN TRANSACTION;
GO

DROP TABLE [TRN_QuotationAttachment];
GO

DROP TABLE [TRN_QuotationEvent];
GO

DROP INDEX [IX_Quotation_RevisionOf] ON [TRN_Quotation];
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'CgstAmount');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [CgstAmount];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'DiscountShare');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [DiscountShare];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'EndUtc');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [EndUtc];
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'IgstAmount');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [IgstAmount];
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'LineNotes');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [LineNotes];
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'LineType');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [LineType];
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'PassPlanId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [PassPlanId];
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'ServiceType');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [ServiceType];
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'SgstAmount');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [SgstAmount];
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'StandardRate');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [StandardRate];
GO

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'StartUtc');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [StartUtc];
GO

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'TaxableAmount');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [TaxableAmount];
GO

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'WorkspaceId');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [TRN_QuotationLine] DROP COLUMN [WorkspaceId];
GO

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'AdjustmentAmount');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [AdjustmentAmount];
GO

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'AdjustmentLabel');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [AdjustmentLabel];
GO

DECLARE @var15 sysname;
SELECT @var15 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'AdvanceInvoiceEnabled');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var15 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [AdvanceInvoiceEnabled];
GO

DECLARE @var16 sysname;
SELECT @var16 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'AdvanceType');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var16 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [AdvanceType];
GO

DECLARE @var17 sysname;
SELECT @var17 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'AdvanceValue');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var17 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [AdvanceValue];
GO

DECLARE @var18 sysname;
SELECT @var18 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'CustomerCompanyName');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var18 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [CustomerCompanyName];
GO

DECLARE @var19 sysname;
SELECT @var19 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DecisionConfirmedBy');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var19 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DecisionConfirmedBy];
GO

DECLARE @var20 sysname;
SELECT @var20 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DecisionIpAddress');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var20 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DecisionIpAddress];
GO

DECLARE @var21 sysname;
SELECT @var21 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DecisionMode');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var21 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DecisionMode];
GO

DECLARE @var22 sysname;
SELECT @var22 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DecisionNote');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var22 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DecisionNote];
GO

DECLARE @var23 sysname;
SELECT @var23 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DecisionOnBehalf');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var23 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DecisionOnBehalf];
GO

DECLARE @var24 sysname;
SELECT @var24 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DiscountType');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var24 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DiscountType];
GO

DECLARE @var25 sysname;
SELECT @var25 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'DiscountValue');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var25 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [DiscountValue];
GO

DECLARE @var26 sysname;
SELECT @var26 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'InternalNote');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var26 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [InternalNote];
GO

DECLARE @var27 sysname;
SELECT @var27 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'PlaceOfSupplyStateCode');
IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var27 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [PlaceOfSupplyStateCode];
GO

DECLARE @var28 sysname;
SELECT @var28 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'ReferenceNumber');
IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var28 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [ReferenceNumber];
GO

DECLARE @var29 sysname;
SELECT @var29 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'RejectionCode');
IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var29 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [RejectionCode];
GO

DECLARE @var30 sysname;
SELECT @var30 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'RevisionNumber');
IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var30 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [RevisionNumber];
GO

DECLARE @var31 sysname;
SELECT @var31 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'RevisionOfQuotationId');
IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var31 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [RevisionOfQuotationId];
GO

DECLARE @var32 sysname;
SELECT @var32 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'RoundOffAmount');
IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var32 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [RoundOffAmount];
GO

DECLARE @var33 sysname;
SELECT @var33 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'SalespersonId');
IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var33 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [SalespersonId];
GO

DECLARE @var34 sysname;
SELECT @var34 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'Subject');
IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var34 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [Subject];
GO

DECLARE @var35 sysname;
SELECT @var35 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'TaxMode');
IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var35 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [TaxMode];
GO

DECLARE @var36 sysname;
SELECT @var36 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'TdsAmount');
IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var36 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [TdsAmount];
GO

DECLARE @var37 sysname;
SELECT @var37 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'TdsCode');
IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var37 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [TdsCode];
GO

DECLARE @var38 sysname;
SELECT @var38 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'TdsLabel');
IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var38 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [TdsLabel];
GO

DECLARE @var39 sysname;
SELECT @var39 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'TdsPercent');
IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var39 + '];');
ALTER TABLE [TRN_Quotation] DROP COLUMN [TdsPercent];
GO

DECLARE @var40 sysname;
SELECT @var40 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'AdjustmentAmount');
IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var40 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [AdjustmentAmount];
GO

DECLARE @var41 sysname;
SELECT @var41 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'AdjustmentLabel');
IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var41 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [AdjustmentLabel];
GO

DECLARE @var42 sysname;
SELECT @var42 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'RoundOffAmount');
IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var42 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [RoundOffAmount];
GO

DECLARE @var43 sysname;
SELECT @var43 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'TdsAmount');
IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var43 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [TdsAmount];
GO

DECLARE @var44 sysname;
SELECT @var44 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'TdsLabel');
IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var44 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [TdsLabel];
GO

DECLARE @var45 sysname;
SELECT @var45 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'TdsPercent');
IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var45 + '];');
ALTER TABLE [TRN_Invoice] DROP COLUMN [TdsPercent];
GO

DECLARE @var46 sysname;
SELECT @var46 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_Workspace]') AND [c].[name] = N'PricePerMonth');
IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [MST_Workspace] DROP CONSTRAINT [' + @var46 + '];');
ALTER TABLE [MST_Workspace] DROP COLUMN [PricePerMonth];
GO

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields';
GO

COMMIT;
GO


-- The settings the forward script inserted. Dropped only if still untouched, so a
-- value an admin edited is never silently discarded by a rollback.
DELETE FROM dbo.MST_AppSetting
 WHERE SettingGroup = 'Quotation'
   AND SettingKey IN (
       'Quotation.DefaultGstPercent', 'Quotation.GstRateOptions', 'Quotation.HomeStateCode',
       'Quotation.RoundOffEnabled', 'Quotation.CustomerNotes', 'Quotation.TdsOptions',
       'Quotation.EmailSubjectTemplate', 'Quotation.EmailBodyTemplate',
       'Quotation.WhatsAppTemplate', 'Quotation.PortalBaseUrl',
       'Quotation.ExpiryReminderDays', 'Quotation.AdvanceMonths')
   AND ModifiedDate IS NULL;
GO
