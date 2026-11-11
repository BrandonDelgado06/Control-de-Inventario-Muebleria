# Plan y resultados de pruebas · Avance 2

**Fecha de ejecución:** 9 de noviembre de 2026 · **Equipo:** Windows 11, SQL Server 2022 Developer, .NET 10.0.14, Node.js 26
**Rama:** `test/avance-2-pruebas` · actualizado el 10 de noviembre de 2026 en `feature/RF-pendientes` (RF-08, 11, 13, 15, 26 a 29 y 33)

## Resumen

| Nivel | Qué prueba | Herramientas | Pruebas | Resultado |
|---|---|---|---:|---|
| Unitarias | Reglas de negocio y servicios, con una base de datos en memoria | xUnit, FluentAssertions, EF Core InMemory | 74 | 74 correctas |
| Integración | La API completa contra SQL Server real (base `MuebleriaInventario_Pruebas`, que se crea de cero en cada ejecución) | xUnit, WebApplicationFactory, SQL Server 2022 | 33 | 33 correctas |
| Frontend | Formato de datos y mensajes de error que ve el usuario | Vitest | 12 | 12 correctas |
| **Total** | | | **119** | **119 correctas** |

La suite de integración se ejecutó 6 veces seguidas sin fallos, para descartar resultados inestables en las pruebas de concurrencia.

## Cómo ejecutarlas

```powershell
dotnet test tests\Muebleria.UnitTests          # unitarias (≈2 s)
dotnet test tests\Muebleria.IntegrationTests   # integración (≈12 s; necesita SQL Server local)
cd src\frontend; npm test                      # frontend
```

Las pruebas de integración usan la instancia local (`Server=.`) o la que indique la variable de entorno `PRUEBAS_SQL`. **No tocan la base de desarrollo.** En GitHub, el CI ejecuta las tres suites en cada Pull Request con un SQL Server 2022 en contenedor (`.github/workflows/ci.yml`).

## Registro de pruebas

Formato de la Tabla 7 del machote. En la columna "Automatizada", **U** = unitaria, **I** = integración, **F** = frontend.

| Prueba y requisito | Acción | Resultado esperado | Resultado real | Automatizada |
|---|---|---|---|---|
| CP-01 · RF-01 | Pedir `/api/articulos` sin iniciar sesión | La API responde 401 | 401 · **Cumple** | I |
| CP-02 · RF-01 | Iniciar sesión con usuario inexistente y con contraseña equivocada | El mismo mensaje en los dos casos: "Usuario o contraseña incorrectos." | Mensajes idénticos · **Cumple** | I |
| CP-03 · RF-01 | Cerrar sesión y volver a pedir la sesión | 401 | 401 · **Cumple** | I |
| CP-04 · RNF-01 | Guardar la misma contraseña dos veces | Hashes distintos (con sal), sin la contraseña en texto; la verificación distingue mayúsculas | **Cumple** | U |
| CP-05 · RF-02 | Crear un usuario | Queda en minúsculas, con hash y registrado en la bitácora | **Cumple** | U |
| CP-06 · RF-02 | El administrador intenta desactivar su propia cuenta | Se rechaza | **Cumple** | U |
| CP-07 · RNF-02 | El trabajador llama directo a 9 rutas de administración (usuarios, bitácora, alertas, reportes, respaldos, herramientas, ubicaciones, aprobar solicitud) | 403 en todas | 403 en las 9 · **Cumple** | I |
| CP-08 · RF-21, RNF-02 | El encargado registra una entrada y luego intenta un ajuste | Entrada 200; ajuste 403 "Solo el administrador…" | **Cumple** | I |
| CP-09 · RNF-03 | Crear usuario con nombre vacío, usuario con espacios, contraseña de 3 caracteres y sin rol | 400 con los mensajes en español de cada campo | **Cumple** | I |
| CP-10 · RNF-03 | Buscar `x'; DROP TABLE ARTICULO; --` | Se trata como texto: 0 resultados y la tabla sigue existiendo | **Cumple** | I |
| CP-11 · RF-03 | Crear una ubicación y consultar la bitácora | Aparecen "Iniciar sesión" y "Crear ubicación …" con su usuario | **Cumple** | I |
| CP-12 · RF-07 | Registrar una lija sin el campo obligatorio "Número de grano", luego con un valor fuera de la lista y luego con 120 | Se rechazan los dos primeros intentos; el tercero se guarda con el valor | **Cumple** | U |
| CP-13 · RF-08 | Registrar un código que ya existe, escrito en minúsculas | "El código LIJ-220 ya está registrado" | **Cumple** | U |
| CP-14 · RF-08 | Registrar un artículo con existencia inicial de 40 | Existencia 40 y un movimiento de entrada "Existencia inicial" | **Cumple** | U |
| CP-15 · Limitación 2 | Buscar por un alias ("papel de agua") | Encuentra el artículo | **Cumple** | U |
| CP-16 · RF-11 | Cambiar el estado de una lija de Buena a Desgastada | Queda en el historial con usuario | **Cumple** | U |
| CP-17 · RF-12 | Registrar un producto de acabado sin fecha de vencimiento | Se rechaza | **Cumple** | U |
| CP-18 · RF-19, RF-20, RF-21 | Entradas, salidas, mermas, bajas y ajustes sobre la regla de existencia (13 casos) | Suma, resta o fija la existencia; rechaza cantidades ≤ 0 y artículos desactivados | **Cumple** | U |
| CP-19 · RF-20 | Sacar 21 piezas cuando hay 20 | Se rechaza y no se guarda ningún movimiento | **Cumple** | U |
| CP-20 · RNF-06 | **10 usuarios hacen a la vez una salida de 3** sobre un artículo con 20 | Nunca queda negativo; solo se guardan las que caben y cada salida guardada tiene su movimiento | **Cumple** | I |
| CP-21 · RNF-06 | Escribir `stock_actual = -1` directo en SQL Server | La base lo rechaza (`CK_ARTICULO_stock_actual`) | **Cumple** | I |
| CP-22 · RF-23 a RF-25 | El trabajador pide 4 lijas; intenta aprobar él mismo; el encargado intenta entregar sin aprobar; aprueba y entrega | 403, luego 400, luego Aprobada y Entregada; la existencia baja 4 | **Cumple** | I |
| CP-23 · RF-24 | Rechazar una solicitud y luego intentar aprobarla | Guarda el motivo; no se puede aprobar | **Cumple** | U |
| CP-24 · RF-25 | Entregar una solicitud aprobada de 50 cuando hay 20 | Se rechaza y la solicitud sigue aprobada | **Cumple** | U |
| CP-25 · RF-16 | Sacar la única unidad de un taladro y volver a sacarlo | Existencia 0; el segundo intento dice "todas están fuera" | **Cumple** | U |
| CP-26 · RF-17 | Devolver una herramienta dañada | Vuelve la unidad, el estado pasa a Dañada y queda en el historial | **Cumple** | U |
| CP-27 · RF-32 | Una herramienta con fecha prevista hace 2 días | Aparece en alertas con 2 días de retraso; al devolverla desaparece | **Cumple** | I |
| CP-28 · RF-30 | Artículos con existencia bajo y igual al mínimo | Ambos aparecen en la alerta | **Cumple** | U |
| CP-29 · RF-31 | Productos que vencieron hace 2 días, vencen en 10 y en 60; luego cambiar la anticipación a 5 días | Con 30 días aparecen los 2 primeros; con 5, solo el vencido; el cambio queda en la bitácora | **Cumple** | U |
| CP-30 · RNF-07 | Sin respaldos, con uno de 8 días y con uno de hoy | Avisa en los dos primeros casos; el encargado no ve el aviso | **Cumple** | U |
| CP-31 · RF-33 | Reporte de existencias | Las mismas filas que artículos activos en la base | **Cumple** | I |
| CP-32 · RF-34 | Reporte de consumo con salidas, mermas, entradas y la salida de un taladro de instalación | Suma salidas y mermas; no cuenta entradas ni herramientas prestadas | **Cumple** | U |
| CP-33 · RF-35 | Exportar 4 reportes a PDF y Excel | Archivos válidos (`%PDF` / `PK`), tipo y nombre correctos | **Cumple** | I |
| CP-34 · RF-38, RF-39 | Generar respaldo, crear una ubicación, intentar restaurar con "restaurar" en minúsculas y luego con "RESTAURAR" | Se rechaza el primer intento; el segundo devuelve la base al momento del respaldo y antes guarda una copia automática | **Cumple** (después de corregir los defectos D-1 y D-3) | I y U |
| CP-35 · **RNF-04** | **10 usuarios simultáneos**, 150 operaciones mezcladas (buscar, ver, panel, registrar entrada) | Ninguna supera 3 segundos | Máximo **337 ms** · **Cumple** | I |
| CP-36 · Interfaz | Formato de números, fechas sin desfase, días para vencer, nombres de estados y mensajes de error | Textos correctos en español | **Cumple** | F (12) |
| CP-37 · RF-08 | Subir una foto PNG; subir un texto renombrado a `.jpg`; el trabajador intenta cambiarla | La foto se guarda y reemplaza a la anterior; el archivo falso se rechaza ("JPG, PNG o WEBP"); el trabajador recibe 403 | **Cumple** | U e I |
| CP-38 · RF-11 | Dar de baja la última unidad de una herramienta | El historial de estados muestra Buena → De baja con el usuario; en la base queda "De baja" | **Cumple** | U e I |
| CP-39 · RF-13 | Dos lotes del mismo tinte (uno vencido) y una laca | Se agrupan por producto; el lote vencido va primero; cuenta 1 vencido y 1 por vencer | **Cumple** | U e I |
| CP-40 · RF-15 | Filtrar artículos por unidad "plancha" | Solo aparecen artículos en planchas | **Cumple** | U e I |
| CP-41 · RF-26 | Merma por daño de 2 lijas; merma por vencimiento de un tinte que no ha vencido; merma mayor a la existencia | Resta 2 y guarda "Merma por daño: …"; los otros dos se rechazan; el trabajador recibe 403 | **Cumple** | U e I |
| CP-42 · RF-27 | Dar de baja una herramienta y luego un material | La herramienta queda De baja y desactivada; el material se rechaza | **Cumple** | U e I |
| CP-43 · RF-28 | Abrir un conteo por ubicación y otro que se cruza con el primero | El primero guarda la existencia del sistema de cada artículo; el segundo se rechaza | **Cumple** | U e I |
| CP-44 · RF-29 | Contar 2 lijas menos mientras salen 5 al taller; aplicar sin terminar; aplicar como encargado | Sin terminar se rechaza ("Faltan contar…"); el encargado recibe 403; el administrador aplica: existencia 13 (no 18), un solo ajuste "Conteo físico #n: diferencia -2", la columna calculada `diferencia` da -2 y queda en la bitácora | **Cumple** | U e I |
| CP-45 · RF-33 | Reporte de movimientos filtrado por artículo y responsable | Muestra solo esas filas y los filtros aparecen en el encabezado | **Cumple** | I |

### Detalle de rendimiento (RNF-04)

Medición del 9 de noviembre de 2026: 10 usuarios a la vez, 15 operaciones cada uno.

| Operación | Peticiones | Promedio | p95 | Máximo |
|---|---:|---:|---:|---:|
| Buscar artículo | 40 | 8 ms | 10 ms | 31 ms |
| Ver artículo | 40 | 26 ms | 86 ms | 86 ms |
| Panel principal | 40 | 15 ms | 20 ms | 25 ms |
| Registrar entrada (los 10 sobre el mismo artículo) | 30 | 80 ms | 324 ms | 337 ms |

El límite es 3 000 ms; el peor caso quedó 9 veces por debajo.

## Defectos encontrados por las pruebas y corregidos

| # | Defecto | Cómo se encontró | Corrección |
|---|---|---|---|
| D-1 | **Restaurar podía restaurar el estado equivocado.** El respaldo elegido y la copia automática "antes de restaurar" se hacían en el mismo segundo, compartían el nombre de archivo y la segunda sobrescribía a la primera. | CP-34: después de restaurar, el dato creado seguía ahí; en `msdb` dos respaldos apuntaban al mismo archivo. | Cada archivo lleva un sufijo único (`inventario_AAAAMMDD_HHMMSS_xxxxxx.bak`). |
| D-2 | **Dos entradas simultáneas del mismo artículo: una fallaba** con "otro usuario modificó este registro". | CP-35: 10 usuarios registrando entradas a la vez. | Si hay conflicto, el sistema vuelve a leer la existencia y reintenta, hasta 10 veces y con una espera corta aleatoria. Una salida que ya no alcanza se rechaza por existencia insuficiente. |
| D-3 | **Error 500 al restaurar:** el texto de la bitácora superaba los 100 caracteres de la columna `accion`. | CP-34 | Texto más corto y recorte automático a 100 caracteres de toda acción de bitácora. |
| D-4 | El "último respaldo" podía mostrarse en desorden si dos terminaban en el mismo segundo. | CP-34 repetida varias veces. | Orden por fecha y por número de respaldo. |

Defectos encontrados antes, en pruebas manuales del avance (ya corregidos): el panel daba error 500 por una consulta no traducible a SQL; el gráfico contaba herramientas prestadas como consumo; y las sesiones se cerraban al reiniciar la API.

## Lo que todavía no cubren

- **Navegadores (RNF-09):** la revisión en Chrome, Edge y Firefox es manual. Hasta ahora se revisó en Chrome (panel del navegador); faltan Edge y Firefox.
- **Usabilidad (RNF-05):** requiere una prueba con una persona del taller.
- **Cierre por inactividad de 30 minutos (RF-01):** está configurado, pero no hay una prueba automática que espere 30 minutos.
