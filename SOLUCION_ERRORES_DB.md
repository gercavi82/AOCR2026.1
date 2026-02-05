# 🚨 SOLUCIÓN A ERRORES DE BASE DE DATOS AOCR

## PROBLEMAS IDENTIFICADOS

### 1. Error Principal: Column "codigoparametro" does not exist
```
Error obteniendo tarifa configurable TARIFA_EMI_AOCR: 42703: column "codigoparametro" does not exist
```

### 2. Errores de Columna Banco
```
System.IndexOutOfRangeException en Npgsql.dll
GetSafeBanco: columna explícita no encontrada, se infiere desde método de pago
```

## CAUSA RAÍZ
- La tabla `aocr_tbparametro` no existe o no tiene la estructura correcta
- La columna `banco` en `aocr_tbpago` puede estar faltando
- El sistema de parámetros configurables no está inicializado

## SOLUCIÓN IMPLEMENTADA

### 1. Scripts de Reparación Creados
- ✅ `fix_database_complete.sql` - Script SQL completo
- ✅ `repair_database.bat` - Generador de script simplificado
- ✅ `temp_repair.sql` - Script SQL generado automáticamente

### 2. Estructura de Tabla Corregida
```sql
CREATE TABLE IF NOT EXISTS aocr_tbparametro (
    codigoparametro SERIAL PRIMARY KEY,
    clave VARCHAR(100) NOT NULL UNIQUE,
    valor VARCHAR(500) NOT NULL,
    descripcion VARCHAR(1000),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    createdby INTEGER,
    updatedat TIMESTAMP,
    updatedby INTEGER,
    deletedat TIMESTAMP,
    deletedby INTEGER
);
```

### 3. Parámetros Configurables Iniciales
- `TEST_EMPRESA_NOMBRE` = "AERONÁUTICA CIVIL"
- `DEMO_MONTO_FIJO` = "80.00"
- `TARIFA_EMI_AOCR` = "250.00"
- `TARIFA_REN_AOCR` = "200.00"
- `TARIFA_MOD_AOCR_INC` = "150.00"
- `TARIFA_MOD_AOCR_SIN_INC` = "100.00"
- `TARIFA_INSPECCION_EXT` = "500.00"
- `TARIFA_VIATICOS_INSPECTOR` = "80.00"
- `PORCENTAJE_ADMIN_VIATICOS` = "15"

### 4. Columna Banco Agregada
```sql
ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255);
UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL OR banco = '';
```

## PASOS PARA EJECUTAR LA REPARACIÓN

### Opción A: Manual (Recomendado)
1. Abrir pgAdmin o cliente PostgreSQL
2. Conectar a la base de datos AOCR
3. Ejecutar el contenido de `temp_repair.sql`
4. Verificar que las tablas se crearon correctamente

### Opción B: Línea de Comandos (Si tienes psql)
```bash
psql -h localhost -U [usuario] -d [base_datos] -f temp_repair.sql
```

### Opción C: Integrar en Aplicación
```csharp
// En Global.asax.cs Application_Start
try {
    var parametroDAO = new ParametroDAO();
    parametroDAO.InicializarParametrosBasicos();
} catch (Exception ex) {
    // Log error pero no romper la aplicación
}
```

## VERIFICACIÓN POST-REPARACIÓN

### 1. Comprobar Tabla de Parámetros
```sql
SELECT COUNT(*) FROM aocr_tbparametro WHERE activo = TRUE;
-- Debería devolver > 0
```

### 2. Comprobar Columna Banco
```sql
SELECT COUNT(*) FROM aocr_tbpago WHERE banco IS NOT NULL;
-- Debería devolver todos los registros
```

### 3. Verificar Logs de Aplicación
- Los errores de "codigoparametro" deberían desaparecer
- Los errores de "IndexOutOfRangeException" de banco deberían reducirse
- La aplicación debería cargar sin errores críticos

## RESULTADOS ESPERADOS

### ✅ Antes de la Reparación
```
❌ Error obteniendo tarifa configurable TARIFA_EMI_AOCR: 42703: column "codigoparametro" does not exist
❌ System.IndexOutOfRangeException en Npgsql.dll
❌ GetSafeBanco: columna explícita no encontrada
```

### ✅ Después de la Reparación
```
✅ Parámetros configurables cargados correctamente
✅ Columna banco disponible en registros de pago
✅ Sistema de configuración funcional
✅ Eliminación de valores hardcodeados
```

## PRÓXIMOS PASOS

1. **Ejecutar la reparación** usando uno de los métodos above
2. **Reiniciar IIS Express** para cargar los cambios
3. **Probar la aplicación** creando una nueva orden
4. **Verificar** que los valores configurables se cargan correctamente
5. **Monitorear logs** para confirmar que no hay más errores

## ARCHIVOS IMPORTANTES

- `temp_repair.sql` - Script SQL principal a ejecutar
- `fix_database_complete.sql` - Script completo con todos los parámetros
- `repair_database.bat` - Generador automático de scripts
- `ParametroDAO.cs` - DAO que maneja los parámetros configurables
- `ConfigApiController.cs` - API REST para valores configurables
- `aocr-config.js` - Cliente JavaScript para configuración dinámica

¡La reparación está lista para ejecutar! 🚀