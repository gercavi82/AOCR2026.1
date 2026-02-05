# INSTRUCCIONES PARA REPARAR LA BASE DE DATOS

## Información de Conexión
- **Servidor**: 172.20.16.55
- **Puerto**: 5432  
- **Base de datos**: dgac_des
- **Usuario**: root
- **Contraseña**: control (completar si está incompleta)

## Opción 1: Usando herramienta gráfica (pgAdmin, DBeaver, etc.)
1. Conecta a la base de datos con los parámetros anteriores
2. Abre el archivo `repair_aocr_tbparametro.sql`
3. Ejecuta el script completo
4. Verifica que los parámetros se insertaron correctamente

## Opción 2: Línea de comandos (si tienes psql instalado)
```bash
psql -h 172.20.16.55 -p 5432 -U root -d dgac_des -f repair_aocr_tbparametro.sql
```

## Opción 3: Desde C# (usando la aplicación actual)
Puedes ejecutar estos comandos directamente desde un controlador temporal:

```csharp
// Agregar columnas
var addColumns = @"
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);
ALTER TABLE aocr_tbparametro ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);
";

// Insertar parámetros
var insertParams = @"
INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estación para cálculo de inspecciones')
ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por día de viático para inspectores')
ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos')
ON CONFLICT (codigoparametro) DO UPDATE SET valorparametro = EXCLUDED.valorparametro;
";
```

## ¿Qué hace este script?
1. **Agrega las columnas faltantes**: `codigoparametro`, `valorparametro`, `descripcionparametro`
2. **Inserta los parámetros necesarios** para el cálculo dinámico de PDFs:
   - `CALCULO_VALOR_POR_ESTACION`: 500.00 (valor por inspección por estación)
   - `CALCULO_VALOR_POR_DIA_VIATICO`: 80.00 (valor por día de viático)  
   - `CALCULO_PORCENTAJE_GASTOS_ADMIN`: 8.00 (8% de gastos administrativos)
3. **Verifica** que todo se ejecutó correctamente

## Después de ejecutar el script
1. **Reinicia la aplicación web** (detén y vuelve a ejecutar desde Visual Studio)
2. Los errores `column "codigoparametro" does not exist` desaparecerán
3. Los PDFs mostrarán valores dinámicos en lugar de los hardcodeados ($500, $80, 8%)

## Verificación
Después de ejecutar, puedes verificar con:
```sql
SELECT codigoparametro, valorparametro, descripcionparametro 
FROM aocr_tbparametro 
WHERE codigoparametro IN ('CALCULO_VALOR_POR_ESTACION', 'CALCULO_VALOR_POR_DIA_VIATICO', 'CALCULO_PORCENTAJE_GASTOS_ADMIN');
```