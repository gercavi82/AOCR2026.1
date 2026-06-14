# Capturas de pantalla — Manual AOCR (100%)

Carpeta de evidencias visuales para el flujo **RT → Financiero → Coordinación → Inspector → DIRDAC → AOCR emitida**.

**Guía de captura por fase:** [GUIA_VISUAL_FLUJO_RT_AOCR.md](../GUIA_VISUAL_FLUJO_RT_AOCR.md)  
**Checklist maestro:** [CHECKLIST_DOCUMENTACION_100.md](../CHECKLIST_DOCUMENTACION_100.md)

---

## Convención

```
docs/images/{rol}/{rol}-{pantalla}-{descripcion}.png
```

| Carpeta | Rol |
|---------|-----|
| `rt/` | Representante Técnico |
| `financiero/` | Financiero |
| `coordinacion/` | Coordinación |
| `inspector/` | Inspector |
| `dirdac/` | DIRDAC / Dirección |
| `ejemplos/` | Comparativas correcto vs incorrecto |
| `admin/` | Administrador (opcional) |

---

## Cómo capturar

1. Entorno: `C:\AOCR\publicacion1` (IIS reciclado, Ctrl+F5).
2. Caso: solicitud **#12** / inspección **#11** / inspector **43**.
3. Resolución: 1920×1080 mínimo; recortar barra de menú lateral visible.
4. Ocultar datos sensibles si el manual sale del equipo DGAC.
5. Guardar PNG con **nombre exacto** de la tabla inferior.
6. Regenerar PDFs: `cd docs/export && npm run build`.

---

## Tabla completa — 42 capturas

### Fases 1–4 (RT + Financiero)

| ☐ | Archivo | Fase | URL de referencia |
|---|---------|------|-------------------|
| ☐ | `rt/rt-formulario-emision-aocr.png` | 1 | `/SolicitudAOCR/FormularioEmisionAOCR?tipoSolicitud=1` |
| ☐ | `rt/rt-mis-tramites-solicitud-nueva.png` | 1 | `/SolicitudAOCR/MisSolicitudes` |
| ☐ | `rt/rt-orden-recaudacion-nueva.png` | 2 | `/OrdenRecaudacion/Nueva` |
| ☐ | `rt/rt-orden-comprobante-cargado.png` | 2 | `/OrdenRecaudacion/Detalles/{id}` |
| ☐ | `financiero/financiero-dashboard.png` | 3 | `/Financiero/Index` |
| ☐ | `financiero/financiero-aprobar-pago.png` | 3 | `/Financiero/TodasOrdenes` |
| ☐ | `rt/rt-documento-subir.png` | 4 | `/Documento/Subir?solicitudId=12` |
| ☐ | `rt/rt-detalle-documentos-completos.png` | 4 | `/SolicitudAOCR/Detalle/12` |
| ☐ | `rt/rt-envio-formulario-en-revision.png` | 4 | Detalle con estado **En Revision** |

### Fases 5–7 (Coordinación)

| ☐ | Archivo | Fase | URL |
|---|---------|------|-----|
| ☐ | `coordinacion/coordinacion-revision-verificacion.png` | 5 | `/CoordinacionJefatura/RevisionVerificacion` |
| ☐ | `coordinacion/coordinacion-documento-devuelto-rt.png` | 5 | Devolución documental |
| ☐ | `rt/rt-subsanar-documentos.png` | 5 | `/SolicitudAOCR/Subsanar/12` |
| ☐ | `coordinacion/coordinacion-firma-aceptacion.png` | 6 | Detalle + firma aceptación |
| ☐ | `rt/rt-detalle-pendiente-asignacion-rt.png` | 6 | Detalle estado Pendiente Asignacion RT |
| ☐ | `coordinacion/coordinacion-tecnico-index.png` | 7 | `/Tecnico/Index` |
| ☐ | `coordinacion/coordinacion-asignar-inspector.png` | 7 | `/Tecnico/AsignarInspector?solicitudId=12` |
| ☐ | `rt/rt-detalle-en-inspeccion.png` | 7 | Detalle estado **En Inspeccion** |

### Fases 8–11 (Inspector)

| ☐ | Archivo | Fase | URL |
|---|---------|------|-----|
| ☐ | `inspector/inspector-revision-documental-bandeja.png` | 8 | `/RevisionDocumental/Index` |
| ☐ | `inspector/inspector-documentos-modo-revision-pendiente.png` | 8 | `/Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental` |
| ☐ | `ejemplos/inspector-modo-ver-incorrecto.png` | 8 | `/Documento/Lista?solicitudId=12&modo=ver` |
| ☐ | `inspector/inspector-documentos-despues-aceptar.png` | 8 | Tras Aceptar un documento |
| ☐ | `inspector/inspector-detalle-cierre-documental.png` | 9 | `/Inspeccion/Detalle/11` |
| ☐ | `inspector/inspector-lv-borrador.png` | 10 | Modal LV/EAE |
| ☐ | `inspector/inspector-lv-firmada.png` | 10 | Badge *LV firmada* |
| ☐ | `inspector/inspector-informe-borrador.png` | 11 | Modal informe técnico |
| ☐ | `inspector/inspector-informe-firmado-enviado.png` | 11 | Post `FirmarInformeInspector` |

### Fases 12–16 (DIRDAC + cierre RT)

| ☐ | Archivo | Fase | URL |
|---|---------|------|-----|
| ☐ | `dirdac/dirdac-pendientes-direccion-informe.png` | 12 | `/Inspeccion/PendientesDireccion` |
| ☐ | `dirdac/dirdac-informe-aprobado.png` | 12 | Tras aprobación informe |
| ☐ | `coordinacion/coordinacion-detalle-aocr-elaboracion.png` | 13 | Detalle **AOCR En Elaboracion** |
| ☐ | `coordinacion/coordinacion-validar-aocr.png` | 14 | `/CoordinacionJefatura/ValidarAocr?solicitudId=12` |
| ☐ | `dirdac/dirdac-firma-aocr.png` | 15 | Firma certificado |
| ☐ | `dirdac/dirdac-estado-emitido-recibido.png` | 15 | Estado **AOCR Emitido/Recibido** |
| ☐ | `rt/rt-generadas-firmadas-bandeja.png` | 16 | `/SolicitudAOCR/GeneradasFirmadas` |
| ☐ | `rt/rt-descarga-pdf-aocr-final.png` | 16 | Descarga PDF AOCR |

### Modificación tipo 3 (anexo)

| ☐ | Archivo | Escenario |
|---|---------|-----------|
| ☐ | `inspector/inspector-cierre-fase-nuevo-aeropuerto.png` | Escenario C — nuevo aeropuerto |
| ☐ | `rt/rt-modificacion-orden-post-requiere-inspeccion.png` | RT orden tras `Requiere Inspeccion` |

---

## Capturas legacy (compatibilidad)

Estos nombres siguen válidos en `GUIA_VISUAL_POR_ROL.md`:

| Archivo legacy | Equivalente fase |
|----------------|------------------|
| `rt/rt-solicitud-detalle-estado.png` | Fase 4/7 genérico |
| `rt/rt-carga-documentos.png` | = `rt-documento-subir.png` |
| `financiero/financiero-bandeja-ordenes.png` | = fase 3 |
| `dirdac/dirdac-pendientes-direccion.png` | = fase 12 |

---

## Insertar imagen en Markdown

```markdown
![Fase 8 — revisión documental](../images/inspector/inspector-documentos-modo-revision-pendiente.png)
```

Tras añadir PNGs, ejecute `npm run build` en `docs/export/`.
