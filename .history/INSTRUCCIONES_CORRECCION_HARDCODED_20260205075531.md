# CORRECCIONES PARA ELIMINAR VALORES HARDCODEADOS

## 1. Agregar referencia al script de configuración

**Ubicación**: Al inicio del archivo, después de la línea con FontAwesome
**Buscar**:
```html
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css">
```

**Agregar después**:
```html
<script src="@Url.Content("~/Scripts/aocr-config.js")"></script>
<script src="@Url.Content("~/Scripts/formulario-config-loader.js")"></script>
```

## 2. Reemplazar función testFormularioCompleto

**Buscar**:
```javascript
function testFormularioCompleto() {
    console.log('=== INICIANDO TEST FORMULARIO COMPLETO ===');
    
    // Crear datos de prueba simples
    const testVm = {
      Solicitud: {
        CodigoSolicitud: 0,
        NombreOperador: 'TEST OPERADOR',
        RepresentanteLegal: 'TEST REPRESENTANTE',
        CedulaRepresentante: '1234567890',
        Direccion: 'TEST DIRECCION',
        Telefono: '0999999999',
        Email: 'test@test.com',
        Ruc: '1234567890001',
        RazonSocial: 'TEST EMPRESA',
        TipoOperacion: 'Test',
        DescripcionOperacion: 'TEST DESCRIPCION',
        ObservacionesGenerales: 'TEST OBSERVACIONES',
        Estado: 'BORRADOR',
        TipoSolicitud: 1
      },
      Banco: 'BANCO TEST',
      NumeroComprobante: '123456',
```

**Reemplazar con**:
```javascript
function testFormularioCompleto() {
    console.log('=== INICIANDO TEST FORMULARIO COMPLETO (CONFIGURABLE) ===');
    
    // Asegurar que la configuración esté cargada
    if (!window.aocrConfig || !window.aocrConfig.cargado) {
        console.warn('⚠️ Configuración no disponible, cargando...');
        cargarConfiguracionAOCR();
        
        // Esperar un momento y reintentar
        setTimeout(function() {
            if (typeof testFormularioCompletoConfigurable === 'function') {
                testFormularioCompletoConfigurable();
            } else {
                console.error('❌ Función configurable no disponible');
            }
        }, 1000);
        return;
    }
    
    // Usar función configurable
    if (typeof testFormularioCompletoConfigurable === 'function') {
        testFormularioCompletoConfigurable();
    } else {
        console.error('❌ Función configurable no disponible');
    }
}
```

## 3. Actualizar función guardarFormulario

**Buscar esta sección**:
```javascript
    // Construir viewmodel completo
    const vm = {
      Solicitud: {
        CodigoSolicitud: codigoSolicitud,
        NombreOperador: datosMapeados.NombreOperador || 'TEST OPERADOR',
        RepresentanteLegal: datosMapeados.RepresentanteLegal || 'TEST REPRESENTANTE',
        CedulaRepresentante: datosMapeados.CedulaRepresentante || '1234567890',
        Direccion: datosMapeados.Direccion || 'TEST DIRECCION',
        Telefono: datosMapeados.Telefono || '0999999999',
        Email: datosMapeados.Email || 'test@test.com',
        Ruc: datosMapeados.Ruc || '1234567890001',
        RazonSocial: datosMapeados.RazonSocial || 'TEST EMPRESA',
        TipoOperacion: datosMapeados.TipoOperacion || 'Test',
        DescripcionOperacion: datosMapeados.DescripcionOperacion || 'TEST DESCRIPCION',
        ObservacionesGenerales: datosMapeados.ObservacionesGenerales || 'TEST OBSERVACIONES',
        Estado: 'BORRADOR',
        TipoSolicitud: 1
      },
```

**Reemplazar con**:
```javascript
    // Obtener datos configurables como fallback
    const datosConfigurables = typeof obtenerDatosTestConfigurables === 'function' 
        ? obtenerDatosTestConfigurables() 
        : {
            nombreOperador: 'EMPRESA NO CONFIGURADA',
            direccion: 'DIRECCIÓN NO CONFIGURADA', 
            telefono: 'TELÉFONO NO CONFIGURADO',
            email: 'EMAIL NO CONFIGURADO',
            razonSocial: 'RAZÓN SOCIAL NO CONFIGURADA'
          };
    
    // Construir viewmodel completo con datos configurables
    const vm = {
      Solicitud: {
        CodigoSolicitud: codigoSolicitud,
        NombreOperador: datosMapeados.NombreOperador || datosConfigurables.nombreOperador,
        RepresentanteLegal: datosMapeados.RepresentanteLegal || 'Representante Legal',
        CedulaRepresentante: datosMapeados.CedulaRepresentante || '0000000000',
        Direccion: datosMapeados.Direccion || datosConfigurables.direccion,
        Telefono: datosMapeados.Telefono || datosConfigurables.telefono,
        Email: datosMapeados.Email || datosConfigurables.email,
        Ruc: datosMapeados.Ruc || '0000000000001',
        RazonSocial: datosMapeados.RazonSocial || datosConfigurables.razonSocial,
        TipoOperacion: datosMapeados.TipoOperacion || 'No especificado',
        DescripcionOperacion: datosMapeados.DescripcionOperacion || 'Sin descripción',
        ObservacionesGenerales: datosMapeados.ObservacionesGenerales || 'Sin observaciones',
        Estado: 'BORRADOR',
        TipoSolicitud: 1
      },
```

## 4. Actualizar función mapearCamposVM

**Buscar el final de la función mapearCamposVM**:
```javascript
  $(document).ready(function(){
    buildCarta12901();
    updateChecklist();
    
    // CORRECCIÓN: Reemplazar operador ?. con verificación C# 5
    var codigoSolicitud = 0;
    @{
        var modeloSolicitudCodigo = Model != null && Model.Solicitud != null ? Model.Solicitud.CodigoSolicitud : 0;
    }
    
    if (@modeloSolicitudCodigo > 0) {
        codigoSolicitud = @modeloSolicitudCodigo;
    }
  });
```

**Reemplazar con**:
```javascript
  $(document).ready(function(){
    buildCarta12901();
    updateChecklist();
    
    // CORRECCIÓN: Reemplazar operador ?. con verificación C# 5
    var codigoSolicitud = 0;
    @{
        var modeloSolicitudCodigo = Model != null && Model.Solicitud != null ? Model.Solicitud.CodigoSolicitud : 0;
    }
    
    if (@modeloSolicitudCodigo > 0) {
        codigoSolicitud = @modeloSolicitudCodigo;
    }
    
    // Cargar configuración AOCR al inicializar
    setTimeout(function() {
        if (typeof cargarConfiguracionAOCR === 'function') {
            cargarConfiguracionAOCR();
        }
    }, 800);
  });
```

## 5. Agregar botón de prueba configurable

**Buscar**:
```html
                    <button class="btn btn-info mr-2" type="button" onclick="testFormularioCompleto()">
                        <i class="fas fa-flask"></i> Test ViewModel
                    </button>
```

**Reemplazar con**:
```html
                    <button class="btn btn-info mr-2" type="button" onclick="testFormularioCompleto()">
                        <i class="fas fa-flask"></i> Test ViewModel
                    </button>
                    <button class="btn btn-success mr-2" type="button" onclick="testFormularioCompletoConfigurable()">
                        <i class="fas fa-database"></i> Test Configurable
                    </button>
```

## 6. Script de validación de configuración

**Agregar al final, antes del cierre del último script**:
```javascript
// Validar que la configuración se cargó correctamente
function validarConfiguracion() {
    if (window.aocrConfig && window.aocrConfig.cargado) {
        console.log('✅ Configuración AOCR cargada:', Object.keys(window.aocrConfig.valores).length, 'parámetros');
        return true;
    } else {
        console.warn('⚠️ Configuración AOCR no disponible');
        return false;
    }
}

// Debug: mostrar configuración en consola después de 2 segundos
setTimeout(function() {
    console.log('🔍 Estado de configuración AOCR:');
    console.log('- Cargado:', window.aocrConfig ? window.aocrConfig.cargado : false);
    console.log('- Valores:', window.aocrConfig ? window.aocrConfig.valores : 'No disponible');
}, 2000);
```

## RESULTADO ESPERADO

Después de aplicar estos cambios:

✅ **Los valores hardcodeados serán reemplazados por datos de la base de datos**
- 'TEST OPERADOR' → Valor desde `TEST_EMPRESA_NOMBRE`
- 'TEST DIRECCION' → Valor desde `TEST_EMPRESA_DIRECCION`
- 'test@test.com' → Valor desde `TEST_EMPRESA_EMAIL`
- etc.

✅ **El formulario cargará automáticamente la configuración**
- Al abrir la página se conectará a `/Config/TestValues`
- Los placeholders mostrarán valores reales de BD
- Los campos tendrán valores por defecto configurables

✅ **Las funciones de test usarán datos reales**
- `testFormularioCompleto()` usará configuración de BD
- `guardarFormulario()` aplicará valores configurables como fallback
- Nuevo botón "Test Configurable" para pruebas específicas