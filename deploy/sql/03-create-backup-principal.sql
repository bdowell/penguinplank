-- Grants the Windows account that runs the nightly backup task permission to back up the database.
-- No SQL password is involved: the scheduled task connects with Windows authentication.
--
-- Run as a sysadmin with sqlcmd. Values come from environment variables:
--   PP_DB_NAME, PP_BACKUP_WINDOWS_ACCOUNT (e.g. DOMAIN\ppbackup$ or HOST\ppbackup)
--
--   sqlcmd -S .\SQLEXPRESS -E -b -i deploy\sql\03-create-backup-principal.sql
:setvar DbName "$(PP_DB_NAME)"
:setvar WindowsAccount "$(PP_BACKUP_WINDOWS_ACCOUNT)"
:on error exit

IF SUSER_ID(N'$(WindowsAccount)') IS NULL
    CREATE LOGIN [$(WindowsAccount)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(DbName)];
GO

USE [$(DbName)];
GO

IF USER_ID(N'$(WindowsAccount)') IS NULL
    CREATE USER [$(WindowsAccount)] FOR LOGIN [$(WindowsAccount)];
GO

-- db_backupoperator can run BACKUP DATABASE; it cannot read or write business data.
ALTER ROLE db_backupoperator ADD MEMBER [$(WindowsAccount)];
GO

-- The capacity check reads file sizes from sys.master_files.
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_permissions p JOIN sys.server_principals s ON s.principal_id = p.grantee_principal_id
               WHERE s.name = N'$(WindowsAccount)' AND p.permission_name = N'VIEW ANY DEFINITION')
    GRANT VIEW ANY DEFINITION TO [$(WindowsAccount)];
GO
