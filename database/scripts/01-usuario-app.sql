/*
  Crea la base de datos y el usuario de la aplicación con permisos mínimos.
  Ejecutar UNA vez como administrador de SQL Server (sqlcmd -S . -E -C -i 01-usuario-app.sql).
  Cambiar la contraseña antes de ejecutarlo y guardarla solo en appsettings.Production.json del servidor.
*/
IF DB_ID(N'MuebleriaInventario') IS NULL
    CREATE DATABASE MuebleriaInventario;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'muebleria_app')
    CREATE LOGIN muebleria_app WITH PASSWORD = N'CAMBIAR_ESTA_CONTRASENA', CHECK_POLICY = ON;
GO

USE MuebleriaInventario;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'muebleria_app')
    CREATE USER muebleria_app FOR LOGIN muebleria_app;
GO

ALTER ROLE db_datareader ADD MEMBER muebleria_app;
ALTER ROLE db_datawriter ADD MEMBER muebleria_app;
ALTER ROLE db_ddladmin   ADD MEMBER muebleria_app;   -- para aplicar migraciones
ALTER ROLE db_backupoperator ADD MEMBER muebleria_app; -- RF-38
GO
-- Restaurar (RF-39) requiere permisos de servidor: dbcreator.
ALTER SERVER ROLE dbcreator ADD MEMBER muebleria_app;
GO
