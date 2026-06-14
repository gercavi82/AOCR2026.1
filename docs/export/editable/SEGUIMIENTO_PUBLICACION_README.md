# Plantilla de seguimiento — Publicación AOCR

## Archivo principal

**CSV (Excel):** [SEGUIMIENTO_PUBLICACION_AOCR.csv](SEGUIMIENTO_PUBLICACION_AOCR.csv)

Abrir con **Excel** o importar en **Teams / Planner / Jira**. Codificación **UTF-8** (compatible con tildes).

## Columnas

| Columna | Uso |
|---------|-----|
| **ID** | Identificador único (ej. `GB-INS1A`) |
| **Gate** | Gate A / B / C / D / E o Gate 2 (infra) / Gate 3 (gobernanza) |
| **Fase** | 0–8 según `HOJA_RUTA_PUBLICACION.md` |
| **Semana_Sugerida** | 1 = inmediato · 2 = Gate B/C · 3 = E + infra · 4 = go-live |
| **Prioridad** | Crítica / Alta / Media / Baja |
| **Bloquea_GoLive** | Sí = no prod sin esto |
| **Tarea** | Descripción accionable |
| **Rol_Responsable** | Dev, QA, Infra, Coordinación, Inspector, DIRDAC, RT, PO… |
| **Responsable_Nombre** | Completar con persona asignada |
| **Fecha_Planificada** | Fecha objetivo |
| **Fecha_Real** | Fecha en que se completó |
| **Estado** | `Pendiente` · `En progreso` · `Completado` · `Bloqueado` · `N/A` |
| **Evidencia** | Captura, log, ticket, ruta PDF |
| **Notas** | IDs solicitud, incidencias |
| **Referencia_Doc** | Documento guía en `docs/` |

## Filas HITO (MILE-*)

Las filas que empiezan por `MILE-` son **umbrales de fase**. Márcalas `Completado` solo cuando todas las tareas hijas de esa fase estén listas.

| ID | Significa |
|----|-----------|
| MILE-G0 | Deploy listo |
| MILE-GA | Gate A 8/8 |
| MILE-GB | Emisión #12 hasta AOCR RT |
| MILE-GC | Modificación tipo 3 |
| MILE-GD | NC + financiero |
| MILE-GE | Endurecimiento mínimo |
| MILE-G2 | Infra pre-prod |
| MILE-G3 | Checklist producción firmado |
| MILE-LIVE | Go-live ejecutado |

## Cómo usar semanalmente

1. Filtrar por **Semana_Sugerida** = semana actual.
2. Asignar **Responsable_Nombre** en filas de esa semana.
3. Reunión de seguimiento: revisar filas `Bloquea_GoLive = Sí` aún `Pendiente`.
4. Actualizar **Estado** y **Evidencia** al cerrar cada ítem.

## Dashboard rápido en Excel

- Tabla dinámica: filas por **Estado** y **Gate**.
- % avance Gate B: contar `GB-*` con Estado = Completado / 13.
- Semáforo: conditional formatting en columna Estado (Completado = verde, Bloqueado = rojo).

## Documento maestro

Detalle de cada tarea: [HOJA_RUTA_PUBLICACION.md](../../HOJA_RUTA_PUBLICACION.md)
