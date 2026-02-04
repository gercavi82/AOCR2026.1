# Análisis de EmailQueueItem - Propiedades del Modelo

## Estado Actual (Febrero 2026)

### Propiedades que EXISTEN en la Base de Datos Real

✅ **En BD y en código:**
| Propiedad C# | Columna DB | Uso |
|--------------|------------|-----|
| `Id` | `id` | Primary key, usado en todas las operaciones |
| `Para` | `to_address` | Email destinatario, REQUERIDO |
| `Asunto` | `subject` | Asunto del email, REQUERIDO |
| `Cuerpo` | `body` | Contenido HTML/texto del email, REQUERIDO |
| `Estado` | `status` | Estados: PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO |
| `FechaCreacion` | `created_at` | Timestamp de creación del registro |
| `ProximoIntento` | `proximo_intento` | Timestamp para reintentos con backoff exponencial |
| `OrdenId` | `solicitud_id` | FK a aocr_tbsolicitud (nota: semánticamente ambiguo) |

### Propiedades que SOLO EXISTEN EN MEMORIA

⚠️ **Usadas en tiempo de ejecución pero NO en la base de datos:**

| Propiedad C# | Uso en Código | Se persiste en DB | Acción Recomendada |
|--------------|---------------|-------------------|-------------------|
| `ParaNombre` | ✅ Usado en `EmailQueueProcessor` línea 341 para envío de email | ❌ NO | **MANTENER** - Necesario para envío de emails |
| `EsHtml` | ✅ Usado en `NotificacionService` y `EmailService` para indicar formato | ❌ NO | **MANTENER** - Necesario para envío de emails |
| `AdjuntoNombre` | ✅ Usado en `NotificacionService` línea 39 para adjuntar PDFs | ❌ NO | **MANTENER** - Necesario para envío de emails |
| `AdjuntoContenido` | ✅ Usado en `EmailQueueProcessor` línea 344 para enviar PDFs | ❌ NO | **MANTENER** - Necesario para envío de emails |
| `AdjuntoMimeType` | ✅ Usado en `NotificacionService` línea 41 (application/pdf) | ❌ NO | **MANTENER** - Necesario para envío de emails |
| `CorrelationId` | ✅ Usado en `NotificacionService` y logs (línea 331) | ❌ NO | **MANTENER** - Necesario para trazabilidad |
| `NumeroOrden` | ✅ Usado en logs de `EmailQueueProcessor` (línea 332) | ❌ NO | **MANTENER** - Necesario para trazabilidad |
| `TipoNotificacion` | ✅ Usado en `NotificacionService` línea 45 | ❌ NO | **MANTENER** - Necesario para categorización |
| `MaxIntentos` | ✅ Usado en `NotificacionService` línea 42 (valor 3) | ❌ NO | **MANTENER** - Usado en lógica de reintentos |

### Propiedades que NO SE USAN (CANDIDATAS PARA ELIMINACIÓN)

❌ **No usadas en el código actual:**

| Propiedad C# | ¿Se lee de DB? | ¿Se asigna en código? | Estado |
|--------------|---------------|-----------------------|--------|
| `Intentos` | ❌ Solo en versiones antiguas (`.history`) | ❌ Solo en código histórico | **ELIMINAR** |
| `UltimoError` | ❌ Solo en versiones antiguas (`.history`) | ❌ NO | **ELIMINAR** |
| `FechaEnvio` | ❌ Solo en versiones antiguas (`.history`) | ⚠️ Usado en `OrdenRecaudacionOrchestrator` pero no se guarda | **EVALUAR** |

## Conclusión y Recomendación

### ✅ NO ELIMINAR LAS SIGUIENTES PROPIEDADES:
Aunque no están en la base de datos, estas propiedades son **esenciales para el flujo de envío de emails**:
- `ParaNombre`, `EsHtml`, `AdjuntoNombre`, `AdjuntoContenido`, `AdjuntoMimeType`
- `CorrelationId`, `NumeroOrden`, `TipoNotificacion`, `MaxIntentos`

### ❌ CONSIDERAR ELIMINAR:
- `Intentos` - No se usa en la versión actual, solo en versiones históricas
- `UltimoError` - No se usa en la versión actual
- `FechaEnvio` - Se asigna pero no se persiste ni se usa posteriormente

### ⚠️ NOTA SOBRE EL DISEÑO ACTUAL:

El patrón actual es **CORRECTO y EFICIENTE**:
1. Se insertan solo los datos mínimos en la base de datos (8 columnas)
2. Las propiedades adicionales (`ParaNombre`, `AdjuntoContenido`, etc.) se pasan **en memoria** al procesador
3. Esto evita almacenar datos binarios (PDFs) en la base de datos
4. Reduce el tamaño de la tabla `email_queue`

## Flujo de Datos

```
NotificacionService.EnviarAsync()
  ↓ Crea EmailQueueItem con TODAS las propiedades (incluye PDF en memoria)
  ↓
EmailQueueService.EncolarAsync() 
  ↓ Inserta solo: to_address, subject, body, status, solicitud_id, created_at, proximo_intento
  ↓
EmailQueueProcessor.ProcessItemAsync()
  ↓ Lee de DB: solo las 8 columnas básicas
  ↓ NO tiene ParaNombre, AdjuntoContenido, etc.
  ↓ **PROBLEMA:** Las propiedades adicionales se pierden
  ↓
EmailService.EnviarAsync()
  ✅ Necesita: ParaNombre, AdjuntoContenido, AdjuntoNombre
  ❌ Pero no las tiene porque no están en la BD
```

## ⚠️ BUG IDENTIFICADO

**El procesador actual NO PUEDE ENVIAR EMAILS CON ADJUNTOS** porque:
1. `NotificacionService` crea `EmailQueueItem` con el PDF en `AdjuntoContenido`
2. `EmailQueueService.EncolarAsync` NO guarda `AdjuntoContenido` en la BD
3. `EmailQueueProcessor` lee de la BD y obtiene un item SIN adjunto
4. `EmailService.EnviarAsync` recibe `AdjuntoContenido = null`

### Posibles Soluciones:

#### Opción 1: Agregar columnas BYTEA a la BD (NO RECOMENDADO)
```sql
ALTER TABLE email_queue ADD COLUMN adjunto_contenido BYTEA;
ALTER TABLE email_queue ADD COLUMN adjunto_nombre VARCHAR(255);
ALTER TABLE email_queue ADD COLUMN adjunto_mime_type VARCHAR(100);
```
❌ **Desventaja:** Almacenar PDFs en la BD aumenta su tamaño significativamente

#### Opción 2: Regenerar PDF en el procesador (RECOMENDADO)
```csharp
private async Task ProcessItemAsync(EmailQueueItem item)
{
    byte[] pdfBytes = null;
    string pdfNombre = null;
    
    if (item.OrdenId.HasValue)
    {
        // Regenerar el PDF desde la orden
        var pdfService = new PdfOrdenService();
        pdfBytes = await pdfService.GenerarPdfOrdenAsync(item.OrdenId.Value);
        pdfNombre = $"Orden_{item.NumeroOrden}.pdf";
    }
    
    await _emailService.EnviarAsync(
        item.Para,
        item.ParaNombre, // Obtener desde aocr_tbsolicitud
        item.Asunto,
        item.Cuerpo,
        pdfBytes,
        pdfNombre);
}
```
✅ **Ventaja:** No almacena binarios en BD, regenera el PDF cuando se necesita

#### Opción 3: Envío inmediato sin cola (ACTUAL - FUNCIONAL)
```csharp
// NotificacionService.cs
// NO usa EmailQueueService, envía directamente
var emailService = new EmailService();
await emailService.EnviarAsync(para, nombre, asunto, cuerpo, pdfBytes, pdfNombre);
```
✅ **Ventaja:** Funciona actualmente, emails con PDF se envían inmediatamente
⚠️ **Desventaja:** No hay reintentos automáticos si falla el envío

## Recomendación Final

### Para el informe de auditoría:
✅ **NO marcar como error** las propiedades que no están en la BD
✅ **Documentar** que es un patrón de diseño intencional
⚠️ **Advertir** que el `EmailQueueProcessor` no puede manejar adjuntos actualmente
✅ **Confirmar** que el envío directo de emails (sin cola) funciona correctamente

### Para el código:
1. **MANTENER** todas las propiedades actuales en `EmailQueueItem`
2. **ELIMINAR** solo: `Intentos`, `UltimoError`, `FechaEnvio`
3. **DOCUMENTAR** el flujo con comentarios XML en la clase
4. **CONSIDERAR** implementar Opción 2 si se necesita envío diferido con adjuntos
