-- Creates the database (if missing) and the least-privilege login the API connects with.
--
--   export CMS_APP_DB_PASSWORD='<a strong password with no single quote and no "$(">'
--   sqlcmd -I -C -b -S <server> -U <administrator> -i deploy/sql/create-app-login.sql        (password in SQLCMDPASSWORD)
--
-- The password is read from the CMS_APP_DB_PASSWORD environment variable (sqlcmd scripting variable), so it never appears on a command
-- line. The login can read and write the CMS tables and nothing else: it cannot change the schema, and it has no server-level rights.
-- Migrations are applied separately by an administrator (docs/operations/database-and-migrations.md). Safe to run again: it resets the
-- application login's password to the current value of the variable, which is also how the password is rotated.
SET NOCOUNT ON;

IF DB_ID(N'Cms') IS NULL CREATE DATABASE [Cms];
GO

DECLARE @password nvarchar(128) = N'$(CMS_APP_DB_PASSWORD)';
IF LEN(@password) < 12 THROW 50000, 'CMS_APP_DB_PASSWORD must be set to a password of at least 12 characters.', 1;

DECLARE @sql nvarchar(max);
IF SUSER_ID(N'cms_app') IS NULL
    SET @sql = N'CREATE LOGIN [cms_app] WITH PASSWORD = ' + QUOTENAME(@password, '''') + N', CHECK_POLICY = ON, DEFAULT_DATABASE = [Cms];';
ELSE
    SET @sql = N'ALTER LOGIN [cms_app] WITH PASSWORD = ' + QUOTENAME(@password, '''') + N';';
EXEC (@sql);
GO

USE [Cms];
IF USER_ID(N'cms_app') IS NULL CREATE USER [cms_app] FOR LOGIN [cms_app] WITH DEFAULT_SCHEMA = [dbo];
ALTER ROLE db_datareader ADD MEMBER [cms_app];
ALTER ROLE db_datawriter ADD MEMBER [cms_app];
GO
PRINT 'The cms_app login is ready.';
