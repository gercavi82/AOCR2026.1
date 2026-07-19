(function () {
    if (window.__aocrInformeTecnicoModalLoaded) {
        return;
    }

    window.__aocrInformeTecnicoModalLoaded = true;
    var attachmentInputState = typeof WeakMap !== 'undefined' ? new WeakMap() : null;

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

    function logPdfPreview(message, data) {
        if (!window.console || typeof window.console.log !== 'function') {
            return;
        }

        try {
            window.console.log('[PDF_PREVIEW][INFORME_TECNICO] ' + message, data || {});
        } catch (error) {
        }
    }

    function appendCacheBuster(url) {
        if (!url || url === '#') {
            return url || '';
        }

        var separator = url.indexOf('?') >= 0 ? '&' : '?';
        return url + separator + '_aocrPdfTs=' + encodeURIComponent(Date.now().toString());
    }

    function syncGeneratedDocumentFields(form) {
        if (!form) {
            return;
        }

        syncInspectionDateRangeField(form);

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

    function padDatePart(value) {
        value = parseInt(value, 10);
        return value < 10 ? '0' + value : String(value);
    }

    function toIsoDateString(date) {
        if (!date || isNaN(date.getTime())) {
            return '';
        }

        return date.getFullYear() + '-' + padDatePart(date.getMonth() + 1) + '-' + padDatePart(date.getDate());
    }

    function toDisplayDateString(isoValue) {
        if (!isoValue) {
            return '';
        }

        var parts = isoValue.split('-');
        if (parts.length !== 3) {
            return isoValue;
        }

        return parts[2] + '/' + parts[1] + '/' + parts[0];
    }

    function parseInformeDate(value) {
        value = (value || '').trim();
        if (!value) {
            return '';
        }

        var iso = value.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
        if (iso) {
            return iso[1] + '-' + padDatePart(iso[2]) + '-' + padDatePart(iso[3]);
        }

        var local = value.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
        if (local) {
            return local[3] + '-' + padDatePart(local[2]) + '-' + padDatePart(local[1]);
        }

        var parsed = new Date(value);
        return isNaN(parsed.getTime()) ? '' : toIsoDateString(parsed);
    }

    function splitInformeDateRange(value) {
        value = (value || '').trim();
        if (!value) {
            return { from: '', to: '' };
        }

        var normalized = value
            .replace(/\s+hasta\s+/ig, ' - ')
            .replace(/\s+al\s+/ig, ' - ')
            .replace(/\s+a\s+/ig, ' - ');
        var parts = normalized.indexOf(' - ') >= 0 ? normalized.split(/\s+-\s+/) : [normalized];

        return {
            from: parseInformeDate(parts[0]),
            to: parseInformeDate(parts.length > 1 ? parts[1] : '')
        };
    }

    function syncInspectionDateRangeField(form) {
        if (!form) {
            return;
        }

        var hidden = form.querySelector('#fechasInspeccionManualFieldModal');
        var fromInput = form.querySelector('#fechaInspeccionDesdeModal');
        var toInput = form.querySelector('#fechaInspeccionHastaModal');

        if (!hidden || !fromInput || !toInput) {
            return;
        }

        var fromValue = fromInput.value || '';
        var toValue = toInput.value || '';

        if (fromValue && toValue) {
            hidden.value = toDisplayDateString(fromValue) + ' - ' + toDisplayDateString(toValue);
        } else if (fromValue) {
            hidden.value = toDisplayDateString(fromValue);
        } else if (toValue) {
            hidden.value = toDisplayDateString(toValue);
        } else {
            hidden.value = '';
        }
    }

    function initInspectionDateRange(modal) {
        var form = modal ? modal.querySelector('[data-aocr-informe-form="true"]') : null;
        if (!form) {
            return;
        }

        var hidden = form.querySelector('#fechasInspeccionManualFieldModal');
        var fromInput = form.querySelector('#fechaInspeccionDesdeModal');
        var toInput = form.querySelector('#fechaInspeccionHastaModal');

        if (!hidden || !fromInput || !toInput) {
            return;
        }

        var initial = splitInformeDateRange(hidden.value);
        fromInput.value = initial.from || '';
        toInput.value = initial.to || '';

        fromInput.addEventListener('change', function () {
            if (fromInput.value && toInput.value && toInput.value < fromInput.value) {
                toInput.value = fromInput.value;
            }

            if (fromInput.value) {
                toInput.setAttribute('min', fromInput.value);
            } else {
                toInput.removeAttribute('min');
            }

            syncInspectionDateRangeField(form);
        });

        toInput.addEventListener('change', function () {
            if (fromInput.value && toInput.value && toInput.value < fromInput.value) {
                fromInput.value = toInput.value;
            }

            syncInspectionDateRangeField(form);
        });

        if (fromInput.value) {
            toInput.setAttribute('min', fromInput.value);
        }

        syncInspectionDateRangeField(form);
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

    function isResultadoSatisfactorio(value) {
        return normalizeResultado(value) === 'SATISFACTORIO';
    }

    function setSectionVisible(section, visible) {
        if (!section) {
            return;
        }

        section.style.display = visible ? '' : 'none';
    }

    function clearSectionInputs(section) {
        if (!section) {
            return;
        }

        Array.prototype.forEach.call(section.querySelectorAll('textarea, input[type="text"], input[type="hidden"]'), function (field) {
            field.value = '';
        });
    }

    function syncResultadoSections(modal) {
        if (!modal) {
            return;
        }

        var form = modal.querySelector('[data-aocr-informe-form="true"]');
        if (!form) {
            return;
        }

        var resultadoSeleccionado = form.querySelector('input[name="resultado"]:checked');
        var resultado = normalizeResultado(resultadoSeleccionado ? resultadoSeleccionado.value : '');

        Array.prototype.forEach.call(modal.querySelectorAll('[data-resultado-section]'), function (section) {
            var valorSeccion = normalizeResultado(section.getAttribute('data-resultado-section'));
            var visible = valorSeccion === resultado;
            setSectionVisible(section, visible);

            if (!visible) {
                clearSectionInputs(section);
            }
        });
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

    function getAttachmentState(input) {
        if (!input) {
            return { files: [] };
        }

        if (!attachmentInputState) {
            if (!input.__aocrAttachmentState) {
                input.__aocrAttachmentState = { files: [] };
            }

            return input.__aocrAttachmentState;
        }

        var state = attachmentInputState.get(input);
        if (!state) {
            state = { files: [] };
            attachmentInputState.set(input, state);
        }

        return state;
    }

    function fileIdentity(file) {
        if (!file) {
            return '';
        }

        return [file.name || '', file.size || 0, file.lastModified || 0].join('|');
    }

    function rebuildInputFiles(input) {
        if (!input || typeof window.DataTransfer === 'undefined') {
            return;
        }

        var state = getAttachmentState(input);
        var dataTransfer = new window.DataTransfer();
        state.files.forEach(function (file) {
            dataTransfer.items.add(file);
        });
        input.files = dataTransfer.files;
    }

    function renderSelectedFileList(input) {
        if (!input) {
            return;
        }

        var targetId = input.getAttribute('data-selected-list-target');
        if (!targetId) {
            return;
        }

        var target = document.getElementById(targetId);
        if (!target) {
            return;
        }

        var state = getAttachmentState(input);
        var emptyText = target.getAttribute('data-empty-text') || 'Sin archivos nuevos seleccionados.';
        target.innerHTML = '';

        if (!state.files.length) {
            target.textContent = emptyText;
            return;
        }

        state.files.forEach(function (file, index) {
            var row = document.createElement('div');
            row.className = 'aocr-selected-file-row';

            var name = document.createElement('span');
            name.className = 'aocr-selected-file-name';
            name.textContent = file.name || 'Archivo sin nombre';

            var meta = document.createElement('span');
            meta.className = 'aocr-selected-file-meta';
            meta.textContent = file.size ? Math.round(file.size / 1024) + ' KB' : '';

            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn btn-sm btn-outline-danger';
            remove.textContent = 'Quitar';
            remove.addEventListener('click', function () {
                state.files.splice(index, 1);
                rebuildInputFiles(input);
                renderSelectedFileList(input);
                syncAttachmentFileSummary(input);
            });

            row.appendChild(name);
            row.appendChild(meta);
            row.appendChild(remove);
            target.appendChild(row);
        });
    }

    function accumulateAttachmentFiles(input) {
        if (!input) {
            return;
        }

        var state = getAttachmentState(input);
        var existing = {};
        state.files.forEach(function (file) {
            existing[fileIdentity(file)] = true;
        });

        Array.prototype.slice.call(input.files || []).forEach(function (file) {
            var identity = fileIdentity(file);
            if (identity && !existing[identity]) {
                state.files.push(file);
                existing[identity] = true;
            }
        });

        rebuildInputFiles(input);
        renderSelectedFileList(input);
    }

    function clearAttachmentInputState(input) {
        if (!input) {
            return;
        }

        var state = getAttachmentState(input);
        state.files = [];
        input.value = '';
        rebuildInputFiles(input);
        renderSelectedFileList(input);
        syncAttachmentFileSummary(input);
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
        var state = getAttachmentState(input);
        var files = state.files && state.files.length
            ? state.files.slice()
            : Array.prototype.slice.call(input.files || []);
        if (files.length === 0) {
            target.textContent = existingSummary;
            return;
        }

        var selectedSummary = (files.length > 1 ? 'Nuevos archivos seleccionados: ' : 'Nuevo archivo seleccionado: ') + files.map(function (file) {
            return file.name;
        }).join(', ');

        target.textContent = existingSummary
            ? existingSummary + ' | ' + selectedSummary
            : selectedSummary;
    }

    function syncAttachmentUploadContainer(checkbox) {
        if (!checkbox) {
            return;
        }

        var item = checkbox.closest('.aocr-document-row') || checkbox.closest('.aocr-document-item');
        if (!item) {
            return;
        }

        var container = item.querySelector('[data-upload-container="true"]');
        if (!container) {
            return;
        }

        var visible = !!checkbox.checked;
        container.style.display = visible ? '' : 'none';

        if (visible) {
            return;
        }

        var input = container.querySelector('input[type="file"]');
        if (input) {
            clearAttachmentInputState(input);
        }
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

        var pdfUrl = appendCacheBuster(result.pdfUrl);
        var downloadUrl = appendCacheBuster(result.downloadUrl || result.pdfUrl);

        viewerRoot.setAttribute('data-pdf-url', pdfUrl);
        viewerRoot.setAttribute('data-download-url', downloadUrl);

        var status = viewerRoot.querySelector('.aocr-pdf-status');
        if (status) {
            status.textContent = result.estado || 'VISTA PREVIA';
        }

        var help = viewerRoot.querySelector('.aocr-pdf-help');
        if (help) {
            help.textContent = result.message || 'Vista previa actualizada correctamente.';
        }

        var download = viewerRoot.querySelector('.aocr-pdf-download');
        if (download) {
            download.setAttribute('href', downloadUrl);
            download.classList.remove('is-disabled');
            download.removeAttribute('aria-disabled');
        }

        var form = modal.querySelector('[data-aocr-informe-form="true"]');
        logPdfPreview('visor_actualizado', {
            inspeccionId: form ? form.getAttribute('data-inspeccion-id') : '',
            informeTecnicoId: form ? form.getAttribute('data-informe-tecnico-id') : '',
            estadoInforme: result.estado || '',
            pdfUrl: pdfUrl,
            downloadUrl: downloadUrl
        });

        if (window.AOCRPdfViewer && window.AOCRPdfViewer.instances) {
            var viewer = window.AOCRPdfViewer.instances[viewerRoot.id];
            if (!viewer && typeof window.AOCRPdfViewer.init === 'function') {
                window.AOCRPdfViewer.init(modal);
                viewer = window.AOCRPdfViewer.instances[viewerRoot.id];
            }

            if (viewer && typeof viewer.load === 'function') {
                viewer.load(pdfUrl);
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

    function focusSignaturePanel(modal) {
        if (!modal) {
            return;
        }

        var panel = modal.querySelector('[data-aocr-signature-panel="true"]');
        if (!panel) {
            return;
        }

        if (typeof panel.scrollIntoView === 'function') {
            panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }

        var target = panel.querySelector('input[name="CertificadoInspector"]:not([disabled])')
            || panel.querySelector('input[name="passwordCertificado"]:not([disabled])')
            || panel;

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

        if (isResultadoSatisfactorio(resultadoSeleccionado ? resultadoSeleccionado.value : '')) {
            formData.set('noConformidades', '');
        } else if (isResultadoInsatisfactorio(resultadoSeleccionado ? resultadoSeleccionado.value : '')) {
            formData.set('observaciones', '');
        } else {
            formData.set('observaciones', '');
            formData.set('noConformidades', '');
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

        logPdfPreview('solicitud_preview', {
            urlPreview: previewUrl,
            inspeccionId: form.getAttribute('data-inspeccion-id') || (form.querySelector('input[name="id"]') ? form.querySelector('input[name="id"]').value : ''),
            informeTecnicoId: form.getAttribute('data-informe-tecnico-id') || '',
            estadoInforme: form.getAttribute('data-estado-informe') || ''
        });

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
                    logPdfPreview('respuesta_preview_error', {
                        httpStatus: response.status,
                        message: payload && payload.message ? payload.message : ''
                    });
                    throw new Error(payload && payload.message ? payload.message : 'No se pudo generar la vista previa del informe técnico.');
                }

                logPdfPreview('respuesta_preview_ok', {
                    httpStatus: response.status,
                    pdfUrl: payload.pdfUrl || '',
                    downloadUrl: payload.downloadUrl || '',
                    estado: payload.estado || ''
                });

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
            logPdfPreview('preview_fallo', {
                message: error && error.message ? error.message : ''
            });
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
                }, 250);
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
        initInspectionDateRange(modal);

        Array.prototype.forEach.call(modal.querySelectorAll('input[type="file"]'), function (input) {
            input.addEventListener('change', function () {
                accumulateAttachmentFiles(input);
                syncAttachmentFileSummary(input);
            });
            renderSelectedFileList(input);
        });

        Array.prototype.forEach.call(modal.querySelectorAll('input[name="documentosAdjuntos"][type="checkbox"]'), function (checkbox) {
            checkbox.addEventListener('change', function () {
                syncAttachmentUploadContainer(checkbox);
            });

            syncAttachmentUploadContainer(checkbox);
        });

        Array.prototype.forEach.call(modal.querySelectorAll('input[name="resultado"]'), function (radio) {
            radio.addEventListener('change', function () {
                syncInsatisfactorioSection(modal, { guideUser: isResultadoInsatisfactorio(radio.value) });
                syncResultadoSections(modal);
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
        syncResultadoSections(modal);
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

    function openInformeTecnicoModal(url, options) {
        if (!url) {
            notify('error', 'No se encontró la ruta del Informe Técnico.');
            return Promise.resolve(false);
        }

        options = options || {};

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
                if (options.focusSignaturePanel) {
                    var onShown = function () {
                        focusSignaturePanel(modal);
                        modal.removeEventListener('shown.bs.modal', onShown);
                    };

                    modal.addEventListener('shown.bs.modal', onShown);
                }

                window.bootstrap.Modal.getOrCreateInstance(modal).show();
            } else if (options.focusSignaturePanel) {
                focusSignaturePanel(modal);
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

    document.addEventListener('change', function (event) {
        var checkbox = event.target;
        if (!checkbox || !checkbox.matches('.aocr-document-row input[type="checkbox"][name="documentosAdjuntos"]')) {
            return;
        }

        syncAttachmentUploadContainer(checkbox);
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
