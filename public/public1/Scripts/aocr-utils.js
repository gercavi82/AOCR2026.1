(function(window){
    'use strict';
    window.AOCR = window.AOCR || {};

    function notify(message, type){
        if (!message) {
            return;
        }

        if (window.Site && typeof window.Site.notify === 'function') {
            try {
                window.Site.notify(message, type || 'error');
                return;
            } catch (error) {
            }
        }

        if (window.Swal && typeof window.Swal.fire === 'function') {
            window.Swal.fire({
                icon: type === 'success' ? 'success' : (type === 'info' ? 'info' : 'error'),
                text: message,
                confirmButtonText: 'Aceptar'
            });
            return;
        }

        window.alert(message);
    }

    window.AOCR.notify = notify;

    function toPlainError(reason, fallbackMessage) {
        if (reason instanceof Error) {
            return reason;
        }

        var message = fallbackMessage || 'Error inesperado al procesar la solicitud.';

        if (typeof reason === 'string' && reason.trim()) {
            message = reason.trim();
        } else if (reason && typeof reason === 'object') {
            message = reason.message || reason.mensaje || reason.error || reason.statusText || message;
        }

        var error = new Error(message);
        if (reason && typeof reason === 'object') {
            try {
                Object.keys(reason).forEach(function (key) {
                    if (!(key in error)) {
                        error[key] = reason[key];
                    }
                });
            } catch (ignore) {
            }
        }

        return error;
    }

    function logPromiseDiagnostic(tag, reason, extra) {
        var details = {
            reason: reason,
            type: typeof reason,
            message: reason && reason.message ? reason.message : (typeof reason === 'string' ? reason : ''),
            status: reason && reason.status ? reason.status : null,
            responseText: reason && reason.responseText ? String(reason.responseText).substring(0, 500) : '',
            stack: reason && reason.stack ? reason.stack : '',
            keys: [],
            url: window.location && window.location.href ? window.location.href : ''
        };

        if (extra) {
            Object.keys(extra).forEach(function (key) {
                details[key] = extra[key];
            });
        }

        try {
            details.keys = reason && typeof reason === 'object' ? Object.keys(reason).slice(0, 20) : [];
        } catch (ignore) {
        }

        window.__AOCR_LAST_UNHANDLED_REJECTION__ = details;

        if (window.console && typeof window.console.error === 'function') {
            window.console.error(tag, details);
        }
    }

    function createJsonResponse(payload, statusCode, originalResponse){
        var headers = new Headers();
        headers.set('content-type', 'application/json; charset=utf-8');

        if (originalResponse && originalResponse.headers) {
            var redirected = originalResponse.headers.get('x-redirected-by');
            if (redirected) {
                headers.set('x-redirected-by', redirected);
            }
        }

        return new Response(JSON.stringify(payload), {
            status: statusCode,
            statusText: statusCode === 403 ? 'Forbidden' : 'Unauthorized',
            headers: headers
        });
    }

    // Escapa texto para incluirlo en HTML (contenido) de forma segura
    window.AOCR.escapeHtml = function(text){
        if (text === null || text === undefined) return '';
        return String(text).replace(/[&<>"']/g, function (m) { return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]; });
    };

    // Escapa texto para atributos (añade comillas escapadas)
    window.AOCR.escapeAttr = function(text){
        return window.AOCR.escapeHtml(text).replace(/"/g, '&quot;');
    };

    window.AOCR.getAppBaseUrl = function(){
        var baseUrl = window.AOCR_CONFIG && window.AOCR_CONFIG.apiBaseUrl
            ? String(window.AOCR_CONFIG.apiBaseUrl)
            : '/';

        if (!baseUrl) {
            baseUrl = '/';
        }

        return /\/$/.test(baseUrl) ? baseUrl : baseUrl + '/';
    };

    window.AOCR.buildLoginUrl = function(returnUrl){
        var loginUrl = window.AOCR.getAppBaseUrl() + 'Account/Login';
        var targetUrl = returnUrl;

        if (!targetUrl && window.location) {
            targetUrl = window.location.pathname + window.location.search + window.location.hash;
        }

        if (!targetUrl) {
            return loginUrl;
        }

        return loginUrl + '?ReturnUrl=' + encodeURIComponent(targetUrl);
    };

    window.AOCR.isLoginMarkup = function(text){
        if (text === null || text === undefined) {
            return false;
        }

        var normalized = String(text).toLowerCase();
        var isHtmlDocument = normalized.indexOf('<!doctype html') >= 0 || normalized.indexOf('<html') >= 0;
        if (!isHtmlDocument || normalized.indexOf('<form') < 0) {
            return false;
        }

        var loginMarkers = 0;

        if (normalized.indexOf('id="loginform"') >= 0 || normalized.indexOf("id='loginform'") >= 0) {
            loginMarkers++;
        }

        if (normalized.indexOf('name="usuario"') >= 0 || normalized.indexOf("name='usuario'") >= 0) {
            loginMarkers++;
        }

        if (
            normalized.indexOf('name="contrasena"') >= 0 ||
            normalized.indexOf("name='contrasena'") >= 0 ||
            normalized.indexOf('id="clave"') >= 0 ||
            normalized.indexOf("id='clave'") >= 0
        ) {
            loginMarkers++;
        }

        if (
            normalized.indexOf('login-card') >= 0 ||
            normalized.indexOf('acceso sistema aocr') >= 0 ||
            normalized.indexOf('ingreso aocr') >= 0
        ) {
            loginMarkers++;
        }

        return loginMarkers >= 2;
    };

    window.AOCR.shouldSkipGlobalAuthRedirect = function(settings){
        return !!(settings && settings.aocrSkipAuthRedirect === true);
    };

    window.AOCR.readFetchPayload = function(response){
        var contentType = response && response.headers ? (response.headers.get('content-type') || '') : '';
        if (contentType.indexOf('application/json') >= 0) {
            return response.json();
        }

        return response.text().then(function (text) {
            if (window.AOCR.isLoginMarkup(text)) {
                return {
                    success: false,
                    code: 401,
                    requiresLogin: true,
                    redirectUrl: window.AOCR.buildLoginUrl(),
                    message: 'La sesión expiró. Inicie sesión nuevamente.'
                };
            }

            return {
                success: response ? response.ok : false,
                message: text,
                rawText: text
            };
        });
    };

    window.AOCR.handleUnauthorizedFetch = function(payload){
        if (!payload) {
            return false;
        }

        var code = parseInt(payload.code, 10);
        if (payload.requiresLogin || code === 401) {
            window.location.href = payload.redirectUrl || window.AOCR.buildLoginUrl();
            return true;
        }

        return false;
    };

    window.AOCR.notifyHttpAuthError = function(payload, fallbackStatusCode){
        if (!payload) {
            return false;
        }

        var code = parseInt(payload.code, 10);
        if (!code && fallbackStatusCode) {
            code = fallbackStatusCode;
        }

        if (payload.requiresLogin || code === 401) {
            window.location.href = payload.redirectUrl || window.AOCR.buildLoginUrl();
            return true;
        }

        if (code === 403) {
            notify(payload.message || 'No tiene permisos para realizar esta acción.', 'error');
            return true;
        }

        return false;
    };

    window.AOCR.toError = toPlainError;

    window.AOCR.fetchJson = function(url, options) {
        if (!url) {
            var emptyUrlError = new Error('No se recibio una URL valida para la solicitud AJAX.');
            if (window.console && typeof window.console.error === 'function') {
                window.console.error('[AOCR][FETCH_JSON_ERROR]', { url: url, error: emptyUrlError });
            }
            notify(emptyUrlError.message, 'error');
            return Promise.resolve(null);
        }

        options = options || {};
        var headers = {};

        if (options.headers) {
            Object.keys(options.headers).forEach(function (key) {
                headers[key] = options.headers[key];
            });
        }

        headers['X-Requested-With'] = headers['X-Requested-With'] || 'XMLHttpRequest';

        return fetch(url, Object.assign({}, options, {
            credentials: options.credentials || 'same-origin',
            headers: headers
        })).then(function(response) {
            var contentType = response.headers ? (response.headers.get('content-type') || '') : '';

            if (response.status === 401) {
                throw new Error('Su sesion expiro. Inicie sesion nuevamente.');
            }

            if (response.status === 403) {
                throw new Error('No tiene permisos para ejecutar esta accion con el rol activo actual.');
            }

            if (!response.ok) {
                return response.text().then(function(text) {
                    throw new Error('Error HTTP ' + response.status + ' en ' + url + ': ' + String(text || '').substring(0, 500));
                });
            }

            if (contentType.indexOf('application/json') < 0) {
                return response.text().then(function(text) {
                    if (window.AOCR.isLoginMarkup(text)) {
                        throw new Error('El servidor devolvio la pantalla de login en una solicitud AJAX.');
                    }

                    throw new Error('El servidor no devolvio JSON. Respuesta recibida: ' + String(text || '').substring(0, 300));
                });
            }

            return response.json().then(function(data) {
                if (data && data.success === false) {
                    throw new Error(data.message || data.mensaje || 'La operacion no fue exitosa.');
                }

                return data;
            });
        }).catch(function(reason) {
            var error = toPlainError(reason);
            if (window.console && typeof window.console.error === 'function') {
                window.console.error('[AOCR][FETCH_JSON_ERROR]', {
                    url: url,
                    error: error,
                    message: error.message,
                    stack: error.stack
                });
            }
            notify(error.message || 'Error inesperado al procesar la solicitud.', 'error');
            return null;
        });
    };

    window.AOCR.installGlobalDiagnostics = function(){
        if (!window.__aocrGlobalDiagnosticsInstalled) {
            window.addEventListener('unhandledrejection', function(event) {
                logPromiseDiagnostic('[AOCR][UNHANDLED_PROMISE]', event.reason);
            });

            window.addEventListener('error', function(event) {
                if (window.console && typeof window.console.error === 'function') {
                    window.console.error('[AOCR][JS_ERROR]', {
                        message: event.message,
                        source: event.filename,
                        line: event.lineno,
                        column: event.colno,
                        error: event.error,
                        url: window.location && window.location.href ? window.location.href : ''
                    });
                }
            });

            window.__aocrGlobalDiagnosticsInstalled = true;
        }
    };

    window.AOCR.installGlobalHttpHandlers = function(){
        if (window.fetch && !window.__aocrFetchWrapped) {
            var originalFetch = window.fetch;
            window.fetch = function () {
                var requestUrl = arguments && arguments.length ? arguments[0] : '';
                return originalFetch.apply(this, arguments).then(function (response) {
                    var contentType = response && response.headers
                        ? (response.headers.get('content-type') || '')
                        : '';

                    if (response && (response.status === 401 || response.status === 403)) {
                        return window.AOCR.readFetchPayload(response.clone())
                            .then(function (payload) {
                                window.AOCR.notifyHttpAuthError(payload, response.status);
                                return response;
                            })
                            .catch(function () {
                                window.AOCR.notifyHttpAuthError({ code: response.status }, response.status);
                                return response;
                            });
                    }

                    if (response && response.status === 200 && contentType.indexOf('text/html') >= 0) {
                        return response.clone().text().then(function (text) {
                            if (!window.AOCR.isLoginMarkup(text)) {
                                return response;
                            }

                            var payload = {
                                success: false,
                                code: 401,
                                requiresLogin: true,
                                redirectUrl: window.AOCR.buildLoginUrl(),
                                message: 'La sesión expiró. Inicie sesión nuevamente.'
                            };

                            window.AOCR.notifyHttpAuthError(payload, 401);
                            return createJsonResponse(payload, 401, response);
                        }).catch(function () {
                            return response;
                        });
                    }

                    return response;
                }).catch(function (reason) {
                    var error = toPlainError(reason, 'No se pudo completar la solicitud fetch.');
                    logPromiseDiagnostic('[AOCR][FETCH_ERROR]', error, { requestUrl: requestUrl });
                    throw error;
                });
            };

            window.__aocrFetchWrapped = true;
        }

        if (window.jQuery && !window.__aocrJQueryAuthHandlersInstalled) {
            window.jQuery(document)
                .off('ajaxError.aocrAuth')
                .on('ajaxError.aocrAuth', function (event, xhr, settings) {
                    if (window.AOCR.shouldSkipGlobalAuthRedirect(settings)) {
                        return;
                    }

                    if (!xhr) {
                        return;
                    }

                    if (xhr.status === 401) {
                        window.location.href = window.AOCR.buildLoginUrl();
                        return;
                    }

                    if (xhr.status === 403) {
                        notify('No tiene permisos para realizar esta acción.', 'error');
                    }
                })
                .off('ajaxComplete.aocrAuth')
                .on('ajaxComplete.aocrAuth', function (event, xhr, settings) {
                    if (window.AOCR.shouldSkipGlobalAuthRedirect(settings)) {
                        return;
                    }

                    if (!xhr || xhr.status !== 200 || typeof xhr.responseText !== 'string') {
                        return;
                    }

                    if (window.AOCR.isLoginMarkup(xhr.responseText)) {
                        window.location.href = window.AOCR.buildLoginUrl();
                    }
                });

            window.__aocrJQueryAuthHandlersInstalled = true;
        }
    };

    // Crea una opción de select de forma segura
    window.AOCR.createOption = function(value, text){
        var $opt = $('<option>').val(value).text(text);
        return $opt;
    };

    window.AOCR.confirmarGeneracionSolicitudInspeccion = function (onConfirm) {
        var titulo = 'Confirmar generación de Solicitud de Inspecciones';
        var html = '<div style="text-align:left;">' +
            '<p><strong>Está por generar el PDF definitivo de la Solicitud de Inspecciones.</strong></p>' +
            '<p>Si continúa, la solicitud quedará <strong>cerrada</strong> y ya no podrá agregar nuevas acciones, conceptos o inspecciones adicionales a esta orden.</p>' +
            '<p>Si aún necesita agregar más acciones, seleccione <strong>“No, seguir agregando acciones”</strong> para regresar y completar la orden antes de generar el documento.</p>' +
            '<p><strong>¿Desea generar el PDF definitivo con la información actual?</strong></p>' +
            '</div>';

        function ejecutarSiConfirma() {
            if (typeof onConfirm === 'function') {
                onConfirm();
            }
        }

        try {
            if (window.Swal && typeof window.Swal.fire === 'function') {
                return window.Swal.fire({
                    title: titulo,
                    html: html,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Sí, generar PDF definitivo',
                    cancelButtonText: 'No, seguir agregando acciones',
                    confirmButtonColor: '#dc3545',
                    cancelButtonColor: '#0d6efd',
                    reverseButtons: true,
                    focusCancel: true,
                    customClass: {
                        popup: 'aocr-swal-advertencia-inspeccion',
                        htmlContainer: 'aocr-swal-advertencia-inspeccion__content'
                    }
                }).then(function (result) {
                    if (result.isConfirmed) {
                        ejecutarSiConfirma();
                    }
                });
            }
        } catch (e) {
            // Continuar con confirm nativo.
        }

        var mensajePlano = 'Está por generar el PDF definitivo de la Solicitud de Inspecciones.\n\n' +
            'Si continúa, la solicitud quedará cerrada y ya no podrá agregar nuevas acciones, conceptos o inspecciones adicionales a esta orden.\n\n' +
            '¿Desea generar el PDF definitivo con la información actual?';

        if (window.confirm(mensajePlano)) {
            ejecutarSiConfirma();
        }
    };

    window.AOCR.confirmarRechazoGeneracionSolicitudInspeccion = function (onConfirm) {
        var titulo = 'Rechazar generación y seguir agregando acciones';
        var html = '<div style="text-align:left;">' +
            '<p>Está por <strong>rechazar la generación actual</strong> de la Solicitud de Inspecciones.</p>' +
            '<p>El PDF generado quedará invalidado y podrá volver a agregar acciones, conceptos o inspecciones a la orden.</p>' +
            '<p><strong>Esta opción no está disponible si ya cargó la solicitud firmada.</strong></p>' +
            '<p>¿Desea continuar?</p>' +
            '</div>';

        function ejecutarSiConfirma() {
            if (typeof onConfirm === 'function') {
                onConfirm();
            }
        }

        try {
            if (window.Swal && typeof window.Swal.fire === 'function') {
                return window.Swal.fire({
                    title: titulo,
                    html: html,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Sí, rechazar generación',
                    cancelButtonText: 'No, mantener PDF generado',
                    confirmButtonColor: '#dc3545',
                    cancelButtonColor: '#6c757d',
                    reverseButtons: true,
                    focusCancel: true
                }).then(function (result) {
                    if (result.isConfirmed) {
                        ejecutarSiConfirma();
                    }
                });
            }
        } catch (e) {
            // Continuar con confirm nativo.
        }

        if (window.confirm('¿Desea rechazar la generación actual y seguir agregando acciones a la orden?')) {
            ejecutarSiConfirma();
        }
    };

    window.AOCR.informarGeneracionSolicitudInspeccion = window.AOCR.confirmarGeneracionSolicitudInspeccion;

    window.AOCR.installAocrModalPortal = function () {
        if (window.__aocrModalPortalInstalled) {
            return;
        }
        window.__aocrModalPortalInstalled = true;

        var modalZIndex = '10500';
        var backdropZIndex = '10490';

        function moverModalesAlBody() {
            var modales = document.querySelectorAll('.modal');
            for (var i = 0; i < modales.length; i++) {
                if (modales[i].parentElement !== document.body) {
                    document.body.appendChild(modales[i]);
                }
            }
        }

        function aplicarStackingModal() {
            var backdrops = document.querySelectorAll('.modal-backdrop');
            for (var i = 0; i < backdrops.length; i++) {
                backdrops[i].style.zIndex = backdropZIndex;
            }

            var modalesVisibles = document.querySelectorAll('.modal.show');
            for (var j = 0; j < modalesVisibles.length; j++) {
                modalesVisibles[j].style.zIndex = modalZIndex;
            }
        }

        document.addEventListener('show.bs.modal', function (event) {
            var modal = event.target;
            if (!modal || !modal.classList || !modal.classList.contains('modal')) {
                return;
            }

            if (modal.parentElement !== document.body) {
                document.body.appendChild(modal);
            }

            modal.style.zIndex = modalZIndex;
        }, true);

        document.addEventListener('shown.bs.modal', aplicarStackingModal, true);

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', moverModalesAlBody);
        } else {
            moverModalesAlBody();
        }
    };

    window.AOCR.abrirModalAocr = function (selectorOrElement) {
        var modal = typeof selectorOrElement === 'string'
            ? document.querySelector(selectorOrElement)
            : selectorOrElement;

        if (!modal) {
            return null;
        }

        if (modal.parentElement !== document.body) {
            document.body.appendChild(modal);
        }

        modal.style.zIndex = '10500';

        if (window.bootstrap && bootstrap.Modal) {
            return bootstrap.Modal.getOrCreateInstance(modal).show();
        }

        return null;
    };

    // -------------------------------------------------------------------------
    // Loader / bloqueo de formulario AOCR (idempotente, sin SweetAlert2)
    // -------------------------------------------------------------------------
    window.AOCR._loader = window.AOCR._loader || { depth: 0, formLockDepth: 0, element: null };

    window.AOCR.injectLoaderStyles = function () {
        if (document.getElementById('aocr-loader-styles')) {
            return;
        }
        var style = document.createElement('style');
        style.id = 'aocr-loader-styles';
        style.textContent = [
            '#aocr-global-loader{display:none;position:fixed;inset:0;z-index:19990;',
            'align-items:center;justify-content:center;background:rgba(248,250,252,.72);',
            'backdrop-filter:blur(1px);pointer-events:none;}',
            '#aocr-global-loader.is-visible{display:flex;pointer-events:auto;}',
            '.aocr-global-loader-panel{text-align:center;padding:1.25rem 1.5rem;border-radius:12px;',
            'background:#fff;box-shadow:0 10px 30px rgba(15,23,42,.12);min-width:220px;}',
            '.aocr-global-loader-spinner{width:42px;height:42px;margin:0 auto .75rem;',
            'border:3px solid #d1d5db;border-top-color:#0c7c86;border-radius:50%;',
            'animation:aocr-spin .8s linear infinite;}',
            '@keyframes aocr-spin{to{transform:rotate(360deg);}}',
            '.aocr-global-loader-text{margin:0;color:#374151;font-weight:600;font-size:.95rem;}',
            '.aocr-formulario-bloqueado{pointer-events:auto;}'
        ].join('');
        document.head.appendChild(style);
    };

    window.AOCR.ensureLoaderElement = function () {
        this.injectLoaderStyles();
        if (this._loader.element) {
            return this._loader.element;
        }
        var el = document.getElementById('aocr-global-loader');
        if (!el) {
            el = document.createElement('div');
            el.id = 'aocr-global-loader';
            el.setAttribute('aria-hidden', 'true');
            el.setAttribute('role', 'status');
            el.innerHTML = '<div class="aocr-global-loader-panel">' +
                '<div class="aocr-global-loader-spinner" aria-hidden="true"></div>' +
                '<p class="aocr-global-loader-text">Procesando...</p></div>';
            document.body.appendChild(el);
        }
        this._loader.element = el;
        return el;
    };

    window.AOCR.showAocrLoader = function (message) {
        var el = this.ensureLoaderElement();
        var textEl = el.querySelector('.aocr-global-loader-text');
        if (textEl) {
            textEl.textContent = message || 'Procesando...';
        }
        this._loader.depth = (this._loader.depth || 0) + 1;
        el.classList.add('is-visible');
        el.setAttribute('aria-hidden', 'false');
        if (window.console && console.log) {
            console.log('[AOCR_LOADER] show depth=' + this._loader.depth + ' msg=' + (message || ''));
        }
    };

  /**
   * Oculta el loader AOCR. Idempotente: { force: true } resetea el contador.
   * @param {string} origen
   * @param {{ force?: boolean }} opciones
   */
    window.AOCR.hideAocrLoader = function (origen, opciones) {
        opciones = opciones || {};
        if (opciones.force) {
            this._loader.depth = 0;
        } else if (this._loader.depth > 0) {
            this._loader.depth--;
        }
        var el = this._loader.element || document.getElementById('aocr-global-loader');
        if (el && this._loader.depth <= 0) {
            this._loader.depth = 0;
            el.classList.remove('is-visible');
            el.setAttribute('aria-hidden', 'true');
        }
        this.liberarOverlayUi({ forzarCierre: true });
        if (window.console && console.log) {
            console.log('[AOCR_LOADER] hide origen=' + (origen || '') + ' depth=' + this._loader.depth);
        }
    };

    window.AOCR.lockAocrForm = function (selector) {
        this._loader.formLockDepth = (this._loader.formLockDepth || 0) + 1;
        var root = selector ? (typeof selector === 'string' ? document.querySelector(selector) : selector) : null;
        var btnSel = '#btnGuardarExplotador,#btnGuardarOperaciones,#btnGuardarListadoAeronaves,#btnGuardarFormulario';
        if (window.jQuery) {
            window.jQuery(btnSel).prop('disabled', true);
            if (root) {
                window.jQuery(root).addClass('aocr-formulario-bloqueado');
            }
        }
    };

    window.AOCR.unlockAocrForm = function (selector) {
        if (this._loader.formLockDepth > 0) {
            this._loader.formLockDepth--;
        }
        if (this._loader.formLockDepth > 0) {
            return;
        }
        this._loader.formLockDepth = 0;
        var root = selector ? (typeof selector === 'string' ? document.querySelector(selector) : selector) : null;
        var btnSel = '#btnGuardarExplotador,#btnGuardarOperaciones,#btnGuardarListadoAeronaves,#btnGuardarFormulario';
        if (window.jQuery) {
            window.jQuery(btnSel).prop('disabled', false);
            if (root) {
                window.jQuery(root).removeClass('aocr-formulario-bloqueado');
            }
        }
    };

    window.AOCR.resetAocrUiGuardado = function (origen) {
        this.hideAocrLoader(origen || 'reset', { force: true });
        this.unlockAocrForm('#formularioEmisionAOCR');
        this.liberarOverlayUi({ forzarCierre: true });
    };

    /**
     * AJAX JSON seguro para formulario AOCR: siempre cierra loader en complete.
     */
    window.AOCR.ajaxJsonSeguro = function (options) {
        options = options || {};
        var $ = window.jQuery;
        var origen = options.origen || 'ajax';
        var formSelector = options.formSelector || '#formularioEmisionAOCR';

        if (!$) {
            return Promise.reject(new Error('jQuery no está disponible.'));
        }

        console.log('[AOCR_GUARDAR][' + origen + '] inicio guardado');
        if (options.payload !== undefined) {
            console.log('[AOCR_GUARDAR][' + origen + '] payload:', options.payload);
        }

        window.AOCR.lockAocrForm(formSelector);
        window.AOCR.showAocrLoader(options.loaderMessage || 'Guardando...');

        var ajaxOpts = {
            url: options.url,
            type: options.type || 'POST',
            dataType: 'json',
            timeout: options.timeout || 30000,
            aocrSkipAuthRedirect: true,
            headers: Object.assign({ 'X-Requested-With': 'XMLHttpRequest' }, options.headers || {}),
            data: options.data,
            processData: options.processData,
            contentType: options.contentType
        };

        if (options.beforeSend) {
            ajaxOpts.beforeSend = options.beforeSend;
        }

        return new Promise(function (resolve, reject) {
            $.ajax(ajaxOpts)
                .done(function (data, textStatus, xhr) {
                    console.log('[AOCR_GUARDAR][' + origen + '] respuesta backend:', data);
                    var raw = xhr && xhr.responseText ? xhr.responseText : '';
                    if (typeof data === 'string' || (raw && window.AOCR.isLoginMarkup(raw))) {
                        reject({ tipo: 'html', xhr: xhr, data: data });
                        return;
                    }
                    if (raw && raw.trim().charAt(0) === '<' && raw.toLowerCase().indexOf('<html') >= 0) {
                        reject({ tipo: 'html', xhr: xhr, data: data });
                        return;
                    }
                    console.log('[AOCR_GUARDAR][' + origen + '] success ejecutado');
                    resolve({ data: data, xhr: xhr });
                })
                .fail(function (xhr, status, error) {
                    console.error('[AOCR_GUARDAR][' + origen + '] error ejecutado:', status, error);
                    reject({ tipo: 'error', xhr: xhr, status: status, error: error });
                })
                .always(function () {
                    console.log('[AOCR_GUARDAR][' + origen + '] complete/finally ejecutado');
                    window.AOCR.hideAocrLoader(origen + '-complete', { force: true });
                    window.AOCR.unlockAocrForm(formSelector);
                    console.log('[AOCR_GUARDAR][' + origen + '] overlay ocultado; formulario desbloqueado');
                });
        });
    };

    /**
     * Limpia overlays huérfanos de SweetAlert2/Bootstrap que dejan la UI bloqueada.
     * @param {{ forzarCierre?: boolean }} opciones
     */
    window.AOCR.liberarOverlayUi = function (opciones) {
        opciones = opciones || {};
        var forzarCierre = opciones.forzarCierre === true;
        var body = document.body;

        if (forzarCierre && typeof Swal !== 'undefined') {
            try { Swal.close(); } catch (e) { }
        }

        if (!body) {
            return;
        }

        body.classList.remove('swal2-shown', 'swal2-height-auto', 'modal-open');
        body.style.removeProperty('overflow');
        body.style.removeProperty('padding-right');

        var containers = document.querySelectorAll('body > .swal2-container');
        for (var i = 0; i < containers.length; i++) {
            if (forzarCierre || containers.length > 1) {
                if (containers[i].parentNode) {
                    containers[i].parentNode.removeChild(containers[i]);
                }
            }
        }

        if (forzarCierre) {
            containers = document.querySelectorAll('body > .swal2-container');
            for (var j = 0; j < containers.length; j++) {
                if (containers[j].parentNode) {
                    containers[j].parentNode.removeChild(containers[j]);
                }
            }
        }
    };

    window.AOCR.installAocrModalPortal();
    window.AOCR.installGlobalDiagnostics();
    window.AOCR.installGlobalHttpHandlers();

    function limpiarUiAlCargarAocr() {
        window.AOCR.resetAocrUiGuardado('global-init');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', limpiarUiAlCargarAocr);
    } else {
        limpiarUiAlCargarAocr();
    }

})(window);
