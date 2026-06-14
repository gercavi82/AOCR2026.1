# AOCR — Revisión integral del flujo (matrices y plan de implementación)

**Fecha:** 2026-06-11  
**Alcance:** RT/Solicitante → recaudación → documentación → coordinación → inspector → inspección → AOCR/Condiciones → firma DIRDAC → cierre.

**Pruebas manuales post-deploy:** `docs/GUIA_PRUEBAS_POST_REPUBLICACION.md`  
**Plan de cierre por rol (2 entregables/rol):** `docs/PLAN_CIERRE_POR_ROL.md`  
**Manual de usuario:** `docs/MANUAL_USUARIO_AOCR.md`  
**Flujo RT→AOCR:** `docs/MANUAL_FLUJO_RT_A_AOCR.md` · **Guía visual:** `docs/GUIA_VISUAL_FLUJO_RT_AOCR.md`  
**Checklist doc. 100%:** `docs/CHECKLIST_DOCUMENTACION_100.md`  
**Hoja de ruta publicación:** `docs/HOJA_RUTA_PUBLICACION.md`  
**Manual técnico:** `docs/MANUAL_TECNICO_AOCR.md`  
**Definición de done (100%):** §12 de este documento

---

## 1. Diagnóstico de causa raíz (estado actual)

### 1.1 Problemas ya corregidos en esta iteración

| Síntoma | Causa raíz | Corrección |
|--------|------------|------------|
| Error 500 al login coordinador (`42703`) | SQL estático referenciaba columnas inexistentes (`tecnico_responsable_cedula`) | `ExpresionTieneInspectorEfectivo` adaptativo por esquema |
| Solicitud #12 en bandeja pero asignación falla | `AsignarInspectores` exigía recaudación finalizada aunque estado = `En Revision`; última orden `ANULADA` | Alineación bandeja ↔ asignación: no exigir recaudación en estados de coordinación |
| Contador sidebar ≠ bandeja (riesgo) | Lógica duplicada en `SidebarMenuBuilder` | `AocrSidebarCounterService` centraliza contadores coordinación |
| `AOCR EMITIDO` clasificado como `AOCR LEGALIZADO` en SQL | Desalineación `EstadoSolicitudSql` vs C# | Tokens SQL separados para emitido vs legalizado |
| Matriz de transiciones fragmentada | Excepciones en `SolicitudEstadoTransitionBL` sin servicio único | `AocrFlujoService` concentra reglas extendidas |
| Post-aceptación documental cerraba en `Finalizado` | Emisión/renovación iba a `FirmadoCoordinador` y la descarga del PDF forzaba `Finalizado` | Firma coordinador → `PendienteAsignacionRT` (tipos 1/2); modificación tipo 3 → `FirmadoCoordinador`; sin atajo a cierre |
| Rama modificación “nuevo aeropuerto” ausente | `SolicitudModificacionTieneNuevoAeropuertoDeclarado` definido pero no usado en flujo | `AocrModificationWorkflowService` + acción `CerrarFaseDocumentalNuevoAeropuertoModificacion` |
| Texto fantasma azul en `/Tecnico` | Reglas `::selection` / anti-fantasma no cubrían `.tecnico-index-table` | Fix en `aocr-contrast.css`, `aocr-datatables.css`, `Tecnico/Index.cshtml` |

### 1.2 Brechas estructurales pendientes (no parches aislados)

1. **Autorización:** `[AocrAuthorize]` aplicado en `Tecnico`, `Documento`, `CoordinacionJefatura` (acciones críticas) y `SolicitudAOCR/FirmarAceptacionDocumental`. Persisten acciones legacy con solo `[Authorize(Roles)]` en `InspeccionController` y en acciones de modificación AOCR (`Inspector,Administrador`).
2. **Lógica de flujo en vistas:** `Detalle.cshtml` consume `SolicitudAocrFlujoViewModel` parcialmente; `_FormularioEmisionAOCR.cshtml` aún decide botones en Razor.
3. **Workflows fragmentados:** post-pago, final, modificación, revisión documental en servicios separados (matriz declarativa parcial en `AocrFlujoService`).
4. **Legacy activo:** `EstadosSolicitudAOCR.cs`, `EstadoOrdenService` duplican constantes.
5. **Correos:** `AocrPostPagoWorkflowService` migrado a `AocrEmailFlujoService` (idempotencia); `SolicitudAocrCorreoService` ya tenía deduplicación por `event_key`.
6. **Fases LV → firma DIRDAC:** validaciones centralizadas en `AocrFlujoValidacionService`; auditoría completa de controladores Inspección/Informe pendiente de prueba manual.

---

## 2. Arquitectura objetivo (implementada parcialmente)

```
EstadoSolicitud / EstadoSolicitudSql  ← fuente canónica persistencia
         ↓
AocrEstadoService                     ← normalización C# + claves institucionales
         ↓
AocrFlujoService                      ← transiciones, roles, reglas asignación
         ↓
SolicitudEstadoTransitionBL           ← cambio de estado + historial + correo
AocrAuthorizationService              ← permisos por módulo/acción/recurso
*BandejaService / AocrSidebarCounterService ← bandejas = contadores
```

---

## 3. Matriz de estados institucionales

Clave institucional (`AocrEstadoService.NormalizarClaveInstitucional`) → estado canónico C#:

| Clave institucional | Estado canónico (persistencia) | Rol propietario | Bandeja principal |
|---------------------|--------------------------------|-----------------|-------------------|
| `DOCUMENTACION_EN_CARGA` | Pendiente | RT | Solicitudes habilitadas |
| `PAGO_PENDIENTE` / `PAGO_APROBADO` | Pago Pendiente / Validado | Financiero / RT | Financiero / RT |
| `EN_REVISION_COORDINADOR` | En Revision | Coordinador | Tecnico/Index, Dashboard |
| `DEVUELTO_RT_OBSERVACIONES` | Observada | RT | Subsanaciones |
| `SUBSANADA` | Subsanada | Coordinador | Cola documental |
| `DOCUMENTACION_ACEPTADA_COORDINADOR` | Aceptacion Documental | Coordinador / Inspector (mod.) | Asignación inspector / resolución modificación |
| `PENDIENTE_ASIGNACION_INSPECTOR` | Pendiente Asignacion RT | Coordinador | Tecnico/Index, bandeja asignación |
| `REQUIERE_INSPECCION` | Requiere Inspeccion | RT (orden recaudación) | OrdenRecaudacion/Nueva |
| `FIRMADO_COORDINADOR` | Firmado Coordinador | Inspector (mod. sin nuevo aeropuerto) | Resolución CL / derivar inspección |
| `INSPECTOR_ASIGNADO` | En Inspeccion | Inspector | Inspeccion/Index |
| `EN_INSPECCION` | En Inspeccion | Inspector | Inspecciones asignadas |
| `AOCR_EN_ELABORACION` | AOCR En Elaboracion | Inspector/Coord. | Informe/AOCR |
| `AOCR_EN_REVISION_COORDINADOR` | AOCR En Revision | Coordinador | Revisión formal AOCR |
| `AOCR_ENVIADO_DIRDAC` | Enviado DCAV | DIRDAC | Firmas pendientes |
| `AOCR_FIRMADO` | Firmado DCAV | DIRDAC | Liberación |
| `DOCUMENTOS_FINALES_DISPONIBLES` | AOCR Emitido/Recibido | RT / Inspector | Descargas finales |
| `CERRADO` | Finalizado | Sistema | Historial |
| `ANULADO` | Anulada | Admin | — |

---

## 4. Matriz de roles × acciones (backend)

Validación en `AocrFlujoService.RolPuedeEjecutarAccion` + `AocrAuthorizationService`:

| Acción | RT | Financiero | Coordinador | Inspector | DIRDAC | Admin |
|--------|:--:|:----------:|:-----------:|:---------:|:------:|:-----:|
| Crear orden / cargar comprobante | ✓ | | | | | ✓ |
| Aprobar pago | | ✓ | | | | ✓ |
| Cargar/enviar documentación | ✓ | | | | | ✓ |
| Revisar/aceptar documentación | ✓ | | ✓ | | | ✓ |
| Asignar inspector | | | ✓ | | | ✓ |
| Cerrar fase documental mod. (nuevo aeropuerto) | | | | ✓ | | ✓ |
| Generar CL modificación (sin nuevo aeropuerto) | | | | ✓ | | ✓ |
| Crear orden recaudación post-mod. nuevo aeropuerto | ✓ | | | | | ✓ |
| Revisión técnica documental | | | | ✓ | | ✓ |
| LV / Informe Técnico | | | | ✓ | | ✓ |
| Revisar AOCR formal | | | ✓ | | | ✓ |
| Firmar AOCR/Condiciones final | | | | | ✓ | ✓ |
| Liberar documentos finales | | | | | ✓ | ✓ |

**Regla:** toda acción debe validarse en controlador/servicio, no solo ocultando botones.

---

## 5. Matriz de bandejas por rol

| Rol | Servicio / DAO | Contador sidebar |
|-----|----------------|------------------|
| RT | `AocrSidebarCounterService.ObtenerContadoresRt` | Unificado en Fase 5 |
| Coordinador | `CoordinacionBandejaService` | `AocrSidebarCounterService.ObtenerContadoresCoordinacion` |
| Inspector | `InspectorBandejaService` | `AocrSidebarCounterService.ObtenerContadoresInspector` |
| Financiero | `OrdenRecaudacionDAO` + `FinancialOrderStateHelper` | `AocrSidebarCounterService.ObtenerContadoresFinanciero` |
| DIRDAC | `DireccionBandejaService` | `AocrSidebarCounterService.ObtenerContadoresDireccion` |

**Regla:** contador = misma query que bandeja (coordinación ya unificada en esta iteración).

---

## 6. Fases de implementación (plan maestro)

| Fase | Entregable | Estado |
|------|------------|--------|
| **1** | `AocrEstadoService` | ✅ Implementado |
| **2** | Paridad SQL/C# (`EstadoSolicitudSql`) | ✅ Parcial (AOCR emitido/legalizado) |
| **3** | `AocrFlujoService` + delegación transiciones | ✅ Implementado |
| **4** | Autorización unificada en controladores críticos | ✅ Parcial (Tecnico, Documento, CoordinacionJefatura, FirmarAceptacion) |
| **5** | `AocrSidebarCounterService` completo | ✅ Implementado (RT + financiero + roles previos) |
| **6** | Email idempotente por evento de flujo | ✅ Parcial (`AocrEmailFlujoService` + post-pago) |
| **7** | ViewModels sin lógica de estado en Razor | ⏳ Parcial (`Detalle.cshtml` + `SolicitudAocrFlujoViewModel`) |
| **8** | Fases LV → Informe → NC → AOCR → firma | ⏳ Parcial (`AocrFlujoValidacionService`; prueba manual pendiente) |
| **9** | PDFs institucionales | ⏳ Revisión por documento |
| **10** | Pruebas integrales Casos 1–10 | ⏳ Parcial (unit tests base + rama nuevo aeropuerto) |

---

## 7. Archivos modificados / creados (esta iteración)

### Nuevos
- `CapaNegocio/Services/AocrEstadoService.cs`
- `CapaNegocio/Services/AocrFlujoService.cs`
- `CapaNegocio/Services/AocrSidebarCounterService.cs`
- `AOCR.Tests/Unit/AocrEstadoServiceTests.cs`
- `AOCR.Tests/Unit/AocrFlujoServiceTests.cs`
- `AOCR.Tests/Unit/AocrModificationNuevoAeropuertoTests.cs`
- `AOCR.Tests/Unit/RevisionDocumentalFirmaPlanningTests.cs`
- `docs/AOCR_FLUJO_INTEGRAL_MATRICES.md`

### Modificados (sesión completa incl. fixes previos)
- `CapaDatos/Constants/EstadoConstants.cs` (sin atajo `FirmadoCoordinador → Finalizado`)
- `CapaDatos/Constants/EstadoSolicitudSql.cs`
- `CapaDatos/DAOs/SolicitudAOCRDAO.cs` (bandeja + asignación inspector)
- `CapaNegocio/Services/AocrModificationWorkflowService.cs` (rama nuevo aeropuerto)
- `CapaNegocio/Services/RevisionDocumentalService.cs` (destino firma por tipo solicitud)
- `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- `CapaNegocio/Services/AocrAuthorizationService.cs`
- `CapaPresentacion/Controllers/SolicitudAOCRController.cs` (firma, descarga, cierre fase nuevo aeropuerto)
- `CapaPresentacion/Views/SolicitudAOCR/Detalle.cshtml` (UI bifurcada modificación)
- `CapaPresentacion/Content/aocr-contrast.css`, `aocr-datatables.css`
- `CapaPresentacion/Views/Tecnico/Index.cshtml`
- `CapaPresentacion/Helpers/SidebarMenuBuilder.cs`
- `CapaNegocio/CapaNegocio.csproj`
- `AOCR.Tests/Unit/CoordinacionBandejaEstadoTests.cs`
- `AOCR.Tests/Unit/EstadoSolicitudTransitionMatrixTests.cs`
- `AOCR.Tests/Unit/OperationalFlowCharacterizationTests.cs`
- `AOCR.Tests/Unit/AocrModificationAuthorizationTests.cs`
- `AOCR.Tests/AOCR.Tests.csproj`

---

## 8. Casos de prueba — estado

| Caso | Descripción | Estado |
|------|-------------|--------|
| **7** | Bandejas/contadores coordinación #12 | ✅ Corregido (log + unit tests) |
| **6** | URL directa sin permiso | ✅ Parcial (Fase 4 en módulos críticos) |
| **1** | Flujo satisfactorio completo (emisión → asignación RT) | ⏳ Prueba manual post-deploy |
| **3** | Modificación con nuevo aeropuerto → orden recaudación | ✅ Implementado; prueba manual pendiente |
| **5** | Informe no satisfactorio → NC | ⏳ Fase 8 |
| **9** | Correos desde no_reply@ | ✅ Canal central; idempotencia post-pago activa |

---

## 9. Próximos pasos operativos inmediatos

1. **Republicar** `CapaDatos.dll`, `CapaNegocio.dll`, `CapaPresentacion.dll` y CSS en `publicacion1`.
2. **Reiniciar IIS** y ejecutar la guía **`docs/GUIA_PRUEBAS_POST_REPUBLICACION.md`** (escenarios A–D).
3. Continuar **Fase 7**: migrar botones restantes de `Detalle.cshtml` y `_FormularioEmisionAOCR.cshtml` a `SolicitudAocrFlujoViewModel`.
4. Continuar **Fase 8**: prueba manual Inspección → LV → Informe → AOCR final con solicitud #12 o nueva.

---

## 10. Pendientes reales (dependencias externas)

- Certificados de firma digital DIRDAC (si aplica en producción).
- Credenciales SMTP institucional (ya configurado; validar en entorno publicado).
- Datos productivos de esquema parcial (columnas ausentes en `aocr_tbsolicitud` — mitigado con introspección dinámica).

---

## 11. Correcciones de coherencia con diagramas institucionales

### 11.1 Firma de aceptación documental (post-coordinador)

Destino calculado en `RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental`:

| Tipo solicitud | Destino tras firma | Siguiente paso institucional |
|----------------|-------------------|------------------------------|
| **1** Emisión | `Pendiente Asignacion RT` | Coordinación asigna inspector vía `/Tecnico` |
| **2** Renovación | `Pendiente Asignacion RT` | Idem emisión |
| **3** Modificación | `Firmado Coordinador` | Resolución en panel de modificación (ver §11.2) |

**Regla eliminada:** descargar el PDF de aceptación documental **ya no** cierra la solicitud en `Finalizado`.

### 11.2 Modificación de Condiciones y Limitaciones (tipo 3)

Tras `Aceptacion Documental`, el inspector resuelve la modificación por una de **dos ramas**:

```
                    Aceptacion Documental (tipo 3)
                              |
              +---------------+---------------+
              |                               |
    Sin aeropuertos declarados      Con AeropuertosEcuador
    (campo vacío)                   (nuevo aeropuerto)
              |                               |
    +---------+---------+                     |
    |                   |                     |
 Generar CL      Derivar inspección    Cerrar fase documental
 directo         (Requiere Inspeccion)  (única vía permitida)
    |                   |                     |
    v                   v                     v
Generado CL      Requiere Inspeccion    Requiere Inspeccion
    |                   |                     |
 Revisión coord.    RT: orden           RT: orden
 → DCAV → firma      recaudación         recaudación
                      |                     |
                      +---------+-----------+
                                v
                         Flujo inspección
                         estándar → AOCR
```

**Detección:** `AocrModificationWorkflowService.TieneNuevoAeropuertoDeclarado` — modificación tipo 3 con `AeropuertosEcuador` no vacío.

**Bloqueos institucionales:**
- `PrepararGeneracionCondicionesLimitaciones` → rechaza si hay nuevo aeropuerto.
- `PrepararRequiereInspeccion` (genérico) → rechaza si hay nuevo aeropuerto; exige `CerrarFaseDocumentalNuevoAeropuertoModificacion`.

**Acción backend:** `SolicitudAOCRController.CerrarFaseDocumentalNuevoAeropuertoModificacion`  
**Roles:** `Inspector, Administrador`  
**Servicio:** `AocrModificationWorkflowService.EjecutarCierreFaseDocumentalNuevoAeropuerto` → `Requiere Inspeccion`

**UI:** `Detalle.cshtml` — panel “Resolución de modificación” bifurcado; panel RT con enlace a `OrdenRecaudacion/Nueva` cuando `RtDebeSolicitarInspeccionNuevoAeropuerto`.

### 11.3 Transiciones desde `Firmado Coordinador`

Ya **no** transiciona a `Finalizado`. Rutas válidas:

- `Pendiente Asignacion RT`
- `Requiere Inspeccion`
- `Generado Condiciones Limitaciones`

Fuente: `EstadoConstants.cs`, `AocrFlujoService.cs`.

### 11.4 Tests de regresión añadidos

| Archivo | Cobertura |
|---------|-----------|
| `RevisionDocumentalFirmaPlanningTests` | Destino firma por tipo 1/2/3 |
| `EstadoSolicitudTransitionMatrixTests` | Matriz sin atajo a `Finalizado` |
| `AocrFlujoServiceTests` | Transiciones `FirmadoCoordinador` |
| `AocrModificationNuevoAeropuertoTests` | Rama nuevo aeropuerto (plan + bloqueos) |
| `AocrModificationAuthorizationTests` | Autorización acción cierre fase |
| `OperationalFlowCharacterizationTests` | Cableado servicio ↔ controlador |

---

## 12. Definición de done — checklist al 100%

**Regla:** el sistema AOCR se considera **100% funcional** solo cuando todos los niveles aplicables están marcados ✅.

**Guías operativas:** `GUIA_PRUEBAS_POST_REPUBLICACION.md` · `PLAN_CIERRE_POR_ROL.md` · `GUIA_INSPECTOR_SOLICITUD_12.md` · **`GUIA_VISUAL_FLUJO_RT_AOCR.md`** · **`CHECKLIST_DOCUMENTACION_100.md`**

---

### 12.1 Nivel A — Correcciones de esta iteración (código + deploy)

| ☐ | Criterio | Cómo validar |
|---|----------|--------------|
| ☐ | A1 | Republicación en `publicacion1` con DLL/CSS recientes | Timestamps en `bin\` y `Content\` |
| ☐ | A2 | App pool / IIS reciclado en servidor real | Reinicio confirmado |
| ☐ | A3 | Escenario A: `/Tecnico` sin texto fantasma azul | Ctrl+F5 + inspección visual |
| ☐ | A4 | Escenario B2: firma coord. emisión → `Pendiente Asignacion RT` | Detalle #12 o solicitud tipo 1/2 |
| ☐ | A5 | Escenario B3: descarga constancia **no** cierra en `Finalizado` | Estado antes/después descarga |
| ☐ | A6 | Escenario C: mod. con aeropuertos → cierre inspector → `Requiere Inspeccion` | Panel único “Cerrar fase…” |
| ☐ | A7 | Escenario D: mod. sin aeropuertos → ramas CL / inspección clásicas | Botones bifurcados correctos |
| ☐ | A8 | Tests unitarios de regresión compilan y pasan | `AOCR.Tests` (§11.4) |

**Done Nivel A:** A1–A8 ✅

---

### 12.2 Nivel B — Flujo emisión completo (solicitud #12)

| ☐ | ID | Rol | Entregable |
|---|-----|-----|------------|
| ☐ | COO-1 | Coordinación | Firma revisión final + asignación inspector en `/Tecnico` |
| ☐ | INS-1 | Inspector | Cierre documental → LV firmada → Informe firmado → envío Dirección |
| ☐ | DIR-1 | DIRDAC | Aprobar informe técnico en `PendientesDireccion` |
| ☐ | DIR-2 | DIRDAC | Firma AOCR/CL + documentos finales descargables por RT |

**Done Nivel B:** COO-1 → INS-1 → DIR-1 → DIR-2 ✅

---

### 12.3 Nivel C — Flujo modificación (tipo 3)

| ☐ | ID | Rol | Entregable |
|---|-----|-----|------------|
| ☐ | INS-2a | Inspector | Mod. **con** aeropuertos: solo cierre institucional (Escenario C) |
| ☐ | RT-1 | RT | Tras `Requiere Inspeccion`: orden recaudación en `/OrdenRecaudacion/Nueva` |
| ☐ | INS-2b | Inspector | Mod. **sin** aeropuertos: CL directo o derivar inspección (Escenario D) |

**Done Nivel C:** INS-2a + RT-1 + INS-2b ✅

---

### 12.4 Nivel D — Rama insatisfactoria (NC)

| ☐ | ID | Rol | Entregable |
|---|-----|-----|------------|
| ☐ | COO-2 | Coordinación | Aprobar NC → nueva inspección **o** subsanación RT |
| ☐ | RT-2 | RT | Subsanación documental **bloqueada** hasta aprobación NC; **habilitada** después |
| ☐ | FIN-1 | Financiero | Pago aprobado + correo idempotente (sin duplicados) |
| ☐ | FIN-2 | Financiero | Contador sidebar financiero = filas bandeja |

**Done Nivel D:** COO-2 + RT-2 + FIN-1 + FIN-2 ✅ (Caso 5 + Caso 1)

---

### 12.5 Nivel E — Plataforma endurecida (desarrollo + QA)

| ☐ | Fase | Entregable | Archivos / ámbito principal |
|---|------|------------|----------------------------|
| ☐ | E1 | **4** Autorización unificada | `InspeccionController`, acciones mod. AOCR → `[AocrAuthorize]` |
| ☐ | E2 | **7** ViewModels sin lógica Razor | `Detalle.cshtml`, `_FormularioEmisionAOCR.cshtml` → `SolicitudAocrFlujoViewModel` |
| ☐ | E3 | **8** Validación cadena LV→AOCR auditada | `AocrFlujoValidacionService` + prueba manual firmada |
| ☐ | E4 | **9** PDFs institucionales revisados | Plantillas aceptación, CL, AOCR |
| ☐ | E5 | **10** Casos 1–10 | Checklist integrado o tests E2E |
| ☐ | E6 | **2** Paridad SQL/C# completa | `EstadoSolicitudSql` vs `EstadoConstants` |
| ☐ | E7 | **6** Correos idempotentes globales | Todos los eventos de flujo, no solo post-pago |
| ☐ | E8 | Legacy retirado | `EstadosSolicitudAOCR`, duplicados de constantes/servicios |
| ☐ | E9 | **ADM-1** URLs sin permiso → 403 | Caso 6 en módulos no migrados |
| ☐ | E10 | **ADM-2** Inventario AOCR legacy | `Health/Dashboard` documentado |

**Done Nivel E:** E1–E10 ✅

---

### 12.6 Dependencias externas (bloqueantes de producción)

| ☐ | Dependencia |
|---|-------------|
| ☐ | Certificados digitales `.p12` / `.pfx` vigentes (LV, Informe, DIRDAC) |
| ☐ | SMTP institucional validado en entorno publicado |
| ☐ | Esquema BD estable (columnas críticas presentes o mitigación aceptada) |

---

### 12.7 Resumen ejecutivo

| Nivel | Alcance | Estado típico hoy |
|-------|---------|-------------------|
| **A** | Correcciones sesión + deploy + escenarios A–D | ✅ Código · ⏳ pruebas manuales |
| **B** | Emisión #12 hasta documentos finales | ⏳ Pendiente cadena COO→INS→DIR |
| **C** | Modificación con/sin aeropuerto | ⏳ Implementado · prueba manual |
| **D** | NC / insatisfactorio / financiero | ⏳ Caso 5 + contadores |
| **E** | Robustez técnica (fases 4–10) | ⏳ Parcial |

**100% global = A + B + C + D + E + dependencias externas.**

---

### 12.8 Orden de ejecución recomendado

```
A1–A2 (deploy) → A3–A7 (guía post-republicación)
→ COO-1 → INS-1 → DIR-1 → DIR-2          [Nivel B]
→ INS-2a/RT-1/INS-2b                     [Nivel C]
→ COO-2/RT-2 + FIN-1/FIN-2               [Nivel D]
→ E1→E2→E3→E4→E5 + E6/E7/E8 + ADM        [Nivel E]
```

*Última actualización sección 12: 2026-06-11*

