# 📧 Guía de Implementación de Envío de Correos - Sistema AOCR

## 📋 Resumen

Esta guía documenta la implementación del sistema de envío de correos electrónicos en el Sistema AOCR usando el servidor SMTP interno de la DGAC.

## 🔧 Configuración Realizada

### 1. Archivo Web.config

Se ha configurado el servidor SMTP interno:

```xml
<!-- Configuración de correo -->
<add key="SmtpServer" value="172.20.16.21" />
<add key="EmailFrom" value="no_reply@aviacioncivil.gob.ec" />
<add key="EmailFromName" value="Sistema AOCR" />

<!-- Configuración SMTP detallada -->
<add key="Email:SmtpServer" value="172.20.16.21" />
<add key="Email:SmtpPort" value="25" />
<add key="Email:Username" value="" />
<add key="Email:Password" value="" />
<add key="Email:UseSsl" value="false" />
<add key="Email:FromAddress" value="no_reply@aviacioncivil.gob.ec" />
```

**Nota:** El servidor SMTP `172.20.16.21` es un relay interno que NO requiere autenticación.

### 2. Clase EnviarCorreo

La clase `EnviarCorreo` ya está implementada en:
- **Ubicación:** `CapaDatos\Services\EnviarCorreo.cs`
- **Métodos principales:**
  - `enviaMensajeCorreo()` - Envío simple
  - `enviaMensajeCorreoDesde()` - Envío con remitente personalizado

## 🚀 Cómo Usar el Sistema de Correos

### Opción 1: Envío Simple

```csharp
using CapaDatos.Services;

// Crear instancia del servicio
var emailService = new EnviarCorreo();

// Enviar correo
bool resultado = emailService.enviaMensajeCorreo(
    coreoPara: "destinatario@aviacioncivil.gob.ec",
    asunto: "Notificación del Sistema AOCR",
    mensajeDetalle: "<h2>Título</h2><p>Contenido del mensaje en HTML</p>"
);

if (resultado)
{
    // Correo enviado exitosamente
    Console.WriteLine("Correo enviado");
}
else
{
    // Error al enviar
    Console.WriteLine("Error al enviar correo");
}
```

### Opción 2: Envío con Remitente Personalizado

```csharp
using CapaDatos.Services;

var emailService = new EnviarCorreo();

bool resultado = emailService.enviaMensajeCorreoDesde(
    coreoDesde: "sistema@aviacioncivil.gob.ec",
    coreoPara: "destinatario@aviacioncivil.gob.ec",
    asunto: "Notificación Importante",
    mensajeDetalle: "<html><body><h1>Mensaje</h1></body></html>"
);
```

### Opción 3: Desde un Controlador

```csharp
using System.Web.Mvc;
using CapaDatos.Services;

public class OrdenRecaudacionController : Controller
{
    [HttpPost]
    public ActionResult NotificarOrden(int ordenId)
    {
        try
        {
            var emailService = new EnviarCorreo();
            
            // Obtener datos de la orden
            var orden = /* tu código para obtener la orden */;
            
            // Preparar el correo
            string destinatario = orden.EmailContacto;
            string asunto = $"Orden de Recaudación #{ordenId}";
            string mensaje = GenerarHtmlNotificacion(orden);
            
            // Enviar correo
            bool enviado = emailService.enviaMensajeCorreo(
                coreoPara: destinatario,
                asunto: asunto,
                mensajeDetalle: mensaje
            );
            
            if (enviado)
            {
                TempData["Mensaje"] = "Notificación enviada correctamente";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["Error"] = "Error al enviar la notificación";
                return RedirectToAction("Detalle", new { id = ordenId });
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error: " + ex.Message;
            return RedirectToAction("Index");
        }
    }
    
    private string GenerarHtmlNotificacion(object orden)
    {
        return $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <div style='background-color: #003366; color: white; padding: 20px;'>
                <h2>Sistema AOCR - DGAC Ecuador</h2>
            </div>
            <div style='padding: 20px;'>
                <h3>Notificación de Orden de Recaudación</h3>
                <p>Estimado/a usuario/a,</p>
                <p>Se ha generado una nueva orden de recaudación.</p>
                <p><strong>Número de Orden:</strong> #{orden.Id}</p>
                <p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy}</p>
                <p>Por favor, ingrese al sistema para más detalles.</p>
            </div>
            <div style='background-color: #f0f0f0; padding: 10px; text-align: center;'>
                <small>Este es un correo automático. No responder.</small>
            </div>
        </body>
        </html>";
    }
}
```

## 🧪 Página de Prueba

Se ha creado una página de prueba para verificar el envío de correos:

**URL:** `http://tu-servidor/TestEmail`

### Funcionalidades de la página de prueba:
1. **Envío Simple:** Prueba básica de envío de correos
2. **Envío con Remitente:** Personaliza el remitente
3. **Notificación de Orden:** Simula una notificación de orden de recaudación

### Cómo usar la página de prueba:
1. Compila y ejecuta el proyecto
2. Navega a: `http://localhost:puerto/TestEmail`
3. Ingresa tu correo como destinatario
4. Haz clic en "Enviar Correo Simple"
5. Verifica que el correo llegue

## 📝 Ejemplos de Mensajes HTML

### Ejemplo 1: Mensaje Simple
```html
<h2>Notificación del Sistema</h2>
<p>Estimado usuario,</p>
<p>Este es un mensaje de notificación del Sistema AOCR.</p>
<p>Fecha: 02/02/2026</p>
```

### Ejemplo 2: Mensaje con Estilos
```html
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; }
        .header { background-color: #003366; color: white; padding: 20px; }
        .content { padding: 20px; }
        .button { background-color: #003366; color: white; padding: 10px 20px; text-decoration: none; }
    </style>
</head>
<body>
    <div class='header'>
        <h2>Sistema AOCR - DGAC Ecuador</h2>
    </div>
    <div class='content'>
        <h3>Notificación Importante</h3>
        <p>Su solicitud ha sido procesada.</p>
        <a href='#' class='button'>Ver Detalles</a>
    </div>
</body>
</html>
```

### Ejemplo 3: Notificación de Orden con Tabla
```html
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2 style='color: #003366;'>Orden de Recaudación</h2>
    <table style='width: 100%; border-collapse: collapse;'>
        <tr style='background-color: #f0f0f0;'>
            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Número:</strong></td>
            <td style='padding: 10px; border: 1px solid #ddd;'>OR-2026-001</td>
        </tr>
        <tr>
            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Fecha:</strong></td>
            <td style='padding: 10px; border: 1px solid #ddd;'>02/02/2026</td>
        </tr>
        <tr style='background-color: #f0f0f0;'>
            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Monto:</strong></td>
            <td style='padding: 10px; border: 1px solid #ddd;'>$1,500.00</td>
        </tr>
        <tr>
            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Estado:</strong></td>
            <td style='padding: 10px; border: 1px solid #ddd;'>Pendiente</td>
        </tr>
    </table>
    <p style='margin-top: 20px;'>Por favor, proceda con el pago correspondiente.</p>
</body>
</html>
```

## ⚠️ Consideraciones Importantes

### 1. Servidor SMTP
- **IP:** `172.20.16.21`
- **Puerto:** `25`
- **Autenticación:** NO requerida (relay interno)
- **SSL:** NO habilitado
- **Red:** Solo accesible desde la red interna de DGAC

### 2. Direcciones de Correo
- **Remitente por defecto:** `no_reply@aviacioncivil.gob.ec`
- **Dominio válido:** `@aviacioncivil.gob.ec`, `@dgac.gob.ec`

### 3. Manejo de Errores
```csharp
try
{
    var emailService = new EnviarCorreo();
    bool enviado = emailService.enviaMensajeCorreo(destinatario, asunto, mensaje);
    
    if (!enviado)
    {
        // Registrar el error en log
        LogError("Error al enviar correo a: " + destinatario);
    }
}
catch (Exception ex)
{
    // Manejar la excepción
    LogError("Excepción al enviar correo: " + ex.Message);
}
```

### 4. Mejores Prácticas
- ✅ Siempre valida el correo del destinatario
- ✅ Usa mensajes HTML bien formateados
- ✅ Incluye información clara y concisa
- ✅ Registra los envíos en logs
- ✅ Maneja errores apropiadamente
- ❌ No envíes correos masivos sin control
- ❌ No incluyas información sensible sin cifrar

## 🔍 Troubleshooting

### Problema: El correo no se envía
**Solución:**
1. Verifica que el servidor SMTP `172.20.16.21` sea accesible
2. Comprueba que estás en la red interna de DGAC
3. Revisa los logs de la aplicación
4. Usa la página de prueba `/TestEmail` para diagnóstico

### Problema: Correo se envía pero no llega
**Solución:**
1. Verifica el correo destinatario
2. Revisa la carpeta de SPAM
3. Confirma que el dominio del destinatario acepte correos de `@aviacioncivil.gob.ec`
4. Contacta al administrador del servidor de correo

### Problema: Error de autenticación
**Solución:**
- El servidor `172.20.16.21` NO requiere autenticación
- Si ves este error, verifica la configuración en Web.config
- Asegúrate de que los campos Username y Password estén vacíos

## 📞 Soporte

Para problemas con el envío de correos, contactar a:
- **Equipo de Desarrollo:** desarrollo@dgac.gob.ec
- **Infraestructura IT:** soporte.ti@dgac.gob.ec

## 📝 Registro de Cambios

| Fecha | Cambio | Autor |
|-------|--------|-------|
| 02/02/2026 | Configuración inicial del sistema de correos | Sistema |
| 02/02/2026 | Creación de página de prueba TestEmail | Sistema |
| 02/02/2026 | Documentación de implementación | Sistema |

---

**Última actualización:** 02/02/2026
