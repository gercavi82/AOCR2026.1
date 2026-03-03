# PLAN DE PRUEBAS - AOCR Enterprise Hardening

## Fase 7: Testing & Validación de Producción

**Fecha:** Enero 2026  
**Versión:** 1.0  
**Alcance:** Circuit Breaker, Health Checks, Idempotencia, Sync Log, Audit Trail, FR3 Retry

---

## 1. PRUEBAS DE CIRCUIT BREAKER (AS400)

### 1.1 Escenario: AS400 no disponible
| # | Caso | Precondición | Acción | Resultado Esperado |
|---|------|-------------|--------|-------------------|
| CB-01 | Circuit Breaker se abre tras 3 fallas | AS400 apagado/inalcanzable | Intentar 3 operaciones FR3 | Estado cambia a `Open`, 4ta llamada retorna `CircuitBreakerOpenException` sin intentar conexión |
| CB-02 | Half-Open tras timeout | CB en Open, esperar 60s | Intentar operación FR3 | Estado cambia a `HalfOpen`, permite 1 intento |
| CB-03 | Recuperación automática | CB en HalfOpen, AS400 recuperado | Operación exitosa | Estado vuelve a `Closed`, failureCount=0 |
| CB-04 | Reset manual desde dashboard | CB en Open | Admin hace POST `/Health/ResetCircuitBreaker?name=AS400` | CB reseteado a `Closed` |
| CB-05 | Operaciones PostgreSQL no afectadas | CB Open para AS400 | Crear orden, registrar pago | Funciona normalmente, solo FR3 bloqueado |

### 1.2 Escenario: AS400 intermitente
| # | Caso | Acción | Resultado Esperado |
|---|------|--------|-------------------|
| CB-06 | 2 fallas + 1 éxito | 2 FR3 fallan, 3ro exitoso | CB permanece `Closed`, failureCount se resetea |
| CB-07 | Concurrencia con CB abierto | 10 requests simultáneos con CB Open | Todos reciben error inmediato, no saturan AS400 |

---

## 2. PRUEBAS DE HEALTH CHECK

### 2.1 Endpoint `/Health/Index`
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| HC-01 | App respondiendo | `{ "status": "healthy" }` con HTTP 200 |
| HC-02 | Sin autenticación | Accesible (AllowAnonymous) |

### 2.2 Endpoint `/Health/Details`
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| HC-03 | Todo correcto | `status: "healthy"`, postgresql OK, disk OK, memory OK |
| HC-04 | PostgreSQL caída | `status: "unhealthy"`, postgresql.healthy=false |

### 2.3 Endpoint `/Health/As400`
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| HC-05 | AS400 accesible | `status: "healthy"`, connection.success=true, tablas verificadas |
| HC-06 | AS400 deshabilitado | `status: "degraded"`, facturacion_enabled=false |
| HC-07 | AS400 inaccesible | `status: "unhealthy"`, connection.success=false, circuit_breaker info |
| HC-08 | Solo Admin/Financiero | Rol Operador → 403 Forbidden |

### 2.4 Dashboard `/Health/Dashboard`
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| HC-09 | Carga inicial | 4 tarjetas con estado, auto-refresh cada 30s |
| HC-10 | Solo Admin | Rol Financiero → redirigido a login |

---

## 3. PRUEBAS DE IDEMPOTENCIA FR3

### 3.1 Doble-click / Duplicados
| # | Caso | Acción | Resultado Esperado |
|---|------|--------|-------------------|
| ID-01 | Doble submit | Enviar 2 veces "Aprobar Pago" rápidamente | 2do intento detecta clave idempotente, retorna resultado anterior |
| ID-02 | Retry tras error transitorio | FR3 falla por timeout, reintento manual | Si la clave fue liberada → reintenta; si ya completada → retorna resultado |
| ID-03 | Hash SHA256 consistente | Mismos datos (ordenId, pagoId, total) | Genera misma clave idempotente |
| ID-04 | Claves diferentes por monto | OrdenId=1 con total=100 vs total=200 | Claves diferentes, ambas se procesan |
| ID-05 | Expiración 24h | Operación completada hace >24h | Clave expirada, permite re-procesamiento |

### 3.2 Concurrencia
| # | Caso | Acción | Resultado Esperado |
|---|------|--------|-------------------|
| ID-06 | TryAcquire concurrente | 2 threads con misma clave al mismo tiempo | Solo 1 adquiere, otro recibe false |
| ID-07 | Fail-open | Error de BD en IdempotencyService | Permite la operación (no bloquea por falla propia) |

---

## 4. PRUEBAS DE SYNC LOG

### 4.1 Registro de operaciones
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| SL-01 | FR3 exitoso | aocr_sync_log con estado=COMPLETADO, fr3_numero, duración |
| SL-02 | FR3 error | aocr_sync_log con estado=ERROR, error_mensaje, reintentar=true |
| SL-03 | Error en SyncLog | Operación FR3 NO se ve afectada (fire-and-forget) |
| SL-04 | Estadísticas endpoint | `/Health/SyncStats` muestra totales correctos |

### 4.2 Vista de resumen
```sql
SELECT * FROM v_aocr_sync_resumen;
-- Debe mostrar: día, total, completados, errores, reintentando, duración promedio
```

---

## 5. PRUEBAS DE AUDIT TRAIL

### 5.1 Registros automáticos
| # | Caso | Evento Esperado en aocr_audit_trail |
|---|------|--------------------------------------|
| AT-01 | FR3 generado | accion=FR3_GENERADO, tabla=aocr_or_orden, campo=fr3_numero |
| AT-02 | FR3 error | accion=FR3_ERROR, tabla=aocr_or_orden, campo=fr3_estado |
| AT-03 | Pago registrado | accion=PAGO_REGISTRADO, campo=monto_pagado |
| AT-04 | Error en AuditTrail | No afecta flujo principal (silenced exceptions) |

### 5.2 Consulta historial
```sql
SELECT * FROM aocr_audit_trail 
WHERE tabla = 'aocr_or_orden' AND registro_id = '123'
ORDER BY fecha DESC;
```

---

## 6. PRUEBAS DE FR3 RETRY QUEUE

### 6.1 Encolamiento automático
| # | Caso | Resultado Esperado |
|---|------|-------------------|
| RQ-01 | FR3 falla por timeout | Registro en aocr_fr3_retry_queue con intentos=0, estado=PENDIENTE |
| RQ-02 | No duplicar en cola | Si ya existe entrada PENDIENTE para ordenId, no crea otra |
| RQ-03 | Backoff exponencial | intento 1: 5min, 2: 10min, 3: 20min, ..., max: 240min |

### 6.2 Procesamiento de reintentos
| # | Caso | Acción | Resultado Esperado |
|---|------|--------|-------------------|
| RQ-04 | Reintento exitoso | Admin pulsa "Procesar" en dashboard | Estado → COMPLETADO, FR3 generado |
| RQ-05 | Reintento fallido | AS400 sigue caído | intentos++, proximo_intento recalculado |
| RQ-06 | Max reintentos agotados | 10 intentos fallidos | Estado → FALLIDO, no más reintentos |
| RQ-07 | FOR UPDATE SKIP LOCKED | 2 workers procesan cola | Cada item procesado por solo 1 worker |
| RQ-08 | Cancelar reintento | Admin cancela orden FR3 | Estado → CANCELADO |

---

## 7. PRUEBAS DE INTEGRACIÓN END-TO-END

### 7.1 Flujo completo (happy path)
```
1. Operador crea Orden de Recaudación → estado BORRADOR
2. Solicitante registra Pago → estado PAGADA
3. Financiero aprueba Pago → FR3 generado en AS400
   ✓ Idempotency key creada
   ✓ SyncLog: COMPLETADO
   ✓ AuditTrail: FR3_GENERADO
4. Dashboard muestra estadísticas actualizadas
```

### 7.2 Flujo con error y recovery
```
1. Financiero aprueba Pago → FR3 falla por AS400 down
   ✓ Circuit Breaker registra falla
   ✓ SyncLog: ERROR con reintentar=true
   ✓ Fr3 Retry Queue: PENDIENTE con backoff
   ✓ Idempotency key liberada
   ✓ AuditTrail: FR3_ERROR
2. AS400 se recupera
3. Admin procesa reintentos desde Dashboard
   ✓ FR3 generado exitosamente
   ✓ SyncLog nuevo: COMPLETADO
   ✓ Idempotency key nueva completada
   ✓ Retry queue: COMPLETADO
```

### 7.3 Flujo con doble-click
```
1. Financiero hace doble-click en "Aprobar"
2. Request 1: TryAcquire → exitoso → genera FR3
3. Request 2: TryAcquire → false (ya adquirida)
   ✓ Retorna "Operación en proceso"
   ✓ No genera FR3 duplicado
```

---

## 8. PRUEBAS DE RESILIENCIA

| # | Escenario | Componente que falla | Comportamiento |
|---|-----------|---------------------|----------------|
| RE-01 | PostgreSQL down | SyncLogService | FR3 prosigue, sync log se pierde |
| RE-02 | PostgreSQL down | IdempotencyService | Permite operación (fail-open) |
| RE-03 | PostgreSQL down | AuditTrailService | FR3 prosigue, audit se pierde |
| RE-04 | AS400 timeout | FacturacionAS400DAO | Error capturado, CB cuenta falla |
| RE-05 | AS400 down prolongado | CircuitBreaker | Abre tras 3 fallas, protege sistema |
| RE-06 | ODBC driver no instalado | AS400BaseDAO | Error claro en health check |
| RE-07 | Tablas FR3 no existen | AS400HealthCheck | Reporte específico de tabla faltante |

---

## 9. PRUEBAS DE SEGURIDAD

| # | Caso | Resultado Esperado |
|---|------|-------------------|
| SE-01 | `/Health/As400` sin auth | Redirect a login |
| SE-02 | `/Health/CircuitBreakers` con rol Financiero | 403 (solo Admin) |
| SE-03 | POST ResetCircuitBreaker sin token | 400 (AntiForgeryToken) |
| SE-04 | POST ProcessFr3Retries con rol Operador | 403 |
| SE-05 | Credenciales AS400 en logs | No deben aparecer password/userId |

---

## 10. PRUEBAS DE RENDIMIENTO

| # | Caso | Métrica Esperada |
|---|------|-----------------|
| PE-01 | Health check básico | < 50ms |
| PE-02 | Health check detallado | < 500ms |
| PE-03 | AS400 health check | < 5000ms (incluye connection test) |
| PE-04 | CB Open → respuesta inmediata | < 5ms (no intenta conexión) |
| PE-05 | SyncLog no bloquea operación | < 10ms overhead |

---

## 11. SQL MIGRATION CHECKLIST

Antes de ejecutar en producción:

```bash
# 1. Backup de la base
pg_dump -h 172.20.16.55 -U root dgac_des > backup_pre_migration.sql

# 2. Ejecutar migration
psql -h 172.20.16.55 -U root -d dgac_des -f scripts/20260601_sync_audit_idempotency.sql

# 3. Verificar tablas creadas
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' AND table_name LIKE 'aocr_%'
ORDER BY table_name;

# 4. Verificar columnas añadidas a aocr_or_orden
SELECT column_name, data_type FROM information_schema.columns
WHERE table_name = 'aocr_or_orden' AND column_name IN ('fr3_estado','fr3_numero','fr3_error','idempotency_key');

# 5. Verificar función de limpieza
SELECT * FROM aocr_limpiar_datos_expirados();

# 6. Verificar vistas
SELECT * FROM v_aocr_sync_resumen LIMIT 1;
SELECT * FROM v_aocr_fr3_pendientes LIMIT 1;
```

---

## 12. CHECKLIST DE DEPLOY

- [ ] SQL migration ejecutada sin errores
- [ ] `AS400:Facturacion:Enabled` en `false` para deploy inicial
- [ ] Health check básico retorna 200
- [ ] Health check detallado muestra PostgreSQL OK
- [ ] Dashboard de salud accesible para Admin
- [ ] Circuit breaker aparece en dashboard
- [ ] Habilitar `AS400:Facturacion:Enabled = true` gradualmente
- [ ] Verificar FR3 con orden de prueba
- [ ] Verificar idempotencia con doble-click
- [ ] Verificar sync log en base de datos
- [ ] Verificar audit trail
- [ ] Monitorear circuit breaker por 24h
- [ ] Programar limpieza periódica: `SELECT aocr_limpiar_datos_expirados();`

---

## 13. ARCHIVOS CREADOS / MODIFICADOS

### Nuevos archivos:
| Archivo | Capa | Propósito |
|---------|------|-----------|
| `CapaDatos/Infrastructure/CircuitBreaker.cs` | Infrastructure | Patrón Circuit Breaker con Registry |
| `CapaDatos/Infrastructure/AS400HealthCheck.cs` | Infrastructure | Health check estructurado AS400 |
| `CapaDatos/Services/SyncLogService.cs` | Data Services | Logging de sincronización PG↔AS400 |
| `CapaDatos/Services/IdempotencyService.cs` | Data Services | Prevención de duplicados |
| `CapaDatos/Services/AuditTrailService.cs` | Data Services | Audit trail completo |
| `CapaNegocio/Services/Fr3RetryService.cs` | Business Services | Cola de reintentos FR3 |
| `Views/Health/Dashboard.cshtml` | UI | Dashboard visual de salud |
| `scripts/20260601_sync_audit_idempotency.sql` | SQL | Migration para tablas enterprise |

### Archivos modificados:
| Archivo | Cambio |
|---------|--------|
| `CapaDatos/Infrastructure/AS400BaseDAO.cs` | Circuit breaker integrado en ExecuteWithConnection |
| `CapaNegocio/Services/FacturacionAS400Service.cs` | SyncLog + Idempotency + AuditTrail integrados |
| `CapaPresentacion/Controllers/HealthController.cs` | Endpoints: As400, CircuitBreakers, SyncStats, ProcessFr3Retries, Dashboard |
| `CapaDatos/CapaDatos.csproj` | Registradas 5 nuevas clases |
| `CapaNegocio/CapaNegocio.csproj` | Registrada Fr3RetryService |
