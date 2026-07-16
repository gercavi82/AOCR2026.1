(function (window, document) {
    'use strict';

    function configure($) {
        if (!$ || !$.fn || !$.fn.dataTable) return;

        $.extend(true, $.fn.dataTable.defaults, {
            pageLength: 5,
            lengthChange: false,
            responsive: true,
            autoWidth: false,
            scrollX: false,
            language: {
                processing: 'Procesando...',
                search: 'Buscar:',
                lengthMenu: 'Mostrar _MENU_ registros',
                info: 'Mostrando _START_ a _END_ de _TOTAL_ registros',
                infoEmpty: 'Mostrando 0 a 0 de 0 registros',
                infoFiltered: '(filtrado de _MAX_ registros en total)',
                loadingRecords: 'Cargando...',
                zeroRecords: 'No se encontraron resultados',
                emptyTable: 'No hay datos disponibles en esta tabla',
                paginate: {
                    first: 'Primero',
                    previous: 'Anterior',
                    next: 'Siguiente',
                    last: 'Ultimo'
                }
            }
        });
    }

    function initTables(root) {
        var $ = window.jQuery;
        if (!$ || !$.fn || !$.fn.DataTable) return;
        configure($);

        $(root || document).find('table.aocr-datatable').each(function () {
            var $table = $(this);
            if ($.fn.DataTable.isDataTable(this)) {
                $table.DataTable().columns.adjust();
                return;
            }

            $table.wrap('<div class="table-responsive aocr-table-responsive"></div>');
            $table.DataTable({
                pageLength: 5,
                responsive: true,
                autoWidth: false,
                scrollX: $table.outerWidth() > $table.parent().width()
            });
        });
    }

    function boot() {
        if (!window.jQuery || !window.jQuery.fn || !window.jQuery.fn.DataTable) {
            setTimeout(boot, 80);
            return;
        }

        configure(window.jQuery);
        initTables(document);
        document.dispatchEvent(new Event('AOCR.DataTablesReady'));
    }

    document.addEventListener('DOMContentLoaded', boot);
    window.AOCRDataTables = {
        configure: function () { configure(window.jQuery); },
        init: initTables
    };
})(window, document);
