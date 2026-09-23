/* 20260922162733_AddGoogleSignIn_Rollback.sql
   Reverses 20260922162733_AddGoogleSignIn.sql.

   NOT loss-free, and it can fail outright. Read before running:

     1. Every Google-only account has its NULL PasswordHash rewritten to the empty
        string, because the column goes back to NOT NULL. Those members can then
        neither sign in with Google (the endpoint is gone) nor with a password (there
        is none). They must use Forgot Password to set one.

     2. UX_User_Mobile goes back to covering the empty string. If two or more members
        signed up through Google - all of whom carry an empty mobile number - creating
        that index FAILS and the whole script rolls back. Give those rows real mobile
        numbers first, or delete them.

     3. GoogleId is dropped, so the link between a Regalis account and its Google
        identity is gone. Re-running the forward script does not restore it; each
        member re-links on their next Google sign-in.

   Deploy the previous API binaries BEFORE running this - the shipped API reads
   MST_User.GoogleId on every Google sign-in and EF names columns explicitly, so
   dropping it under a running new API breaks those queries. */

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    DROP INDEX [UX_User_GoogleId] ON [MST_User];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    DROP INDEX [UX_User_Mobile] ON [MST_User];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'GoogleId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [GoogleId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'PasswordHash');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var1 + '];');
    EXEC(N'UPDATE [MST_User] SET [PasswordHash] = N'''' WHERE [PasswordHash] IS NULL');
    ALTER TABLE [MST_User] ALTER COLUMN [PasswordHash] nvarchar(400) NOT NULL;
    ALTER TABLE [MST_User] ADD DEFAULT N'' FOR [PasswordHash];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_User_Mobile] ON [MST_User] ([MobileNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922162733_AddGoogleSignIn';
END;
GO

COMMIT;
GO

