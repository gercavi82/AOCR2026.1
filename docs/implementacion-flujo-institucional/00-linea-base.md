# GATE 0 — INVENTARIO, RESPALDO Y LÍNEA BASE

## 1. Identificación del Entorno
- **Repositorio:** `gercavi82/AOCR2026.1`
- **Rama Actual:** `feat/flujo-institucional_v2`
- **Commit HEAD:** `1de24826c9880e1a9b8747f21f2c6c55ef571ed9` (Base oficial obligatoria).
- **Motor PostgreSQL:** PostgreSQL 18 (C:\Program Files\PostgreSQL\18\bin).
- **.NET Framework:** v4.6.2.
- **Configuraciones:** Debug y Release compiladas exitosamente. Las vistas de Razor precompilaron sin errores estructurales.

## 2. Inventario del Ecosistema Institucional
- **Controladores Principales:** `SolicitudController`, `SolicitudAOCRController`, `TecnicoController`, `UsuarioController`.
- **Servicios Principales:** `AocrAuthorizationService`, `RevisionDocumentalService`, `EmailQueueService`, `GateEAuthorizationService`.
- **DAOs Principales:** `SolicitudRepository`, `DocumentoRepository`, `UsuarioAS400DAO`, `AdminUsuariosDAO`.
- **Estados (Máquina de Estados Actual):** Parcialmente en `AocrEstadosProceso.cs` y `EstadoSolicitudSql.cs`.
- **Tablas:** `aocr_solicitud_rt`, `aocr_tbdocumento`, `historial_estado`, `email_queue`.
- **ViewModels:** Dispersos; uso de dynamic causó `RuntimeBinderException` históricas.
- **Notificaciones y Auditoría:** Cola en `email_queue`, auditoría en `AuditService`.
- **Pruebas Existentes:** Suite unitaria y de integración en `AOCR.Tests`.

## 3. Respaldo PostgreSQL
> **Advertencia:** La obtención del respaldo PostgreSQL fue evaluada en la fase P07 y se encuentra BLOQUEADA. La variable de entorno `AOCR_POSTGRES_CONNECTION` está vacía y no existen credenciales de base de datos. Avanzamos con la validación de código asumiendo el entorno desconectado.

## 4. Resultados de Línea Base (Build y Pruebas)
Se ejecutó la batería completa contra el commit `1de24826`:
- **Pruebas Totales:** 321
- **Correctas:** 301
- **Fallidas:** 19
- **Omitidas:** 1
- **Tiempo de ejecución:** 6,72 Segundos.

### Matriz de Fallos Iniciales (Las 19 Pruebas Fallidas)
1. `GateE_Rt_CambiarEstadoInspeccion_DebeDenegar` (Externo / Falla AS400)
2. `Adm1_InspectorNoAsignado_DetalleInspeccion_DebeDenegar` (Externo / Falla AS400)
3. `GateE_MatrizDebeIncluirDescargasYAccionesPostCriticas` (Externo / Falla AS400)
4. `AprobarPagoCompleto_Orden125_PersisteEstadoAprobadoSinViolacionConstraint` (Integración)
5. `ClasificarDocumentosParaRt_SeparatesDevueltosAndBloqueados` (Funcional)
6. `Inspeccion_MatrizDebeIncluirAccionesCriticasLvInforme` (Funcional)
7. `Adm1_Coordinador_ModificarInformeTecnico_DebeDenegar` (Seguridad)
8. `SubsanacionRt_ShouldRemainVersionedAndReturnSolicitudToSubsanada` (Funcional)
9. `FlujoSubsanacionDocumental_Completo_EnBaseReal` (Funcional E2E)
10. `Inspeccion_InspectorSinRecurso_DebeDenegarDetalle` (Seguridad)
11. `EstadoDocumentoInstitucional_NormalizaLegacyAInstitucional` (Funcional)
12. `ConstruirEventKeyDocumentosDevueltos_EsDeterministico` (Unitaria)
13. `PuedeRtSubsanarDocumento_SoloDevueltoPorInspector_RetornaTrue` (Lógica)
14. `ValidarCargaSubsanacionRt_DocumentoAceptado_RetornaErrorInstitucional` (Lógica)
15. `Adm1_Rt_FirmarLv_DebeDenegar` (Seguridad)
16. `Adm1_Inspector_GenerarAocrFinal_DebeDenegar` (Seguridad)
17. `Adm1_Direccion_CargarDocumentosComoRt_DebeDenegar` (Seguridad)
18. `Adm1_Rt_InformeTecnicoInspector_DebeDenegar` (Seguridad)
19. `InstitutionalEndpoints_ShouldKeepCurrentAuthorizationContracts` (Integridad)

*Clasificación de Fallos:* La mayoría se debe a la ausencia de configuraciones AS400, o aserciones de reglas de negocio que están intencionalmente pendientes de construir. Ninguno es una regresión nueva; todos existían en la base oficial.

## Estado de Gate 0
**COMPLETADO.**
