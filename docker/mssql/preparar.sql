:on error exit
-- Prepara la base del ensayo con dos identidades: una con DDL para las
-- migraciones y otra sin DDL para la API. Se ejecuta como administrador, una
-- sola vez, contra un SQL Server desechable; no toca Azure.
--
-- Variables (sqlcmd -v): Base, UsuarioApp, ClaveApp, UsuarioMigra, ClaveMigra.

IF DB_ID(N'$(Base)') IS NULL
    EXEC(N'CREATE DATABASE [$(Base)]');
GO

IF SUSER_ID(N'$(UsuarioMigra)') IS NULL
    EXEC(N'CREATE LOGIN [$(UsuarioMigra)] WITH PASSWORD = N''$(ClaveMigra)'', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF');
IF SUSER_ID(N'$(UsuarioApp)') IS NULL
    EXEC(N'CREATE LOGIN [$(UsuarioApp)] WITH PASSWORD = N''$(ClaveApp)'', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF');
GO

USE [$(Base)];
GO

IF USER_ID(N'$(UsuarioMigra)') IS NULL
    CREATE USER [$(UsuarioMigra)] FOR LOGIN [$(UsuarioMigra)];
IF USER_ID(N'$(UsuarioApp)') IS NULL
    CREATE USER [$(UsuarioApp)] FOR LOGIN [$(UsuarioApp)];
GO

-- El migrador cambia el esquema; la aplicación solo lee y escribe. `db_owner`
-- es el máximo de este ensayo: en Azure SQL el equivalente es el usuario que
-- aplica las migraciones, nunca el del servicio.
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS miembro
    JOIN sys.database_principals AS rol ON rol.principal_id = miembro.role_principal_id
    JOIN sys.database_principals AS usuario ON usuario.principal_id = miembro.member_principal_id
    WHERE rol.name = N'db_owner' AND usuario.name = N'$(UsuarioMigra)')
    ALTER ROLE db_owner ADD MEMBER [$(UsuarioMigra)];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS miembro
    JOIN sys.database_principals AS rol ON rol.principal_id = miembro.role_principal_id
    JOIN sys.database_principals AS usuario ON usuario.principal_id = miembro.member_principal_id
    WHERE rol.name = N'db_datareader' AND usuario.name = N'$(UsuarioApp)')
    ALTER ROLE db_datareader ADD MEMBER [$(UsuarioApp)];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS miembro
    JOIN sys.database_principals AS rol ON rol.principal_id = miembro.role_principal_id
    JOIN sys.database_principals AS usuario ON usuario.principal_id = miembro.member_principal_id
    WHERE rol.name = N'db_datawriter' AND usuario.name = N'$(UsuarioApp)')
    ALTER ROLE db_datawriter ADD MEMBER [$(UsuarioApp)];
GO
