(function () {
    function installSidebarMenu() {
        var body = document.body;
        var shell = document.querySelector('[data-aocr-sidebar]');
        var sidebar = document.querySelector('.main-sidebar.aocr-sidebar');
        var panel = document.getElementById('aocrSubnavPanel');
        var title = document.getElementById('aocrSubnavTitle');
        var subtitle = document.getElementById('aocrSubnavSubtitle');
        var content = document.getElementById('aocrSubnavContent');
        var closeBtn = document.getElementById('aocrSubnavClose');

        if (!shell || !sidebar) {
            return;
        }

        var menuItems = shell.querySelectorAll('[data-aocr-menu-item]');
        if (!menuItems.length) {
            return;
        }

        var searchInput = shell.querySelector('[data-aocr-sidebar-search]');
        var sidebarScroll = shell.closest('.sidebar');

        if (!panel || !title || !subtitle || !content) {
            console.warn('[AOCR_SUBNAV] Panel secundario no encontrado.');
            return;
        }

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
            });
        }

        function getActiveButton() {
            return shell.querySelector('[data-aocr-subnav].is-open');
        }

        function closeFlyout() {
            clearOpenState();

            panel.classList.remove('is-open');
            panel.setAttribute('aria-hidden', 'true');
            content.innerHTML = '';
            body.classList.remove('aocr-subnav-open');
        }

        function openDesktop(button) {
            var template = getTemplate(button);
            var item = button ? button.closest('[data-aocr-menu-item]') : null;

            if (!button || !template || !item) {
                return;
            }

            closeFlyout();

            content.innerHTML = template.innerHTML;
            title.textContent = button.getAttribute('data-title') || button.textContent.trim();
            subtitle.textContent = button.getAttribute('data-subtitle') || 'Seleccione una opción';

            item.classList.add('open');
            button.classList.add('is-open');
            button.setAttribute('aria-expanded', 'true');
            panel.classList.add('is-open');
            panel.setAttribute('aria-hidden', 'false');
            body.classList.add('aocr-subnav-open');
        }

        Array.prototype.forEach.call(menuItems, function (item) {
            var button = item.querySelector('[data-aocr-subnav]');
            if (!button) {
                return;
            }

            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();

                var alreadyOpen = item.classList.contains('open') || button.classList.contains('is-open');
                if (alreadyOpen) {
                    closeFlyout();
                    return;
                }

                openDesktop(button);
            });
        });

        if (searchInput) {
            searchInput.addEventListener('input', function () {
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

            });
        }

        document.addEventListener('click', function (event) {
            var insideSidebar = shell.contains(event.target);
            var insidePanel = panel.contains(event.target);

            if (!insideSidebar && !insidePanel) {
                closeFlyout();
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

        console.info('[AOCR_SUBNAV] Panel secundario inicializado. Menús=' + menuItems.length);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', installSidebarMenu);
    } else {
        installSidebarMenu();
    }
})();