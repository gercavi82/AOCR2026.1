# Checklist documentación y validación al 100%

**Versión:** 2026-06-11  
**Objetivo:** marcar ✅ cuando existan **documento**, **captura PNG**, **prueba manual** y **export PDF/DOCX** para el flujo emisión solicitud #12 (RT → AOCR).

**Regla global:** documentación 100% = este checklist + niveles A–E en `AOCR_FLUJO_INTEGRAL_MATRICES.md` §12.

---

## A. Artefactos documentales

| ☐ | Artefacto | Contenido | Export |
|---|-----------|-----------|--------|
| ☐ | A1 | [MANUAL_FLUJO_RT_A_AOCR.md](MANUAL_FLUJO_RT_A_AOCR.md) — 16 fases narrativas | `export/MANUAL_FLUJO_RT_A_AOCR.pdf` |
| ☐ | A2 | [GUIA_VISUAL_FLUJO_RT_AOCR.md](GUIA_VISUAL_FLUJO_RT_AOCR.md) — textos UI + 42 PNG | `export/GUIA_VISUAL_FLUJO_RT_AOCR.pdf` |
| ☐ | A3 | [MANUAL_USUARIO_AOCR.md](MANUAL_USUARIO_AOCR.md) | `export/MANUAL_USUARIO_AOCR.pdf` |
| ☐ | A4 | [MANUAL_TECNICO_AOCR.md](MANUAL_TECNICO_AOCR.md) §16–§18 | `export/MANUAL_TECNICO_AOCR.pdf` |
| ☐ | A5 | [GUIA_INSPECTOR_SOLICITUD_12.md](GUIA_INSPECTOR_SOLICITUD_12.md) | `export/GUIA_INSPECTOR_SOLICITUD_12.pdf` |
| ☐ | A6 | [GUIA_VISUAL_POR_ROL.md](GUIA_VISUAL_POR_ROL.md) | `export/GUIA_VISUAL_POR_ROL.pdf` |
| ☐ | A7 | Regenerar todos: `cd docs/export && npm run build` | Carpeta `export/editable/` |

---

## B. Capturas PNG por fase (42)

Convención: `docs/images/{rol}/{archivo}.png` — lista completa en [images/README.md](images/README.md).

| ☐ | Fase | Archivo | Rol |
|---|------|---------|-----|
| ☐ | 1 | `rt/rt-formulario-emision-aocr.png` | RT |
| ☐ | 1 | `rt/rt-mis-tramites-solicitud-nueva.png` | RT |
| ☐ | 2 | `rt/rt-orden-recaudacion-nueva.png` | RT |
| ☐ | 2 | `rt/rt-orden-comprobante-cargado.png` | RT |
| ☐ | 3 | `financiero/financiero-dashboard.png` | Financiero |
| ☐ | 3 | `financiero/financiero-aprobar-pago.png` | Financiero |
| ☐ | 4 | `rt/rt-documento-subir.png` | RT |
| ☐ | 4 | `rt/rt-detalle-documentos-completos.png` | RT |
| ☐ | 4 | `rt/rt-envio-formulario-en-revision.png` | RT |
| ☐ | 5 | `coordinacion/coordinacion-revision-verificacion.png` | Coordinación |
| ☐ | 5 | `coordinacion/coordinacion-documento-devuelto-rt.png` | Coordinación |
| ☐ | 5 | `rt/rt-subsanar-documentos.png` | RT |
| ☐ | 6 | `coordinacion/coordinacion-firma-aceptacion.png` | Coordinación |
| ☐ | 6 | `rt/rt-detalle-pendiente-asignacion-rt.png` | RT |
| ☐ | 7 | `coordinacion/coordinacion-tecnico-index.png` | Coordinación |
| ☐ | 7 | `coordinacion/coordinacion-asignar-inspector.png` | Coordinación |
| ☐ | 7 | `rt/rt-detalle-en-inspeccion.png` | RT |
| ☐ | 8 | `inspector/inspector-revision-documental-bandeja.png` | Inspector |
| ☐ | 8 | `inspector/inspector-documentos-modo-revision-pendiente.png` | Inspector |
| ☐ | 8 | `ejemplos/inspector-modo-ver-incorrecto.png` | QA |
| ☐ | 8 | `inspector/inspector-documentos-despues-aceptar.png` | Inspector |
| ☐ | 9 | `inspector/inspector-detalle-cierre-documental.png` | Inspector |
| ☐ | 10 | `inspector/inspector-lv-borrador.png` | Inspector |
| ☐ | 10 | `inspector/inspector-lv-firmada.png` | Inspector |
| ☐ | 11 | `inspector/inspector-informe-borrador.png` | Inspector |
| ☐ | 11 | `inspector/inspector-informe-firmado-enviado.png` | Inspector |
| ☐ | 12 | `dirdac/dirdac-pendientes-direccion-informe.png` | DIRDAC |
| ☐ | 12 | `dirdac/dirdac-informe-aprobado.png` | DIRDAC |
| ☐ | 13 | `coordinacion/coordinacion-detalle-aocr-elaboracion.png` | Coordinación |
| ☐ | 14 | `coordinacion/coordinacion-validar-aocr.png` | Coordinación |
| ☐ | 15 | `dirdac/dirdac-firma-aocr.png` | DIRDAC |
| ☐ | 15 | `dirdac/dirdac-estado-emitido-recibido.png` | DIRDAC |
| ☐ | 16 | `rt/rt-generadas-firmadas-bandeja.png` | RT |
| ☐ | 16 | `rt/rt-descarga-pdf-aocr-final.png` | RT |
| ☐ | A | `rt/rt-modificacion-orden-post-requiere-inspeccion.png` | RT mod. |
| ☐ | A | `inspector/inspector-cierre-fase-nuevo-aeropuerto.png` | Inspector mod. |

**Done capturas:** 34/34 obligatorias emisión + 2 modificación = **36 mínimo** (42 con variantes listadas).

---

## C. Prueba manual end-to-end (solicitud #12)

Ejecutar en orden con usuarios reales. Evidencia: log + captura + estado BD.

| ☐ | ID | Fase | Rol | Criterio | Evidencia |
|---|-----|------|-----|----------|-----------|
| ☐ | E2E-01 | 1–2 | RT | Solicitud + orden creadas | Detalle #12 |
| ☐ | E2E-02 | 3 | Financiero | Pago aprobado | Log financiero |
| ☐ | E2E-03 | 4 | RT | Estado `En Revision` | Historial envío |
| ☐ | E2E-04 | 6 | Coordinación | `[FirmarAceptacionDocumental]` | `Pendiente Asignacion RT` |
| ☐ | E2E-05 | 7 | Coordinación | `[GestionInspeccion]` | `En Inspeccion`, insp. #11 |
| ☐ | E2E-06 | 8 | Inspector | 12 docs PENDIENTE `modo=revision` | PNG fase 8 |
| ☐ | E2E-07 | 9 | Inspector | Cierre documental confirmado | comentario inspección |
| ☐ | E2E-08 | 10 | Inspector | LV firmada | `aocr_tblv_operacional_eae` |
| ☐ | E2E-09 | 11 | Inspector | Informe `ENVIADO_A_DIRDAC` | `aocr_tbinforme_inspeccion` |
| ☐ | E2E-10 | 12 | DIRDAC | → `AOCR En Elaboracion` | PendientesDireccion |
| ☐ | E2E-11 | 14 | Coordinación | ValidarAocr → `AOCR En Revision` | PNG fase 14 |
| ☐ | E2E-12 | 15 | DIRDAC | → `AOCR Emitido/Recibido` | PNG fase 15 |
| ☐ | E2E-13 | 16 | RT | PDF descargado GeneradasFirmadas | PNG fase 16 |

**Done E2E:** E2E-01 → E2E-13 ✅

---

## D. Validaciones técnicas críticas (código)

| ☐ | ID | Criterio | Referencia |
|---|-----|----------|------------|
| ☐ | T1 | Sin revisión inspector → PENDIENTE | `MANUAL_TECNICO_AOCR.md` T1 |
| ☐ | T2 | Revisiones COO-1 excluidas post-asignación | T2 |
| ☐ | T3 | `modo=revision` vs `modo=ver` | T4 |
| ☐ | T4 | LV bloqueada hasta cierre documental | T5 |
| ☐ | T5 | Informe exige LV firmada | §17 |
| ☐ | T6 | Descarga constancia ≠ Finalizado | `MANUAL_USUARIO_AOCR.md` §4 |
| ☐ | T7 | Tests unitarios pasan | `vstest.console AOCR.Tests.dll` |

---

## E. Exportaciones editables

| ☐ | Entregable | Ruta |
|---|------------|------|
| ☐ | PDF flujo RT→AOCR | `docs/export/MANUAL_FLUJO_RT_A_AOCR.pdf` |
| ☐ | PDF guía visual flujo | `docs/export/GUIA_VISUAL_FLUJO_RT_AOCR.pdf` |
| ☐ | Word editable flujo | `docs/export/editable/MANUAL_FLUJO_RT_A_AOCR.docx` |
| ☐ | Word guía visual | `docs/export/editable/GUIA_VISUAL_FLUJO_RT_AOCR.docx` |
| ☐ | Markdown copia | `docs/export/editable/*.md` |

Comando: `cd docs\export` → `npm run build`

---

## F. Resumen ejecutivo — % completitud

| Bloque | Items | Cómo medir |
|--------|-------|------------|
| **Documentos A** | 7 | Archivos `.md` + PDF regenerados |
| **Capturas B** | 36–42 | PNG en `docs/images/` |
| **E2E C** | 13 | Prueba manual #12 |
| **Técnico D** | 7 | Tests + checklist técnico |
| **Export E** | 5 | Carpeta `export/` |

**100% documentación + flujo emisión** = A7 ✅ + B (36 PNG) ✅ + C (E2E-13) ✅ + D ✅ + E ✅

**100% plataforma global** = lo anterior + niveles C–E en `AOCR_FLUJO_INTEGRAL_MATRICES.md` §12 (modificación, NC, deuda técnica E1–E10).

---

## G. Orden de trabajo recomendado

```text
1. npm run build (docs/export) — artefactos A + E
2. Recorrer fases 1→16 en publicacion1 — capturas B + prueba C
3. Ejecutar AOCR.Tests — bloque D
4. Marcar AOCR_FLUJO_INTEGRAL §12 niveles B–E
5. Entregar paquete: export/*.pdf + export/editable/*.docx + images/
```

---

*Última actualización: 2026-06-11*
