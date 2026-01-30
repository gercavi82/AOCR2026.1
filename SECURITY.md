# Política de Seguridad

Este documento describe prácticas recomendadas para endurecer la aplicación
AOCR en entornos DEV/QA/PROD.

## Reporte de vulnerabilidades

Si encuentra una vulnerabilidad, por favor reporte al equipo responsable de
seguridad de la organización. Evite abrir issues públicos con detalles
sensibles.

## Hardening recomendado

- **Autenticación y roles**: limitar acciones críticas a los roles correctos.
- **CSRF**: usar tokens antiforgery en formularios de escritura.
- **Validación de entrada**: validar parámetros y tipos en controladores.
- **Registro y monitoreo**: registrar eventos clave (pagos, aprobaciones,
  rechazos) sin exponer información sensible.

## Cargas de archivos (uploads)

Recomendaciones mínimas:

- Guardar archivos **fuera del webroot** (`UploadStoragePath`).
- Validar extensión y tipo MIME (p. ej. solo `.pdf`, `.jpg`, `.png`).
- Validar tamaño máximo (`MaxUploadSize`).
- Evitar nombres de archivo proporcionados por usuario (renombrar con GUID).

## Cabeceras HTTP sugeridas

Configurar estas cabeceras en IIS o en `web.config`:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: SAMEORIGIN`
- `X-XSS-Protection: 0` (o política CSP moderna)
- `Content-Security-Policy` (según necesidades del front)
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` (según servicios habilitados)

## Transporte seguro

- Forzar HTTPS (redirect HTTP → HTTPS).
- Deshabilitar protocolos obsoletos en el servidor.
- Usar TLS 1.2+.

## Backups y recuperación

- Definir respaldos periódicos de BD y archivos.
- Verificar procedimientos de restauración en QA.

