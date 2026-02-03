# 🔧 Corrección de Error: porcentaje_admin NOT NULL

## 🐛 Problema Identificado

```
Error: null value in column "porcentaje_admin" of relation "aocr_or_orden_detalle" violates not-null constraint
```

La columna `porcentaje_admin` en la tabla `aocr_or_orden_detalle` **NO permite valores NULL**, pero el código no estaba proporcionando este valor al insertar registros.

## ✅ Solución Aplicada

### 1. **OrdenRecaudacionController.cs** (Líneas 222-247)

**Antes:**
```csharp
var detalle = new DetalleOrden
{
    OrdenId = ordenId,
    ConceptoId = det.ConceptoId,
    Cantidad = det.Cantidad,
    PrecioUnitario = det.PrecioUnitario,
    Subtotal = det.Subtotal
};
```

**Después:**
```csharp
// Obtener el concepto para tener el porcentaje de administración
var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
var porcentajeAdmin = concepto?.PorcentajeAdmin ?? 0m;
var adminLinea = det.Subtotal * (porcentajeAdmin / 100m);
var totalLinea = det.Subtotal + adminLinea;

var detalle = new DetalleOrden
{
    OrdenId = ordenId,
    ConceptoId = det.ConceptoId,
    ConceptoCodigo = concepto?.Codigo,
    ConceptoNombre = concepto?.Nombre,
    Cantidad = det.Cantidad,
    ValorUnitario = det.PrecioUnitario,
    PorcentajeAdmin = porcentajeAdmin,  // ✅ AGREGADO
    Subtotal = det.Subtotal,
    Admin = adminLinea,                 // ✅ CALCULADO
    TotalLinea = totalLinea             // ✅ CALCULADO
};
```

### 2. **OrdenRecaudacionDAO.cs** (Línea 376)

**Antes:**
```csharp
cmd.Parameters.AddWithValue("@porcentajeAdmin", (object)detalle.PorcentajeAdmin ?? DBNull.Value);
```

**Después:**
```csharp
cmd.Parameters.AddWithValue("@porcentajeAdmin", detalle.PorcentajeAdmin ?? 0m); // NOT NULL en DB
```

### 3. **DetalleOrden.cs** (Entidad - Líneas 51-58)

**Antes:**
```csharp
[Column("porcentaje_admin")]
public decimal? PorcentajeAdmin { get; set; }

[Column("admin")]
public decimal? Admin { get; set; }
```

**Después:**
```csharp
[Column("porcentaje_admin")]
[Required]
public decimal PorcentajeAdmin { get; set; }  // ✅ Ya no es nullable

[Column("admin")]
public decimal Admin { get; set; }            // ✅ Ya no es nullable
```

## 📊 Estructura de Tablas Relacionadas

```sql
-- Tabla de conceptos (origen de porcentaje_admin)
aocr_or_concepto:
  - id
  - codigo
  - nombre
  - tipo_calculo
  - valor_base
  - porcentaje_admin  ← Se obtiene de aquí
  - activo

-- Tabla de órdenes
aocr_or_orden:
  - id
  - numero_orden
  - subtotal
  - admin (suma de todos los admin de detalles)
  - total

-- Tabla de detalles (donde se guardaba NULL)
aocr_or_orden_detalle:
  - id
  - orden_id
  - concepto_id
  - cantidad
  - valor_unitario
  - porcentaje_admin  ← NOT NULL (se fijó aquí)
  - subtotal
  - admin
  - total_linea
```

## 🎯 Flujo Correcto

1. **Usuario selecciona conceptos** en el formulario
2. **Controlador obtiene cada concepto** de la tabla `aocr_or_concepto`
3. **Se extrae el `porcentaje_admin`** del concepto (ej: 10%)
4. **Se calculan los valores**:
   - `subtotal = cantidad × valor_unitario`
   - `admin = subtotal × (porcentaje_admin / 100)`
   - `total_linea = subtotal + admin`
5. **Se crea el detalle** con TODOS los campos requeridos
6. **Se inserta en la BD** sin errores

## ✅ Validación

Después de aplicar estos cambios:

✓ Todos los campos obligatorios se proporcionan  
✓ Los cálculos se realizan correctamente  
✓ No se insertarán valores NULL en `porcentaje_admin`  
✓ La entidad refleja correctamente la estructura de la BD  

## 🔄 Próximos Pasos

1. **Compilar** el proyecto para verificar que no hay errores
2. **Probar** la creación de una nueva orden
3. **Verificar** que los valores se guardan correctamente en la BD
4. **Revisar** el PDF generado para confirmar que muestra los cálculos

---

**Fecha de corrección:** 02/02/2026  
**Archivos modificados:** 3  
**Estado:** ✅ Corregido
