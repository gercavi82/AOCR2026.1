# Checklist de Seguridad - AOCR

## Fase 3: Seguridad Obligatoria (P0)

### CSRF / AntiForgery
- [ ] `PagoController.Validar` tiene `[ValidateAntiForgeryToken]`
- [ ] `PagoController.Registrar` tiene `[ValidateAntiForgeryToken]`
- [ ] `OrdenRecaudacionController.Nueva (POST)` tiene `[ValidateAntiForgeryToken]`
- [ ] `OrdenRecaudacionController.Editar (POST)` tiene `[ValidateAntiForgeryToken]`
- [ ] `DocumentoController.Subir (POST)` tiene `[ValidateAntiForgeryToken]`
- [ ] Todas las vistas con formularios POST tienen `@Html.AntiForgeryToken()`

### Roles y Autorización
- [ ] `PagoController.Validar` restringido a `[Authorize(Roles = "Financiero,Administrador")]`
- [ ] `PagoController.Rechazar` restringido a `[Authorize(Roles = "Financiero,Administrador")]`
- [ ] `OrdenRecaudacionController.Anular` restringido a `[Authorize(Roles = "Administrador")]`
- [ ] Acciones de consulta permiten roles apropiados

### Validación de Modelos
- [ ] Todos los POST verifican `ModelState.IsValid`
- [ ] ViewModels tienen DataAnnotations apropiadas
- [ ] Salidas en Views usan `@Html.Encode()` o `@` (que codifica por defecto)

### Subida de Archivos
- [ ] `FileUploadValidator` implementado y usado en todos los uploads
- [ ] Tamaño máximo: 5MB
- [ ] Extensiones permitidas: .pdf, .jpg, .jpeg, .png
- [ ] Validación de magic bytes implementada
- [ ] Archivos guardados fuera del webroot (App_Data/Uploads)
- [ ] Nombres renombrados con GUID
- [ ] Metadatos registrados en tabla `ArchivoSubido`
- [ ] Hash SHA256 calculado y almacenado

### Secretos y Configuración
- [ ] Credenciales AS400 en variables de entorno (producción)
- [ ] Credenciales Email en variables de entorno (producción)
- [ ] Connection strings sensibles en variables de entorno (producción)
- [ ] `SecureConfigurationService` usado en lugar de acceso directo a config
- [ ] Transforms de Web.config por ambiente configurados

### Headers de Seguridad
- [ ] `X-Frame-Options: SAMEORIGIN` configurado
- [ ] `X-Content-Type-Options: nosniff` configurado
- [ ] `X-XSS-Protection: 1; mode=block` configurado
- [ ] `Referrer-Policy` configurado
- [ ] `Content-Security-Policy` configurado
- [ ] `Strict-Transport-Security` configurado (producción)
- [ ] Headers que revelan información removidos (X-Powered-By, Server)

### Validación Manual
- [ ] Intento de acceso a `/Pago/Validar` sin rol Financiero = 403
- [ ] Intento de subir archivo .exe = rechazado
- [ ] Intento de subir archivo con extensión falsa = rechazado
- [ ] Intento de path traversal en nombre de archivo = rechazado
- [ ] No hay secretos en código fuente (buscar: password, pwd, secret, key)

## Variables de Entorno Requeridas (Producción)

```bash
# AS400
AOCR_AS400_SERVER=servidor.as400.ejemplo
AOCR_AS400_DATABASE=BIBLIOTECA
AOCR_AS400_USERID=usuario
AOCR_AS400_PASSWORD=********
AOCR_AS400_LIBRARY=LIBPROD

# Email
AOCR_EMAIL_SERVER=smtp.ejemplo.com
AOCR_EMAIL_PORT=587
AOCR_EMAIL_USERNAME=notificaciones@ejemplo.com
AOCR_EMAIL_PASSWORD=********
AOCR_EMAIL_USESSL=true
AOCR_EMAIL_FROM=notificaciones@ejemplo.com
AOCR_EMAIL_FROMNAME=Sistema AOCR

# Connection Strings
AOCR_CONNSTR_POSTGRESQL=Host=servidor;Database=aocr;Username=app;Password=********
```

## Fecha de Revisión
- Revisado por: _________________
- Fecha: _________________
- Ambiente: [ ] DEV [ ] QA [ ] PROD
