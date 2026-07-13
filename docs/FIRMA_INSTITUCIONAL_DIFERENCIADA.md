# Firma institucional diferenciada

La fase separa de forma invariable la firma del AOCR (`RECONOCIMIENTO`) por el rol real `17/Direccion` de la firma de Condiciones y Limitaciones por `24/DIRECTOR_CERTIFICACIONES_DCAV`.

## Flujo

La aprobación conjunta DCAV conserva los PDF exactos en `aocr_tbdocumento_inspeccion`, deja el AOCR en `PENDIENTE_FIRMA_DGAC`, las Condiciones en `PENDIENTE_FIRMA_DCAV` y el expediente en `PENDIENTE_FIRMAS_INSTITUCIONALES`. Cada firma bloquea el expediente y los dos documentos, valida versión, tamaño y SHA-256 del PDF fuente, perfil, imagen y posición, genera un archivo nuevo y registra firma, historial e idempotencia en una sola transacción. Un fallo de base elimina el archivo creado y, si la compensación falla, registra `FIRMA_COMPENSACION_ERROR`.

La primera firma mantiene `PENDIENTE_FIRMAS_INSTITUCIONALES`. Solo `FIRMADO_DGAC` + `FIRMADO_DCAV` produce `DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE`. Esta fase no llama a finalización, correo final, AS400 ni finanzas. La ruta de firma heredada rechaza expresamente expedientes del flujo diferenciado.

## Provisión obligatoria

Las imágenes no se guardan en rutas públicas. Deben residir bajo `App_Data/Signatures` y configurarse mediante transformación segura o variables de entorno:

- `AOCR_SIGNATURE_DGAC_AUTHORIZEDUSERID`, `IMAGEID`, `IMAGEPATH`, `IMAGESHA256`, `CARGO`.
- `AOCR_SIGNATURE_DCAV_AUTHORIZEDUSERID`, `IMAGEID`, `IMAGEPATH`, `IMAGESHA256`, `CARGO`.
- opcionalmente `VALIDFROM` y `VALIDTO` con formato `yyyy-MM-dd`.

El nombre y el rol se leen de PostgreSQL. Se validan simultáneamente el código y la descripción mediante `AOCR.Signature.DGAC.RoleCode=17` y `AOCR.Signature.DCAV.RoleCode=24`. El cargo se toma primero de `usuario.cargo`; la configuración solo cubre el dato institucional faltante. Sin nombre, cargo, imagen, hash o vigencia, la operación devuelve 422/403 y no crea archivos ni firmas. Los rechazos quedan auditados como `FIRMA_DOCUMENTO_RECHAZADA`.

## Posiciones

`AOCR.Signature.Position.<TIPO>.V<VERSION>` usa `pagina,x,y,ancho,alto,margen,qr,alineacion,nombreY,cargoY,fechaY,qrX,qrY,qrTamanio`; las medidas son ratios del área de página y se resuelven exclusivamente en backend. El navegador nunca envía coordenadas. Cada versión nueva de plantilla requiere su propia entrada y validación visual previa al despliegue.

## Despliegue y reversión

1. Respaldar esquema y archivos `App_Data/Uploads/AOCR`.
2. Ejecutar `scripts/011_firma_institucional_diferenciada.sql` y luego `scripts/012_firma_institucional_endurecimiento.sql` con `ON_ERROR_STOP`.
3. Provisionar perfiles e imágenes y verificar sus SHA-256.
4. Publicar aplicación, validar bandejas DGAC/DCAV y firmar un expediente controlado.
5. Para revertir restricciones e índices, ejecutar los rollback. Los PDF, firmas, estados e historial no se borran automáticamente.

No existen expedientes activos ni firmas institucionales en la base diagnosticada; por ello no fue posible producir evidencia real sin fabricar datos de negocio. La validación visual debe realizarse con las imágenes institucionales autorizadas y el primer paquete aprobado real.
