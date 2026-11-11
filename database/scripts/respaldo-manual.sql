/*
  Respaldo manual de emergencia (equivale a RF-38 si la aplicación no está disponible).
  sqlcmd -S . -E -C -i respaldo-manual.sql -v CARPETA="C:\Respaldos\Muebleria"
  Después, copiar el .bak a una USB o disco externo (RNF-07).
*/
DECLARE @archivo NVARCHAR(400) =
    N'$(CARPETA)\inventario_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmm') + N'.bak';

BACKUP DATABASE MuebleriaInventario
    TO DISK = @archivo
    WITH INIT, CHECKSUM, NAME = N'Respaldo manual MuebleriaInventario';

RESTORE VERIFYONLY FROM DISK = @archivo WITH CHECKSUM;
PRINT N'Respaldo creado: ' + @archivo;
