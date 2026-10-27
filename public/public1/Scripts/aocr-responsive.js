(function () {
    'use strict';

    var ROOT_SELECTOR = '.aocr-main-wrapper';
    var initialized = false;
    var resizeTimer = null;

    function queryWithin(root, selector) {
        if (root === document) return document.querySelectorAll(ROOT_SELECTOR + ' ' + selector);
        return root.querySelectorAll ? root.querySelectorAll(selector) : [];
    }

    function wrapTables(root) {
        Array.prototype.forEach.call(queryWithin(root, 'table'), function (table) {
            if (table.closest('.table-responsive, .aocr-table-scroll, .dataTables_scroll, .aocr-pdf-document')) return;
            var wrapper = document.createElement('div');
            wrapper.className = 'aocr-table-scroll';
            wrapper.setAttribute('role', 'region');
            wrapper.setAttribute('aria-label', table.getAttribute('aria-label') || 'Tabla con desplazamiento horizontal');
            wrapper.setAttribute('tabindex', '0');
            table.parentNode.insertBefore(wrapper, table);
            wrapper.appendChild(table);
        });
    }

    function enhanceFileInputs(root) {
        Array.prototype.forEach.call(queryWithin(root, 'input[type="file"]'), function (input) {
            if (input.hasAttribute('aria-describedby')) return;
            var help = input.parentElement && input.parentElement.querySelector('.form-text, small, .help-block');
            if (!help) return;
            if (!help.id) help.id = 'aocr-file-help-' + Math.random().toString(36).slice(2, 9);
            input.setAttribute('aria-describedby', help.id);
        });
    }

    function enhanceIconButtons(root) {
        Array.prototype.forEach.call(queryWithin(root, 'a.btn, button'), function (control) {
            if (control.getAttribute('aria-label') || (control.textContent || '').trim()) return;
            var title = control.getAttribute('title');
            if (title) control.setAttribute('aria-label', title);
        });
    }

    function refreshPlugins() {
        if (window.jQuery && window.jQuery.fn && window.jQuery.fn.dataTable) {
            var api = window.jQuery.fn.dataTable.tables({ visible: true, api: true });
            if (api && api.columns) api.columns.adjust();
            if (api && api.responsive && api.responsive.recalc) api.responsive.recalc();
        }
        document.documentElement.style.setProperty('--aocr-viewport-height', window.innerHeight + 'px');
        window.dispatchEvent(new CustomEvent('aocr:viewportchange'));
    }

    function enhance(root) {
        wrapTables(root);
        enhanceFileInputs(root);
        enhanceIconButtons(root);
    }

    function boot() {
        if (initialized) return;
        initialized = true;
        enhance(document);
        refreshPlugins();

        var main = document.querySelector(ROOT_SELECTOR);
        if (main && window.MutationObserver) {
            new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    Array.prototype.forEach.call(mutation.addedNodes, function (node) {
                        if (node.nodeType === 1) enhance(node);
                    });
                });
            }).observe(main, { childList: true, subtree: true });
        }

        window.addEventListener('resize', function () {
            window.clearTimeout(resizeTimer);
            resizeTimer = window.setTimeout(refreshPlugins, 150);
        }, { passive: true });
        window.addEventListener('orientationchange', function () {
            window.setTimeout(refreshPlugins, 200);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot, { once: true });
    else boot();
}());
