$(function () {
    // 1. Auto-load tracking sections based on attributes
    $('[data-aocr-recorrido-solicitud]').each(function () {
        var $div = $(this);
        var id = $div.data('aocr-recorrido-solicitud');
        if (id) {
            $div.html('<div class="p-3 text-center text-muted"><i class="fas fa-spinner fa-spin fa-2x mb-2"></i><br/>Cargando recorrido del trámite...</div>');
            $.get('/RecorridoTramite/Ver', { solicitudId: id }, function (html) {
                $div.html(html);
                initFilterHandlers($div);
            }).fail(function() {
                $div.html('<div class="alert alert-danger m-2">Error al cargar el recorrido del trámite.</div>');
            });
        }
    });

    $('[data-aocr-recorrido-orden]').each(function () {
        var $div = $(this);
        var id = $div.data('aocr-recorrido-orden');
        if (id) {
            $div.html('<div class="p-3 text-center text-muted"><i class="fas fa-spinner fa-spin fa-2x mb-2"></i><br/>Cargando recorrido del trámite...</div>');
            $.get('/RecorridoTramite/VerPorOrden', { ordenId: id }, function (html) {
                $div.html(html);
                initFilterHandlers($div);
            }).fail(function() {
                $div.html('<div class="alert alert-danger m-2">Error al cargar el recorrido del trámite.</div>');
            });
        }
    });

    $('[data-aocr-recorrido-inspeccion]').each(function () {
        var $div = $(this);
        var id = $div.data('aocr-recorrido-inspeccion');
        if (id) {
            $div.html('<div class="p-3 text-center text-muted"><i class="fas fa-spinner fa-spin fa-2x mb-2"></i><br/>Cargando recorrido del trámite...</div>');
            $.get('/RecorridoTramite/VerPorInspeccion', { inspeccionId: id }, function (html) {
                $div.html(html);
                initFilterHandlers($div);
            }).fail(function() {
                $div.html('<div class="alert alert-danger m-2">Error al cargar el recorrido del trámite.</div>');
            });
        }
    });

    $('[data-aocr-recorrido-informe]').each(function () {
        var $div = $(this);
        var id = $div.data('aocr-recorrido-informe');
        if (id) {
            $div.html('<div class="p-3 text-center text-muted"><i class="fas fa-spinner fa-spin fa-2x mb-2"></i><br/>Cargando recorrido del trámite...</div>');
            $.get('/RecorridoTramite/VerPorInforme', { informeId: id }, function (html) {
                $div.html(html);
                initFilterHandlers($div);
            }).fail(function() {
                $div.html('<div class="alert alert-danger m-2">Error al cargar el recorrido del trámite.</div>');
            });
        }
    });

    function initFilterHandlers($container) {
        $container.find('#filterGroupAocr button').on('click', function () {
            var $btn = $(this);
            var filter = $btn.data('filter');
            
            // Toggle active class on buttons
            $btn.addClass('active').siblings().removeClass('active');
            
            // Filter rows
            var $rows = $container.find('.aocr-tabla-recorrido tbody tr');
            if (filter === 'todos') {
                $rows.show();
            } else {
                $rows.hide();
                $rows.filter('[data-status-type="' + filter + '"]').show();
            }
        });
    }

    // Modal quick-view click handler for Financiero dashboard
    $(document).on('click', '.btn-ver-recorrido', function() {
        var ordenId = $(this).data('orden-id');
        var $body = $('#modalRecorridoGeneralBody');
        if (ordenId && $body.length) {
            $body.html('<div class="p-3 text-center text-muted"><i class="fas fa-spinner fa-spin fa-2x mb-2"></i><br/>Cargando recorrido del trámite...</div>');
            $.get('/RecorridoTramite/VerPorOrden', { ordenId: ordenId }, function (html) {
                $body.html(html);
                initFilterHandlers($body);
            }).fail(function() {
                $body.html('<div class="alert alert-danger m-2">Error al cargar el recorrido del trámite.</div>');
            });
        }
    });
});
