# Gate B — Emisión completa solicitud #12 (RT → AOCR final)

**Fecha:** 2026-06-12  
**Caso:** `DGAC-GOP-2026-AOCR012` · SolicitudId **12** · Inspección **#11** · Inspector **43**  
**Build:** Release OK · Tests **203/203** · Publicación `FolderProfile4` → `C:\AOCR\publicacion1`  
**Veredicto Gate B:** **NO APROBADO** (E2E manual incompleto; fase inspector en curso)

---

## Estado inicial y final (BD `dgac_des`)

| Campo | Valor al inicio Gate B | Valor al cierre de esta sesión |
|-------|------------------------|--------------------------------|
| `aocr_tbsolicitud.estado` | En Inspeccion | **En Inspeccion** (sin cambio) |
| `aocr_tbsolicitud.numero_solicitud` | DGAC-GOP-2026-AOCR012 | DGAC-GOP-2026-AOCR012 |
| `aocr_tbsolicitud.codigo_usuario` (RT) | 45 | 45 |
| `aocr_tbinspeccion` (#11) | inspector 43, `VERIFICACION_SOLICITUD` | **igual** |
| `aocr_tbinspeccion.estado_documental` | NULL | **NULL** (pendiente INS-1b) |
| LV (`aocr_tblv_operacional_eae`) | 0 filas | **0 filas** |
| Informe (`aocr_tbinforme_inspeccion`) | 0 filas | **0 filas** |
| Documentos (`aocr_tbdocumento`) | 12 × `Aprobado` | **12 × `Aprobado** |
| Orden (`aocr_or_orden`) | id 122, `ANULADA` | **sin cambio** |

**Último historial relevante (`aocr_tbhistorial_estado`):**

| Fecha | Anterior → Nuevo | Observación |
|-------|------------------|-------------|
| 11/06 14:26 | Aceptacion Documental → En Inspeccion | Asignación inspección Nro. 11 |
| 11/06 19:19 | En Revision → Aceptacion Documental | Revision documental cerrada (COO-1) |
| 11/06 09:43 | Pendiente → En Revision | RT envió a coordinación |

---

## Resultado por paso E2E

| Paso | Rol | Resultado | Evidencia / notas |
|------|-----|-----------|-------------------|
| **E2E-01** RT | Crear solicitud, orden, comprobante | **HECHO (histórico)** | Solicitud existe; 12 docs cargados incl. `COMPROBANTE_PAGO`. Orden #122 figura `ANULADA` — anomalía de dato; no bloquea fase actual. |
| **E2E-02** Financiero | Aprobar pago | **HECHO (inferido)** | Docs habilitados y flujo avanzó a coordinación; no hay estado `PAGO_PENDIENTE` en solicitud. Validar en IIS historial financiero. |
| **E2E-03** RT | Cargar docs → Coordinación | **HECHO** | Historial 84: `Pendiente → En Revision`. 12 documentos vigentes `Aprobado`. |
| **COO-1a** Coordinación | Aceptación documental | **HECHO** | Historial 86: cierre revisión documental. Estado intermedio `Aceptacion Documental`. |
| **COO-1b** Coordinación | Asignar inspector 43 | **HECHO** | `aocr_tbinspeccion` #11, `codigo_inspector=43`, solicitud `En Inspeccion`. |
| **INS-1a** Inspector | Revisión documentos `modo=revision` | **PENDIENTE** | `estado_documental` NULL. Docs ya `Aprobado` por coordinación; inspector debe confirmar cierre (INS-1b). URL: `/Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental` |
| **INS-1b** Inspector | Confirmar cierre documental | **PENDIENTE** | Acción POST `Inspeccion/ConfirmarRevisionDocumentalInspector/11`. Esperado: `estado_documental=EN_REVISION`, LV habilitada. |
| **INS-1c** Inspector | LV → firmar .p12 | **PENDIENTE** | Sin filas en `aocr_tblv_operacional_eae`. |
| **INS-1d** Inspector | Informe Técnico → firmar | **PENDIENTE** | Sin filas en `aocr_tbinforme_inspeccion`. |
| **DIR-1** DIRDAC | Aprobar informe | **PENDIENTE** | — |
| **COO-2** Coordinación | Validar AOCR / Condiciones | **PENDIENTE** | — |
| **DIR-2** DIRDAC | Firmar AOCR + Condiciones | **PENDIENTE** | — |
| **RT-FIN** RT | Descarga final Generadas/Firmadas | **PENDIENTE** | Estado final esperado: `DOCUMENTOS_FINALES_DISPONIBLES` / `CIERRE_RT`. |

---

## Hallazgos y correcciones

### 1. Botón revisión inspector en bandeja (corregido)

**Problema:** En `Inspeccion/Index` solo existía acción rápida `modo=ver`; el inspector podía entrar en consulta sin decisiones.

**Corrección:** Botón `modo=revision` visible cuando `esBandejaInspector && faseDocumentalPendiente`.

**Archivo:** `CapaPresentacion/Views/Inspeccion/Index.cshtml`

### 2. Modo revisión en detalle (ya OK)

`Inspeccion/Detalle.cshtml` ya enlaza revisión con `modo=revision` (línea ~228). `DocumentoController.Lista` distingue `modo=revision` vs `modo=ver` y habilita `PuedeRevisarDocumentos` para inspector asignado.

### 3. Duplicación de correos inspector asignado (vigilar)

En `email_queue` (solicitud_id=12) hay **4 registros** del mismo evento `SOLICITUD_INSPECTOR_ASIGNADO` el 11/06 14:26:56–57 (2 `ENVIADO`, 2 `ERROR_CONFIG_SMTP`). `event_key` NULL en todos → deduplicación no aplicada.

**Acción recomendada:** Revisar encolado en asignación inspector (`OrdenRecaudacionDAO` / workflow coordinación) y poblar `event_key` único.

### 4. Orden recaudación ANULADA

Orden #122 (`DGAC-OR-2026-AOCR009`) en estado `ANULADA` pese a comprobante aprobado y flujo avanzado. No bloquea INS-1a pero conviene auditar en Financiero.

### 5. SMTP parcial

Algunos correos en `ERROR_CONFIG_SMTP`; otros `ENVIADO`. Validar remitente `no_reply@aviacioncivil.gob.ec` en IIS/SMTP al continuar E2E.

---

## Scripts de diagnóstico

```powershell
dotnet run --project scripts\dev\SchemaProbeNet\SchemaProbeNet.csproj -- `
  "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;" `
  scripts\gate-b-check-solicitud-12.sql
```

Archivos:

- `scripts/gate-b-check-solicitud-12.sql` — snapshot solicitud/inspección/LV/informe/orden/docs
- `scripts/gate-b-probe-orden-docs.sql` — detalle documentos y orden
- `scripts/gate-b-probe-email-cols.sql` — cola de correos

---

## Guía para continuar (desde INS-1a)

Referencia operativa: [GUIA_INSPECTOR_SOLICITUD_12.md](GUIA_INSPECTOR_SOLICITUD_12.md)

1. **Login inspector id 43** → `/Inspeccion/Index` → fila solicitud #12.
2. Clic botón revisión (clipboard-check) o ir a `/Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental`.
3. Verificar acciones de decisión visibles (no usar `modo=ver`).
4. `/Inspeccion/Detalle/11` → **Confirmar cierre documental** (INS-1b).
5. Completar LV → Finalizar → Firmar .p12 (INS-1c).
6. Informe Técnico → Finalizar → Firmar (INS-1d).
7. DIRDAC aprueba → Coordinación valida AOCR → DIRDAC firma → RT descarga en `/SolicitudAOCR/GeneradasFirmadas`.

Tras cada paso, re-ejecutar `gate-b-check-solicitud-12.sql` y actualizar este documento.

---

## Artefactos modificados en esta sesión Gate B

| Archivo | Cambio |
|---------|--------|
| `CapaPresentacion/Views/Inspeccion/Index.cshtml` | Botón acción rápida `modo=revision` para inspector |
| `scripts/gate-b-check-solicitud-12.sql` | SQL diagnóstico alineado a esquema real PostgreSQL |
| `scripts/gate-b-probe-orden-docs.sql` | Probe orden/documentos |
| `scripts/gate-b-probe-email-cols.sql` | Probe email_queue |
| `docs/GATE_B_RESULTADO_20260612.md` | Este informe |

**Republicación:** `FolderProfile4` → `C:\AOCR\publicacion1` (Release). Reciclar App Pool IIS antes de probar UI.

---

## Criterios de aceptación Gate B

| # | Criterio | Estado |
|---|----------|--------|
| 1 | Solicitud #12 RT → AOCR final | ❌ Detenida en fase inspector |
| 2 | Roles en orden correcto | ✅ Hasta COO-1b |
| 3 | Sin saltos de flujo | ✅ Historial coherente |
| 4 | Inspector solo trámites asignados | ✅ inspector 43 en #11 |
| 5 | Coordinación antes de inspector y DIRDAC | ✅ COO-1a/b completados |
| 6 | LV firmada | ❌ Pendiente |
| 7 | Informe firmado | ❌ Pendiente |
| 8 | AOCR/Condiciones si procede | ❌ Pendiente |
| 9 | DIRDAC firma finales | ❌ Pendiente |
| 10 | RT descarga finales | ❌ Pendiente |
| 11 | Correos desde no_reply@… | ⚠️ Parcial (SMTP errors en cola) |
| 12 | Historial completo | ⚠️ Parcial (hasta asignación inspector) |
| 13 | Sin errores 500 | ⚠️ No validado E2E en IIS |
| 14 | Sin errores JavaScript | ⚠️ No validado E2E en IIS |
| 15 | Gate B aprobado | **NO** |

---

## Veredicto final

**Gate B: NO APROBADO**

La solicitud #12 está correctamente posicionada en **En Inspeccion** con inspector **43** e inspección **#11**. El flujo RT → Financiero → Coordinación (E2E-01 a COO-1b) está completado según BD. Falta ejecutar manualmente en IIS la cadena **INS-1a → RT-FIN**. Se aplicó corrección UI para acceso directo a `modo=revision` desde la bandeja inspector. Tras completar los pasos pendientes y verificar descarga RT + correo final, re-evaluar Gate B.
