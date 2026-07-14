# Auditoría final independiente — flujo INSATISFACTORIO

Fecha de auditoría: 2026-07-14  
Repositorio: `gercavi82/AOCR2026.1`  
Rama auditada: `firma-dirdac-tec`  
Commit base: `da964be92ca99370c92e4aa3e0c29f283fcd2cb0`  
Estado del checkout: **sucio**, con cambios rastreados y archivos nuevos sin commit.

## Veredicto

# NO APROBADO PARA MERGE

El flujo contiene piezas funcionales relevantes, pero no está terminado ni es desplegable de forma confiable. Los bloqueantes principales son: credenciales en texto claro dentro de archivos rastreados, persistencia de GATE 8 ausente en la base configurada, integración incompleta de eventos/auditoría, ausencia de una nueva versión formal de NC después de devolución, 19 pruebas fallidas, 3 pruebas omitidas/inconclusas y falta de validación visual y CI ejecutada.

Los documentos de los GATE se trataron únicamente como orientación. Ningún estado se concedió por lo declarado en README, documentos, comentarios, nombres de métodos, botones, TRX antiguos ni binarios versionados.

## Evidencia ejecutada

- Git: rama y commit obtenidos mediante `git branch --show-current` y `git rev-parse HEAD`; se inspeccionó `git status --short` y `git diff --check`. No se realizó merge.
- Build Release: `MSBuild.exe AOCR.sln /t:Rebuild /p:Configuration=Release /m /v:minimal`. Resultado: **aprobado**, incluida la precompilación ASP.NET/Razor. Se observó una advertencia de compatibilidad de versión de `System.IO.Compression` proveniente de `itext.commons`.
- Pruebas: ejecución global nueva con VSTest. Resultado: **321 totales, 299 aprobadas, 19 fallidas y 3 omitidas/inconclusas**. Evidencia: `TestResults/auditoria-independiente-global.trx`.
- PostgreSQL configurado: consulta directa a `dgac_des`. Existen columnas de GATE 1/2 y una unicidad de cola de correo; **no existe** `aocr_evento_workflow` ni su índice único de GATE 8 (`gate8_table=0`, `gate8_unique=0`).
- Búsqueda estática: `rg` sobre C#, Razor, configuración, SQL, proyectos y Git para los patrones obligatorios.
- Validación visual: se intentó abrir el navegador integrado; no había ninguna sesión/página disponible. No se inventó evidencia visual.

## Matriz de auditoría

| # | Control | Estado | Evidencia técnica verificada | Tabla / prueba | Riesgo |
|---:|---|---|---|---|---|
| 1 | NC formal | IMPLEMENTADO | `CapaDatos/DAOs/NoConformidadDAO.cs`, clase `NoConformidadDAO`, método `Insertar`; controlador de inspección genera y persiste la NC. | Tablas de NC; `NoConformidadDAOTests` | La funcionalidad existe, pero la auditoría de eventos no cubre su creación en el entorno configurado. |
| 2 | Firma Inspector | PARCIAL | `CapaPresentacion/Controllers/InspeccionController.cs`, acción de firma de NC; actualiza firma, estado, PDF/hash. | Pruebas de caracterización/flujo | No se encontró integración efectiva del evento `NC_FIRMADA_INSPECTOR` en el ledger de GATE 8. |
| 3 | Devolución Coordinador | PARCIAL | `InspeccionController`, acción de devolución cambia a `DEVUELTA_INSPECTOR`. El propio código indica que la observación se guarda en el informe y no como revisión/versionado formal de la NC. | Estado de NC; pruebas de flujo | Se pierde semántica y trazabilidad específica de la devolución. |
| 4 | Nueva versión NC | NO IMPLEMENTADO | Después de devolver, la corrección vuelve a actualizar la misma NC; no se evidenció creación de fila/version formal ni incremento consistente de versión. | No hay prueba conductual de nueva versión | Pérdida de historial jurídico/técnico y del vínculo entre versiones. |
| 5 | Firma Coordinador | PARCIAL | `InspeccionController`, acción de firma de coordinación actualiza la NC y documento. | Pruebas de flujo | Sin evento/auditoría idempotente efectiva en la base configurada. |
| 6 | Notificación RT | PARCIAL | Existen llamadas de notificación/correo en el flujo, pero no una prueba real de entrega ni integración completa con `NC_NOTIFICADA_RT`. | `NotificacionBL`, cola de correo | Riesgo de destinatario o duplicación no detectado de extremo a extremo. |
| 7 | Ruta SIN_INSPECCION | PARCIAL | DAOs, controladores y vistas implementan carga/revisión documental. | GATE 2/3; pruebas DAO y de caracterización | Varias pruebas dependen de AS400/fixtures; una prueba de rollback queda inconclusa. |
| 8 | Versionado individual | PARCIAL | `DocumentoDAO` y modelo de subsanación conservan versión y documento anterior. | Columnas verificadas en PostgreSQL; pruebas GATE 2 | No hay recorrido real autenticado que demuestre todas las iteraciones. |
| 9 | Revisión por documento | PARCIAL | Acciones de aceptación/rechazo por documento y panel de inspector presentes. | Pruebas GATE 3 | Parte de la cobertura es inspección de texto fuente, no comportamiento integrado. |
| 10 | Rechazo y nueva versión | PARCIAL | El rechazo habilita una carga posterior y conserva vínculos de versión. | DAO/documentos subsanados | Sin E2E y con fixtures incompletos; no se demostró notificación idempotente al RT. |
| 11 | Cierre documental | PARCIAL | Transacción en `NoConformidadDAO` valida documentos y cambia NC a `SUBSANACION_ACEPTADA` e inspección a `EN_INSPECCION`, sin volver satisfactorio el resultado. | Pruebas GATE 3 | La prueba contra PostgreSQL no es completamente autocontenida y la auditoría/notificación está incompleta. |
| 12 | Nuevo Informe Técnico | IMPLEMENTADO | `CapaNegocio/Services/ReevaluacionInspeccionService.cs`, método de preparación crea nueva versión y relaciona informe anterior, NC y ciclo. | Pruebas GATE 5 | Falta validación visual/E2E, pero persistencia y reglas están representadas. |
| 13 | Nueva NC por reincidencia | IMPLEMENTADO | `ReevaluacionInspeccionService`, creación idempotente de nueva NC/ciclo ante reevaluación insatisfactoria. | Pruebas GATE 5 | El evento `NUEVA_NC_GENERADA` no está integrado en todos los puntos del flujo. |
| 14 | Cierre por satisfactorio | IMPLEMENTADO | Servicio de reevaluación exige informe finalizado, firmado, satisfactorio y con hash antes de cerrar la NC. | Pruebas GATE 5 | Persisten carencias de auditoría transversal y E2E. |
| 15 | Ruta CON_INSPECCION | PARCIAL | `RTController.SolicitarNuevaInspeccion` y servicio transaccional dedicado. | Pruebas GATE 4 | No se recorrió con usuarios y datos institucionales reales. |
| 16 | Nueva solicitud real | IMPLEMENTADO | `CapaNegocio/Services/NuevaInspeccionPorNcService.cs` crea una solicitud institucional dentro de transacción y retorna/rechaza duplicados. | Tablas de solicitud; pruebas GATE 4 | Integración externa financiera/AS400 no demostrada. |
| 17 | Relación con origen | IMPLEMENTADO | El servicio y migraciones relacionan solicitud, inspección, informe y NC de origen. | SQL GATE 1/4; pruebas DAO | La consistencia existe en código/esquema, pero requiere despliegue controlado en cada ambiente. |
| 18 | Idempotencia | PARCIAL | Hay restricción/consulta para impedir más de una solicitud activa por NC. GATE 8 define `event_key`, pero su tabla no existe en la base configurada. | Índices de solicitud; `Gate8EventoDAO` | Idempotencia de solicitud sí; idempotencia transversal de notificaciones no está operativa. |
| 19 | Nueva inspección | IMPLEMENTADO | `NuevaInspeccionPorNcService` crea y enlaza la inspección cuando corresponde. | Pruebas GATE 4 | No se verificó asignación/notificación completa con actores reales. |
| 20 | Nueva orden | PARCIAL | El servicio prepara/crea la orden y devuelve su código; existe un `TODO` de integración de facturación en `OrdenRecaudacionOrchestrator`. | Pruebas GATE 4; fixture financiero fallido | Riesgo de comportamiento incompleto frente al sistema financiero real. |
| 21 | Módulo 7 | PARCIAL | `AocrCierrePorTipoTramiteService` decide EMISIÓN/RENOVACIÓN y genera AOCR + Condiciones. | `AocrCierrePorTipoTramiteServiceTests` | Sin E2E de destinatarios, firma y descarga de ambos documentos. |
| 22 | Módulo 8 | PARCIAL | El mismo servicio genera solo Condiciones para modificación. | Prueba unitaria de no adjuntar AOCR | No se demostró el recorrido institucional completo ni la firma DCAV/DIRDAC. |
| 23 | Descargas seguras | PARCIAL | `DocumentoSeguroService` aplica token/ruta confinada y fue incorporado en endpoints principales. | `DocumentoSeguroServiceTests` | Persisten `File(path)`, `Server.MapPath` y rutas directas en otros controladores; no hubo prueba HTTP autenticada/maliciosa. |
| 24 | Notificaciones idempotentes | PARCIAL | `AocrProcesoNotificacionService` llama a GATE 8 para eventos finales; búsqueda de usos encontró integración efectiva solo para un subconjunto final. | `Gate8WorkflowEventService`, `Gate8EventoDAO`, pruebas GATE 8 | La mayoría de los 32 eventos obligatorios existe solo en catálogo/documentación, no en las transiciones reales; tabla ausente en DB. |
| 25 | Auditoría | PARCIAL | Se definieron `correlation_id`, `event_key` y ledger, pero la base configurada no tiene la tabla y no se encontraron llamadas en NC, subsanación, nueva solicitud, inspección y reevaluación. | `aocr_evento_workflow`: inexistente en `dgac_des` | Operaciones críticas quedan sin cadena auditable completa; algunos errores son absorbidos. |
| 26 | Build Release | IMPLEMENTADO | Rebuild Release ejecutado y aprobado. | MSBuild | Advertencia de compatibilidad de ensamblado iText/System.IO.Compression. |
| 27 | Razor | IMPLEMENTADO | La precompilación ASP.NET/Razor terminó correctamente durante Release. | MSBuild/aspnet compiler | No sustituye la validación en navegador. |
| 28 | SQL | PARCIAL | Scripts 014–020 existen y fueron diseñados idempotentes; consulta directa confirmó que GATE 8 no está aplicado en la DB configurada. | PostgreSQL `dgac_des` | Código desplegado puede fallar o degradar silenciosamente al intentar registrar eventos. |
| 29 | Pruebas | PARCIAL | Ejecución fresca: 321/299/19/3. Los 19 fallos incluyen 16 dependencias AS400, 1 fixture financiero y 2 contratos de fuente/estado; 3 quedan omitidas/inconclusas. | TRX nuevo de auditoría | Suite no verde, fixtures no autocontenidos y exceso de pruebas basadas en cadenas de código. |
| 30 | Validación visual | NO VERIFICABLE | El navegador integrado no tenía sesión/página disponible; no existe evidencia nueva A–D autenticada. | Ninguna prueba visual ejecutable | UX, roles, botones, descargas y navegación pueden fallar pese al build. |
| 31 | CI | NO VERIFICABLE | Existe `.github/workflows/aocr-validation.yml`, pero está sin seguimiento y no se ejecutó. El secreto PG no se conecta de forma demostrable con el `app.config` usado por las pruebas. | Sin run de GitHub Actions | No hay control automático reproducible de build, Razor, SQL y pruebas. |
| 32 | Preparación del PR | NO IMPLEMENTADO | Checkout con decenas de cambios y archivos nuevos sin commit, workflow/docs sin seguimiento y binarios modificados. `git diff --check` sí pasó. | `git status --short` | No existe unidad de cambio revisable, limpia ni reproducible para abrir/actualizar PR. |

## Defectos bloqueantes y hallazgos priorizados

### P0 — críticos

1. **Credenciales en texto claro y rastreadas por Git.** `AOCR.Tests/app.config` y `CapaPresentacion/Web.config` contienen credenciales PostgreSQL y AS400. Deben revocarse/rotarse, retirarse del repositorio e inyectarse mediante secretos/configuración de ambiente. Ocultarlas solo en un commit nuevo no elimina su exposición histórica.
2. **Ledger de auditoría GATE 8 ausente en la base configurada.** La consulta directa devolvió `gate8_table=0` y `gate8_unique=0`. El código no puede garantizar auditoría, `event_key` ni reintentos idempotentes en ese entorno.

### P1 — altos

1. **GATE 8 no está conectado a todas las transiciones.** Se encontraron llamadas solo para eventos finales de documentos; NC, subsanación, nueva solicitud, nueva inspección, reevaluación y cierre carecen de integración completa.
2. **La corrección tras devolución no crea una nueva versión formal de NC.** Se actualiza la misma entidad, lo que impide demostrar versiones y firmas históricas inmutables.
3. **Suite no aprobada:** 19 fallos y 3 omitidas/inconclusas. Dos pruebas PostgreSQL no crean sus propios datos y 16 dependen de AS400 no configurado.
4. **No existe recorrido E2E visual/autenticado.** No se verificaron actores, destinatarios, rutas 5/6, firma, descargas ni documentos finales en navegador.
5. **CI no ejecutada ni incorporada al commit.** El workflow está sin seguimiento y la conexión PG secreta no está cableada de forma verificable a la configuración que consumen las pruebas.
6. **Checkout no apto para PR.** Mezcla código, SQL, documentos, resultados y binarios generados sin un commit auditable.

### P2 — medios

1. Se detectaron **69 `catch {}` vacíos** exactos, además de bloques que absorben errores con comentarios. Esto puede ocultar fallos de auditoría, correo y persistencia.
2. Permanecen artefactos versionados: aproximadamente **76 rutas bin/obj, 1.354 DLL, 58 PDB, 37 TRX y 38 ejecutables**. Actualizar `.gitignore` no elimina los ya rastreados.
3. Existe al menos un POST administrativo/local sin `ValidateAntiForgeryToken`: `InspeccionController.RegenerarHistoricosPdfLvEaeOficial`. Aunque restringido por rol y `Request.IsLocal`, no cumple el control uniforme.
4. Hay accesos directos `File(path)`/`Server.MapPath` fuera del servicio seguro, incluyendo `UsuarioController` y `6_CertificadoController`; requieren inventario y confinamiento homogéneo.
5. Varias pruebas GATE validan presencia de cadenas/nombres en fuente. Por ejemplo, comprobar que un evento está en el catálogo no prueba que DIRDAC reciba el documento correcto.
6. Build Release emite una advertencia de resolución de versión entre iText y `System.IO.Compression`; debe validarse en el runtime de IIS objetivo.

### P3 — bajos

1. `AOCR.Tests/Unit/EmailQueueTests.cs` conserva un TODO para implementar pruebas cuando el servicio esté disponible.
2. `OrdenRecaudacionOrchestrator` conserva un TODO de integración con facturación.
3. Endpoints de diagnóstico en `SolicitudAOCRController` están condicionados por DEBUG/404 fuera de DEBUG, pero deben excluirse o controlarse explícitamente en releases institucionales.

## Resultado de búsquedas obligatorias

| Patrón | Resultado |
|---|---|
| `TODO`, `FIXME` | TODOs activos en pruebas de cola y orquestación de recaudación; sin evidencia material de FIXME bloqueante. |
| `NotImplementedException` | Sin ocurrencias funcionales relevantes en el alcance auditado. |
| `Assert.Inconclusive`, `Assert.Ignore`, `[Ignore]` | Tres pruebas terminaron omitidas/inconclusas; dos fixtures PostgreSQL no crean los datos requeridos. |
| `catch` vacíos | 69 coincidencias exactas `catch {}` en fuente. |
| Endpoints sin `Authorize` | La mayoría de alertas eran falsos positivos por autorización a nivel de clase; no se probó anónimamente cada ruta. |
| POST sin antiforgery | Se confirmó el POST administrativo/local citado sin token. |
| `File(path`, `Server.MapPath`, `RutaPdf`, rutas en modelo | Uso extendido; el flujo principal tiene servicio seguro parcial, pero quedan accesos directos. No se halló un `href` directo a `RutaPdf` en el foco revisado. |
| Credenciales / connection strings | Credenciales en texto claro en dos archivos de configuración rastreados. |
| `bin/`, `obj/`, DLL, PDB, TRX, ejecutables | Cantidad significativa de artefactos ya versionados; algunos aparecen modificados en el checkout. |

## Condiciones mínimas antes de una nueva auditoría

1. Rotar y retirar secretos, incluida la historia Git cuando corresponda al procedimiento institucional.
2. Aplicar y validar SQL 020 dos veces y su rollback en una base desechable; verificar también el ambiente destino.
3. Integrar los eventos obligatorios en cada transición real y demostrar `event_key`, reintento y `correlation_id` mediante pruebas conductuales.
4. Implementar versionado formal e inmutable de NC tras devolución/corrección.
5. Dejar la suite autocontenida y verde, o documentar exclusiones externas mediante una categoría reproducible sin `Inconclusive` por fixtures faltantes.
6. Ejecutar E2E autenticado para Inspector, Coordinador, RT y DCAV/DIRDAC, incluyendo módulos 7 y 8 y descargas adversariales.
7. Incorporar y ejecutar CI en GitHub Actions con secretos correctamente inyectados.
8. Limpiar artefactos rastreados, separar cambios y preparar un commit/PR reproducible.

Esta auditoría no realizó merge ni implementó funcionalidad del flujo.
