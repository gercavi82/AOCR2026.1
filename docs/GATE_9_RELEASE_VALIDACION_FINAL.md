# GATE 9 — Release y validación final

Fecha: 2026-07-14  
Estado general: **PARCIAL**  
Rama: `firma-dirdac-tec`  
Commit base validado: `da964be92ca99370c92e4aa3e0c29f283fcd2cb0`

## Resumen ejecutivo

El código compila desde `Clean`, Razor precompila, las migraciones 014–020 son idempotentes y sus rollbacks fueron probados en una copia desechable. Las 40 pruebas focales pasan y la suite global mantiene 19 fallos, pero termina con tres omisiones. El estado no puede ser COMPLETADO porque dos fixtures PostgreSQL adicionales dependen de datos preexistentes, no se ejecutaron los escenarios E2E A–D con usuarios autenticados ni existe una ejecución real del workflow de CI. GATE 8 continúa además parcialmente integrado para eventos anteriores al cierre institucional.

La cifra 281/261/19/1 del prompt no es reproducible en este checkout. La línea reproducible previa era 306/286/19/1; tras las 15 pruebas de GATE 8, la suite actual es 321/301/19/1.

## Higiene

- `.gitignore` ampliado para `bin`, `obj`, DLL, PDB, cache, EXE, TRX, `TestResults` y documentos operativos generados en `App_Data`.
- El repositorio ya contiene artefactos históricos versionados (`bin`, `obj`, DLL/PDB y TRX). No se retiraron masivamente del índice para evitar mezclar una limpieza histórica con la entrega funcional; requiere una PR de higiene separada.
- Los TRX nuevos quedan ignorados y sus resultados están trasladados a este documento.

## Build reproducible

Herramientas:

- Visual Studio 2022 Community `17.14.37328.6`.
- MSBuild `17.14.40.60911`.
- NuGet restaurado con `nuget restore AOCR.sln -NonInteractive`.

Comandos:

```powershell
msbuild AOCR.sln /t:Clean /p:Configuration=Debug /m /v:minimal
msbuild AOCR.sln /t:Clean /p:Configuration=Release /m /v:minimal
msbuild AOCR.sln /t:Rebuild /p:Configuration=Debug /m /v:minimal
msbuild AOCR.sln /t:Rebuild /p:Configuration=Release /m /v:minimal
```

Después de `Clean`, `CapaPresentacion.dll` y `AOCR.Tests.dll` no existían. Debug, Release, todos los proyectos y ASP.NET/Razor aprobaron. Advertencia única: `itext.commons 9.5.0.0` referencia una versión de `System.IO.Compression` posterior al framework objetivo.

## SQL

Se creó mediante `pg_dump`/`pg_restore` la copia aislada `aocr_gate9_20260714` (fuente de 40 MB). Se ejecutaron dos veces 014, 015, 016, 017, 018, 019 y 020; todos aprobaron. Luego se ejecutaron rollbacks 020→014, se confirmó que `aocr_tbsolicitud` conservó 1 fila, y se reaplicaron 014→020. Resultado final antes de eliminar la copia: 292 constraints CHECK/UNIQUE/FK, 545 índices públicos, 27 columnas en `aocr_evento_workflow` y ambos índices únicos de eventos presentes. La base temporal fue eliminada y se confirmó conteo 0 en `pg_database`.

## Pruebas

- Focales seguridad + GATE 7A + GATE 8: **40/40**.
- Global final reproducible: **321 totales; 299 aprobadas; 19 fallidas; 3 omitidas**.
- Regresiones nuevas respecto de la línea reproducible: **0**.

Clasificación de fallos:

- 17 externos/fixture: AS400 no configurado (16 pruebas de autorización, DocumentoSubsanacion e integración) y fixture financiero de orden 125 sin comprobante (1).
- 2 contratos de caracterización desactualizados: autorización literal de `FinalizarRevisionDocumental` y expectativa legacy `PENDIENTE_REVISION_SUBSANACION`.
- Omitida conocida: `FlujoCompleto_CrearOrdenHastaPago_Exitoso`.
- Omisiones adicionales no aceptables: `Gate1_VinculoNuevaEvaluacion_EsTransaccionalEIdempotente` no crea una NC `CON_INSPECCION`; `Gate2_Dao_NcInvalidaHaceRollbackSinAlterarDocumento` no crea el documento requerido. Ambas deben usar fixtures transaccionales autocontenidos.

## Corrección encontrada durante QA

Dos llamadas directas `EmailHelper.EnviarEmail` en `SolicitudAOCRController` fueron sustituidas por `EmailQueueService`, con `event_key`, `correlation_id`, reintentos y aislamiento de errores. La auditoría posterior no encontró SMTP ni helpers de envío directo en los controladores del flujo.

## CI

Se creó `.github/workflows/aocr-validation.yml`: Windows 2022, restauración NuGet, Release/Razor, pruebas focales y publicación de TRX. PostgreSQL está separado en un job manual que exige `AOCR_PG_CONNECTION`. Estado: **NO EJECUTADO EN GITHUB**; no se declara aprobado.

## Estado final solicitado

| Campo | Resultado |
|---|---|
| ESTADO GENERAL | PARCIAL |
| Commit validado | `da964be92ca99370c92e4aa3e0c29f283fcd2cb0` + cambios locales no confirmados |
| Rama | `firma-dirdac-tec` |
| Build Debug | Aprobado desde Clean |
| Build Release | Aprobado desde Clean |
| Razor | Aprobado |
| SQL | Idempotencia, rollback y reaplicación aprobados en copia desechable |
| Pruebas | 321 / 299 / 19 / 3 |
| CI | Workflow creado; ejecución real pendiente |
| Validación visual | Pendiente; navegador integrado no disponible |
| Regresiones nuevas | 0 fallos nuevos; 2 omisiones adicionales por fixtures no autocontenidos |
| Pendientes | Corregir 2 fixtures inconclusos, E2E A–D, evidencia visual, completar integración GATE 8, ejecutar CI/PR y PR separada de higiene histórica |
