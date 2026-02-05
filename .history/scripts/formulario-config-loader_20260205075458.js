/**
 * Loader de configuración para FormularioCompleto.cshtml
 * Reemplaza valores hardcodeados con configuración de BD
 */

// Variables globales para configuración
window.aocrConfig = {
    valores: {},
    cargado: false
};

/**
 * Cargar configuración desde la API
 */
function cargarConfiguracionAOCR() {
    console.log('🔧 Cargando configuración AOCR desde API...');
    
    // Usar la función del archivo aocr-config.js si existe
    if (typeof obtenerValoresTestConfigurables === 'function') {
        obtenerValoresTestConfigurables(function(config) {
            if (config.success && config.data) {
                window.aocrConfig.valores = config.data;
                window.aocrConfig.cargado = true;
                console.log('✅ Configuración cargada:', window.aocrConfig.valores);
                
                // Aplicar configuración a campos del formulario
                aplicarConfiguracionFormulario();
            } else {
                console.warn('⚠️ No se pudo cargar configuración, usando valores por defecto');
                cargarValoresPorDefecto();
            }
        });
    } else {
        // Fallback: cargar directamente desde ConfigApiController
        $.ajax({
            url: '/Config/TestValues',
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response.success && response.data) {
                    window.aocrConfig.valores = response.data;
                    window.aocrConfig.cargado = true;
                    console.log('✅ Configuración cargada directamente:', window.aocrConfig.valores);
                    aplicarConfiguracionFormulario();
                } else {
                    console.warn('⚠️ Respuesta de API no válida');
                    cargarValoresPorDefecto();
                }
            },
            error: function(xhr, status, error) {
                console.error('❌ Error al cargar configuración:', error);
                cargarValoresPorDefecto();
            }
        });
    }
}

/**
 * Valores por defecto cuando no se puede cargar desde BD
 */
function cargarValoresPorDefecto() {
    window.aocrConfig.valores = {
        TEST_EMPRESA_NOMBRE: 'AERONÁUTICA CIVIL',
        TEST_EMPRESA_DIRECCION: 'Av. El Dorado # 103-15',
        TEST_EMPRESA_TELEFONO: '+57 1 425-1000',
        TEST_EMPRESA_EMAIL: 'info@aerocivil.gov.co',
        DEMO_MONTO_FIJO: '80.00'
    };
    window.aocrConfig.cargado = true;
    console.log('📝 Usando valores por defecto:', window.aocrConfig.valores);
    aplicarConfiguracionFormulario();
}

/**
 * Aplicar configuración a campos del formulario
 */
function aplicarConfiguracionFormulario() {
    const config = window.aocrConfig.valores;
    
    // Aplicar a campos de empresa si están vacíos
    if (!$('#nombreCompania').val() && config.TEST_EMPRESA_NOMBRE) {
        $('#nombreCompania').attr('placeholder', config.TEST_EMPRESA_NOMBRE);
    }
    
    if (!$('#direccionCompania').val() && config.TEST_EMPRESA_DIRECCION) {
        $('#direccionCompania').attr('placeholder', config.TEST_EMPRESA_DIRECCION);
    }
    
    if (!$('#telefonoCompania').val() && config.TEST_EMPRESA_TELEFONO) {
        $('#telefonoCompania').attr('placeholder', config.TEST_EMPRESA_TELEFONO);
    }
    
    if (!$('#correoCompania').val() && config.TEST_EMPRESA_EMAIL) {
        $('#correoCompania').attr('placeholder', config.TEST_EMPRESA_EMAIL);
    }
    
    console.log('🎨 Configuración aplicada a formulario');
}

/**
 * Obtener datos configurables para testing
 */
function obtenerDatosTestConfigurables() {
    const config = window.aocrConfig.valores;
    
    return {
        nombreOperador: config.TEST_EMPRESA_NOMBRE || 'AERONÁUTICA CIVIL',
        direccion: config.TEST_EMPRESA_DIRECCION || 'Av. El Dorado # 103-15',
        telefono: config.TEST_EMPRESA_TELEFONO || '+57 1 425-1000',
        email: config.TEST_EMPRESA_EMAIL || 'info@aerocivil.gov.co',
        razonSocial: config.TEST_EMPRESA_NOMBRE || 'AERONÁUTICA CIVIL',
        montoDemo: config.DEMO_MONTO_FIJO || '80.00'
    };
}

/**
 * Actualizar función testFormularioCompleto para usar configuración
 */
function testFormularioCompletoConfigurable() {
    console.log('=== INICIANDO TEST FORMULARIO COMPLETO (CONFIGURABLE) ===');
    
    if (!window.aocrConfig.cargado) {
        console.warn('Configuración no cargada, usando valores por defecto');
        cargarValoresPorDefecto();
    }
    
    const datos = obtenerDatosTestConfigurables();
    
    const testVm = {
        Solicitud: {
            CodigoSolicitud: 0,
            NombreOperador: datos.nombreOperador,
            RepresentanteLegal: 'Representante de ' + datos.nombreOperador,
            CedulaRepresentante: '1234567890',
            Direccion: datos.direccion,
            Telefono: datos.telefono,
            Email: datos.email,
            Ruc: '1234567890001',
            RazonSocial: datos.razonSocial,
            TipoOperacion: 'Certificación AOCR',
            DescripcionOperacion: 'Solicitud de reconocimiento AOCR con datos configurables',
            ObservacionesGenerales: 'Datos obtenidos desde configuración de BD',
            Estado: 'BORRADOR',
            TipoSolicitud: 1
        },
        Banco: 'BANCO DE LA REPÚBLICA',
        NumeroComprobante: 'CFG-' + new Date().getTime().toString().slice(-6),
        Aeronaves: [
            {
                Marca: 'Boeing',
                Modelo: '737',
                Serie: 'CFG123',
                Matricula: 'HC-CFG',
                Configuracion: 'Pasajeros',
                EtapaRuido: '3'
            }
        ]
    };

    console.log('Datos de prueba configurables:', testVm);

    // AJAX call using configurable data
    $.ajax({
        url: '/SolicitudAOCR/FormularioCompleto',
        type: 'POST',
        data: JSON.stringify(testVm),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        timeout: 10000,
        success: function (response) {
            console.log('TEST FORMULARIO COMPLETO (CONFIGURABLE) SUCCESS:', response);
            Swal.fire({
                title: '✅ Test Formulario Completo Exitoso',
                text: 'Datos configurables aplicados correctamente',
                icon: 'success'
            });
        },
        error: function (xhr, status, error) {
            console.log('TEST FORMULARIO COMPLETO (CONFIGURABLE) ERROR:', { status: xhr.status, error: error });
            Swal.fire({
                title: '❌ Test Formulario Completo Fallido', 
                text: 'Status: ' + xhr.status + ', Error: ' + error,
                icon: 'error'
            });
        }
    });
}

/**
 * Mapear campos del formulario usando configuración
 */
function mapearCamposVMConfigurable() {
    const datos = obtenerDatosTestConfigurables();
    
    // Obtener valores de los campos o usar configuración como fallback
    return {
        NombreOperador: $('#nombreCompania').val() || datos.nombreOperador,
        RepresentanteLegal: $('#nombreRepresentante').val() || 'Representante Legal',
        CedulaRepresentante: $('#rucRepresentante').val() || '0000000000',
        Direccion: $('#direccionEcuador').val() || $('#direccionCompania').val() || datos.direccion,
        Telefono: $('#telefonoEcuador').val() || $('#telefonoCompania').val() || datos.telefono,
        Email: $('#emailEcuador').val() || $('#correoCompania').val() || datos.email,
        Ruc: $('#rucRepresentante').val() || $('#rucOperador').val() || '0000000000001',
        RazonSocial: $('#razonSocial').val() || $('#nombreCompania').val() || datos.razonSocial,
        TipoOperacion: obtenerTipoOperacion(),
        DescripcionOperacion: $('#resumenOperaciones').val() || 'Sin descripción',
        ObservacionesGenerales: $('#conceptoFacturaPago').val() || 'Sin observaciones'
    };
}

/**
 * Obtener tipos de operación seleccionados
 */
function obtenerTipoOperacion() {
    const tipos = [];
    if ($('#opsRegulares').is(':checked')) tipos.push('Ops Regulares');
    if ($('#opsNoRegulares').is(':checked')) tipos.push('Ops No Regulares');
    if ($('#pasajeros').is(':checked')) tipos.push('Pasajeros/Carga/Correo');
    if ($('#carga').is(':checked')) tipos.push('Carga');
    return tipos.length > 0 ? tipos.join(' | ') : 'No especificado';
}

// Inicializar cuando el documento esté listo
$(document).ready(function() {
    console.log('📋 Formulario Config Loader inicializado');
    
    // Pequeña espera para asegurar que otros scripts se carguen
    setTimeout(function() {
        cargarConfiguracionAOCR();
    }, 500);
});