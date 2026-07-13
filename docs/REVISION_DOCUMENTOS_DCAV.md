# Segunda bandeja DCAV: AOCR y Condiciones

## Diagnostico y causa raiz

La ruta anterior `/AocrDcav/Revision` componia en memoria informes, documentos finales, observados, estados legacy e informes recuperados. `ConstruirItem` seleccionaba el ultimo documento por solicitud y `VerDocumentoEnviado` abria esa version, no necesariamente la enviada. Aprobacion y devolucion compartian el flujo de informe; no actualizaban selectivamente el par documental ni tenian idempotencia propia.

La primera bandeja queda dedicada a `PENDIENTE_REVISION_INFORME_DCAV`. La segunda usa `/AocrDcav/RevisionDocumentos` y exclusivamente `PENDIENTE_REVISION_DOCUMENTOS_DCAV`.

## Componentes

- `IAocrDcavDocumentosDAO` / `AocrDcavDocumentosDAO`: consulta conjunta con alias explicitos, detalle, historial y contador.
- `IRevisionDocumentosDcavService`: autorizacion, integridad, aprobacion, devolucion, concurrencia e idempotencia.
- `RevisionDocumentosDcavViewModels`: modelos tipados de bandeja, detalle, documentos, soportes, historial y observacion.
- `AocrDcavController`: `RevisionDocumentos`, `DetalleDocumentos`, `HistorialDocumentos`, `AprobarDocumentos` y `DevolverDocumentos`.

No se usa `ObtenerParaBandejaEjecutivaAprobacion`, consultas de primera revision, `dynamic`, rutas fisicas del cliente ni compañia activa DCAV.

## Version exacta

El envio conjunto registra `AocrId`, `AocrPdfId`, `CondicionesId`, `CondicionesPdfId` y versiones en `aocr_proceso_estado_historial`. La consulta recupera esos IDs del ultimo evento `ENVIAR_DOCUMENTOS_DCAV` y enlaza por claves exactas; no usa `MAX(version)`. Antes de mostrar o decidir, `DocumentoPdfService` valida pertenencia, archivo, tamaño y SHA-256.

## Decisiones

- Aprobar: ambos documentos `APROBADO_DCAV`; historial `APROBADO_DOCUMENTOS_DCAV`; estado final atomico `PENDIENTE_FIRMA_DIRDAC`; notificacion a usuarios DIRDAC/DGAC reales.
- Devolver AOCR: AOCR `OBSERVADO_DCAV`, Condiciones `APROBADO_DCAV`.
- Devolver Condiciones: AOCR `APROBADO_DCAV`, Condiciones `OBSERVADO_DCAV`.
- Devolver ambos: ambos `OBSERVADO_DCAV`.
- Toda devolucion termina en `DOCUMENTOS_OBSERVADOS_DCAV`, exige documento, seccion/campo y observacion, y notifica al Inspector asignado.

Cada operacion usa transaccion `SERIALIZABLE`, advisory lock, version de expediente, IDs/versiones exactos y clave de idempotencia. Los errores producen codigos HTTP reales y nunca una bandeja vacia silenciosa.

## Despliegue y reversion

1. Respaldar esquema y confirmar scripts `004` y `007`.
2. Ejecutar `scripts/008_revision_documentos_dcav.sql` con `ON_ERROR_STOP`.
3. Publicar binarios y vistas.
4. Confirmar que contador y filas visibles coincidan.

La reversion estructural ejecuta `008_revision_documentos_dcav_rollback.sql`. No se revierten aprobaciones/devoluciones consumadas ni se eliminan documentos o historial. Para revertir la aplicacion, restaurar los binarios anteriores junto con los enlaces de menu.

## Evidencia

Ejecutar `RevisionDocumentosDcavSegundaBandejaTests` y `RevisionDocumentosDcavSegundaBandejaIntegrationTests`, compilar `AOCR.sln` y precompilar Razor. Logs operativos usan el prefijo `[DCAV_DOCS]`.
