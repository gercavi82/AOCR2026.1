/**
 * Módulo centralizado: Formulario Emisión AOCR
 * Loader idempotente, request JSON seguro (sin seguir 302), guardado por sección.
 */
(function (window, $) {
    'use strict';

    var MODULE = {
        config: {},
        _inFlight: {},
        _hooks: null
    };

    function log() {
        if (window.console && console.log) {
            console.log.apply(console, arguments);
        }
    }

    function logError() {
        if (window.console && console.error) {
            console.error.apply(console, arguments);
        }
    }

    function aocr() {
        return window.AOCR || {};
    }

    MODULE.showLoader = function (message) {
        var api = aocr();
        if (api.lockAocrForm) {
            api.lockAocrForm('#formularioEmisionAOCR');
        }
        if (api.showAocrLoader) {
            api.showAocrLoader(message || 'Guardando...');
        }
        log('[AOCR_FE] showLoader:', message || 'Guardando...');
    };

    MODULE.hideLoader = function (origen) {
        var api = aocr();
        if (api.hideAocrLoader) {
            api.hideAocrLoader(origen || 'fe', { force: true });
        }
        if (api.unlockAocrForm) {
            api.unlockAocrForm('#formularioEmisionAOCR');
        }
        if (api.liberarOverlayUi) {
            api.liberarOverlayUi({ forzarCierre: true });
        }
        log('[AOCR_FE] hideLoader:', origen || 'fe');
    };

    MODULE.lockForm = function () {
        var api = aocr();
        if (api.lockAocrForm) {
            api.lockAocrForm('#formularioEmisionAOCR');
        }
    };

    MODULE.unlockForm = function () {
        var api = aocr();
        if (api.unlockAocrForm) {
            api.unlockAocrForm('#formularioEmisionAOCR');
        }
    };

    MODULE.getAntiForgeryToken = function () {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    };

    MODULE.toast = function (tipo, titulo, texto) {
        if (typeof Swal === 'undefined') {
            return;
        }
        Swal.fire({
            toast: true,
            position: 'top-end',
            icon: tipo || 'success',
            title: titulo || '',
            text: texto || '',
            showConfirmButton: false,
            timer: 2800,
            timerProgressBar: true
        });
    };

    MODULE.isHtmlResponse = function (text) {
        if (!text) {
            return false;
        }
        var t = String(text).trim().toLowerCase();
        return t.indexOf('<!doctype') === 0 || t.indexOf('<html') === 0;
    };

    MODULE.isLoginMarkup = function (text) {
        var api = aocr();
        if (api.isLoginMarkup) {
            return api.isLoginMarkup(text);
        }
        return MODULE.isHtmlResponse(text) && (
            text.indexOf('Account/Login') >= 0 ||
            text.indexOf('name="Password"') >= 0 ||
            text.indexOf('Iniciar sesión') >= 0 ||
            text.indexOf('Iniciar Sesión') >= 0
        );
    };

    MODULE.isCompanySelectionMarkup = function (text) {
        return text && (
            text.indexOf('SeleccionarCompania') >= 0 ||
            text.indexOf('Seleccionar Compañía') >= 0 ||
            text.indexOf('Seleccionar Compania') >= 0
        );
    };

    /**
     * Petición JSON segura: no sigue redirecciones 302 (redirect: manual).
     */
    MODULE.requestJson = function (url, options) {
        options = options || {};
        var token = MODULE.getAntiForgeryToken();
        var headers = {
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json, text/plain, */*',
            'RequestVerificationToken': token
        };

        if (options.headers) {
            Object.keys(options.headers).forEach(function (k) {
                headers[k] = options.headers[k];
            });
        }

        var fetchOpts = {
            method: options.method || 'POST',
            credentials: 'same-origin',
            redirect: 'manual',
            headers: headers
        };

        if (options.json !== undefined) {
            headers['Content-Type'] = 'application/json; charset=utf-8';
            fetchOpts.body = JSON.stringify(options.json);
        } else if (options.body !== undefined) {
            fetchOpts.body = options.body;
        }

        log('[AOCR_FE] requestJson inicio', url, options.json || '(body)');

        return window.fetch(url, fetchOpts).then(function (response) {
            if (response.type === 'opaqueredirect' || response.status === 301 || response.status === 302) {
                logError('[AOCR_FE] Redirección detectada (no seguida). Status=', response.status);
                return {
                    _meta: { redirected: true, status: response.status },
                    success: false,
                    requiresCompanySelection: true,
                    message: 'Debe seleccionar una compañía activa antes de continuar.',
                    redirectUrl: MODULE.config.urlSeleccionarCompania || '/Account/SeleccionarCompania'
                };
            }

            return response.text().then(function (text) {
                log('[AOCR_FE] Respuesta raw (primeros 200):', (text || '').substring(0, 200));

                if (MODULE.isLoginMarkup(text)) {
                    return {
                        _meta: { html: true, login: true, status: response.status },
                        success: false,
                        requiresLogin: true,
                        message: 'Su sesión expiró. Debe iniciar sesión nuevamente.',
                        redirectUrl: MODULE.config.urlLogin || '/Account/Login'
                    };
                }

                if (MODULE.isCompanySelectionMarkup(text) || (MODULE.isHtmlResponse(text) && text.indexOf('Seleccionar') >= 0)) {
                    return {
                        _meta: { html: true, company: true, status: response.status },
                        success: false,
                        requiresCompanySelection: true,
                        message: 'Debe seleccionar una compañía activa antes de continuar.',
                        redirectUrl: MODULE.config.urlSeleccionarCompania || '/Account/SeleccionarCompania'
                    };
                }

                if (MODULE.isHtmlResponse(text)) {
                    return {
                        _meta: { html: true, status: response.status },
                        success: false,
                        message: 'El servidor respondió con HTML en lugar de JSON. Recargue la página o seleccione la compañía activa.',
                        redirectUrl: null
                    };
                }

                var payload = null;
                try {
                    payload = text ? JSON.parse(text) : null;
                } catch (e) {
                    return {
                        _meta: { parseError: true, status: response.status },
                        success: false,
                        message: 'El servidor respondió con un formato inválido.',
                        redirectUrl: null
                    };
                }

                if (!response.ok && payload && payload.success !== true) {
                    payload.success = false;
                    if (!payload.message && !payload.mensaje) {
                        payload.message = 'Error HTTP ' + response.status;
                    }
                }

                return payload;
            });
        });
    };

    MODULE.handleAjaxError = function (payload, origen) {
        MODULE.hideLoader(origen + '-error');

        if (!payload) {
            MODULE.toast('error', 'Error', 'No se recibió respuesta del servidor.');
            return;
        }

        if (payload.requiresCompanySelection || payload.code === 403 && payload.requiresCompanySelection !== false) {
            var urlCompania = payload.redirectUrl || MODULE.config.urlSeleccionarCompania;
            var returnUrl = window.location.pathname + window.location.search;
            if (urlCompania.indexOf('returnUrl') < 0 && returnUrl) {
                urlCompania += (urlCompania.indexOf('?') >= 0 ? '&' : '?') + 'returnUrl=' + encodeURIComponent(returnUrl);
            }
            MODULE.toast('warning', 'Compañía requerida', payload.message || payload.mensaje || 'Seleccione la compañía activa.');
            setTimeout(function () { window.location.href = urlCompania; }, 1200);
            return;
        }

        if (payload.requiresLogin) {
            MODULE.toast('warning', 'Sesión expirada', payload.message || 'Inicie sesión nuevamente.');
            setTimeout(function () {
                window.location.href = payload.redirectUrl || MODULE.config.urlLogin || '/Account/Login';
            }, 1200);
            return;
        }

        MODULE.toast('error', 'Error', payload.message || payload.mensaje || 'No se pudo completar la operación.');
    };

    MODULE.handleSuccess = function (payload, origen, onSuccess) {
        if (payload && (payload.success === true || payload.ok === true)) {
            log('[AOCR_FE] success', origen, payload);
            if (typeof onSuccess === 'function') {
                try {
                    onSuccess(payload);
                } catch (e) {
                    logError('[AOCR_FE] Excepción post-success:', e);
                    MODULE.toast('info', 'Guardado', 'Los datos se guardaron, pero hubo un error al actualizar la pantalla.');
                }
            }
            MODULE.toast('success', 'Guardado', payload.message || payload.mensaje || 'Operación exitosa.');
            return true;
        }
        MODULE.handleAjaxError(payload, origen);
        return false;
    };

    MODULE.runGuardado = function (clave, url, options, onSuccess) {
        if (MODULE._inFlight[clave]) {
            log('[AOCR_FE] Ignorado doble envío:', clave);
            return Promise.resolve();
        }
        MODULE._inFlight[clave] = true;
        MODULE.showLoader(options.loaderMessage || 'Guardando...');

        return MODULE.requestJson(url, options)
            .then(function (payload) {
                if (payload && (payload.success === true || payload.ok === true)) {
                    MODULE.handleSuccess(payload, clave, onSuccess);
                } else {
                    MODULE.handleAjaxError(payload, clave);
                }
                return payload;
            })
            .catch(function (err) {
                logError('[AOCR_FE] Excepción requestJson:', err);
                MODULE.handleAjaxError({
                    success: false,
                    message: 'Error de comunicación con el servidor. Verifique su conexión e intente nuevamente.'
                }, clave);
            })
            .then(function (result) {
                MODULE._inFlight[clave] = false;
                MODULE.hideLoader(clave + '-finally');
                log('[AOCR_FE] finally ejecutado:', clave);
                return result;
            });
    };

    MODULE.guardarProgreso = function (seccion) {
        var hooks = MODULE._hooks;
        if (!hooks || typeof hooks.buildGuardarProgresoPayload !== 'function') {
            MODULE.toast('error', 'Error', 'Configuración del formulario incompleta.');
            return Promise.resolve();
        }
        var built = hooks.buildGuardarProgresoPayload(seccion);
        return MODULE.runGuardado('progreso-' + seccion, MODULE.config.urlGuardarProgreso, {
            json: built.payload,
            loaderMessage: 'Guardando...'
        }, function (r) {
            if (r.id && r.id > 0 && hooks.setCodigoSolicitud) {
                hooks.setCodigoSolicitud(r.id);
            }
            if (hooks.sincronizarUrl && r.id) {
                hooks.sincronizarUrl(r.id);
            }
            var snapshot = r.data && r.data.solicitud ? r.data.solicitud : null;
            if (snapshot && hooks.aplicarDatosPersistidos) {
                hooks.aplicarDatosPersistidos(snapshot, seccion);
            }
        });
    };

    MODULE.guardarOperaciones = function () {
        return MODULE.guardarProgreso('operaciones');
    };

    MODULE.guardarFlota = function () {
        var hooks = MODULE._hooks;
        if (!hooks || typeof hooks.buildGuardarFlotaPayload !== 'function') {
            MODULE.toast('error', 'Error', 'Configuración del formulario incompleta.');
            return Promise.resolve();
        }
        var built = hooks.buildGuardarFlotaPayload();
        if (built.error) {
            MODULE.toast('warning', 'Validación', built.error);
            return Promise.resolve();
        }
        return MODULE.runGuardado('flota', MODULE.config.urlGuardarFlota, {
            json: built.payload,
            loaderMessage: 'Guardando flota...'
        }, function (r) {
            if (hooks.refrescarFlotaEnTabla && r.data && r.data.aeronaves) {
                hooks.refrescarFlotaEnTabla(r.data.aeronaves);
            } else if (hooks.refrescarFlotaEnTabla && r.aeronaves) {
                hooks.refrescarFlotaEnTabla(r.aeronaves);
            }
            if (hooks.actualizarResumenFlota) {
                hooks.actualizarResumenFlota();
            }
        });
    };

    MODULE.guardarSolicitud = function () {
        var hooks = MODULE._hooks;
        if (!hooks || typeof hooks.ejecutarGuardarSolicitudCompleta !== 'function') {
            MODULE.toast('error', 'Error', 'Guardado completo no configurado.');
            return Promise.resolve();
        }
        return hooks.ejecutarGuardarSolicitudCompleta(MODULE);
    };

    MODULE.init = function (config, hooks) {
        MODULE.config = config || {};
        MODULE._hooks = hooks || window.AocrFormularioEmisionHooks || {};
        MODULE.hideLoader('init');
        log('[AOCR_FE] Módulo inicializado', MODULE.config);
    };

    MODULE.wireButtons = function () {
        $(document)
            .off('click.aocrFe', '#btnGuardarExplotador')
            .on('click.aocrFe', '#btnGuardarExplotador', function (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                MODULE.guardarProgreso('explotador');
            })
            .off('click.aocrFe', '#btnGuardarOperaciones')
            .on('click.aocrFe', '#btnGuardarOperaciones', function (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                MODULE.guardarOperaciones();
            })
            .off('click.aocrFe', '#btnGuardarListadoAeronaves')
            .on('click.aocrFe', '#btnGuardarListadoAeronaves', function (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                MODULE.guardarFlota();
            });
    };

    window.AocrFormularioEmision = MODULE;

    $(function () {
        var cfgEl = document.getElementById('aocr-formulario-emision-config');
        if (!cfgEl) {
            return;
        }
        var cfg = {
            urlGuardarProgreso: cfgEl.getAttribute('data-url-guardar-progreso') || '',
            urlGuardarFlota: cfgEl.getAttribute('data-url-guardar-flota') || '',
            urlFormularioCompleto: cfgEl.getAttribute('data-url-formulario-completo') || '',
            urlSeleccionarCompania: cfgEl.getAttribute('data-url-seleccionar-compania') || '',
            urlLogin: cfgEl.getAttribute('data-url-login') || ''
        };
        MODULE.init(cfg, window.AocrFormularioEmisionHooks);
        MODULE.wireButtons();
    });

})(window, window.jQuery);
