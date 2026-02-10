# 🔍 Diagnóstico: Conexión AS/400 - Modal Empresas

## ❌ Problema Reportado
El dropdown de empresas en el modal de registro aparece vacío - no carga datos desde AS/400 CIAARC.

---

## 🔎 Checklist de Diagnóstico

### **1. ¿Está instalado el driver ODBC de IBM i?**

```powershell
# Verificar drivers ODBC instalados
Get-OdbcDriver | Where-Object {$_.Name -like "*IBM*"}
```

**Resultado esperado:**
```
Name                          Platform
----                          --------
IBM i Access ODBC Driver      64-bit
```

**Si NO aparece:**
- Descargar e instalar **IBM i Access Client Solutions**
- URL: https://www.ibm.com/support/pages/ibm-i-access-client-solutions
- Ejecutar instalador y seleccionar componente "ODBC Driver"

---

### **2. ¿Está configurada la conexión en Web.config?**

Verificar archivo: `CapaPresentacion\Web.config`

```xml
<connectionStrings>
  <!-- AS/400 - Debe estar presente -->
  <add name="AS400" 
       connectionString="Driver={IBM i Access ODBC Driver};System=190.152.8.185;..." />
</connectionStrings>
```

**O usando credenciales del servicio:**
```xml
<appSettings>
  <add key="AS400:Server" value="190.152.8.185" />
  <add key="AS400:Database" value="biblioteca_as400" />
  <add key="AS400:UserId" value="usuario_encriptado" />
  <add key="AS400:Password" value="password_encriptado" />
</appSettings>
```

---

### **3. Probar conexión desde PowerShell**

```powershell
# Test de conexión ODBC básica
$connectionString = "Driver={IBM i Access ODBC Driver};System=190.152.8.185;Database=biblioteca;Uid=user;Pwd=pass;"

try {
    $connection = New-Object System.Data.Odbc.OdbcConnection($connectionString)
    $connection.Open()
    Write-Host "✅ Conexión exitosa al AS/400" -ForegroundColor Green
    
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) FROM CIAARC WHERE CIAEST='AC'"
    $result = $command.ExecuteScalar()
    Write-Host "✅ Empresas activas encontradas: $result" -ForegroundColor Green
    
    $connection.Close()
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}
```

---

### **4. Verificar logs de IIS Express**

**Abrir consola del navegador (F12) → Network tab:**

1. Recargar página de Login
2. Abrir modal de registro  
3. Buscar request a `/Empresa/ObtenerEmpresas`

**Estados posibles:**
- ✅ **200 OK con datos**: Funciona correctamente
- ❌ **500 Internal Server Error**: Ver response para mensaje de error
- ❌ **404 Not Found**: Controlador no encontrado (problema de routing)
- ⏱️ **Timeout**: Conexión lenta o bloqueada por firewall

---

### **5. Habilitar logs detallados temporalmente**

Agregar en `EmpresaAS400DAO.cs → ObtenerEmpresas()`:

```csharp
public List<Empresa> ObtenerEmpresas()
{
    var empresas = new List<Empresa>();
    
    try
    {
        System.Diagnostics.Debug.WriteLine("🔍 Intentando conectar al AS/400...");
        
        using (var conn = GetConnection())
        {
            System.Diagnostics.Debug.WriteLine($"🔗 Connection String: {conn.ConnectionString.Replace("Pwd=", "Pwd=***")}");
            
            conn.Open();
            System.Diagnostics.Debug.WriteLine("✅ Conexión abierta exitosamente");
            
            string query = @"
                SELECT CIACOD, CIACO2, CIACO3, CIANOM 
                FROM CIAARC 
                WHERE CIAEST = 'AC'
                ORDER BY CIANOM";
            
            System.Diagnostics.Debug.WriteLine($"📝 Query: {query}");
            
            using (var cmd = new OdbcCommand(query, conn))
            using (var reader = cmd.ExecuteReader())
            {
                int count = 0;
                while (reader.Read())
                {
                    empresas.Add(new Empresa {...});
                    count++;
                }
                System.Diagnostics.Debug.WriteLine($"✅ {count} empresas leídas");
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"❌ ERROR: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
        throw;
    }
    
    return empresas;
}
```

**Ver Output en Visual Studio:**
- View → Output
- Seleccionar "Debug" en dropdown

---

### **6. Probar endpoint directamente**

Abrir navegador:
```
https://localhost:44333/Empresa/ObtenerEmpresas
```

**Respuesta esperada (JSON):**
```json
[
  {
    "CodigoOaci": "T01",
    "CodigoIata": "EQ",
    "CodigoNumeroCia": "00001",
    "Nombre": "TAME LINEA AEREA DEL ECUADOR",
    "Codigo": "T01"
  },
  ...
]
```

**Errores comunes:**
- `"error": "Fallo conexión AS400: ..."` → Ver mensaje específico
- `[SQL0204] CIAARC not found` → Tabla no existe o biblioteca incorrecta
- `[SQL0901] SQL system error` → Problema de permisos o sintaxis
- `Connection timeout` → Firewall bloqueando puerto

---

### **7. Verificar biblioteca correcta en AS/400**

La tabla CIAARC debe estar en la biblioteca configurada:

```sql
-- Desde un cliente AS/400 (o ACS Run SQL Scripts)
SELECT TABLE_SCHEMA, TABLE_NAME 
FROM QSYS2.SYSTABLES 
WHERE TABLE_NAME = 'CIAARC';
```

**Asegurarse que la biblioteca coincida con Web.config:**
```xml
<add key="AS400:Database" value="BIBLIOTECA_CORRECTA" />
```

---

### **8. Problemas conocidos y soluciones**

| Síntoma | Causa | Solución |
|---------|-------|----------|
| Dropdown vacío, sin errores en consola | AJAX success pero data = [] | Verificar condición `WHERE CIAEST='AC'` - puede que no haya registros activos |
| Error "Driver not found" | IBM i Access no instalado | Instalar IBM i Access Client Solutions con ODBC |
| Error "[SQL0204]" | Tabla en biblioteca incorrecta | Cambiar `Database` en connection string |
| Timeout después de 10 seg | Firewall o red lenta | Aumentar timeout o verificar conectividad de red |
| Error 500 sin detalles | Excepción capturada silenciosamente | Agregar logs detallados (ver paso 5) |

---

## 🚀 Solución Rápida: Modo de prueba con datos mock

Si AS/400 no está disponible temporalmente, modificar `EmpresaController.cs`:

```csharp
[HttpGet]
public JsonResult ObtenerEmpresas()
{
    try
    {
        var dao = new EmpresaAS400DAO();
        var empresas = dao.ObtenerEmpresas();
        
        // 🧪 TEMPORAL: Si no hay datos del AS/400, usar mock
        if (empresas == null || empresas.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("⚠️ AS/400 sin datos, usando MOCK");
            empresas = new List<CapaDatos.DAOs.Empresa>
            {
                new CapaDatos.DAOs.Empresa { 
                    CodigoOaci = "TEST", 
                    CodigoIata = "TS", 
                    Nombre = "EMPRESA DE PRUEBA - AS/400 NO DISPONIBLE" 
                }
            };
        }
        
        return Json(empresas, JsonRequestBehavior.AllowGet);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"❌ Error completo: {ex}");
        Response.StatusCode = 500;
        return Json(new { 
            error = "Fallo conexión AS400: " + ex.Message,
            details = ex.ToString() 
        }, JsonRequestBehavior.AllowGet);
    }
}
```

---

## 📊 Verificación Final

Después de aplicar correcciones, verificar:

1. ✅ Modal de registro se abre correctamente
2. ✅ Dropdown muestra "Cargando empresas..." (1-2 segundos)
3. ✅ Dropdown se llena con empresas en formato: `[OACI/IATA] Nombre`
4. ✅ Seleccionar empresa y completar registro funciona
5. ✅ PostgreSQL guarda `empresa_codigo` correctamente

---

**Fecha:** 9 de febrero, 2026  
**Estado:** Pendiente diagnóstico de conexión AS/400
