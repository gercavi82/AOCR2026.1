(function ($, window, document) {
    'use strict';

    var moduleSelector = '[data-module="coordination-dashboard"]';
    var eventNamespace = '.coordDashboard';

    function normalize(value) {
        return $.trim(value || '').toUpperCase();
    }

    function initMainTabs($module) {
        var $tabs = $module.find('.aocr-coordination-tabs [role="tab"]');
        var $panels = $module.find('.aocr-tab-panel');

        function resolvePane() {
            var hash = (window.location.hash || '').replace('#', '');
            var exists;

            if (!hash) {
                return 'pane-bandeja';
            }

            exists = $panels.filter(function () {
                return this.id === hash;
            }).length > 0;

            return exists ? hash : 'pane-bandeja';
        }

        function activate(paneId, updateHash) {
            $tabs.each(function () {
                var active = $(this).attr('data-pane') === paneId;
                $(this).toggleClass('active', active).attr('aria-selected', active ? 'true' : 'false');
            });
            $panels.each(function () {
                $(this).toggleClass('active', this.id === paneId);
            });

            $module.find('.js-dashboard-table').each(function () {
                if ($.fn.DataTable && $.fn.DataTable.isDataTable(this)) {
                    $(this).DataTable().columns.adjust();
                }
            });

            if (updateHash && window.history && window.history.replaceState) {
                window.history.replaceState(null, document.title, window.location.pathname + window.location.search + '#' + paneId);
            }
        }

        $tabs.off(eventNamespace).on('click' + eventNamespace, function () {
            activate($(this).attr('data-pane'), true);
        });
        $(window).off('hashchange' + eventNamespace).on('hashchange' + eventNamespace, function () {
            activate(resolvePane(), false);
        });
        activate(resolvePane(), false);
    }

    function initStageTabs($module) {
        var $tabs = $module.find('[data-mobile-stage]');
        var $columns = $module.find('[data-column]');

        function activate(stage) {
            $tabs.each(function () {
                var active = $(this).attr('data-mobile-stage') === stage;
                $(this).toggleClass('is-active', active).attr('aria-selected', active ? 'true' : 'false');
            });
            $columns.removeClass('is-active-mobile')
                .filter('[data-column="' + stage + '"]').addClass('is-active-mobile');
        }

        $tabs.off(eventNamespace).on('click' + eventNamespace, function () {
            activate($(this).attr('data-mobile-stage'));
        });
        activate($tabs.filter('.is-active').first().attr('data-mobile-stage') || 'PENDIENTES');
    }

    function initResponsiveMode($module) {
        var stageBreakpoint = 900;
        var compactBreakpoint = 620;
        var observer;

        function apply() {
            var width = Math.round($module[0].getBoundingClientRect().width);
            $module.toggleClass('is-stage-mode', width > 0 && width <= stageBreakpoint);
            $module.toggleClass('is-mobile-layout', width > 0 && width <= compactBreakpoint);
        }

        if (window.ResizeObserver) {
            observer = new window.ResizeObserver(apply);
            observer.observe($module[0]);
            $module.data('coordinationResizeObserver', observer);
        } else {
            $(window).off('resize' + eventNamespace).on('resize' + eventNamespace, apply);
        }

        apply();
    }

    function initFilters($module) {
        var $search = $module.find('#coordinationSearch');
        var $company = $module.find('#compania');
        var $inspector = $module.find('#inspector');
        var $status = $module.find('#estado');
        var $priority = $module.find('#coordinationPriority');
        var $type = $module.find('#coordinationType');
        var $cards = $module.find('[data-flow-card]');
        var $rows = $module.find('[data-coordination-row]');
        var $live = $module.find('#aocrFlowLiveStatus');
        var $noResults = $module.find('#aocrFlowNoResults');

        function matches($item) {
            var query = $.trim($search.val() || '').toLowerCase();
            var company = normalize($company.val());
            var inspector = normalize($inspector.val());
            var status = normalize($status.val());
            var priority = normalize($priority.val());
            var type = normalize($type.val());
            var itemSearch = ($item.attr('data-search') || '').toLowerCase();

            return (!query || itemSearch.indexOf(query) !== -1)
                && (!company || itemSearch.indexOf(company.toLowerCase()) !== -1)
                && (!inspector || normalize($item.attr('data-inspector')) === inspector)
                && (!status || normalize($item.attr('data-status')) === status)
                && (!priority || normalize($item.attr('data-priority')) === priority)
                && (!type || normalize($item.attr('data-type')) === type);
        }

        function updateColumn($column) {
            var limit = parseInt($column.attr('data-preview-limit'), 10) || 3;
            var expanded = $column.attr('data-expanded') === 'true';
            var $matching = $column.find('[data-flow-card][data-match="true"]');

            $column.find('[data-flow-card]').addClass('d-none');
            (expanded ? $matching : $matching.slice(0, limit)).removeClass('d-none');
            $column.find('.js-kanban-visible').text(expanded ? $matching.length : Math.min(limit, $matching.length));
            $column.find('.di3-col__foot').toggleClass('d-none', $matching.length <= limit);
            $column.find('.js-kanban-toggle').text(expanded ? 'Ver menos' : 'Ver todos (' + $matching.length + ')');
        }

        function apply() {
            var visible = 0;
            $cards.each(function () {
                var match = matches($(this));
                $(this).attr('data-match', match ? 'true' : 'false');
                if (match) visible++;
            });
            $rows.each(function () {
                $(this).toggle(matches($(this)));
            });
            $module.find('[data-column]').each(function () {
                updateColumn($(this));
            });
            $noResults.prop('hidden', visible !== 0);
            $live.text(visible + (visible === 1 ? ' trámite visible' : ' trámites visibles'));
        }

        $module.find('#coordinationSearch,#compania,#inspector,#estado,#coordinationPriority,#coordinationType')
            .off(eventNamespace)
            .on('input' + eventNamespace + ' change' + eventNamespace, apply);

        $module.find('.js-kanban-toggle').off(eventNamespace).on('click' + eventNamespace, function () {
            var $column = $module.find('[data-column="' + $(this).attr('data-column') + '"]');
            $column.attr('data-expanded', $column.attr('data-expanded') === 'true' ? 'false' : 'true');
            updateColumn($column);
        });
        apply();
    }

    function initViewMode($module) {
        var $flow = $module.find('.aocr-flow-board-page');
        var storageKey = 'aocr-coordination-view';
        var mode = 'board';

        try {
            mode = window.sessionStorage.getItem(storageKey) || 'board';
        } catch (ignore) {}

        function apply(nextMode) {
            mode = nextMode === 'compact' ? 'compact' : 'board';
            $flow.toggleClass('is-compact', mode === 'compact');
            $module.find('[data-view-mode]').each(function () {
                $(this).toggleClass('is-active', $(this).attr('data-view-mode') === mode);
            });
            try {
                window.sessionStorage.setItem(storageKey, mode);
            } catch (ignore) {}
        }

        $module.find('[data-view-mode]').off(eventNamespace).on('click' + eventNamespace, function () {
            apply($(this).attr('data-view-mode'));
        });
        apply(mode);
    }

    function initRefresh($module) {
        $module.find('.aocr-coordination-filters__toggle').off(eventNamespace).on('click' + eventNamespace, function () {
            var $button = $(this);
            var open = !$module.find('.aocr-coordination-filters').hasClass('is-open');
            $module.find('.aocr-coordination-filters').toggleClass('is-open', open);
            $button.attr('aria-expanded', open ? 'true' : 'false')
                .html('<i class="fas fa-filter" aria-hidden="true"></i> ' + (open ? 'Ocultar filtros' : 'Mostrar filtros'));
        });
    }

    function initTooltips($module) {
        if (!window.bootstrap || !window.bootstrap.Tooltip) {
            return;
        }
        $module.find('[title]').each(function () {
            window.bootstrap.Tooltip.getOrCreateInstance(this);
        });
    }

    function init() {
        var $module = $(moduleSelector);
        if (!$module.length) {
            return;
        }

        initMainTabs($module);
        initRefresh($module);
        initTooltips($module);
    }

    $(document).ready(init);

})(jQuery, window, document);
