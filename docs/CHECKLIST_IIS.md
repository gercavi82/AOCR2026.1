# Checklist de Despliegue IIS - AOCR

## Información del Despliegue

| Campo | Valor |
|-------|-------|
| Fecha | _________________ |
| Versión | _________________ |
| Ambiente | [ ] DEV [ ] QA [ ] PROD |
| Responsable | _________________ |
| Ticket/CR | _________________ |

---

## Pre-Despliegue

### Validación de Build
- [ ] Build Release exitoso (sin errores)
- [ ] Tests unitarios pasan (mínimo 10 tests)
- [ ] Código revisado y aprobado
- [ ] Versión taggeada en Git: `v__________`

### Backup
- [ ] Backup de BD PostgreSQL realizado
- [ ] Backup de carpeta de aplicación actual
- [ ] Backup de Web.config actual
- [ ] Backup verificado y restaurable

### Documentación
- [ ] Release notes preparadas
- [ ] Cambios en BD documentados
- [ ] Variables de entorno documentadas

---

## Configuración del Servidor

### IIS Application Pool
- [ ] .NET CLR Version: `v4.0`
- [ ] Managed Pipeline Mode: `Integrated`
- [ ] Identity: `ApplicationPoolIdentity` o cuenta de servicio
- [ ] Enable 32-bit Applications: `False`
- [ ] Idle Timeout: `20 minutos` (o según requerimiento)
- [ ] Recycling: Configurado para horario de bajo uso

### IIS Site Configuration
- [ ] Physical Path correcto
- [ ] Bindings configurados (HTTP/HTTPS)
- [ ] Certificado SSL válido (PROD)
- [ ] Host headers configurados

### Permisos de Carpetas
- [ ] `App_Data`: Lectura/Escritura para AppPool
- [ ] `Uploads`: Lectura/Escritura para AppPool
- [ ] `Logs`: Lectura/Escritura para AppPool
- [ ] Carpeta raíz: Solo lectura para AppPool

---

## Variables de Entorno (Producción)

### Base de Datos
- [ ] `AOCR_CONNSTR_POSTGRESQL` configurada
- [ ] Conexión probada desde servidor

### AS400
- [ ] `AOCR_AS400_SERVER` configurada
- [ ] `AOCR_AS400_DATABASE` configurada
- [ ] `AOCR_AS400_USERID` configurada
- [ ] `AOCR_AS400_PASSWORD` configurada (rotada)
- [ ] Conexión probada desde servidor

### Email
- [ ] `AOCR_EMAIL_SERVER` configurada
- [ ] `AOCR_EMAIL_PORT` configurada
- [ ] `AOCR_EMAIL_USERNAME` configurada (si aplica)
- [ ] `AOCR_EMAIL_PASSWORD` configurada (si aplica)
- [ ] Envío de prueba exitoso

---

## Despliegue

### Pasos de Despliegue
1. [ ] Notificar usuarios de mantenimiento
2. [ ] Detener Application Pool
3. [ ] Copiar archivos de aplicación
4. [ ] Aplicar Web.config.transform (si aplica)
5. [ ] Ejecutar scripts de BD (si aplica)
6. [ ] Verificar permisos de carpetas
7. [ ] Iniciar Application Pool
8. [ ] Verificar inicio de aplicación

### Verificación Post-Despliegue
- [ ] Página de login carga correctamente
- [ ] Login con usuario de prueba exitoso
- [ ] Crear orden de prueba exitoso
- [ ] PDF de orden se genera correctamente
- [ ] Correo de prueba se envía correctamente
- [ ] Logs se escriben correctamente
- [ ] Sin errores en Event Viewer

---

## Post-Despliegue

### Monitoreo (primeras 2 horas)
- [ ] CPU del servidor normal
- [ ] Memoria del servidor normal
- [ ] Sin errores 500 en logs IIS
- [ ] Sin errores en logs de aplicación
- [ ] Usuarios reportan funcionamiento normal

### Documentación Final
- [ ] Checklist firmado
- [ ] Incidentes documentados (si hubo)
- [ ] Rollback NO fue necesario
- [ ] Release notes publicadas

---

## Rollback (si es necesario)

### Pasos de Rollback
1. [ ] Detener Application Pool
2. [ ] Restaurar carpeta de aplicación desde backup
3. [ ] Restaurar Web.config desde backup
4. [ ] Ejecutar scripts de rollback de BD (si aplica)
5. [ ] Iniciar Application Pool
6. [ ] Verificar funcionamiento
7. [ ] Notificar equipo de desarrollo

---

## Firmas de Aprobación

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Desarrollo | _____________ | _______ | _______ |
| QA | _____________ | _______ | _______ |
| Infraestructura | _____________ | _______ | _______ |
| Líder Técnico | _____________ | _______ | _______ |
