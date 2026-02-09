# Sistema de Notificaciones Básicas AOCR

## 📋 Resumen de Implementación

Sistema completo de notificaciones en tiempo real para AOCR con **4 niveles** (INFO, SUCCESS, WARNING, ERROR), **6 categorías** (Solicitud, Inspección, Pago, Documento, Certificado, Sistema) y **15 tipos de eventos**.

---

## ✅ Archivos Creados

### 1. **Constants/TiposNotificacion.cs** (CapaDatos)
Define tipos formalizados de notificaciones:
```csharp
// 4 niveles
public const string INFO = "INFO";
public const string SUCCESS = "SUCCESS";
public const string WARNING = "WARNING";
public const string ERROR = "ERROR";

// 6 categorías
public const string CATEGORIA_SOLICITUD = "SOLICITUD";
public const string CATEGORIA_INSPECCION = "INSPECCION";
// ...

// 15 eventos
public const string SOLICITUD_NUEVA = "solicitud.nueva";
public const string INSPECCION_PROGRAMADA = "inspeccion.programada";
// ...

// Plantillas estáticas
public static class Plantillas {
    public static string SolicitudNueva(int id) => $"Se ha registrado una nueva solicitud AOCR #{id}";
    public static string InspeccionProgramada(int id, DateTime fecha) => ...;
}

// Helpers
public static bool RequiereEmail(string evento);
public static bool EsCritica(string evento);
public static int ObtenerTiempoVida(string nivel);
public static string ObtenerColorBadge(string nivel);
public static string ObtenerIcono(string nivel);
```

### 2. **NotificacionController.cs** (CapaPresentacion/Controllers)
API REST para notificaciones con **9 endpoints**:

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/Notificacion/Index` | GET | Panel de notificaciones |
| `/Notificacion/ObtenerNoLeidas` | GET | JSON con notificaciones no leídas |
| `/Notificacion/ObtenerRecientes` | GET | JSON con últimas N notificaciones |
| `/Notificacion/ContarNoLeidas` | GET | JSON con contador {cantidad: N} |
| `/Notificacion/MarcarComoLeida` | POST | Marca 1 como leída |
| `/Notificacion/MarcarTodasComoLeidas` | POST | Marca todas del usuario |
| `/Notificacion/Eliminar` | POST | Elimina 1 notificación |
| `/Notificacion/EliminarTodas` | POST | Elimina todas leídas (Admin) |
| `/Notificacion/Enviar` | POST | Envío manual (Admin/Testing) |

### 3. **Views/Notificacion/Index.cshtml**
Panel completo de notificaciones con:
- Lista de notificaciones con badges por tipo
- Diferenciación visual leídas/no leídas
- Click para redirigir a URL de acción
- Botón "Marcar todas leídas"
- Botón "Limpiar leídas"
- Responsive con AdminLTE

### 4. **Views/Shared/_NotificacionesBadge.cshtml**
Partial View para navbar con:
- Campana con badge contador (rojo)
- Dropdown menu con últimas 10 notificaciones
- Auto-refresh cada 30 segundos
- Click en notificación → marca como leída + redirige
- Link "Ver todas" al panel completo

### 5. **NotificacionDAO.cs - Actualizado**
Agregado método:
```csharp
public static bool EliminarTodasLeidas(int codigoUsuario)
```

### 6. **NotificacionBL.cs - Actualizado**
Agregado método:
```csharp
public static bool EliminarTodasLeidas(int codigoUsuario, out string mensaje)
```

### 7. **scripts/mejoras_tabla_notificaciones.sql**
Migración SQL con:
- CHECK constraint para `tipo` (INFO/SUCCESS/WARNING/ERROR)
- 4 índices compuestos para optimizar consultas
- Función `limpiar_notificaciones_antiguas(dias)`
- Vista `vw_notificaciones_resumen` con estadísticas
- Trigger para validar usuario existe
- Normalización de datos legacy

---

## 🚀 Pasos de Integración

### **PASO 1: Ejecutar Migración SQL**

```powershell
# Desde raíz del proyecto
cd scripts

# Linux/Mac
psql -h 172.20.16.55 -U dgac_admin -d dgac_des -f mejoras_tabla_notificaciones.sql

# Windows con contraseña en pipe
echo control | psql -h 172.20.16.55 -U dgac_admin -d dgac_des -f mejoras_tabla_notificaciones.sql
```

✅ **Verificación:**
```sql
-- Ver constraint CHECK
SELECT constraint_name, check_clause 
FROM information_schema.check_constraints
WHERE constraint_name = 'chk_notificacion_tipo';

-- Ver índices creados
SELECT indexname FROM pg_indexes 
WHERE tablename = 'aocr_tbnotificacion'
ORDER BY indexname;

-- Ver vista resumen
SELECT * FROM vw_notificaciones_resumen LIMIT 5;
```

---

### **PASO 2: Agregar Badge al _Layout.cshtml**

Ubicar el `<ul class="navbar-nav ml-auto">` en el navbar y agregar:

```cshtml
<!-- En Views/Shared/_Layout.cshtml -->
<ul class="navbar-nav ml-auto">
    
    @* AGREGAR ESTE PARTIAL *@
    @Html.Partial("_NotificacionesBadge")
    
    <!-- Otros elementos del navbar (usuario, logout, etc.) -->
    <li class="nav-item dropdown">
        <a class="nav-link" data-toggle="dropdown" href="#">
            <i class="fa fa-user"></i> @Session["NombreUsuario"]
        </a>
        <!-- ... -->
    </li>
</ul>
```

**Screenshot esperado:**
```
┌─────────────────────────────────────────────────────┐
│ AOCR Sistema    [🔔 5] [👤 Juan Pérez ▼] [Salir] │
└─────────────────────────────────────────────────────┘
```

---

### **PASO 3: Agregar Ruta al Menú (Opcional)**

En el menú lateral, agregar enlace al panel de notificaciones:

```cshtml
<li class="nav-item">
    <a href="@Url.Action("Index", "Notificacion")" class="nav-link">
        <i class="nav-icon fa fa-bell"></i>
        <p>
            Notificaciones
            <span class="badge badge-danger right" id="menuBadgeNotif">0</span>
        </p>
    </a>
</li>
```

Actualizar el badge del menú con jQuery:
```javascript
// En _Layout.cshtml después del script de _NotificacionesBadge
<script>
    setInterval(function() {
        $.get('@Url.Action("ContarNoLeidas", "Notificacion")', function(data) {
            if (data.success && data.cantidad > 0) {
                $('#menuBadgeNotif').text(data.cantidad).show();
            } else {
                $('#menuBadgeNotif').hide();
            }
        });
    }, 30000);
</script>
```

---

### **PASO 4: Integrar Notificaciones Automáticas**

En los controladores donde ocurran eventos importantes, usar `NotificacionBL`:

#### **Ejemplo 1: Nueva Solicitud AOCR**
```csharp
// En SolicitudAOCRController.cs
[HttpPost]
public ActionResult Recepcionar(SolicitudAOCR model)
{
    // ... validaciones ...
    
    int idSolicitud = SolicitudAOCRBL.Crear(model, out mensaje);
    
    if (idSolicitud > 0)
    {
        // ✅ ENVIAR NOTIFICACIÓN AL SOLICITANTE
        NotificacionBL.EnviarNotificacion(
            codigoUsuario: model.CodigoUsuario,
            titulo: TiposNotificacion.Plantillas.SolicitudNueva(idSolicitud),
            mensaje: "Tu solicitud ha sido recepcionada y está en proceso de revisión.",
            tipo: TiposNotificacion.SUCCESS,
            url: TiposNotificacion.Urls.Solicitud(idSolicitud)
        );
        
        // ✅ NOTIFICAR A OPERADORES/EVALUADORES
        var operadores = UsuarioBL.ObtenerPorRol("Operador");
        foreach (var op in operadores)
        {
            NotificacionBL.EnviarNotificacion(
                codigoUsuario: op.CodigoUsuario,
                titulo: "Nueva solicitud AOCR para revisar",
                mensaje: $"Solicitud #{idSolicitud} requiere evaluación",
                tipo: TiposNotificacion.INFO,
                url: TiposNotificacion.Urls.Solicitud(idSolicitud)
            );
        }
    }
    
    return RedirectToAction("MisSolicitudes");
}
```

#### **Ejemplo 2: Cambio de Estado**
```csharp
// En SolicitudAOCRController.cs
[HttpPost]
public ActionResult Aprobar(int id)
{
    bool resultado = SolicitudAOCRBL.CambiarEstado(id, EstadosSolicitudAOCR.APROBADA, out mensaje);
    
    if (resultado)
    {
        var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
        
        // ✅ NOTIFICAR AL SOLICITANTE
        NotificacionBL.EnviarNotificacion(
            codigoUsuario: solicitud.CodigoUsuario,
            titulo: TiposNotificacion.Plantillas.SolicitudCambioEstado(id, "APROBADA"),
            mensaje: "Tu solicitud AOCR ha sido aprobada. Puedes continuar con las inspecciones.",
            tipo: TiposNotificacion.SUCCESS,
            url: TiposNotificacion.Urls.Solicitud(id)
        );
    }
    
    return Json(new { success = resultado, message = mensaje });
}
```

#### **Ejemplo 3: Inspección Programada**
```csharp
// En InspeccionController.cs
[HttpPost]
public ActionResult Programar(Inspeccion model)
{
    int idInspeccion = InspeccionBL.Crear(model, out mensaje);
    
    if (idInspeccion > 0)
    {
        // ✅ NOTIFICAR AL INSPECTOR ASIGNADO
        NotificacionBL.EnviarNotificacion(
            codigoUsuario: model.CodigoInspector,
            titulo: TiposNotificacion.Plantillas.InspeccionProgramada(idInspeccion, model.FechaProgramada.Value),
            mensaje: $"Tienes una inspección programada para el {model.FechaProgramada:dd/MM/yyyy}",
            tipo: TiposNotificacion.WARNING,
            url: TiposNotificacion.Urls.Inspeccion(idInspeccion)
        );
        
        // ✅ NOTIFICAR AL OPERADOR QUE SOLICITÓ
        var solicitud = SolicitudAOCRBL.ObtenerPorId(model.CodigoSolicitud);
        NotificacionBL.EnviarNotificacion(
            codigoUsuario: solicitud.CodigoUsuario,
            titulo: "Inspección programada",
            mensaje: $"Se ha programado la inspección para tu solicitud #{model.CodigoSolicitud}",
            tipo: TiposNotificacion.INFO,
            url: TiposNotificacion.Urls.Inspeccion(idInspeccion)
        );
    }
    
    return Json(new { success = true, id = idInspeccion });
}
```

#### **Ejemplo 4: Pago Registrado**
```csharp
// En PagoController.cs
[HttpPost]
public ActionResult RegistrarPago(Pago model)
{
    bool resultado = PagoBL.Registrar(model, out mensaje);
    
    if (resultado)
    {
        // ✅ NOTIFICAR AL USUARIO
        NotificacionBL.EnviarNotificacion(
            codigoUsuario: model.CodigoUsuario,
            titulo: TiposNotificacion.Plantillas.PagoRecibido(model.CodigoPago, model.Monto),
            mensaje: $"Se ha registrado tu pago de ${model.Monto:N2} para la solicitud #{model.CodigoSolicitud}",
            tipo: TiposNotificacion.SUCCESS,
            url: TiposNotificacion.Urls.Pago(model.CodigoPago)
        );
    }
    
    return Json(new { success = resultado, message = mensaje });
}
```

---

## 🔍 Testing Manual

### **Test 1: Badge en Navbar**
1. Login al sistema
2. Verificar que aparece la campana 🔔 en el navbar
3. Si hay notificaciones, debe mostrar badge rojo con número
4. Click en campana → debe abrir dropdown con lista

### **Test 2: Crear Notificación Manual**
```sql
-- Insertar notificación de prueba
INSERT INTO aocr_tbnotificacion 
    (codigo_usuario, titulo, mensaje, tipo, url_accion)
VALUES 
    (1, 'Test Notificación', 'Este es un mensaje de prueba', 'INFO', '/Solicitud/Index');
```

Refresh navegador → debe aparecer badge con contador `1`

### **Test 3: API Endpoints**
```javascript
// En consola del navegador (F12)

// Obtener contador
fetch('/Notificacion/ContarNoLeidas')
    .then(r => r.json())
    .then(data => console.log('Contador:', data));

// Obtener notificaciones
fetch('/Notificacion/ObtenerRecientes?cantidad=5')
    .then(r => r.json())
    .then(data => console.log('Notificaciones:', data));

// Marcar como leída (cambiar ID)
fetch('/Notificacion/MarcarComoLeida', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: 'id=1'
})
.then(r => r.json())
.then(data => console.log('Marcada:', data));
```

### **Test 4: Verificar Tipos Inválidos Fallan**
```sql
-- ❌ Debe FALLAR por CHECK constraint
INSERT INTO aocr_tbnotificacion 
    (codigo_usuario, titulo, mensaje, tipo)
VALUES 
    (1, 'Test', 'Mensaje', 'INVALID_TYPE');

-- Error esperado: new row violates check constraint "chk_notificacion_tipo"
```

---

## 📊 Estructura de Base de Datos

### **Tabla: aocr_tbnotificacion**
```sql
Column           | Type         | Nullable | Default
-----------------+--------------+----------+-------------------
codigo_notificacion | INTEGER   | NOT NULL | nextval(...)
codigo_usuario      | INTEGER   | NOT NULL |
titulo              | VARCHAR(200) | NOT NULL |
mensaje             | TEXT      | NOT NULL |
tipo                | VARCHAR(50) | NOT NULL | -- CHECK constraint
leida               | BOOLEAN   | NOT NULL | false
fecha_lectura       | TIMESTAMP | NULL     |
url_accion          | VARCHAR(500) | NULL  |
icono               | VARCHAR(50) | NULL   |
created_at          | TIMESTAMP | NOT NULL | CURRENT_TIMESTAMP
```

### **Índices:**
```sql
idx_notificacion_usuario          (codigo_usuario)
idx_notificacion_leida            (leida)
idx_notificacion_usuario_leida    (codigo_usuario, leida) WHERE leida = FALSE
idx_notificacion_usuario_tipo     (codigo_usuario, tipo)
idx_notificacion_created_at       (created_at DESC)
```

### **Vista: vw_notificaciones_resumen**
```sql
SELECT 
    codigo_usuario,
    COUNT(*) AS total_notificaciones,
    COUNT(*) FILTER (WHERE leida = FALSE) AS no_leidas,
    COUNT(*) FILTER (WHERE tipo = 'WARNING') AS tipo_warning,
    MAX(created_at) AS ultima_notificacion
FROM aocr_tbnotificacion
GROUP BY codigo_usuario;
```

---

## 🎨 Personalización de Colores

En `TiposNotificacion.cs`:

```csharp
public static string ObtenerColorBadge(string nivel)
{
    switch (nivel?.ToUpper())
    {
        case INFO:    return "badge-info";      // Azul
        case SUCCESS: return "badge-success";   // Verde
        case WARNING: return "badge-warning";   // Amarillo
        case ERROR:   return "badge-danger";    // Rojo
        default:      return "badge-secondary"; // Gris
    }
}

public static string ObtenerIcono(string nivel)
{
    switch (nivel?.ToUpper())
    {
        case INFO:    return "fa fa-info-circle";
        case SUCCESS: return "fa fa-check-circle";
        case WARNING: return "fa fa-exclamation-triangle";
        case ERROR:   return "fa fa-times-circle";
        default:      return "fa fa-bell";
    }
}
```

---

## 🔧 Mantenimiento

### **Limpiar Notificaciones Antiguas (Automático)**
```sql
-- Ejecutar como tarea programada (cron/scheduled task)
-- Elimina notificaciones leídas con +90 días
SELECT limpiar_notificaciones_antiguas(90);
```

### **Limpiar desde Código C#**
```csharp
// En NotificacionBL.cs
public static bool LimpiarNotificacionesAntiguas(int diasAntiguedad, out string mensaje)
{
    try
    {
        using (var cn = new NpgsqlConnection(connectionString))
        using (var cmd = new NpgsqlCommand("SELECT limpiar_notificaciones_antiguas(@dias)", cn))
        {
            cmd.Parameters.AddWithValue("@dias", diasAntiguedad);
            cn.Open();
            int eliminadas = Convert.ToInt32(cmd.ExecuteScalar());
            mensaje = $"Se eliminaron {eliminadas} notificaciones antiguas.";
            return true;
        }
    }
    catch (Exception ex)
    {
        mensaje = "Error: " + ex.Message;
        return false;
    }
}
```

---

## 📌 Próximos Pasos (Opcional)

1. **Email Notifications**: Integrar con `EmailHelper.cs` para enviar correos desde `NotificacionBL.EnviarNotificacion()` cuando `TiposNotificacion.RequiereEmail(evento) == true`

2. **WebSockets/SignalR**: Para notificaciones push en tiempo real sin necesidad de polling cada 30 segundos

3. **Centro de Preferencias**: Permitir al usuario configurar qué tipos de notificaciones recibe (email, web, ambas)

4. **Historial Completo**: Vista de administrador con todas las notificaciones del sistema + filtros avanzados

5. **Notificaciones de Sistema**: Agregar eventos como "Mantenimiento programado", "Nueva versión disponible", etc.

---

## ✅ Checklist de Implementación

- [x] Crear `TiposNotificacion.cs` con constantes
- [x] Crear `NotificacionController.cs` con 9 endpoints
- [x] Crear vista `Index.cshtml` para panel
- [x] Crear partial `_NotificacionesBadge.cshtml`
- [x] Agregar `EliminarTodasLeidas()` en DAO y BL
- [x] Crear script SQL `mejoras_tabla_notificaciones.sql`
- [ ] **Ejecutar migración SQL**
- [ ] **Agregar `@Html.Partial("_NotificacionesBadge")` a _Layout.cshtml**
- [ ] **Integrar notificaciones en SolicitudAOCRController**
- [ ] **Integrar notificaciones en InspeccionController**
- [ ] **Integrar notificaciones en PagoController**
- [ ] **Testing manual de badge y dropdown**
- [ ] **Testing de API endpoints**
- [ ] **Configurar tarea programada para limpiar notificaciones antiguas**

---

## 📚 Documentación Técnica

- **Tipos de Notificación**: Ver `TiposNotificacion.cs` para lista completa de eventos
- **Plantillas de Mensajes**: `TiposNotificacion.Plantillas` para mensajes estandarizados
- **URLs de Redirección**: `TiposNotificacion.Urls` para construir links a páginas específicas
- **Helpers**: `RequiereEmail()`, `EsCritica()`, `ObtenerTiempoVida()` para lógica de negocio

---

**🎉 Sistema de notificaciones básicas AOCR implementado correctamente!**
