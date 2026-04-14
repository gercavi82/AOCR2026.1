// app.js - Lógica específica de la aplicación
// ✅ MODIFICADO: Integración segura con layout

(function () {
    // Evitar múltiples cargas
    if (window._APP_JS_LOADED) {
        console.log('ℹ️ app.js ya cargado, omitiendo...');
        return;
    }

    window._APP_JS_LOADED = true;

    console.log('🚀 app.js cargado');

    // Aplicación principal
    var OrdenRecaudacionApp = {
        init: function () {
            // Verificar que jQuery esté disponible
            if (typeof jQuery === 'undefined') {
                console.error('jQuery no disponible para OrdenRecaudacionApp');
                return;
            }

            console.log('✅ Aplicación de Órdenes iniciada');
            this.bindEvents();
            this.initComponents();
        },

        bindEvents: function () {
            // Eventos globales de la aplicación
            $(document).on('click', '.btn-export', this.exportData);
            $(document).on('change', '.filtro-estado', this.filtrarPorEstado);
        },

        initComponents: function () {
            // Inicializar componentes específicos
            console.log('🔧 Inicializando componentes app.js');

            // Puedes inicializar DataTables específicos aquí si es necesario
            // pero el layout principal ya maneja los DataTables globales
        },

        exportData: function () {
            console.log('📤 Exportando datos...');

            // Usar notificación de Site o AOCR
            var message = 'Exportando datos...';
            if (window.Site && Site.notify) {
                Site.notify(message, 'info');
            } else if (window.AOCR && AOCR.showAlert) {
                AOCR.showAlert('Exportar', message, 'info');
            }

            // Lógica de exportación
            // ... tu código de exportación aquí
        },

        filtrarPorEstado: function () {
            var estado = $(this).val();
            console.log('🔍 Filtrando por estado:', estado);

            // Buscar tabla de ordenes en esta página
            var $tabla = $('#tablaOrdenes');
            if ($tabla.length && $.fn.DataTable && $.fn.DataTable.isDataTable('#tablaOrdenes')) {
                // Asumiendo que la columna 4 (índice 3) es la de estado
                $tabla.DataTable().column(3).search(estado).draw();
            }
        }
    };

    // Inicializar cuando todo esté listo
    // Esperar a que jQuery esté disponible y el DOM esté listo
    function initializeApp() {
        if (typeof jQuery !== 'undefined') {
            $(document).ready(function () {
                OrdenRecaudacionApp.init();
            });
        } else {
            // Si jQuery no está disponible aún, esperar
            console.log('⏳ app.js esperando jQuery...');
            setTimeout(initializeApp, 100);
        }
    }

    // Iniciar
    initializeApp();

    // Hacer disponible globalmente
    window.OrdenRecaudacionApp = OrdenRecaudacionApp;
})();