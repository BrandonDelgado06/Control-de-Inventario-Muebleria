# Guía de trabajo en GitHub

## Ramas

Usamos un flujo **Git Flow simplificado**:

```
main      ●───────────────●───────────────●──────── v1.0.0
           \             /               /
develop     ●──●──●──●──●──●──●──●──●──●
               \  /     \  /     \   /
feature/...     ●●       ●●       ●●●
```

| Rama | Para qué | Se crea desde | Se integra en |
|---|---|---|---|
| `main` | Versión estable y entregable. Cada merge lleva una etiqueta (`v0.2.0`). | — | — |
| `develop` | Integración del sprint en curso. Siempre debe compilar. | `main` | `main` (al cerrar el sprint) |
| `feature/RF-XX-descripcion` | Un requerimiento o una parte de él. | `develop` | `develop` |
| `fix/descripcion` | Corrección de un error encontrado en `develop`. | `develop` | `develop` |
| `hotfix/descripcion` | Corrección urgente sobre lo ya entregado. | `main` | `main` **y** `develop` |
| `docs/descripcion` | Solo documentación. | `develop` | `develop` |
| `chore/descripcion` | Configuración, dependencias, CI. | `develop` | `develop` |

Ejemplos: `feature/RF-01-login`, `feature/RF-19-20-entradas-salidas`, `fix/stock-negativo-salidas`, `chore/ci-frontend`.

**Reglas**
- Nunca se hace commit directo en `main` ni en `develop`: todo entra por Pull Request.
- Las ramas duran poco (1 a 3 días). Si un RF es grande, se divide en backend y frontend.
- Antes de abrir el PR, actualizar la rama: `git pull --rebase origin develop`.
- Borrar la rama después del merge.

## Commits (Conventional Commits)

```
<tipo>(<módulo>): <descripción en presente>
```

| Tipo | Uso |
|---|---|
| `feat` | Funcionalidad nueva |
| `fix` | Corrección |
| `docs` | Documentación |
| `test` | Pruebas |
| `refactor` | Cambio sin alterar el comportamiento |
| `chore` | Configuración, dependencias, CI |
| `db` | Migraciones o scripts SQL |

Ejemplos:
```
feat(solicitudes): aprobar y rechazar solicitudes pendientes (RF-24)
fix(movimientos): impedir salidas mayores al stock (RF-20)
db: agregar tabla ARTICULO_ALIAS
```

## Pull Requests

- Título con el mismo formato que los commits.
- Vincular el issue con `Closes #N` en la descripción.
- Llenar la plantilla: qué RF cubre, cómo se probó y capturas si hay cambios de UI.
- Al menos **1 aprobación** de otro integrante y CI en verde.
- Merge con **Squash and merge** hacia `develop`. Al cerrar el sprint, **Create a merge commit** de `develop` a `main`.

## Migraciones de base de datos

- Solo una persona a la vez crea migraciones. Si dos ramas crean migraciones en paralelo, la segunda en hacer merge debe eliminar la suya y volver a generarla después de actualizar su rama.
- Nombre descriptivo: `dotnet ef migrations add AgregarAliasArticulo`.
- Nunca editar una migración que ya está en `develop`; crear una nueva.

## Configuración del repositorio en GitHub (una sola vez)

Se hace desde **Settings** del repositorio:

1. **Branches → Add branch ruleset** para `main` y `develop`:
   - Require a pull request before merging (1 approval).
   - Require status checks to pass → `backend`, `frontend`.
   - Block force pushes.
2. **General → Pull Requests:** permitir *Squash merging* y *Merge commits*; activar *Automatically delete head branches*.
3. **Collaborators:** agregar a los integrantes y al profesor (lectura) si se requiere.
4. **Issues → Labels:**

| Etiqueta | Color | Uso |
|---|---|---|
| `modulo:seguridad` | `#5319e7` | Usuarios, roles, bitácora |
| `modulo:inventario` | `#0e8a16` | Catálogo, herramientas, acabados, materia prima, instalación, ubicaciones |
| `modulo:movimientos` | `#1d76db` | Entradas, salidas, solicitudes, bajas, toma física |
| `modulo:consulta` | `#fbca04` | Alertas, reportes, panel, respaldos |
| `prioridad:alta` / `media` / `baja` | rojo / naranja / gris | Según el documento |
| `backend` / `frontend` / `bd` | — | Capa afectada |
| `bug` | `#d73a4a` | Error |

5. **Milestones:** `Sprint 0` … `Sprint 4` con las fechas de [docs/roadmap.md](docs/roadmap.md).
6. **Projects:** tablero tipo *Board* con columnas *Por hacer, En progreso, En revisión, Hecho*, y una issue por RF.
