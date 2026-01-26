// ============================================
// CONFIGURACIÓN DE ÓRDENES DE RECAUDACIÓN
// ============================================

// Variable global para la instancia de DataTables
var tablaOrdenesDT = null;

// ============================================
// 1. FUNCIONES DE ESPERA INTELIGENTE
// ============================================

/**
 * Espera a que jQuery esté disponible
 */
function waitForJQuery(callback) {
    console.log('Esperando jQuery...');

    if (window.jQuery && window.jQuery.fn) {
        console.log('✅ jQuery cargado correctamente');
        callback();
    } else {
        console.log('⏳ jQuery no disponible, reintentando...');
        setTimeout(function () {
            waitForJQuery(callback);
        }, 100);
    }
}

/**
 * Espera a que DataTables esté disponible
 */
function waitForDataTables(callback) {
    console.log('Esperando DataTables...');

    if (window.jQuery && window.jQuery.fn && window.jQuery.fn.DataTable) {
        console.log('✅ DataTables cargado correctamente');
        callback();
    } else {
        console.log('⏳ DataTables no disponible, reintentando...');
        setTimeout(function () {
            waitForDataTables(callback);
        }, 100);
    }
}

/**
 * Espera a que Bootstrap esté disponible
 */
function waitForBootstrap(callback) {
    console.log('Esperando Bootstrap...');

    if (window.bootstrap && window.bootstrap.Tooltip) {
        console.log('✅ Bootstrap cargado correctamente');
        callback();
    } else {
        console.log('⏳ Bootstrap no disponible, reintentando...');
        setTimeout(function () {
            waitForBootstrap(callback);
        }, 100);
    }
}

// ============================================
// 2. MANEJO DE ERRORES DE IMÁGENES
// ============================================

/**
 * Configura manejo de errores en imágenes
 */
function configurarManejoImagenes() {
    console.log('Configurando manejo de imágenes...');

    $(document).on('error', 'img', function () {
        var $img = $(this);
        var src = $img.attr('src') || '';

        console.log('❌ Error cargando imagen:', src);

        // Si es una imagen de usuario
        if (src.includes('user') || src.includes('usuario') || src.includes('profile')) {
            // Si ya intentamos la imagen por defecto y también falló
            if (src.includes('user-default') || src.includes('default')) {
                $img.replaceWith(
                    '<div class="user-img-default" data-bs-toggle="tooltip" title="Imagen no disponible">' +
                    '<i class="fas fa-user"></i>' +
                    '</div>'
                );
                inicializarTooltips(); // Re-inicializar tooltips
            } else {
                // Intentar cargar imagen por defecto
                $img.attr('src', 'Content/imagenes/user-default.png');
            }
        }
    });

    console.log('✅ Manejo de imágenes configurado');
}

// ============================================
// 3. INICIALIZACIÓN DE TOOLTIPS
// ============================================

/**
 * Inicializa los tooltips de Bootstrap 5
 */
function inicializarTooltips() {
    console.log('Inicializando tooltips...');

    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        // Destruir tooltips existentes para evitar duplicados
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.forEach(function (tooltipTriggerEl) {
            var existingTooltip = bootstrap.Tooltip.getInstance(tooltipTriggerEl);
            if (existingTooltip) {
                existingTooltip.dispose();
            }
        });

        // Crear nuevos tooltips
        var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl, {
                trigger: 'hover'
            });
        });

        console.log('✅ Tooltips inicializados:', tooltipList.length);
    } else {
        console.error('❌ Bootstrap Tooltip no disponible');
    }
}

// ============================================
// 4. CONFIGURACIÓN DE DATATABLES
// ============================================

/**
 * Verifica la estructura de la tabla antes de inicializar
 */
function verificarEstructuraTabla() {
    console.log('Verificando estructura de la tabla...');

    var $tabla = $('#tablaOrdenes');

    if ($tabla.length === 0) {
        console.error('❌ No se encontró la tabla con ID #tablaOrdenes');
        return false;
    }

    // Contar columnas en thead
    var columnasThead = $tabla.find('thead th').length;
    console.log('Columnas en thead:', columnasThead);

    // Contar columnas en la primera fila del tbody
    var primeraFila = $tabla.find('tbody tr:first');
    var columnasTbody = primeraFila.find('td').length;
    console.log('Columnas en primera fila:', columnasTbody);

    if (columnasThead === 0) {
        console.error('❌ No hay columnas definidas en thead');
        return false;
    }

    if (columnasThead !== columnasTbody) {
        console.error('❌ ERROR: Número de columnas no coincide');
        console.error('Thead tiene', columnasThead, 'columnas, pero la primera fila tiene', columnasTbody, 'columnas');
        return false;
    }

    console.log('✅ Estructura de tabla verificada correctamente');
    return true;
}

/**
 * Inicializa DataTables con configuración robusta
 */
function inicializarDataTables() {
    console.log('Inicializando DataTables...');

    // Verificar estructura primero
    if (!verificarEstructuraTabla()) {
        console.error('❌ No se puede inicializar DataTables por error en estructura');
        mostrarErrorTabla();
        return;
    }

    try {
        // Destruir instancia previa si existe
        if ($.fn.DataTable.isDataTable('#tablaOrdenes')) {
            $('#tablaOrdenes').DataTable().destroy();
            $('#tablaOrdenes tbody').empty(); // Limpiar si es necesario
        }

        // Configuración de DataTables
        var config = {
            "language": {
                "url": "https://cdn.datatables.net/plug-ins/1.13.4/i18n/es-ES.json",
                "decimal": ",",
                "thousands": "."
            },
            "responsive": true,
            "autoWidth": false,
            "pageLength": 10,
            "lengthMenu": [[5, 10, 25, 50, -1], [5, 10, 25, 50, "Todos"]],
            "order": [], // Sin orden inicial
            "columnDefs": [
                {
                    "targets": [0], // Primera columna (ID)
                    "visible": true,
                    "searchable": true
                },
                {
                    "targets": '_all',
                    "className": 'text-center align-middle'
                }
            ],
            "drawCallback": function (settings) {
                console.log('DataTables dibujado, reinicializando tooltips...');
                setTimeout(function () {
                    inicializarTooltips();
                }, 100);
            },
            "initComplete": function (settings, json) {
                console.log('✅ DataTables inicializado correctamente');
                inicializarTooltips();
            },
            "error": function (settings, techNote, message) {
                console.error('❌ Error en DataTables:', message);
                mostrarErrorDataTables(message);
            }
        };

        // Inicializar DataTables
        tablaOrdenesDT = $('#tablaOrdenes').DataTable(config);

        // Agregar evento para botones dentro de la tabla
        $('#tablaOrdenes').on('click', '.btn-accion', function (e) {
            e.preventDefault();
            var id = $(this).data('id');
            var accion = $(this).data('accion');
            manejarAccion(id, accion);
        });

    } catch (error) {
        console.error('❌ Error crítico al inicializar DataTables:', error);
        mostrarErrorInicializacion(error.message);
    }
}

// ============================================
// 5. MANEJO DE ERRORES Y MENSAJES
// ============================================

/**
 * Muestra error cuando falla la tabla
 */
function mostrarErrorTabla() {
    Swal.fire({
        icon: 'error',
        title: 'Error en la tabla',
        text: 'La estructura de la tabla es incorrecta. Verifica que el número de columnas coincida.',
        footer: '<a href="#" onclick="recargarPagina()">Recargar página</a>'
    });
}

/**
 * Muestra error de DataTables
 */
function mostrarErrorDataTables(mensaje) {
    Swal.fire({
        icon: 'warning',
        title: 'Error en DataTables',
        text: 'Ocurrió un error al cargar la tabla: ' + mensaje,
        confirmButtonText: 'Reintentar',
        showCancelButton: true,
        cancelButtonText: 'Cerrar'
    }).then((result) => {
        if (result.isConfirmed) {
            setTimeout(function () {
                inicializarDataTables();
            }, 1000);
        }
    });
}

/**
 * Muestra error de inicialización
 */
function mostrarErrorInicializacion(mensaje) {
    console.error('Error de inicialización:', mensaje);

    // Mostrar mensaje en consola y en UI si hay un contenedor para errores
    if ($('#errorContainer').length) {
        $('#errorContainer').html(
            '<div class="alert alert-danger alert-dismissible fade show" role="alert">' +
            '<strong>Error:</strong> ' + mensaje +
            '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>' +
            '</div>'
        );
    }
}

/**
 * Recarga la página
 */
function recargarPagina() {
    window.location.reload();
}

// ============================================
// 6. FUNCIONES DE UTILIDAD
// ============================================

/**
 * Maneja acciones de los botones
 */
function manejarAccion(id, accion) {
    console.log('Acción:', accion, 'para ID:', id);

    switch (accion) {
        case 'ver':
            Swal.fire({
                title: 'Detalles de Orden',
                text: 'Mostrando detalles para orden: ' + id,
                icon: 'info'
            });
            break;

        case 'editar':
            Swal.fire({
                title: 'Editar Orden',
                text: 'Editando orden: ' + id,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Guardar cambios'
            });
            break;

        default:
            console.warn('Acción no reconocida:', accion);
    }
}

/**
 * Verifica el estado de todas las dependencias
 */
function verificarDependencias() {
    console.log('=== VERIFICANDO DEPENDENCIAS ===');
    console.log('jQuery:', typeof jQuery !== 'undefined' ? '✅' : '❌');
    console.log('Bootstrap:', typeof bootstrap !== 'undefined' ? '✅' : '❌');
    console.log('DataTables:', typeof $.fn.DataTable !== 'undefined' ? '✅' : '❌');
    console.log('SweetAlert2:', typeof Swal !== 'undefined' ? '✅' : '❌');
    console.log('================================');
}

// ============================================
// 7. INICIALIZACIÓN PRINCIPAL
// ============================================

/**
 * Función principal de inicialización
 */
function inicializarAplicacion() {
    console.log('🚀 Inicializando aplicación...');

    // Verificar dependencias
    verificarDependencias();

    // Configurar manejo de imágenes
    configurarManejoImagenes();

    // Inicializar tooltips iniciales
    inicializarTooltips();

    // Inicializar DataTables con espera inteligente
    waitForJQuery(function () {
        waitForBootstrap(function () {
            waitForDataTables(function () {
                inicializarDataTables();
            });
        });
    });

    // Configurar eventos adicionales
    $(document).on('click', '[data-action="recargar"]', function () {
        if (tablaOrdenesDT) {
            tablaOrdenesDT.ajax.reload();
        }
    });

    console.log('✅ Aplicación inicializada');
}

// ============================================
// 8. INICIO CUANDO EL DOCUMENTO ESTÁ LISTO
// ============================================

// Versión 1: Usando jQuery.ready
$(document).ready(function () {
    console.log('📄 Documento listo (jQuery)');
    inicializarAplicacion();
});

// Versión 2: Usando DOMContentLoaded como respaldo
document.addEventListener('DOMContentLoaded', function () {
    console.log('📄 Documento listo (DOMContentLoaded)');

    // Si jQuery no está cargado aún, esperar
    if (!window.jQuery) {
        console.log('jQuery no disponible, usando inicialización nativa...');

        // Inicializar tooltips nativos si Bootstrap está disponible
        if (window.bootstrap && bootstrap.Tooltip) {
            var tooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]');
            tooltips.forEach(function (el) {
                new bootstrap.Tooltip(el);
            });
        }

        // Esperar a jQuery para DataTables
        var checkJQuery = setInterval(function () {
            if (window.jQuery) {
                clearInterval(checkJQuery);
                inicializarAplicacion();
            }
        }, 100);
    }
});

// Versión 3: Para window.onload (cuando TODO está cargado)
window.onload = function () {
    console.log('🔄 Ventana completamente cargada');

    // Verificar si DataTables se inicializó correctamente
    setTimeout(function () {
        if (!tablaOrdenesDT && $('#tablaOrdenes').length) {
            console.warn('⚠️ DataTables no se inicializó automáticamente, intentando manualmente...');
            inicializarAplicacion();
        }
    }, 2000);
};

// ============================================
// 9. EXPORTAR FUNCIONES PARA DEBUG
// ============================================

// Hacer funciones disponibles globalmente para depuración
window.debugOrdenes = {
    recargarTabla: function () {
        if (tablaOrdenesDT) {
            tablaOrdenesDT.ajax.reload();
            return 'Tabla recargada';
        }
        return 'Tabla no inicializada';
    },

    verificarTabla: function () {
        return verificarEstructuraTabla();
    },

    reinicializar: function () {
        inicializarAplicacion();
        return 'Aplicación reinicializada';
    },

    getEstado: function () {
        return {
            jQuery: !!window.jQuery,
            bootstrap: !!window.bootstrap,
            dataTables: !!(window.jQuery && $.fn.DataTable),
            tablaInicializada: !!tablaOrdenesDT
        };
    }
};

console.log('📋 ordenes-config.js cargado correctamente');