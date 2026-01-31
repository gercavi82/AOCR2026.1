# Checklist Final - Listo para Producción

## Información del Release

| Campo | Valor |
|-------|-------|
| **Versión** | _________________ |
| **Fecha Planificada** | _________________ |
| **Responsable Release** | _________________ |
| **Ticket/CR** | _________________ |

---

## 1. SEGURIDAD

### 1.1 Autenticación y Autorización
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 1.1.1 | Endpoints críticos con `[Authorize]` | [ ] | _______ | _______ |
| 1.1.2 | Roles correctos en acciones financieras (`Financiero/Admin`) | [ ] | _______ | _______ |
| 1.1.3 | Acciones de admin restringidas a `Administrador` | [ ] | _______ | _______ |

**Verificación:**
```
GET /Pago/Validar sin login → 401
GET /Pago/Validar con rol Solicitante → 403
GET /OrdenRecaudacion/Anular con rol Financiero → 403
```

### 1.2 CSRF
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 1.2.1 | `[ValidateAntiForgeryToken]` en todos los POST sensibles | [ ] | _______ | _______ |
| 1.2.2 | `@Html.AntiForgeryToken()` en todos los formularios | [ ] | _______ | _______ |

**Controllers a verificar:**
- [ ] `OrdenRecaudacionController`: Nueva, Editar, Anular
- [ ] `PagoController`: Registrar, Validar, Rechazar
- [ ] `DocumentoController`: Subir, Eliminar
- [ ] `UsuarioController`: Crear, Editar, CambiarPassword

### 1.3 Secrets
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 1.3.1 | Credenciales AS400 en variables de entorno | [ ] | _______ | _______ |
| 1.3.2 | Credenciales Email en variables de entorno | [ ] | _______ | _______ |
| 1.3.3 | Connection strings sensibles externalizados | [ ] | _______ | _______ |
| 1.3.4 | Claves rotadas desde último deploy | [ ] | _______ | _______ |
| 1.3.5 | No hay secrets en código fuente (verificar con grep) | [ ] | _______ | _______ |

**Comando de verificación:**
```bash
grep -ri "password\|pwd\|secret\|apikey" --include="*.cs" --include="*.config" .
```

### 1.4 Upload Seguro
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 1.4.1 | Validación de extensiones permitidas (.pdf, .jpg, .png) | [ ] | _______ | _______ |
| 1.4.2 | Validación de MIME type | [ ] | _______ | _______ |
| 1.4.3 | Validación de magic bytes | [ ] | _______ | _______ |
| 1.4.4 | Cálculo y almacenamiento de hash SHA256 | [ ] | _______ | _______ |
| 1.4.5 | Archivos guardados fuera de webroot (App_Data) | [ ] | _______ | _______ |
| 1.4.6 | Renombrado con GUID | [ ] | _______ | _______ |
| 1.4.7 | Tamaño máximo configurado (5MB) | [ ] | _______ | _______ |
| 1.4.8 | Metadatos en tabla `archivos_subidos` | [ ] | _______ | _______ |

**Prueba de penetración básica:**
- [ ] Subir archivo `.exe` renombrado a `.pdf` → Rechazado
- [ ] Subir archivo > 5MB → Rechazado
- [ ] Path traversal `../../etc/passwd.pdf` → Sanitizado

### 1.5 Descargas Seguras
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 1.5.1 | Verificación de ownership antes de descarga | [ ] | _______ | _______ |
| 1.5.2 | Verificación de rol apropiado | [ ] | _______ | _______ |
| 1.5.3 | IDs no predecibles o validados | [ ] | _______ | _______ |

---

## 2. INTEGRIDAD DE DATOS

### 2.1 Transacciones
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 2.1.1 | Crear orden + detalles en transacción | [ ] | _______ | _______ |
| 2.1.2 | Registrar pago + actualizar estado en transacción | [ ] | _______ | _______ |
| 2.1.3 | Validar pago + actualizar orden en transacción | [ ] | _______ | _______ |
| 2.1.4 | Rollback funciona en caso de error parcial | [ ] | _______ | _______ |

**Prueba de rollback:**
```sql
-- Simular fallo después de crear orden pero antes de detalles
-- Verificar que no queda orden huérfana
SELECT * FROM ordenes_recaudacion WHERE id NOT IN (SELECT DISTINCT orden_id FROM detalles_orden);
```

### 2.2 SQL Parametrizado
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 2.2.1 | `OrdenRecaudacionDAO` - todas las queries | [ ] | _______ | _______ |
| 2.2.2 | `PagoDAO` - todas las queries | [ ] | _______ | _______ |
| 2.2.3 | `ConceptoDAO` - todas las queries | [ ] | _______ | _______ |
| 2.2.4 | `ContribuyenteDAO` - todas las queries | [ ] | _______ | _______ |
| 2.2.5 | `EmpresaAS400DAO` - todas las queries | [ ] | _______ | _______ |
| 2.2.6 | Sin concatenación de strings en SQL | [ ] | _______ | _______ |

**Comando de verificación:**
```bash
grep -ri "\" + \|' + \|string.Format.*SELECT\|string.Format.*INSERT\|string.Format.*UPDATE" --include="*.cs" .
```

### 2.3 Conexiones
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 2.3.1 | Todas las conexiones con `using` | [ ] | _______ | _______ |
| 2.3.2 | Timeout de comando: 30s (PG), 60s (AS400) | [ ] | _______ | _______ |
| 2.3.3 | Pooling configurado (Min=5, Max=100) | [ ] | _______ | _______ |
| 2.3.4 | Errores no filtran detalles internos | [ ] | _______ | _______ |

---

## 3. MANEJO DE ERRORES

### 3.1 Custom Errors
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 3.1.1 | `customErrors mode="RemoteOnly"` en Web.config | [ ] | _______ | _______ |
| 3.1.2 | Página de error 404 personalizada | [ ] | _______ | _______ |
| 3.1.3 | Página de error 500 personalizada | [ ] | _______ | _______ |
| 3.1.4 | Página de error 403 personalizada | [ ] | _______ | _______ |
| 3.1.5 | No se muestra stacktrace en producción | [ ] | _______ | _______ |
| 3.1.6 | Correlation ID visible para usuario | [ ] | _______ | _______ |

**Prueba:**
```
Forzar error 500 → Verificar que NO muestra stacktrace
Verificar que muestra "Código de referencia: XXXX"
```

### 3.2 Headers de Seguridad
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 3.2.1 | `X-Frame-Options: SAMEORIGIN` | [ ] | _______ | _______ |
| 3.2.2 | `X-Content-Type-Options: nosniff` | [ ] | _______ | _______ |
| 3.2.3 | `X-XSS-Protection: 1; mode=block` | [ ] | _______ | _______ |
| 3.2.4 | `Referrer-Policy` configurado | [ ] | _______ | _______ |
| 3.2.5 | `Content-Security-Policy` configurado | [ ] | _______ | _______ |
| 3.2.6 | `Strict-Transport-Security` (HSTS) activo | [ ] | _______ | _______ |
| 3.2.7 | `X-Powered-By` removido | [ ] | _______ | _______ |
| 3.2.8 | `Server` header removido | [ ] | _______ | _______ |

**Verificación con curl:**
```bash
curl -I https://aocr.aviacioncivil.gob.ec/
```

---

## 4. OBSERVABILIDAD

### 4.1 Logging
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 4.1.1 | Logging estructurado implementado | [ ] | _______ | _______ |
| 4.1.2 | Correlation ID en todos los logs | [ ] | _______ | _______ |
| 4.1.3 | NumeroOrden/CodigoSolicitud correlacionados | [ ] | _______ | _______ |
| 4.1.4 | Logs escriben a archivo (App_Data/Logs) | [ ] | _______ | _______ |
| 4.1.5 | Rotación de logs configurada | [ ] | _______ | _______ |
| 4.1.6 | No se loguean datos sensibles (passwords, etc.) | [ ] | _______ | _______ |

**Verificación:**
```bash
# Buscar un request específico por correlation ID
grep "CID:abc123" App_Data/Logs/AOCR_*.log
```

### 4.2 Auditoría
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 4.2.1 | Cambios de estado de orden auditados | [ ] | _______ | _______ |
| 4.2.2 | Cambios de estado de pago auditados | [ ] | _______ | _______ |
| 4.2.3 | Usuario y timestamp registrados | [ ] | _______ | _______ |
| 4.2.4 | IP de origen registrada | [ ] | _______ | _______ |
| 4.2.5 | Tabla `audit_cambios_estado` poblándose | [ ] | _______ | _______ |

**Query de verificación:**
```sql
SELECT * FROM audit_cambios_estado 
WHERE fecha_cambio > NOW() - INTERVAL '1 hour'
ORDER BY fecha_cambio DESC;
```

---

## 5. RESILIENCIA

### 5.1 Correo
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 5.1.1 | Cola de correos implementada | [ ] | _______ | _______ |
| 5.1.2 | Reintentos configurados (3 intentos) | [ ] | _______ | _______ |
| 5.1.3 | Backoff exponencial (1m, 5m, 15m) | [ ] | _______ | _______ |
| 5.1.4 | Correos NO bloquean requests | [ ] | _______ | _______ |
| 5.1.5 | Errores de SMTP auditados | [ ] | _______ | _______ |
| 5.1.6 | Procesador de cola arranca con aplicación | [ ] | _______ | _______ |

**Prueba:**
```sql
-- Verificar cola
SELECT estado, COUNT(*), AVG(intentos) FROM email_queue GROUP BY estado;
```

### 5.2 PDF
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 5.2.1 | Validación de datos antes de generar | [ ] | _______ | _______ |
| 5.2.2 | Reintentos en caso de fallo (2 intentos) | [ ] | _______ | _______ |
| 5.2.3 | Registro de generaciones en BD | [ ] | _______ | _______ |
| 5.2.4 | Tiempo y tamaño registrados | [ ] | _______ | _______ |
| 5.2.5 | Errores registrados para diagnóstico | [ ] | _______ | _______ |

**Query de verificación:**
```sql
SELECT tipo_documento, exitoso, COUNT(*), AVG(tamano_bytes)
FROM pdf_generaciones 
WHERE fecha_inicio > NOW() - INTERVAL '1 day'
GROUP BY tipo_documento, exitoso;
```

---

## 6. BACKUPS Y RECUPERACIÓN

### 6.1 Backups
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 6.1.1 | Backup PostgreSQL automatizado (diario) | [ ] | _______ | _______ |
| 6.1.2 | Backup DB2/AS400 procedimiento documentado | [ ] | _______ | _______ |
| 6.1.3 | Retención de backups definida (30 días) | [ ] | _______ | _______ |
| 6.1.4 | Backups almacenados en ubicación segura | [ ] | _______ | _______ |

### 6.2 Restore
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 6.2.1 | Procedimiento de restore PostgreSQL probado | [ ] | _______ | _______ |
| 6.2.2 | Tiempo de restore conocido (RTO) | [ ] | _______ | _______ |
| 6.2.3 | Punto de recuperación conocido (RPO) | [ ] | _______ | _______ |

**Última prueba de restore:**
- Fecha: _________________
- Tiempo: _______ minutos
- Resultado: [ ] Exitoso [ ] Fallido

---

## 7. MONITOREO

### 7.1 Health Check
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 7.1.1 | Endpoint `/health` implementado | [ ] | _______ | _______ |
| 7.1.2 | Verifica conexión PostgreSQL | [ ] | _______ | _______ |
| 7.1.3 | Verifica conexión AS400 (opcional) | [ ] | _______ | _______ |
| 7.1.4 | Verifica escritura en disco | [ ] | _______ | _______ |

### 7.2 Alertas
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 7.2.1 | Alerta por errores 500 > 10/hora | [ ] | _______ | _______ |
| 7.2.2 | Alerta por cola email > 100 pendientes | [ ] | _______ | _______ |
| 7.2.3 | Alerta por CPU > 90% sostenido | [ ] | _______ | _______ |
| 7.2.4 | Alerta por disco > 85% | [ ] | _______ | _______ |
| 7.2.5 | Contactos de alerta configurados | [ ] | _______ | _______ |

---

## 8. DESPLIEGUE

### 8.1 Rollback
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 8.1.1 | Plan de rollback documentado | [ ] | _______ | _______ |
| 8.1.2 | Scripts de rollback de BD preparados | [ ] | _______ | _______ |
| 8.1.3 | Backup pre-deploy obligatorio | [ ] | _______ | _______ |
| 8.1.4 | Tiempo estimado de rollback: ______ min | [ ] | _______ | _______ |

### 8.2 CI/CD
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 8.2.1 | Pipeline configurado (restore/build/test) | [ ] | _______ | _______ |
| 8.2.2 | Artefactos se generan correctamente | [ ] | _______ | _______ |
| 8.2.3 | Deploy a DEV automatizado | [ ] | _______ | _______ |
| 8.2.4 | Deploy a QA con aprobación | [ ] | _______ | _______ |
| 8.2.5 | Deploy a PROD manual/controlado | [ ] | _______ | _______ |

### 8.3 Tests
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 8.3.1 | Tests unitarios pasan (mínimo 10) | [ ] | _______ | _______ |
| 8.3.2 | Test de flujo completo pasa | [ ] | _______ | _______ |
| 8.3.3 | Tests de seguridad pasan | [ ] | _______ | _______ |
| 8.3.4 | Cobertura de código conocida: ____% | [ ] | _______ | _______ |

---

## 9. DOCUMENTACIÓN

### 9.1 Documentos Requeridos
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 9.1.1 | README.md actualizado | [ ] | _______ | _______ |
| 9.1.2 | Guía de despliegue IIS (CHECKLIST_IIS.md) | [ ] | _______ | _______ |
| 9.1.3 | Variables por ambiente documentadas | [ ] | _______ | _______ |
| 9.1.4 | Arquitectura de datos documentada | [ ] | _______ | _______ |
| 9.1.5 | Procedimiento de rollback documentado | [ ] | _______ | _______ |

### 9.2 Dependencias
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 9.2.1 | Auditoría de paquetes NuGet completada | [ ] | _______ | _______ |
| 9.2.2 | Sin vulnerabilidades críticas conocidas | [ ] | _______ | _______ |
| 9.2.3 | Versiones de paquetes documentadas | [ ] | _______ | _______ |

---

## 10. VALIDACIÓN QA

### 10.1 Pruebas End-to-End
| # | Item | Estado | Verificado por | Fecha |
|---|------|--------|----------------|-------|
| 10.1.1 | Crear orden completa | [ ] | _______ | _______ |
| 10.1.2 | Subir comprobante de pago | [ ] | _______ | _______ |
| 10.1.3 | Validar pago (aprobar) | [ ] | _______ | _______ |
| 10.1.4 | Validar pago (rechazar) | [ ] | _______ | _______ |
| 10.1.5 | Generar PDF de orden | [ ] | _______ | _______ |
| 10.1.6 | Recibir correo de notificación | [ ] | _______ | _______ |
| 10.1.7 | Consultar historial/auditoría | [ ] | _______ | _______ |
| 10.1.8 | Exportar a Excel | [ ] | _______ | _______ |

### 10.2 Ambiente de Prueba
| Campo | Valor |
|-------|-------|
| Ambiente QA URL | _________________ |
| Fecha de pruebas | _________________ |
| Datos de prueba | _________________ |
| Tester | _________________ |

---

## FIRMAS DE APROBACIÓN

### Aprobación Técnica
| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Líder Desarrollo | _____________ | _______ | _______ |
| DBA | _____________ | _______ | _______ |
| Seguridad | _____________ | _______ | _______ |

### Aprobación Operativa
| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| QA Lead | _____________ | _______ | _______ |
| Infraestructura | _____________ | _______ | _______ |
| Product Owner | _____________ | _______ | _______ |

### Aprobación Final
| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| **Gerente de Proyecto** | _____________ | _______ | _______ |

---

## RESUMEN EJECUTIVO

| Categoría | Items | Completados | Pendientes |
|-----------|-------|-------------|------------|
| Seguridad | 20 | ___ | ___ |
| Integridad | 15 | ___ | ___ |
| Errores | 14 | ___ | ___ |
| Observabilidad | 11 | ___ | ___ |
| Resiliencia | 11 | ___ | ___ |
| Backups | 6 | ___ | ___ |
| Monitoreo | 9 | ___ | ___ |
| Despliegue | 12 | ___ | ___ |
| Documentación | 8 | ___ | ___ |
| QA | 10 | ___ | ___ |
| **TOTAL** | **116** | ___ | ___ |

### Estado Final
- [ ] **APROBADO PARA PRODUCCIÓN**
- [ ] **APROBADO CON OBSERVACIONES** (listar abajo)
- [ ] **NO APROBADO** (requiere correcciones)

### Observaciones
```
_____________________________________________________________________________
_____________________________________________________________________________
_____________________________________________________________________________
```

---

*Documento generado: {fecha}*
*Versión del checklist: 1.0*
