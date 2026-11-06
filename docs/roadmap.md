# Roadmap

**Inicio:** 4 de noviembre de 2026 · **Entrega:** 4 de diciembre de 2026 · **Duración:** 4,5 semanas

El plan se divide en sprints de una semana. Cada sprint termina con un merge de `develop` a `main` y una etiqueta de versión (`v0.1.0`, `v0.2.0`, …), así siempre hay una versión estable para mostrar.

Prioridad: los RF de prioridad **Alta** van primero. Si el tiempo no alcanza, se recortan primero los de prioridad **Baja** (RF-06, RF-36 y RF-37).

## Entregas al profesor

Antes de la entrega final hay dos avances. Los sprints se acomodan a ellos:

| Entrega | Fecha | Qué se presenta | Estado |
|---|---|---|---|
| **Avance 1** | por confirmar | Código funcional parcial: base de datos completa y los módulos de seguridad, catálogo y movimientos funcionando de punta a punta | Listo para revisar (rama `chore/sprint0-proyectos`) |
| **Avance 2** | por confirmar | Pruebas: unitarias, de integración y plan de pruebas manuales con evidencias, más los módulos del sprint 3 | Pendiente |
| **Entrega final** | 4 dic | Sistema completo, reportes, respaldos, documento final | Pendiente |

### Contenido del avance 1

**Base de datos (completa):** las 19 tablas del modelo ([propuesta-tecnica.md §5](propuesta-tecnica.md#5-modelo-de-datos)) con los nombres del diccionario de datos (`ARTICULO`, `id_articulo`, `stock_actual`…), llaves foráneas sin borrado en cascada, restricciones `CHECK` (existencia ≥ 0, cantidades > 0), `rowversion` en `ARTICULO`, índices únicos y la columna calculada `diferencia` del conteo físico. Se crea con la migración `Inicial` y se llena con datos de ejemplo del taller.

**Módulos funcionando (API + pantallas):**

| Módulo | RF | Qué se puede mostrar |
|---|---|---|
| Usuarios y accesos | RF-01, RF-02, RNF-01, RNF-02 | Login, cierre por inactividad (30 min), usuarios y roles; un trabajador recibe 403 si llama a un endpoint de administrador |
| Bitácora | RF-03, RF-04 | Cada creación, edición y movimiento queda registrado solo; consulta con filtros |
| Ubicaciones | RF-05, RF-06 | Crear, editar, desactivar (no deja si tiene artículos) y ver qué hay en cada una |
| Catálogo | RF-07, RF-08, RF-09 | Categorías con campos personalizados (grano, espesor, especie), artículos con otros nombres (alias) y búsqueda por ellos |
| Inventario por tipo | RF-10, RF-12, RF-14, RF-15 (parcial) | Filtro por tipo; acabados exigen lote y vencimiento |
| Movimientos | RF-19, RF-20, RF-21, RF-22, RNF-06 | Entradas, salidas (no permite stock negativo), ajustes solo del administrador e historial |
| Panel | RF-36 (inicial) | Totales, artículos bajo el mínimo y últimos movimientos |

**Queda para después:** fotografía del artículo (la columna ya existe), RF-11 en pantalla (el historial de estado ya se guarda), herramientas de instalación (RF-16 a 18), solicitudes, bajas y mermas, inventario físico, alertas, reportes y respaldos.

## Resumen

| Sprint | Fechas | Objetivo | Versión |
|---|---|---|---|
| 0 | 4–8 nov | Proyecto creado, BD inicial, CI funcionando | `v0.1.0` |
| 1 | 9–15 nov | Seguridad, ubicaciones y catálogo de artículos | `v0.2.0` |
| 2 | 16–22 nov | Movimientos e inventario por tipo | `v0.3.0` |
| 3 | 23–29 nov | Solicitudes, bajas, inventario físico y alertas | `v0.4.0` |
| 4 | 30 nov–4 dic | Reportes, panel, respaldos, pruebas y entrega | `v1.0.0` |

---

## Sprint 0 · Base del proyecto (4–8 nov)

- [ ] Instalar el SDK de .NET 10, Node.js 22 LTS y SQL Server 2025 en los equipos del equipo.
- [ ] Crear el repositorio con esta estructura, configurar la protección de ramas y crear las etiquetas y los milestones ([CONTRIBUTING.md](../CONTRIBUTING.md)).
- [x] Ejecutar `scripts/crear-proyectos.ps1`: solución .NET de 4 capas, 2 proyectos de pruebas y el proyecto Vite.
- [x] `AppDbContext` con **todas** las entidades del modelo ([propuesta-tecnica.md §5](propuesta-tecnica.md#5-modelo-de-datos)) y la migración `Inicial`.
- [x] Datos semilla: 3 roles, permisos y el usuario administrador inicial.
- [x] Middleware de errores y OpenAPI (Scalar en `/scalar`). No hace falta CORS: Vite redirige `/api` a la API.
- [x] Layout del frontend: menú lateral por módulo, ruteo y tema de MUI.
- [ ] El CI compila y prueba el backend y el frontend en cada PR.

**Listo cuando:** `dotnet run` y `npm run dev` levantan, y la migración crea la BD vacía con los datos semilla.

## Sprint 1 · Seguridad y catálogo (9–15 nov)

| RF | Descripción | Prioridad |
|---|---|---|
| RF-01 | Inicio y cierre de sesión, cierre por inactividad | Alta |
| RF-02 | Gestión de usuarios y roles | Alta |
| RNF-01/02/03 | Hash de contraseñas, permisos validados en el servidor, validación de entradas | Alta |
| RF-03 | Registro automático en la bitácora (interceptor) | Media |
| RF-05 | Gestión de ubicaciones | Media |
| RF-07 | Categorías y campos personalizados | Alta |
| RF-08 | Registro de artículos (con foto y alias) | Alta |
| RF-09 | Consulta, búsqueda y edición de artículos | Alta |
| RF-06 | Artículos por ubicación | Baja |

**Listo cuando:** un administrador inicia sesión, crea usuarios, ubicaciones y categorías con campos propios, registra artículos y los busca por nombre o alias. Un trabajador **no** puede llamar a los endpoints de administración (lo comprueba una prueba de integración).

## Sprint 2 · Movimientos e inventario por tipo (16–22 nov)

| RF | Descripción | Prioridad |
|---|---|---|
| RF-19 | Registro de entradas | Alta |
| RF-20 | Registro de salidas (sin stock negativo) | Alta |
| RNF-06 | Movimiento y stock en una sola transacción | Alta |
| RF-21 | Ajustes de inventario (valor anterior y nuevo) | Media |
| RF-22 | Historial de movimientos | Media |
| RF-10 | Herramientas y accesorios con variantes | Alta |
| RF-11 | Estado de herramientas e historial de estados | Media |
| RF-12 | Productos de acabado (presentación, lote y vencimiento) | Alta |
| RF-13 | Existencias de acabados por lote y vencimiento | Media |
| RF-14 | Ingreso de materia prima | Alta |
| RF-15 | Consulta de existencias de materia prima | Media |
| RF-16 | Salida de herramientas de instalación | Alta |
| RF-17 | Devolución de herramientas | Alta |
| RF-18 | Herramientas fuera del taller | Media |

**Listo cuando:** las pruebas de integración demuestran que dos salidas simultáneas no dejan el stock en negativo, y que una herramienta que sale queda como no disponible hasta su devolución.

## Sprint 3 · Flujo de trabajo y alertas (23–29 nov)

| RF | Descripción | Prioridad |
|---|---|---|
| RF-23 | Creación de solicitudes | Alta |
| RF-24 | Aprobación o rechazo de solicitudes | Alta |
| RF-25 | Entrega (genera la salida) e historial | Media |
| RF-26 | Mermas de materiales | Media |
| RF-27 | Baja de herramientas | Media |
| RF-28 | Registro del conteo físico | Media |
| RF-29 | Comparación y ajuste de diferencias | Media |
| RF-30 | Alertas de stock mínimo | Alta |
| RF-31 | Alertas de vencimiento (anticipación configurable) | Media |
| RF-32 | Alertas de herramientas sin devolver | Alta |
| RF-04 | Consulta y filtros de la bitácora | Media |

**Listo cuando:** se recorre el flujo completo trabajador → encargado → entrega, y el stock y la bitácora quedan correctos.

## Sprint 4 · Reportes, respaldos y entrega (30 nov–4 dic)

| RF | Descripción | Prioridad |
|---|---|---|
| RF-38 | Generar copia de seguridad | Media |
| RF-39 | Restaurar la base de datos (con confirmación) | Media |
| RNF-07 | Procedimiento de respaldo semanal documentado | Media |
| RF-33 | Reportes de existencias y movimientos | Media |
| RF-34 | Reportes de consumo y de herramientas fuera | Media |
| RF-35 | Exportación a PDF y Excel | Media |
| RF-36 | Indicadores del panel principal | Baja |
| RF-37 | Últimos movimientos y gráficos | Baja |

Cierre (2–4 dic):
- [ ] Pruebas de rendimiento con 10 usuarios simultáneos (RNF-04).
- [ ] Probar en Chrome, Edge y Firefox (RNF-09).
- [ ] Instalar en un equipo limpio siguiendo la guía de despliegue.
- [ ] Completar en el documento las secciones "Pruebas y resultados" y "Conclusiones y recomendaciones", con capturas como anexos.
- [ ] Release `v1.0.0` en GitHub con el ejecutable publicado adjunto.

---

## Trazabilidad RF → sprint

| Módulo | RF | Sprint |
|---|---|---|
| 1. Usuarios y accesos | RF-01, RF-02 | 1 |
| 2. Bitácora | RF-03 · RF-04 | 1 · 3 |
| 3. Ubicaciones | RF-05, RF-06 | 1 |
| 4. Catálogo | RF-07, RF-08, RF-09 | 1 |
| 5. Herramientas manuales | RF-10, RF-11 | 2 |
| 6. Productos de acabado | RF-12, RF-13 | 2 |
| 7. Materia prima | RF-14, RF-15 | 2 |
| 8. Herramientas de instalación | RF-16, RF-17, RF-18 | 2 |
| 9. Entradas, salidas y ajustes | RF-19 a RF-22 | 2 |
| 10. Solicitudes | RF-23, RF-24, RF-25 | 3 |
| 11. Bajas y mermas | RF-26, RF-27 | 3 |
| 12. Inventario físico | RF-28, RF-29 | 3 |
| 13. Alertas | RF-30, RF-31, RF-32 | 3 |
| 14. Reportes | RF-33, RF-34, RF-35 | 4 |
| 15. Panel principal | RF-36, RF-37 | 4 |
| 16. Respaldo | RF-38, RF-39 | 4 |

## Riesgos

| Riesgo | Mitigación |
|---|---|
| El plazo es corto (4,5 semanas para 39 RF) | Hacer primero los de prioridad Alta y recortar los de prioridad Baja. Merge diario a `develop` para no acumular conflictos. |
| Las versiones de .NET o SQL Server no coinciden entre los equipos | Fijar el SDK con `global.json` y documentar las versiones en el README. |
| Pérdida de datos en el taller | RF-38/39 en el Sprint 4 y guía de respaldo en USB. |
| Duplicados por nombres distintos | Alias en el Sprint 1 y búsqueda que los incluya. |
