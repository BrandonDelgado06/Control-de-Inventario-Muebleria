using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Reportes;

/// <summary>Tipo de dato de cada columna, para alinear y dar formato en pantalla, PDF y Excel.</summary>
public enum TipoColumna { Texto, Numero, Fecha, FechaHora }

public record ColumnaReporte(string Titulo, TipoColumna Tipo = TipoColumna.Texto);

/// <summary>Reporte listo para mostrar o exportar: título, filtros aplicados, columnas y filas.</summary>
public record ReporteDto(
    string Clave, string Titulo, string Descripcion, IReadOnlyList<string> Filtros,
    IReadOnlyList<ColumnaReporte> Columnas, IReadOnlyList<object?[]> Filas, DateTime Generado, string GeneradoPor);

public record ReporteFiltro(
    DateOnly? Desde = null, DateOnly? Hasta = null, int? CategoriaId = null, int? UbicacionId = null,
    int? ArticuloId = null, int? UsuarioId = null, TipoMovimiento? Tipo = null);

public record TipoReporteDto(string Clave, string Titulo, string Descripcion, string Requerimiento, IReadOnlyList<string> Filtros);

/// <summary>RF-33 a RF-35.</summary>
public class ReporteService(IAppDbContext db, IUsuarioActual actual, IExportadorReportes exportador)
{
    public static readonly IReadOnlyList<TipoReporteDto> Tipos =
    [
        new("existencias", "Existencias actuales", "Lo que hay de cada artículo, con su mínimo y su estado.", "RF-33", ["categoria", "ubicacion"]),
        new("movimientos", "Movimientos por período", "Entradas, salidas, ajustes, bajas y mermas en un rango de fechas.", "RF-33", ["periodo", "categoria", "articulo", "usuario", "tipo"]),
        new("consumo", "Consumo por categoría", "Material que salió del inventario en el período, agrupado por categoría.", "RF-34", ["periodo", "categoria"]),
        new("herramientas-fuera", "Herramientas fuera del taller", "Herramientas de instalación que no han regresado.", "RF-34", []),
    ];

    /// <summary>Para el filtro "usuario": todos, también los desactivados (sus movimientos siguen en el historial).</summary>
    public Task<List<Instalacion.PersonaDto>> ResponsablesAsync(CancellationToken ct) =>
        db.Usuarios.OrderBy(u => u.Nombre).Select(u => new Instalacion.PersonaDto(u.Id, u.Nombre)).ToListAsync(ct);

    public async Task<ReporteDto> GenerarAsync(string clave, ReporteFiltro f, CancellationToken ct)
    {
        var usuario = await db.Usuarios.Where(u => u.Id == actual.Id).Select(u => u.Nombre).FirstOrDefaultAsync(ct) ?? "";
        var filtros = await DescribirFiltrosAsync(clave, f, ct);
        return clave switch
        {
            "existencias" => await ExistenciasAsync(f, filtros, usuario, ct),
            "movimientos" => await MovimientosAsync(f, filtros, usuario, ct),
            "consumo" => await ConsumoAsync(f, filtros, usuario, ct),
            "herramientas-fuera" => await HerramientasFueraAsync(filtros, usuario, ct),
            _ => throw new DomainException($"No existe el reporte \"{clave}\"."),
        };
    }

    /// <summary>RF-35: exporta a PDF o Excel y deja constancia en la bitácora.</summary>
    public async Task<(byte[] Contenido, string Tipo, string Archivo)> ExportarAsync(string clave, string formato, ReporteFiltro f, CancellationToken ct)
    {
        var r = await GenerarAsync(clave, f, ct);
        var nombre = $"reporte-{clave}-{DateTime.Now:yyyyMMdd-HHmm}";
        var salida = formato switch
        {
            "pdf" => (exportador.Pdf(r), "application/pdf", nombre + ".pdf"),
            "xlsx" => (exportador.Excel(r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre + ".xlsx"),
            _ => throw new DomainException("El formato debe ser pdf o xlsx."),
        };
        db.Bitacoras.Add(new Bitacora
        {
            UsuarioId = actual.Id!.Value, Modulo = "Reportes", FechaHora = DateTime.Now,
            Accion = $"Exportar \"{r.Titulo}\" a {(formato == "pdf" ? "PDF" : "Excel")}",
        });
        await db.SaveChangesAsync(ct);
        return salida;
    }

    private (DateTime desde, DateTime hasta) Periodo(ReporteFiltro f)
    {
        var hasta = f.Hasta ?? DateOnly.FromDateTime(DateTime.Today);
        var desde = f.Desde ?? hasta.AddDays(-29);
        if (desde > hasta) throw new DomainException("La fecha inicial no puede ser posterior a la final.");
        return (desde.ToDateTime(TimeOnly.MinValue), hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));
    }

    // ------------------------------------------------------------------ RF-33
    private async Task<ReporteDto> ExistenciasAsync(ReporteFiltro f, List<string> filtros, string usuario, CancellationToken ct)
    {
        var q = db.Articulos.Where(a => a.Activo);
        if (f.CategoriaId is { } c) q = q.Where(a => a.CategoriaId == c);
        if (f.UbicacionId is { } u) q = q.Where(a => a.UbicacionId == u);
        var filas = await q.OrderBy(a => a.Categoria.Nombre).ThenBy(a => a.Nombre)
            .Select(a => new { a.Codigo, a.Nombre, Cat = a.Categoria.Nombre, Ubi = a.Ubicacion.Nombre, a.UnidadMedida, a.StockActual, a.StockMinimo, a.Estado })
            .ToListAsync(ct);
        return new ReporteDto("existencias", "Existencias actuales", $"{filas.Count} artículos activos.", filtros,
            [new("Código"), new("Artículo"), new("Categoría"), new("Ubicación"), new("Unidad"),
             new("Existencia", TipoColumna.Numero), new("Mínimo", TipoColumna.Numero), new("Estado"), new("Bajo mínimo")],
            filas.Select(a => new object?[] { a.Codigo, a.Nombre, a.Cat, a.Ubi, a.UnidadMedida, a.StockActual, a.StockMinimo,
                NombreEstado(a.Estado), a.StockActual <= a.StockMinimo ? "Sí" : "No" }).ToList(),
            DateTime.Now, usuario);
    }

    private async Task<ReporteDto> MovimientosAsync(ReporteFiltro f, List<string> filtros, string usuario, CancellationToken ct)
    {
        var (desde, hasta) = Periodo(f);
        var q = db.Movimientos.Where(m => m.FechaHora >= desde && m.FechaHora < hasta);
        if (f.CategoriaId is { } c) q = q.Where(m => m.Articulo.CategoriaId == c);
        if (f.ArticuloId is { } a) q = q.Where(m => m.ArticuloId == a);
        if (f.UsuarioId is { } u) q = q.Where(m => m.UsuarioId == u);
        if (f.Tipo is { } t) q = q.Where(m => m.Tipo == t);
        var filas = await q.OrderBy(m => m.FechaHora)
            .Select(m => new { m.FechaHora, m.Tipo, m.Articulo.Codigo, m.Articulo.Nombre, m.Cantidad, m.Articulo.UnidadMedida, m.StockAnterior, m.StockNuevo, m.Motivo, Usuario = m.Usuario.Nombre })
            .ToListAsync(ct);
        return new ReporteDto("movimientos", "Movimientos por período", $"{filas.Count} movimientos registrados.", filtros,
            [new("Fecha y hora", TipoColumna.FechaHora), new("Tipo"), new("Código"), new("Artículo"), new("Cantidad", TipoColumna.Numero),
             new("Unidad"), new("Existencia anterior", TipoColumna.Numero), new("Existencia nueva", TipoColumna.Numero), new("Motivo"), new("Responsable")],
            filas.Select(m => new object?[] { m.FechaHora, m.Tipo.ToString(), m.Codigo, m.Nombre, m.Cantidad, m.UnidadMedida, m.StockAnterior, m.StockNuevo, m.Motivo, m.Usuario }).ToList(),
            DateTime.Now, usuario);
    }

    // ------------------------------------------------------------------ RF-34
    private async Task<ReporteDto> ConsumoAsync(ReporteFiltro f, List<string> filtros, string usuario, CancellationToken ct)
    {
        var (desde, hasta) = Periodo(f);
        var tipos = new[] { TipoMovimiento.Salida, TipoMovimiento.Merma };
        // Las herramientas de instalación salen prestadas y vuelven: no cuentan como consumo.
        var q = db.Movimientos.Where(m => m.FechaHora >= desde && m.FechaHora < hasta && tipos.Contains(m.Tipo)
            && m.Articulo.Categoria.TipoInventario != TipoInventario.Instalacion);
        if (f.CategoriaId is { } c) q = q.Where(m => m.Articulo.CategoriaId == c);
        var filas = await q
            .GroupBy(m => new { Cat = m.Articulo.Categoria.Nombre, m.Articulo.Codigo, m.Articulo.Nombre, m.Articulo.UnidadMedida })
            .Select(g => new { g.Key.Cat, g.Key.Codigo, g.Key.Nombre, g.Key.UnidadMedida, Salidas = g.Count(), Total = g.Sum(m => m.Cantidad) })
            .OrderBy(x => x.Cat).ThenByDescending(x => x.Total)
            .ToListAsync(ct);
        var categorias = filas.Select(x => x.Cat).Distinct().Count();
        return new ReporteDto("consumo", "Consumo por categoría", $"{filas.Count} artículos con salidas en {categorias} categorías.", filtros,
            [new("Categoría"), new("Código"), new("Artículo"), new("Unidad"), new("Salidas", TipoColumna.Numero), new("Cantidad consumida", TipoColumna.Numero)],
            filas.Select(x => new object?[] { x.Cat, x.Codigo, x.Nombre, x.UnidadMedida, x.Salidas, x.Total }).ToList(),
            DateTime.Now, usuario);
    }

    private async Task<ReporteDto> HerramientasFueraAsync(List<string> filtros, string usuario, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var filas = await db.SalidasHerramienta.Where(s => s.FechaDevolucion == null).OrderBy(s => s.FechaPrevista)
            .Select(s => new { s.Articulo.Codigo, s.Articulo.Nombre, Responsable = s.Responsable.Nombre, s.Destino, s.FechaSalida, s.FechaPrevista })
            .ToListAsync(ct);
        return new ReporteDto("herramientas-fuera", "Herramientas fuera del taller", $"{filas.Count} herramientas sin devolver.", filtros,
            [new("Código"), new("Herramienta"), new("Responsable"), new("Destino"), new("Salida", TipoColumna.Fecha),
             new("Devolución prevista", TipoColumna.Fecha), new("Días de retraso", TipoColumna.Numero)],
            filas.Select(s => new object?[] { s.Codigo, s.Nombre, s.Responsable, s.Destino, s.FechaSalida, s.FechaPrevista,
                Math.Max(0, hoy.DayNumber - s.FechaPrevista.DayNumber) }).ToList(),
            DateTime.Now, usuario);
    }

    private async Task<List<string>> DescribirFiltrosAsync(string clave, ReporteFiltro f, CancellationToken ct)
    {
        var l = new List<string>();
        if (clave is "movimientos" or "consumo")
        {
            var hasta = f.Hasta ?? DateOnly.FromDateTime(DateTime.Today);
            l.Add($"Período: {(f.Desde ?? hasta.AddDays(-29)):dd/MM/yyyy} al {hasta:dd/MM/yyyy}");
        }
        if (f.CategoriaId is { } c) l.Add("Categoría: " + await db.Categorias.Where(x => x.Id == c).Select(x => x.Nombre).FirstOrDefaultAsync(ct));
        if (f.UbicacionId is { } u && clave == "existencias") l.Add("Ubicación: " + await db.Ubicaciones.Where(x => x.Id == u).Select(x => x.Nombre).FirstOrDefaultAsync(ct));
        if (f.ArticuloId is { } a && clave == "movimientos") l.Add("Artículo: " + await db.Articulos.Where(x => x.Id == a).Select(x => x.Codigo + " " + x.Nombre).FirstOrDefaultAsync(ct));
        if (f.UsuarioId is { } us && clave == "movimientos") l.Add("Responsable: " + await db.Usuarios.Where(x => x.Id == us).Select(x => x.Nombre).FirstOrDefaultAsync(ct));
        if (f.Tipo is { } t && clave == "movimientos") l.Add("Tipo: " + t);
        return l;
    }

    private static string NombreEstado(EstadoArticulo e) => e switch
    {
        EstadoArticulo.Danada => "Dañada",
        EstadoArticulo.DeBaja => "De baja",
        _ => e.ToString(),
    };
}
