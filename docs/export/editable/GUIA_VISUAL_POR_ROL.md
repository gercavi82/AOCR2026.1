# Guía visual por rol — AOCR (validación contra código)

**Versión:** 2026-06-11  
**Caso de prueba:** solicitud **#12** (`DGAC-GOP-2026-AOCR012`), inspección **#11**, inspector **id 43**

**Flujo completo 16 fases (RT→AOCR):** [GUIA_VISUAL_FLUJO_RT_AOCR.md](GUIA_VISUAL_FLUJO_RT_AOCR.md) · **Checklist 100%:** [CHECKLIST_DOCUMENTACION_100.md](CHECKLIST_DOCUMENTACION_100.md)

Cada fila indica el **texto exacto** que debe verse en pantalla según `Views/*.cshtml` y el **endpoint** según `*Controller.cs`.

---

## Leyenda

| Marca | Significado |
|-------|-------------|
| ✅ | Debe aparecer (implementación correcta) |
| ❌ | No debe aparecer (defecto) |
| 📷 | Archivo PNG en `docs/images/{rol}/` |

---

## 0. RT — fases 1, 2 y 4 (inicio del trámite)

Ver detalle captura por captura en [GUIA_VISUAL_FLUJO_RT_AOCR.md](GUIA_VISUAL_FLUJO_RT_AOCR.md).

### 0.1 Formulario emisión

**URL:** `/SolicitudAOCR/FormularioEmisionAOCR?tipoSolicitud=1`  
📷 `rt/rt-formulario-emision-aocr.png`

### 0.2 Orden de recaudación

**URL:** `/OrdenRecaudacion/Nueva` · `/OrdenRecaudacion/Detalles/{id}`  
📷 `rt/rt-orden-recaudacion-nueva.png` · `rt/rt-orden-comprobante-cargado.png`

| Texto UI | Validación |
|----------|------------|
| *Orden: {NumeroOrden}* | ✅ |
| *Debe registrar el comprobante antes de continuar.* | ✅ Si falta pago |
| **Generar Orden** | ✅ Borrador con total > 0 |

### 0.3 Carga documental y envío

**URL:** `/Documento/Subir?solicitudId=12` · `/SolicitudAOCR/Detalle/12`  
📷 `rt/rt-documento-subir.png` · `rt/rt-detalle-documentos-completos.png` · `rt/rt-envio-formulario-en-revision.png`

| Tras envío formal | Estado **`En Revision`** |

### 0.4 Seguimiento RT (fases 6–7, 12–16)

📷 `rt/rt-detalle-pendiente-asignacion-rt.png` · `rt/rt-detalle-en-inspeccion.png`  
📷 `rt/rt-generadas-firmadas-bandeja.png` · `rt/rt-descarga-pdf-aocr-final.png`

| Pantalla | Texto UI |
|----------|----------|
| GeneradasFirmadas | *AOCR Generadas y Firmadas* |
| Resumen | *Firmadas / finalizadas* |

---

## 1. Coordinación — `GEN_COORDINACION`

### 1.0 Revisión documental COO-1 (pre-asignación)

**URL:** `/CoordinacionJefatura/RevisionVerificacion`  
📷 `coordinacion/coordinacion-revision-verificacion.png`

| Validación | Detalle |
|------------|---------|
| Solicitud #12 en cola | ✅ |
| Devolución a RT | → `/SolicitudAOCR/Subsanar/12` 📷 `rt/rt-subsanar-documentos.png` |

### 1.1 Bandeja asignación

**URL:** `/Tecnico/Index`  
**Vista:** `Views/Tecnico/Index.cshtml`  
**Datos:** `CoordinacionBandejaService.ObtenerPendientesAsignacion()`  
📷 `coordinacion/coordinacion-tecnico-index.png`

| Elemento | Validación |
|----------|------------|
| Fila solicitud `DGAC-GOP-2026-AOCR012` o id 12 | ✅ |
| Estado `Pendiente Asignacion RT` (pre-asignación) | ✅ |
| Enlace asignar → `/Tecnico/AsignarInspector?solicitudId=12` | ✅ |
| Texto con fondo azul `#0d6efd` fijo en tabla | ❌ (fix CSS `aocr-contrast.css`) |

### 1.2 Formulario asignación

**URL:** `/Tecnico/AsignarInspector?solicitudId=12&tipoInspector=OPS`  
**POST:** `Tecnico/AsignarInspector`  
📷 `coordinacion/coordinacion-asignar-inspector.png`

| Campo POST | Ejemplo |
|------------|---------|
| `inspectorPrincipal` | Cédula/login inspector del catálogo RT |
| `tipoInspector` | `OPS` \| `AIR` \| `TODOS` |
| `fechaInspeccion` / `horaInspeccion` | Obligatorios |

**Tras éxito:** estado solicitud = **`En Inspeccion`** · log `[GestionInspeccion]`

### 1.3 Firma aceptación (pre-asignación)

**POST:** `/SolicitudAOCR/FirmarAceptacionDocumental/12`  
📷 `coordinacion/coordinacion-firma-aceptacion.png`

| Tipo solicitud #12 | Estado destino |
|--------------------|----------------|
| Si tipo = 1 o 2 | `Pendiente Asignacion RT` |
| Si tipo = 3 | `Firmado Coordinador` |

---

## 2. Inspector — usuario id 43

### 2.1 Bandeja revisión documental

**URL:** `/RevisionDocumental/Index`  
**Vista:** `Views/RevisionDocumental/Index.cshtml`  
**Autorización:** `[AocrAuthorize(Roles = "Inspector,Administrador")]`  
📷 `inspector/inspector-revision-documental-bandeja.png`

| Elemento | Validación |
|----------|------------|
| Título sección revisión documental | ✅ |
| Fila codigoSolicitud **12** | ✅ |
| Botón **Revisar documentación** (outline azul) | ✅ Si hay docs pendientes |
| Botón **Confirmar fase documental** (amarillo) | ✅ Si `DocumentacionAprobada` y falta confirmación |
| Botón **Continuar en LV/EAE** (azul) | ✅ Tras cierre confirmado |
| Mensaje vacío: *"No hay solicitudes pendientes..."* | ❌ Si #12 está asignada con docs pendientes |

**Log esperado:** `[DOC_FLOW] Accion=BANDEJA_INSPECTOR; ... TotalSolicitudes=...`

### 2.2 Documentos — modo revisión (CORRECTO)

**URL exacta:**
```
/Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental
```

📷 `inspector/inspector-documentos-modo-revision-pendiente.png`

| Texto UI (`Lista.cshtml`) | Valor esperado |
|---------------------------|----------------|
| `ViewBag.Title` contiene | *Revisión documental - Solicitud AOCR DGAC-GOP-2026-AOCR012* |
| `tablaTitulo` | **Bandeja de revisión documental** |
| `tablaBadge` | **Revisión activa** |
| `estadoTarjetaValor` | **Aceptar y devolver documentos** |
| Columna ESTADO (cada fila) | Badge gris **PENDIENTE** |
| OBSERVACION / MOTIVO | **Vacío** |
| REVISADO POR | **Vacío** |
| ACCIONES | **Aceptar** + **Devolver** + Vista previa + Descargar |

**AJAX al aceptar:** `POST /Documento/AceptarDocumentoSolicitud`  
**AJAX al devolver:** `POST /Documento/DevolverDocumentoSolicitud` (motivo ≥ 10 chars)

### 2.3 Documentos — modo ver (NO decidir aquí)

**URL:** `/Documento/Lista?solicitudId=12&modo=ver`  
(enlace desde `Inspeccion/Detalle` — `modo = "ver"`)

📷 `ejemplos/inspector-modo-ver-incorrecto.png`

| Texto UI | Valor esperado |
|----------|----------------|
| `tablaTitulo` | **Archivos del expediente** |
| `tablaBadge` / chip | **Solo lectura** |
| Botones Aceptar/Devolver | ❌ Ausentes (correcto para modo ver) |
| Docs ACEPTADO + revisor GERMAN/id 45 **sin** acción inspector | ❌ **Defecto** si ocurre en `modo=revision` |

### 2.4 Tras aceptar (ejemplo un documento)

📷 `inspector/inspector-documentos-despues-aceptar.png`

| Campo | Valor |
|-------|-------|
| ESTADO | **ACEPTADO** (badge verde) |
| REVISADO POR | Nombre inspector 43 (no id 45) |
| Registro BD | `aocr_tbrevision_documental.codigo_usuario_revisor = 43` |

### 2.5 Cierre documental + LV

**URL:** `/Inspeccion/Detalle/11`  
📷 `inspector/inspector-detalle-cierre-documental.png`

| Elemento | Validación |
|----------|------------|
| Botón **Confirmar cierre documental** | ✅ Visible si docs aceptados |
| POST | `Inspeccion/ConfirmarRevisionDocumentalInspector/11` |
| Tras confirmar — botón LV | **Completar LV en ventana** (no candado) |
| Mensaje bloqueo previo | *"...confirme el cierre documental antes de habilitar la LV/EAE..."* |
| Badge post-firma LV | *LV firmada* |

### 2.6 Informe técnico

**URL:** `/Inspeccion/Detalle/11` → modal informe  
📷 `inspector/inspector-informe-borrador.png` · `inspector/inspector-informe-firmado-enviado.png`

| Paso | POST |
|------|------|
| Guardar | `Inspeccion/GuardarInformeTecnico` |
| Finalizar | `Inspeccion/FinalizarInformeTecnico/{id}` |
| Firmar | `Inspeccion/FirmarInformeInspector` → `ENVIADO_A_DIRDAC` |

| Bloqueo | *Debe finalizar y firmar la LV antes de gestionar el Informe Técnico.* |

---

## 3. RT — propietario solicitud #12

**URL:** `/SolicitudAOCR/Detalle/12`  
📷 `rt/rt-solicitud-detalle-estado.png`

| Elemento | Validación |
|----------|------------|
| `NumeroSolicitud` | `DGAC-GOP-2026-AOCR012` |
| Estado | Coherente con fase |
| Tarjetas documento | Tipos: Comprobante pago, Certificado aeronavegabilidad, Manual operaciones, OpSpecs, etc. |
| Enlace documentos modo ver | `/Documento/Lista?solicitudId=12&modo=ver` |

---

## 4. Financiero

**URL:** `/Financiero/Index` · `/Financiero/TodasOrdenes`  
📷 `financiero/financiero-dashboard.png` · `financiero/financiero-bandeja-ordenes.png` · `financiero/financiero-aprobar-pago.png`

| Texto UI | Validación |
|----------|------------|
| *Dashboard Financiero* | ✅ |
| *Todas las Órdenes de Recaudación* | ✅ |
| *En revision financiera* (contador dashboard) | ✅ Coherente con bandeja |
| Botón **Aprobar** | ✅ `Financiero/AprobarPago` o `AprobarOrden` |

---

## 5. DIRDAC

**URL informes:** `/Inspeccion/PendientesDireccion`  
📷 `dirdac/dirdac-pendientes-direccion-informe.png` · `dirdac/dirdac-informe-aprobado.png`

| Elemento | Validación |
|----------|------------|
| Informe #12 tras `FirmarInformeInspector` | ✅ En bandeja |
| Tras aprobación | Solicitud → **`AOCR En Elaboracion`** |

**URL AOCR formal:** `/CoordinacionJefatura/ValidarAocr?solicitudId=12`  
📷 `coordinacion/coordinacion-validar-aocr.png`

**Firma y emisión:**  
📷 `dirdac/dirdac-firma-aocr.png` · `dirdac/dirdac-estado-emitido-recibido.png`

| Cadena estados | `AOCR En Revision` → `Enviado DCAV` → `Firmado DCAV` → **`AOCR Emitido/Recibido`** |

---

## 6. Matriz comparativa revisión documental

| | Pre-asignación COO-1 | Post-asignación inspector #43 |
|--|----------------------|-------------------------------|
| **Detector código** | `EsRevisionDocumentalPreAsignacion=true` | `RequiereDecisionDocumentalInspector=true` |
| **Pantalla acción** | `CoordinacionJefatura/RevisionVerificacion` | `Documento/Lista?modo=revision` |
| **Tabla revisiones** | `aocr_tbrevision_documental` (todas) | Solo `codigo_usuario_revisor=43` |
| **Estado UI sin decisión** | Puede mostrar coord. | **PENDIENTE** obligatorio |
| **Observación COO-1 visible** | ✅ Permitido | ❌ En modo revision |

---

## 7. Exportar PDF con capturas

1. Guarde PNG según [images/README.md](images/README.md) (42 archivos).
2. Opcional: inserte `![desc](../images/rt/archivo.png)` en Markdown fuente.
3. Regenerar: `cd docs/export && npm run build`.
4. Entregables: `export/*.pdf` + `export/editable/*.docx`.

**Checklist 100%:** [CHECKLIST_DOCUMENTACION_100.md](CHECKLIST_DOCUMENTACION_100.md)

---

*Referencia código: `DocumentoController.AplicarContextoRevisionDocumental`, `SolicitudAocrInfraBL.FiltrarDetallesRevisionPorInspector`, `Views/Documento/Lista.cshtml` líneas 41–71.*
