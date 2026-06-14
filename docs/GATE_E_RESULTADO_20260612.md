# Gate E — Endurecimiento técnico, seguridad y permisos

**Fecha:** 2026-06-12  
**Alcance:** E1, E2, E3, E6, E7, ADM-1  
**Veredicto:** **APROBADO EN CÓDIGO Y TESTS** — E2E manual IIS pendiente (PDF AOCR/CL, URL directa en browser)

---

## Resumen ejecutivo

Se cerraron brechas de autorización unificada (`AocrAuthorize` + `AocrAuthorizationService`), se eliminó el listado global sin filtro en revisión documental, se centralizaron flags de flujo en `SolicitudAocrFlujoViewModel`, se añadió normalizador legacy de estados, idempotencia en `NotificacionService` y **9 tests ADM-1/Gate E** en verde.

**Suite unitaria:** 223/223 ejecutables OK (1 omitido integración).

---

## E1 / DT-1 — Autorización unificada InspeccionController

### Acciones críticas protegidas (nuevo o reforzado)

| Acción | Protección |
|--------|------------|
| `Detalle` | `[AocrAuthorize]` + validación asignación en controlador |
| `VerInforme`, `DescargarInforme` | `[AocrAuthorize]` + matriz + `ValidarAccesoInspeccion` |
| `Ver/Descargar LV EAE`, adjuntos informe | Idem |
| `Ver/DescargarLvEaeOficial` | Matriz solo Inspector/Admin + LV habilitada |
| `CambiarEstado` | `[AocrAuthorize]` + bloqueo RT/coordinación en LV/informe |
| `ConfirmarRevisionDocumentalInspector` | Ya existía |
| `Guardar/Finalizar/Firmar LV e Informe` | Matriz + reglas por fase |
| `SubirInforme`, `RegistrarNoConforme` | `[AocrAuthorize]` + fase LV firmada |
| `AprobarNcSubsanacionDocumental` | `[AocrAuthorize]` coordinación |
| Acciones dirección (`FirmarDireccion`, etc.) | Ya existían |

### Reglas nuevas en `AocrAuthorizationService`

- RT **no puede** cambiar estado, firmar LV/informe ni registrar NC.
- Coordinación **no puede** modificar LV ni Informe Técnico del inspector.
- Administrador bypass antes de consulta DAO en `ValidarAccesoInspeccion` (evita 500/403 espurio).
- Matriz ampliada con **16 acciones** de descarga/gestión POST.

**Archivos:** `AocrAuthorizationService.cs`, `InspeccionController.cs`

---

## E3 / DT-3 — Revisión documental filtrada

| Control | Estado |
|---------|--------|
| `RevisionDocumentalBandejaService` filtra por inspector asignado | OK (existente) |
| `Documento/Lista` valida `modo=revision` vs `modo=ver` | OK (existente) |
| `Documento/RevisarDocumentos` sin filtro global | **CORREGIDO** → redirige a `RevisionDocumental/Index` |
| `DocumentoController` con `[AocrAuthorize(Modulo = "Documento")]` | **NUEVO** |

**Archivo:** `DocumentoController.cs`

---

## ADM-1 — Pruebas 403 (unitarias)

| Escenario | Resultado esperado | Test |
|-----------|-------------------|------|
| RT → Informe Técnico inspector | Denegado | `Adm1_Rt_InformeTecnicoInspector_DebeDenegar` |
| RT → Firmar LV | Denegado | `Adm1_Rt_FirmarLv_DebeDenegar` |
| Inspector → Generar AOCR final | Denegado | `Adm1_Inspector_GenerarAocrFinal_DebeDenegar` |
| Coordinador → Guardar informe | Denegado | `Adm1_Coordinador_ModificarInformeTecnico_DebeDenegar` |
| Dirección → Subir documentos RT | Denegado | `Adm1_Direccion_CargarDocumentosComoRt_DebeDenegar` |
| Inspector no asignado → Detalle | Denegado | `Adm1_InspectorNoAsignado_DetalleInspeccion_DebeDenegar` |
| RT → Cambiar estado inspección | Denegado | `GateE_Rt_CambiarEstadoInspeccion_DebeDenegar` |

**Archivo:** `AOCR.Tests/Unit/GateEAuthorizationTests.cs`

> En IIS: acceso directo debe redirigir a `Error/NoAutorizado` (web) o JSON 403 (AJAX) vía `AocrAuthorizeAttribute`.

---

## E2 / DT-2 — Botones hacia ViewModel

`SolicitudAocrFlujoViewModel` ampliado con flags centralizados:

- `PuedeGenerarOrden`, `PuedeCargarDocumentos`, `PuedeEnviarCoordinacion`
- `PuedeRevisarTecnico`, `PuedeGenerarLv`, `PuedeGenerarInforme`
- `PuedeFirmarDireccion`, `PuedeDescargarFinal`

El builder consulta `AocrAuthorizationService` — la vista puede consumir `ViewBag.Flujo.*` sin recalcular permisos.

**Archivo:** `SolicitudAocrFlujoViewModel.cs`

---

## E6 / DT-4 — Estados legacy

- Catálogo `EstadosSolicitudAOCR` permanece marcado LEGACY/no canónico.
- **Nuevo:** `AocrEstadoService.NormalizarDesdeLegacyCatalogo()` mapea equivalencias controladas hacia `EstadoSolicitud`.
- Capas activas (CapaNegocio/DAOs) no referencian el catálogo legacy.

**Archivo:** `AocrEstadoService.cs`

---

## E7 — Correos idempotentes

| Servicio | event_key |
|----------|-----------|
| `SolicitudAocrCorreoService` | OK (Gate D) |
| `AocrEmailFlujoService` | OK |
| `InspeccionCorreoService` | OK |
| `OrdenRecaudacionDAO` | OK |
| **`NotificacionService`** | **CORREGIDO** — `ORDEN:{tipo}:{ordenId}:{correlation}:{email}` |
| Remitente | `no_reply@aviacioncivil.gob.ec` vía `AocrEmailService.CorreoNoReply` |

**Archivo:** `NotificacionService.cs`

---

## E4 / DT-5 — PDFs AOCR/CL

**Pendiente E2E manual:** validar formato institucional, firmas, márgenes y ausencia de variables internas en PDF AOCR y Condiciones/Limitaciones tras despliegue IIS.

Tests existentes: `PdfGeneratorTests.cs` (estructura básica).

---

## Validaciones técnicas Gate E

| # | Control | Estado |
|---|---------|--------|
| 1 | No confiar en botones ocultos | Backend valida en POST |
| 2 | POST valida permiso y estado | AocrAuthorize + servicios |
| 3 | Descarga valida propiedad documento | ValidarAccesoInspeccion / Detalle solicitud |
| 4 | Cambios registran historial | Servicios workflow (existente) |
| 5 | Trámites cerrados no editables | EstadoSolicitud + auth |
| 6 | Documentos firmados no modificables | Reglas LV/informe |
| 7 | No saltos de estado | Matriz transiciones |
| 8 | No secrets expuestos | SecureConfigurationService |
| 9 | CSRF | `[ValidateAntiForgeryToken]` en POST críticos |
| 10–11 | Carga archivos tamaño/extensión | Validaciones Documento/Inspeccion |
| 12 | Logs sin datos sensibles | Logging institucional |

---

## Criterios de aceptación

| Criterio | Estado |
|----------|--------|
| Acciones críticas con AocrAuthorize | ✅ |
| URL sin permiso → 403 | ✅ código + tests; E2E IIS pendiente |
| Inspector solo trámites asignados | ✅ |
| RT solo trámites propios | ✅ |
| Coordinador estados coordinación | ✅ |
| Dirección solo firma finales | ✅ |
| Correos idempotentes | ✅ (NotificacionService corregido) |
| Estados normalizados | ✅ normalizador legacy |
| PDFs revisados | ⏳ manual |
| Sin errores 500 por auth | ✅ admin bypass DAO |
| Gate E aprobado | ✅ código/tests; E2E parcial |

---

## Próximos pasos recomendados

1. Republicar a `publicacion1` y reciclar App Pool IIS.
2. E2E ADM-1 en browser (6 escenarios de la tabla).
3. Revisión visual PDF AOCR #12 y CL tipo 3.
4. Verificar auditoría de intentos inválidos en tabla/log institucional.
