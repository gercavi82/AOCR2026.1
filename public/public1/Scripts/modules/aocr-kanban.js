(function ($, window, document) {
    'use strict';

    var moduleSelector = '[data-module="aocr-kanban"]';
    var namespace = '.aocrKanban';

    function normalize(value) {
        return $.trim(value || '').toUpperCase();
    }

    function initStageNavigation($module) {
        var $tabs = $module.find('[data-stage]');
        var $columns = $module.find('[data-column]');

        function activate(stage, moveBoard) {
            $tabs.each(function () {
                var selected = $(this).attr('data-stage') === stage;
                $(this).toggleClass('is-active', selected).attr('aria-selected', selected ? 'true' : 'false');
            });
            $columns.removeClass('is-active-stage')
                .filter('[data-column="' + stage + '"]').addClass('is-active-stage');

            if (moveBoard && !$module.hasClass('is-stage-layout')) {
                var column = $columns.filter('[data-column="' + stage + '"]')[0];
                var board = $module.find('.aocr-kanban__board')[0];
                if (column && board) {
                    board.scrollTo({ left: Math.max(0, column.offsetLeft - board.offsetLeft), behavior: 'smooth' });
                }
            }
        }

        $tabs.off(namespace).on('click' + namespace, function () {
            activate($(this).attr('data-stage'), true);
        });
        activate($tabs.first().attr('data-stage') || 'PENDIENTES', false);
    }

    function initFilters($module) {
        var $cards = $module.find('[data-flow-card]');
        var $rows = $module.find('[data-compact-row]');
        var $search = $module.find('#aocrKanbanSearch');
        var $company = $module.find('#aocrKanbanCompany');
        var $inspector = $module.find('#aocrKanbanInspector');
        var $status = $module.find('#aocrKanbanStatus');
        var $priority = $module.find('#aocrKanbanPriority');
        var $type = $module.find('#aocrKanbanType');
        var $noResults = $module.find('.aocr-kanban__no-results');
        var $live = $module.find('[data-live-status]');

        function matches($item) {
            var query = $.trim($search.val() || '').toLowerCase();
            var itemSearch = ($item.attr('data-search') || '').toLowerCase();
            return (!query || itemSearch.indexOf(query) !== -1)
                && (!normalize($company.val()) || normalize($item.attr('data-company')) === normalize($company.val()))
                && (!normalize($inspector.val()) || normalize($item.attr('data-inspector')) === normalize($inspector.val()))
                && (!normalize($status.val()) || normalize($item.attr('data-status')) === normalize($status.val()))
                && (!normalize($priority.val()) || normalize($item.attr('data-priority')) === normalize($priority.val()))
                && (!normalize($type.val()) || normalize($item.attr('data-type')) === normalize($type.val()));
        }

        function renderColumn($column) {
            var limit = parseInt($column.attr('data-preview-limit'), 10) || 3;
            var expanded = $column.attr('data-expanded') === 'true';
            var $matching = $column.find('[data-flow-card][data-match="true"]');
            var $toggle = $column.find('.js-kanban-toggle');

            $column.find('[data-flow-card]').addClass('d-none');
            (expanded ? $matching : $matching.slice(0, limit)).removeClass('d-none');
            $column.find('.js-kanban-visible').text(expanded ? $matching.length : Math.min(limit, $matching.length));
            $column.find('.aocr-kanban-column__footer').toggleClass('d-none', $matching.length <= limit);
            $toggle.text(expanded ? 'Ver menos' : 'Ver todos (' + $matching.length + ')');
        }

        function apply() {
            var visible = 0;
            $cards.each(function () {
                var match = matches($(this));
                $(this).attr('data-match', match ? 'true' : 'false');
                if (match) {
                    visible++;
                }
            });
            $rows.each(function () {
                $(this).prop('hidden', !matches($(this)));
            });
            $module.find('[data-column]').each(function () {
                renderColumn($(this));
            });
            $noResults.prop('hidden', visible > 0);
            $live.text(visible + (visible === 1 ? ' trámite visible' : ' trámites visibles'));
        }

        $module.find('[data-filter-apply]').off(namespace).on('click' + namespace, apply);
        $module.find('[data-filter-clear]').off(namespace).on('click' + namespace, function () {
            $search.val('');
            $company.val('');
            $inspector.val('');
            $status.val('');
            $priority.val('');
            $type.val('');
            apply();
            $search.trigger('focus');
        });
        $search.off(namespace).on('keydown' + namespace, function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                apply();
            }
        });
        $module.find('.js-kanban-toggle').off(namespace).on('click' + namespace, function () {
            var $column = $module.find('[data-column="' + $(this).attr('data-column') + '"]');
            $column.attr('data-expanded', $column.attr('data-expanded') === 'true' ? 'false' : 'true');
            renderColumn($column);
        });
        apply();
    }

    function initViewMode($module) {
        var $board = $module.find('.aocr-kanban__board');
        var $compact = $module.find('.aocr-kanban__compact');
        var mode = 'board';

        try {
            mode = window.sessionStorage.getItem('aocr-kanban-view') || 'board';
        } catch (ignore) {}

        function apply(next) {
            mode = next === 'compact' ? 'compact' : 'board';
            $board.prop('hidden', mode !== 'board');
            $compact.prop('hidden', mode !== 'compact');
            $module.find('[data-view-mode]').each(function () {
                $(this).toggleClass('is-active', $(this).attr('data-view-mode') === mode);
            });
            try {
                window.sessionStorage.setItem('aocr-kanban-view', mode);
            } catch (ignore) {}
        }

        $module.find('[data-view-mode]').off(namespace).on('click' + namespace, function () {
            apply($(this).attr('data-view-mode'));
        });
        apply(mode);
    }

    function initRefresh($module) {
        $module.find('.aocr-kanban__filters-toggle').off(namespace).on('click' + namespace, function () {
            var open = !$module.hasClass('is-filters-open');
            $module.toggleClass('is-filters-open', open);
            $(this).attr('aria-expanded', open ? 'true' : 'false')
                .text(open ? 'Ocultar filtros' : 'Mostrar filtros');
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

    function initResponsiveMode($module) {
        function apply() {
            var width = Math.round($module[0].getBoundingClientRect().width);
            $module.toggleClass('is-stage-layout', width > 0 && width <= 900);
        }

        if (window.ResizeObserver) {
            var observer = new window.ResizeObserver(apply);
            observer.observe($module[0]);
            $module.data('aocrKanbanResizeObserver', observer);
        } else {
            $(window).off('resize' + namespace).on('resize' + namespace, apply);
        }
        apply();
    }

    function init() {
        var $module = $(moduleSelector);
        if (!$module.length) {
            return;
        }

        initStageNavigation($module);
        initFilters($module);
        initViewMode($module);
        initRefresh($module);
        initTooltips($module);
        initResponsiveMode($module);
    }

    $(document).ready(init);

})(jQuery, window, document);
