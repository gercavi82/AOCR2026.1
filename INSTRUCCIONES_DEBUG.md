# 🔍 Cómo Ver los Logs del Error 500

## Método 1: Visual Studio Output Window (RECOMENDADO)

### Pasos:
1. **Abre tu solución en Visual Studio**
2. **Inicia la aplicación en modo Debug** (F5)
3. **Ve a**: `View` → `Output` (o presiona `Ctrl+Alt+O`)
4. **En el dropdown "Show output from:"**, selecciona **"Debug"**
5. **Reproduce el error**:
   - Ve a `/OrdenRecaudacion/Nueva`
   - Llena el formulario
   - Haz clic en "Guardar"
6. **En la ventana Output**, verás mensajes como:
   ```
   === DAO Insertar ===
   Compania: Ejemplo SA
   Conexion abierta
   INSERT exitoso, ID: 123
   ```
   O si hay error:
   ```
   Insertar ERROR: column "xxx" does not exist
   StackTrace: at Npgsql...
   ```

## Método 2: Navegador (Error Detallado)

Tu Web.config tiene `customErrors mode="Off"`, así que verás errores detallados:

1. **Abre el navegador**
2. **Presiona F12** (Developer Tools)
3. **Ve a la pestaña "Network"**
4. **Reproduce el error**
5. **Haz clic en la petición POST** (la que tiene estado 500)
6. **Ve a la pestaña "Response"** para ver:
   - Tipo de excepción
   - Mensaje de error
   - Stack trace completo

### Ejemplo de lo que verás:
```
Server Error in '/' Application.

NullReferenceException: Object reference not set to an instance of an object.

Description: An unhandled exception occurred during the execution of the current web request.

Exception Details: System.NullReferenceException: Object reference not set to an instance of an object.

Source Error:
Line 217:         var orden = new OrdenRecaudacion
Line 218:         {
Line 219:             NumeroOrden = numeroOrden,

Stack Trace:
[NullReferenceException: Object reference not set to an instance of an object.]
   CapaPresentacion.Controllers.OrdenRecaudacionController.Nueva(OrdenRecaudacionNuevaVM model) in C:\...\OrdenRecaudacionController.cs:line 219
```

## Método 3: Event Viewer de Windows

### Comando PowerShell:
```powershell
Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 10 | 
    Select-Object TimeGenerated, EntryType, Message | 
    Format-List
```

O abre el Event Viewer gráficamente:
1. Presiona `Win + R`
2. Escribe `eventvwr.msc`
3. Ve a: `Windows Logs` → `Application`
4. Busca errores de "ASP.NET" recientes

## Método 4: Logs de IIS Express

Si usas Visual Studio, los logs están en:
```
C:\Users\[TuUsuario]\Documents\IISExpress\TraceLogFiles\
```

Para ver el último log:
```powershell
Get-ChildItem "$env:USERPROFILE\Documents\IISExpress\TraceLogFiles" -File | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 | 
    Get-Content -Tail 100
```

## Método 5: Agregar Más Logs en el Código

Si necesitas más detalle, agrega estos logs temporalmente:

### En OrdenRecaudacionController.cs (línea ~130):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Nueva(OrdenRecaudacionNuevaVM model)
{
    System.Diagnostics.Debug.WriteLine("=== INICIO Nueva POST ===");
    System.Diagnostics.Debug.WriteLine($"Model recibido: {model != null}");
    System.Diagnostics.Debug.WriteLine($"DetallesJson: {model?.DetallesJson}");
    
    try
    {
        // ... resto del código
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("ERROR COMPLETO:");
        System.Diagnostics.Debug.WriteLine(ex.ToString()); // ToString() da más info que Message
        throw; // Re-lanzar para que aparezca en el navegador
    }
}
```

## ⚠️ Errores Comunes y Sus Mensajes

| Error que verías | Causa probable |
|-----------------|----------------|
| `Object reference not set to an instance of an object` | `_dao` o `_conceptoDao` es null (fallo en constructor) |
| `column "NombreOperador" does not exist` | Nombres de columnas con mayúsculas incorrectas |
| `relation "AocrOrOrden" does not exist` | Nombre de tabla con mayúsculas incorrectas |
| `cannot insert NULL into column` | Falta un campo requerido en la tabla |
| `timeout expired` | Conexión a PostgreSQL fallida o lenta |
| `A deadlock occurred` | Uso de `.Result` en código async |

## 🎯 Próximos Pasos

1. **Reproduce el error** con Visual Studio en modo Debug
2. **Copia el error completo** de la ventana Output o del navegador
3. **Comparte el mensaje de error** para que pueda ayudarte a solucionarlo

---
Fecha: Febrero 2, 2026
