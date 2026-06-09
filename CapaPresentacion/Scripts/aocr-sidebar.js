(function () {
    function logInfo(message, data) {
        if (window.console && typeof window.console.info === 'function') {
            window.console.info(message, data || '');
        }
    }

    function logWarn(message, data) {
        if (window.console && typeof window.console.warn === 'function') {
            window.console.warn(message, data || '');
        }
    }

    function logError(message, error) {
        if (window.console && typeof window.console.error === 'function') {
            window.console.error(message, error || '');
        }
    }

    function installSidebarMenu() {
        var body;
        var shell;
        var sidebar;
        var panel;
        var title;
        var subtitle;
        var content;
        var closeBtn;

        try {
            body = document.body;
            shell = document.querySelector('[data-aocr-sidebar]');
            sidebar = document.querySelector('.main-sidebar.aocr-sidebar');
            panel = document.getElementById('aocrSubnavPanel');
            title = document.getElementById('aocrSubnavTitle');
            subtitle = document.getElementById('aocrSubnavSubtitle');
            content = document.getElementById('aocrSubnavContent');
            closeBtn = document.getElementById('aocrSubnavClose');
        } catch (error) {
            logError('[AOCR_SUBNAV_INIT] No se pudo leer la estructura del sidebar.', error);
            return false;
        }

        if (!shell || !sidebar) {
            logWarn('[AOCR_SUBNAV_INIT] Sidebar no disponible todavia.');
            return false;
        }

        if (shell.getAttribute('data-aocr-sidebar-ready') === 'true') {
            return true;
        }

        var menuItems = shell.querySelectorAll('[data-aocr-menu-item]');
        if (!menuItems.length) {
            logWarn('[AOCR_SUBNAV_INIT] Sidebar sin menus renderizados.');
            return false;
        }

        var searchInput = shell.querySelector('[data-aocr-sidebar-search]');
        var sidebarScroll = shell.closest('.sidebar');

        if (!panel || !title || !subtitle || !content) {
            logWarn('[AOCR_SUBNAV_INIT] Panel secundario incompleto.');
            return false;
        }

        shell.setAttribute('data-aocr-sidebar-ready', 'true');
        logInfo('[AOCR_SUBNAV_INIT] Preparando panel secundario. Menus=' + menuItems.length);

        function getTemplate(button) {
            if (!button) {
                return null;
            }

            var menuId = button.getAttribute('data-aocr-subnav');
            if (!menuId) {
                return null;
            }

            return document.getElementById('aocr-subnav-' + menuId);
        }

        function clearOpenState() {
            Array.prototype.forEach.call(menuItems, function (item) {
                try {
                    item.classList.remove('open');

                    var button = item.querySelector('[data-aocr-subnav]');
                    if (button) {
                        button.classList.remove('is-open');
                        button.setAttribute('aria-expanded', 'false');
                    }

                    var mobileTemplate = item.querySelector('.aocr-submenu-template.mobile-visible');
                    if (mobileTemplate) {
                        mobileTemplate.setAttribute('aria-hidden', 'true');
                    }
                } catch (error) {
                    logError('[AOCR_SUBNAV_MENU_ERROR] Error limpiando estado de menu.', error);
                }
            });
        }

        function getActiveButton() {
            return shell.querySelector('[data-aocr-subnav].is-open');
        }

        function closeFlyout() {
            try {
                clearOpenState();

                panel.classList.remove('is-open');
                panel.setAttribute('aria-hidden', 'true');
                content.innerHTML = '';
                body.classList.remove('aocr-subnav-open');
            } catch (error) {
                logError('[AOCR_SUBNAV_MENU_ERROR] Error cerrando panel secundario.', error);
            }
        }

        function openDesktop(button) {
            try {
                var template = getTemplate(button);
                var item = button ? button.closest('[data-aocr-menu-item]') : null;

                if (!button || !template || !item) {
                    logWarn('[AOCR_SUBNAV_MENU_ERROR] Menu sin plantilla o boton invalido.', {
                        hasButton: !!button,
                        hasTemplate: !!template,
                        hasItem: !!item
                    });
                    return;
                }

                closeFlyout();

                content.innerHTML = template.innerHTML;
                title.textContent = button.getAttribute('data-title') || button.textContent.trim();
                subtitle.textContent = button.getAttribute('data-subtitle') || 'Seleccione una opcion';

                item.classList.add('open');
                button.classList.add('is-open');
                button.setAttribute('aria-expanded', 'true');
                panel.classList.add('is-open');
                panel.setAttribute('aria-hidden', 'false');
                body.classList.add('aocr-subnav-open');
            } catch (error) {
                logError('[AOCR_SUBNAV_MENU_ERROR] Error abriendo panel secundario.', error);
            }
        }

        Array.prototype.forEach.call(menuItems, function (item) {
            var button = item.querySelector('[data-aocr-subnav]');
            if (!button) {
                return;
            }

            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();

                try {
                    var alreadyOpen = item.classList.contains('open') || button.classList.contains('is-open');
                    if (alreadyOpen) {
                        closeFlyout();
                        return;
                    }

                    openDesktop(button);
                } catch (error) {
                    logError('[AOCR_SUBNAV_MENU_ERROR] Error manejando click de menu.', error);
                }
            });
        });

        if (searchInput) {
            searchInput.addEventListener('input', function () {
                try {
                    var query = (searchInput.value || '').toLowerCase().trim();
                    var firstMatch = null;

                    Array.prototype.forEach.call(menuItems, function (item) {
                        var haystack = (item.getAttribute('data-search-text') || '').toLowerCase();
                        var matches = !query || haystack.indexOf(query) !== -1;
                        item.hidden = !matches;

                        if (matches && !firstMatch) {
                            firstMatch = item;
                        }

                        if (!matches) {
                            item.classList.remove('open');
                        }
                    });

                    var activeButton = getActiveButton();
                    if (activeButton) {
                        var activeItem = activeButton.closest('[data-aocr-menu-item]');
                        if (activeItem && activeItem.hidden) {
                            closeFlyout();
                        }
                    }
                } catch (error) {
                    logError('[AOCR_SUBNAV_MENU_ERROR] Error filtrando menus del sidebar.', error);
                }
            });
        }

        document.addEventListener('click', function (event) {
            try {
                var insideSidebar = shell.contains(event.target);
                var insidePanel = panel.contains(event.target);

                if (!insideSidebar && !insidePanel) {
                    closeFlyout();
                }
            } catch (error) {
                logError('[AOCR_SUBNAV_MENU_ERROR] Error manejando click global.', error);
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' || event.keyCode === 27) {
                closeFlyout();
            }
        });

        panel.addEventListener('click', function (event) {
            event.stopPropagation();
        });

        if (closeBtn) {
            closeBtn.addEventListener('click', function (event) {
                event.preventDefault();
                closeFlyout();
            });
        }

        if (sidebarScroll) {
            sidebarScroll.addEventListener('scroll', function () {
                closeFlyout();
            }, { passive: true });
        }

        window.addEventListener('resize', function () {
            closeFlyout();
        });

        logInfo('[AOCR_SUBNAV_COUNTERS_START] Sidebar sin endpoint dinamico de contadores; se usan valores renderizados por servidor.');
        logInfo('[AOCR_SUBNAV_COUNTERS_OK] Contadores del sidebar disponibles por render Razor.');
        logInfo('[AOCR_SUBNAV] Panel secundario inicializado. Menus=' + menuItems.length);
        return true;
    }

    var sidebarInitAttempts = 0;
    var sidebarInitMaxAttempts = 40;
    var sidebarInitTimer = null;

    function bootSidebarMenu() {
        try {
            if (installSidebarMenu()) {
                if (sidebarInitTimer) {
                    window.clearTimeout(sidebarInitTimer);
                    sidebarInitTimer = null;
                }
                return;
            }
        } catch (error) {
            logError('[AOCR_SUBNAV_INIT] Error no controlado inicializando sidebar.', error);
        }

        sidebarInitAttempts += 1;
        if (sidebarInitAttempts >= sidebarInitMaxAttempts) {
            logWarn('[AOCR_SUBNAV] No se pudo inicializar el panel secundario del sidebar.');
            return;
        }

        sidebarInitTimer = window.setTimeout(bootSidebarMenu, 100);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootSidebarMenu);
    } else {
        bootSidebarMenu();
    }

    window.addEventListener('load', bootSidebarMenu);
})();
