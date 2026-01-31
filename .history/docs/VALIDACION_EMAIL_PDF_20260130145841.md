# Validación: Correo y PDF Resilientes

## Criterios de Aceptación

### ✅ Correos NO bloquean requests

| Criterio | Cómo validar | Resultado esperado |
|----------|--------------|-------------------|
| Encolado inmediato | Medir tiempo entre crear orden y respuesta HTTP | < 500ms total |
| Sin espera SMTP | El request retorna antes del envío real | Estado `QUEUED-{id}` |
| Procesamiento background | Correo llega después de cerrar página | Email recibido en 1-5 min |

**Prueba manual:**
1. Crear orden de recaudación
2. Verificar que página responde inmediatamente
3. Consultar `SELECT * FROM email_queue WHERE orden_id = X`
4. Verificar estado `PENDIENTE` → `ENVIANDO` → `ENVIADO`

### ✅ Reintentos auditados

| Criterio | Cómo validar | Resultado esperado |
|----------|--------------|-------------------|
| Intentos registrados | Campo `intentos` en `email_queue` | Incrementa con cada retry |
| Error registrado | Campo `ultimo_error` | Mensaje del fallo |
| Backoff exponencial | Campo `proximo_intento` | Incrementa: 1min, 5min, 15min |
| Máximo intentos | Estado final | `ERROR` después de 3 intentos |

**Query de validación:**
```sql
SELECT id, intentos, ultimo_error, proximo_intento, estado
FROM email_queue 
WHERE intentos > 1
ORDER BY fecha_creacion DESC;
```

### ✅ PDF con registro de metadatos

| Criterio | Cómo validar | Resultado esperado |
|----------|--------------|-------------------|
| Registro de generación | Tabla `pdf_generaciones` | Fila por cada PDF |
| Tiempo de generación | `fecha_fin - fecha_inicio` | Registrado |
| Tamaño registrado | Campo `tamano_bytes` | > 0 para exitosos |
| Errores registrados | Campo `error` | Mensaje si falla |
| Intentos registrados | Campo `intentos` | Número de intentos |

**Query de validación:**
```sql
SELECT tipo_documento, numero_referencia, exitoso, 
       tamano_bytes, intentos, error,
       EXTRACT(SECONDS FROM (fecha_fin - fecha_inicio)) as segundos
FROM pdf_generaciones
WHERE fecha_inicio > NOW() - INTERVAL '1 hour';
```

## Escenarios de Prueba

### Escenario 1: Flujo exitoso
1. Crear orden → PDF generado → Email encolado
2. Esperar procesamiento → Email enviado
3. Verificar registros en BD

### Escenario 2: Fallo de SMTP (simular)
1. Configurar servidor SMTP inválido temporalmente
2. Crear orden → Email se encola
3. Verificar reintentos en `email_queue`
4. Restaurar SMTP → Email se envía en siguiente intento

### Escenario 3: Datos inválidos para PDF
1. Intentar generar PDF con orden sin datos requeridos
2. Verificar que se registra en `pdf_generaciones` con `exitoso = false`
3. Verificar mensaje de error almacenado

## Monitoreo en Producción

### Alertas recomendadas:
- Correos en estado `ERROR` > 10 en última hora
- PDFs fallidos > 5% de intentos
- Cola de correos > 100 pendientes

### Dashboard sugerido:
- Correos enviados por hora
- Tasa de éxito de envío
- Tiempo promedio en cola
- PDFs generados por tipo
- Errores recientes
