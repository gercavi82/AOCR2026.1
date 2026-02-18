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
            }).catch((error) => {
                console.error('Error en confirmAction:', error);
                showNotification('No se pudo procesar la confirmacion.', 'error');
            });
        } else if (confirm(pregunta) && typeof callback === 'function') {
            callback();
        }
    }

    // Evita "Uncaught (in promise)" silencioso en cliente.
    window.addEventListener('unhandledrejection', function (event) {
        try {
            var reason = event && event.reason ? event.reason : event;
            console.error('Unhandled Promise Rejection:', reason);

            // Si el backend devolvió el objeto estándar { ok:false, message, traceId }
            if (reason && (reason.ok === false || reason.success === false)) {
                var msg = reason.message || 'Error en el servidor';
                var trace = reason.traceId ? ' (traceId: ' + reason.traceId + ')' : '';
                showNotification(msg + trace, 'error');
            } else {
                showNotification('Ocurrio un error inesperado. Revise e intente nuevamente.', 'error');
            }

            if (event && typeof event.preventDefault === 'function') {
                event.preventDefault();
            }
        } catch (_) {
            // no-op
        }
    });

    // Global jQuery AJAX error fallback (cubre llamadas que no usan .fail)
    if (typeof jQuery !== 'undefined') {
        $(document).ajaxError(function (event, jqxhr, settings, thrownError) {
            try {
                console.error('Global AJAX error:', settings && settings.url, thrownError, jqxhr);
                var message = 'Error al comunicarse con el servidor';

                if (jqxhr && jqxhr.responseJSON) {
                    var body = jqxhr.responseJSON;
                    if (body && (body.ok === false || body.success === false) && body.message) {
                        message = body.message;
                        if (body.traceId) message += ' (traceId: ' + body.traceId + ')';
                    }
                } else if (jqxhr && jqxhr.status === 401) {
                    message = 'No autorizado. Por favor inicie sesión.';
                }

                showNotification(message, 'error');
            } catch (e) {
                console.error(e);
            }
        });
    }

    // Helpers para manejo consistente de promesas/respuestas
    function _handleApiResponse(resp) {
        // Normalizar respuesta { ok: true/false } o { success: true/false }
        if (!resp) return Promise.reject(new Error('Respuesta vacía'));
        if (resp.ok === false || resp.success === false) {
            var err = new Error(resp.message || 'Error en la operación');
            err.payload = resp;
            return Promise.reject(err);
        }
        return Promise.resolve(resp);
    }

    function _safe(promise) {
        if (!promise || typeof promise.then !== 'function') return promise;
        promise.catch(function (err) {
            try {
                console.error('Site.safe caught error:', err);
                // Mostrar mensaje amigable
                if (err && err.payload && (err.payload.traceId || err.payload.message)) {
                    var m = err.payload.message || 'Error en el servidor';
                    var t = err.payload.traceId ? ' (traceId: ' + err.payload.traceId + ')' : '';
                    showNotification(m + t, 'error');
                } else {
                    showNotification('Ocurrio un error inesperado. Revise la consola.', 'error');
                }
            } catch (e) {
                // noop
            }
        });
        return promise;
    }

    // Exponer helpers en Site
    window.Site = window.Site || {};
    window.Site.handleApiResponse = window.Site.handleApiResponse || _handleApiResponse;
    window.Site.safe = window.Site.safe || _safe;

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
