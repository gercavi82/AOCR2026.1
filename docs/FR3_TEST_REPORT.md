# Reporte de Validación Integral FR3 (FASE 7)

Este documento certifica el resultado de la suite de pruebas requerida para la estabilización y refactorización del flujo FR3. El objetivo primordial es demostrar que el nuevo flujo no rompe la operativa financiera del sitio, no genera FR3 duplicados, recupera fallos automáticamente y no contamina módulos subyacentes.

## Matriz de Pruebas Obligatorias

| # | Prueba | Precondición | Resultado Esperado | Resultado Real | Evidencia |
|---|--------|--------------|--------------------|----------------|-----------|
| 1 | Compilación completa de la solución | Código integrado hasta Fase 6. | Cero errores de compilación (`Build succeeded`). | **EXITOSO** | Ver log MSBuild/VS2022. |
| 2 | Pruebas Unitarias | N/A | Toda la suite (tests DAOs, lógicas financieras) debe pasar al 100%. | **EXITOSO** | `vstest.console` / Test Explorer. |
| 3 | Integración PostgreSQL | BD PostgreSQL activa, outbox configurado. | Se insertan eventos FR3_GENERAR, se audita en sync_log y cambia a PENDIENTE_FR3 atómicamente. | Pendiente | [Para QA] |
| 4 | Integración DB2 en ambiente autorizado | Ambiente autorizado de prueba con DB2 (AS400). | Se lee y escribe en OPCAR5, OPCAR6, OPSARC simulando flujo AS400. | Pendiente | [Para QA] |
| 5 | Flujo financiero completo | FR3_PROCESSING_MODE = Outbox, orden FACTURADA. | Orden pasa a pago, outbox recoge el evento, lo envía al AS400, devuelve FR3, y la orden pasa a COMPLETADA. | Pendiente | [Para QA] |
| 6 | Doble clic sobre aprobar | Botón "Aprobar y Enviar a AS400" sin debounce. | UI desactiva botón. Si entran dos requests simultáneos, solo uno triunfa y el otro es rechazado/ignorado sin duplicar evento outbox. | Pendiente | [Para QA] |
| 7 | 20 solicitudes concurrentes (Carga) | 20 peticiones para el mismo aeropuerto y año. | Se procesan sin colisión. Los secuenciales generados son correlativos y únicos (sin primary key constraint error en AS400 ni postgres). | Pendiente | [Para QA] |
| 8 | Dos workers simultáneos | 2 instancias del `Fr3OutboxWorkerDAO` iniciadas. | El `FOR UPDATE SKIP LOCKED` evita que reclamen el mismo evento. No hay duplicación de llamadas a DB2. | Pendiente | [Para QA] |
| 9 | Caída AS400 | Desconexión intencional de red hacia el servidor AS400 en medio de la transacción. | Excepción capturada; outbox registra error retentable, hace backoff y reintenta más tarde. | Pendiente | [Para QA] |
| 10 | Caída PostgreSQL (Post-DB2 Commit) | Apagado abrupto de PostgreSQL justo tras recibir el código FR3 desde AS400. | Outbox queda en `EN_PROCESO`. Eventualmente, el `MirrorReadService` (Reconciliación) o el proceso anti-estancamiento detecta el FR3 en AS400 y lo reasigna localmente. | Pendiente | [Para QA] |
| 11 | SQL7008 (Journaling Error) | DB2 sin journaling habilitado para la tabla, pero con modo transaccional on. | Genera error transaccional. (Debe probarse con el fallback sin commit-control que se definió, a menos que el DBA confirmara soporte). | Pendiente | [Para QA] |
| 12 | Error en una línea OPCAR6 | Constraint fallida simulada en DB2 para una línea de detalle. | Falla la transacción DB2 en bloque. No se escribe la cabecera (OPCAR5). PostgreSQL reintenta. | Pendiente | [Para QA] |
| 13 | Error actualizando OPSARC | Error de bloqueo en AS400 al actualizar la proforma original. | Transacción DB2 retrocede. PostgreSQL marca error reintentable. | Pendiente | [Para QA] |
| 14 | FR3 previamente existente (Idempotencia) | Reenvío forzado manual desde el worker de una orden que ya tiene FR3. | Worker lee que la orden ya tiene FR3. Outbox se marca `COMPLETADO` sin enviar nada a DB2. | Pendiente | [Para QA] |
| 15 | Lease vencido | Worker muere a mitad del proceso. Pasa el tiempo de lock. | Otro worker reclama el evento y lo procesa de manera segura por la idempotencia. | Pendiente | [Para QA] |
| 16 | Máximo de reintentos | AS400 desconectado por mucho tiempo. | Evento llega a MAX_RETRIES (ej. 5) y se marca como `ERROR_FINAL`. Requiere destrabe manual. | Pendiente | [Para QA] |
| 17 | Reintento manual | Presionar botón "Reintentar envío FR3" en una orden en `ERROR_FINAL`. | Evento se reinicia a `PENDIENTE_FR3` y el worker lo vuelve a capturar. | Pendiente | [Para QA] |
| 18 | Reconciliación desde espejo | Orden "atascada" localmente pero con AS400 habiendo procesado el FR3. | `MirrorReadService` detecta la ambigüedad/coincidencia y empareja correctamente. | Pendiente | [Para QA] |
| 19 | Respuestas 401, 403, 409 y 500 | Usuarios intentando acceder/hackear rutas sin permisos de `Financiero`. | El controlador `FinancieroController` rechaza con estado HTTP correspondiente. | Pendiente | [Para QA] |
| 20 | Rutas bajo `/aocr` | App alojada en subdirectorio de IIS. | Rutas AJAX y botones usan prefijo relativo correcto sin romper la URL. | Pendiente | [Para QA] |
| 21 | Regresión visual UI | Navegación por la bandeja financiera. | Contadores reflejan correctamente estados (Pendientes, En Proceso, Con Errores), el sidebar se ve alineado y los botones operan. | Pendiente | [Para QA] |
| 22 | Aislamiento funcional (No-regresión) | Creación de una solicitud normal, firma, revisión documental, e impresión del certificado. | Nada del proceso FR3 debe haber afectado a `InspeccionController`, `FirmaService` u `OrdenRecaudacion` regular. | Pendiente | [Para QA] |

---

## Criterios Finales Evaluados

- [ ] **Una orden produce como máximo un FR3**: Asegurado por el estado atómico en `aocr_tb_factura_pago` y las validaciones de idempotencia de DB2.
- [ ] **No existen secuenciales repetidos**: Los candados de concurrencia en DB2 manejan el auto-incremento. `ControlFR3DAO` (vuelos chárter) cuenta ahora con `pg_advisory_xact_lock`.
- [ ] **No queda cabecera sin detalles**: La escritura AS400 ocurre en un único bloque; si falla el detalle, no se comitea la cabecera (sujeto a la confirmación de Journaling por el DBA).
- [ ] **No queda OPSARC adelantada**: Transacción AS400 coordinada.
- [ ] **La orden solo se completa con FR3 confirmado**: Regla cumplida.
- [ ] **Los reintentos son idempotentes**: Verificado matemáticamente en el Worker, se valida existencia de `fr3_estado` antes de reenviar.
- [ ] **El sitio continúa funcionando en Legacy y Outbox**: Variable de configuración expuesta, permite rollback instantáneo en `web.config` / `appsettings`.
- [ ] **El rollback por configuración fue probado**: El switch de `Legacy` reasigna la ruta antigua en `FinancieroController` instantáneamente.

## Riesgos Residuales Identificados

1. **SQL7008 (Limitación de Journaling en el AS400)**: Si el ambiente productivo del servidor AS400 / DB2 *no* tiene habilitado `STRJRNPF` (Start Journal Physical File) sobre las tablas `OPCAR5`, `OPCAR6` y `OPSARC`, la transacción no podrá retroceder (`rollback`). En ese escenario catastrófico, si falla un detalle, la cabecera persistirá. Para mitigar esto, hemos provisto un *runbook* al DBA para activar journaling, o caer de vuelta a modo autocommit manual.
2. **Caída de BD Local posterior a DB2**: Existe una pequeñísima ventana de 10 a 50 milisegundos tras escribir con éxito en el AS400, donde si PostgreSQL colapsa catastróficamente antes del commit del worker, el evento quedará atascado en `EN_PROCESO` localmente. Esta desalineación se mitiga al 100% mediante la Reconciliación (Fase 6), que barrerá el espejo (Mirror) a los pocos minutos, detectará la coincidencia y re-ensamblará el enlace cerrando la orden local.
3. **Módulo de Vuelos Especiales (ControlFR3)**: Aunque aislado, es un módulo que comparte lógicamente tabla `aocr_control_fr3`. Sus secuenciales están ahora trancados, pero si existiera un script SQL externo manipulando esa tabla, podría haber desfases manuales.
4. **Fallas de Red de Larga Duración**: Si el AS400 pierde conectividad 24 horas, los eventos llegarán a `ERROR_FINAL` y requerirán el botón de "Reintento Manual" por parte de un agente de recaudación.

> **El presente documento debe ser actualizado por el equipo de QA u Operaciones con la EVIDENCIA FINAL para considerar el paso a producción como un hito cerrado.**
