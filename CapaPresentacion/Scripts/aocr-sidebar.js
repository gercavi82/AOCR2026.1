(function () {
    'use strict';

    var DEBUG = false;

    function logInfo(message) {
        if (DEBUG && window.console && typeof window.console.info === 'function') {
            window.console.info(message);
        }
    }

    function logError(message, error) {
        if (window.console && typeof window.console.error === 'function') {
            window.console.error(message, error || '');
        }
    }

    function isMobileViewport() {
        return window.matchMedia('(max-width: 991.98px)').matches;
    }

    var sidebarTrigger = null;

    function getFocusableElements(shell) {
        if (!shell) return [];
        return Array.prototype.slice.call(shell.querySelectorAll(
            'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )).filter(function (element) {
            return !element.hidden && element.getAttribute('aria-hidden') !== 'true' && element.offsetParent !== null;
        });
    }

    function focusSidebar(shell) {
        var focusable = getFocusableElements(shell);
        (focusable[0] || shell).focus();
    }

    function closeMobileSidebar() {
        if (!isMobileViewport()) {
            return;
        }

        document.body.classList.remove('sidebar-open');
        var backdrop = document.getElementById('aocrSidebarBackdrop');
        if (backdrop) backdrop.setAttribute('aria-hidden', 'true');
        if (sidebarTrigger && typeof sidebarTrigger.focus === 'function') sidebarTrigger.focus();
        var pushMenu = document.querySelector('[data-widget="pushmenu"]');
        if (pushMenu) pushMenu.setAttribute('aria-expanded', 'false');
    }

    function setGroupState(item, toggle, submenu, open) {
        if (!item || !toggle || !submenu) {
            return;
        }

        if (open) {
            item.classList.add('is-open', 'open');
            toggle.classList.add('is-open');
            toggle.setAttribute('aria-expanded', 'true');
            submenu.classList.add('is-open');
            submenu.setAttribute('aria-hidden', 'false');
            return;
        }

        item.classList.remove('is-open', 'open');
        toggle.classList.remove('is-open');
        toggle.setAttribute('aria-expanded', 'false');
        submenu.classList.remove('is-open');
        submenu.setAttribute('aria-hidden', 'true');
    }

    function closeOtherGroups(menuItems, exceptItem) {
        Array.prototype.forEach.call(menuItems, function (otherItem) {
            if (otherItem === exceptItem) {
                return;
            }

            var otherToggle = otherItem.querySelector('[data-menu-toggle]');
            var otherSubmenu = otherItem.querySelector('.aocr-submenu');
            setGroupState(otherItem, otherToggle, otherSubmenu, false);
        });
    }

    function installSidebarAccordion() {
        var shell = document.querySelector('[data-aocr-sidebar]');
        if (!shell) {
            return false;
        }

        if (shell.getAttribute('data-aocr-sidebar-ready') === 'true') {
            return true;
        }

        var menuItems = shell.querySelectorAll('[data-aocr-menu-item]');
        if (!menuItems.length) {
            return false;
        }

        shell.setAttribute('data-aocr-sidebar-ready', 'true');
        logInfo('[AOCR_SIDEBAR] Acordeón inicializado. Grupos=' + menuItems.length);

        Array.prototype.forEach.call(menuItems, function (item) {
            var toggle = item.querySelector('[data-menu-toggle]');
            var submenu = item.querySelector('.aocr-submenu');
            if (!toggle || !submenu) {
                return;
            }

            var hasActiveChild = !!submenu.querySelector('.aocr-submenu-link.active, .aocr-subnav-link.active');
            if (item.classList.contains('is-current') || hasActiveChild) {
                setGroupState(item, toggle, submenu, true);
            }

            toggle.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();

                var isOpen = item.classList.contains('is-open');
                if (isOpen) {
                    setGroupState(item, toggle, submenu, false);
                    return;
                }

                closeOtherGroups(menuItems, item);
                setGroupState(item, toggle, submenu, true);
            });
        });

        var searchInput = shell.querySelector('[data-aocr-sidebar-search]');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                var query = (searchInput.value || '').toLowerCase().trim();

                Array.prototype.forEach.call(menuItems, function (item) {
                    var haystack = (item.getAttribute('data-search-text') || '').toLowerCase();
                    var matches = !query || haystack.indexOf(query) !== -1;
                    item.hidden = !matches;

                    if (matches && query) {
                        var toggle = item.querySelector('[data-menu-toggle]');
                        var submenu = item.querySelector('.aocr-submenu');
                        setGroupState(item, toggle, submenu, true);
                    }

                    if (!matches) {
                        var hiddenToggle = item.querySelector('[data-menu-toggle]');
                        var hiddenSubmenu = item.querySelector('.aocr-submenu');
                        setGroupState(item, hiddenToggle, hiddenSubmenu, false);
                    }
                });
            });
        }

        shell.querySelectorAll('.aocr-submenu-link[href]').forEach(function (link) {
            link.addEventListener('click', function () {
                closeMobileSidebar();
            });
        });

        shell.querySelectorAll('.aocr-quick-action[href]').forEach(function (link) {
            link.addEventListener('click', function () {
                closeMobileSidebar();
            });
        });

        shell.querySelectorAll('.aocr-footer-link[href]').forEach(function (link) {
            link.addEventListener('click', function () {
                closeMobileSidebar();
            });
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Tab' && isMobileViewport() && document.body.classList.contains('sidebar-open')) {
                var focusable = getFocusableElements(shell);
                if (!focusable.length) {
                    event.preventDefault();
                    shell.focus();
                    return;
                }
                var first = focusable[0];
                var last = focusable[focusable.length - 1];
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first.focus();
                }
            }
            if (event.key === 'Escape' || event.keyCode === 27) {
                closeMobileSidebar();
            }
        });

        var pushMenu = document.querySelector('[data-widget="pushmenu"]');
        if (pushMenu) {
            pushMenu.setAttribute('aria-expanded', 'false');
            pushMenu.addEventListener('click', function () {
                if (!isMobileViewport()) return;
                sidebarTrigger = pushMenu;
                window.setTimeout(function () {
                    if (!document.body.classList.contains('sidebar-open')) return;
                    pushMenu.setAttribute('aria-expanded', 'true');
                    var backdropElement = document.getElementById('aocrSidebarBackdrop');
                    if (backdropElement) backdropElement.setAttribute('aria-hidden', 'false');
                    focusSidebar(shell);
                }, 50);
            });
        }

        var closeButton = shell.querySelector('[data-aocr-sidebar-close]');
        if (closeButton) {
            closeButton.addEventListener('click', function () {
                closeMobileSidebar();
            });
        }

        var backdrop = document.getElementById('aocrSidebarBackdrop');
        if (backdrop) {
            backdrop.addEventListener('click', function () {
                closeMobileSidebar();
            });
        }

        if (isMobileViewport()) closeMobileSidebar();

        var breakpoint = window.matchMedia('(max-width: 991.98px)');
        var handleBreakpointChange = function (event) {
            document.body.classList.remove('sidebar-open');
            if (backdrop) backdrop.setAttribute('aria-hidden', 'true');
            if (pushMenu) pushMenu.setAttribute('aria-expanded', 'false');
            if (!event.matches) shell.removeAttribute('aria-modal');
        };
        if (typeof breakpoint.addEventListener === 'function') breakpoint.addEventListener('change', handleBreakpointChange);
        else if (typeof breakpoint.addListener === 'function') breakpoint.addListener(handleBreakpointChange);

        return true;
    }

    function bootSidebarMenu() {
        try {
            installSidebarAccordion();
        } catch (error) {
            logError('[AOCR_SIDEBAR] Error inicializando acordeón.', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootSidebarMenu);
    } else {
        bootSidebarMenu();
    }
})();
