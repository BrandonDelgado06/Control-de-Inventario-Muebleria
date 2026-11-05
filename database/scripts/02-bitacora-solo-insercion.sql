/*
  RF-03: la bitácora no puede modificarse.
  Ejecutar DESPUÉS de aplicar la migración inicial (cuando ya existe la tabla BITACORA).
*/
USE MuebleriaInventario;
GO
DENY UPDATE, DELETE ON dbo.BITACORA TO muebleria_app;
GO
