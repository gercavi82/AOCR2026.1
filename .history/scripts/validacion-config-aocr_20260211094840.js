// =============================================
// SCRIPT DE VALIDACIÓN CONFIGURACIÓN AOCR
// Verifica que no queden valores hardcodeados
// =============================================

function validarConfiguracionCompleta() {
    console.log('🔍 === VALIDACIÓN COMPLETA DE CONFIGURACIÓN AOCR ===');
    
    var resultados = {
        configuracionCargada: false,
        parametrosEncontrados: 0,
        valoresHardcoded: [],
        funcionesCorrectas: [],
        errores: []
    };
    
    // 1. Verificar que la configuración se haya cargado
    if (window.aocrConfig && window.aocrConfig.cargado) {
        resultados.configuracionCargada = true;
        resultados.parametrosEncontrados = Object.keys(window.aocrConfig.valores).length;
        console.log('✅ Configuración cargada con', resultados.parametrosEncontrados, 'parámetros');
    } else {
        resultados.errores.push('❌ Configuración AOCR no cargada');
        console.error('❌ window.aocrConfig no está disponible');
    }
    
    // 2. Verificar funciones disponibles
    const funcionesRequeridas = [
        'cargarConfiguracionAOCR',
        'obtenerDatosTestConfigurables', 
        'testFormularioCompletoConfigurable',
        'mapearCamposVMConfigurable'
    ];
    
    funcionesRequeridas.forEach(function(nombreFuncion) {
        if (typeof window[nombreFuncion] === 'function') {
            resultados.funcionesCorrectas.push(nombreFuncion);
            console.log('✅ Función disponible:', nombreFuncion);
        } else {
            resultados.errores.push('❌ Función faltante: ' + nombreFuncion);
            console.error('❌ Función no encontrada:', nombreFuncion);
        }
    });
    
    // 3. Buscar valores hardcodeados restantes (patrones comunes)
    const patronesHardcoded = [
        'TEST OPERADOR',
        'TEST EMPRESA', 
        'TEST REPRESENTANTE',
        'TEST DIRECCION',
        'test@test.com',
        '0999999999'
    ];
    
    // Verificar en el DOM
    patronesHardcoded.forEach(function(patron) {
        if (document.body.innerHTML.includes(patron)) {
            resultados.valoresHardcoded.push(patron);
            console.warn('⚠️ Valor hardcodeado encontrado:', patron);
        }
    });
    
    // 4. Verificar scripts cargados
    const scriptsRequeridos = [
        'aocr-config.js',
        'formulario-config-loader.js'
    ];
    
    scriptsRequeridos.forEach(function(script) {
        var scriptEncontrado = false;
        $('script').each(function() {
            if ($(this).attr('src') && $(this).attr('src').includes(script)) {
                scriptEncontrado = true;
            }
        });
        
        if (scriptEncontrado) {
            console.log('✅ Script cargado:', script);
        } else {
            resultados.errores.push('❌ Script faltante: ' + script);
            console.error('❌ Script no encontrado:', script);
        }
    });
    
    // 5. Probar conexión con API
    if (resultados.configuracionCargada) {
        $.ajax({
            url: '/ConfigApi/TestValues',
            type: 'GET',
            timeout: 5000,
            success: function(response) {
                if (response.success && response.data) {
                    console.log('✅ API de configuración respondiendo correctamente');
                    console.log('📊 Datos disponibles:', Object.keys(response.data));
                } else {
                    resultados.errores.push('❌ API responde pero sin datos válidos');
                }
            },
            error: function(xhr, status, error) {
                resultados.errores.push('❌ Error en API: ' + error);
                console.error('❌ No se puede conectar con ConfigApiController:', error);
            }
        });
    }
    
    // 6. Mostrar resumen
    setTimeout(function() {
        console.log('\n📋 === RESUMEN DE VALIDACIÓN ===');
        console.log('Configuración cargada:', resultados.configuracionCargada);
        console.log('Parámetros encontrados:', resultados.parametrosEncontrados);
        console.log('Funciones correctas:', resultados.funcionesCorrectas.length);
        console.log('Valores hardcoded restantes:', resultados.valoresHardcoded.length);
        console.log('Errores encontrados:', resultados.errores.length);
        
        if (resultados.errores.length === 0 && resultados.valoresHardcoded.length === 0) {
            console.log('🎉 ¡VALIDACIÓN EXITOSA! No hay valores hardcodeados');
            
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    title: '🎉 ¡Configuración Exitosa!',
                    text: `${resultados.parametrosEncontrados} parámetros configurables cargados correctamente`,
                    icon: 'success',
                    timer: 3000
                });
            }
        } else {
            console.warn('⚠️ Se encontraron ' + (resultados.errores.length + resultados.valoresHardcoded.length) + ' problemas');
            
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    title: '⚠️ Configuración Incompleta',
                    text: `Errores: ${resultados.errores.length}, Hardcoded: ${resultados.valoresHardcoded.length}`,
                    icon: 'warning'
                });
            }
        }
        
        return resultados;
    }, 2000);
}

// =============================================
// FUNCIÓN PARA MOSTRAR CONFIGURACIÓN ACTUAL
// =============================================
function mostrarConfiguracionActual() {
    console.log('📊 === CONFIGURACIÓN ACTUAL ===');
    
    if (window.aocrConfig && window.aocrConfig.valores) {
        console.table(window.aocrConfig.valores);
        
        // Crear tabla HTML para mostrar en modal si está disponible
        if (typeof Swal !== 'undefined') {
            var tablaHtml = '<table class="table table-sm"><thead><tr><th>Parámetro</th><th>Valor</th></tr></thead><tbody>';
            
            Object.keys(window.aocrConfig.valores).forEach(function(key) {
                // Escape key and value for safe HTML insertion
                var safeKey = (typeof AOCR !== 'undefined' && AOCR.escapeHtml) ? AOCR.escapeHtml(key) : String(key);
                var safeVal = (typeof AOCR !== 'undefined' && AOCR.escapeHtml) ? AOCR.escapeHtml(String(window.aocrConfig.valores[key] || '')) : String(window.aocrConfig.valores[key] || '');
                tablaHtml += `<tr><td><code>${safeKey}</code></td><td>${safeVal}</td></tr>`;
            });
            
            tablaHtml += '</tbody></table>';
            
            Swal.fire({
                title: '📊 Configuración AOCR Actual',
                html: tablaHtml,
                width: '80%',
                showConfirmButton: true,
                confirmButtonText: 'Cerrar'
            });
        }
    } else {
        console.error('❌ No hay configuración disponible');
        
        if (typeof Swal !== 'undefined') {
            Swal.fire('❌ Error', 'No hay configuración disponible', 'error');
        }
    }
}

// =============================================
// FUNCIÓN PARA RECARGAR CONFIGURACIÓN
// =============================================
function recargarConfiguracion() {
    console.log('🔄 Recargando configuración AOCR...');
    
    if (typeof cargarConfiguracionAOCR === 'function') {
        cargarConfiguracionAOCR();
        
        setTimeout(function() {
            console.log('✅ Configuración recargada');
            validarConfiguracionCompleta();
        }, 1500);
    } else {
        console.error('❌ Función cargarConfiguracionAOCR no disponible');
    }
}

// =============================================
// INICIALIZACIÓN AUTOMÁTICA
// =============================================
$(document).ready(function() {
    // Esperar un poco para que otros scripts se carguen
    setTimeout(function() {
        console.log('🚀 Iniciando validación automática de configuración...');
        validarConfiguracionCompleta();
    }, 3000);
    
    // Agregar funciones al scope global para debugging
    window.validarConfiguracionCompleta = validarConfiguracionCompleta;
    window.mostrarConfiguracionActual = mostrarConfiguracionActual;
    window.recargarConfiguracion = recargarConfiguracion;
});

// =============================================
// COMANDOS DE CONSOLA PARA DEBUGGING
// =============================================
console.log(`
🔧 === COMANDOS DISPONIBLES PARA DEBUGGING ===
• validarConfiguracionCompleta() - Verifica configuración
• mostrarConfiguracionActual() - Muestra parámetros cargados  
• recargarConfiguracion() - Recarga desde API
• testFormularioCompletoConfigurable() - Test con datos configurables
`);