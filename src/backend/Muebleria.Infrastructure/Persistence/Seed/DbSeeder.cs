using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;

namespace Muebleria.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos iniciales. Siempre: roles, permisos, configuración y el administrador.
/// Solo si Seed:DatosEjemplo = true (desarrollo): usuarios de prueba, ubicaciones, categorías y artículos del taller.
/// Es idempotente: no duplica nada si se ejecuta otra vez.
/// </summary>
public class DbSeeder(AppDbContext db, IHasherContrasenas hasher, IConfiguration config, ILogger<DbSeeder> log)
{
    public async Task EjecutarAsync(CancellationToken ct = default)
    {
        await SembrarSeguridadAsync(ct);
        await SembrarConfiguracionAsync(ct);
        if (config.GetValue<bool>("Seed:DatosEjemplo"))
            await SembrarEjemploAsync(ct);
    }

    private async Task SembrarSeguridadAsync(CancellationToken ct)
    {
        foreach (var (codigo, descripcion) in Permisos.Descripciones)
            if (!await db.Permisos.AnyAsync(p => p.Codigo == codigo, ct))
                db.Permisos.Add(new Permiso { Codigo = codigo, Descripcion = descripcion });
        foreach (var nombre in Roles.PermisosPorRol.Keys)
            if (!await db.Roles.AnyAsync(r => r.Nombre == nombre, ct))
                db.Roles.Add(new Rol { Nombre = nombre });
        await db.SaveChangesAsync(ct);

        // Sincroniza la matriz de permisos con la del código.
        var permisos = await db.Permisos.ToDictionaryAsync(p => p.Codigo, ct);
        foreach (var rol in await db.Roles.Include(r => r.Permisos).ToListAsync(ct))
        {
            if (!Roles.PermisosPorRol.TryGetValue(rol.Nombre, out var codigos)) continue;
            rol.Permisos.RemoveAll(rp => !codigos.Contains(permisos.Values.First(p => p.Id == rp.PermisoId).Codigo));
            foreach (var c in codigos.Where(c => !rol.Permisos.Any(rp => rp.PermisoId == permisos[c].Id)))
                rol.Permisos.Add(new RolPermiso { PermisoId = permisos[c].Id });
        }
        await db.SaveChangesAsync(ct);

        if (!await db.Usuarios.AnyAsync(ct))
        {
            var contrasena = config["Seed:AdminContrasena"];
            if (string.IsNullOrWhiteSpace(contrasena))
                throw new InvalidOperationException("Falta Seed:AdminContrasena para crear el administrador inicial.");
            await CrearUsuarioAsync("Administrador del sistema", "admin", contrasena, Roles.Administrador, ct);
            log.LogInformation("Se creó el usuario administrador inicial \"admin\".");
        }
    }

    private async Task SembrarConfiguracionAsync(CancellationToken ct)
    {
        var valores = new (string clave, string valor, string desc)[]
        {
            ("alertas.dias_vencimiento", "30", "Días de anticipación para avisar que un producto de acabado está por vencer (RF-31)."),
            ("sesion.minutos_inactividad", "30", "Minutos sin actividad antes de cerrar la sesión (RF-01)."),
        };
        foreach (var (clave, valor, desc) in valores)
            if (!await db.Configuraciones.AnyAsync(c => c.Clave == clave, ct))
                db.Configuraciones.Add(new Configuracion { Clave = clave, Valor = valor, Descripcion = desc });
        await db.SaveChangesAsync(ct);
    }

    private async Task SembrarEjemploAsync(CancellationToken ct)
    {
        await CrearUsuarioSiFaltaAsync("María Rodríguez", "encargado", config["Seed:EncargadoContrasena"], Roles.Encargado, ct);
        await CrearUsuarioSiFaltaAsync("José Pérez", "trabajador", config["Seed:TrabajadorContrasena"], Roles.Trabajador, ct);

        if (await db.Articulos.AnyAsync(ct)) return;

        var bodega = new Ubicacion { Nombre = "Bodega principal", Descripcion = "Materia prima y herrajes" };
        var estante = new Ubicacion { Nombre = "Estante de herramientas", Descripcion = "Herramientas manuales y lijas" };
        var acabados = new Ubicacion { Nombre = "Cuarto de acabados", Descripcion = "Tintes, fondos y diluyentes; ventilado" };
        var patio = new Ubicacion { Nombre = "Patio de madera", Descripcion = "Tablas y reglas bajo techo" };
        db.Ubicaciones.AddRange(bodega, estante, acabados, patio);

        var madera = Cat("Madera", TipoInventario.MateriaPrima,
            Campo("Especie", TipoDato.Lista, true, "Pino|Cedro|Laurel|Melina|Teca"),
            Campo("Medidas", TipoDato.Texto, false));
        var tableros = Cat("Tableros", TipoInventario.MateriaPrima,
            Campo("Tipo", TipoDato.Lista, true, "MDF|Melamina|Plywood"),
            Campo("Espesor (mm)", TipoDato.Numero, true));
        var herrajes = Cat("Herrajes", TipoInventario.MateriaPrima, Campo("Acabado", TipoDato.Texto, false));
        var lijas = Cat("Lijas", TipoInventario.Herramienta,
            Campo("Número de grano", TipoDato.Lista, true, "60|80|100|120|150|180|220|320|400"));
        var manuales = Cat("Herramientas manuales", TipoInventario.Herramienta);
        var productos = Cat("Productos de acabado", TipoInventario.Acabado, Campo("Color", TipoDato.Texto, false));
        var instalacion = Cat("Herramientas de instalación", TipoInventario.Instalacion);
        db.Categorias.AddRange(madera, tableros, herrajes, lijas, manuales, productos, instalacion);
        await db.SaveChangesAsync(ct);

        var admin = await db.Usuarios.FirstAsync(u => u.NombreUsuario == "admin", ct);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        string V(Categoria c, string campo) => c.Campos.First(x => x.Nombre == campo).Id.ToString();

        var articulos = new (Articulo a, decimal stock, (string campoId, string valor)[] valores, string[] alias)[]
        {
            (Art("MAD-PIN-001", "Regla de pino 1x4", madera, patio, "pieza", 20), 45, [(V(madera, "Especie"), "Pino"), (V(madera, "Medidas"), "1\" x 4\" x 4 varas")], ["regla 1x4", "pino cepillado"]),
            (Art("MAD-CED-001", "Tabla de cedro 1x12", madera, patio, "pieza", 10), 6, [(V(madera, "Especie"), "Cedro"), (V(madera, "Medidas"), "1\" x 12\" x 4 varas")], ["cedro amargo"]),
            (Art("TAB-MDF-15", "Lámina MDF 15 mm", tableros, bodega, "plancha", 5), 12, [(V(tableros, "Tipo"), "MDF"), (V(tableros, "Espesor (mm)"), "15")], ["fibrán"]),
            (Art("TAB-MEL-18", "Melamina blanca 18 mm", tableros, bodega, "plancha", 4), 3, [(V(tableros, "Tipo"), "Melamina"), (V(tableros, "Espesor (mm)"), "18")], []),
            (Art("HER-BIS-35", "Bisagra de cazoleta 35 mm", herrajes, bodega, "par", 30), 120, [], ["bisagra europea"]),
            (Art("HER-COR-45", "Corredera telescópica 45 cm", herrajes, bodega, "par", 10), 8, [], ["riel de gaveta"]),
            (Art("LIJ-080", "Lija de agua grano 80", lijas, estante, "pieza", 25), 60, [(V(lijas, "Número de grano"), "80")], []),
            (Art("LIJ-220", "Lija de agua grano 220", lijas, estante, "pieza", 25), 18, [(V(lijas, "Número de grano"), "220")], ["lija fina"]),
            (Art("HMA-FOR-01", "Formón de 1/2 pulgada", manuales, estante, "pieza", 2), 4, [], ["escoplo"]),
            (Art("ACA-TIN-NOG", "Tinte nogal", productos, acabados, "galón", 2, "galón", "L-2410", hoy.AddDays(20)), 3, [(V(productos, "Color"), "Nogal")], ["tinte café"]),
            (Art("ACA-CAT-01", "Catalizador para laca", productos, acabados, "litro", 4, "litro", "C-0915", hoy.AddDays(120)), 2, [], []),
            (Art("ACA-FON-LIJ", "Fondo lijable", productos, acabados, "galón", 2, "galón", "F-1102", hoy.AddDays(200)), 5, [], ["sellador"]),
            (Art("INS-TAL-01", "Taladro inalámbrico 20 V", instalacion, estante, "pieza", 1), 2, [], ["taladro de batería"]),
            (Art("INS-NIV-01", "Nivel de burbuja 60 cm", instalacion, estante, "pieza", 1), 3, [], []),
        };

        foreach (var (a, stock, valores, alias) in articulos)
        {
            foreach (var (campoId, valor) in valores) a.Valores.Add(new ArticuloValorCampo { CampoId = int.Parse(campoId), Valor = valor });
            foreach (var al in alias) a.Alias.Add(new ArticuloAlias { Alias = al });
            var (anterior, nuevo) = a.AplicarMovimiento(TipoMovimiento.Entrada, stock);
            db.Articulos.Add(a);
            db.Movimientos.Add(new Movimiento
            {
                Articulo = a, UsuarioId = admin.Id, Tipo = TipoMovimiento.Entrada, Cantidad = stock,
                StockAnterior = anterior, StockNuevo = nuevo, Motivo = "Existencia inicial", FechaHora = DateTime.Now.AddDays(-3),
            });
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Datos de ejemplo creados: {Total} artículos.", articulos.Length);
    }

    private static Categoria Cat(string nombre, TipoInventario tipo, params CampoPersonalizado[] campos)
    {
        var c = new Categoria { Nombre = nombre, TipoInventario = tipo };
        var orden = 0;
        foreach (var campo in campos) { campo.Orden = ++orden; c.Campos.Add(campo); }
        return c;
    }

    private static CampoPersonalizado Campo(string nombre, TipoDato tipo, bool obligatorio, string? opciones = null) =>
        new() { Nombre = nombre, TipoDato = tipo, Obligatorio = obligatorio, Opciones = opciones };

    private static Articulo Art(string codigo, string nombre, Categoria cat, Ubicacion ubi, string unidad, decimal minimo,
        string? presentacion = null, string? lote = null, DateOnly? vence = null) => new()
    {
        Codigo = codigo, Nombre = nombre, Categoria = cat, Ubicacion = ubi, UnidadMedida = unidad, StockMinimo = minimo,
        Presentacion = presentacion, Lote = lote, FechaVencimiento = vence,
    };

    private async Task CrearUsuarioSiFaltaAsync(string nombre, string usuario, string? contrasena, string rol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || await db.Usuarios.AnyAsync(u => u.NombreUsuario == usuario, ct)) return;
        await CrearUsuarioAsync(nombre, usuario, contrasena, rol, ct);
    }

    private async Task CrearUsuarioAsync(string nombre, string usuario, string contrasena, string rol, CancellationToken ct)
    {
        var rolId = await db.Roles.Where(r => r.Nombre == rol).Select(r => r.Id).FirstAsync(ct);
        db.Usuarios.Add(new Usuario
        {
            Nombre = nombre, NombreUsuario = usuario, ContrasenaHash = hasher.Hash(contrasena), RolId = rolId, FechaCreacion = DateTime.Now,
        });
        await db.SaveChangesAsync(ct);
    }
}
