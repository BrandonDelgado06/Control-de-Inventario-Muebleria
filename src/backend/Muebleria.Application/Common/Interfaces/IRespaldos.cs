namespace Muebleria.Application.Common.Interfaces;

/// <summary>Copia de seguridad registrada por SQL Server (historial de msdb, que sobrevive a una restauración).</summary>
public record RespaldoInfo(int Id, string Archivo, DateTime Fecha, long TamanoBytes, string? Descripcion);

/// <summary>RF-38 y RF-39. Lo implementa la infraestructura con BACKUP y RESTORE DATABASE.</summary>
public interface IRespaldos
{
    /// <summary>Carpeta donde SQL Server guarda los respaldos.</summary>
    Task<string> CarpetaAsync(CancellationToken ct);
    Task<RespaldoInfo> GenerarAsync(string descripcion, CancellationToken ct);
    Task<List<RespaldoInfo>> ListarAsync(CancellationToken ct);
    /// <summary>Reemplaza la base de datos actual por la copia indicada.</summary>
    Task RestaurarAsync(int idRespaldo, CancellationToken ct);
}

/// <summary>Exporta un reporte a PDF o Excel (RF-35).</summary>
public interface IExportadorReportes
{
    byte[] Pdf(Features.Reportes.ReporteDto reporte);
    byte[] Excel(Features.Reportes.ReporteDto reporte);
}
