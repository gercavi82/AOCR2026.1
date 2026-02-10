# 📋 Documentación: Integración Empresas AS/400

## 🎯 Resumen

Se implementó integración con la tabla **CIAARC** del AS/400 para que los usuarios seleccionen su empresa operadora durante el registro. **Solo se guarda el código OACI** en PostgreSQL, manteniendo al AS/400 como fuente única.

---

## 📊 Arquitectura

### **Opción Implementada: Solo Código OACI** ✅

```
┌─────────────┐          ┌──────────────┐          ┌──────────────┐
│   AS/400    │          │  PostgreSQL  │          │   Usuario    │
│   CIAARC    │◄─────────┤    usuario   │◄─────────┤  Registro    │
│             │  Lookup  │              │  Guarda  │   (Modal)    │
│ - CIACOD    │          │ empresa_cod  │          │              │
│ - CIANOM    │          │              │          │              │
└─────────────┘          └──────────────┘          └──────────────┘
```

**Flujo:**
1. Usuario abre modal de registro
2. JavaScript carga empresas desde AS/400 via AJAX
3. Usuario selecciona empresa del dropdown
4. Se guarda **solo código OACI** en `usuario.empresa_codigo`
5. Cuando se necesite el nombre, se consulta AS/400 con ese código

---

## 🗄️ Cambios en Base de Datos

### **PostgreSQL - Tabla `usuario`**

```sql
-- Nuevas columnas agregadas
ALTER TABLE usuario ADD COLUMN empresa_codigo VARCHAR(5);
ALTER TABLE usuario ADD COLUMN ruta_documento_legal VARCHAR(500);
```

**Ejecutar script:**
```bash
psql -h 172.20.16.55 -U postgres -d dgac_des -f scripts/add_empresa_columns.sql
```

---

## 📁 Archivos Modificados

### **1. Capa de Datos**

#### `EmpresaAS400DAO.cs`
```csharp
// Método para listar todas las empresas activas
public List<Empresa> ObtenerEmpresas()
{
    // SELECT CIACOD, CIACO2, CIACO3, CIANOM 
    // FROM CIAARC 
    // WHERE CIAEST = 'AC'
}

// Método helper para obtener una empresa específica
public Empresa ObtenerEmpresaPorCodigo(string codigoOaci)
{
    // SELECT * FROM CIAARC WHERE TRIM(CIACOD) = ?
}
```

#### `UsuarioDAO.cs`
```csharp
// INSERT actualizado incluye empresa_codigo y ruta_documento_legal
INSERT INTO usuario
  (codigousuario, clave, correo, estadoactividad, nombreusuario, rol, 
   empresa_codigo, ruta_documento_legal, fechacreado)
VALUES
  (@CodigoUsuario, @Contrasena, @Email, '1', @NombreCompleto, @Rol,
   @EmpresaCodigo, @RutaDocumentoLegal, NOW())
```

**Métodos actualizados para leer nuevas columnas:**
- `ObtenerPorNombreUsuario()`
- `ObtenerPorId()`

---

### **2. Capa de Modelo**

#### `Usuario.cs`
```csharp
public class Usuario
{
    // ... propiedades existentes ...
    
    // Nuevas propiedades
    public string EmpresaCodigo { get; set; }        // Código OACI
    public string RutaDocumentoLegal { get; set; }  // Ruta al PDF
}
```

#### `Empresa.cs` (DTO en EmpresaAS400DAO.cs)
```csharp
public class Empresa
{
    public string CodigoOaci { get; set; }      // CIACOD
    public string CodigoIata { get; set; }      // CIACO2
    public string CodigoNumeroCia { get; set; } // CIACO3
    public string Nombre { get; set; }          // CIANOM
    
    public string Codigo => CodigoOaci; // Propiedad legacy
}
```

---

### **3. Capa de Presentación**

#### `EmpresaController.cs`
```csharp
// Endpoint 1: Listar todas las empresas (usado en modal de registro)
[HttpGet]
public JsonResult ObtenerEmpresas()

// Endpoint 2: Obtener empresa específica por código
[HttpGet]
public JsonResult ObtenerEmpresaPorCodigo(string codigo)
```

#### `UsuarioController.cs`
```csharp
[HttpPost]
public JsonResult Crear()
{
    var empresaCodigo = Request.Form["EmpresaCodigo"];
    var documento = Request.Files["DocumentoRepresentante"];
    
    // ... validaciones ...
    
    nuevoUsuario.EmpresaCodigo = empresaCodigo;
    nuevoUsuario.RutaDocumentoLegal = rutaDocumento;
}
```

#### `_ModalCrearUsuario.cshtml`
```html
<!-- Dropdown de empresas -->
<select id="EmpresaCodigo" name="EmpresaCodigo" required>
    <option value="">-- Seleccione una empresa --</option>
    <!-- Poblado desde AS/400 via AJAX -->
</select>

<!-- JavaScript -->
<script>
function cargarEmpresasOperadoras() {
    $.ajax({
        url: '@Url.Action("ObtenerEmpresas", "Empresa")',
        success: function(data) {
            // Formato: [OACI/IATA] Nombre Empresa
            // Ejemplo: [T01/AV] TAME LÍNEA AÉREA DEL ECUADOR
        }
    });
}
</script>
```

---

## 🔧 Uso en Código

### **Obtener nombre de empresa de un usuario**

```csharp
// Opción 1: Consultar AS/400 cuando lo necesites
var usuario = UsuarioDAO.ObtenerPorId(userId);
if (!string.IsNullOrEmpty(usuario.EmpresaCodigo))
{
    var dao = new EmpresaAS400DAO();
    var empresa = dao.ObtenerEmpresaPorCodigo(usuario.EmpresaCodigo);
    string nombreEmpresa = empresa?.Nombre ?? "Empresa no encontrada";
}

// Opción 2: Desde JavaScript (en la vista)
$.get('@Url.Action("ObtenerEmpresaPorCodigo", "Empresa")', 
    { codigo: empresaCodigo }, 
    function(empresa) {
        console.log(empresa.Nombre);
    }
);
```

### **Ejemplo en Razor View**

```cshtml
@if (!string.IsNullOrEmpty(Model.EmpresaCodigo))
{
    <p>
        Empresa: <span id="nombreEmpresa">Cargando...</span>
    </p>
    
    <script>
        $.get('@Url.Action("ObtenerEmpresaPorCodigo", "Empresa")', 
            { codigo: '@Model.EmpresaCodigo' },
            function(data) {
                $('#nombreEmpresa').text('[' + data.CodigoOaci + '] ' + data.Nombre);
            }
        );
    </script>
}
```

---

## 📈 Ventajas de esta Arquitectura

✅ **Simple**: No hay sincronización compleja  
✅ **Fuente única**: AS/400 es siempre la verdad  
✅ **Liviano**: PostgreSQL solo almacena 5 caracteres  
✅ **Escalable**: Fácil agregar más datos del AS/400 cuando lo necesites  
✅ **Auditable**: Sabes exactamente qué empresa seleccionó el usuario  

---

## 🚀 Testing

### **1. Probar carga de empresas**
```javascript
// En consola del navegador (F12)
$.get('/Empresa/ObtenerEmpresas', function(data) {
    console.table(data);
});
```

### **2. Probar consulta individual**
```javascript
$.get('/Empresa/ObtenerEmpresaPorCodigo', { codigo: 'T01' }, 
    function(data) {
        console.log(data);
    }
);
```

### **3. Verificar registro de usuario**
```sql
-- Verificar en PostgreSQL
SELECT 
    codigousuario, 
    nombreusuario, 
    empresa_codigo,
    ruta_documento_legal
FROM usuario 
WHERE empresa_codigo IS NOT NULL
ORDER BY fechacreado DESC
LIMIT 10;
```

---

## 📝 Notas Importantes

1. **Columnas en PostgreSQL**: Ejecutar `scripts/add_empresa_columns.sql` antes de usar
2. **AS/400 debe estar disponible**: Sin conexión, el dropdown estará vacío
3. **Validación**: Empresa es campo **requerido** en el registro
4. **Formato código**: CIACOD en AS/400 es VARCHAR(5), se almacena sin espacios
5. **Empresas activas**: Solo se muestran empresas con `CIAEST = 'AC'`

---

## 🔍 Troubleshooting

### Problema: Dropdown vacío
```
✓ Verificar conexión AS/400 (190.152.8.185)
✓ Verificar tabla CIAARC tiene registros activos
✓ Ver consola del navegador (F12) para errores AJAX
✓ Verificar que EmpresaController.ObtenerEmpresas() no lanza excepciones
```

### Problema: Error al guardar usuario
```
✓ Verificar columnas existen: empresa_codigo, ruta_documento_legal
✓ Ejecutar script: scripts/add_empresa_columns.sql
✓ Verificar que archivo PDF se sube correctamente
```

---

## 📞 Contacto

Para dudas o mejoras en esta integración, consultar con el equipo de desarrollo.

---

**Última actualización:** 9 de febrero, 2026
