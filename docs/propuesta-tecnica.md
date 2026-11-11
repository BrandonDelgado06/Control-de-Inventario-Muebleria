# Propuesta técnica: Control de Inventario Mueblería

Este documento traduce el documento del proyecto (39 RF y 9 RNF) en decisiones de arquitectura, estructura de carpetas y modelo de datos.

## 1. Restricciones que guían el diseño

| Restricción | Origen | Consecuencia técnica |
|---|---|---|
| Sin internet fijo ni nube | Limitación 1 | Todo corre en **un equipo del taller** y se usa por la red local (LAN). Nada de CDN, fuentes de Google ni servicios externos: todo va empaquetado. |
| Respaldo manual | Limitación 1, RF-38/39, RNF-07 | El módulo de respaldos es crítico: `BACKUP DATABASE` a una carpeta local y copia a USB/disco externo. |
| Nombres distintos para el mismo producto | Limitación 2 | Tabla de **alias/sinónimos** por artículo y búsqueda que los incluya. |
| Hasta 10 usuarios, respuesta ≤ 3 s | RNF-04 | Monolito sencillo, sin microservicios. Índices en código, nombre y FKs. |
| Sin instalar nada en los equipos cliente | RNF-09 | Aplicación web; el cliente solo necesita Chrome, Edge o Firefox. |

## 2. Stack

| Capa | Tecnología | Notas |
|---|---|---|
| Backend | **ASP.NET Core 10 (C# 14)**, Web API con controladores | .NET 10 es LTS. |
| Acceso a datos | **Entity Framework Core 10** + `Microsoft.EntityFrameworkCore.SqlServer` | Las consultas parametrizadas son automáticas (RNF-03). Migraciones = fuente de verdad del esquema. |
| Base de datos | **SQL Server 2022**: Developer en desarrollo y Express en el equipo del taller | Es la versión que indica el trabajo escrito. |
| Autenticación | Cookie de autenticación de ASP.NET Core (HttpOnly, SameSite=Strict, expiración deslizante de 30 min) | Cubre el cierre por inactividad de RF-01. Como la SPA se sirve desde el mismo origen, no hace falta JWT. |
| Contraseñas | `PasswordHasher<T>` de ASP.NET Core (PBKDF2) | RNF-01. Sin instalar Identity completo. |
| Autorización | Políticas por permiso (`[Authorize(Policy = "Articulos.Editar")]`) | RNF-02: se valida en el servidor, no solo se oculta el menú. |
| Validación | DataAnnotations en los DTO (backend) y reglas de negocio en los servicios; el frontend muestra los mensajes que devuelve la API | RNF-03. Los mensajes de error están en español. |
| Reportes | **QuestPDF** (PDF) y **ClosedXML** (Excel) | RF-35. Revisar la licencia de QuestPDF (gratuita para uso académico / empresas pequeñas). |
| Frontend | **React 19 + TypeScript + Vite** | |
| UI | Material UI (MUI) + MUI X DataGrid + MUI X Charts | Tablas con filtros y gráficos del panel (RF-37) sin depender de internet. |
| Estado del servidor | TanStack Query | Caché y recarga de listados. |
| Formularios | React Hook Form + Zod | |
| Ruteo | React Router | Rutas protegidas por rol. |
| Pruebas | xUnit + FluentAssertions + Testcontainers o LocalDB (backend), Vitest + Testing Library (frontend) | |
| Control de versiones | Git + GitHub (ver [CONTRIBUTING.md](../CONTRIBUTING.md)) | |

> **Nota de versiones (4 nov 2026):** el equipo de desarrollo ya tiene el SDK de .NET 10.0.403, Node.js 26 LTS y SQL Server 2022 Developer. Se decidió desarrollar con SQL Server 2022 porque la instancia tiene bases de datos de otras materias y actualizarla es riesgoso. El trabajo escrito se actualizó a 2022 (Express en el taller).

## 3. Arquitectura

```
 Equipos del taller (navegador)
            │  http://servidor-taller:5000  (LAN)
            ▼
 ┌──────────────────────────────────────────┐
 │  Muebleria.Api (ASP.NET Core / Kestrel)   │
 │  ├─ /api/*      → controladores REST      │
 │  └─ /*          → build de React (wwwroot)│
 └───────────────┬──────────────────────────┘
                 │ EF Core
                 ▼
        SQL Server 2022 (local) ──►  database/backups  ──►  USB / disco externo
```

Un **solo proceso** sirve la API y el frontend compilado. En producción se instala como **servicio de Windows** para que arranque con el equipo.

### Capas del backend (Clean Architecture simplificada)

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Muebleria.Domain` | Entidades, enums (`TipoMovimiento`, `EstadoArticulo`, `EstadoSolicitud`…) y reglas de negocio puras (p. ej. "el stock no puede quedar negativo"). | nada |
| `Muebleria.Application` | Casos de uso por módulo (`Features/<Modulo>`): servicios, DTOs, validadores e interfaces (`IAppDbContext`, `IUsuarioActual`, `IReporteService`). | Domain |
| `Muebleria.Infrastructure` | EF Core (`AppDbContext`, configuraciones, migraciones, interceptor de bitácora, datos semilla), hashing de contraseñas, PDF/Excel y respaldos. | Application |
| `Muebleria.Api` | Controladores, middleware de errores, políticas de autorización, `Program.cs` y archivos estáticos. | Application, Infrastructure |

### Decisiones clave

1. **Movimientos atómicos (RNF-06).** Cada servicio que cambia el stock (entrada, salida, ajuste, merma, baja, entrega de solicitud, toma física) abre **una transacción**: inserta el `MOVIMIENTO`, actualiza `ARTICULO.stock_actual` y guarda `stock_anterior` y `stock_nuevo`. También hay un `CHECK (stock_actual >= 0)` en la base de datos y un `rowversion` en `ARTICULO` para evitar que dos usuarios descuenten a la vez.
2. **Un solo punto de cambio de stock.** Toda modificación pasa por `MovimientoService`. Ningún otro módulo actualiza `stock_actual` directamente.
3. **Bitácora automática (RF-03).** Un `SaveChangesInterceptor` de EF registra cada creación, edición y eliminación con usuario, módulo y acción. La tabla `BITACORA` solo admite inserciones: el usuario de base de datos de la app no tiene permisos `UPDATE` ni `DELETE` sobre ella.
4. **Borrado lógico.** Usuarios, artículos, ubicaciones y categorías se **desactivan** (`activo = 0`), nunca se eliminan (RF-09, RF-27).
5. **Alertas calculadas, no almacenadas (RF-30 a RF-32).** Se obtienen con consultas o vistas (`stock_actual <= stock_minimo`, `fecha_vencimiento <= hoy + N días`, `fecha_prevista < hoy AND fecha_devolucion IS NULL`). Así nunca quedan desactualizadas.
6. **Campos personalizados (RF-07, RNF-08).** Se usa el modelo EAV: la categoría define sus campos y cada artículo guarda sus valores. Permite agregar "grano", "grosor" o "color" sin tocar código.
7. **Respaldo (RF-38/39).** El backend ejecuta `BACKUP DATABASE … TO DISK` con nombre `inventario_AAAAMMDD_HHmm.bak`. La restauración pide confirmación, pone la base en `SINGLE_USER`, restaura y vuelve a `MULTI_USER`. La cuenta del servicio de SQL Server necesita permiso de escritura en la carpeta de respaldos.
8. **Fotografías (RF-08).** Se guardan en disco (`uploads/articulos/`) y la base de datos solo guarda la ruta. Hay que incluir esa carpeta en el respaldo.

## 4. Estructura de carpetas

```
Control-de-Inventario-Muebleria/
├── .github/
│   ├── ISSUE_TEMPLATE/          # Plantillas: requerimiento, bug
│   ├── workflows/ci.yml         # Compila y prueba en cada PR
│   └── pull_request_template.md
├── .vscode/extensions.json      # Extensiones recomendadas
├── database/
│   ├── scripts/                 # Respaldo, restauración, usuario de BD, vistas de alertas
│   └── backups/                 # .bak locales (ignorados por git)
├── docs/
│   ├── diagramas/               # Casos de uso, entidad-relación
│   ├── propuesta-tecnica.md     # Este documento
│   └── roadmap.md               # Sprints y trazabilidad RF → sprint
├── scripts/
│   └── crear-proyectos.ps1      # Genera la solución .NET y el proyecto Vite (Sprint 0)
├── src/
│   ├── backend/
│   │   ├── Muebleria.Inventario.slnx
│   │   ├── Muebleria.Api/
│   │   │   ├── Controllers/     # Un controlador por módulo
│   │   │   ├── Authorization/   # Permisos y políticas
│   │   │   ├── Middleware/      # Manejo global de errores (ProblemDetails)
│   │   │   └── Program.cs
│   │   ├── Muebleria.Application/
│   │   │   ├── Common/Interfaces/
│   │   │   └── Features/        # Auth, Usuarios, Bitacora, Ubicaciones, Catalogo,
│   │   │                        # Herramientas, Acabados, MateriaPrima, Instalacion,
│   │   │                        # Movimientos, Solicitudes, Bajas, InventarioFisico,
│   │   │                        # Alertas, Reportes, Dashboard, Respaldos
│   │   ├── Muebleria.Domain/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   └── Exceptions/
│   │   └── Muebleria.Infrastructure/
│   │       ├── Persistence/     # AppDbContext, Configurations, Migrations,
│   │       │                    # Interceptors (bitácora), Seed (roles, admin)
│   │       ├── Security/        # Hash de contraseñas, usuario actual
│   │       ├── Reports/         # QuestPDF, ClosedXML
│   │       └── Backup/
│   └── frontend/
│       ├── public/
│       └── src/
│           ├── app/             # Router, providers, layout, rutas protegidas
│           ├── api/             # Cliente HTTP y endpoints
│           ├── components/      # UI reutilizable (tablas, formularios, diálogos)
│           ├── features/        # Una carpeta por módulo (mismos nombres que el backend)
│           ├── hooks/
│           ├── lib/             # Utilidades, formato de fechas y números
│           ├── styles/
│           └── types/
└── tests/
    ├── Muebleria.UnitTests/         # Reglas de dominio y servicios
    └── Muebleria.IntegrationTests/  # API + BD real (movimientos, permisos)
```

Cada carpeta de `features/` (frontend) y de `Features/` (backend) contiene todo lo de su módulo. Así una rama `feature/RF-23-solicitudes` toca casi siempre una sola carpeta en cada lado y se reducen los conflictos.

## 5. Modelo de datos

Las 9 tablas del documento ([entidad-relacion.png](diagramas/entidad-relacion.png) y diccionario de datos) se crean **tal como están escritas**:

- Mismos nombres de tabla y de columna (`ARTICULO`, `id_articulo`, `stock_actual`, `contrasena`…) y en el mismo orden.
- Mismos tipos: `INT`, `VARCHAR(n)`, `DECIMAL(10,2)`, `DATE`, `DATETIME` y `BIT`.
- Las mismas llaves primarias, foráneas y únicas (PK, FK, UK).
- Los mismos valores por defecto: `stock_actual = 0`, `stock_minimo = 0`, `estado = 'Buena'`, `estado = 'Pendiente'` y `fecha_hora = GETDATE()`.
- Las mismas reglas: existencia no negativa y cantidades mayores que cero.
- Los valores de texto también son los del documento: `Nueva`, `Buena`, `Desgastada`, `Dañada`, `De baja`; `Entrada`, `Salida`, `Ajuste`, `Baja`, `Merma`; `Pendiente`, `Aprobada`, `Rechazada`, `Entregada`.

`USUARIO.contrasena` guarda el hash PBKDF2, nunca la contraseña en texto (RNF-01).

### Columnas agregadas al final de las tablas del documento

Solo donde un requerimiento lo exige. Van después de las columnas del diccionario, así que no cambian su orden:

| Tabla | Columnas agregadas | Requerimiento |
|---|---|---|
| CATEGORIA | `tipo_inventario VARCHAR(20)` | Separar los módulos 5 a 8 (herramientas, acabados, materia prima, instalación) |
| UBICACION | `activo BIT` | RF-05: desactivar ubicaciones |
| ARTICULO | `presentacion VARCHAR(20)`, `foto_ruta VARCHAR(260)`, `activo BIT`, `version ROWVERSION` | RF-12 (litro o galón), RF-08 (fotografía), RF-09 (desactivar), RNF-06 (dos usuarios no pueden descontar a la vez) |
| MOVIMIENTO | `stock_anterior`, `stock_nuevo DECIMAL(10,2)` | RF-21: valor anterior y valor nuevo del ajuste |
| SOLICITUD | `motivo`, `motivo_rechazo VARCHAR(150)`, `fecha_entrega DATETIME` | RF-23, RF-24 y RF-25 |

ROL, USUARIO, SALIDA_HERRAMIENTA y BITACORA quedan exactamente como en el documento.

### Tablas nuevas

| Tabla | Campos principales | RF |
|---|---|---|
| CAMPO_PERSONALIZADO | id, id_categoria, nombre, tipo_dato (texto/número/fecha/lista), obligatorio, opciones | RF-07 |
| ARTICULO_VALOR_CAMPO | id_articulo, id_campo, valor | RF-07, RF-08 |
| ARTICULO_ALIAS | id_articulo, alias | Limitación 2 |
| HISTORIAL_ESTADO | id_articulo, estado_anterior, estado_nuevo, id_usuario, fecha_hora | RF-11 |
| CONTEO_FISICO | id, fecha, id_ubicacion (nulo), id_categoria (nulo), id_responsable, estado (Abierto/Aplicado) | RF-28 |
| CONTEO_DETALLE | id_conteo, id_articulo, stock_sistema, cantidad_contada, diferencia (calculada) | RF-28, RF-29 |
| PERMISO / ROL_PERMISO | código de permiso (`Articulos.Editar`, …) por rol | RF-02, RNF-02 |
| CONFIGURACION | clave, valor (p. ej. `alertas.dias_anticipacion_vencimiento = 30`) | RF-31 |
| RESPALDO | id, archivo, fecha_hora, id_usuario, tamaño | RF-38 |

**Lotes (RF-12/13):** para cumplir el plazo, cada lote de un producto de acabado se registra como un artículo propio (mismo nombre y lote distinto). Si después se necesita, se puede separar en una tabla `LOTE`.

## 6. API (convención)

- Prefijo `/api`, recursos en plural y en español: `/api/articulos`, `/api/movimientos/entradas`, `/api/solicitudes/{id}/aprobar`.
- Errores con `ProblemDetails` (RFC 9457) y mensajes en español.
- Paginación: `?pagina=1&tamano=20` y respuesta `{ items, total }`.
- Documentación con OpenAPI (`/openapi/v1.json`) y Scalar en desarrollo.

## 7. Roles y permisos (resumen)

| Módulo | Trabajador | Encargado de bodega | Administrador |
|---|:-:|:-:|:-:|
| Consultar artículos y existencias | ✔ | ✔ | ✔ |
| Crear solicitudes | ✔ | ✔ | ✔ |
| Registrar artículos, entradas, salidas, mermas y bajas | | ✔ | ✔ |
| Aprobar o rechazar solicitudes | | ✔ | ✔ |
| Salida y devolución de herramientas de instalación | | ✔ | ✔ |
| Toma de inventario físico | | ✔ | ✔ |
| Alertas | | ✔ | ✔ |
| Ajustes de inventario | | | ✔ |
| Usuarios, roles, ubicaciones y categorías | | | ✔ |
| Reportes y bitácora | | | ✔ |
| Respaldo y restauración | | | ✔ |

## 8. Despliegue en el taller

1. Instalar SQL Server 2022 Express y crear el usuario de la app (`database/scripts/01-usuario-app.sql`).
2. `dotnet publish -c Release` (antes, el build de React se copia a `wwwroot`).
3. Registrar el servicio: `sc.exe create MuebleriaInventario binPath= "...\Muebleria.Api.exe"`.
4. Abrir el puerto en el firewall de Windows para la red local.
5. Programar un recordatorio semanal de respaldo (RNF-07) y documentar cómo copiarlo a USB.
