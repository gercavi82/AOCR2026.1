# Servicio central de PDF oficiales

## Diagnóstico e inventario

La búsqueda de `GenerarPdf`, `GuardarPdf`, `File.Exists`, `MapPath`, `FileResult`, rutas, hashes y firmas encontró 253 referencias. Los generadores oficiales del par final estaban concentrados en `FirmaAocrPdfService` y se invocaban desde `RevisionDocumentosInspectorService` y el flujo legado de `FirmaAocrController`. También existen generadores de informe, certificado, orden y vistas previas; quedan fuera de esta fase porque no producen el par AOCR/Condiciones enviado a DCAV.

Se reutilizan exclusivamente estructuras existentes:

- `aocr_tbdocumento_generado`: documento origen, solicitud, inspección, compañía, estado, vigencia y versión editable.
- `aocr_tbdocumento_inspeccion`: metadatos físicos, ruta, hash, tamaño, tipo, versión, usuario y observación.
- `aocr_tbfirma_documento`: evidencia de firma e inmutabilidad.
- `aocr_tbauditoria`: eventos de persistencia y flujo.

Las rutas anteriores se escribían en `~/App_Data/Uploads/AOCR/{Oficiales|Condiciones}/{solicitud}` con nombre basado en segundos. Esto permitía doble generación, colisión temporal, registro sin archivo si fallaba la actualización y archivo huérfano si fallaba la base. El método aceptaba bytes vacíos y la lectura confiaba sólo en `File.Exists`.

Riesgos confirmados: cálculo de versión separado del archivo, ausencia de restricción única, doble clic, falta de SHA-256 persistido en el flujo final, acceso mediante coordenadas de solicitud/tipo/versión y posibilidad de que metadatos y disco diverjan. La base no permite detectar archivos sin registro; se requiere el diagnóstico físico incluido.

## Diseño implementado

`IDocumentoPdfService` es el único punto de generación oficial del par final. Valida identidad, rol, asignación, compañía, solicitud, inspección, origen vigente, versión, estado editable, campos obligatorios y ausencia de firma. La previsualización continúa separada y con marca `BORRADOR`.

La clave idempotente es `{solicitud}:{inspeccion}:{origen}:{tipo}:{versionOrigen}:GENERAR_PDF`. Se protege con `pg_advisory_xact_lock`; la versión se calcula dentro de una transacción `SERIALIZABLE`. El archivo se crea como temporal, se comprueba cabecera `%PDF-`, tamaño y SHA-256, y se mueve sin sobrescritura a:

`~/App_Data/AOCR/{Solicitud}/{Inspeccion}/{Tipo}/vNNN/{Tipo}_{Solicitud}_{Inspeccion}_vNNN.pdf`

Después se insertan metadatos, se actualiza el origen y se audita dentro de la misma transacción. Si la base falla después del movimiento, se elimina el archivo final; si la compensación falla se emite `[PDF][ORPHAN_FILE]` para que el diagnóstico lo reporte.

Las versiones históricas permanecen intactas. La vigente se infiere como la mayor versión por inspección y tipo, porque la tabla reutilizada no tiene columna `vigente`. Un documento firmado no puede regenerarse.

## Descarga y continuidad de flujo

La descarga usa sólo `/DocumentoPdf/Descargar/{id}`. El navegador nunca entrega una ruta. El servicio resuelve exclusivamente rutas bajo `App_Data/AOCR`, bloquea traversal, revalida cabecera, tamaño y SHA-256, y aplica autorización por Inspector asignado, rol interno o compañía.

Antes de `ENVIAR_DOCUMENTOS_DCAV`, `AocrDcavRevisionService` exige AOCR y Condiciones vigentes y valida físicamente ambos. Un registro ausente, 404, tamaño distinto o hash inválido bloquea la transición.

## Operación

1. Ejecutar `scripts/006_documento_pdf_central.sql` en QA. Si el preflight devuelve duplicados, resolverlos conservando históricos; el script aborta y no crea el índice.
2. Asegurar permisos de lectura/escritura del Application Pool sobre `App_Data/AOCR` y denegar publicación web de `App_Data`.
3. Generar AOCR y Condiciones desde la sección exclusiva del Inspector.
4. Consultar `/Diagnostico/ConsistenciaPdf` con rol Administrador. Debe devolver `Consistente=true`.
5. Probar doble clic/reintento: debe devolver el mismo `DocumentoPdfId`, sin nueva versión.

## Eventos relevantes

`[PDF][GENERAR_IN]`, `[PDF][GENERAR_OK]`, `[PDF][GENERAR_ERROR]`, `[PDF][IDEMPOTENT_HIT]`, `[PDF][INTEGRITY_ERROR]`, `[PDF][DOWNLOAD_OK]`, `[PDF][DOWNLOAD_DENY]`, `[PDF][ORPHAN_FILE]` y `[PDF][CONSISTENCY]`.

## Alcance no modificado

No se implementó firma final DIRDAC, segunda revisión DCAV, cierre final, notificación final, finanzas, AS400 ni rediseño general. Los demás generadores históricos fueron inventariados, pero migrarlos requiere una fase separada para no alterar contratos ajenos al par AOCR/Condiciones.
