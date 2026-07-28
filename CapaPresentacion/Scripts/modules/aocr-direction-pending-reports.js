(function ($, window, document) {
    'use strict';

    var moduleSelector = '[data-module="direction-pending-reports"]';

    function normalize(value) {
        return $.trim(value || '').toUpperCase();
    }

    function initFilters($module) {
        var $rows = $module.find('[data-report-row]');
        if (!$rows.length) return;

        var $search = $module.find('#aocrReportSearch');
        var $status = $module.find('#aocrReportStatus');
        var $inspector = $module.find('#aocrReportInspector');
        var $dateFrom = $module.find('#aocrReportDateFrom');
        var $dateTo = $module.find('#aocrReportDateTo');
        var $counter = $module.find('#aocrReportVisibleCount');
        var $empty = $module.find('#aocrReportNoResults');

        function apply() {
            var query = $.trim($search.val() || '').toLowerCase();
            var status = normalize($status.val());
            var inspector = normalize($inspector.val());
            var dateFrom = $.trim($dateFrom.val() || '');
            var dateTo = $.trim($dateTo.val() || '');
            var visible = 0;

            $rows.each(function () {
                var $row = $(this);
                var date = $row.attr('data-date') || '';
                var show = (!query || ($row.attr('data-search') || '').indexOf(query) !== -1)
                    && (!status || normalize($row.attr('data-status')) === status)
                    && (!inspector || normalize($row.attr('data-inspector')) === inspector)
                    && (!dateFrom || (date && date >= dateFrom))
                    && (!dateTo || (date && date <= dateTo));
                $row.prop('hidden', !show);
                if (show) visible++;
            });

            $counter.text(visible + (visible === 1 ? ' registro' : ' registros'));
            $empty.prop('hidden', visible !== 0);
        }

        $module.find('#aocrReportSearch,#aocrReportStatus,#aocrReportInspector,#aocrReportDateFrom,#aocrReportDateTo')
            .off('.directionReports')
            .on('input.directionReports change.directionReports', apply);

        $module.find('#aocrReportClear').off('click.directionReports').on('click.directionReports', function () {
            $search.val('');
            $status.val('');
            $inspector.val('');
            $dateFrom.val('');
            $dateTo.val('');
            apply();
            $search.trigger('focus');
        });
    }

    function init() {
        var $module = $(moduleSelector);
        if (!$module.length) return;
        initFilters($module);
    }

    $(document).ready(init);
})(jQuery, window, document);
