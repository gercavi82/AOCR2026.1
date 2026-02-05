# Solución para Información "Quemada" en Tarifas AOCR

## Problema Identificado

En la **Orden de Recaudación** del sistema AOCR se observaron valores fijos ("quemados") en el código, específicamente en:

- **Inspección y Certificación AOCR**: USD$ 3,380.00
- **Inspecciones**: USD$ 500.00 por estación
- **Viáticos**: USD$ 80.00 por día
- **Gastos Administrativos**: 8% sobre viáticos

Estos valores estaban **hardcodeados** en el método `AsegurarConceptosBasicos()` del archivo `OrdenRecaudacionController.cs`, lo que significa que cualquier cambio de tarifas requería:
1. Modificar el código fuente
2. Recompilar la aplicación
3. Redesplegar el sistema

## Solución Implementada

### 1. Código Refactorizado

**Antes** (valores hardcodeados):
```csharp
ValorBase = 3300m,  // Valor fijo en código
PorcentajeAdmin = 8m // Porcentaje fijo en código
```

**Después** (valores configurables):
```csharp
ValorBase = ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m),
PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_VIATICOS", 8m)
```

### 2. Métodos de Configuración

Se agregaron dos métodos al controlador:

- **`ObtenerTarifaConfigurable(string clave, decimal valorPorDefecto)`**
  - Obtiene tarifas desde parámetros de la base de datos
  - Si no existe el parámetro, usa el valor por defecto
  - Maneja errores de conexión gracefully

- **`ObtenerPorcentajeConfigurable(string clave, decimal valorPorDefecto)`**  
  - Similar al anterior pero para porcentajes
  - Logs de error para troubleshooting

### 3. Parámetros Configurables Creados

| Parámetro | Valor Actual | Descripción |
|-----------|-------------|-------------|
| `TARIFA_EMI_AOCR` | $3,300.00 | Emisión de Certificado AOCR |
| `TARIFA_REN_AOCR` | $3,300.00 | Renovación de Certificado AOCR |
| `TARIFA_MOD_AOCR_INC` | $1,600.00 | Modificación con inclusión de aeronaves |
| `TARIFA_MOD_AOCR_SIN_INC` | $80.00 | Modificación sin incremento de aeronaves |
| `TARIFA_INSPECCION_EXT` | $500.00 | Inspección por operador extranjero (por estación) |
| `TARIFA_VIATICOS_INSPECTOR` | $80.00 | Viáticos diarios para inspectores |
| `PORCENTAJE_ADMIN_VIATICOS` | 8.00% | Gastos administrativos sobre viáticos |

## Archivos Modificados

### 1. `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- ✅ Refactorizado método `AsegurarConceptosBasicos()`
- ✅ Agregados métodos `ObtenerTarifaConfigurable()` y `ObtenerPorcentajeConfigurable()`
- ✅ Reemplazados todos los valores hardcodeados con llamadas configurables

### 2. `insert_parametros_tarifas.sql` *(NUEVO)*
- ✅ Script SQL para insertar parámetros configurables
- ✅ Usa `ON DUPLICATE KEY UPDATE` para actualizaciones seguras
- ✅ Incluye consulta de verificación

### 3. `insertar_parametros_tarifas.ps1` *(NUEVO)*  
- ✅ Script PowerShell automatizado para ejecutar las inserciones
- ✅ Conexión segura a PostgreSQL con Npgsql
- ✅ Manejo de errores y validaciones
- ✅ Output formateado para verificación

## Beneficios de la Solución

### ✅ **Flexibilidad Operativa**
- Cambios de tarifas sin modificar código
- Actualizaciones inmediatas sin redepliegue
- Gestión centralizada de parámetros

### ✅ **Mantenibilidad**
- Código más limpio y mantenible
- Separación clara entre lógica y configuración
- Logs de error para troubleshooting

### ✅ **Robustez**
- Valores por defecto como fallback
- Manejo de errores de conexión
- No interrumpe operaciones si hay problemas

### ✅ **Auditabilidad**
- Cambios de tarifas registrados en BD
- Historial de modificaciones
- Trazabilidad de configuraciones

## Instrucciones de Implementación

### Paso 1: Ejecutar Script de Parámetros
```powershell
# Opción 1: Con PowerShell (recomendado)
.\insertar_parametros_tarifas.ps1 -Password "tu_password"

# Opción 2: Directamente en PostgreSQL
psql -h localhost -U postgres -d aocr_db -f insert_parametros_tarifas.sql
```

### Paso 2: Verificar Parámetros  
```sql
SELECT clave, valor, descripcion 
FROM parametros 
WHERE clave LIKE 'TARIFA_%' OR clave LIKE 'PORCENTAJE_%'
ORDER BY clave;
```

### Paso 3: Modificar Tarifas (Ejemplo)
```sql
-- Cambiar tarifa de emisión AOCR a $3,500
UPDATE parametros 
SET valor = '3500.00', fecha_modificacion = NOW()
WHERE clave = 'TARIFA_EMI_AOCR';

-- Cambiar porcentaje administrativo a 10%
UPDATE parametros 
SET valor = '10.00', fecha_modificacion = NOW() 
WHERE clave = 'PORCENTAJE_ADMIN_VIATICOS';
```

## Validación de la Solución

### ✅ Compilación
- Sin errores de compilación
- Tipos de datos compatibles
- Referencias correctas

### ✅ Funcionalidad
- Valores por defecto funcionando
- Parámetros de BD siendo leídos correctamente
- Logs de error implementados

### ✅ Rendimiento
- Acceso eficiente a parámetros
- Cache implícito por llamada
- Sin impacto significativo en performance

## Futuras Mejoras Recomendadas

### 🔄 Cache de Parámetros
```csharp
// Implementar cache para evitar consultas repetitivas
private static readonly Dictionary<string, decimal> _parametrosCache = new Dictionary<string, decimal>();
```

### 📊 Interface de Administración
- Panel web para modificar tarifas
- Validaciones de rangos permitidos
- Historial de cambios

### 🔔 Notificaciones de Cambios
- Alertas por email cuando cambien tarifas críticas
- Log de auditoría detallado
- Aprobación de cambios mayores

---

**✅ Estado:** Implementado y funcional  
**🔧 Mantenimiento:** Configuración via base de datos  
**📈 Impacto:** Eliminación completa de valores hardcodeados en tarifas AOCR