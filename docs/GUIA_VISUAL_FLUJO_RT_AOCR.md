# Guía visual — Flujo completo RT → AOCR (16 fases)

**Versión:** 2026-06-11  
**Trámite de referencia:** solicitud **#12** (`DGAC-GOP-2026-AOCR012`), inspección **#11**, inspector **id 43**  
**Entorno capturas:** `C:\AOCR\publicacion1` · navegador incógnito · **Ctrl+F5** antes de cada PNG

**Manuales vinculados:** `MANUAL_FLUJO_RT_A_AOCR.md` (narrativa) · `GUIA_VISUAL_POR_ROL.md` (matrices por rol) · `GUIA_INSPECTOR_SOLICITUD_12.md` (detalle inspector)

---

## Leyenda

| Símbolo | Significado |
|---------|-------------|
| 📷 | Archivo PNG obligatorio en `docs/images/{rol}/` |
| ✅ | Debe verse en implementación correcta |
| ❌ | Defecto si aparece (o ausencia incorrecta) |
| 👁 | RT solo observa; no actúa |

---

## Índice de capturas (42 archivos)

| Fase | Archivo PNG | Rol |
|------|-------------|-----|
| 1 | `rt/rt-formulario-emision-aocr.png` | RT |
| 1 | `rt/rt-mis-tramites-solicitud-nueva.png` | RT |
| 2 | `rt/rt-orden-recaudacion-nueva.png` | RT |
| 2 | `rt/rt-orden-comprobante-cargado.png` | RT |
| 3 | `financiero/financiero-dashboard.png` | Financiero |
| 3 | `financiero/financiero-aprobar-pago.png` | Financiero |
| 4 | `rt/rt-documento-subir.png` | RT |
| 4 | `rt/rt-detalle-documentos-completos.png` | RT |
| 4 | `rt/rt-envio-formulario-en-revision.png` | RT |
| 5 | `coordinacion/coordinacion-revision-verificacion.png` | Coordinación |
| 5 | `coordinacion/coordinacion-documento-devuelto-rt.png` | Coordinación |
| 5 | `rt/rt-subsanar-documentos.png` | RT (rama) |
| 6 | `coordinacion/coordinacion-firma-aceptacion.png` | Coordinación |
| 6 | `rt/rt-detalle-pendiente-asignacion-rt.png` | RT 👁 |
| 7 | `coordinacion/coordinacion-tecnico-index.png` | Coordinación |
| 7 | `coordinacion/coordinacion-asignar-inspector.png` | Coordinación |
| 7 | `rt/rt-detalle-en-inspeccion.png` | RT 👁 |
| 8 | `inspector/inspector-revision-documental-bandeja.png` | Inspector |
| 8 | `inspector/inspector-documentos-modo-revision-pendiente.png` | Inspector |
| 8 | `ejemplos/inspector-modo-ver-incorrecto.png` | Comparativa ❌ |
| 8 | `inspector/inspector-documentos-despues-aceptar.png` | Inspector |
| 9 | `inspector/inspector-detalle-cierre-documental.png` | Inspector |
| 10 | `inspector/inspector-lv-borrador.png` | Inspector |
| 10 | `inspector/inspector-lv-firmada.png` | Inspector |
| 11 | `inspector/inspector-informe-borrador.png` | Inspector |
| 11 | `inspector/inspector-informe-firmado-enviado.png` | Inspector |
| 12 | `dirdac/dirdac-pendientes-direccion-informe.png` | DIRDAC |
| 12 | `dirdac/dirdac-informe-aprobado.png` | DIRDAC |
| 13 | `coordinacion/coordinacion-detalle-aocr-elaboracion.png` | Coord. 👁 |
| 14 | `coordinacion/coordinacion-validar-aocr.png` | Coordinación |
| 15 | `dirdac/dirdac-firma-aocr.png` | DIRDAC |
| 15 | `dirdac/dirdac-estado-emitido-recibido.png` | DIRDAC |
| 16 | `rt/rt-generadas-firmadas-bandeja.png` | RT |
| 16 | `rt/rt-descarga-pdf-aocr-final.png` | RT |
| A | `rt/rt-modificacion-orden-post-requiere-inspeccion.png` | RT mod. |
| A | `inspector/inspector-cierre-fase-nuevo-aeropuerto.png` | Inspector mod. |

---

## Fase 1 — RT: crear solicitud (emisión)

| Campo | Valor |
|-------|-------|
| **Usuario** | RT / Solicitante |
| **Menú** | **Solicitud AOCR** |
| **URL** | `/SolicitudAOCR/FormularioEmisionAOCR?tipoSolicitud=1` |
| **Vista** | `Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml` |
| **Estado inicial** | Borrador / pendiente pago (según orden) |

📷 `rt/rt-formulario-emision-aocr.png` · 📷 `rt/rt-mis-tramites-solicitud-nueva.png`

| Texto UI | Validación |
|----------|------------|
| Título formulario emisión | ✅ Visible |
| Campos operadora, aeronaves, representante | ✅ Obligatorios según tipo |
| Tras guardar → **Mis trámites** | ✅ Redirección o mensaje éxito |
| `NumeroSolicitud` generado | ✅ Formato `DGAC-GOP-YYYY-AOCR###` |

**Handoff:** menú **Órdenes (RT)** habilitado.

---

## Fase 2 — RT: orden de recaudación

| Campo | Valor |
|-------|-------|
| **URL nueva orden** | `/OrdenRecaudacion/Nueva` |
| **URL detalle** | `/OrdenRecaudacion/Detalles/{idOrden}` |
| **Vista** | `Views/OrdenRecaudacion/Nueva.cshtml`, `Detalles.cshtml` |

📷 `rt/rt-orden-recaudacion-nueva.png` · 📷 `rt/rt-orden-comprobante-cargado.png`

| Texto UI (`Detalles.cshtml`) | Validación |
|------------------------------|------------|
| Encabezado *Orden: {NumeroOrden}* | ✅ |
| Botón **Generar Orden** | ✅ Si borrador con total > 0 |
| *Debe registrar el comprobante antes de continuar.* | ✅ Si falta comprobante |
| Subida comprobante | ✅ Tipo `COMPROBANTE_PAGO` |

| Badge menú RT | Condición |
|---------------|-----------|
| Warning en **Subir comprobante** | Orden pendiente comprobante |

---

## Fase 3 — Financiero: aprobar pago

| Campo | Valor |
|-------|-------|
| **Usuario** | Financiero |
| **Menú** | **Dashboard financiero** / **Órdenes de recaudación** |
| **URL bandeja** | `/Financiero/TodasOrdenes` o `/Financiero/Index?estado=TODAS` |
| **Vista** | `Views/Financiero/TodasOrdenes.cshtml`, `Index.cshtml` |

📷 `financiero/financiero-dashboard.png` · 📷 `financiero/financiero-aprobar-pago.png`

| Texto UI | Validación |
|----------|------------|
| *Dashboard Financiero* | ✅ `ViewBag.Title` |
| *Todas las Órdenes de Recaudación* | ✅ |
| Botón **Aprobar** / **Aprobar pago** | ✅ Por fila pendiente |
| POST | `Financiero/AprobarPago/{id}` o `AprobarOrden` |
| Modal *Devolver Orden* | ✅ Si rechazo |

| RT 👁 | Tras aprobación |
|-------|-----------------|
| Estado solicitud | Hacia **Documentacion Pendiente** |
| Menú **Documentos y expediente** | ✅ Activo |

| Contador | Regla |
|----------|-------|
| Badge sidebar financiero | = filas bandeja (`AocrSidebarCounterService`) |

---

## Fase 4 — RT: cargar documentos y enviar

| Campo | Valor |
|-------|-------|
| **URL subir** | `/Documento/Subir?solicitudId=12` |
| **URL expediente** | `/Documento/Lista?solicitudId=12&modo=ver` |
| **URL detalle** | `/SolicitudAOCR/Detalle/12` |
| **POST envío** | `SolicitudAOCR/FormularioCompleto` |

📷 `rt/rt-documento-subir.png` · 📷 `rt/rt-detalle-documentos-completos.png` · 📷 `rt/rt-envio-formulario-en-revision.png`

| Texto UI | Validación |
|----------|------------|
| Panel docs obligatorios faltantes | ✅ Vacío antes de enviar |
| Tipos: Certificado aeronavegabilidad, Manual operaciones, OPSPECS, AOC, etc. | ✅ Tarjetas en detalle |
| Tras envío — estado | **`En Revision`** |
| Historial | *Solicitud formal enviada al coordinador para revisión documental.* |

| Modo RT en Lista | Valor |
|------------------|-------|
| `modo=ver` | **Archivos del expediente** / **Solo lectura** — correcto para RT |

---

## Fase 5 — Coordinación: revisión COO-1

| Campo | Valor |
|-------|-------|
| **Usuario** | `GEN_COORDINACION` |
| **Menú** | **Bandeja integral** / **Pendientes de revisión** |
| **URL** | `/CoordinacionJefatura/RevisionVerificacion` |
| **Vista** | `Views/CoordinacionJefatura/RevisionVerificacion.cshtml` |

📷 `coordinacion/coordinacion-revision-verificacion.png`

| Validación | Detalle |
|------------|---------|
| Solicitud #12 en cola | ✅ |
| Aceptar/devolver documentos | ✅ Pre-asignación |
| Revisiones cuentan todas en BD | ✅ `ObtenerUltimasRevisionesPorSolicitud` |

### Rama — devolución al RT

📷 `coordinacion/coordinacion-documento-devuelto-rt.png` · 📷 `rt/rt-subsanar-documentos.png`

| RT | URL `/SolicitudAOCR/Subsanar/12` |
|----|----------------------------------|
| Estado | **Observada** → **Subsanada** tras reenvío |
| POST | `SubsanarPost` |

---

## Fase 6 — Coordinación: firma aceptación documental

| Campo | Valor |
|-------|-------|
| **POST** | `/SolicitudAOCR/FirmarAceptacionDocumental/12` |
| **Log** | `[FirmarAceptacionDocumental]` |

📷 `coordinacion/coordinacion-firma-aceptacion.png` · 📷 `rt/rt-detalle-pendiente-asignacion-rt.png`

| Tipo 1/2 — estado destino | **`Pendiente Asignacion RT`** |
| RT 👁 en Detalle | Constancia descargable |
| ❌ Defecto | Estado → `Finalizado` solo por descargar PDF |

---

## Fase 7 — Coordinación: asignar inspector

| Campo | Valor |
|-------|-------|
| **URL bandeja** | `/Tecnico/Index` |
| **URL formulario** | `/Tecnico/AsignarInspector?solicitudId=12&tipoInspector=OPS` |
| **POST** | `Tecnico/AsignarInspector` |
| **Vista** | `Views/Tecnico/Index.cshtml`, `AsignarInspector.cshtml` |

📷 `coordinacion/coordinacion-tecnico-index.png` · 📷 `coordinacion/coordinacion-asignar-inspector.png` · 📷 `rt/rt-detalle-en-inspeccion.png`

| Texto UI | Validación |
|----------|------------|
| Fila #12 estado `Pendiente Asignacion RT` | ✅ En bandeja |
| Tras POST — estado | **`En Inspeccion`** |
| Log | `[GestionInspeccion]` |
| RT 👁 Detalle | Inspector asignado visible |
| ❌ Texto fantasma azul fijo en tabla | Corregido en CSS |

---

## Fase 8 — Inspector: revisión documental post-asignación

| Campo | Valor |
|-------|-------|
| **URL bandeja** | `/RevisionDocumental/Index` |
| **URL acción (correcta)** | `/Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental` |
| **URL consulta (no decidir)** | `/Documento/Lista?solicitudId=12&modo=ver` |

📷 `inspector/inspector-revision-documental-bandeja.png` · 📷 `inspector/inspector-documentos-modo-revision-pendiente.png` · 📷 `ejemplos/inspector-modo-ver-incorrecto.png` · 📷 `inspector/inspector-documentos-despues-aceptar.png`

| Texto UI modo **revision** | Valor |
|----------------------------|-------|
| `tablaTitulo` | **Bandeja de revisión documental** |
| `tablaBadge` | **Revisión activa** |
| ESTADO inicial | **PENDIENTE** (×12 docs aprox.) |
| OBSERVACIÓN / REVISADO POR | **Vacío** |
| ACCIONES | **Aceptar** + **Devolver** |

| Texto UI modo **ver** | Valor |
|-----------------------|-------|
| `tablaTitulo` | **Archivos del expediente** |
| Badge | **Solo lectura** |
| Botones Aceptar/Devolver | ❌ Ausentes (OK en ver) |

| ❌ Defecto crítico | ACEPTADO + revisor id 45 en `modo=revision` sin acción inspector |

---

## Fase 9 — Inspector: cierre documental

| Campo | Valor |
|-------|-------|
| **URL** | `/Inspeccion/Detalle/11` |
| **Botón** | **Confirmar cierre documental** |
| **POST** | `/Inspeccion/ConfirmarRevisionDocumentalInspector/11` |

📷 `inspector/inspector-detalle-cierre-documental.png`

| Antes | Mensaje bloqueo LV |
|-------|-------------------|
| Sin cierre | *"...confirme el cierre documental antes de habilitar la LV/EAE..."* |

| Después | Botón **Completar LV en ventana** sin candado |

---

## Fase 10 — Inspector: LV/EAE

| Campo | Valor |
|-------|-------|
| **Modal** | `#modalListaVerificacionOperacionalEae` en `Detalle.cshtml` |
| **POST guardar** | `Inspeccion/GuardarListaVerificacionOperacionalEae` |
| **POST finalizar** | `Inspeccion/FinalizarListaVerificacionOperacionalEae/11` |
| **POST firmar** | `Inspeccion/FirmarListaVerificacionOperacionalEae` |

📷 `inspector/inspector-lv-borrador.png` · 📷 `inspector/inspector-lv-firmada.png`

| Badge UI | Estado |
|----------|--------|
| *LV pendiente* | Sin borrador |
| *LV en borrador* | Guardado |
| *LV completada* | Finalizada sin firma |
| *LV firmada* | ✅ Handoff informe |
| *BLOQUEADO POR LV/EAE* | ❌ Si informe antes de LV |

---

## Fase 11 — Inspector: informe técnico

| Campo | Valor |
|-------|-------|
| **Modal** | `GET Inspeccion/ModalInformeTecnico` |
| **POST guardar** | `Inspeccion/GuardarInformeTecnico` |
| **POST finalizar** | `Inspeccion/FinalizarInformeTecnico/{id}` |
| **POST firmar** | `Inspeccion/FirmarInformeInspector` |

📷 `inspector/inspector-informe-borrador.png` · 📷 `inspector/inspector-informe-firmado-enviado.png`

| Tras firma inspector | Valor BD |
|----------------------|----------|
| `estado_informe` | `FIRMADO_INSPECTOR` → `ENVIADO_A_DIRDAC` |
| Bandeja DIRDAC | Fila visible en Pendientes Dirección |

| Mensaje auth si LV incompleta | *Debe finalizar y firmar la LV antes de gestionar el Informe Técnico.* |

---

## Fase 12 — DIRDAC: aprobar informe

| Campo | Valor |
|-------|-------|
| **Menú** | **Informe técnico y NC** / **Listos para firma** |
| **URL** | `/Inspeccion/PendientesDireccion` |
| **Vista** | `Views/InformeTecnico/PendientesDireccion.cshtml` |
| **POST firma** | `Inspeccion/FirmarInformeDirdac` |

📷 `dirdac/dirdac-pendientes-direccion-informe.png` · 📷 `dirdac/dirdac-informe-aprobado.png`

| Tras aprobación | Estado solicitud **`AOCR En Elaboracion`** |
| RT 👁 | Seguimiento en Detalle — aún sin AOCR final |

---

## Fase 13 — Generación borrador AOCR (sistema)

| Campo | Valor |
|-------|-------|
| **Servicio** | `GeneracionAOCRService` |
| **Documento** | Tipo `AOCR_GENERADO` |
| **Plantilla PDF** | `Views/Certificado/CertificadoAOCR.cshtml` |

📷 `coordinacion/coordinacion-detalle-aocr-elaboracion.png`

| RT 👁 | Estado **`AOCR En Elaboracion`** |
| Inspector | Puede ver borrador en panel inspección (según permisos) |

---

## Fase 14 — Coordinación: ValidarAocr

| Campo | Valor |
|-------|-------|
| **Menú** | **AOCR y condiciones** (rol Coordinación / DIRDAC) |
| **URL** | `/CoordinacionJefatura/ValidarAocr?solicitudId=12` |
| **Vista** | `Views/CoordinacionJefatura/ValidarAocr.cshtml` |
| **Auth** | `[AocrAuthorize Modulo=CoordinacionJefatura Accion=ValidarAocr]` |

📷 `coordinacion/coordinacion-validar-aocr.png`

| Tras validación formal | Estado **`AOCR En Revision`** |

---

## Fase 15 — DIRDAC: firma y emisión AOCR

| Campo | Valor |
|-------|-------|
| **Cadena estados** | `AOCR En Revision` → `Enviado DCAV` → `Firmado DCAV` → **`AOCR Emitido/Recibido`** |
| **Menú** | Pendientes Dirección + ValidarAocr |

📷 `dirdac/dirdac-firma-aocr.png` · 📷 `dirdac/dirdac-estado-emitido-recibido.png`

| Certificado | Firma digital `.p12` DIRDAC |
| RT 👁 | Badge **Documentos finales** incrementado |

---

## Fase 16 — RT: descarga documentos finales

| Campo | Valor |
|-------|-------|
| **Menú** | **Documentos finales** / **Finalizados** / **AOCR y condiciones** |
| **URL** | `/SolicitudAOCR/GeneradasFirmadas` |
| **Vista** | `Views/SolicitudAOCR/GeneradasFirmadas.cshtml` |

📷 `rt/rt-generadas-firmadas-bandeja.png` · 📷 `rt/rt-descarga-pdf-aocr-final.png`

| Texto UI | Validación |
|----------|------------|
| *AOCR Generadas y Firmadas* | ✅ `ViewBag.Title` / `<h2>` |
| Tarjeta resumen *Firmadas / finalizadas* | ✅ Contador coherente |
| Filtro búsqueda | Placeholder *Solicitud, AOCR, explotador...* |
| Fila #12 | Estado **`AOCR Emitido/Recibido`** |
| Acción descarga PDF | ✅ Certificado AOCR firmado |

**Done operativo RT:** PDF AOCR descargado y archivado.

---

## Anexo A — Modificación tipo 3 (capturas)

| Escenario | 📷 | URL clave |
|-----------|-----|-----------|
| Cierre nuevo aeropuerto | `inspector/inspector-cierre-fase-nuevo-aeropuerto.png` | POST `CerrarFaseDocumentalNuevoAeropuertoModificacion` |
| RT orden post `Requiere Inspeccion` | `rt/rt-modificacion-orden-post-requiere-inspeccion.png` | `/OrdenRecaudacion/Nueva` |

---

## Anexo B — Matriz visual rápida por rol

| Rol | Pantallas obligatorias en manual 100% |
|-----|--------------------------------------|
| RT | Formulario, orden, docs, envío, detalle estados, subsanar, GeneradasFirmadas |
| Financiero | Dashboard, aprobar pago |
| Coordinación | RevisionVerificacion, firma, Tecnico/Index, AsignarInspector, ValidarAocr |
| Inspector | RevisionDocumental, Lista revision/ver, Detalle, LV, Informe |
| DIRDAC | PendientesDireccion, firma AOCR |

---

## Procedimiento de captura (equipo QA)

1. Ejecutar flujo #12 con usuarios reales por rol (ver `CHECKLIST_DOCUMENTACION_100.md`).
2. En cada fase, capturar PNG según tabla § Índice.
3. Guardar en `docs/images/{rol}/` con nombre exacto.
4. Insertar en Markdown: `![Fase N](../images/rt/archivo.png)` (opcional).
5. Regenerar exportaciones: `cd docs/export && npm run build`.

---

*Referencia código: `SidebarMenuBuilder.cs`, `DocumentoController.cs`, `InspeccionController.cs`, `Financiero/Index.cshtml`, `GeneradasFirmadas.cshtml`, `ValidarAocr.cshtml`.*
