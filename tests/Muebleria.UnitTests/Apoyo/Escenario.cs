using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Infrastructure.Persistence;

namespace Muebleria.UnitTests.Apoyo;

/// <summary>Usuario en sesión que se puede cambiar dentro de una prueba.</summary>
public class UsuarioFijo : IUsuarioActual
{
    public int? Id { get; set; }
}

/// <summary>Respaldos simulados: las alertas solo necesitan la fecha del último.</summary>
public class RespaldosFalsos : IRespaldos
{
    public List<RespaldoInfo> Lista { get; } = [];
    public Task<string> CarpetaAsync(CancellationToken ct) => Task.FromResult(@"C:\Respaldos");
    public Task<RespaldoInfo> GenerarAsync(string descripcion, CancellationToken ct)
    {
        var r = new RespaldoInfo(Lista.Count + 1, $@"C:\Respaldos\r{Lista.Count + 1}.bak", DateTime.Now, 1024, descripcion);
        Lista.Insert(0, r);
        return Task.FromResult(r);
    }
    public Task<List<RespaldoInfo>> ListarAsync(CancellationToken ct) => Task.FromResult(Lista.ToList());
    public Task RestaurarAsync(int idRespaldo, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Base de datos en memoria con los datos mínimos del taller: tres usuarios (uno por rol),
/// una ubicación, categorías de cada tipo y algunos artículos con existencia.
/// Cada escenario usa su propia base, así las pruebas no se afectan entre sí.
/// </summary>
public sealed class Escenario : IDisposable
{
    public UsuarioFijo Usuario { get; } = new();
    public AppDbContext Db { get; }

    public Usuario Admin { get; private set; } = null!;
    public Usuario Encargado { get; private set; } = null!;
    public Usuario Trabajador { get; private set; } = null!;
    public Ubicacion Bodega { get; private set; } = null!;
    public Categoria Lijas { get; private set; } = null!;
    public Categoria Acabados { get; private set; } = null!;
    public Categoria Instalacion { get; private set; } = null!;
    public Articulo Lija { get; private set; } = null!;
    public Articulo Tinte { get; private set; } = null!;
    public Articulo Taladro { get; private set; } = null!;

    public Escenario()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pruebas-{Guid.NewGuid()}")
            // La base en memoria no maneja transacciones; la regla de una sola transacción se prueba en integración.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new AppDbContext(opciones, Usuario);
        Sembrar();
    }

    private void Sembrar()
    {
        var rol = new Rol { Nombre = "Administrador" };
        Db.Roles.Add(rol);
        Admin = new Usuario { Nombre = "Ana Admin", NombreUsuario = "admin", ContrasenaHash = "x", Rol = rol };
        Encargado = new Usuario { Nombre = "María Bodega", NombreUsuario = "encargado", ContrasenaHash = "x", Rol = rol };
        Trabajador = new Usuario { Nombre = "José Taller", NombreUsuario = "trabajador", ContrasenaHash = "x", Rol = rol };
        Bodega = new Ubicacion { Nombre = "Bodega principal" };
        Lijas = new Categoria { Nombre = "Lijas", TipoInventario = TipoInventario.Herramienta };
        Acabados = new Categoria { Nombre = "Productos de acabado", TipoInventario = TipoInventario.Acabado };
        Instalacion = new Categoria { Nombre = "Herramientas de instalación", TipoInventario = TipoInventario.Instalacion };
        Db.AddRange(Admin, Encargado, Trabajador, Bodega, Lijas, Acabados, Instalacion);

        Lija = NuevoArticulo("LIJ-220", "Lija grano 220", Lijas, existencia: 20, minimo: 10);
        Tinte = NuevoArticulo("ACA-TIN", "Tinte nogal", Acabados, existencia: 3, minimo: 1, vence: DateOnly.FromDateTime(DateTime.Today).AddDays(10));
        Taladro = NuevoArticulo("INS-TAL", "Taladro inalámbrico", Instalacion, existencia: 1, minimo: 0);
        Db.SaveChanges();   // sin usuario en sesión: no se escribe bitácora
    }

    public Articulo NuevoArticulo(string codigo, string nombre, Categoria cat, decimal existencia, decimal minimo, DateOnly? vence = null)
    {
        var a = new Articulo
        {
            Codigo = codigo, Nombre = nombre, Categoria = cat, Ubicacion = Bodega, UnidadMedida = "pieza",
            StockMinimo = minimo, FechaVencimiento = vence, Lote = vence is null ? null : "L-1",
        };
        if (existencia > 0) a.AplicarMovimiento(TipoMovimiento.Entrada, existencia);
        Db.Articulos.Add(a);
        return a;
    }

    /// <summary>Simula que la persona indicada inició sesión.</summary>
    public Escenario Como(Usuario u) { Usuario.Id = u.Id; return this; }

    public void Dispose() => Db.Dispose();
}
