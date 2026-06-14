# Manual de usuario — Sistema AOCR (implementación actual)

**Versión:** 2026-06-11  
**Basado en:** `CapaPresentacion`, `CapaNegocio`, `CapaDatos` del repositorio AOCR  
**No es documentación genérica:** cada ruta, estado, menú y mensaje citado corresponde al código desplegado.

**Complementos:** [`MANUAL_FLUJO_RT_A_AOCR.md`](MANUAL_FLUJO_RT_A_AOCR.md) (recorrido RT→AOCR por fases) · `MANUAL_TECNICO_AOCR.md` (§16 LV · §17 Informe · §18 Mod. tipo 3) · `GUIA_VISUAL_POR_ROL.md` · `GUIA_INSPECTOR_SOLICITUD_12.md`

---

## 0. Caso de prueba institucional documentado

Use la solicitud **#12** para validar el flujo emisión post-asignación:

| Campo | Valor de referencia (entorno `dgac_des`) |
|-------|------------------------------------------|
| Código interno | `12` |
| Número institucional | `DGAC-GOP-2026-AOCR012` |
| Operadora (ejemplo) | AMERICAN AIRLINES INC. |
| Estado esperado tras asignación | `En Inspeccion` (`EstadoSolicitud.EnInspeccion`) |
| Inspección vinculada | `CodigoInspeccion = 11` |
| Inspector asignado | `CodigoTecnico` / `CodigoInspector = 43` (ej. CATOTA TORRES) |
| Coordinación (COO-1 histórico) | Usuario revisor id `45` — **no cuenta** como decisión del inspector |
| Documentos en revisión | 12 tipos (excluye `SOLICITUD_INSPECCION_EXT` del conteo documental) |
| Publicación IIS | `C:\AOCR\publicacion1` (perfil `FolderProfile4`) |
| Log aplicación | `CapaPresentacion/App_Data/Logs/AOCR_YYYYMMDD.log` |

---

## 1. Roles canónicos en pantalla

El menú lateral usa roles **unificados** (`RoleGroupingHelper`), no los nombres crudos de BD:

| Rol unificado (sesión) | Roles crudos que mapean | Usuario prueba habitual |
|------------------------|-------------------------|-------------------------|
| `Coordinacion` | `CoordinadorInspecciones`, `GEN_COORDINACION` (forzado) | `GEN_COORDINACION` |
| `InspectorTecnico` | `Inspector`, `InspectorTecnico`, `EvaluadorTecnico`, `TECNICO` | Inspector id 43 |
| `Solicitante` | RT / operador | Propietario solicitud |
| `Financiero` | Perfil financiero | Revisor de órdenes |
| `DireccionJefaturaTecnica` | DIRDAC / dirección | Bandeja `PendientesDireccion` |
| `Administrador` | Acceso total | Soporte / QA |

**Selector de rol:** si el usuario tiene varios perfiles unificados, debe elegir uno antes de operar; el menú se construye en `SidebarMenuBuilder.Build()`.

---

## 2. Menú lateral — rutas reales por perfil

Textos y destinos extraídos de `SidebarMenuBuilder.cs`:

### 2.1 Coordinación (`Coordinacion`)

| Etiqueta menú | Controller | Action | Badge (servicio) |
|---------------|------------|--------|------------------|
| Bandeja integral | `CoordinacionJefatura` | `DashboardInspeccion` | `CoordinacionBandejaService` → `CoordinatorDocumentalQueue` |
| Pendientes de revisión | `CoordinacionJefatura` | `RevisionVerificacion` | Idem |
| *(Grupo Solicitud)* Revisión documental | `CoordinacionJefatura` | `RevisionVerificacion` | Cola documental coordinador |
| Inspecciones | `Inspeccion` | `Index` | — |

**Asignación de inspector (no está como ítem suelto):** se accede desde la bandeja de coordinación hacia  
`/Tecnico/Index` → `/Tecnico/AsignarInspector?solicitudId={id}&tipoInspector=OPS|AIR|TODOS`

### 2.2 Inspector (`InspectorTecnico`)

| Etiqueta menú | Controller | Action | Badge |
|---------------|------------|--------|-------|
| Dashboard técnico | `Dashboard` | `Inspector` | `InspectorPendingRevision` |
| Revisión documental | `RevisionDocumental` | `Index` | `RevisionDocumentalBandejaService.ContarBandejaInspector` |
| Pendientes de revisión | `RevisionDocumental` | `Index` | Idem |
| Inspecciones | `Inspeccion` | `Index` | `InspectorBandejaService` |
| Lista de Verificación LV/EAE | `Inspeccion` | `Index` | `?vista=operativa` |
| Informe técnico y NC | `Inspeccion` | `Index` | — |

### 2.3 RT / Solicitante

| Etiqueta menú | Controller | Action | Parámetros |
|---------------|------------|--------|------------|
| Solicitud AOCR | `SolicitudAOCR` | `FormularioEmisionAOCR` | `tipoSolicitud=1` |
| Renovación AOCR | `SolicitudAOCR` | `FormularioEmisionAOCR` | `tipoSolicitud=2` |
| Condiciones y limitaciones | `SolicitudAOCR` | `FormularioEmisionAOCR` | `tipoSolicitud=3` |
| Órdenes (RT) | `OrdenRecaudacion` | `Nueva` / `Detalles` | — |
| Mis trámites | `SolicitudAOCR` | `MisSolicitudes` | — |
| Documentos y expediente | `Documento` | `Subir` / `Lista` | — |

### 2.4 Financiero

| Etiqueta menú | Controller | Action |
|---------------|------------|--------|
| Dashboard financiero | `Financiero` | `Dashboard` |
| Órdenes de recaudación | `Financiero` | `TodasOrdenes` |
| Aprobar pago | `Financiero` | `AprobarPago` / `AprobarPagoConFactura` |

### 2.5 DIRDAC (`DireccionJefaturaTecnica`)

| Etiqueta menú | Controller | Action |
|---------------|------------|--------|
| Dashboard dirección | `CoordinacionJefatura` | `DashboardGerencial` |
| Informe técnico y NC | `Inspeccion` | `PendientesDireccion` |
| Listos para firma | `Inspeccion` | `PendientesDireccion` |
| AOCR y condiciones | `CoordinacionJefatura` | `ValidarAocr` |

**Regla de contadores:** el número del badge debe igualar las filas de la bandeja del mismo servicio (`AocrSidebarCounterService`).

---

## 3. Tipos de solicitud y campo `tipoSolicitud`

| Valor | Formulario | Destino tras firma coordinador (`RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental`) |
|-------|------------|-------------------------------------------------------------------------------------------------------------|
| `1` | `SolicitudAOCR/FormularioEmisionAOCR?tipoSolicitud=1` | `Pendiente Asignacion RT` |
| `2` | `...?tipoSolicitud=2` | `Pendiente Asignacion RT` |
| `3` | `...?tipoSolicitud=3` | `Firmado Coordinador` → panel modificación en `Detalle.cshtml` |

Constantes: `CapaDatos/Constants/EstadoConstants.cs` → clase `EstadoSolicitud`.

---

## 4. Estados de solicitud relevantes (texto persistido)

Estos son los valores **exactos** en columna estado de `aocr_tbsolicitud`:

| Estado C# | Texto en BD / UI |
|-----------|------------------|
| `EnRevision` | `En Revision` |
| `DocumentacionPendiente` | `Documentacion Pendiente` |
| `Observada` | `Observada` |
| `Subsanada` | `Subsanada` |
| `AceptacionDocumental` | `Aceptacion Documental` |
| `PendienteAsignacionRT` | `Pendiente Asignacion RT` |
| `EnInspeccion` | `En Inspeccion` |
| `RequiereInspeccion` | `Requiere Inspeccion` |
| `GeneradoCondicionesLimitaciones` | `Generado Condiciones y Limitaciones` |
| `FirmadoCoordinador` | `Firmado Coordinador` |
| `AOCR_EnElaboracion` | `AOCR En Elaboracion` |
| `AOCR_EnRevision` | `AOCR En Revision` |
| `EnviadoDcav` | `Enviado DCAV` |
| `FirmadoDcav` | `Firmado DCAV` |
| `AOCR_EmitidoRecibido` | `AOCR Emitido/Recibido` |
| `Finalizado` | `Finalizado` |

**Regla verificada en código:** descargar PDF de aceptación documental **no** ejecuta transición a `Finalizado` (`SolicitudAOCRController` — sin atajo post-descarga).

---

## 5. Flujo emisión (tipo 1/2) — secuencia verificable

```text
RT: SolicitudAOCR/FormularioEmisionAOCR (tipo 1 o 2)
  → OrdenRecaudacion/Nueva + comprobante
Financiero: Financiero/AprobarPago → estado avanza a carga documental
RT: Documento/Subir + envío desde SolicitudAOCR/Detalle/{id}
  → estado En Revision
Coordinación: CoordinacionJefatura/RevisionVerificacion (pre-asignación)
  → SolicitudAOCR/FirmarAceptacionDocumental/{id}
  → Pendiente Asignacion RT
Coordinación: Tecnico/Index → Tecnico/AsignarInspector (POST)
  → En Inspeccion + registro en aocr_tbinspeccion
Inspector: RevisionDocumental/Index → Documento/Lista?modo=revision
  → Inspeccion/ConfirmarRevisionDocumentalInspector/{idInspeccion}
  → LV (Guardar/Finalizar/FirmarListaVerificacionOperacionalEae)
  → Informe (Guardar/Finalizar/FirmarInformeInspector) → FIRMADO_INSPECTOR
DIRDAC: Inspeccion/PendientesDireccion → aprobación
Coordinación: CoordinacionJefatura/ValidarAocr
DIRDAC: firma AOCR → AOCR Emitido/Recibido
RT: SolicitudAOCR/GeneradasFirmadas
```

---

## 6. Revisión documental — dos fases (regla central del sistema)

### 6.1 Fase A — Coordinación **pre-asignación** (COO-1)

**Condición código:** `SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion` = true cuando:
- **No** hay inspector asignado (`TieneInspectorAsignado` = false), y
- Estado ∈ `{ En Revision, Documentacion Pendiente, Subsanada }`.

| Aspecto | Comportamiento |
|---------|----------------|
| Pantalla | `CoordinacionJefatura/RevisionVerificacion` o revisión desde `SolicitudAOCR/Detalle` |
| Revisiones que cuentan | Todas en `aocr_tbrevision_documental` (`ObtenerUltimasRevisionesPorSolicitud`) |
| Inferencia `documento.estado = Aprobado` | **Sí** cuenta como ACEPTADO si no hay fila en revisiones |
| Observación típica prueba | *"Revision documental institucional pre-asignacion (COO-1)"* (script dev o coordinador) |

### 6.2 Fase B — Inspector **post-asignación**

**Condición código:** `RequiereDecisionDocumentalInspector(codigoSolicitud)` = `TieneInspectorAsignado` (campo `CodigoTecnico` en solicitud **o** `CodigoInspector` en inspección).

| Aspecto | Comportamiento |
|---------|----------------|
| Bandeja | `/RevisionDocumental/Index` — servicio `RevisionDocumentalBandejaService` |
| Pantalla de decisión | `/Documento/Lista?solicitudId={id}&modo=revision&origen=revision-documental` |
| Revisiones que cuentan | Solo donde `codigo_usuario_revisor` ∈ `{CodigoTecnico, CodigoInspector}` |
| Inferencia desde `Aprobado` en BD | **No** — sin revisión del inspector → **PENDIENTE** |
| Revisor / observación en UI | Solo datos de revisión del inspector (`ViewBag.EsFaseInspectorDocumental`) |

**Endpoints AJAX de decisión** (`DocumentoController`):

| Acción | URL | Validación observación |
|--------|-----|------------------------|
| Aceptar | `POST Documento/AceptarDocumentoSolicitud` | Devolución N/A |
| Devolver | `POST Documento/DevolverDocumentoSolicitud` | Motivo ≥ 10 caracteres |
| Reabrir | `POST Documento/ReabrirDocumentoSolicitud` | Solo Coordinación / Admin |

Persistencia: `RevisionDocumentalDAO.RegistrarRevision` → tabla `aocr_tbrevision_documental`.

### 6.3 Textos exactos de la pantalla `Documento/Lista`

Definidos en `Views/Documento/Lista.cshtml`:

| Modo | `ViewBag.ModoDocumentos` | Título tabla | Badge capacidad |
|------|--------------------------|--------------|-----------------|
| Consulta | `ver` | **Archivos del expediente** | **Solo lectura** |
| Revisión | `revision` | **Bandeja de revisión documental** | **Revisión activa** (si `PuedeRevisarDocumentos=true`) |

**Error frecuente validado:** abrir `modo=ver` desde `Inspeccion/Detalle` (enlace consulta) muestra expediente **sin** botones Aceptar/Devolver — es correcto para consulta, incorrecto para decidir.

### 6.4 Acciones en bandeja `RevisionDocumental/Index`

Según estado de cada fila (`RevisionDocumentalSolicitudRowViewModel`):

| Condición | Botón | Destino |
|-----------|-------|---------|
| Documentos pendientes de decisión | **Revisar documentación** | `Documento/Lista?modo=revision` |
| Todos aceptados, falta confirmación | **Confirmar fase documental** | `Inspeccion/Detalle/{CodigoInspeccion}` |
| Cierre confirmado | **Continuar en LV/EAE** | `Inspeccion/Detalle/{CodigoInspeccion}` |

Códigos de fila: `EN_REVISION_DOCUMENTAL`, `PENDIENTE_CONFIRMACION_INSPECTOR`, `LISTO_INSPECCION_CAMPO`.

---

## 7. Coordinación — procedimiento preciso

### 7.1 Firma aceptación documental

- **Ruta:** `POST SolicitudAOCR/FirmarAceptacionDocumental/{id}`
- **Autorización:** `[AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "FirmarAceptacionDocumental")]`
- **Servicio:** `RevisionDocumentalService.PrepararFirmaAceptacionDocumental`
- **Log:** `[FirmarAceptacionDocumental]` en log de aplicación

| Tipo | Estado destino exacto |
|------|----------------------|
| 1 / 2 | `Pendiente Asignacion RT` |
| 3 | `Firmado Coordinador` |

### 7.2 Asignar inspector

1. `/Tecnico/Index` — datos: `CoordinacionBandejaService.ObtenerPendientesAsignacion()`
2. `/Tecnico/AsignarInspector?solicitudId=12&tipoInspector=OPS`
3. **POST** `Tecnico/AsignarInspector` con:
   - `inspectorPrincipal` (cédula/login del catálogo `UsuarioInternoRTBL.ListarInspectoresAsignables`)
   - `fechaInspeccion`, `horaInspeccion`
   - Filtros: `OPS` | `AIR` | `TODOS`
4. **Autorización:** `[AocrAuthorize(Modulo = "Tecnico", Accion = "AsignarInspector")]`
5. **Log éxito:** `[GestionInspeccion]`
6. **Estado esperado solicitud #12:** `En Inspeccion`

---

## 8. Inspector — procedimiento preciso (solicitud #12)

### 8.1 Revisión documental

1. Login usuario inspector id **43**.
2. Menú **Revisión documental** → `/RevisionDocumental/Index`.
3. Fila `DGAC-GOP-2026-AOCR012` → **Revisar documentación**.
4. URL resultante:
   ```
   /Documento/Lista?solicitudId=12&modo=revision&origen=revision-documental
   ```
5. Verificar **12 filas** (aprox.) con:
   - Estado: badge gris **PENDIENTE**
   - Observación: vacía
   - Revisado por: vacío
   - Botones: **Aceptar** / **Devolver**
6. Tras aceptar cada documento:
   - `POST Documento/AceptarDocumentoSolicitud`
   - Revisor = nombre del inspector en sesión (no `GERMAN ALBERTO` / id 45)
7. Log: `[DOC_FLOW]`, `[DOC_SOLICITUD]`

### 8.2 Confirmar cierre documental

1. `/Inspeccion/Detalle/11`
2. Botón: **Confirmar cierre documental** (`Views/Inspeccion/Detalle.cshtml` línea ~3388)
3. **POST** `Inspeccion/ConfirmarRevisionDocumentalInspector/11`
4. **Autorización:** `[AocrAuthorize(Modulo = "Inspeccion", Accion = "ConfirmarRevisionDocumentalInspector")]`
5. Confirmación registrada si:
   - `inspeccion.estado_documental` ∈ `{ EN_REVISION, ACEPTADA, APROBADO }`, **o**
   - `inspeccion.Comentarios` contiene *"Inspector confirmó revisión documental"*
   - (`RevisionDocumentalService.InspectorConfirmoCierreDocumental`)

**Mensaje UI si LV bloqueada:** *"Revise la documentación cargada y confirme el cierre documental antes de habilitar la LV/EAE y el informe técnico."*

### 8.3 LV/EAE

| Paso | Acción HTTP | Atributo AocrAuthorize |
|------|-------------|------------------------|
| Guardar borrador | `POST Inspeccion/GuardarListaVerificacionOperacionalEae` | `GuardarListaVerificacionOperacionalEae` |
| Finalizar PDF | `POST Inspeccion/FinalizarListaVerificacionOperacionalEae/{id}` | `FinalizarListaVerificacionOperacionalEae` |
| Firmar | `POST Inspeccion/FirmarListaVerificacionOperacionalEae` | `FirmarListaVerificacionOperacionalEae` |

Precondición: `RevisionDocumentalService.PuedeInspectorAbrirFaseOperativaLv` = fase documental aprobada **+** cierre confirmado.

**Textos UI bloqueo:** *"BLOQUEADO POR LV/EAE"*, *"Debe finalizar y firmar la Lista de Verificación Operacional LV/EAE antes de elaborar el informe técnico."*

### 8.4 Informe técnico

| Paso | Acción HTTP | Resultado |
|------|-------------|-----------|
| Guardar | `POST Inspeccion/GuardarInformeTecnico` | Borrador |
| Finalizar | `POST Inspeccion/FinalizarInformeTecnico/{id}` | PDF generado |
| Firmar inspector | `POST Inspeccion/FirmarInformeInspector` | `FIRMADO_INSPECTOR` + envío DIRDAC |

Modal: `GET Inspeccion/ModalInformeTecnico` · PDF en `App_Data/Documentos/...`

---

## 9. Financiero — procedimiento preciso

| Paso | Ruta | Notas |
|------|------|-------|
| Bandeja | `Financiero/TodasOrdenes` o `Financiero/Index?estado=TODAS` | Badge = `FinancialPendingReviewOrders` |
| Detalle | `Financiero/DetalleOrden/{id}` | — |
| Aprobar | `Financiero/AprobarPago/{id}` o `AprobarPagoConFactura` | Correo vía `AocrEmailFlujoService` (idempotente por `event_key`) |
| Rechazar | `Financiero/RechazarOrden/{id}?motivo=...` | — |

**Validación contador:** sidebar badge financiero = filas en bandeja con mismo filtro `FinancialOrderStateHelper`.

---

## 10. DIRDAC — procedimiento preciso

| Paso | Ruta | Efecto |
|------|------|--------|
| Bandeja informes | `Inspeccion/PendientesDireccion` | Informes con `FIRMADO_INSPECTOR` |
| Aprobar informe | Acción en bandeja (controlador `InspeccionController`) | Solicitud → `AOCR En Elaboracion` / revisión según tipo |
| Validar AOCR | `CoordinacionJefatura/ValidarAocr` | Revisión formal coordinación |
| Firma DIRDAC | `Inspeccion/FirmarInformeDirdac` (informe) + flujo firma AOCR | `Enviado DCAV` → `Firmado DCAV` → `AOCR Emitido/Recibido` |

---

## 11. Documentos incluidos en conteo documental

`SolicitudAocrInfraBL.DebeIncluirEnRevisionDocumental` excluye **solo** `SOLICITUD_INSPECCION_EXT`.

Tipos canónicos reconocidos (entre otros):

- `COMPROBANTE_PAGO`
- `CERTIFICADO_AERONAVEGABILIDAD`
- `MANUAL_OPERACIONES`
- `OPSPECS_ESPECIFICACIONES_OPERACIONALES`
- `COPIA_AOC_VALIDA`
- `CERTIFICADO_RUIDO_AERONAVES_EAE`
- `PERMISO_OPERACION_CNAC`
- `COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR`

Se toma la **última versión** por tipo (`Version`, `FechaCarga`, `CodigoDocumento`).

---

## 12. Modificación tipo 3 — ramas exactas

Detección: `AocrModificationWorkflowService.TieneNuevoAeropuertoDeclarado` (campo `AeropuertosEcuador` no vacío).

| Escenario | Acción permitida | Controlador |
|-----------|------------------|-------------|
| Con aeropuertos nuevos | Solo `CerrarFaseDocumentalNuevoAeropuertoModificacion` | `SolicitudAOCRController` |
| Sin aeropuertos | Generar CL **o** derivar inspección | Panel `Detalle.cshtml` |
| Tras `Requiere Inspeccion` | RT crea orden | `OrdenRecaudacion/Nueva` |

Bloqueos: `PrepararGeneracionCondicionesLimitaciones` rechaza si hay nuevo aeropuerto.

---

## 13. Errores — síntoma, causa en código, acción

| Síntoma en UI | Causa en implementación | Acción |
|---------------|------------------------|--------|
| Docs ACEPTADO + revisor coordinador sin actuar inspector | `modo=ver` o inferencia antigua desde `documento.ValidadoPor` | Usar `modo=revision`; republicar DLLs recientes |
| Badge **Solo lectura** | `ViewBag.ModoDocumentos = ver` | Entrar por `RevisionDocumental/Index` |
| Bandeja revisión vacía | Sin asignación o `InspeccionDAO` sin filas para inspector 43 | Coordinación: `Tecnico/AsignarInspector` |
| LV candado | `InspectorConfirmoCierreDocumental` = false | Confirmar cierre en `Inspeccion/Detalle/11` |
| HTTP 403 en LV/Informe | `ValidarAutorizacionFlujoInspeccion` — usuario ≠ inspector asignado | Verificar `CodigoInspector` en inspección #11 |
| Asignación error 500 | Desalineación bandeja/recaudación (corregido en `SolicitudAOCRDAO`) | Ver log `[AOCR][ASIGNACION_INSPECTOR]` |

**Marcadores log:**

```text
[DOC_FLOW] Accion=BANDEJA_INSPECTOR
[DOC_SOLICITUD] solicitudId=12; modo=revision
[FirmarAceptacionDocumental]
[GestionInspeccion]
[AUTH] Acceso bloqueado
```

---

## 14. Checklist validación — solicitud #12

| # | Prueba exacta | Evidencia | ☐ |
|---|---------------|-----------|---|
| 1 | `GEN_COORDINACION` → `Tecnico/AsignarInspector?solicitudId=12` sin error | Log `[GestionInspeccion]` | ☐ |
| 2 | Estado solicitud = `En Inspeccion` | `SolicitudAOCR/Detalle/12` | ☐ |
| 3 | Inspector 43 → `RevisionDocumental/Index` muestra #12 | ≥1 fila | ☐ |
| 4 | `Documento/Lista?solicitudId=12&modo=revision` — 12 docs **PENDIENTE** | Captura | ☐ |
| 5 | Columnas observación/revisor vacías antes de decidir | Captura | ☐ |
| 6 | Aceptar 1 doc → revisor = inspector 43 | Fila actualizada | ☐ |
| 7 | Todos aceptados → botón **Confirmar fase documental** en bandeja | `RevisionDocumental/Index` | ☐ |
| 8 | `ConfirmarRevisionDocumentalInspector/11` → LV sin candado | `Inspeccion/Detalle/11` | ☐ |
| 9 | LV finalizada + firmada | Badge *LV firmada* | ☐ |
| 10 | Informe → `FirmarInformeInspector` → `FIRMADO_INSPECTOR` | Panel informe | ☐ |

---

## 15. Glosario (términos del código)

| Término | Definición en AOCR |
|---------|-------------------|
| COO-1 | Revisión pre-asignación; revisiones con revisor ≠ inspector asignado |
| `aocr_tbrevision_documental` | Tabla de decisiones por documento |
| `FlujoDocumentalCodigo` | Ej. `EN_REVISION_INSPECTOR`, `PENDIENTE_CONFIRMACION_INSPECTOR` |
| `EsFaseInspectorDocumental` | `ViewBag` en `Documento/Lista` cuando hay inspector asignado |
| Post-asignación coordinador | `AocrPostPagoWorkflowService.EsFlujoPostAsignacionCoordinador` — no exige pago RT si ya asignó coordinación |

---

*Última actualización: 2026-06-11 — alineado a código en `CapaPresentacion/Controllers/DocumentoController.cs`, `RevisionDocumentalController.cs`, `InspeccionController.cs`, `TecnicoController.cs`, `CapaNegocio/SolicitudAocrInfraBL.cs`.*
