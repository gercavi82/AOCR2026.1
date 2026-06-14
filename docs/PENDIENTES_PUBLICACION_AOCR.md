# Pendientes para publicación oficial — Sistema AOCR

**Versión:** 2026-06-11  
**Entorno piloto:** `C:\AOCR\publicacion1` · DLLs **11-jun-2026**  
**Caso de prueba:** solicitud **#12** (`DGAC-GOP-2026-AOCR012`), inspección **#11**, inspector **id 43**

---

## Resumen ejecutivo

| Pregunta | Respuesta |
|----------|-----------|
| ¿Código del flujo emisión implementado? | **Sí** (~85%) — fixes recientes desplegados |
| ¿Probado E2E hasta AOCR emitido? | **No** — cadena COO→INS→DIR incompleta en #12 |
| ¿Listo para producción institucional? | **No** — faltan Gates A–E + checklist prod + infra |
| ¿Listo para piloto QA interno? | **Sí**, tras reciclar IIS y ejecutar Gate A |

**Regla de cierre del proyecto:** 100% = Niveles **A + B + C + D + E** + dependencias externas + `CHECKLIST_PRODUCCION.md` firmado.

**Duración orientativa para cierre:** 10–15 días hábiles.

---

## Estado por área

| Área | % estimado | Bloquea go-live |
|------|------------|-----------------|
| Código flujo emisión (tipo 1/2) | 85% | Parcial |
| Prueba E2E #12 → AOCR RT | 40% | **Sí** |
| Modificación tipo 3 (C/D) | Código OK · prueba ☐ | **Sí** |
| Rama NC / informe insatisfactorio | 20% | **Sí** |
| Endurecimiento plataforma (Nivel E) | 35% | **Sí** |
| Documentación escrita | 90% | No |
| Capturas PNG (42) | 0% | No (entrega usuario) |
| Checklist producción (116 ítems) | 0% firmado | **Sí** |
| Deploy publicacion1 | Republicado | Verificar IIS reciclado |

---

## Tres gates obligatorios antes de prod

| Gate | Pregunta | Estado |
|------|----------|--------|
| **1 Funcional** | ¿Trámite emisión llegó de RT a AOCR emitido con todos los roles? | ☐ |
| **2 Operación** | ¿Servidor prod: SMTP, certificados, BD, backups, IIS? | ☐ |
| **3 Gobernanza** | ¿QA + Infra + PO firmaron checklist producción? | ☐ |

---

## Bloqueantes absolutos (no prod sin esto)

1. **Gate B completo** — solicitud #12 (u otra emisión) hasta PDF AOCR descargado por RT en `GeneradasFirmadas`
2. **Gate A** — 8 escenarios post-republicación (`GUIA_PRUEBAS_POST_REPUBLICACION.md`)
3. **Reciclar IIS** + verificar DLL/CSS actuales en servidor de prueba
4. **`CHECKLIST_PRODUCCION.md`** — mínimo seguridad + QA E2E + firmas
5. **SMTP + certificados `.p12`** vigentes en servidor destino (LV, informe, DIRDAC)
6. **Backup y rollback** probados antes del go-live

---

## Importante (prod con plan de remediación)

7. Gate C — modificación tipo 3 (escenarios C y D)  
8. Gate D — informe no satisfactorio (NC) + financiero idempotente  
9. Gate E1 — autorización unificada en `InspeccionController` (DT-1)  
10. 42 capturas PNG para manuales usuario (`docs/images/`)

---

## Mejora continua (post go-live mes 1)

11. DT-2 — ViewModels flujo en Razor (`Detalle.cshtml`)  
12. DT-4 — retirar legacy `EstadosSolicitudAOCR`  
13. Test integración BD (`FlujoCompletoTests` hoy `Inconclusive`)  
14. E7 — correos idempotentes en todos los eventos de flujo  

---

## Fase 0 — Deploy (0,5 día) · Dev + Infra

| ☐ | Tarea | Verificación |
|---|-------|--------------|
| ☐ | Build Release sin errores | Visual Studio |
| ☐ | Tests unitarios ~95 verdes | `AOCR.Tests` |
| ☐ | Publicar `FolderProfile4` → publicacion1 | Timestamps DLL |
| ☐ | Reciclar App Pool IIS | Reinicio confirmado |
| ☐ | Ctrl+F5 / incógnito en pruebas | Sin caché CSS |
| ☐ | Tag Git + release notes | `v2026.06.xx` |

**Archivos críticos en publicación:**

```
bin\CapaDatos.dll · CapaNegocio.dll · CapaPresentacion.dll
Content\aocr-contrast.css · aocr-datatables.css
Views\Documento\Lista.cshtml · RevisionDocumental\Index.cshtml
Views\Inspeccion\Detalle.cshtml · Tecnico\Index.cshtml · SolicitudAOCR\Detalle.cshtml
```

---

## Gate A — Regresión (1 día) · QA + Coordinación

| ☐ | ID | Escenario | Rol | Resultado esperado |
|---|-----|-----------|-----|-------------------|
| ☐ | A1 | Fix visual `/Tecnico` | Coordinación | Sin texto azul fantasma |
| ☐ | B2 | Firma coord. emisión #12 | Coordinación | → `Pendiente Asignacion RT` |
| ☐ | B3 | Descarga constancia | RT | **No** → `Finalizado` |
| ☐ | B4 | Asignación inspector #12 | Coordinación | → `En Inspeccion` · log `[GestionInspeccion]` |
| ☐ | C2 | Mod. con aeropuertos | Inspector | Solo cierre → `Requiere Inspeccion` |
| ☐ | C3 | RT orden post mod. | RT | `/OrdenRecaudacion/Nueva` |
| ☐ | D | Mod. sin aeropuertos | Inspector | Ramas CL / inspección |
| ☐ | A8 | Tests unitarios | Dev | Todos pasan |

**Salida Gate A:** 8/8 ✅ · Guía: `GUIA_PRUEBAS_POST_REPUBLICACION.md`

---

## Gate B — Emisión completa #12 (2–3 días) · CRÍTICO

Cadena obligatoria solicitud **#12** hasta **`AOCR Emitido/Recibido`**:

| ☐ | ID | Rol | Acción | Estado destino |
|---|-----|-----|--------|----------------|
| ☐ | E2E-01 | RT | Solicitud + orden + comprobante | Pago pendiente |
| ☐ | E2E-02 | Financiero | Aprobar pago | Docs habilitados |
| ☐ | E2E-03 | RT | Cargar docs + enviar | `En Revision` |
| ☐ | COO-1a | Coordinación | Revisión COO-1 + firma | `Pendiente Asignacion RT` |
| ☐ | COO-1b | Coordinación | Asignar inspector 43 | `En Inspeccion` |
| ☐ | INS-1a | Inspector | `modo=revision` — docs **PENDIENTE** | Decisiones inspector |
| ☐ | INS-1b | Inspector | Confirmar cierre documental #11 | LV habilitada |
| ☐ | INS-1c | Inspector | LV finalizada + firmada (.p12) | `firmado_tecnico=true` |
| ☐ | INS-1d | Inspector | Informe firmado | `ENVIADO_A_DIRDAC` |
| ☐ | DIR-1 | DIRDAC | Aprobar informe | `AOCR En Elaboracion` |
| ☐ | COO-2 | Coordinación | `ValidarAocr` | `AOCR En Revision` |
| ☐ | DIR-2 | DIRDAC | Firma AOCR | `AOCR Emitido/Recibido` |
| ☐ | RT-FIN | RT | `GeneradasFirmadas` — descarga PDF | Cierre RT |

**Puntos de fallo a vigilar:**

| Síntoma | Causa probable |
|---------|----------------|
| Docs ACEPTADO sin acción inspector | URL debe ser `modo=revision`, no `modo=ver` |
| Bandeja revisión vacía | Sin asignación en `aocr_tbinspeccion` |
| LV candado | Cierre documental no confirmado |
| Informe bloqueado | LV no firmada |
| AOCR no genera | Informe no aprobado DIRDAC |

**Guías:** `GUIA_INSPECTOR_SOLICITUD_12.md` · `MANUAL_FLUJO_RT_A_AOCR.md`

---

## Gate C — Modificación tipo 3 (1 día)

| ☐ | Escenario | Acción | Estado final |
|---|-----------|--------|--------------|
| ☐ | C | Inspector: `CerrarFaseDocumentalNuevoAeropuertoModificacion` | `Requiere Inspeccion` |
| ☐ | C-RT | RT: `OrdenRecaudacion/Nueva` | Orden creada |
| ☐ | D-CL | Inspector: `GenerarCondicionesLimitacionesModificacion` | `Generado CL` |
| ☐ | D-INS | Inspector: `MarcarRequiereInspeccionModificacion` | `Requiere Inspeccion` |
| ☐ | C-neg | CL con aeropuertos → debe **rechazar** | Mensaje §18.4 |

---

## Gate D — NC + Financiero (1–2 días)

| ☐ | ID | Qué probar |
|---|-----|------------|
| ☐ | FIN-1 | Pago aprobado + **un solo** correo por `event_key` |
| ☐ | FIN-2 | Contador sidebar financiero = filas bandeja |
| ☐ | COO-2 | Informe no satisfactorio → coordinación aprueba NC |
| ☐ | RT-2 | RT no subsana antes de NC; sí después de aprobación |

---

## Gate E — Endurecimiento técnico (3–5 días dev)

### Prioridad alta (antes de prod)

| ☐ | ID | Entregable |
|---|-----|------------|
| ☐ | E1 / DT-1 | `[AocrAuthorize]` en acciones críticas `InspeccionController` (47 `[Authorize]` legacy) |
| ☐ | E3 / DT-3 | Callers de revisión documental siempre filtran por inspector |
| ☐ | ADM-1 | URL sin permiso → 403 en módulos críticos |

### Prioridad media (mes 1 post go-live)

| ☐ | ID | Entregable |
|---|-----|------------|
| ☐ | E2 / DT-2 | Botones flujo → `SolicitudAocrFlujoViewModel` |
| ☐ | E4 / DT-5 | Revisión PDFs AOCR/CL con Legal |
| ☐ | E7 | Correos idempotentes globales |
| ☐ | E6 / DT-4 | Retirar `EstadosSolicitudAOCR` legacy |

### Prioridad baja

| ☐ | E5 | Casos 1–10 E2E checklist |
| ☐ | E10 | `FlujoCompletoTests` con BD real |
| ☐ | ADM-2 | Inventario AOCR legacy |

---

## Pre-producción — Infraestructura (Fase 6)

| ☐ | Categoría | Pendiente |
|---|-----------|-----------|
| ☐ | IIS | App Pool .NET 4.8, SSL prod, permisos `App_Data` |
| ☐ | BD | `AOCR_CONNSTR_POSTGRESQL`, backup diario probado |
| ☐ | AS400 | ODBC + credenciales en variables de entorno |
| ☐ | Email | SMTP prod + cola `email_queue` + prueba envío |
| ☐ | Certificados | `.p12` LV, informe inspector, firma DIRDAC en servidor |
| ☐ | Seguridad | CSRF, upload, headers HSTS/CSP |
| ☐ | Observabilidad | Logs, rotación, `/health` |
| ☐ | Rollback | Backup pre-deploy + restore probado |

**Dependencias externas:**

| ☐ | Responsable | Dependencia |
|---|-------------|-------------|
| ☐ | DIRDAC / TI | Certificados digitales vigentes |
| ☐ | Infra | SMTP `no_reply@` en prod |
| ☐ | DBA | Esquema BD estable |

**Documentos:** `CHECKLIST_IIS.md` · `CHECKLIST_PRODUCCION.md` · `VARIABLES_AMBIENTE.md` · `CHECKLIST_SEGURIDAD.md`

---

## Documentación pendiente (Fase 7)

| ☐ | Entregable | Estado |
|---|------------|--------|
| ☑ | Manuales PDF en `docs/export/` | Generados |
| ☐ | 42 capturas PNG `docs/images/` | **0/42** |
| ☐ | Regenerar PDF post-capturas | Pendiente |
| ☐ | Release notes usuario | Pendiente |
| ☐ | Capacitación RT / Inspector / Coord. (2h) | Pendiente |
| ☐ | Seguimiento CSV completado | `SEGUIMIENTO_PUBLICACION_AOCR.csv` |

---

## Go-live — Día D (Fase 8)

| ☐ | Momento | Acción |
|---|---------|--------|
| ☐ | T-24h | Backup BD + carpeta aplicación |
| ☐ | T-1h | Publicar Release → prod |
| ☐ | T-0 | Reciclar App Pool |
| ☐ | T+15m | Smoke: login todos los roles + `/health` |
| ☐ | T+30m | Smoke: `/Tecnico`, `/RevisionDocumental` sin 500 |
| ☐ | T+1h | Monitoreo logs 1 hora |

**Smoke test mínimo:**

| ☐ | Prueba |
|---|--------|
| ☐ | Login RT, Coordinación, Inspector, Financiero, DIRDAC |
| ☐ | Sidebar contadores sin error |
| ☐ | Correo financiero de prueba |
| ☐ | Log en `App_Data/Logs` |

---

## Deuda técnica documentada

| ID | Descripción | Prioridad |
|----|-------------|-----------|
| DT-1 | Autorización híbrida `InspeccionController` | Alta |
| DT-2 | Lógica botones en Razor | Media |
| DT-3 | `RevisionDocumentalService` — callers deben filtrar revisiones | Alta |
| DT-4 | Legacy `EstadosSolicitudAOCR` duplicado | Media |
| DT-5 | PDFs institucionales sin revisión formal | Media |

---

## Checklist producción — resumen (116 ítems)

Categorías sin marcar en `CHECKLIST_PRODUCCION.md`:

| Categoría | Ítems aprox. |
|-----------|--------------|
| Seguridad (auth, CSRF, secrets, upload) | 20 |
| Integridad datos (transacciones, SQL) | 15 |
| Manejo errores + headers | 14 |
| Observabilidad + auditoría | 11 |
| Resiliencia (correo, PDF) | 11 |
| Backups + monitoreo | 15 |
| Despliegue + tests | 12 |
| Documentación | 8 |
| QA E2E | 10 |
| **Firmas aprobación** | Dev · DBA · Seguridad · QA · Infra · PO |

---

## Plan 4 semanas sugerido

| Semana | Objetivo | Hitos |
|--------|----------|-------|
| **1** | Deploy + Gate A + inicio Gate B | MILE-G0, MILE-GA, COO-1, INS-1a |
| **2** | Cerrar Gate B + Gate C/D | MILE-GB, MILE-GC, MILE-GD |
| **3** | Gate E mínimo + infra pre-prod | MILE-GE, MILE-G2, capturas PNG |
| **4** | Checklist prod + go-live | MILE-G3, MILE-LIVE |

---

## Próximo paso inmediato

```text
1. Reciclar IIS en publicacion1
2. Ejecutar Gate A (8 escenarios) — GUIA_PRUEBAS_POST_REPUBLICACION.md
3. Si B4 OK → Gate B inspector 43 solicitud #12 — GUIA_INSPECTOR_SOLICITUD_12.md
```

**Seguimiento:** `docs/export/editable/SEGUIMIENTO_PUBLICACION_AOCR.csv`

---

## Índice documentación

| Documento | Contenido |
|-----------|-----------|
| `HOJA_RUTA_PUBLICACION.md` | Hoja de ruta detallada |
| `PENDIENTES_PUBLICACION_AOCR.md` | Este documento |
| `GUIA_PRUEBAS_POST_REPUBLICACION.md` | Gate A escenarios |
| `GUIA_INSPECTOR_SOLICITUD_12.md` | Gate B inspector |
| `MANUAL_FLUJO_RT_A_AOCR.md` | 16 fases RT→AOCR |
| `CHECKLIST_PRODUCCION.md` | 116 ítems gobernanza |
| `AOCR_FLUJO_INTEGRAL_MATRICES.md` | Definición done §12 |
| `SEGUIMIENTO_PUBLICACION_AOCR.csv` | Seguimiento Excel |

---

*Documento consolidado AOCR · 2026-06-11 · No apto para go-live hasta Gates 1–3 completos.*
