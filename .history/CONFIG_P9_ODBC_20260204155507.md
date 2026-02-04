# CONFIGURACIÓN DEL ORIGEN DE DATOS ODBC PARA P9/AS400

## Problema Detectado
```
Error en ListaValores: ERROR [IM002] [Microsoft][Administrador de controladores ODBC] No se encuentra el nombre del origen de datos y no se especificó ningún controlador predeterminado
```

## Solución: Configurar DSN ODBC para P9

### Opción 1: DSN del Sistema (Recomendado)
1. Abrir "Administrador de orígenes de datos ODBC" como administrador
2. Ir a la pestaña "DSN del sistema"
3. Hacer clic en "Agregar"
4. Seleccionar "IBM i Access ODBC Driver" o "IBM DB2 for i (ODBC)"
5. Configurar:
   ```
   Nombre del origen de datos: P9_AOCR
   Descripción: Conexión a base de datos P9 para AOCR
   Sistema: [IP del servidor P9]
   Puerto: 446 (por defecto para DB2/400)
   Base de datos: DGACSYS
   ```

### Opción 2: Cadena de Conexión Directa
Modificar `AS400BaseDAO.cs` para usar cadena de conexión directa:

```csharp
// En lugar de DSN, usar cadena completa
string connectionString = $"DRIVER={{IBM i Access ODBC Driver}};" +
                         $"SYSTEM={servidor};" +
                         $"UID={usuario};" +
                         $"PWD={password};" +
                         $"DBQ=DGACSYS;" +
                         $"NAMING=1;" +
                         $"TRANSLATE=1";
```

### Verificación
Después de configurar, probar la conexión con:
```
Test-OdbcConnection -Name "P9_AOCR"
```

### Configuración en webServerApiSettings.json
```json
{
  "AS400Connection": {
    "Server": "IP_DEL_SERVIDOR_P9",
    "Database": "DGACSYS", 
    "Username": "usuario_p9",
    "Password": "password_encriptado",
    "DSN": "P9_AOCR"
  }
}
```