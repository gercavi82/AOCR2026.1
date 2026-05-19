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

    window.AOCR.installGlobalHttpHandlers = function(){
        if (window.fetch && !window.__aocrFetchWrapped) {
            var originalFetch = window.fetch;
            window.fetch = function () {
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

    window.AOCR.installGlobalHttpHandlers();

})(window);
