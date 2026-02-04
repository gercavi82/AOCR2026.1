# AUDITORÍA DE VÍNCULO CÓDIGO ↔ BASE DE DATOS
## Proyecto AOCR (ASP.NET MVC + Dapper + PostgreSQL)
**Fecha de Auditoría:** 3 de febrero de 2026  
**Auditor:** Sistema de Auditoría Automatizada

---

# 1) RESUMEN EJECUTIVO

| # | FALLA CRÍTICA |
|---|---------------|
| 1 | **ERROR CRÍTICO:** `EmailQueueService.cs` usa columnas `to_address`, `subject`, `body`, `status`, `created_at` pero la tabla `email_queue` tiene columnas `para`, `asunto`, `cuerpo`, `estado`, `fecha_creacion` según los scripts de creación |
| 2 | **ERROR CRÍTICO:** Discrepancia de scripts SQL: existen 3 definiciones diferentes de `email_queue` con columnas inconsistentes (`para` vs `to_address`) |
| 3 | **RIESGO ALTO:** `OrdenRecaudacion.CodigoUsuario` es `string` en C# pero la columna `codigo_usuario` en `aocr_or_orden` es `INTEGER` - el DAO hace conversión manual que puede fallar |
| 4 | **ERROR CRÍTICO:** `EmailQueueItem.OrdenId` se mapea a `solicitud_id` en el INSERT pero el SELECT espera `solicitud_id` - inconsistencia conceptual |
| 5 | **RIESGO MEDIO:** Los valores de estado en `email_queue` varían: scripts usan `'Pendiente'` (capitalizado) vs código usa `'PENDIENTE'` (mayúsculas) |
| 6 | **ERROR:** `OrdenRecaudacionModel.CodigoUsuario` es `int` pero `OrdenRecaudacion` (entidad) lo define como `string` - incompatibilidad de tipos |
| 7 | **RIESGO:** Falta columna `ultimo_error` en algunos scripts de `email_queue` pero el modelo C# `EmailQueueItem.UltimoError` la espera |
| 8 | **ERROR:** El script `fix_email_queue_table.sql` línea 44 tiene sintaxis SQL inválida: `table_name = 'email_queue', 'solicitud_id'` |
| 9 | **RIESGO:** No existe FK declarada entre `email_queue.orden_id` y `aocr_or_orden.id` en scripts base (solo en `add_email_queue_foreign_keys.sql`) |
| 10 | **RIESGO:** Columna `proximo_intento` falta en `create_email_queue_table.sql` pero sí está en `email_pdf_tables.sql` |

---

# 2) MATRIZ DE VÍNCULO CÓDIGO ↔ DB

## Tabla: `email_queue`

| Columna DB (script `email_pdf_tables.sql`) | Tipo DB | Propiedad C# (`EmailQueueItem`) | Tipo C# | Columna usada en SQL del DAO | Estado | Corrección |
|---|---|---|---|---|---|---|
| `id` | SERIAL | `Id` | int | `id` | ✅ OK | - |
| `para` | VARCHAR(255) | `Para` | string | `to_address` | ❌ **ERROR CRÍTICO** | Cambiar SQL a `para` o migrar columna a `to_address` |
| `para_nombre` | VARCHAR(255) | `ParaNombre` | string | NO SE INSERTA | ⚠️ Riesgo | Agregar al INSERT o quitar del modelo |
| `asunto` | VARCHAR(500) | `Asunto` | string | `subject` | ❌ **ERROR CRÍTICO** | Cambiar SQL a `asunto` |
| `cuerpo` | TEXT | `Cuerpo` | string | `body` | ❌ **ERROR CRÍTICO** | Cambiar SQL a `cuerpo` |
| `es_html` | BOOLEAN | `EsHtml` | bool | NO SE INSERTA | ⚠️ Riesgo | Agregar al INSERT |
| `adjunto_nombre` | VARCHAR(255) | `AdjuntoNombre` | string | NO SE USA | ⚠️ Riesgo | - |
| `adjunto_contenido` | BYTEA | `AdjuntoContenido` | byte[] | NO SE USA | ⚠️ Riesgo | - |
| `adjunto_mime_type` | VARCHAR(100) | `AdjuntoMimeType` | string | NO SE USA | ⚠️ Riesgo | - |
| `estado` | VARCHAR(20) | `Estado` | string | `status` | ❌ **ERROR CRÍTICO** | Cambiar SQL a `estado` |
| `intentos` | INTEGER | `Intentos` | int | NO SE USA | ⚠️ Riesgo | - |
| `max_intentos` | INTEGER | `MaxIntentos` | int | NO SE USA | ⚠️ Riesgo | - |
| `ultimo_error` | TEXT | `UltimoError` | string | NO SE USA | ⚠️ Riesgo | - |
| `fecha_creacion` | TIMESTAMP | `FechaCreacion` | DateTime | `created_at` | ❌ **ERROR CRÍTICO** | Cambiar SQL a `fecha_creacion` |
| `fecha_envio` | TIMESTAMP | `FechaEnvio` | DateTime? | NO SE USA | ⚠️ Riesgo | - |
| `proximo_intento` | TIMESTAMP | `ProximoIntento` | DateTime? | `proximo_intento` | ✅ OK | - |
| `correlation_id` | VARCHAR(50) | `CorrelationId` | string | NO SE USA | ⚠️ Riesgo | - |
| `numero_orden` | VARCHAR(50) | `NumeroOrden` | string | NO SE USA | ⚠️ Riesgo | - |
| `orden_id` | INTEGER | `OrdenId` | int? | `solicitud_id` | ⚠️ Ambiguo | Verificar si `orden_id` = `solicitud_id` |
| `tipo_notificacion` | VARCHAR(50) | `TipoNotificacion` | string | NO SE USA | ⚠️ Riesgo | - |

### Evidencia del ERROR CRÍTICO en `EmailQueueService.cs` (líneas 82-100):
```csharp
// EmailQueueService.cs líneas 82-100
const string sql = @"
    INSERT INTO email_queue (
        to_address, subject, body, status,     // ← COLUMNAS INCORRECTAS
        solicitud_id, created_at, proximo_intento
    ) VALUES (
        @to_address, @subject, @body, @status,
        @solicitud_id, @created_at, @proximo_intento
    ) RETURNING id";
```

**Pero la tabla según `scripts/sql/email_pdf_tables.sql` (líneas 4-25) define:**
```sql
CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    para VARCHAR(255) NOT NULL,      -- ← NO es "to_address"
    para_nombre VARCHAR(255),
    asunto VARCHAR(500) NOT NULL,    -- ← NO es "subject"
    cuerpo TEXT NOT NULL,            -- ← NO es "body"
    ...
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',  -- ← NO es "status"
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, -- ← NO es "created_at"
```

---

## Tabla: `aocr_or_orden`

| Columna DB | Tipo DB | Propiedad C# (`OrdenRecaudacion` Entidad) | Tipo C# | Evidencia SQL en DAO | Estado | Corrección |
|---|---|---|---|---|---|---|
| `id` | SERIAL | `Id` | int | `id` ✓ | ✅ OK | - |
| `codigo_usuario` | INTEGER | `CodigoUsuario` | string | Conversión int→string en MapearOrden línea 606 | ⚠️ Riesgo | Usar int en entidad o mantener conversión |
| `codigo_solicitud` | INTEGER/VARCHAR | `CodigoSolicitud` | string | Conversión líneas 290-298 | ⚠️ Riesgo | Definir tipo único en DB |
| `numero_orden` | VARCHAR(50) | `NumeroOrden` | string | `numero_orden` ✓ | ✅ OK | - |
| `fecha_creacion` | TIMESTAMP | `FechaCreacion` | DateTime | `fecha_creacion` ✓ | ✅ OK | - |
| `estado` | VARCHAR(50) | `Estado` | string | `estado` ✓ | ✅ OK | - |
| `observacion` | VARCHAR(500) | `Observacion` | string | `observacion` ✓ | ✅ OK | - |
| `subtotal` | DECIMAL(18,2) | `Subtotal` | decimal? | `subtotal` ✓ | ✅ OK | - |
| `admin` | DECIMAL(18,2) | `Admin` | decimal? | `admin` ✓ | ✅ OK | - |
| `total` | DECIMAL(18,2) | `Total` | decimal? | `total` ✓ | ✅ OK | - |
| `lugar_emision` | VARCHAR(200) | `LugarEmision` | string | `lugar_emision` ✓ | ✅ OK | - |
| `compania` | VARCHAR(200) | `Compania` | string | `compania` ✓ | ✅ OK | - |
| `ruc_cedula` | VARCHAR(20) | `RucCedula` | string | `ruc_cedula` ✓ | ✅ OK | - |
| `correo` | VARCHAR(100) | `Correo` | string | `correo` ✓ | ✅ OK | - |
| `telefono` | VARCHAR(20) | `Telefono` | string | `telefono` ✓ | ✅ OK | - |
| `concepto_id` | INTEGER | `ConceptoId` | int? | `concepto_id` ✓ | ✅ OK | - |

### Evidencia del manejo de tipo en `OrdenRecaudacionDAO.cs` (líneas 274-298):
```csharp
// Conversión de CodigoUsuario (string → int para INSERT)
int codigoUsuarioInt;
if (!string.IsNullOrWhiteSpace(orden.CodigoUsuario) && int.TryParse(orden.CodigoUsuario, out codigoUsuarioInt))
{
    cmd.Parameters.AddWithValue("@codigo_usuario", codigoUsuarioInt);  // ← Funciona si es número
}
else
{
    cmd.Parameters.AddWithValue("@codigo_usuario", DBNull.Value);  // ← Falla silenciosa si no es parseable
}
```

---

## Tabla: `aocr_or_orden_detalle`

| Columna DB | Tipo DB | Propiedad C# (`DetalleOrden`) | Tipo C# | Estado |
|---|---|---|---|---|
| `id` | SERIAL | `Id` | int | ✅ OK |
| `orden_id` | INTEGER | `OrdenId` | int | ✅ OK |
| `concepto_id` | INTEGER | `ConceptoId` | int? | ✅ OK |
| `concepto_codigo` | VARCHAR(50) | `ConceptoCodigo` | string | ✅ OK |
| `concepto_nombre` | VARCHAR(200) | `ConceptoNombre` | string | ✅ OK |
| `descripcion` | VARCHAR(500) | `Descripcion` | string | ✅ OK |
| `cantidad` | INTEGER | `Cantidad` | int | ✅ OK |
| `valor_unitario` | DECIMAL | `ValorUnitario` | decimal | ✅ OK |
| `porcentaje_admin` | DECIMAL (NOT NULL) | `PorcentajeAdmin` | decimal | ✅ OK |
| `subtotal` | DECIMAL | `Subtotal` | decimal | ✅ OK |
| `admin` | DECIMAL | `Admin` | decimal | ✅ OK |
| `total_linea` | DECIMAL | `TotalLinea` | decimal | ✅ OK |

---

## Tabla: `aocr_or_concepto`

| Columna DB | Tipo DB | Propiedad C# (`ConceptoModel`) | Tipo C# | Evidencia | Estado |
|---|---|---|---|---|---|
| `id` | SERIAL | `Id` | int | Dapper auto-mapeo | ✅ OK |
| `codigo` | VARCHAR | `Codigo` | string | `ConceptoDAO.cs` línea 41 | ✅ OK |
| `nombre` | VARCHAR | `Nombre` | string | `ConceptoDAO.cs` línea 41 | ✅ OK |
| `tipo_calculo` | VARCHAR | `TipoCalculo` | string | `ConceptoDAO.cs` línea 65 | ✅ OK |
| `valor_base` | DECIMAL | `ValorBase` | decimal | `ConceptoDAO.cs` línea 66 | ✅ OK |
| `porcentaje_admin` | DECIMAL | `PorcentajeAdmin` | decimal | `ConceptoDAO.cs` línea 67 | ✅ OK |
| `activo` | BOOLEAN | `Activo` | bool | `ConceptoDAO.cs` línea 68 | ✅ OK |
| `orden` | INTEGER | `Orden` | int | `ConceptoDAO.cs` línea 69 | ✅ OK |
| `descripcion` | TEXT | `Descripcion` | string | `ConceptoDAO.cs` línea 70 | ✅ OK |
| `por_estacion` | BOOLEAN | `PorEstacion` | bool | `ConceptoDAO.cs` línea 71 | ✅ OK |
| `por_dia` | BOOLEAN | `PorDia` | bool | `ConceptoDAO.cs` línea 72 | ✅ OK |
| `es_viatico` | BOOLEAN | `EsViatico` | bool? | `ConceptoDAO.cs` línea 73 | ✅ OK |

---

# 3) AUDITORÍA DAPPER/POSTGRESQL (CRÍTICO)

## ❌ ERRORES CRÍTICOS DE NOMBRES DE COLUMNAS

### 3.1 EmailQueueService - MISMATCH TOTAL

**Archivo:** `CapaDatos/Services/EmailQueueService.cs`

| Operación | Columna usada en código | Columna real en scripts SQL | Línea | Estado |
|-----------|------------------------|----------------------------|-------|--------|
| INSERT | `to_address` | `para` | 84 | ❌ ERROR |
| INSERT | `subject` | `asunto` | 84 | ❌ ERROR |
| INSERT | `body` | `cuerpo` | 84 | ❌ ERROR |
| INSERT | `status` | `estado` | 84 | ❌ ERROR |
| INSERT | `created_at` | `fecha_creacion` | 86 | ❌ ERROR |
| SELECT | `to_address` | `para` | 225 | ❌ ERROR |
| SELECT | `subject` | `asunto` | - | ❌ ERROR |
| SELECT | `body` | `cuerpo` | - | ❌ ERROR |
| SELECT | `status` | `estado` | 227 | ❌ ERROR |
| SELECT | `created_at` | `fecha_creacion` | 228 | ❌ ERROR |

**Evidencia (EmailQueueService.cs línea 225):**
```csharp
private EmailQueueItem MapearItem(System.Data.IDataReader reader)
{
    return new EmailQueueItem
    {
        Id = GetInt(reader, "id"),
        Para = GetString(reader, "to_address"),     // ← INCORRECTO: debería ser "para"
        Asunto = GetString(reader, "subject"),       // ← INCORRECTO: debería ser "asunto"
        Cuerpo = GetString(reader, "body"),          // ← INCORRECTO: debería ser "cuerpo"
        Estado = GetString(reader, "status"),        // ← INCORRECTO: debería ser "estado"
        FechaCreacion = GetDateTime(reader, "created_at"), // ← INCORRECTO: debería ser "fecha_creacion"
```

### 3.2 Discrepancia entre Scripts SQL

**PROBLEMA:** Existen múltiples definiciones de `email_queue` con columnas diferentes:

| Script | Columna para destinatario | Columna para estado | Columna para fecha |
|--------|--------------------------|---------------------|-------------------|
| `scripts/sql/email_pdf_tables.sql` | `para` | `estado` | `fecha_creacion` |
| `scripts/sql/create_email_queue.sql` | `para` | `estado` | `fecha_creacion` |
| `scripts/create_email_queue_table.sql` | `para` | `estado` | `fecha_creacion` |
| **EmailQueueService.cs (código)** | `to_address` | `status` | `created_at` |

**CONCLUSIÓN:** El código C# fue escrito con nombres en inglés pero los scripts SQL usan nombres en español.

### 3.3 OrdenRecaudacionDAO - ALIAS SQL CORRECTOS

**Archivo:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs`

Los JOINs usan alias correctamente:
```sql
SELECT o.*, c.nombre as concepto_nombre 
FROM aocr_or_orden o 
LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id
```

Y el mapeo lo maneja:
```csharp
// línea 626
var conceptoNombreOrdinal = reader.GetOrdinal("concepto_nombre");
if (!reader.IsDBNull(conceptoNombreOrdinal))
{
    orden.ConceptoNombre = reader.GetString(conceptoNombreOrdinal);
}
```
✅ **CORRECTO**

---

# 4) CONSISTENCIA DE TIPOS (CRÍTICO)

## 4.1 Correspondencia de Tipos

| Columna DB | Tipo PostgreSQL | Propiedad C# | Tipo C# | Estado | Observación |
|------------|-----------------|--------------|---------|--------|-------------|
| `email_queue.id` | SERIAL | `Id` | int | ✅ OK | - |
| `email_queue.intentos` | INTEGER | `Intentos` | int | ✅ OK | - |
| `email_queue.fecha_creacion` | TIMESTAMP | `FechaCreacion` | DateTime | ✅ OK | - |
| `email_queue.fecha_envio` | TIMESTAMP | `FechaEnvio` | DateTime? | ✅ OK | Nullable correcto |
| `email_queue.proximo_intento` | TIMESTAMP | `ProximoIntento` | DateTime? | ✅ OK | Nullable correcto |
| `aocr_or_orden.codigo_usuario` | INTEGER | `CodigoUsuario` | string | ⚠️ RIESGO | Conversión manual |
| `aocr_or_orden.codigo_solicitud` | INTEGER | `CodigoSolicitud` | string | ⚠️ RIESGO | Conversión manual |
| `aocr_or_orden.subtotal` | DECIMAL(18,2) | `Subtotal` | decimal? | ✅ OK | - |
| `aocr_or_orden.total` | DECIMAL(18,2) | `Total` | decimal? | ✅ OK | - |
| `aocr_or_orden.fecha_creacion` | TIMESTAMP | `FechaCreacion` | DateTime | ✅ OK | - |

## 4.2 Conversiones Peligrosas Detectadas

### 4.2.1 `codigo_usuario` (INTEGER → string)

**Archivo:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` líneas 274-288

```csharp
// INSERT: convierte string → int
int codigoUsuarioInt;
if (!string.IsNullOrWhiteSpace(orden.CodigoUsuario) && int.TryParse(orden.CodigoUsuario, out codigoUsuarioInt))
{
    cmd.Parameters.AddWithValue("@codigo_usuario", codigoUsuarioInt);
}
else
{
    cmd.Parameters.AddWithValue("@codigo_usuario", DBNull.Value);  // ⚠️ Se pierde el dato
}
```

**RIESGO:** Si `CodigoUsuario` contiene un valor no numérico (ej: "admin" o "ABC123"), se guardará como NULL sin error explícito.

### 4.2.2 Lectura en `MapearOrden` (línea 606)

```csharp
// SELECT: convierte cualquier tipo → string
CodigoUsuario = reader.IsDBNull(reader.GetOrdinal("codigo_usuario")) 
    ? null 
    : Convert.ToString(reader["codigo_usuario"]),  // ← Tolerante pero inconsistente
```
✅ Esta conversión es tolerante y funciona.

## 4.3 NullReference Potenciales

| Propiedad | Nullable en C# | Nullable en DB | Riesgo |
|-----------|---------------|----------------|--------|
| `OrdenRecaudacion.Subtotal` | decimal? | nullable | ✅ OK |
| `OrdenRecaudacion.Total` | decimal? | nullable | ✅ OK |
| `OrdenRecaudacion.FechaCreacion` | DateTime (NOT NULL) | NOT NULL | ✅ OK |
| `EmailQueueItem.FechaEnvio` | DateTime? | nullable | ✅ OK |
| `EmailQueueItem.ProximoIntento` | DateTime? | nullable | ✅ OK |
| `OrdenRecaudacionModel.FechaCreacion` | DateTime (NOT NULL) | NOT NULL | ✅ OK |
| `DetalleOrden.PorcentajeAdmin` | decimal (NOT NULL) | NOT NULL | ✅ OK |

---

# 5) VÍNCULO AJAX → CONTROLLER → VIEWMODEL (CRÍTICO)

## 5.1 Vista `Nueva.cshtml` → Controller `OrdenRecaudacionController`

### Formulario POST

**Vista (`Nueva.cshtml` líneas 19-20):**
```html
@using (Html.BeginForm("Nueva", "OrdenRecaudacion", FormMethod.Post, 
    new { id = "formOrden", @class = "form-horizontal", autocomplete = "off" }))
```

**Controller (`OrdenRecaudacionController.cs` línea 125):**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Solicitante,Administrador")]
public ActionResult Nueva(OrdenRecaudacionNuevaVM model)
```

✅ **Método y ruta coinciden.**

### Propiedades del ViewModel vs Campos del Formulario

| Campo en Vista | Propiedad esperada en ViewModel | Estado |
|----------------|--------------------------------|--------|
| `Orden_CodigoSolicitud` | `Orden.CodigoSolicitud` | ✅ OK |
| `Orden_LugarEmision` | `Orden.LugarEmision` | ✅ OK |
| `Orden_Compania` | `Orden.Compania` | ✅ OK |
| `Orden_RucCedula` | `Orden.RucCedula` | ✅ OK |
| `Orden_Correo` | `Orden.Correo` | ✅ OK |
| `Orden_Telefono` | `Orden.Telefono` | ✅ OK |
| `Orden_Observacion` | `Orden.Observacion` | ✅ OK |
| `DetallesJson` (hidden) | `DetallesJson` | ✅ OK |
| `Subtotal` (hidden) | `Orden.Subtotal` | ✅ OK |
| `Admin` (hidden) | `Orden.Admin` | ✅ OK |
| `Total` (hidden) | `Orden.Total` | ✅ OK |

### JSON de Detalles

**JavaScript (`Nueva.cshtml` líneas 262-265):**
```javascript
function actualizarCamposOcultos() {
    var payload = detalles.map(function (d) {
        return { ConceptoId: d.ConceptoId, Cantidad: d.Cantidad };  // ← Solo 2 propiedades
    });
    $('#DetallesJson').val(JSON.stringify(payload));
}
```

**Controller parseo (`OrdenRecaudacionController.cs` líneas 137-145):**
```csharp
var detallesRaw = serializer.Deserialize<List<Dictionary<string, object>>>(model.DetallesJson);
foreach (var d in detallesRaw)
{
    var conceptoId = d.ContainsKey("ConceptoId") ? Convert.ToInt32(d["ConceptoId"]) : 0;  // ✅ OK
    var cantidad = d.ContainsKey("Cantidad") ? Convert.ToInt32(d["Cantidad"]) : 1;        // ✅ OK
```

✅ **El JSON enviado coincide con lo esperado en el Controller.**

## 5.2 Rutas en Sidebar vs Controller

**Sidebar (`_Sidebar.cshtml` líneas 110-123):**
```razor
<a href="@Url.Action("Nueva","OrdenRecaudacion")">
<a href="@Url.Action("MisOrdenes", "Orden")">        // ⚠️ ¿Existe OrdenController?
<a href="@Url.Action("TodasOrdenes", "Financiero")"> // ⚠️ ¿Existe FinancieroController?
<a href="@Url.Action("Obligatoria", "OrdenRecaudacion")">
```

| Ruta en Sidebar | Controller | Action | Existe | Estado |
|-----------------|------------|--------|--------|--------|
| `/OrdenRecaudacion/Nueva` | OrdenRecaudacionController | Nueva | ✅ Sí | ✅ OK |
| `/OrdenRecaudacion/Obligatoria` | OrdenRecaudacionController | Obligatoria | ✅ Sí | ✅ OK |
| `/OrdenRecaudacion/Index` | OrdenRecaudacionController | Index | ✅ Sí | ✅ OK |
| `/Orden/MisOrdenes` | OrdenController | MisOrdenes | ❓ Pendiente verificar | ⚠️ Verificar |
| `/Financiero/TodasOrdenes` | FinancieroController | TodasOrdenes | ❓ Pendiente verificar | ⚠️ Verificar |

---

# 6) FLUJO DE IDENTIDAD / LLAVES (CRÍTICO)

## 6.1 INSERT en `aocr_or_orden` - Recuperación de ID

**Archivo:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` líneas 262-316

```csharp
var sql = @"INSERT INTO aocr_or_orden 
            (codigo_usuario, codigo_solicitud, numero_orden, ...)
            VALUES 
            (@codigo_usuario, @codigo_solicitud, @numero_orden, ...)
            RETURNING id";  // ← ✅ RETURNING id CORRECTO

// ...
var result = cmd.ExecuteScalar();
nuevoId = Convert.ToInt32(result);  // ← ✅ Recupera el ID generado
```

✅ **CORRECTO:** Usa `RETURNING id` y `ExecuteScalar()`.

## 6.2 Uso del ID como FK para `aocr_or_orden_detalle`

**Archivo:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` líneas 321-329

```csharp
// Insertar detalles si existen
if (orden.Detalles != null && orden.Detalles.Count > 0)
{
    foreach (var detalle in orden.Detalles)
    {
        detalle.OrdenId = nuevoId;  // ← ✅ Asigna el ID recuperado
        InsertarDetalle(detalle, conn);
    }
}
```

✅ **CORRECTO:** El `nuevoId` se propaga a los detalles.

## 6.3 Vínculo con `email_queue`

**Archivo:** `CapaDatos/Services/EmailQueueService.cs` líneas 82-100

```csharp
const string sql = @"
    INSERT INTO email_queue (
        to_address, subject, body, status,
        solicitud_id, created_at, proximo_intento   // ← Usa "solicitud_id"
    ) VALUES (...) RETURNING id";

// El parámetro:
AddParameter(cmd, "@solicitud_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
```

⚠️ **AMBIGÜEDAD CONCEPTUAL:**
- La propiedad C# se llama `OrdenId`
- La columna DB se llama `solicitud_id`
- El FK script (`add_email_queue_foreign_keys.sql`) apunta a `aocr_or_orden.id`

**Recomendación:** Renombrar `solicitud_id` a `orden_id` para coherencia, o documentar explícitamente la relación.

## 6.4 Vínculo con `aocr_tbsolicitud`

**Archivo:** `CapaDatos/DAOs/SolicitudAOCRDAO.cs` líneas 90-145

```csharp
const string sql = @"
INSERT INTO aocr_tbsolicitud
(numero_solicitud, fecha_solicitud, tipo_solicitud, ...)
VALUES
(@numero, @fecha, @tipo_solicitud, ...)
RETURNING codigo_solicitud;";  // ← ✅ RETURNING correcto

return Convert.ToInt32(cmd.ExecuteScalar());  // ← ✅ Recupera ID
```

✅ **CORRECTO**

---

# 7) ERRORES DE SINTAXIS EN VISTAS (JS)

## 7.1 Análisis de `Nueva.cshtml`

### Script Analizado (líneas 209-355)

**Estructura de llaves y paréntesis:**
```javascript
$(function () {
    var detalles = [];
    
    function aplicarSolicitudSeleccionada() { ... }  // ✅ Cerrado
    function toNumber(v) { ... }                     // ✅ Cerrado
    function actualizarTotales() { ... }             // ✅ Cerrado
    function renderTabla() { ... }                   // ✅ Cerrado
    function actualizarCamposOcultos() { ... }       // ✅ Cerrado
    
    $('#btnAgregarConcepto').on('click', function () { ... });  // ✅ Cerrado
    $(document).on('click', '.btnEliminarDetalle', function () { ... });  // ✅ Cerrado
    $(document).on('change', '.cantidad-detalle', function () { ... });  // ✅ Cerrado
    
    $('#formOrden').on('submit', function (e) { ... });  // ✅ Cerrado
    $('#Orden_CodigoSolicitud').on('change', aplicarSolicitudSeleccionada);
    aplicarSolicitudSeleccionada();
});  // ✅ Cerrado
```

✅ **No se detectaron errores de sintaxis** en el código JavaScript de `Nueva.cshtml`.

## 7.2 Manejo de Respuestas AJAX

**Observación:** La vista `Nueva.cshtml` usa un formulario POST estándar (no AJAX), por lo que no hay problemas de JSON vs HTML en las respuestas.

```javascript
$('#formOrden').on('submit', function (e) {
    if (detalles.length === 0) {
        alert('Debe agregar al menos un concepto a la orden');
        e.preventDefault();
        return false;
    }
    actualizarCamposOcultos();
    return true;  // ← Permite el submit normal del form
});
```

✅ **CORRECTO:** No es AJAX, es form submit tradicional.

## 7.3 Error en Script SQL

**Archivo:** `scripts/fix_email_queue_table.sql` línea 44-46

```sql
-- SINTAXIS INCORRECTA:
WHERE 
    table_schema = 'public' 
    AND table_name = 'email_queue', 'solicitud_id'   -- ❌ ERROR: coma inesperada
    AND column_name IN ('estado', 'proximo_intento')
```

❌ **ERROR DE SINTAXIS SQL:** La condición `table_name = 'email_queue', 'solicitud_id'` es inválida.

**Corrección:**
```sql
WHERE 
    table_schema = 'public' 
    AND table_name = 'email_queue'
    AND column_name IN ('estado', 'proximo_intento', 'solicitud_id')
```

---

# 8) LISTA DE FIXES MÍNIMOS (ORDENADOS)

## P0 - CRÍTICOS (Rompen la aplicación)

| # | Archivo | Qué cambiar | Verificación |
|---|---------|-------------|--------------|
| P0.1 | `CapaDatos/Services/EmailQueueService.cs` líneas 82-100 | Cambiar `to_address` → `para`, `subject` → `asunto`, `body` → `cuerpo`, `status` → `estado`, `created_at` → `fecha_creacion` en el INSERT | Ejecutar INSERT y verificar que no da error "column does not exist" |
| P0.2 | `CapaDatos/Services/EmailQueueService.cs` líneas 223-231 | Cambiar nombres de columnas en `MapearItem()`: `to_address` → `para`, etc. | Ejecutar SELECT y verificar mapeo correcto |
| P0.3 | `scripts/fix_email_queue_table.sql` línea 44 | Corregir sintaxis: `table_name = 'email_queue', 'solicitud_id'` → `table_name = 'email_queue'` | Ejecutar script sin error de sintaxis |

## P1 - ALTOS (Pueden causar datos corruptos o pérdida)

| # | Archivo | Qué cambiar | Verificación |
|---|---------|-------------|--------------|
| P1.1 | Scripts SQL de `email_queue` | Unificar definición: elegir español (`para`, `estado`) O inglés (`to_address`, `status`) y actualizar todos los scripts | Revisar estructura de tabla con `\d email_queue` |
| P1.2 | `CapaDatos/Services/EmailQueueService.cs` | Agregar columnas faltantes al INSERT: `es_html`, `intentos`, `max_intentos` | Verificar que nuevos registros tienen valores correctos |
| P1.3 | DB/Scripts | Decidir si `email_queue.orden_id` = `email_queue.solicitud_id` y renombrar para coherencia | Verificar FK con `\d+ email_queue` |

## P2 - MEDIOS (Inconsistencias que pueden causar bugs)

| # | Archivo | Qué cambiar | Verificación |
|---|---------|-------------|--------------|
| P2.1 | `CapaDatos/Entidades/OrdenRecaudacion.cs` línea 27 | Considerar cambiar `CodigoUsuario` de `string` a `int` para coincidir con DB | Test unitario de serialización |
| P2.2 | `scripts/create_email_queue_table.sql` | Agregar columna `proximo_intento` que falta | Verificar estructura con `\d email_queue` |
| P2.3 | `scripts/create_email_queue_table.sql` línea 11 | Cambiar default de `estado` de `'Pendiente'` a `'PENDIENTE'` para coincidir con código | INSERT sin estado y verificar valor |
| P2.4 | `CapaDatos/Models/OrdenRecaudacionModel.cs` | Verificar que `CodigoUsuario` (int) vs `OrdenRecaudacion.CodigoUsuario` (string) no cause problemas | Test de mapeo Model ↔ Entidad |

---

# RESUMEN FINAL

## Estadísticas de la Auditoría

| Categoría | Total | OK | Errores | Riesgos |
|-----------|-------|-----|---------|---------|
| Columnas `email_queue` | 20 | 3 | **6** | 11 |
| Columnas `aocr_or_orden` | 15 | 13 | 0 | **2** |
| Columnas `aocr_or_orden_detalle` | 11 | 11 | 0 | 0 |
| Columnas `aocr_or_concepto` | 11 | 11 | 0 | 0 |
| Tipos de datos | 20 | 18 | 0 | **2** |
| Rutas AJAX/Controller | 5 | 3 | 0 | **2** |
| Flujo de llaves | 3 | 3 | 0 | 0 |
| Sintaxis JS | 1 | 1 | 0 | 0 |
| Sintaxis SQL | 1 | 0 | **1** | 0 |

## Conclusión

**La principal falla crítica está en `EmailQueueService.cs`** que usa nombres de columnas en inglés (`to_address`, `status`, etc.) mientras que los scripts SQL definen la tabla con nombres en español (`para`, `estado`, etc.). **Esto causará errores en tiempo de ejecución** cuando se intente insertar o leer de la tabla `email_queue`.

**Acción inmediata requerida:** Alinear el código C# con los scripts SQL O viceversa.
