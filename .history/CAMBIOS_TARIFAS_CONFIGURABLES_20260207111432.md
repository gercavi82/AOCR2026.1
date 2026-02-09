# CAMBIOS: Tarifas Configurables Implementadas

**Fecha:** 7 de febrero de 2026  
**Módulo:** OrdenRecaudacion  
**Tarea completada:** Parametrización de tarifas hardcodeadas

---

## ✅ CAMBIOS IMPLEMENTADOS

### 1. Modificación en OrdenRecaudacionController.cs

**Archivo:** `CapaPresentacion\Controllers\OrdenRecaudacionController.cs`

#### Cambio 1: Agregar ParametroDAO

```csharp
// AGREGADO en línea 35 (aproximadamente)
private readonly ParametroDAO _parametroDao = new ParametroDAO();
```

#### Cambio 2: Nuevos métodos helper

```csharp
/// <summary>
/// Obtiene valor de tarifa configurable desde BD, con fallback a valor por defecto
/// </summary>
private decimal ObtenerTarifaConfigurable(string clave, decimal valorPorDefecto)
{
    try
    {
        var parametro = _parametroDao.ObtenerPorClave(clave);
        if (parametro != null && parametro.Activo && !string.IsNullOrEmpty(parametro.Valor))
        {
            if (decimal.TryParse(parametro.Valor, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
            {
                return valor;
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error obteniendo tarifa '{clave}': {ex.Message}");
    }

    return valorPorDefecto;
}

/// <summary>
/// Obtiene porcentaje configurable desde BD, con fallback a valor por defecto
/// </summary>
private decimal ObtenerPorcentajeConfigurable(string clave, decimal valorPorDefecto)
{
    return ObtenerTarifaConfigurable(clave, valorPorDefecto);
}
```

#### Cambio 3: Actualizar AsegurarConceptosBasicos()

**ANTES (Hardcoded):**
```csharp
new CapaDatos.Models.ConceptoModel { 
    Codigo = "EMI_AOCR", 
    Nombre = "Emisión AOCR", 
    TipoCalculo = "FIJO", 
    ValorBase = 3300m,          // ❌ HARDCODED
    PorcentajeAdmin = 0m,       // ❌ HARDCODED
    Activo = true, 
    Orden = 1, 
    ...
}
```

**DESPUÉS (Configurable desde BD):**
```csharp
new CapaDatos.Models.ConceptoModel { 
    Codigo = "EMI_AOCR", 
    Nombre = "Emisión AOCR", 
    TipoCalculo = "FIJO", 
    ValorBase = ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m),               // ✅ DESDE BD
    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_EMI_AOCR", 0m), // ✅ DESDE BD
    Activo = true, 
    Orden = 1, 
    ...
}
```

**Se aplicó el mismo cambio a los 6 conceptos:**
1. **EMI_AOCR** - Emisión AOCR
2. **REN_AOCR** - Renovación AOCR
3. **MOD_AOCR_INC** - Modificación AOCR (con inclusión)
4. **MOD_AOCR_SIN_INC** - Modificación AOCR (sin inclusión)
5. **INSPECCION_EXT** - Inspección externa
6. **VIATICOS_INSPECTOR** - Viáticos inspectores

---

## 📊 PARÁMETROS EN BASE DE DATOS

### Estado actual de tabla `parametros`:

```sql
SELECT clave, valor FROM parametros 
WHERE clave LIKE 'TARIFA_%' OR clave LIKE 'PORCENTAJE_%' 
ORDER BY clave;
```

**Resultado (11 parámetros insertados):**

| Clave | Valor |
|-------|-------|
| PORCENTAJE_ADMIN_EMI_AOCR | 0.00 |
| PORCENTAJE_ADMIN_INSPECCION | 0.00 |
| PORCENTAJE_ADMIN_MOD | 0.00 |
| PORCENTAJE_ADMIN_REN_AOCR | 0.00 |
| PORCENTAJE_ADMIN_VIATICOS | 8.00 |
| TARIFA_EMI_AOCR | 3300.00 |
| TARIFA_INSPECCION_EXT | 500.00 |
| TARIFA_MOD_AOCR_INC | 1600.00 |
| TARIFA_MOD_AOCR_SIN_INC | 80.00 |
| TARIFA_REN_AOCR | 3300.00 |
| TARIFA_VIATICOS_INSPECTOR | 80.00 |

✅ **TODOS los parámetros ya están insertados en la BD.**

---

## 🎯 BENEFICIOS

### 1. Flexibilidad
- **ANTES:** Cambiar tarifas requería editar código y recompilar
- **AHORA:** Cambiar tarifas solo requiere UPDATE en BD

### 2. Auditabilidad
- Todos los cambios de tarifas quedan registrados en tabla `parametros`
- Campos: `fecha_modificacion`, `usuario_modificacion`

### 3. Fallback seguro
- Si falla lectura de BD, usa valores por defecto hardcoded
- No rompe la aplicación si hay problemas de conexión

### 4. Mantenibilidad
- Administradores pueden cambiar tarifas sin intervención de desarrollo
- Posible crear interfaz administrativa para gestión de parámetros

---

## 📝 CÓMO MODIFICAR TARIFAS

### Usando SQL directo:

```sql
-- Modificar tarifa de emisión AOCR de $3,300 a $3,500
UPDATE parametros 
SET valor = '3500.00',
    fecha_modificacion = NOW(),
    usuario_modificacion = 'NombreUsuario'
WHERE clave = 'TARIFA_EMI_AOCR';

-- Modificar porcentaje admin viáticos de 8% a 10%
UPDATE parametros 
SET valor = '10.00',
    fecha_modificacion = NOW(),
    usuario_modificacion = 'NombreUsuario'
WHERE clave = 'PORCENTAJE_ADMIN_VIATICOS';
```

### Verificar cambios:

```sql
SELECT clave, valor, fecha_modificacion, usuario_modificacion 
FROM parametros 
WHERE clave IN ('TARIFA_EMI_AOCR', 'PORCENTAJE_ADMIN_VIATICOS');
```

---

## 🔄 FLUJO DE APLICACIÓN

1. **Usuario crea nueva orden** → POST `/OrdenRecaudacion/Nueva`
2. **Se llama `AsegurarConceptosBasicos()`** (línea 637 aprox.)
3. **Para cada concepto:**
   - Llama `ObtenerTarifaConfigurable(clave, valorPorDefecto)`
   - ParametroDAO consulta BD: `SELECT * FROM parametros WHERE clave = @clave`
   - Si existe y está activo → usa valor de BD
   - Si no existe o hay error → usa valor por defecto (fallback)
4. **ConceptoDAO.Upsert()** guarda concepto con tarifa actualizada
5. **Orden se crea** con precios correctos

---

## ⚠️ NOTA IMPORTANTE

**NO ES NECESARIO modificar construcción de nueva orden** (`Nueva()` método POST).

El flujo actual funciona así:
1. `AsegurarConceptosBasicos()` actualiza la tabla `aocr_or_concepto` con tarifas de BD
2. Cuando se crea orden, se consulta `ConceptoDAO.ObtenerPorId(conceptoId)`
3. El concepto YA tiene el `ValorBase` actualizado desde BD
4. Se usa ese `ValorBase` para calcular subtotales

**Orden de ejecución:**
```
RegistrarPago() línea 637
  └─> AsegurarConceptosBasicos() ← LEE BD y actualiza conceptos
         └─> _conceptoDao.Upsert() ← GUARDA en aocr_or_concepto

Nueva() línea 152
  └─> _conceptoDao.ObtenerPorId() ← LEE de aocr_or_concepto (YA actualizado)
```

---

## ✅ ESTADO FINAL

| Tarea | Estado |
|-------|--------|
| ✅ Crear tabla `parametros` | COMPLETADO |
| ✅ Insertar 11 parámetros de tarifas | COMPLETADO |
| ✅ Crear métodos helper en Controller | COMPLETADO |
| ✅ Modificar `AsegurarConceptosBasicos()` | COMPLETADO |
| ✅ Validar parámetros en BD | COMPLETADO |

---

## 🚀 PRÓXIMOS PASOS SUGERIDOS

### 1. Crear interfaz administrativa (Opcional)
**Archivo nuevo:** `ParametrosController.cs`

```csharp
[Authorize(Roles = "Administrador")]
public class ParametrosController : Controller
{
    private readonly ParametroDAO _dao = new ParametroDAO();

    public ActionResult Index()
    {
        var parametros = _dao.ObtenerTodos()
            .Where(p => p.Clave.StartsWith("TARIFA_") || p.Clave.StartsWith("PORCENTAJE_"))
            .ToList();
        return View(parametros);
    }

    [HttpPost]
    public ActionResult Actualizar(int id, string nuevoValor)
    {
        var parametro = _dao.ObtenerPorId(id);
        if (parametro == null)
            return HttpNotFound();

        parametro.Valor = nuevoValor;
        parametro.UpdatedAt = DateTime.Now;
        parametro.UpdatedBy = GetUserId();
        
        _dao.Actualizar(parametro);
        
        TempData["OK"] = "Parámetro actualizado correctamente";
        return RedirectToAction("Index");
    }
}
```

### 2. Testing
- Probar creación de orden con tarifas de BD
- Modificar un parámetro en BD y verificar que se usa nuevo valor
- Probar fallback cuando hay error de conexión

### 3. Documentación usuario final
- Manual de cómo cambiar tarifas vía SQL
- Video tutorial para administradores

---

**FIN DEL DOCUMENTO**
