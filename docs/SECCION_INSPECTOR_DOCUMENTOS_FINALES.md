# Sección exclusiva del Inspector: AOCR y Condiciones

## Diagnóstico

La ruta operativa existente era `FirmaAocr/PendientesInspector -> FirmaAocr/Index`. Esa pantalla comparte responsabilidades con firma institucional y el sidebar general también enviaba al Inspector a `SolicitudAOCR/GeneradasFirmadas`. La revisión final no tenía un controlador, contrato de servicio o ViewModels propios. La revisión documental inicial continúa en `RevisionDocumental/Index`; no forma parte de esta fase y la nueva sección no utiliza `Documento/Lista`.

Los datos finales ya disponibles se obtienen de `aocr_tbdocumento_generado`, `aocr_tbsolicitud`, `aocr_tbinspeccion`, `aocr_tbinforme_inspeccion`, `aocr_tbcertificado`, aeronaves de la solicitud y el estado/historial central. Se reutilizan los generadores `FirmaAocrPdfService`, pero no sus rutas compartidas.

## Arquitectura implementada

- `InspectorDocumentosFinalesController`: HTTP, antiforgery y códigos de respuesta.
- `IRevisionDocumentosInspectorService`: bandeja, detalle, guardados, previews y generación separada.
- `RevisionDocumentosInspectorService`: autorización del Inspector asignado, reglas documentales, auditoría y composición de ViewModels.
- `AocrDocumentoGeneradoDAO`: documento exacto por solicitud + inspección + tipo, versiones, actualización optimista y registro de PDF.
- ViewModels tipados para AOCR, Condiciones, observaciones y versiones.
- Vistas exclusivas `Revision` y `Detalle`, con dos bloques independientes.

## Rutas

- `GET /InspectorDocumentosFinales/Revision`
- `GET /InspectorDocumentosFinales/Detalle?solicitudId={id}`
- `POST /InspectorDocumentosFinales/GuardarAocr`
- `POST /InspectorDocumentosFinales/GuardarCondiciones`
- `GET /InspectorDocumentosFinales/PrevisualizarAocr`
- `GET /InspectorDocumentosFinales/PrevisualizarCondiciones`
- `POST /InspectorDocumentosFinales/GenerarPdfAocr`
- `POST /InspectorDocumentosFinales/GenerarPdfCondiciones`

`Url.Action` conserva automáticamente el virtual path `/aocr`. La notificación transaccional apunta a `/aocr/InspectorDocumentosFinales/Detalle`.

## Reglas

- Solo el Inspector técnico asignado puede acceder; otro usuario obtiene 403.
- La bandeja usa exclusivamente `DOCUMENTOS_HABILITADOS_INSPECTOR` y `DOCUMENTOS_OBSERVADOS_DCAV` para filas editables/observadas.
- El guardado no cambia el estado central ni genera PDF.
- La versión esperada se compara y se incrementa mediante actualización optimista; un conflicto devuelve 409.
- Un documento con firma registrada no es editable.
- Cuando DCAV observa documentos, el texto histórico determina AOCR, Condiciones o ambos y solo habilita el documento afectado.
- La previsualización usa datos guardados, agrega marca `BORRADOR` en memoria y no persiste ni cambia estados.
- La generación guarda AOCR y Condiciones separadamente, registra ruta, tamaño, versión y SHA-256 en auditoría, sin considerarlos enviados a DCAV.

## Persistencia de edición

No se inventaron columnas. AOCR conserva el estado del explotador y vencimiento en los campos existentes de solicitud/certificado. Condiciones conserva limitaciones y condiciones en `aprobaciones_especiales_otros` y `aprobaciones_especiales`, que ya alimentan el modelo PDF institucional. Los documentos históricos no se eliminan.

## Despliegue

1. Desplegar primero los prerrequisitos `004_habilitacion_documentos_dcav.sql` y ejecutar su reporte de duplicados.
2. Ejecutar `005_seccion_inspector_documentos_finales.sql`.
3. Publicar CapaDatos y CapaPresentacion.
4. Limpiar caché del sidebar.
5. Probar bajo `/aocr` con un Inspector asignado y con otro Inspector.
6. Guardar cada bloque, abrir ambos previews y generar ambos PDF.
7. Confirmar que contador y filas coinciden y que no se solicita `Documento/Lista`.

## Reversión

Revertir los binarios y eliminar únicamente los dos índices indicados al final del script 005. No eliminar documentos, versiones, auditoría ni datos guardados durante la vigencia del cambio.

## Verificación

- Compilar `AOCR.sln` con MSBuild en Debug.
- Ejecutar `RevisionDocumentosInspectorTests` y `RevisionDocumentosInspectorIntegrationTests`.
- Revisar logs con prefijo `[INSPECTOR_DOCS]`.
