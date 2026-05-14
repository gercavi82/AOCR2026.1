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

        if (submitter && submitter.name && !formData.has(submitter.name)) {
            formData.append(submitter.name, submitter.value || '');
        }

        return fetch(form.action, {
            method: (form.method || 'POST').toUpperCase(),
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: formData
        })
        .then(function (response) {
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

        ['lvAutoFlow', 'autoOpenInformeTecnico'].forEach(function (key) {
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
        if (!url) {
            return;
        }

        window.setTimeout(function () {
            if (window.AOCRInformeTecnicoModal && typeof window.AOCRInformeTecnicoModal.open === 'function') {
                window.AOCRInformeTecnicoModal.open(url);
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