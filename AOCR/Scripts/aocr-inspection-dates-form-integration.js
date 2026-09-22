/**
 * AOCR - Inspection Dates Form Integration
 * 
 * Integra el manejador de fechas de inspección con el formulario
 * de presentación de solicitud (FormularioCompleto)
 */

;(function($, window) {
    'use strict';

    /**
     * Interceptor para el envío del formulario
     * Transforma datos de fechas inline a estructura legacy si es necesario
     */
    window.AocrInspectionDatesFormIntegration = {
        
        /**
         * Inicializar integración con formulario
         */
        init: function() {
            var self = this;

            // Interceptar envío del formulario principal
            $(document).on('submit', '#formAOCR, form[name="FormularioCompleto"]', function(e) {
                // Validar que las fechas estén completas
                if (!window.AocrInspectionDateManager.validateAllAirportDates()) {
                    e.preventDefault();
                    console.error('[AOCR] Validation failed: some airports missing dates');
                    return false;
                }

                // Preparar datos de fechas para envío
                self.prepareInspectionDatesForSubmission();
            });

            // Interceptar clic en "Guardar Operaciones"
            $(document).on('click', '#btnGuardarOperaciones', function(e) {
                if (!window.AocrInspectionDateManager.validateAllAirportDates()) {
                    e.preventDefault();
                    alert('Error: Todos los aeropuertos seleccionados deben tener fecha de inspección definida.');
                    return false;
                }
            });

            console.log('[AOCR] Form integration initialized');
        },

        /**
         * Preparar datos de fechas para envío al servidor
         * Crea campos hidden con los datos en formato que el controller espera
         */
        prepareInspectionDatesForSubmission: function() {
            var $form = $('#formAOCR, form[name="FormularioCompleto"]');
            var dateManager = window.AocrInspectionDateManager;

            // Limpiar campos hidden previos
            $form.find('input[name^="Estaciones["]').remove();

            // Recopilar datos de aeropuertos seleccionados
            var index = 0;
            var self = this;

            $('.aeropuerto-ecuador:checked').each(function() {
                var $checkbox = $(this);
                var value = $checkbox.val();
                var code = dateManager.getAirportCode(value);
                var name = dateManager.getAirportName(value);
                var $dateInput = $('#inspection-date-' + value.toLowerCase() + ' .inspection-date-input');
                var dateStr = $dateInput.val();

                // Crear entrada para cada aeropuerto seleccionado
                if (dateStr) {
                    // Formato legacy para retrocompatibilidad:
                    // Estaciones[0].EstacionCodigo = "UIO"
                    // Estaciones[0].EstacionNombre = "Quito"
                    // Estaciones[0].FechaInspeccion = "2026-09-22"
                    
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].EstacionCodigo').val(code)
                    );
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].EstacionNombre').val(name)
                    );
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].FechaInspeccion').val(dateStr)
                    );

                    console.log('[AOCR] Added inspection date:', { code: code, name: name, date: dateStr });
                    index++;
                }
            });

            // Manejar "Otros"
            if ($('#aeropuertoOtros:checked').length > 0) {
                var otrosDetail = $('#aeropuertoOtrosDetalle').val();
                var otrosDate = $('#inspection-date-otros .inspection-date-input').val();

                if (otrosDetail && otrosDate) {
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].EstacionCodigo').val('OTROS')
                    );
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].EstacionNombre').val(otrosDetail)
                    );
                    $form.append(
                        $('<input type="hidden">').attr('name', 'Estaciones[' + index + '].FechaInspeccion').val(otrosDate)
                    );

                    console.log('[AOCR] Added "Otros" inspection date:', { detail: otrosDetail, date: otrosDate });
                    index++;
                }
            }

            console.log('[AOCR] Total inspection dates prepared:', index);
        }
    };

    // Inicializar cuando DOM esté listo
    $(document).ready(function() {
        window.AocrInspectionDatesFormIntegration.init();
    });

}(jQuery, window));
