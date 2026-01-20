# Solución para Error de Token Antifalsificación

## Problema
Error: `System.Web.Mvc.HttpAntiForgeryException: El campo de formulario antifalsificación "__RequestVerificationToken" no está presente.`

## Solución Aplicada

### 1. Token agregado al HTML
Se agregó `@Html.AntiForgeryToken()` al inicio del formulario para generar el token.

### 2. Token incluido en petición AJAX
El token se envía de dos formas:
- Como parte del objeto JSON: `vm.__RequestVerificationToken = token`
- Como header HTTP: `xhr.setRequestHeader('RequestVerificationToken', token)`

## Si el Error Persiste

Si después de estos cambios el error continúa, puede ser necesario ajustar el controlador. Aquí hay dos opciones:

### Opción 1: Configurar el Controlador para Aceptar Token en Header (Recomendado)

En el controlador `SolicitudAOCRController`, asegúrate de que el método `FormularioCompleto` esté configurado así:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
{
    // Tu código aquí
}
```

Y agrega esta configuración en `Global.asax.cs` o `Startup.cs`:

```csharp
// Permitir que el token se lea del header
AntiForgeryConfig.SuppressXFrameOptionsHeader = true;
```

### Opción 2: Usar FormData en lugar de JSON (Alternativa)

Si la Opción 1 no funciona, puedes cambiar la petición AJAX para usar FormData:

```javascript
// En lugar de JSON.stringify(vm), usar FormData
var formData = new FormData();
formData.append('__RequestVerificationToken', token);
formData.append('Solicitud', JSON.stringify(vm.Solicitud));
// ... agregar otros campos

$.ajax({
    url: '@Url.Action("FormularioCompleto", "SolicitudAOCR")',
    type: 'POST',
    data: formData,
    contentType: false,
    processData: false,
    // ...
});
```

### Opción 3: Deshabilitar Validación (NO RECOMENDADO - Solo para pruebas)

**⚠️ ADVERTENCIA: Esto reduce la seguridad. Solo úsalo para pruebas.**

```csharp
[HttpPost]
// [ValidateAntiForgeryToken] // Comentar esta línea
public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
{
    // Tu código aquí
}
```

## Verificación

Para verificar que el token se está enviando:

1. Abre las herramientas de desarrollador del navegador (F12)
2. Ve a la pestaña "Network" (Red)
3. Intenta guardar el formulario
4. Busca la petición a "FormularioCompleto"
5. Verifica en "Headers" que existe:
   - `RequestVerificationToken: [valor del token]`
   - O en "Payload" que existe `__RequestVerificationToken: [valor]`

## Notas Adicionales

- El token se genera automáticamente con `@Html.AntiForgeryToken()`
- El token es único por sesión
- Si recargas la página, obtendrás un nuevo token
- El token expira cuando la sesión expira

