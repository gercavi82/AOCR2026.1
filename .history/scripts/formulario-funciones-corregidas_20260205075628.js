/* REEMPLAZO PARA FUNCIONES HARDCODEADAS EN FormularioCompleto.cshtml */

// ====== FUNCIÓN TESTFORMULARIOCOMPLETO MEJORADA ======
function testFormularioCompleto() {
    console.log('=== INICIANDO TEST FORMULARIO COMPLETO (CONFIGURABLE) ===');
    
    // Verificar si la configuración está disponible
    if (!window.aocrConfig || !window.aocrConfig.cargado) {
        console.warn('⚠️ Configuración no disponible, cargando...');
        
        // Intentar cargar configuración
        if (typeof cargarConfiguracionAOCR === 'function') {
            cargarConfiguracionAOCR();
            
            // Esperar y reintentar
            setTimeout(function() {
                testFormularioCompleto();
            }, 1500);
        } else {
            // Usar API directamente
            $.ajax({
                url: '/ConfigApi/TestValues',
                type: 'GET',
                success: function(response) {
                    if (response.success) {
                        window.aocrConfig = { valores: response.data, cargado: true };
                        ejecutarTestConfigurable();
                    } else {
                        console.error('❌ No se pudo obtener configuración');
                        ejecutarTestConDefecto();
                    }
                },
                error: function() {
                    console.error('❌ Error al cargar configuración, usando valores por defecto');
                    ejecutarTestConDefecto();
                }
            });
        }
        return;
    }
    
    ejecutarTestConfigurable();
}

// ====== FUNCIÓN DE TEST CON CONFIGURACIÓN ======
function ejecutarTestConfigurable() {
    const config = window.aocrConfig.valores;
    
    const testVm = {
        Solicitud: {
            CodigoSolicitud: 0,
            NombreOperador: config.operadorDefecto || config.TEST_EMPRESA_NOMBRE || 'AERONÁUTICA CIVIL',
            RepresentanteLegal: config.representanteDefecto || 'Representante Legal Demo',
            CedulaRepresentante: config.cedulaDefecto || '1234567890',
            Direccion: config.direccionDefecto || config.TEST_EMPRESA_DIRECCION || 'Av. El Dorado # 103-15',
            Telefono: config.telefonoDefecto || config.TEST_EMPRESA_TELEFONO || '+57 1 425-1000',
            Email: config.emailDefecto || config.TEST_EMPRESA_EMAIL || 'info@aerocivil.gov.co',
            Ruc: config.rucDefecto || '1234567890001',
            RazonSocial: config.operadorDefecto || config.TEST_EMPRESA_NOMBRE || 'AERONÁUTICA CIVIL',
            TipoOperacion: 'Certificación AOCR (Configurable)',
            DescripcionOperacion: 'Solicitud generada con datos configurables desde BD',
            ObservacionesGenerales: 'Test realizado con configuración desde parámetros DB',
            Estado: 'BORRADOR',
            TipoSolicitud: 1
        },
        Banco: config.bancoDefecto || 'BANCO DE LA REPÚBLICA',
        NumeroComprobante: 'CFG-' + new Date().getTime().toString().slice(-6),
        Aeronaves: [{
            Marca: 'Boeing',
            Modelo: '737',
            Serie: 'CFG123',
            Matricula: 'HC-CFG',
            Configuracion: 'Pasajeros',
            EtapaRuido: '3'
        }]
    };

    console.log('📊 Datos de test configurables:', testVm);

    enviarTestFormulario(testVm, 'CONFIGURABLE');
}

// ====== FUNCIÓN DE TEST CON VALORES POR DEFECTO ======
function ejecutarTestConDefecto() {
    const testVm = {
        Solicitud: {
            CodigoSolicitud: 0,
            NombreOperador: 'AERONÁUTICA CIVIL (Por defecto)',
            RepresentanteLegal: 'Representante Legal Por Defecto',
            CedulaRepresentante: '0000000000',
            Direccion: 'Dirección no configurada',
            Telefono: 'Teléfono no configurado',
            Email: 'email@noconfigurado.com',
            Ruc: '0000000000001',
            RazonSocial: 'RAZÓN SOCIAL POR DEFECTO',
            TipoOperacion: 'Sin configuración',
            DescripcionOperacion: 'Test sin configuración disponible',
            ObservacionesGenerales: 'ADVERTENCIA: Configuración no disponible',
            Estado: 'BORRADOR',
            TipoSolicitud: 1
        },
        Banco: 'BANCO POR DEFECTO',
        NumeroComprobante: 'DEF-' + new Date().getTime().toString().slice(-6),
        Aeronaves: [{
            Marca: 'Sin',
            Modelo: 'Configurar',
            Serie: 'DEF123',
            Matricula: 'HC-DEF',
            Configuracion: 'Por defecto',
            EtapaRuido: '0'
        }]
    };

    console.log('⚠️ Usando datos por defecto:', testVm);
    enviarTestFormulario(testVm, 'POR DEFECTO');
}

// ====== FUNCIÓN COMÚN PARA ENVIAR TEST ======
function enviarTestFormulario(viewModel, tipo) {
    $.ajax({
        url: '/SolicitudAOCR/FormularioCompleto',
        type: 'POST',
        data: JSON.stringify(viewModel),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        timeout: 10000,
        success: function (response) {
            console.log(`TEST FORMULARIO (${tipo}) SUCCESS:`, response);
            Swal.fire({
                title: `✅ Test ${tipo} Exitoso`,
                text: `Datos ${tipo.toLowerCase()} procesados correctamente`,
                icon: 'success'
            });
        },
        error: function (xhr, status, error) {
            console.error(`TEST FORMULARIO (${tipo}) ERROR:`, { status: xhr.status, error: error });
            Swal.fire({
                title: `❌ Test ${tipo} Fallido`,
                text: `Status: ${xhr.status}, Error: ${error}`,
                icon: 'error'
            });
        }
    });
}

// ====== FUNCIÓN MAPEAR CAMPOS CON CONFIGURACIÓN ======
function mapearCamposVMConConfiguracion() {
    // Obtener configuración actual
    const config = window.aocrConfig && window.aocrConfig.cargado ? window.aocrConfig.valores : {};
    
    // Obtener tipos de operación
    const tipos = [];
    if ($('#opsRegulares').is(':checked')) tipos.push('Ops Regulares');
    if ($('#opsNoRegulares').is(':checked')) tipos.push('Ops No Regulares');
    if ($('#pasajeros').is(':checked')) tipos.push('Pasajeros/Carga/Correo');
    if ($('#carga').is(':checked')) tipos.push('Carga');
    
    // Actualizar hidden inputs antes de mapear
    $('#nombreOperador').val($('#nombreCompania').val() || config.operadorDefecto || config.TEST_EMPRESA_NOMBRE || '');
    $('#rucOperador').val($('#rucRepresentante').val() || config.rucDefecto || '');
    $('#telefonoOperador').val($('#telefonoEcuador').val() || config.telefonoDefecto || '');
    
    var razonSocialVal = $('#razonSocial').val() || $('#nombreCompania').val() || config.operadorDefecto || config.TEST_EMPRESA_NOMBRE || '';
    $('#razonSocial').val(razonSocialVal);
    
    $('#tipoOperacion').val(tipos.join(' | '));
    $('#descripcionOperacion').val($('#resumenOperaciones').val() || '');
    $('#observacionesGenerales').val($('#conceptoFacturaPago').val() || '');
    
    // Retornar objeto con todos los valores (configurables como fallback)
    return {
        NombreOperador: $('#nombreCompania').val() || config.operadorDefecto || config.TEST_EMPRESA_NOMBRE || 'EMPRESA NO CONFIGURADA',
        RepresentanteLegal: $('#nombreRepresentante').val() || config.representanteDefecto || 'Representante Legal',
        CedulaRepresentante: $('#rucRepresentante').val() || config.cedulaDefecto || '0000000000',
        Direccion: $('#direccionEcuador').val() || config.direccionDefecto || config.TEST_EMPRESA_DIRECCION || 'Dirección no configurada',
        Telefono: $('#telefonoEcuador').val() || $('#telefonoCompania').val() || config.telefonoDefecto || config.TEST_EMPRESA_TELEFONO || 'Teléfono no configurado',
        Email: $('#emailEcuador').val() || $('#correoCompania').val() || config.emailDefecto || config.TEST_EMPRESA_EMAIL || 'email@noconfigurado.com',
        Ruc: $('#rucRepresentante').val() || config.rucDefecto || '0000000000001',
        RazonSocial: razonSocialVal,
        TipoOperacion: tipos.join(' | '),
        DescripcionOperacion: $('#resumenOperaciones').val() || 'Sin descripción',
        ObservacionesGenerales: $('#conceptoFacturaPago').val() || 'Sin observaciones'
    };
}

// ====== REEMPLAZO PARA GUARDARFORMULARIO ======
function guardarFormularioConfigurable() {
    // Recopilar aeronaves de la tabla
    var aeronaves = [];
    $('#tablaAeronaves tbody tr').each(function() {
        var $tr = $(this);
        var aeronave = {
            Marca: $tr.find('td').eq(0).text().trim(),
            Modelo: $tr.find('td').eq(1).text().trim(),
            Serie: $tr.find('td').eq(2).text().trim(),
            Matricula: $tr.find('td').eq(3).text().trim(),
            Configuracion: $tr.find('td').eq(4).text().trim(),
            EtapaRuido: $tr.find('td').eq(5).text().trim()
        };
        
        if (aeronave.Matricula) {
            aeronaves.push(aeronave);
        }
    });
    
    // Validación: debe haber al menos una aeronave
    if (aeronaves.length === 0) {
        Swal.fire({
            title: 'Validación',
            text: 'Por favor, agregue al menos una aeronave.',
            icon: 'warning',
            confirmButtonText: 'OK'
        });
        return;
    }
    
    // Mapear campos con configuración
    var datosMapeados = mapearCamposVMConConfiguracion();
    console.log('📋 Datos mapeados con configuración:', datosMapeados);
    
    // Obtener código de solicitud
    var codigoSolicitud = parseInt($('#codigoSolicitud').val()) || 0;
    
    // Construir viewmodel completo con configuración
    const vm = {
        Solicitud: {
            CodigoSolicitud: codigoSolicitud,
            NombreOperador: datosMapeados.NombreOperador,
            RepresentanteLegal: datosMapeados.RepresentanteLegal,
            CedulaRepresentante: datosMapeados.CedulaRepresentante,
            Direccion: datosMapeados.Direccion,
            Telefono: datosMapeados.Telefono,
            Email: datosMapeados.Email,
            Ruc: datosMapeados.Ruc,
            RazonSocial: datosMapeados.RazonSocial,
            TipoOperacion: datosMapeados.TipoOperacion,
            DescripcionOperacion: datosMapeados.DescripcionOperacion,
            ObservacionesGenerales: datosMapeados.ObservacionesGenerales,
            Estado: 'BORRADOR',
            TipoSolicitud: 1
        },
        Banco: $('#banco').val() || 'BANCO NO ESPECIFICADO',
        NumeroComprobante: $('#numeroComprobante').val() || '',
        Aeronaves: aeronaves
    };
    
    console.log('💾 Guardando con datos configurables:', vm);
    
    // Enviar solicitud
    $.ajax({
        url: '/SolicitudAOCR/FormularioCompleto',
        type: 'POST',
        data: JSON.stringify(vm),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        timeout: 30000,
        success: function (r) {
            if (r && r.success === true) {
                Swal.fire({
                    title: '✅ Éxito',
                    text: r.mensaje || 'Solicitud guardada correctamente',
                    icon: 'success'
                }).then(() => {
                    if (r.id && r.id > 0) {
                        window.location.href = '/SolicitudAOCR/Detalle/' + r.id;
                    } else {
                        window.location.href = '/SolicitudAOCR/MisSolicitudes';
                    }
                });
            } else {
                Swal.fire('❌ Error', r.mensaje || 'Error al guardar', 'error');
            }
        },
        error: function (xhr, status, error) {
            console.error('💥 Error al guardar:', { status: xhr.status, error: error });
            
            let mensaje = 'Error al guardar la solicitud.';
            if (status === 'timeout') mensaje = 'Tiempo agotado. Intente nuevamente.';
            else if (xhr.status === 401) mensaje = 'Sesión expirada. Inicie sesión nuevamente.';
            else if (xhr.status === 500) mensaje = 'Error del servidor. Intente más tarde.';
            
            Swal.fire('❌ Error', mensaje, 'error');
        }
    });
}