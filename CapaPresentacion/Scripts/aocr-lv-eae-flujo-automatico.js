(function () {
    if (window.__aocrLvEaeAutoFlowLoaded) {
        return;
    }

    window.__aocrLvEaeAutoFlowLoaded = true;

    function notify(type, message) {
        if (!message) {
            return;
        }

        if (window.Site && typeof window.Site.notify === 'function') {
            try {
                window.Site.notify(message, type);
                return;
            } catch (error) {
            }
        }

        if (window.toastr && typeof window.toastr[type] === 'function') {
            window.toastr[type](message);
            return;
        }

        if (window.Swal && typeof window.Swal.fire === 'function') {
            window.Swal.fire({
                icon: type === 'success' ? 'success' : (type === 'warning' ? 'warning' : 'error'),
                text: message,
                confirmButtonText: 'Aceptar'
            });
            return;
        }

        window.alert(message);
    }

    function readResponsePayload(response) {
        if (window.AOCR && typeof window.AOCR.readFetchPayload === 'function') {
            return window.AOCR.readFetchPayload(response);
        }

        var contentType = response.headers.get('content-type') || '';
        if (contentType.indexOf('application/json') >= 0) {
            return response.json();
        }

        return response.text().then(function (text) {
            return {
                rawText: text,
                message: text
            };
        });
    }

    function handleUnauthorizedPayload(payload) {
        return !!(window.AOCR
            && typeof window.AOCR.handleUnauthorizedFetch === 'function'
            && window.AOCR.handleUnauthorizedFetch(payload));
    }

    function setButtonsBusy(form, busy, activeButton) {
        if (!form) {
            return;
        }

        Array.prototype.forEach.call(form.querySelectorAll('button[type="submit"], input[type="submit"]'), function (button) {
            if (!button.getAttribute('data-original-label')) {
                button.setAttribute('data-original-label', button.tagName === 'INPUT' ? (button.value || '') : button.innerHTML);
            }

            button.disabled = !!busy;
            if (!busy) {
                if (button.tagName === 'INPUT') {
                    button.value = button.getAttribute('data-original-label') || button.value;
                } else {
                    button.innerHTML = button.getAttribute('data-original-label') || button.innerHTML;
                }
                return;
            }

            if (activeButton && button !== activeButton) {
                return;
            }

            if (button.tagName === 'INPUT') {
                button.value = 'Procesando...';
                return;
            }

            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Procesando...';
        });
    }

    function submitFormAsAjax(form, submitter) {
        var formData = new FormData(form);
        var headers = { 'X-Requested-With': 'XMLHttpRequest' };
        var antiForgeryInput = form.querySelector('input[name="__RequestVerificationToken"]')
            || document.querySelector('#__AjaxAntiForgeryForm input[name="__RequestVerificationToken"]')
            || document.querySelector('input[name="__RequestVerificationToken"]');

        if (submitter && submitter.name && !formData.has(submitter.name)) {
            formData.append(submitter.name, submitter.value || '');
        }

        if (antiForgeryInput && antiForgeryInput.value) {
            if (!formData.has('__RequestVerificationToken')) {
                formData.append('__RequestVerificationToken', antiForgeryInput.value);
            }

            headers.RequestVerificationToken = antiForgeryInput.value;
            headers.__RequestVerificationToken = antiForgeryInput.value;
        }

        return fetch(form.action, {
            method: (form.method || 'POST').toUpperCase(),
            credentials: 'same-origin',
            headers: headers,
            body: formData
        })
        .then(function (response) {
            if (response && response.redirected && /\/Account\/Login/i.test(response.url || '')) {
                var loginPayload = {
                    success: false,
                    code: 401,
                    requiresLogin: true,
                    redirectUrl: window.AOCR && typeof window.AOCR.buildLoginUrl === 'function'
                        ? window.AOCR.buildLoginUrl()
                        : response.url,
                    message: 'La sesi\u00f3n expir\u00f3 o la aplicaci\u00f3n se reinici\u00f3. Inicie sesi\u00f3n nuevamente y vuelva a finalizar la LV/EAE.'
                };

                if (handleUnauthorizedPayload(loginPayload)) {
                    return null;
                }

                throw new Error(loginPayload.message);
            }

            return readResponsePayload(response).then(function (payload) {
                if (handleUnauthorizedPayload(payload)) {
                    return null;
                }

                if (!response.ok || !payload || payload.success === false) {
                    throw new Error(payload && payload.message ? payload.message : 'No se pudo completar la operación.');
                }

                return payload;
            });
        });
    }

    function consumeAutoFlowFlags() {
        if (!window.history || typeof window.history.replaceState !== 'function' || !window.URL) {
            return;
        }

        var url = new window.URL(window.location.href);
        var changed = false;

        ['lvAutoFlow', 'autoOpenInformeTecnico', 'autoFocusInformeFirma'].forEach(function (key) {
            if (url.searchParams.has(key)) {
                url.searchParams.delete(key);
                changed = true;
            }
        });

        if (changed) {
            window.history.replaceState({}, document.title, url.pathname + (url.search || '') + (url.hash || ''));
        }
    }

    function focusSignatureSection(modalElement) {
        if (!modalElement) {
            return;
        }

        var section = modalElement.querySelector('[data-lv-signature-section="true"]');
        if (!section) {
            return;
        }

        if (typeof section.scrollIntoView === 'function') {
            section.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }

        var target = section.querySelector('input[name="CertificadoInspector"]')
            || section.querySelector('input[name="passwordCertificado"]')
            || section;

        if (!target || typeof target.focus !== 'function') {
            return;
        }

        window.setTimeout(function () {
            try {
                target.focus({ preventScroll: true });
            } catch (error) {
                target.focus();
            }
        }, 220);
    }

    function validarCompletitudCliente(form) {
        var headerFields = [
            { id: 'lvNombreEae', label: 'Nombre del EAE' },
            { id: 'lvNumeroAocFechaValidez', label: 'N AOC / Validez' },
            { id: 'lvDireccionEstadoExplotador', label: 'Dirección en el Estado del explotador' },
            { id: 'lvDireccionEstadoReconocimiento', label: 'Dirección en el Estado de reconocimiento' },
            { id: 'lvTiposAeronaves', label: 'Tipos de aeronaves' },
            { id: 'lvTipoOperacion', label: 'Tipo de operación' },
            { id: 'lvInspectorResponsable', label: 'Inspector responsable' }
        ];

        for (var i = 0; i < headerFields.length; i++) {
            var input = form.querySelector('[name="' + headerFields[i].id + '"], #' + headerFields[i].id);
            if (input && !input.value.trim()) {
                return 'Complete el campo de cabecera de la LV: ' + headerFields[i].label;
            }
        }

        var serverFields = form.querySelectorAll('.lv-server-field[data-field="cumplimiento"]');
        for (var j = 0; j < serverFields.length; j++) {
            var code = serverFields[j].getAttribute('data-item-code');
            var implField = form.querySelector('.lv-server-field[data-field="implementacion"][data-item-code="' + code + '"]');
            var commField = form.querySelector('.lv-server-field[data-field="comentarios"][data-item-code="' + code + '"]');

            var cump = (serverFields[j].value || '').trim();
            var impl = implField ? (implField.value || '').trim() : '';
            var comm = commField ? (commField.value || '').trim() : '';

            if ((!cump || !impl) && !comm) {
                return 'Debe seleccionar el estado de cumplimiento/implementación o registrar una observación para el ítem: ' + code;
            }

            if (cump.toUpperCase() === 'NO_SATISFACTORIO' && !comm) {
                return 'Ingrese una observación en Pruebas / Notas / Comentarios para el requisito con resultado No Satisfactorio: ' + code;
            }

            if (impl.toUpperCase() === 'NO_IMPLEMENTADO' && !comm) {
                return 'Ingrese una observación en Pruebas / Notas / Comentarios para el requisito No Implementado: ' + code;
            }
        }

        return null;
    }

    function bindLvEditorForm(form) {
        if (!form || form.getAttribute('data-lv-auto-flow-bound') === 'true') {
            return;
        }

        form.setAttribute('data-lv-auto-flow-bound', 'true');

        form.addEventListener('submit', function (event) {
            if (event.defaultPrevented) {
                return;
            }

            var submitter = event && event.submitter ? event.submitter : null;
            var action = submitter && submitter.getAttribute('data-lv-submit-action')
                ? submitter.getAttribute('data-lv-submit-action')
                : (form.getAttribute('data-lv-last-action') || 'guardar');

            if (action !== 'finalizar') {
                return;
            }

            var errorValidacion = validarCompletitudCliente(form);
            if (errorValidacion) {
                event.preventDefault();
                notify('warning', errorValidacion);
                return;
            }

            event.preventDefault();
            setButtonsBusy(form, true, submitter);

            submitFormAsAjax(form, submitter)
                .then(function (payload) {
                    if (!payload) {
                        return;
                    }

                    notify('success', payload.message || 'Lista de verificación operacional EAE finalizada correctamente.');
                    window.location.assign(payload.redirectUrl || window.location.href);
                })
                .catch(function (error) {
                    notify('error', error && error.message ? error.message : 'No se pudo finalizar la lista de verificación operacional EAE.');
                })
                .finally(function () {
                    setButtonsBusy(form, false, submitter);
                });
        });
    }

    function bindLvSignatureForm(form) {
        if (!form || form.getAttribute('data-lv-signature-bound') === 'true') {
            return;
        }

        form.setAttribute('data-lv-signature-bound', 'true');

        form.addEventListener('submit', function (event) {
            event.preventDefault();

            var submitter = event && event.submitter ? event.submitter : form.querySelector('button[type="submit"], input[type="submit"]');
            var certInput = form.querySelector('input[name="CertificadoInspector"]');
            var passwordInput = form.querySelector('input[name="passwordCertificado"]');

            if (certInput && (!certInput.files || certInput.files.length === 0)) {
                notify('warning', 'Seleccione un certificado digital .p12 o .pfx para firmar la LV/EAE.');
                certInput.focus();
                return;
            }

            if (passwordInput && !passwordInput.value.trim()) {
                notify('warning', 'Ingrese la contraseña del certificado digital para firmar la LV/EAE.');
                passwordInput.focus();
                return;
            }

            setButtonsBusy(form, true, submitter);

            submitFormAsAjax(form, submitter)
                .then(function (payload) {
                    if (!payload) {
                        return;
                    }

                    notify('success', payload.message || 'Lista de verificación operacional EAE firmada correctamente.');
                    window.location.assign(payload.redirectUrl || window.location.href);
                })
                .catch(function (error) {
                    notify('error', error && error.message ? error.message : 'No se pudo firmar la lista de verificación operacional EAE.');
                })
                .finally(function () {
                    setButtonsBusy(form, false, submitter);
                });
        });
    }

    function bindSignatureAutoFocus() {
        var modalElement = document.getElementById('modalListaVerificacionOperacionalEae');
        if (!modalElement) {
            return;
        }

        if ((modalElement.getAttribute('data-auto-open-signature') || 'false') !== 'true') {
            return;
        }

        var onShown = function () {
            focusSignatureSection(modalElement);
            modalElement.removeEventListener('shown.bs.modal', onShown);
        };

        modalElement.addEventListener('shown.bs.modal', onShown);
    }

    function openInformeTecnicoIfRequested() {
        var state = document.getElementById('aocrLvEaeAutoFlowState');
        if (!state || (state.getAttribute('data-auto-open-informe') || 'false') !== 'true') {
            return;
        }

        var url = state.getAttribute('data-informe-url') || '';
        var autoFocusSignature = (state.getAttribute('data-auto-focus-informe-firma') || 'false') === 'true';
        if (!url) {
            return;
        }

        window.setTimeout(function () {
            if (window.AOCRInformeTecnicoModal && typeof window.AOCRInformeTecnicoModal.open === 'function') {
                window.AOCRInformeTecnicoModal.open(url, { focusSignaturePanel: autoFocusSignature });
                return;
            }

            var trigger = document.querySelector('.aocr-btn-informe-tecnico[data-url]');
            if (trigger && typeof trigger.click === 'function') {
                trigger.click();
            }
        }, 360);
    }

    function init() {
        Array.prototype.forEach.call(
            document.querySelectorAll('#bloqueListaVerificacionOperacionalEae form[action*="GuardarListaVerificacionOperacionalEae"]'),
            bindLvEditorForm);

        Array.prototype.forEach.call(
            document.querySelectorAll('#bloqueListaVerificacionOperacionalEae form[data-lv-signature-form="true"], #bloqueListaVerificacionOperacionalEae form[action*="FirmarListaVerificacionOperacionalEae"]'),
            bindLvSignatureForm);

        bindSignatureAutoFocus();
        openInformeTecnicoIfRequested();
        consumeAutoFlowFlags();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
        return;
    }

    init();
})();
