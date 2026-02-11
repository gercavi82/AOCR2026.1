/**
 * Configuración Dinámica AOCR
 * Reemplaza valores hardcodeados en funciones de test JavaScript
 * Autor: Sistema AOCR
 * Fecha: 03/02/2025
 */

// Cache global para configuraciones
window.AOCRConfig = {
    testValues: null,
    lastUpdated: null,
    isLoading: false
};

/**
 * Obtiene valores de test configurables desde el servidor
 * Reemplaza valores hardcodeados como "TEST EMPRESA", "test@test.com", etc.
 */
function obtenerValoresTestConfigurables() {
    return new Promise((resolve, reject) => {
        // Si ya tenemos valores cacheados y son recientes (menos de 5 minutos)
        if (window.AOCRConfig.testValues && 
            window.AOCRConfig.lastUpdated && 
            (Date.now() - window.AOCRConfig.lastUpdated) < 300000) {
            resolve(window.AOCRConfig.testValues);
            return;
        }

        // Evitar múltiples llamadas simultáneas
        if (window.AOCRConfig.isLoading) {
            setTimeout(() => obtenerValoresTestConfigurables().then(resolve).catch(reject), 100);
            return;
        }

        window.AOCRConfig.isLoading = true;

        $.ajax({
            url: '/ConfigApi/TestValues',
            type: 'GET',
            dataType: 'json',
            timeout: 10000,
            success: function(response) {
                window.AOCRConfig.isLoading = false;
                
                if (response.success && response.data) {
                    window.AOCRConfig.testValues = response.data;
                    window.AOCRConfig.lastUpdated = Date.now();
                    
                    console.log('Valores de test configurables cargados:', response.data);
                    resolve(response.data);
                } else {
                    console.warn('Respuesta inesperada del servidor:', response);
                    // Usar valores por defecto
                    const valoresPorDefecto = obtenerValoresPorDefecto();
                    window.AOCRConfig.testValues = valoresPorDefecto;
                    resolve(valoresPorDefecto);
                }
            },
            error: function(xhr, status, error) {
                window.AOCRConfig.isLoading = false;
                
                console.error('Error al cargar valores de test:', {
                    status: xhr.status,
                    statusText: xhr.statusText,
                    error: error
                });
                
                // Usar valores por defecto en caso de error
                const valoresPorDefecto = obtenerValoresPorDefecto();
                window.AOCRConfig.testValues = valoresPorDefecto;
                resolve(valoresPorDefecto);
            }
        });
    });
}

/**
 * Valores por defecto en caso de que el servidor no esté disponible
 */
function obtenerValoresPorDefecto() {
    return {
        operadorDefecto: 'EMPRESA DEMO S.A.',
        representanteDefecto: 'Juan Carlos Pérez Demo',
        cedulaDefecto: '0999999999',
        direccionDefecto: 'Av. Amazonas N24-03 y Colón, Quito, Ecuador',
        telefonoDefecto: '02-2234567',
        emailDefecto: 'demo@ejemplo-dgac.gob.ec',
        rucDefecto: '1790000000001',
        razonSocialDefecto: 'EMPRESA DEMO SERVICIOS AÉREOS S.A.',
        descripcionDefecto: 'Operaciones de demostración y pruebas del sistema AOCR',
        observacionesDefecto: 'Datos de prueba - No usar en producción'
    };
}

/**
 * Función de test mejorada usando valores configurables
 * Reemplaza testFormularioCompleto() original con valores hardcodeados
 */
async function testFormularioCompletoConfigurable() {
    console.log('=== INICIANDO TEST FORMULARIO COMPLETO CON VALORES CONFIGURABLES ===');
    
    try {
        // Obtener valores configurables
        const valores = await obtenerValoresTestConfigurables();
        
        // Mapear datos desde la UI
        var datosMapeados = mapearCamposVM();
        console.log('Datos mapeados:', datosMapeados);
        
        // Actualizar array global
        window.aeronavesSeleccionadas = window.aeronavesSeleccionadas || [];
        
        // Obtener código de solicitud
        var codigoSolicitud = 0;
        var solInput = $('input[name="Solicitud.CodigoSolicitud"], #codigoSolicitud');
        if (solInput.length > 0 && solInput.val()) {
            codigoSolicitud = parseInt(solInput.val()) || 0;
        }
        
        // Construir viewmodel usando valores configurables
        const vm = {
            Solicitud: {
                CodigoSolicitud: codigoSolicitud,
                NombreOperador: datosMapeados.NombreOperador || valores.operadorDefecto,
                RepresentanteLegal: datosMapeados.RepresentanteLegal || valores.representanteDefecto,
                CedulaRepresentante: datosMapeados.CedulaRepresentante || valores.cedulaDefecto,
                Direccion: datosMapeados.Direccion || valores.direccionDefecto,
                Telefono: datosMapeados.Telefono || valores.telefonoDefecto,
                Email: datosMapeados.Email || valores.emailDefecto,
                Ruc: datosMapeados.Ruc || valores.rucDefecto,
                RazonSocial: datosMapeados.RazonSocial || valores.razonSocialDefecto,
                TipoOperacion: datosMapeados.TipoOperacion || 'Test Configurado',
                DescripcionOperacion: datosMapeados.DescripcionOperacion || valores.descripcionDefecto,
                ObservacionesGenerales: datosMapeados.ObservacionesGenerales || valores.observacionesDefecto,
                Estado: 'BORRADOR',
                TipoSolicitud: 1
            },
            Banco: $('#banco').val() || 'BANCO DEMO',
            NumeroComprobante: $('#numeroComprobante').val() || 'DEMO-123456',
            Aeronaves: window.aeronavesSeleccionadas.length > 0 ? window.aeronavesSeleccionadas : [
                {
                    Marca: 'Boeing',
                    Modelo: '737',
                    Serie: 'DEMO123',
                    Matricula: 'HC-DEMO',
                    Configuracion: 'Pasajeros Demo',
                    EtapaRuido: '3'
                }
            ]
        };
        
        // Llamada AJAX para test
        $.ajax({
            url: $('#testFormUrl').val() || '/SolicitudAOCR/FormularioCompleto',
            type: 'POST',
            data: JSON.stringify(vm),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            timeout: 15000,
            success: function(response) {
                console.log('TEST FORMULARIO COMPLETO SUCCESS:', response);
                
                Swal.fire({
                    title: 'Test Formulario Exitoso',
                    html: `<div class="text-left">
                        <p><strong>Operador:</strong> ${AOCR.escapeHtml(valores.operadorDefecto)}</p>
                        <p><strong>Representante:</strong> ${AOCR.escapeHtml(valores.representanteDefecto)}</p>
                        <p><strong>Email:</strong> ${AOCR.escapeHtml(valores.emailDefecto)}</p>
                        <p><strong>Estado:</strong> ${AOCR.escapeHtml(response.success ? 'Exitoso' : 'Fallido')}</p>
                    </div>`,
                    icon: 'success',
                    confirmButtonText: 'OK'
                });
            },
            error: function(xhr, status, error) {
                console.error('TEST FORMULARIO COMPLETO ERROR:', {
                    status: xhr.status,
                    statusText: xhr.statusText,
                    error: error,
                    responseText: xhr.responseText ? xhr.responseText.substring(0, 500) : 'Sin respuesta'
                });
                
                Swal.fire({
                    title: 'Test Formulario Fallido',
                    html: `<div class="text-left">
                        <p><strong>Status:</strong> ${xhr.status}</p>
                        <p><strong>Error:</strong> ${error}</p>
                        <p><strong>Usando valores configurables:</strong> Sí</p>
                        <p><small>Los datos de test ahora son configurables</small></p>
                    </div>`,
                    icon: 'error',
                    confirmButtonText: 'OK'
                });
            }
        });
        
    } catch (err) {
        console.error('Error en test configurable:', err);
        
        Swal.fire({
            title: 'Error en Test Configurable',
            text: 'Error: ' + err.message,
            icon: 'error',
            confirmButtonText: 'OK'
        });
    }
}

/**
 * Test básico para verificar conectividad con API de configuración
 */
async function testConfiguracionApi() {
    console.log('=== TESTING API CONFIGURACIÓN ===');
    
    try {
        const valores = await obtenerValoresTestConfigurables();
        
        Swal.fire({
            title: 'Test API Configuración',
            html: `<div class="text-left">
                <h5>✅ Configuración Cargada</h5>
                <p><strong>Operador:</strong> ${valores.operadorDefecto}</p>
                <p><strong>Email:</strong> ${valores.emailDefecto}</p>
                <p><strong>RUC:</strong> ${valores.rucDefecto}</p>
                <p><strong>Cache:</strong> ${window.AOCRConfig.lastUpdated ? 'Activo' : 'No'}</p>
                <p><small>Los valores hardcodeados han sido reemplazados</small></p>
            </div>`,
            icon: 'success',
            confirmButtonText: 'OK'
        });
        
    } catch (error) {
        Swal.fire({
            title: 'Error API Configuración',
            text: 'No se pudieron cargar los valores configurables: ' + error.message,
            icon: 'error',
            confirmButtonText: 'OK'
        });
    }
}

// Función para limpiar cache (útil para desarrollo)
function limpiarCacheConfiguracion() {
    window.AOCRConfig.testValues = null;
    window.AOCRConfig.lastUpdated = null;
    window.AOCRConfig.isLoading = false;
    console.log('Cache de configuración limpiado');
}

// Inicializar configuración al cargar la página
$(document).ready(function() {
    // Pre-cargar valores configurables en background
    obtenerValoresTestConfigurables().then(function(valores) {
        console.log('Configuración AOCR cargada:', valores);
    }).catch(function(error) {
        console.warn('No se pudo pre-cargar configuración:', error);
    });
});

// Exponer funciones globalmente
window.testFormularioCompletoConfigurable = testFormularioCompletoConfigurable;
window.testConfiguracionApi = testConfiguracionApi;
window.limpiarCacheConfiguracion = limpiarCacheConfiguracion;
window.obtenerValoresTestConfigurables = obtenerValoresTestConfigurables;