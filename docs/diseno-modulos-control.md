# Diseño de los módulos de flujo y control

Solicitudes, herramientas de instalación, alertas, reportes, respaldos y el panel principal.
Este documento explica cómo funciona cada módulo, qué requerimientos cubre y dónde está su código.

## Diseño principal (pantalla y navegación)

```
┌───────────────┬──────────────────────────────────────────────────────────────┐
│ IM Inventario │                                   🔔 6   (AD) Administrador ⏻ │  ← encabezado: alertas activas y sesión
│    Mueblería  ├──────────────────────────────────────────────────────────────┤
│               │  Buenas tardes, Administrador                                 │
│ INVENTARIO    │  lunes, 9 de noviembre                                        │
│  Inicio       │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│  Artículos    │  │14 artícu.│ │6 alertas │ │0 herram. │ │0 solicit.│  RF-36   │
│  Entradas…    │  └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
│  Herramientas │  ┌─────────────────────────────┐ ┌────────────────────┐       │
│  Solicitudes ③│  │ Consumo por categoría (30 d)│ │ Hay que reponer    │ RF-37 │
│ CONSULTA      │  │ ▇▇  ▇  ▇▇▇                  │ │ Lija 220 · 18/25   │       │
│  Alertas    ⑥ │  └─────────────────────────────┘ └────────────────────┘       │
│  Reportes     │  ┌────────────────────────────────────────────────────┐       │
│  Respaldos    │  │ Últimos movimientos                                │ RF-37 │
│ CONFIGURACIÓN │  └────────────────────────────────────────────────────┘       │
│  …            │                                                               │
└───────────────┴──────────────────────────────────────────────────────────────┘
```

- **El menú muestra solo lo que el rol puede usar.** El trabajador ve Inicio, Artículos, Entradas y salidas (consulta), Solicitudes, Ubicaciones y Categorías. La API vuelve a validar cada petición (RNF-02).
- **Contadores en el menú:** alertas activas (rojo) y solicitudes por atender (ámbar). Se actualizan cada minuto.
- **Indicadores del inicio (RF-36):** cada tarjeta lleva a su módulo y se pinta de color cuando hay algo que atender.
- **Gráfico de consumo (RF-37):** cuenta salidas y mermas por categoría en los últimos 30 días. Cuenta salidas y no cantidades, porque cada artículo usa su propia unidad (piezas, litros, planchas). Las herramientas de instalación no cuentan: salen prestadas y vuelven.

## Solicitudes de materiales (RF-23 a RF-25)

```
 Trabajador                 Encargado de bodega                     Inventario
 ──────────                 ───────────────────                     ──────────
 Crea solicitud ──► Pendiente ──► Aprobar ──► Aprobada ──► Entregar ──► Salida automática
                          └────► Rechazar (motivo obligatorio) ──► Rechazada        (descuenta stock)
```

- El trabajador solo ve sus propias solicitudes; quien aprueba ve todas.
- No se puede entregar sin aprobar, ni aprobar o rechazar dos veces.
- **Entregar** crea un movimiento de salida con el motivo "Entrega de la solicitud #N" en la misma transacción. Si no alcanza la existencia, la entrega se rechaza y el botón aparece deshabilitado.
- Cada paso queda en la bitácora: "Crear / Aprobar / Rechazar / Entregar solicitud #N".

| Código | Archivo |
|---|---|
| Servicio | `src/backend/Muebleria.Application/Features/Solicitudes/SolicitudService.cs` |
| API | `src/backend/Muebleria.Api/Controllers/FlujoControllers.cs` → `/api/solicitudes` |
| Pantalla | `src/frontend/src/features/solicitudes/SolicitudesPage.tsx` |

## Herramientas de instalación (RF-16 a RF-18)

Se agregaron porque la alerta de herramientas sin devolver (RF-32) y el reporte de herramientas fuera del taller (RF-34) dependen de ellas.

- **Salida:** solo para artículos de categorías de tipo *Instalación* y con unidades disponibles. Descuenta una unidad, así la herramienta queda como "no disponible". La fecha prevista no puede ser anterior a hoy.
- **Devolución:** repone la unidad. Si regresa desgastada o dañada, cambia el estado del artículo y lo anota en el historial de estados (RF-11).

| Código | Archivo |
|---|---|
| Servicio | `Features/Instalacion/InstalacionService.cs` |
| API | `FlujoControllers.cs` → `/api/instalacion` |
| Pantalla | `features/instalacion/InstalacionPage.tsx` |

## Alertas (RF-30 a RF-32)

Las alertas **se calculan en el momento** con los datos actuales; no se guardan en tablas, así nunca quedan desactualizadas.

| Alerta | Regla | RF |
|---|---|---|
| Stock mínimo | `stock_actual <= stock_minimo` en artículos activos | RF-30 |
| Vencimiento | `fecha_vencimiento <= hoy + N días` y con existencia; N lo configura el administrador (tabla CONFIGURACION) | RF-31 |
| Herramienta sin devolver | sin `fecha_devolucion` y `fecha_prevista < hoy` | RF-32 |
| Respaldo pendiente | último respaldo con 7 días o más, o ninguno (solo la ve el administrador) | RNF-07 |

| Código | Archivo |
|---|---|
| Servicio | `Features/Alertas/AlertaService.cs` |
| API | `ControlControllers.cs` → `/api/alertas` |
| Pantalla | `features/alertas/AlertasPage.tsx` |

## Reportes (RF-33 a RF-35)

Un mismo `ReporteDto` (columnas con su tipo y filas) sirve para mostrar en pantalla, generar el PDF y generar el Excel. Por eso lo que se ve es exactamente lo que se exporta (RF-35).

| Reporte | Filtros | RF |
|---|---|---|
| Existencias actuales | categoría, ubicación | RF-33 |
| Movimientos por período | fechas, categoría, tipo | RF-33 |
| Consumo por categoría | fechas, categoría | RF-34 |
| Herramientas fuera del taller | — | RF-34 |

- **PDF:** QuestPDF, con licencia comunitaria gratuita. Se genera en horizontal cuando hay más de 6 columnas e incluye título, filtros, filas alternadas, quién lo generó y "Página X de Y".
- **Excel:** ClosedXML, como una tabla con filtros, números y fechas reales (no texto) y el encabezado fijo.
- Cada exportación queda en la bitácora.

| Código | Archivo |
|---|---|
| Servicio | `Features/Reportes/ReporteService.cs` |
| Exportador | `src/backend/Muebleria.Infrastructure/Reports/ExportadorReportes.cs` |
| API | `ControlControllers.cs` → `/api/reportes` |
| Pantalla | `features/reportes/ReportesPage.tsx` |

## Respaldo y restauración (RF-38, RF-39, RNF-07)

- **Generar:** `BACKUP DATABASE … WITH CHECKSUM` a la carpeta de respaldos de SQL Server, o a la que indique `Respaldos:Carpeta` en `appsettings`. El archivo se llama `inventario_AAAAMMDD_HHMMSS.bak`.
- **Historial:** se lee de `msdb` (historial de SQL Server). Así no se pierde al restaurar una copia anterior. También queda en la tabla RESPALDO y en la bitácora.
- **Restaurar:**
  1. Exige escribir **RESTAURAR**.
  2. Antes de restaurar **genera automáticamente una copia del estado actual**, para poder deshacer.
  3. Pone la base en `SINGLE_USER`, restaura y la devuelve a `MULTI_USER`, incluso si algo falla.
  4. Solo acepta archivos que SQL Server tiene registrados como respaldo de esta base; no se puede restaurar una ruta arbitraria.
- Después de restaurar se cierra la sesión, porque los usuarios pueden ser distintos en la copia.

| Código | Archivo |
|---|---|
| Servicio | `Features/Respaldos/RespaldoService.cs` |
| SQL Server | `src/backend/Muebleria.Infrastructure/Backup/RespaldosSqlServer.cs` |
| API | `ControlControllers.cs` → `/api/respaldos` |
| Pantalla | `features/respaldos/RespaldosPage.tsx` |

## Otros cambios

- **Una sola regla para mover stock:** `MovimientoService.Crear(...)` la usan movimientos, solicitudes e instalación (RNF-06).
- **Las sesiones ya no se cierran al reiniciar la API:** las claves de la cookie se guardan en `Muebleria.Api/App_Data/claves`, carpeta ignorada por git.
- **Bitácora:** registra solicitudes y salidas de herramientas con acciones legibles.

## Permisos

| Acción | Trabajador | Encargado | Administrador |
|---|:-:|:-:|:-:|
| Crear y ver sus solicitudes | ✓ | ✓ | ✓ |
| Aprobar, rechazar y entregar solicitudes | | ✓ | ✓ |
| Salida y devolución de herramientas | | ✓ | ✓ |
| Ver alertas | | ✓ | ✓ |
| Configurar anticipación de vencimiento | | | ✓ |
| Reportes y exportación | | | ✓ |
| Respaldo y restauración | | | ✓ |
