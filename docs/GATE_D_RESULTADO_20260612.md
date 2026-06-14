# Gate D — No Conformidades + Financiero

**Fecha:** 2026-06-12  
**Alcance:** FIN-1, FIN-2, COO-2, RT-2  
**Build:** Release OK · Tests **214/214** · Republicado `FolderProfile4` → `C:\AOCR\publicacion1`  
**Veredicto Gate D:** **NO APROBADO** (E2E manual NC pendiente; correcciones código aplicadas)

---

## Resumen ejecutivo

Revisión técnica del flujo insatisfactorio → NC, bloqueo AOCR/CL, subsanación RT e idempotencia financiera.

**Hallazgos BD:**
- Correos `PAGO_APROBADO_*` con `event_key` único (cnt=1 por clave) — FIN-1 OK en datos históricos.
- Correos `SOLICITUD_INSPECTOR_ASIGNADO` con `event_key` NULL y duplicados — corregido en código.
- Sin inspecciones `RESULTADO_NO_SATISFACTORIO` en BD — COO-2 / RT-2 E2E no ejecutables.

**Correcciones aplicadas:**
1. `event_key` para todos los eventos de `SolicitudAocrCorreoService` (idempotencia).
2. Fallback `BuildAocrEventKey` sin correlationId.
3. Transición solicitud → `Observada` al aprobar NC para subsanación documental.
4. Notificación RT al habilitar subsanación post-NC.
5. Tests unitarios FIN-2 y Gate D NC.

---

## FIN-1 — Pago aprobado con correo único

| Validación | Resultado |
|------------|-----------|
| `AocrPostPagoWorkflowService.ProcesarPagoAprobado` | Idempotente por flags BD (`notificado_rt_modulo_habilitado`, `notificado_coordinador_pago_aprobado`) |
| `EncolarSiNoDuplicadoAsync` | Usa `BuildAocrEventKey` + `ExisteNotificacionAsync` |
| BD: `PAGO_APROBADO_RT_*` / `PAGO_APROBADO_COORDINADOR_*` | **11 claves únicas, cnt=1 cada una** |
| Remitente institucional | `AocrEmailService.NormalizarRemitenteInstitucional` → `no_reply@aviacioncivil.gob.ec` |
| Reintento/refresco cola | Segundo encolado omitido si `event_key` existe (unique index `uq_email_queue_event_key`) |

**Evidencia BD (extracto):**

| event_key | cnt |
|-----------|-----|
| `PAGO_APROBADO_RT_SOLICITUD_AOCR_HABILITADA_13_MANCHO2002@HOTMAIL.COM` | 1 |
| `PAGO_APROBADO_COORDINADOR_ASIGNACION_INSPECTOR_12_GERMAN.CAJAS@...` | 1 |

**Corrección adicional:** eventos `SOLICITUD_*` (p. ej. `INSPECTOR_ASIGNADO`) antes encolaban sin `event_key` → duplicados en solicitud #12 (4 entradas mismo segundo). Ahora siempre generan clave `AOCR:SOLICITUD_INSPECTOR_ASIGNADO:{solicitud}:{email}`.

**Manual IIS:** ⏳ Pendiente (aprobar pago y verificar un solo registro en `email_queue`).

---

## FIN-2 — Contador financiero = bandeja

| Componente | Consulta |
|------------|----------|
| Sidebar badge | `AocrSidebarCounterService.ObtenerContadoresFinanciero()` → `FinancialOrderStateHelper.EsPendienteGestion` |
| Bandeja `/Financiero/Index?estado=PENDIENTES_FINANCIERO` | `FinancialOrderStateHelper.CoincideFiltro(..., PendientesFinanciero)` |
| Normalización filtro | `FinancialOrderStateHelper.NormalizarFiltro` unifica `PAGOS_PENDIENTES`, `PENDIENTES`, etc. |

Ambos usan **`FinancialOrderStateHelper`** — alineados por diseño.

**Tests:** `FinancialOrderStateHelperTests` (4/4 OK).

**Manual IIS:** ⏳ Comparar badge sidebar vs filas en bandeja con filtro `PENDIENTES_FINANCIERO`.

---

## COO-2 — Informe no satisfactorio → NC

| Validación | Código |
|------------|--------|
| Resultado `INSATISFACTORIO` obligatorio | `InspeccionWorkflowService.EvaluarInspeccion` |
| Hallazgos / `NoConformidades` obligatorios | `AocrFlujoValidacionService` + `AsegurarNoConformidadDesdeInforme` |
| NC autogenerada (hallazgo ABIERTO) | `AsegurarNoConformidadDesdeInforme` → `aocr_tbhallazgo` |
| Estado inspección | `RESULTADO_NO_SATISFACTORIO` |
| Aprobación NC coordinación | `ValidarAprobacionNoConformidad` + `AprobarNoConformidadParaSubsanacionDocumental` / `AprobarNoConformidadParaNuevaInspeccion` |
| Bloqueo AOCR | `GeneracionAOCRService.InformeResultadoPermiteGeneracionAocr` + `ContarNoConformidadesActivas` |
| Bloqueo CL (modificación) | No aplica en ruta inspección estándar; AOCR/Condiciones bloqueados por NC activa |

**BD:** 0 inspecciones con resultado insatisfactorio — **E2E manual pendiente**.

**Panel coordinación:** `CoordinacionJefaturaController` + `DashboardInspeccionDAO.ObtenerNoConformidades()`.

---

## RT-2 — Subsanación RT bloqueada / habilitada

| Momento | Comportamiento | Evidencia |
|---------|----------------|-----------|
| **Antes** aprobación NC | `EstadoPermiteCargaCorreccionSolicitante` rechaza `RESULTADO_NO_SATISFACTORIO` | Mensaje: *"La subsanación documental del RT solo se habilita después de la aprobación formal de la no conformidad por coordinación."* |
| **Después** aprobación NC (subsanación) | Inspección → `OBSERVADA`; solicitud → **`Observada`** (**corregido**); RT puede `/SolicitudAOCR/Subsanar` y cargar en `/Inspeccion/Detalle` | `TransicionarSolicitudObservadaParaSubsanacion` |
| Notificación RT | `SolicitudAocrCorreoService.NotificarEvento(..., "OBSERVADA", correlationId: NC_SUBSANACION_{id})` | Con `event_key` idempotente |

**Transición permitida:** `AocrFlujoService`: `En Inspeccion` → `Observada` ✅

**Manual IIS:** ⏳ Requiere inspección con informe insatisfactorio firmado + aprobación NC coordinación.

---

## Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | `event_key` siempre; fallback sin correlationId |
| `CapaNegocio/Services/InspeccionWorkflowService.cs` | Solicitud → Observada + correo RT post-NC subsanación |
| `AOCR.Tests/Unit/FinancialOrderStateHelperTests.cs` | Tests contador/bandeja |
| `AOCR.Tests/Unit/GateDNoConformidadTests.cs` | Tests idempotencia + flujo NC |
| `AOCR.Tests/AOCR.Tests.csproj` | Referencias tests |
| `scripts/gate-d-check-financiero-nc.sql` | Diagnóstico BD |
| `docs/GATE_D_RESULTADO_20260612.md` | Este informe |

---

## Scripts diagnóstico

```powershell
dotnet run --project scripts\dev\SchemaProbeNet\SchemaProbeNet.csproj -- `
  "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;" `
  scripts\gate-d-check-financiero-nc.sql
```

---

## Guía manual Gate D (IIS)

### FIN-1
1. Financiero aprueba pago en `/Financiero/Index`.
2. Consultar `email_queue` WHERE `tipo_notificacion LIKE 'PAGO_APROBADO%'` AND `solicitud_id = X`.
3. Verificar **1 fila por destinatario/event_key**; reintentar aprobación → sin duplicado.

### FIN-2
1. Abrir sidebar → anotar badge "Pagos pendientes".
2. Ir `/Financiero/Index?estado=PENDIENTES_FINANCIERO`.
3. Contar filas → debe coincidir con badge.

### COO-2 + RT-2
1. Completar inspección hasta informe **INSATISFACTORIO** firmado (tipo `SIN_INSPECCION` o `CON_INSPECCION`).
2. Verificar NC en panel coordinación / hallazgo ABIERTO.
3. Intentar subsanación RT **antes** de aprobar NC → bloqueado.
4. Coordinación: `AprobarNoConformidadParaSubsanacionDocumental`.
5. RT: `/SolicitudAOCR/Subsanar/{id}` habilitado; cargar documentos versionados.
6. Verificar AOCR **no** generable (`GeneracionAOCRService` motivo NC/resultado).

---

## Criterios de aceptación Gate D

| # | Criterio | Estado |
|---|----------|--------|
| 1 | Un correo por event_key en pago | ✅ BD + código |
| 2 | Contador = bandeja financiero | ✅ Mismo helper + tests |
| 3 | Informe insatisfactorio genera NC | ✅ Código; ⏳ E2E |
| 4 | AOCR bloqueado | ✅ `GeneracionAOCRService` |
| 5 | CL bloqueadas si NC/insatisfactorio | ✅ Matriz AOCR |
| 6 | RT no subsana antes de NC | ✅ `EstadoPermiteCargaCorreccionSolicitante` |
| 7 | RT subsana después de NC | ✅ Corregido (solicitud Observada) |
| 8 | Historial completo | ⚠️ Código OK; ⏳ E2E |
| 9 | Sin errores 500 | ⏳ IIS |
| 10 | Sin errores JS | ⏳ IIS |
| 11 | Gate D aprobado | **NO** |

---

## Veredicto final

**Gate D: NO APROBADO**

- **Financiero (FIN-1/FIN-2):** código y datos históricos de pago coherentes; idempotencia reforzada para todos los eventos de solicitud.
- **NC (COO-2/RT-2):** lógica backend completa; gap RT subsanación corregido (solicitud → Observada tras NC); falta caso E2E en BD/IIS con informe insatisfactorio real.

Tras ejecutar la guía manual en IIS y verificar NC + subsanación en un trámite piloto, re-evaluar Gate D.
