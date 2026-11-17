-- Creates the least-privilege RUNTIME principal used by the API. Data access only: it can read and
-- write rows but cannot change schema, manage users/roles, or back up/restore. It is NOT db_owner.
--
-- Run AFTER migrations have been applied (and again after a restore into a new database), as a
-- sysadmin with sqlcmd. Values are read from environment variables (see 01-create-migration-principal.sql):
--   PP_DB_NAME, PP_RUNTIME_LOGIN (e.g. pp_app), PP_RUNTIME_PASSWORD
--
--   sqlcmd -S .\SQLEXPRESS -E -b -i deploy\sql\02-create-runtime-principal.sql
:setvar DbName "$(PP_DB_NAME)"
:setvar LoginName "$(PP_RUNTIME_LOGIN)"
:setvar LoginPassword "$(PP_RUNTIME_PASSWORD)"
:on error exit

IF SUSER_ID(N'$(LoginName)') IS NULL
    CREATE LOGIN [$(LoginName)] WITH PASSWORD = N'$(LoginPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DbName)];
GO

USE [$(DbName)];
GO

-- After a restore the database user may exist but be orphaned from a new login; re-map it.
IF USER_ID(N'$(LoginName)') IS NULL
    CREATE USER [$(LoginName)] FOR LOGIN [$(LoginName)];
ELSE
    ALTER USER [$(LoginName)] WITH LOGIN = [$(LoginName)];
GO

ALTER ROLE db_datareader ADD MEMBER [$(LoginName)];
ALTER ROLE db_datawriter ADD MEMBER [$(LoginName)];

-- Explicitly deny what the runtime must never do, so a later broad grant cannot widen it silently.
DENY ALTER ANY USER TO [$(LoginName)];
DENY ALTER ANY ROLE TO [$(LoginName)];
DENY CREATE TABLE TO [$(LoginName)];
DENY BACKUP DATABASE TO [$(LoginName)];
GO

-- Verification (should list only db_datareader and db_datawriter; must not list db_owner).
SELECT r.name AS RoleName
FROM sys.database_role_members AS m
JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals AS u ON u.principal_id = m.member_principal_id
WHERE u.name = N'$(LoginName)';
GO
