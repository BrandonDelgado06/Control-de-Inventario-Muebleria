using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Infrastructure.Backup;

/// <summary>
/// Respaldo y restauración con los comandos nativos de SQL Server (RF-38, RF-39).
/// Los archivos se guardan en la carpeta configurada en Respaldos:Carpeta o, si no se configura,
/// en la carpeta de respaldos de la instancia (SQL Server ya tiene permiso de escritura ahí).
/// El historial se lee de msdb, así no se pierde al restaurar una copia anterior.
/// </summary>
public class RespaldosSqlServer(IConfiguration config) : IRespaldos
{
    private readonly string _conexion = config.GetConnectionString("Inventario")!;
    private string BaseDatos => new SqlConnectionStringBuilder(_conexion).InitialCatalog;

    private SqlConnection Maestra()
    {
        var b = new SqlConnectionStringBuilder(_conexion) { InitialCatalog = "master", Pooling = false };
        return new SqlConnection(b.ConnectionString);
    }

    public async Task<string> CarpetaAsync(CancellationToken ct)
    {
        var configurada = config["Respaldos:Carpeta"];
        if (!string.IsNullOrWhiteSpace(configurada)) return configurada.TrimEnd('\\');
        await using var cn = Maestra();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260))", cn);
        return ((string)(await cmd.ExecuteScalarAsync(ct))!).TrimEnd('\\');
    }

    public async Task<RespaldoInfo> GenerarAsync(string descripcion, CancellationToken ct)
    {
        // Nombre único: dos respaldos en el mismo segundo (por ejemplo, el automático antes de restaurar)
        // no pueden compartir archivo, porque WITH INIT sobrescribiría el anterior.
        var archivo = Path.Combine(await CarpetaAsync(ct), $"inventario_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.bak");
        await using var cn = Maestra();
        await cn.OpenAsync(ct);
        // El nombre de la base es fijo (de la configuración), pero igual se protege con QUOTENAME.
        var sql = $"""
            DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) +
                N' TO DISK = @archivo WITH INIT, CHECKSUM, NAME = @nombre, DESCRIPTION = @descripcion';
            EXEC sp_executesql @sql, N'@archivo nvarchar(260), @nombre nvarchar(128), @descripcion nvarchar(255)',
                @archivo = @archivo, @nombre = @nombre, @descripcion = @descripcion;
            """;
        await using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 600 })
        {
            cmd.Parameters.AddWithValue("@db", BaseDatos);
            cmd.Parameters.AddWithValue("@archivo", archivo);
            cmd.Parameters.AddWithValue("@nombre", $"Inventario Mueblería {DateTime.Now:yyyy-MM-dd HH:mm}");
            cmd.Parameters.AddWithValue("@descripcion", descripcion);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return (await ListarAsync(ct)).FirstOrDefault(r => string.Equals(r.Archivo, archivo, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("SQL Server no registró el respaldo.");
    }

    public async Task<List<RespaldoInfo>> ListarAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (100) bs.backup_set_id, bmf.physical_device_name, bs.backup_finish_date,
                   CAST(bs.backup_size AS bigint), bs.description
            FROM msdb.dbo.backupset bs
            JOIN msdb.dbo.backupmediafamily bmf ON bmf.media_set_id = bs.media_set_id
            WHERE bs.database_name = @db AND bs.type = 'D' AND bmf.device_type = 2
            ORDER BY bs.backup_finish_date DESC, bs.backup_set_id DESC   -- msdb guarda segundos: el id desempata
            """;
        var lista = new List<RespaldoInfo>();
        await using var cn = Maestra();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@db", BaseDatos);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new RespaldoInfo(r.GetInt32(0), r.GetString(1), r.GetDateTime(2), r.GetInt64(3), r.IsDBNull(4) ? null : r.GetString(4)));
        return lista;
    }

    public async Task RestaurarAsync(int idRespaldo, CancellationToken ct)
    {
        // Solo se restaura un archivo que SQL Server tiene registrado como respaldo de esta base.
        var respaldo = (await ListarAsync(ct)).FirstOrDefault(r => r.Id == idRespaldo)
            ?? throw new NoEncontradoException("Respaldo", idRespaldo);

        SqlConnection.ClearAllPools();
        await using var cn = Maestra();
        await cn.OpenAsync(ct);
        const string sql = """
            DECLARE @q sysname = QUOTENAME(@db);
            DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + @q + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;';
            EXEC (@sql);
            BEGIN TRY
                SET @sql = N'RESTORE DATABASE ' + @q + N' FROM DISK = @archivo WITH REPLACE;';
                EXEC sp_executesql @sql, N'@archivo nvarchar(260)', @archivo = @archivo;
            END TRY
            BEGIN CATCH
                SET @sql = N'ALTER DATABASE ' + @q + N' SET MULTI_USER;';
                EXEC (@sql);
                THROW;
            END CATCH
            SET @sql = N'ALTER DATABASE ' + @q + N' SET MULTI_USER;';
            EXEC (@sql);
            """;
        await using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 900 })
        {
            cmd.Parameters.AddWithValue("@db", BaseDatos);
            cmd.Parameters.AddWithValue("@archivo", respaldo.Archivo);
            try { await cmd.ExecuteNonQueryAsync(ct); }
            catch (SqlException ex) { throw new DomainException($"No se pudo restaurar el respaldo: {ex.Message}"); }
        }
        SqlConnection.ClearAllPools();
    }
}
