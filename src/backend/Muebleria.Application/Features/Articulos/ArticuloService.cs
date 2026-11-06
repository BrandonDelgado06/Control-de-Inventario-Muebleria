using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Categorias;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Articulos;

public record ArticuloResumenDto(
    int Id, string Codigo, string Nombre, int CategoriaId, string Categoria, TipoInventario TipoInventario,
    int UbicacionId, string Ubicacion, string UnidadMedida, decimal StockActual, decimal StockMinimo,
    EstadoArticulo Estado, string? Lote, DateOnly? FechaVencimiento, bool BajoMinimo, bool Activo);

public record ValorCampoDto(int CampoId, string Campo, TipoDato TipoDato, bool Obligatorio, IReadOnlyList<string> Opciones, string? Valor);

public record ArticuloDetalleDto(
    ArticuloResumenDto Articulo, string? Presentacion, IReadOnlyList<string> Alias, IReadOnlyList<ValorCampoDto> Campos);

public record ArticuloFiltro(
    string? Texto = null, int? CategoriaId = null, int? UbicacionId = null, TipoInventario? Tipo = null,
    bool BajoMinimo = false, bool IncluirInactivos = false, int Pagina = 1, int Tamano = 25);

public record ArticuloRequest(
    [Required(ErrorMessage = "El código es obligatorio."), MaxLength(30)] string Codigo,
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100)] string Nombre,
    [Range(1, int.MaxValue, ErrorMessage = "Elige una categoría.")] int CategoriaId,
    [Range(1, int.MaxValue, ErrorMessage = "Elige una ubicación.")] int UbicacionId,
    [Required(ErrorMessage = "La unidad de medida es obligatoria."), MaxLength(20)] string UnidadMedida,
    [Range(0, 99999999, ErrorMessage = "El stock mínimo no puede ser negativo.")] decimal StockMinimo,
    EstadoArticulo Estado,
    [MaxLength(30)] string? Lote,
    DateOnly? FechaVencimiento,
    [MaxLength(20)] string? Presentacion,
    IReadOnlyList<string>? Alias,
    IReadOnlyDictionary<int, string?>? Valores,
    [Range(0, 99999999, ErrorMessage = "La existencia inicial no puede ser negativa.")] decimal? StockInicial);

public class ArticuloService(IAppDbContext db, IUsuarioActual actual)
{
    public static readonly string[] UnidadesMedida = ["pieza", "metro", "plancha", "kilo", "litro", "galón", "caja", "rollo", "par", "juego"];

    public static readonly Expression<Func<Articulo, ArticuloResumenDto>> AResumen = a => new ArticuloResumenDto(
        a.Id, a.Codigo, a.Nombre, a.CategoriaId, a.Categoria.Nombre, a.Categoria.TipoInventario,
        a.UbicacionId, a.Ubicacion.Nombre, a.UnidadMedida, a.StockActual, a.StockMinimo,
        a.Estado, a.Lote, a.FechaVencimiento, a.StockActual <= a.StockMinimo, a.Activo);

    /// <summary>RF-09 y RF-15: búsqueda por código, nombre o alias, con filtros.</summary>
    public Task<Paginado<ArticuloResumenDto>> BuscarAsync(ArticuloFiltro f, CancellationToken ct)
    {
        var q = db.Articulos.AsQueryable();
        if (!f.IncluirInactivos) q = q.Where(a => a.Activo);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(a => a.Codigo.Contains(t) || a.Nombre.Contains(t) || a.Alias.Any(al => al.Alias.Contains(t)));
        }
        if (f.CategoriaId is { } c) q = q.Where(a => a.CategoriaId == c);
        if (f.UbicacionId is { } u) q = q.Where(a => a.UbicacionId == u);
        if (f.Tipo is { } tipo) q = q.Where(a => a.Categoria.TipoInventario == tipo);
        if (f.BajoMinimo) q = q.Where(a => a.StockActual <= a.StockMinimo);

        return q.OrderBy(a => a.Nombre).Select(AResumen).PaginarAsync(f.Pagina, f.Tamano, ct);
    }

    public async Task<ArticuloDetalleDto> ObtenerAsync(int id, CancellationToken ct)
    {
        var resumen = await db.Articulos.Where(a => a.Id == id).Select(AResumen).FirstOrDefaultAsync(ct)
            ?? throw new NoEncontradoException("Artículo", id);
        var extra = await db.Articulos.Where(a => a.Id == id)
            .Select(a => new { a.Presentacion, Alias = a.Alias.Select(x => x.Alias).ToList() })
            .FirstAsync(ct);
        var campos = await db.CamposPersonalizados.Where(c => c.CategoriaId == resumen.CategoriaId).OrderBy(c => c.Orden).ToListAsync(ct);
        var valores = await db.ArticuloValores.Where(v => v.ArticuloId == id).ToDictionaryAsync(v => v.CampoId, v => v.Valor, ct);

        var camposDto = campos.Select(c =>
        {
            var dto = CategoriaService.ACampoDto(c);
            return new ValorCampoDto(c.Id, c.Nombre, c.TipoDato, c.Obligatorio, dto.Opciones, valores.GetValueOrDefault(c.Id));
        }).ToList();

        return new ArticuloDetalleDto(resumen, extra.Presentacion, extra.Alias, camposDto);
    }

    /// <summary>RF-08: registro de artículo. Si trae existencia inicial, se registra como una entrada.</summary>
    public async Task<ArticuloDetalleDto> CrearAsync(ArticuloRequest req, CancellationToken ct)
    {
        var codigo = req.Codigo.Trim().ToUpperInvariant();
        if (await db.Articulos.AnyAsync(a => a.Codigo == codigo, ct))
            throw new DomainException($"El código \"{codigo}\" ya está registrado.");

        var articulo = new Articulo { Codigo = codigo };
        await AplicarDatosAsync(articulo, req, ct);
        db.Articulos.Add(articulo);

        if (req.StockInicial is > 0 and var inicial)
        {
            var (anterior, nuevo) = articulo.AplicarMovimiento(TipoMovimiento.Entrada, inicial);
            db.Movimientos.Add(new Movimiento
            {
                Articulo = articulo,
                UsuarioId = actual.Id ?? throw new DomainException("No hay un usuario en sesión."),
                Tipo = TipoMovimiento.Entrada,
                Cantidad = inicial,
                StockAnterior = anterior,
                StockNuevo = nuevo,
                Motivo = "Existencia inicial",
                FechaHora = DateTime.Now,
            });
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(articulo.Id, ct);
    }

    /// <summary>RF-09: edición. La existencia no se edita aquí; cambia solo con movimientos.</summary>
    public async Task<ArticuloDetalleDto> ActualizarAsync(int id, ArticuloRequest req, CancellationToken ct)
    {
        var articulo = await db.Articulos.Include(a => a.Alias).Include(a => a.Valores).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NoEncontradoException("Artículo", id);
        var codigo = req.Codigo.Trim().ToUpperInvariant();
        if (await db.Articulos.AnyAsync(a => a.Codigo == codigo && a.Id != id, ct))
            throw new DomainException($"El código \"{codigo}\" ya está registrado.");

        var estadoAnterior = articulo.Estado;
        articulo.Codigo = codigo;
        await AplicarDatosAsync(articulo, req, ct);

        // RF-11: historial de cambios de estado.
        if (estadoAnterior != articulo.Estado)
        {
            db.HistorialEstados.Add(new HistorialEstado
            {
                ArticuloId = id,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = articulo.Estado,
                UsuarioId = actual.Id!.Value,
                FechaHora = DateTime.Now,
            });
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>Desactiva o reactiva sin borrar el historial (RF-09).</summary>
    public async Task CambiarActivoAsync(int id, bool activo, CancellationToken ct)
    {
        var articulo = await db.Articulos.FindAsync([id], ct) ?? throw new NoEncontradoException("Artículo", id);
        articulo.Activo = activo;
        await db.SaveChangesAsync(ct);
    }

    private async Task AplicarDatosAsync(Articulo a, ArticuloRequest req, CancellationToken ct)
    {
        var categoria = await db.Categorias.Include(c => c.Campos).FirstOrDefaultAsync(c => c.Id == req.CategoriaId, ct)
            ?? throw new DomainException("La categoría elegida no existe.");
        var ubicacion = await db.Ubicaciones.FindAsync([req.UbicacionId], ct)
            ?? throw new DomainException("La ubicación elegida no existe.");
        if (!ubicacion.Activo && a.UbicacionId != ubicacion.Id)
            throw new DomainException($"La ubicación \"{ubicacion.Nombre}\" está desactivada.");

        if (categoria.TipoInventario == TipoInventario.Acabado && req.FechaVencimiento is null)
            throw new DomainException("Los productos de acabado necesitan fecha de vencimiento (RF-12).");

        a.Nombre = req.Nombre.Trim();
        a.CategoriaId = categoria.Id;
        a.UbicacionId = ubicacion.Id;
        a.UnidadMedida = req.UnidadMedida.Trim().ToLowerInvariant();
        a.StockMinimo = req.StockMinimo;
        a.Estado = req.Estado;
        a.Lote = string.IsNullOrWhiteSpace(req.Lote) ? null : req.Lote.Trim();
        a.FechaVencimiento = req.FechaVencimiento;
        a.Presentacion = string.IsNullOrWhiteSpace(req.Presentacion) ? null : req.Presentacion.Trim();

        // Alias: se reemplaza la lista completa.
        var alias = (req.Alias ?? []).Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        a.Alias.RemoveAll(x => !alias.Contains(x.Alias, StringComparer.OrdinalIgnoreCase));
        foreach (var nuevo in alias.Where(n => !a.Alias.Any(x => x.Alias.Equals(n, StringComparison.OrdinalIgnoreCase))))
            a.Alias.Add(new ArticuloAlias { Alias = nuevo });

        // Campos personalizados de la categoría.
        var valores = req.Valores ?? new Dictionary<int, string?>();
        a.Valores.RemoveAll(v => !categoria.Campos.Any(c => c.Id == v.CampoId));
        foreach (var campo in categoria.Campos)
        {
            var valor = valores.GetValueOrDefault(campo.Id)?.Trim();
            if (string.IsNullOrEmpty(valor))
            {
                if (campo.Obligatorio) throw new DomainException($"El campo \"{campo.Nombre}\" es obligatorio.");
                a.Valores.RemoveAll(v => v.CampoId == campo.Id);
                continue;
            }
            ValidarValor(campo, valor);
            var existente = a.Valores.FirstOrDefault(v => v.CampoId == campo.Id);
            if (existente is null) a.Valores.Add(new ArticuloValorCampo { CampoId = campo.Id, Valor = valor });
            else existente.Valor = valor;
        }
    }

    private static void ValidarValor(CampoPersonalizado campo, string valor)
    {
        var ok = campo.TipoDato switch
        {
            TipoDato.Numero => decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            TipoDato.Fecha => DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out _),
            TipoDato.Lista => (campo.Opciones ?? "").Split('|').Contains(valor),
            _ => valor.Length <= 200,
        };
        if (!ok) throw new DomainException($"El valor \"{valor}\" no es válido para el campo \"{campo.Nombre}\".");
    }
}
