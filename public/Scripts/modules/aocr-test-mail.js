(function ($, window, document) {
    'use strict';

    var moduleSelector = '[data-module="test-mail"]';

    function readValue($module, selector) {
        return $.trim($module.find(selector).val() || '');
    }

    function getTemplateInfo($module) {
        var $select = $module.find('#Plantilla');
        var value = $.trim($select.val() || '');
        var text = value ? $.trim($select.find('option:selected').text()) : 'No seleccionada';
        var category = value.indexOf(':') > -1 ? value.split(':')[0].toUpperCase() : '';
        var requiredReference = category === 'INSPECCION' ? 'INSPECCION'
            : category === 'ORDEN' ? 'ORDEN'
            : (category === 'LEGACY' || category === 'GENERIC' || category === 'SOLICITUD') ? 'SOLICITUD'
            : '';
        return { value: value, text: text, category: category, requiredReference: requiredReference };
    }

    function initTemplateRequirements($module) {
        var $select = $module.find('#Plantilla');

        function refresh() {
            var info = getTemplateInfo($module);
            var descriptions = {
                LEGACY: 'Plantilla compatible con el flujo histórico de solicitudes AOCR.',
                GENERIC: 'Notificación institucional asociada al cambio de estado de una solicitud.',
                SOLICITUD: 'Comunicación vinculada directamente con una solicitud AOCR.',
                INSPECCION: 'Comunicación generada dentro del flujo técnico de inspección.',
                ORDEN: 'Comunicación relacionada con una orden de recaudación.'
            };

            $module.find('#aocrTemplateName').text(info.value ? info.text : 'Seleccione una plantilla');
            $module.find('#aocrTemplateDescription').text(
                info.value ? (descriptions[info.category] || 'Plantilla institucional de prueba.') :
                    'Aquí verá la categoría y los datos necesarios para realizar la prueba.'
            );

            var $tags = $module.find('#aocrTemplateTags').empty();
            if (info.value) {
                $('<span>').text('Categoría: ' + info.category).appendTo($tags);
                $('<span>').text('Requiere: ' + (info.requiredReference || 'validación del servidor')).appendTo($tags);
                $('<span>').text('Opcionales: nombre y observación').appendTo($tags);
            }

            $module.find('[data-reference-field]').each(function () {
                var $field = $(this);
                var isRequired = $field.attr('data-reference-field') === info.requiredReference;
                $field.toggleClass('is-required', isRequired);
                $field.find('.aocr-field-requirement').text(isRequired ? 'Obligatorio' : 'Opcional');
                $field.find('input').attr('aria-required', isRequired ? 'true' : 'false');
            });

            updateSummary($module);
        }

        $select.off('change.testMail').on('change.testMail', refresh);
        refresh();
    }

    function updateSummary($module) {
        var info = getTemplateInfo($module);
        var destination = readValue($module, '#CorreoDestino');
        var requiredValue = info.requiredReference === 'INSPECCION' ? readValue($module, '#InspeccionId')
            : info.requiredReference === 'ORDEN' ? readValue($module, '#OrdenId')
            : info.requiredReference === 'SOLICITUD' ? readValue($module, '#SolicitudId')
            : '';
        var isReady = !!info.value && !!destination && (!info.requiredReference || !!requiredValue);

        $module.find('#aocrSummaryTemplate').text(info.text);
        $module.find('#aocrSummaryDestination').text(destination || 'No informado');
        $module.find('#aocrSummaryRequest').text(readValue($module, '#SolicitudId') || '—');
        $module.find('#aocrSummaryInspection').text(readValue($module, '#InspeccionId') || '—');
        $module.find('#aocrSummaryOrder').text(readValue($module, '#OrdenId') || '—');

        var $status = $module.find('#aocrSummaryStatus');
        $status.toggleClass('is-ready', isReady).toggleClass('is-sending', false);
        $status.contents().filter(function () { return this.nodeType === 3; }).remove();
        $status.append(document.createTextNode(isReady ? ' Listo para enviar' : ' Pendiente de configuración'));
    }

    function initSummary($module) {
        $module.find('#Plantilla,#CorreoDestino,#SolicitudId,#InspeccionId,#OrdenId')
            .off('input.testMail change.testMailSummary')
            .on('input.testMail change.testMailSummary', function () { updateSummary($module); });
        updateSummary($module);
    }

    function initCharacterCounter($module) {
        var $observation = $module.find('#Observacion');
        var $counter = $module.find('#aocrObservationCounter');
        function refresh() {
            $counter.text(($observation.val() || '').length + ' / 1000 caracteres');
        }
        $observation.off('input.testMailCounter').on('input.testMailCounter', refresh);
        refresh();
    }

    function buildConfirmation($module) {
        var info = getTemplateInfo($module);
        return [
            '¿Confirma el envío de esta plantilla al buzón sandbox indicado?',
            '',
            'Plantilla: ' + info.text,
            'Destino: ' + (readValue($module, '#CorreoDestino') || 'No informado'),
            'Solicitud: ' + (readValue($module, '#SolicitudId') || '—'),
            'Inspección: ' + (readValue($module, '#InspeccionId') || '—'),
            'Orden: ' + (readValue($module, '#OrdenId') || '—'),
            '',
            'Este es un envío de prueba controlado.'
        ].join('\n');
    }

    function initSubmitProtection($module) {
        var $form = $module.find('#aocrTestMailForm');
        var $submit = $module.find('#aocrTestMailSubmit');
        var $live = $module.find('#aocrTestMailLiveStatus');
        var submitting = false;

        $form.off('submit.testMail').on('submit.testMail', function (event) {
            if (submitting) {
                event.preventDefault();
                return false;
            }
            if (!this.checkValidity()) {
                return true;
            }
            if (!window.confirm(buildConfirmation($module))) {
                event.preventDefault();
                return false;
            }
            submitting = true;
            $submit.prop('disabled', true)
                .html('<i class="fas fa-spinner fa-spin" aria-hidden="true"></i><span>Enviando…</span>');
            $module.find('#aocrSummaryStatus').removeClass('is-ready').addClass('is-sending')
                .contents().filter(function () { return this.nodeType === 3; }).remove();
            $module.find('#aocrSummaryStatus').append(document.createTextNode(' Enviando'));
            $live.text('Enviando correo de prueba.');
            return true;
        });
    }

    function initClear($module) {
        var $form = $module.find('#aocrTestMailForm');
        var initialState = $form.serialize();
        $module.find('#aocrTestMailClear').off('click.testMail').on('click.testMail', function () {
            if ($form.serialize() !== initialState &&
                !window.confirm('Hay datos modificados. ¿Desea limpiar el formulario?')) {
                return;
            }
            $form[0].reset();
            $module.find('#Plantilla').trigger('change');
            $module.find('#Observacion').trigger('input');
            updateSummary($module);
        });
    }

    function init() {
        var $module = $(moduleSelector);
        if (!$module.length) return;
        initTemplateRequirements($module);
        initSummary($module);
        initCharacterCounter($module);
        initSubmitProtection($module);
        initClear($module);
    }

    $(document).ready(init);
})(jQuery, window, document);
