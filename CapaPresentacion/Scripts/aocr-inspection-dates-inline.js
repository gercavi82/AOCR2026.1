(function ($, window) {
    'use strict';
    var codes = { QUITO: 'UIO', GUAYAQUIL: 'GYE', MANTA: 'MEC', LATACUNGA: 'LTX', OTROS: 'OTROS', OTRA_PROVINCIA: 'OTROS' };
    var selector = '.aeropuerto-ecuador, .lugar-inspeccion-check';
    function key($check) { return String($check.val() || '').toUpperCase(); }
    function otherInput() { return $('#ProvinciaInspeccion, #aeropuertoOtrosDetalle').first(); }
    function box($check) { return $check.data('inspection-box'); }
    function dateText(value) { var p = (value || '').split('-'); return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : ''; }
    var manager = window.AocrInspectionDateManager = {
        init: function () {
            if (!$(selector).length) return;
            var seed = $('[data-inspection-stations]').first().attr('data-inspection-stations');
            var saved = seed ? JSON.parse(seed) : [];
            $(selector).each(function (index) {
                var $check = $(this);
                if ($check.data('inspection-box')) return;
                var code = codes[key($check)];
                var $box = $('#inspection-date-' + (code === 'OTROS' ? 'otros' : key($check).toLowerCase()));
                if (!$box.length) {
                    $box = $('<div class="inspection-date-control mt-2"><label>Fecha requerida de inspección</label><input type="date" class="form-control inspection-date-input"></div>');
                    if (code === 'OTROS' && $('#divProvinciaInspeccion').length) $('#divProvinciaInspeccion').after($box);
                    else $check.closest('label').after($box);
                }
                $box.empty();
                ['inicio', 'fin'].forEach(function (part) {
                    var id = 'fecha-lugar-' + index + '-' + part;
                    $('<label class="inspection-date-label required">').attr('for', id).text(part === 'inicio' ? 'Desde' : 'Hasta').appendTo($box);
                    $('<input type="date" class="form-control inspection-date-input">').attr('id', id).attr('data-range', part).appendTo($box);
                });
                $check.data('inspection-box', $box);
                var record = saved.filter(function (e) { return String(e.EstacionCodigo).toUpperCase() === code; })[0];
                if (!record && code === 'OTROS') record = saved.filter(function(e) { return ['UIO', 'GYE', 'MEC', 'LTX'].indexOf(String(e.EstacionCodigo).toUpperCase()) < 0; })[0];
                if (record) {
                    $check.prop('checked', true).data('station-record', record);
                    if (code === 'OTROS') otherInput().val(record.EstacionNombre || '');
                    $box.find('[data-range=inicio]').val(record.FechaInicio || record.FechaInspeccion || '');
                    $box.find('[data-range=fin]').val(record.FechaFin || record.FechaInspeccion || '');
                }
                manager.toggle($check, false);
            });
            $(document).off('change.inspectionDates', selector).on('change.inspectionDates', selector, function () { manager.toggle($(this), true); manager.refreshPreview(); });
            $(document).off('input.inspectionDates change.inspectionDates', '.inspection-date-input, #ProvinciaInspeccion, #aeropuertoOtrosDetalle')
                .on('input.inspectionDates change.inspectionDates', '.inspection-date-input, #ProvinciaInspeccion, #aeropuertoOtrosDetalle', function () { manager.refreshPreview(); });
            // Native form submission (Nueva Orden) and AJAX (Solicitud AOCR) share this data model.
            $(selector).closest('form').off('submit.inspectionDates').on('submit.inspectionDates', function (e) {
                if ($('#seccionViaticos').length && !$('#seccionViaticos').is(':visible')) return;
                if (!manager.validateAllAirportDates()) { e.preventDefault(); e.stopImmediatePropagation(); manager.showError(); return false; }
                manager.prepareSubmission($(this));
            });
            manager.refreshPreview();
        },
        toggle: function ($check, clear) {
            var checked = $check.is(':checked'), $box = box($check);
            if (!$box) return;
            $box.toggle(checked).find('input').prop('disabled', !checked).prop('required', checked);
            if (!checked && clear) { $box.find('input').val('').removeClass('is-invalid'); }
            if (codes[key($check)] === 'OTROS') {
                otherInput().prop('disabled', !checked).prop('required', checked);
                if (!checked && clear) otherInput().val('');
                $('#divProvinciaInspeccion, #aeropuertoOtrosContenedor').toggle(checked);
            }
        },
        validateAllAirportDates: function () {
            var valid = $(selector).filter(':checked').length > 0;
            $(selector).filter(':checked').each(function () {
                var $input = box($(this)).find('input[type=date]');
                var start = $input.filter('[data-range=inicio]').val(), end = $input.filter('[data-range=fin]').val();
                var ok = !!start && !!end && end >= start && $input.toArray().every(function (input) { return input.checkValidity(); });
                $input.toggleClass('is-invalid', !ok); valid = valid && ok;
                if (codes[key($(this))] === 'OTROS' && !$.trim(otherInput().val())) valid = false;
            });
            return valid;
        },
        getEstaciones: function () {
            return $(selector).filter(':checked').map(function () {
                var $check = $(this), code = codes[key($check)], old = $check.data('station-record') || {};
                var start = box($check).find('[data-range=inicio]').val() || '';
                var end = box($check).find('[data-range=fin]').val() || '';
                return { Id: old.Id || 0, Version: old.Version || 1, EstacionCodigo: code,
                    EstacionNombre: code === 'OTROS' ? $.trim(otherInput().val()) : $check.val(),
                    FechaInicio: start, FechaFin: end, Estado: old.Estado || '', Observacion: old.Observacion || '' };
            }).get();
        },
        prepareSubmission: function ($form) {
            $form.find('.inspection-station-field').remove();
            manager.getEstaciones().forEach(function (station, i) {
                Object.keys(station).forEach(function (prop) {
                    $('<input type="hidden" class="inspection-station-field">').attr('name', 'Estaciones[' + i + '].' + prop).val(station[prop]).appendTo($form);
                });
            });
        },
        refreshPreview: function () {
            var stations = manager.getEstaciones();
            var $body = $('#inspection-place-preview tbody').empty();
            stations.forEach(function (station) {
                var $row = $('<tr>'); $('<td>').text(station.EstacionNombre + (station.EstacionCodigo === 'OTROS' ? '' : ' (' + station.EstacionCodigo + ')')).appendTo($row);
                $('<td>').text(manager.rangeText(station) || 'Seleccione Desde y Hasta').appendTo($row); $body.append($row);
            });
            $('#FechasInspeccion').val(stations.map(function (s) { return s.EstacionNombre + ': ' + manager.rangeText(s); }).join('; '));
        },
        rangeText: function (station) {
            if (!station.FechaInicio || !station.FechaFin) return '';
            return dateText(station.FechaInicio) + (station.FechaInicio === station.FechaFin ? '' : ' al ' + dateText(station.FechaFin));
        },
        updateSaved: function (stations) {
            (stations || []).forEach(function (station) {
                $(selector).each(function () {
                    if (codes[key($(this))] === station.EstacionCodigo) $(this).data('station-record', station);
                });
            });
        },
        showError: function () {
            var msg = 'Seleccione al menos un lugar y complete Desde y Hasta. La fecha final no puede ser anterior a la inicial. Complete también la provincia/localidad si seleccionó Otro.';
            if (window.Swal) window.Swal.fire({ icon: 'warning', title: 'Fechas de inspección', text: msg });
            else window.alert(msg);
        }
    };
    $(function () { manager.init(); });
}(jQuery, window));
