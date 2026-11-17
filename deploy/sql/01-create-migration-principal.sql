-- Creates the SEPARATE schema-migration principal. Used only while applying EF Core migrations
-- (an explicit deployment step); the running API never uses it. It can alter schema but is NOT
-- db_owner and cannot manage users, roles, or backups.
--
-- Run once as a sysadmin with sqlcmd. Values come from sqlcmd scripting variables, which sqlcmd
-- reads from ENVIRONMENT VARIABLES, so no secret is stored in this file or on a command line:
--   PP_DB_NAME            application database (must already exist; create it empty first)
--   PP_MIGRATION_LOGIN    e.g. pp_migrator
--   PP_MIGRATION_PASSWORD supplied from your secret store at run time
--
--   sqlcmd -S .\SQLEXPRESS -E -b -i deploy\sql\01-create-migration-principal.sql
:setvar DbName "$(PP_DB_NAME)"
:setvar LoginName "$(PP_MIGRATION_LOGIN)"
:setvar LoginPassword "$(PP_MIGRATION_PASSWORD)"
:on error exit

IF SUSER_ID(N'$(LoginName)') IS NULL
    CREATE LOGIN [$(LoginName)] WITH PASSWORD = N'$(LoginPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DbName)];
GO

USE [$(DbName)];
GO

IF USER_ID(N'$(LoginName)') IS NULL
    CREATE USER [$(LoginName)] FOR LOGIN [$(LoginName)];
GO

-- Schema changes plus data access needed by the migration history table and data-fix migrations.
ALTER ROLE db_ddladmin ADD MEMBER [$(LoginName)];
ALTER ROLE db_datareader ADD MEMBER [$(LoginName)];
ALTER ROLE db_datawriter ADD MEMBER [$(LoginName)];
GO
