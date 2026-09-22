/**
 * AOCR - Inspection Date Consolidation (Punto 4 Refactor)
 * 
 * Maneja la selección de aeropuertos y fechas de inspección
 * directamente en "Lugares de Inspección" sin duplicar en Punto 3
 * 
 * Features:
 * - Show/hide date picker cuando se marca/desmarca aeropuerto
 * - Validar que cada aeropuerto marcado tenga fecha
 * - Recolectar datos en formato Location-Date pairs
 * - Retrocompatibilidad con datos legacy (FechaInicio/FechaFin)
 */

;(function($, window) {
    'use strict';

    window.AocrInspectionDateManager = {
        // Configuración de aeropuertos
        airports: {
            'quito': { code: 'UIO', name: 'Quito', selector: '#quito' },
            'guayaquil': { code: 'GYE', name: 'Guayaquil', selector: '#guayaquil' },
            'manta': { code: 'MEC', name: 'Manta', selector: '#manta' },
            'latacunga': { code: 'LTX', name: 'Latacunga', selector: '#latacunga' }
        },

        // Estado interno de fechas seleccionadas
        selectedDates: {},

        /**
         * Inicializar manejador de fechas
         */
        init: function() {
            var self = this;

            // Listener para cambios en checkboxes de aeropuertos
            $(document).on('change', '.aeropuerto-ecuador', function() {
                self.handleAirportChange($(this));
            });

            // Listener para cambios en date inputs
            $(document).on('change', '.inspection-date-input', function() {
                self.handleDateChange($(this));
            });

            // Cargar fechas existentes si hay
            self.loadExistingDates();

            // Listener para validación al guardar
            $(document).on('click', '#btnGuardarOperaciones', function(e) {
                if (!self.validateAllAirportDates()) {
                    e.preventDefault();
                    self.showError('Todos los aeropuertos seleccionados deben tener fecha de inspección definida.');
                }
            });

            console.log('[AOCR] Inspection Date Manager initialized');
        },

        /**
         * Manejar cambio en checkbox de aeropuerto
         */
        handleAirportChange: function($checkbox) {
            var value = $checkbox.val().toLowerCase();
            var isChecked = $checkbox.is(':checked');
            var dateContainerId = 'inspection-date-' + value;
            var $dateContainer = $('#' + dateContainerId);

            if (isChecked) {
                // Mostrar date picker
                $dateContainer.fadeIn(200);
                $dateContainer.find('.inspection-date-input').focus();
            } else {
                // Ocultar y limpiar
                $dateContainer.fadeOut(200);
                var $input = $dateContainer.find('.inspection-date-input');
                $input.val('');
                delete this.selectedDates[value];
            }
        },

        /**
         * Manejar cambio en date input
         */
        handleDateChange: function($input) {
            var dateStr = $input.val();
            var value = $input.data('airport').toLowerCase();
            
            if (dateStr) {
                this.selectedDates[value] = dateStr;
            } else {
                delete this.selectedDates[value];
            }

            console.log('[AOCR] Selected dates:', this.selectedDates);
        },

        /**
         * Validar que todos los aeropuertos marcados tengan fecha
         */
        validateAllAirportDates: function() {
            var self = this;
            var allValid = true;

            $('.aeropuerto-ecuador:checked').each(function() {
                var $checkbox = $(this);
                var value = $checkbox.val().toLowerCase();
                var $dateInput = $('#inspection-date-' + value + ' .inspection-date-input');

                if (!$dateInput.val()) {
                    allValid = false;
                    $dateInput.addClass('is-invalid');
                    console.warn('[AOCR] Missing date for airport:', value);
                } else {
                    $dateInput.removeClass('is-invalid');
                }
            });

            return allValid;
        },

        /**
         * Obtener objeto de datos formateado para enviar al servidor
         */
        getFormattedData: function() {
            var self = this;
            var data = [];

            $('.aeropuerto-ecuador:checked').each(function() {
                var $checkbox = $(this);
                var value = $checkbox.val();
                var $dateInput = $('#inspection-date-' + value.toLowerCase() + ' .inspection-date-input');
                var dateStr = $dateInput.val();

                if (dateStr) {
                    data.push({
                        aeropuerto: value,
                        codigo: self.getAirportCode(value),
                        nombre: self.getAirportName(value),
                        fecha: dateStr // formato: YYYY-MM-DD
                    });
                }
            });

            // Manejar "Otros"
            if ($('#aeropuertoOtros:checked').length > 0) {
                var otrosDetail = $('#aeropuertoOtrosDetalle').val();
                var otrosDate = $('#inspection-date-otros .inspection-date-input').val();
                if (otrosDetail && otrosDate) {
                    data.push({
                        aeropuerto: 'OTROS',
                        codigo: 'OTROS',
                        nombre: otrosDetail,
                        fecha: otrosDate
                    });
                }
            }

            return data;
        },

        /**
         * Obtener código de aeropuerto
         */
        getAirportCode: function(name) {
            var key = name.toLowerCase();
            return this.airports[key] ? this.airports[key].code : 'XXX';
        },

        /**
         * Obtener nombre de aeropuerto
         */
        getAirportName: function(name) {
            var key = name.toLowerCase();
            return this.airports[key] ? this.airports[key].name : name;
        },

        /**
         * Cargar fechas existentes desde objetos hidden o data attribute
         */
        loadExistingDates: function() {
            var self = this;

            // Buscar data en atributos data-* o valores ocultos
            // Ejemplo: data-inspection-dates='{"quito":"2026-09-22","guayaquil":"2026-09-26"}'
            var existingDatesJson = $('#formAOCR').data('inspection-dates');
            
            if (existingDatesJson) {
                try {
                    var dates = typeof existingDatesJson === 'string' 
                        ? JSON.parse(existingDatesJson) 
                        : existingDatesJson;

                    $.each(dates, function(airport, date) {
                        var $checkbox = $('[value="' + airport + '"].aeropuerto-ecuador');
                        var $dateInput = $('#inspection-date-' + airport.toLowerCase() + ' .inspection-date-input');

                        if ($checkbox.length && $dateInput.length) {
                            $checkbox.prop('checked', true);
                            $dateInput.val(date);
                            self.selectedDates[airport.toLowerCase()] = date;

                            // Mostrar date picker
                            $('#inspection-date-' + airport.toLowerCase()).show();
                        }
                    });

                    console.log('[AOCR] Loaded existing inspection dates');
                } catch (e) {
                    console.error('[AOCR] Error loading existing dates:', e);
                }
            }
        },

        /**
         * Mostrar error en UI
         */
        showError: function(message) {
            // Crear alerta Bootstrap
            var $alert = $('<div class="alert alert-danger alert-dismissible fade show" role="alert">')
                .html('<strong>Error:</strong> ' + message)
                .prependTo($('#formAOCR'));

            setTimeout(function() {
                $alert.fadeOut(function() { $alert.remove(); });
            }, 5000);
        },

        /**
         * Mostrar éxito
         */
        showSuccess: function(message) {
            var $alert = $('<div class="alert alert-success alert-dismissible fade show" role="alert">')
                .html('<strong>Éxito:</strong> ' + message)
                .prependTo($('#formAOCR'));

            setTimeout(function() {
                $alert.fadeOut(function() { $alert.remove(); });
            }, 3000);
        }
    };

    // Inicializar cuando el DOM esté listo
    $(document).ready(function() {
        window.AocrInspectionDateManager.init();
    });

}(jQuery, window));
