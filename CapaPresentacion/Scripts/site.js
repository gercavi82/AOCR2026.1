// site.js - Funciones generales del sitio
// ✅ MODIFICADO: Verificar que no haya duplicación

(function () {
    // Evitar múltiples cargas
    if (window._SITE_JS_LOADED) {
        console.log('ℹ️ site.js ya cargado, omitiendo...');
        return;
    }

    window._SITE_JS_LOADED = true;

    console.log('🌐 site.js cargado');

    // Configuración global del sitio
    var SiteConfig = {
        debugMode: true,
        apiUrl: '/api',
        version: '1.0.0'
    };

    // Función para mostrar notificaciones
    function showNotification(mensaje, tipo = 'info') {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                icon: tipo,
                title: mensaje,
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000
            });
        } else {
            alert(mensaje);
        }
    }

    // Función para confirmaciones
    function confirmAction(pregunta, callback) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: '¿Estás seguro?',
                text: pregunta,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Sí',
                cancelButtonText: 'No'
            }).then((result) => {
                if (result.isConfirmed && typeof callback === 'function') {
                    callback();
                }
            });
        } else if (confirm(pregunta) && typeof callback === 'function') {
            callback();
        }
    }

    // Verificar si ya existe Site para evitar sobreescribir
    if (!window.Site) {
        window.Site = {
            config: SiteConfig,
            notify: showNotification,
            confirm: confirmAction
        };
    } else {
        console.log('ℹ️ Site ya existe, solo añadiendo funciones faltantes');
        window.Site.notify = window.Site.notify || showNotification;
        window.Site.confirm = window.Site.confirm || confirmAction;
    }
})();