# Solución: Información del Banco en Detalles de Orden de Recaudación

## Problema Identificado
La información del banco no aparecía en la vista de detalles de la orden de recaudación porque:
1. La columna `banco` no existe en la tabla `aocr_tbpago` de la base de datos
2. El método `GetSafeBanco` retornaba `null` cuando no encontraba la columna

## Solución Implementada

### 1. Lógica de Inferencia Inteligente
Se implementó un sistema inteligente en `OrdenRecaudacionDAO.cs` que:
- Detecta si la columna `banco` existe en la base de datos
- Si no existe, intenta inferir el banco desde el método de pago
- Proporciona valores descriptivos en lugar de mostrar "N/A"

#### Archivos modificados:
- `CapaDatos\DAOs\OrdenRecaudacionDAO.cs`
  - Método `GetSafeBanco()` - Manejo inteligente de columna banco
  - Método `InferirBancoDesdeMetodoPago()` - Lógica de inferencia
  - Método `AgregarColumnaBancoTemporal()` - Utilitario para agregar columna

- `CapaPresentacion\Views\OrdenRecaudacion\Detalles.cshtml`
  - Mejorada lógica de visualización del banco

- `CapaPresentacion\Controllers\OrdenRecaudacionController.cs`
  - Endpoint `AgregarColumnaBanco()` para administradores

### 2. Mapeo Inteligente de Bancos
La nueva lógica identifica bancos basándose en el método de pago:
- "PICHINCHA" → "BANCO PICHINCHA"
- "GUAYAQUIL" → "BANCO GUAYAQUIL"
- "PACIFICO" → "BANCO DEL PACIFICO"
- "TRANSFERENCIA" → "TRANSFERENCIA_BANCARIA"
- "EFECTIVO" → "PAGO_EFECTIVO"
- Y más...

### 3. Endpoint Administrativo
Nuevo endpoint: `/OrdenRecaudacion/AgregarColumnaBanco`
- Solo accesible para usuarios con rol "Administrador"
- Agrega la columna `banco VARCHAR(255)` a la tabla `aocr_tbpago`
- Actualiza registros existentes con valor "NO_ESPECIFICADO"
- Manejo seguro de errores

## Instrucciones de Uso

### Opción 1: Usar la Funcionalidad Temporal (Inmediata)
1. La aplicación ya funciona con la lógica de inferencia
2. Los pagos mostrarán información del banco basada en el método de pago
3. En lugar de "N/A" verás valores como "METODO: TRANSFERENCIA" o bancos inferidos

### Opción 2: Agregar Columna a la Base de Datos (Definitiva)
1. Iniciar sesión como usuario con rol "Administrador"
2. Navegar a: `/OrdenRecaudacion/AgregarColumnaBanco`
3. El sistema agregará automáticamente la columna banco
4. Los futuros pagos podrán almacenar información específica del banco

## Verificación de la Solución

### Antes:
- Columna "Banco" mostraba "N/A" para todos los pagos
- Información del banco se perdía

### Después:
- Columna "Banco" muestra información inferida del método de pago
- Si se agrega la columna a la BD, se almacena información específica
- Mejor experiencia de usuario con información más descriptiva

## Archivos de Respaldo
Se recomienda hacer respaldo de los siguientes archivos antes de cualquier cambio:
- `OrdenRecaudacionDAO.cs`
- `Detalles.cshtml`
- `OrdenRecaudacionController.cs`

## Testing
Para probar la solución:
1. Crear una nueva orden de recaudación
2. Registrar un pago con información de banco
3. Ver los detalles de la orden
4. Verificar que la columna "Banco" muestra información apropiada

## Notas Técnicas
- La solución es backward-compatible
- No requiere cambios en la estructura de datos existente
- La lógica de inferencia funciona sin la columna banco en la BD
- Una vez agregada la columna, se usa directamente sin inferencia

## Próximos Pasos
1. Ejecutar el endpoint administrativo para agregar la columna
2. Verificar que los nuevos pagos almacenan correctamente el banco
3. Validar que la información se muestra correctamente en la interfaz