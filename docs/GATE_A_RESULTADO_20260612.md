# Gate A — Resultado regresión post-republicación

**Fecha:** 2026-06-12  
**Entorno:** `C:\AOCR\publicacion1`  
**Versión front:** `VersionFront=1.0.0.4`  
**Guía:** `docs/GUIA_PRUEBAS_POST_REPUBLICACION.md`

---

## Veredicto: **NO APROBADO** (pendiente validación manual en IIS)

| Ámbito | Estado |
|--------|--------|
| Código + unit tests | ✅ 203/204 (1 omitida integración BD) |
| Escenarios manuales A1–D | ☐ Pendiente ejecución en servidor IIS |
| Republicación DLL | ✅ Tras correcciones Gate A |

**Para APROBAR Gate A:** completar checklist manual en navegador (incógnito + Ctrl+F5) y marcar 8/8 escenarios.

---

## Resultado por escenario

| ID | Escenario | Método | Resultado | Evidencia |
|----|-----------|--------|-----------|-----------|
| **A1** | Fix visual `/Tecnico` | Manual IIS | ☐ **PENDIENTE** | CSS `aocr-contrast.css`, `aocr-datatables.css` publicados; fix en `Tecnico/Index.cshtml` + `aocr-contrast.css` verificado en código |
| **B2** | Firma coord. #12 → `Pendiente Asignacion RT` | Manual IIS | ☐ **PENDIENTE** | Backend: `RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental` tipo 1/2 → `PendienteAsignacionRT`; acción `FirmarAceptacionDocumental` |
| **B3** | Descarga constancia no cierra trámite | Manual IIS | ☐ **PENDIENTE** | Test `DescargaAceptacionDocumentalRt_NoDebeCerrarTramiteComoFinalizado` ✅; sin `CambiarEstadoConReglasAocr(...Finalizado...)` en descarga |
| **B4** | Asignación inspector #12 id 43 | Manual IIS | ☐ **PENDIENTE** | Bandeja `CoordinacionBandejaService` + `Tecnico/Index`; logs `[GestionInspeccion]` en `InspeccionController` |
| **C2** | Mod. con aeropuertos → cierre institucional | Manual IIS | ☐ **PENDIENTE** | Tests `AocrModificationNuevoAeropuertoTests` ✅ (4/4); solo botón cierre cuando `TieneNuevoAeropuertoDeclarado` |
| **C3** | RT orden post modificación | Manual IIS | ☐ **PENDIENTE** | `RtDebeSolicitarInspeccionNuevoAeropuerto` + panel en `Detalle.cshtml`; ruta `/OrdenRecaudacion/Nueva` |
| **D** | Mod. sin aeropuertos (CL / inspección) | Manual IIS | ☐ **PENDIENTE** | `PrepararGeneracionCondicionesLimitaciones` / `PrepararRequiereInspeccion` sin aeropuertos ✅ en tests |
| **A8** | Pruebas unitarias | Automatizado | ✅ **203 OK / 1 Omitida** | `FlujoCompletoTests` omitido (requiere BD real) |

---

## A8 — Pruebas unitarias

```
Total:     204
Correctas: 203
Omitidas:  1  (integración BD)
Fallidas:  0
```

---

## Correcciones aplicadas en esta iteración

| Archivo | Cambio |
|---------|--------|
| `AocrEstadoService.cs` | Mapeo `AOCR_EMITIDO/RECIBIDO` → `DOCUMENTOS_FINALES_DISPONIBLES` |
| `RevisionDocumentalService.cs` | Firma aceptación: tipo null → `FirmadoCoordinador`; 1/2 → `PendienteAsignacionRT` |
| `AocrModificationWorkflowService.cs` | Lazy init `AocrFinalWorkflowService` (tests modificación sin email/BD) |
| `AocrAuthorizationService.cs` | Admin bypass LV/informe; fail-closed si BD inspección no disponible |
| `AocrFlujoValidacionService.cs` | Lazy init DAOs (validación id inspección sin BD) |
| `ConexionDAO.cs` | Fallback connection string si falta config |
| `AOCR.Tests/app.config` | ConnectionStrings + AS400 stub + binding `System.Memory` |
| `OperationalFlowCharacterizationTests.cs` | Assert firma con parámetro `TipoSolicitud` |

---

## Checklist manual (ejecutar en servidor IIS)

### A1 — Coordinación `/Tecnico`
- [ ] Login `GEN_COORDINACION`
- [ ] Abrir `/Tecnico/Index` — sin texto azul fantasma
- [ ] Sidebar sin funciones de Inspector
- [ ] DevTools: `aocr-contrast.css`, `aocr-datatables.css` → HTTP 200, `text/css`

### B2 — Solicitud #12
- [ ] `/SolicitudAOCR/Detalle/12` → panel “Revisión final de Coordinación”
- [ ] “Registrar revisión final” → estado **`Pendiente Asignacion RT`**

### B3 — Descarga constancia
- [ ] Descargar PDF aceptación documental
- [ ] Estado **no** cambia a `Finalizado`

### B4 — Asignación
- [ ] `/Tecnico/Index` → #12 visible
- [ ] Asignar inspector **id 43** → éxito
- [ ] Log: `[GestionInspeccion]` · estado `En Inspeccion`

### C2 / C3 / D — Modificación tipo 3
- [ ] Con aeropuertos: solo “Cerrar fase y derivar a inspección”
- [ ] RT: panel orden recaudación → `/OrdenRecaudacion/Nueva`
- [ ] Sin aeropuertos: botones CL / derivar inspección clásicos

---

## Próximo paso

1. Reciclar App Pool (`scripts/recycle-iis-fase0.ps1`)
2. Completar checklist manual arriba
3. Si 8/8 ✅ → **Gate B** con `GUIA_INSPECTOR_SOLICITUD_12.md`

---

*Generado automáticamente tras correcciones Gate A — 2026-06-12*
