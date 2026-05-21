(function () {
    if (window.__aocrInformeTecnicoModalLoaded) {
        return;
    }

    window.__aocrInformeTecnicoModalLoaded = true;

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

    function initTooltips(root) {
        if (!window.bootstrap || !window.bootstrap.Tooltip || !root || !root.querySelectorAll) {
            return;
        }

        var tooltips = root.querySelectorAll('[data-bs-toggle="tooltip"]');
        Array.prototype.forEach.call(tooltips, function (element) {
            window.bootstrap.Tooltip.getOrCreateInstance(element);
        });
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

    function syncGeneratedDocumentFields(form) {
        if (!form) {
            return;
        }

        var fechas = form.querySelector('#fechasInspeccionManualFieldModal');
        var estaciones = form.querySelector('#estacionesInspeccionManualFieldModal');
        var trabajos = form.querySelector('#trabajosRealizadosFieldModal');
        var operador = form.getAttribute('data-operador') || '';
        var fechasTexto = fechas && fechas.value ? fechas.value.trim() : '';
        var estacionesTexto = estaciones && estaciones.value ? estaciones.value.trim() : '';

        if (trabajos) {
            trabajos.value = 'En cumplimiento del Art. 110 del Código Aeronáutico, entre '
                + (fechasTexto || '___________')
                + ' se realizó la inspección a la(s) estación(es) de '
                + (estacionesTexto || '___________')
                + ' con el fin de verificar que la compañía '
                + (operador || '__________________')
                + ' cuenta con instalaciones, facilidades y personal técnico - operativo que brinda asistencia en tierra a sus operaciones comerciales.';
        }
    }

    function normalizeResultado(value) {
        var normalized = (value || '').trim().toUpperCase();
        if (normalized === 'NO_SATISFACTORIO') {
            return 'INSATISFACTORIO';
        }

        if (normalized === 'OBSERVACION_DOCUMENTAL') {
            return 'OBSERVADO';
        }

        if (normalized === 'NO_APLICABLE' || normalized === 'N/A') {
            return 'NO_APLICA';
        }

        return normalized;
    }

    function isResultadoInsatisfactorio(value) {
        return normalizeResultado(value) === 'INSATISFACTORIO';
    }

    function guideInsatisfactorioSelection(block) {
        if (!block) {
            return;
        }

        if (typeof block.scrollIntoView === 'function') {
            block.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
        }

        var selected = block.querySelector('input[name="tipoResultadoInsatisfactorio"]:checked');
        var firstOption = selected || block.querySelector('input[name="tipoResultadoInsatisfactorio"]');
        if (!firstOption || typeof firstOption.focus !== 'function') {
            return;
        }

        window.setTimeout(function () {
            try {
                firstOption.focus({ preventScroll: true });
            } catch (error) {
                firstOption.focus();
            }
        }, 120);
    }

    function setInsatisfactorioSectionVisible(modal, visible, options) {
        if (!modal) {
            return;
        }

        var block = modal.querySelector('#bloqueTipoInsatisfactorio');
        if (!block) {
            return;
        }

        var shouldGuideUser = !!(options && options.guideUser);

        if (visible) {
            block.style.display = 'block';
            window.requestAnimationFrame(function () {
                block.classList.add('is-visible');
                if (shouldGuideUser) {
                    guideInsatisfactorioSelection(block);
                }
            });
            return;
        }

        block.classList.remove('is-visible');
        window.setTimeout(function () {
            if (!block.classList.contains('is-visible')) {
                block.style.display = 'none';
            }
        }, 190);
    }

    function syncInsatisfactorioSection(modal, options) {
        if (!modal) {
            return;
        }

        var form = modal.querySelector('[data-aocr-informe-form="true"]');
        var block = modal.querySelector('#bloqueTipoInsatisfactorio');
        if (!form || !block) {
            return;
        }

        var resultadoSeleccionado = form.querySelector('input[name="resultado"]:checked');
        var show = isResultadoInsatisfactorio(resultadoSeleccionado ? resultadoSeleccionado.value : '');
        Array.prototype.forEach.call(block.querySelectorAll('input[name="tipoResultadoInsatisfactorio"]'), function (radio) {
            radio.disabled = !show;
            if (!show) {
                radio.checked = false;
            }
        });

        setInsatisfactorioSectionVisible(modal, show, options);
    }

    function validateInsatisfactorioSelection(form, submitMode) {
        if (!form || submitMode !== 'finalizar') {
            return true;
        }

        var resultadoSeleccionado = form.querySelector('input[name="resultado"]:checked');
        if (!isResultadoInsatisfactorio(resultadoSeleccionado ? resultadoSeleccionado.value : '')) {
            return true;
        }

        if (form.querySelector('input[name="tipoResultadoInsatisfactorio"]:checked')) {
            return true;
        }

        notify('error', 'Debe seleccionar si el resultado insatisfactorio requiere una nueva inspección o no requiere inspección.');
        return false;
    }

    function syncAttachmentFileSummary(input) {
        if (!input) {
            return;
        }

        var targetId = input.getAttribute('data-file-summary-target');
        if (!targetId) {
            return;
        }

        var target = document.getElementById(targetId);
        if (!target) {
            return;
        }

        var existingSummary = target.getAttribute('data-existing-summary') || '';
        var files = Array.prototype.slice.call(input.files || []);
        if (files.length === 0) {
            target.textContent = existingSummary;
            return;
        }

        target.textContent = (files.length > 1 ? 'Archivos seleccionados: ' : 'Archivo seleccionado: ') + files.map(function (file) {
            return file.name;
        }).join(', ');
    }

    function setButtonsBusy(modal, busy, submitMode) {
        if (!modal) {
            return;
        }

        var buttons = modal.querySelectorAll('[data-aocr-submit-mode], [data-aocr-preview-button]');
        Array.prototype.forEach.call(buttons, function (button) {
            if (!button.dataset.originalText) {
                button.dataset.originalText = button.innerHTML;
            }

            button.disabled = !!busy;
            if (!busy) {
                button.innerHTML = button.dataset.originalText;
                return;
            }

            if (button.getAttribute('data-aocr-preview-button') === 'true') {
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Previsualizando...';
                return;
            }

            if ((button.getAttribute('data-aocr-submit-mode') || '') === submitMode) {
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Procesando...';
            }
        });
    }

    function updateViewer(modal, result) {
        if (!modal || !result || !result.pdfUrl) {
            return;
        }

        var viewerRoot = modal.querySelector('#viewerInformeTecnicoModal');
        if (!viewerRoot) {
            return;
        }

        viewerRoot.setAttribute('data-pdf-url', result.pdfUrl);
        viewerRoot.setAttribute('data-download-url', result.downloadUrl || result.pdfUrl);

        var status = viewerRoot.querySelector('.aocr-pdf-status');
        if (status) {
            status.textContent = result.estado || 'VISTA PREVIA';
        }

        var help = viewerRoot.querySelector('.aocr-pdf-help');
        if (help) {
            help.textContent = result.message || 'Vista previa actualizada correctamente.';
        }

        if (window.AOCRPdfViewer && window.AOCRPdfViewer.instances) {
            var viewer = window.AOCRPdfViewer.instances[viewerRoot.id];
            if (!viewer && typeof window.AOCRPdfViewer.init === 'function') {
                window.AOCRPdfViewer.init(modal);
                viewer = window.AOCRPdfViewer.instances[viewerRoot.id];
            }

            if (viewer && typeof viewer.load === 'function') {
                viewer.load(result.pdfUrl);
            }
        }
    }

    function updateInformeStatus(modal, result) {
        if (!modal || !result || !result.estado) {
            return;
        }

        var badges = modal.querySelectorAll('.aocr-status-badge');
        Array.prototype.forEach.call(badges, function (badge) {
            if (badge.textContent && badge.textContent.indexOf('Estado Informe') >= 0) {
                badge.innerHTML = '<strong>Estado Informe</strong> ' + result.estado;
            }
        });
    }

    function buildFormData(form, submitMode) {
        syncGeneratedDocumentFields(form);

        var formData = new FormData(form);
        var resultadoSeleccionado = form.querySelector('input[name="resultado"]:checked');
        formData.set('modalRequest', 'true');

        if (submitMode === 'finalizar') {
            formData.set('finalizar', 'true');
        } else {
            formData.delete('finalizar');
        }

        if (!isResultadoInsatisfactorio(resultadoSeleccionado ? resultadoSeleccionado.value : '')) {
            formData.delete('tipoResultadoInsatisfactorio');
        }

        return formData;
    }

    function requestPreview(modal) {
        var form = modal.querySelector('[data-aocr-informe-form="true"]');
        if (!form) {
            return;
        }

        var previewUrl = form.getAttribute('data-preview-url');
        if (!previewUrl) {
            notify('error', 'No se encontró la ruta de previsualización del Informe Técnico.');
            return;
        }

        setButtonsBusy(modal, true, 'preview');

        fetch(previewUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: buildFormData(form, 'preview')
        })
        .then(function (response) {
            return readResponsePayload(response).then(function (payload) {
                if (handleUnauthorizedPayload(payload)) {
                    return null;
                }

                if (!response.ok || !payload || payload.success === false) {
                    throw new Error(payload && payload.message ? payload.message : 'No se pudo generar la vista previa del informe técnico.');
                }

                return payload;
            });
        })
        .then(function (payload) {
            if (!payload) {
                return;
            }

            updateViewer(modal, payload);
            updateInformeStatus(modal, payload);
            notify('success', payload.message || 'Vista previa generada correctamente.');
        })
        .catch(function (error) {
            notify('error', error && error.message ? error.message : 'No se pudo generar la vista previa del informe técnico.');
        })
        .finally(function () {
            setButtonsBusy(modal, false, 'preview');
        });
    }

    function submitInforme(modal, submitMode) {
        var form = modal.querySelector('[data-aocr-informe-form="true"]');
        if (!form) {
            return;
        }

        if (!validateInsatisfactorioSelection(form, submitMode)) {
            return;
        }

        if (submitMode === 'finalizar') {
            var confirmar = window.confirm('Una vez finalizado el Informe Técnico, no podrá editarse salvo autorización. ¿Desea continuar?');
            if (!confirmar) {
                return;
            }
        }

        setButtonsBusy(modal, true, submitMode);

        fetch(form.getAttribute('action'), {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: buildFormData(form, submitMode)
        })
        .then(function (response) {
            return readResponsePayload(response).then(function (payload) {
                if (handleUnauthorizedPayload(payload)) {
                    return null;
                }

                if (!response.ok || !payload || payload.success === false) {
                    throw new Error(payload && payload.message ? payload.message : 'No se pudo procesar el Informe Técnico.');
                }

                return payload;
            });
        })
        .then(function (payload) {
            if (!payload) {
                return;
            }

            updateInformeStatus(modal, payload);
            if (payload.pdfUrl) {
                updateViewer(modal, payload);
            }

            notify('success', payload.message || 'Operación completada correctamente.');

            if (submitMode === 'finalizar') {
                window.setTimeout(function () {
                    window.location.href = payload.redirectUrl || window.location.href;
                }, 600);
            }
        })
        .catch(function (error) {
            notify('error', error && error.message ? error.message : 'No se pudo procesar el Informe Técnico.');
        })
        .finally(function () {
            setButtonsBusy(modal, false, submitMode);
        });
    }

    function initInformeModal(modal) {
        if (!modal || modal.getAttribute('data-aocr-modal-initialized') === 'true') {
            return;
        }

        modal.setAttribute('data-aocr-modal-initialized', 'true');
        initTooltips(modal);

        Array.prototype.forEach.call(modal.querySelectorAll('input[type="file"]'), function (input) {
            input.addEventListener('change', function () {
                syncAttachmentFileSummary(input);
            });
        });

        Array.prototype.forEach.call(modal.querySelectorAll('input[name="resultado"]'), function (radio) {
            radio.addEventListener('change', function () {
                syncInsatisfactorioSection(modal, { guideUser: isResultadoInsatisfactorio(radio.value) });
            });
        });

        var previewButton = modal.querySelector('[data-aocr-preview-button="true"]');
        if (previewButton) {
            previewButton.addEventListener('click', function () {
                requestPreview(modal);
            });
        }

        Array.prototype.forEach.call(modal.querySelectorAll('[data-aocr-submit-mode]'), function (button) {
            button.addEventListener('click', function () {
                submitInforme(modal, button.getAttribute('data-aocr-submit-mode'));
            });
        });

        if (window.AOCRPdfViewer && typeof window.AOCRPdfViewer.init === 'function') {
            window.AOCRPdfViewer.init(modal);
        }

        syncInsatisfactorioSection(modal, { guideUser: false });
    }

    function ensureHost() {
        var host = document.getElementById('modalInformeTecnicoHost');
        if (host) {
            return host;
        }

        host = document.createElement('div');
        host.id = 'modalInformeTecnicoHost';
        document.body.appendChild(host);
        return host;
    }

    function openInformeTecnicoModal(url) {
        if (!url) {
            notify('error', 'No se encontró la ruta del Informe Técnico.');
            return Promise.resolve(false);
        }

        var host = ensureHost();
        host.innerHTML = '<div class="aocr-loading-modal p-4 text-center text-muted">Cargando Informe Técnico...</div>';

        return fetch(url, {
            method: 'GET',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
        .then(function (response) {
            return readResponsePayload(response).then(function (payload) {
                if (handleUnauthorizedPayload(payload)) {
                    return null;
                }

                if (typeof payload === 'string') {
                    if (!response.ok) {
                        throw new Error(payload || 'No se pudo cargar el Informe Técnico.');
                    }

                    return payload;
                }

                if (!response.ok || !payload || payload.success === false) {
                    throw new Error(payload && payload.message ? payload.message : 'No se pudo cargar el Informe Técnico.');
                }

                return payload && payload.rawText ? payload.rawText : payload;
            });
        })
        .then(function (html) {
            if (!html) {
                return false;
            }

            if (typeof html !== 'string') {
                throw new Error(html && html.message ? html.message : 'No se pudo cargar el Informe Técnico.');
            }

            host.innerHTML = html;
            var modal = host.querySelector('#modalInformeTecnico');
            if (!modal) {
                throw new Error('No se pudo construir la ventana del Informe Técnico.');
            }

            initInformeModal(modal);

            if (window.bootstrap && window.bootstrap.Modal) {
                window.bootstrap.Modal.getOrCreateInstance(modal).show();
            }

            return true;
        })
        .catch(function (error) {
            host.innerHTML = '';
            notify('error', error && error.message ? error.message : 'No se pudo cargar el Informe Técnico.');
            return false;
        });
    }

    window.AOCRInformeTecnicoModal = {
        open: openInformeTecnicoModal
    };

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('.aocr-btn-informe-tecnico');
        if (!trigger) {
            return;
        }

        event.preventDefault();

        if (trigger.disabled) {
            return;
        }

        var url = trigger.getAttribute('data-url') || '';
        openInformeTecnicoModal(url);
    });

    document.addEventListener('hidden.bs.modal', function (event) {
        if (!event.target || event.target.id !== 'modalInformeTecnico') {
            return;
        }

        var host = document.getElementById('modalInformeTecnicoHost');
        if (host) {
            host.innerHTML = '';
        }
    });

    initTooltips(document);
})();