// global.js - Script principal de la aplicación DGAC
(function ($) {
    'use strict';

    // Namespace global de la aplicación
    window.DGAC = window.DGAC || {};

    // Configuración
    DGAC.config = {
        sessionTimeout: 25 * 60 * 1000, // 25 minutos
        warningTime: 5 * 60 * 1000,     // 5 minutos
        apiBaseUrl: '/api/',
        debug: true
    };

    // Estado de la aplicación
    DGAC.state = {
        sessionActive: true,
        lastActivity: Date.now(),
        timers: {}
    };

    // Funciones de utilidad
    DGAC.util = {
        showNotification: function (message, type) {
            // Implementar notificaciones
            console.log(type.toUpperCase() + ':', message);
        },

        handleImageError: function (img) {
            if (img.src.indexOf('user.png') !== -1) {
                img.src = '/Content/imagenes/user-default.png';
            } else if (img.src.indexOf('user-default.png') !== -1) {
                // Crear avatar con iniciales
                var parent = img.parentNode;
                var alt = img.alt || 'Usuario';
                var fallback = document.createElement('div');
                fallback.className = 'avatar-fallback';
                fallback.textContent = alt.charAt(0).toUpperCase();
                fallback.style.backgroundColor = '#6c757d';
                fallback.style.color = 'white';
                fallback.style.width = img.width + 'px';
                fallback.style.height = img.height + 'px';
                fallback.style.borderRadius = '50%';
                fallback.style.display = 'flex';
                fallback.style.alignItems = 'center';
                fallback.style.justifyContent = 'center';
                fallback.style.fontWeight = 'bold';

                parent.replaceChild(fallback, img);
            }
        },

        extendSession: function () {
            $.ajax({
                url: DGAC.config.apiBaseUrl + 'session/extend',
                type: 'POST',
                success: function () {
                    DGAC.state.sessionActive = true;
                    DGAC.state.lastActivity = Date.now();
                    DGAC.util.showNotification('Sesión extendida', 'success');
                }
            });
        }
    };

    // Inicialización de la aplicación
    DGAC.init = function () {
        console.log('Inicializando aplicación DGAC');

        try {
            // Configurar manejadores de error para imágenes
            $(document).on('error', 'img', function () {
                DGAC.util.handleImageError(this);
            });

            // Inicializar gestión de sesión
            DGAC.initSessionManagement();

            // Inicializar componentes
            DGAC.initComponents();

            // Configurar AJAX global
            $.ajaxSetup({
                cache: false,
                statusCode: {
                    403: function () {
                        DGAC.util.showNotification('Acceso no autorizado', 'error');
                    },
                    404: function () {
                        DGAC.util.showNotification('Recurso no encontrado', 'error');
                    },
                    500: function () {
                        DGAC.util.showNotification('Error del servidor', 'error');
                    }
                }
            });

            console.log('Aplicación DGAC inicializada correctamente');

        } catch (error) {
            console.error('Error inicializando la aplicación:', error);
        }
    };

    // Inicialización de componentes
    DGAC.initComponents = function () {
        // Inicializar tooltips si Bootstrap está disponible
        if (typeof bootstrap !== 'undefined') {
            var tooltips = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltips.forEach(function (el) {
                new bootstrap.Tooltip(el);
            });
        }
    };

    // Gestión de sesión
    DGAC.initSessionManagement = function () {
        // Registrar actividad del usuario
        $(document).on('mousemove keydown click scroll', function () {
            DGAC.state.lastActivity = Date.now();
        });

        // Verificar sesión periódicamente
        setInterval(function () {
            var inactiveTime = Date.now() - DGAC.state.lastActivity;

            if (inactiveTime > DGAC.config.sessionTimeout && DGAC.state.sessionActive) {
                DGAC.util.showNotification('Su sesión ha expirado', 'warning');
                DGAC.state.sessionActive = false;
                // Redirigir a login
                window.location.href = '/Account/Logout';
            } else if (inactiveTime > (DGAC.config.sessionTimeout - DGAC.config.warningTime)) {
                // Mostrar advertencia de sesión
                if ($('#sessionModal').length) {
                    var modal = new bootstrap.Modal(document.getElementById('sessionModal'));
                    modal.show();
                }
            }
        }, 60000); // Verificar cada minuto
    };

    // Inicializar cuando jQuery esté listo
    $(document).ready(function () {
        DGAC.init();
    });

})(jQuery);