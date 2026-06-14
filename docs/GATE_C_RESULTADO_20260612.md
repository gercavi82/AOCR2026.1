# Gate C — Modificación tipo 3

**Fecha:** 2026-06-12  
**Alcance:** escenarios C, C-RT, D-CL, D-INS, C-neg (con/sin nuevo aeropuerto)  
**Build:** Release OK · Tests **206/206** (+3 nuevos) · Republicado `FolderProfile4` → `C:\AOCR\publicacion1`  
**Veredicto Gate C:** **NO APROBADO** (E2E manual pendiente; corrección crítica aplicada en código)

---

## Resumen ejecutivo

Revisión técnica del flujo modificación tipo 3 completada. Se detectó y corrigió un **bug crítico**: tras la firma de coordinación, las solicitudes tipo 3 quedan en **`Firmado Coordinador`**, pero el panel “Resolución de modificación” y los validadores backend solo aceptaban **`Aceptacion Documental`**, bloqueando de facto los escenarios C y D en el flujo institucional normal.

**Corrección aplicada** + mensaje institucional C-neg alineado + 3 tests nuevos.

En BD `dgac_des` **no existen solicitudes tipo 3** — los escenarios manuales C-RT (orden) y E2E completos requieren datos de prueba en IIS.

---

## Resultado por escenario

| Escenario | Rol | Acción | Resultado código/tests | Resultado manual |
|-----------|-----|--------|------------------------|------------------|
| **C** — con nuevo aeropuerto | Inspector | `CerrarFaseDocumentalNuevoAeropuertoModificacion` | **APROBADO** (unit) | **PENDIENTE** |
| **C-RT** — orden recaudación | RT | `/OrdenRecaudacion/Nueva` | **Código OK** | **PENDIENTE** |
| **D-CL** — sin aeropuerto, generar CL | Inspector | `GenerarCondicionesLimitacionesModificacion` | **APROBADO** (unit) | **PENDIENTE** |
| **D-INS** — sin aeropuerto, derivar inspección | Inspector | `MarcarRequiereInspeccionModificacion` | **APROBADO** (unit) | **PENDIENTE** |
| **C-neg** — CL con aeropuertos nuevos | Inspector | `GenerarCondicionesLimitacionesModificacion` | **APROBADO** (mensaje exacto) | **PENDIENTE** |

### C — Modificación con nuevo aeropuerto

| Validación | Estado |
|------------|--------|
| `TieneNuevoAeropuertoDeclarado` detecta `AeropuertosEcuador` / `AeropuertosEcuadorOtros` | OK |
| Solo botón “Cerrar fase y derivar a inspección” en UI (`Detalle.cshtml`) | OK |
| `PrepararCierreFaseDocumentalNuevoAeropuerto` → `Requiere Inspeccion` | OK |
| Bloqueo CL directo (`PrepararGeneracionCondicionesLimitaciones`) | OK |
| Bloqueo `MarcarRequiereInspeccionModificacion` genérico | OK (exige cierre institucional) |
| Historial vía `SolicitudEstadoTransitionBL.CambiarEstadoConReglasAocr` | OK (código) |
| Panel RT tras cierre (`puedeRtSolicitarInspeccionNuevoAeropuerto`) | OK |
| Notificación email | No verificado E2E |

**Estados válidos de entrada (post-corrección):** `Aceptacion Documental` **o** `Firmado Coordinador`.

### C-RT — Orden por RT

| Validación | Estado |
|------------|--------|
| Panel “Solicitar inspección del nuevo aeropuerto” en `Requiere Inspeccion` | OK (vista) |
| Enlace a `/OrdenRecaudacion/Nueva` | OK |
| Anti-duplicado: `ObtenerOrdenPendienteUsuarioAccion` redirige si hay orden borrador/pendiente | OK |
| Concepto `INSPECCION_EXT` obligatorio si `GenerarSolicitudInspeccionAlGuardar` | OK |
| Cálculo subtotal/admin/total en POST `Nueva` | OK |
| Evidencia orden creada en BD tipo 3 | **Sin datos** (0 solicitudes tipo 3) |

### D-CL — Sin aeropuerto → Condiciones y Limitaciones

| Validación | Estado |
|------------|--------|
| Botones “No requiere nueva inspección” + “Derivar a inspección” (rama `else` UI) | OK |
| `PrepararGeneracionCondicionesLimitaciones` → `Generado Condiciones y Limitaciones` | OK |
| Desde `Firmado Coordinador` (flujo real post-firma coord.) | OK (**corregido**) |
| PDF / revisión coordinación (`puedePrepararCondicionesModificacion`) | OK (vista) |

### D-INS — Marcar requiere inspección (sin aeropuerto)

| Validación | Estado |
|------------|--------|
| `PrepararRequiereInspeccion` → `Requiere Inspeccion` | OK |
| Rechaza si hay aeropuertos declarados | OK |
| Bloqueo documentos finales hasta inspección | OK (matriz estados) |

### C-neg — Caso negativo

**Intento:** `GenerarCondicionesLimitacionesModificacion` con aeropuertos declarados.

| Validación | Estado |
|------------|--------|
| `PuedeContinuar = false` | OK |
| No cambia estado | OK |
| Mensaje institucional exacto | OK |

```
La modificación incluye nuevos aeropuertos o condiciones que requieren inspección. Debe completar el flujo de inspección antes de generar Condiciones y Limitaciones.
```

Constante: `AocrModificationWorkflowService.MensajeRechazoClConInspeccionRequerida`

---

## Bug corregido en esta sesión

### Panel y backend ignoraban `Firmado Coordinador`

**Flujo institucional tipo 3:**

1. Coordinación cierra revisión → `Aceptacion Documental`
2. Coordinador firma → **`Firmado Coordinador`** (`RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental(3)`)
3. Inspector resuelve rama C o D

**Problema:** `puedeResolverRutaModificacion` y `Preparar*` solo aceptaban `Aceptacion Documental` → el inspector **no veía el panel** ni podía ejecutar POST tras la firma.

**Corrección:**

| Archivo | Cambio |
|---------|--------|
| `CapaNegocio/Services/AocrModificationWorkflowService.cs` | `EsEstadoResolucionModificacionPermitido` (Aceptacion + FirmadoCoordinador); mensaje C-neg institucional |
| `CapaPresentacion/Views/SolicitudAOCR/Detalle.cshtml` | Panel visible en `Firmado Coordinador` |
| `AOCR.Tests/Unit/AocrModificationNuevoAeropuertoTests.cs` | +3 tests FirmadoCoordinador y mensaje C-neg |

---

## Revisión técnica obligatoria

| Componente | Hallazgo |
|------------|----------|
| `SolicitudAOCRController` | Acciones POST delegan a `AocrModificationWorkflowService` — OK |
| `InspeccionController` | No interviene en resolución modificación tipo 3 — OK |
| `OrdenRecaudacionController` | `Nueva` con validaciones concepto/totales/anti-duplicado — OK |
| CondicionesLimitaciones | Vía `CoordinacionJefatura/EditarDocumentoValidacionAocr` — OK |
| `AocrModificationWorkflowService` | Matriz ramas C/D centralizada — OK (corregido FirmadoCoordinador) |
| `EstadoConstants` / `AocrFlujoService` | Transiciones `FirmadoCoordinador` → `RequiereInspeccion` / `Generado CL` — OK |
| `Detalle.cshtml` | UI bifurcada aeropuerto sí/no; panel RT en `Requiere Inspeccion` — OK |
| Autorización | `[Authorize(Roles = "Inspector,Administrador")]` en acciones inspector — OK |
| Historial | `CambiarEstadoConReglasAocr` registra transición — OK (código) |
| Notificaciones | Sin verificación E2E en esta sesión |

---

## BD — diagnóstico

```sql
-- scripts/gate-c-check-modificacion-tipo3.sql
SELECT * FROM aocr_tbsolicitud WHERE tipo_solicitud = 3;  -- 0 filas
```

No hay solicitudes tipo 3 en `dgac_des` para ejecutar C-RT ni capturas de orden.

---

## Tests ejecutados

```
AocrModificationNuevoAeropuertoTests: 8/8 OK
Suite completa AOCR.Tests: 206/206 OK (1 omitida)
```

Tests nuevos:

- `PrepararGeneracionCondicionesLimitaciones_SinAeropuerto_DesdeFirmadoCoordinador_DebePermitir`
- `PrepararCierreFaseDocumentalNuevoAeropuerto_DesdeFirmadoCoordinador_DebeIrARequiereInspeccion`
- `EsEstadoResolucionModificacionPermitido_FirmadoCoordinador_DebeSerTrue`

---

## Guía manual para completar Gate C en IIS

1. **C1** — RT crea solicitud `tipoSolicitud=3` con `AeropuertosEcuador=SEQM` (u otro).
2. Completar documentación hasta **`Aceptacion Documental`** → coordinador firma → **`Firmado Coordinador`**.
3. **C2** — Inspector abre `/SolicitudAOCR/Detalle/{id}` → panel resolución → “Cerrar fase y derivar a inspección” → estado **`Requiere Inspeccion`**.
4. **C3** — RT ve panel “Solicitar inspección del nuevo aeropuerto” → `/OrdenRecaudacion/Nueva` → concepto inspección → orden creada.
5. **D** — Repetir con solicitud tipo 3 **sin** aeropuertos → probar CL directo o derivar inspección.
6. **C-neg** — En solicitud con aeropuertos, intentar POST `GenerarCondicionesLimitacionesModificacion` → mensaje institucional, sin cambio de estado.

Referencia: [`GUIA_PRUEBAS_POST_REPUBLICACION.md`](GUIA_PRUEBAS_POST_REPUBLICACION.md) § Escenarios C y D.

---

## Criterios de aceptación Gate C

| # | Criterio | Estado |
|---|----------|--------|
| 1 | Modificación con aeropuerto nuevo → `Requiere Inspeccion` | ✅ Unit |
| 2 | RT puede generar orden | ⚠️ Código OK; sin evidencia BD |
| 3 | Sin aeropuerto puede generar CL | ✅ Unit |
| 4 | Rechazo CL cuando requiere inspección | ✅ Unit + mensaje |
| 5 | Sin saltos de flujo | ✅ Matriz estados |
| 6 | Sin errores 500 | ⚠️ No validado IIS |
| 7 | Sin errores JavaScript | ⚠️ No validado IIS |
| 8 | Historial registrado | ⚠️ Código OK; sin E2E |
| 9 | Gate C aprobado | **NO** (E2E manual pendiente) |

---

## Veredicto final

**Gate C: NO APROBADO** (falta ejecución manual en IIS con solicitud tipo 3 de prueba).

**Código: APROBADO con corrección crítica** — el flujo tipo 3 ya es operable desde `Firmado Coordinador`, mensaje C-neg alineado, tests en verde. Tras crear solicitud tipo 3 en ambiente IIS y ejecutar escenarios C → C-RT → D → C-neg, re-evaluar Gate C.
