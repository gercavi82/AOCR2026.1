/**
 * AOCR Cumulative File Upload Handler
 * 
 * Proporciona funcionalidad de carga acumulativa de archivos para inputs file en AOCR.
 * Permite agregar múltiples archivos en diferentes momentos sin que se reemplacen los anteriores.
 * 
 * Uso:
 * 1. Agregar atributos data a input[type="file"]:
 *    - data-cumulative-upload="true"
 *    - data-file-list-target="id" (opcional, para mostrar lista de archivos)
 *    - data-file-summary-target="id" (opcional, para mostrar resumen)
 * 
 * 2. Llamar a initCumulativeUploadHandlers() cuando el DOM está listo
 */

(function (window) {
    'use strict';

    // Estado global para gestionar archivos acumulativos
    var fileUploadState = new WeakMap();

    /**
     * Obtiene o crea el estado de acumulación de archivos para un input
     */
    function getUploadState(input) {
        if (!input) {
            return { files: [] };
        }

        var state = fileUploadState.get(input);
        if (!state) {
            state = { files: [] };
            fileUploadState.set(input, state);
        }

        return state;
    }

    /**
     * Crea un identificador único para un archivo (para detectar duplicados)
     * Basado en: nombre + tamaño + última modificación
     */
    function getFileIdentity(file) {
        if (!file) {
            return '';
        }

        return [
            (file.name || '').toLowerCase().trim(),
            file.size || 0,
            file.lastModified || 0
        ].join('|');
    }

    /**
     * Reconstruye la propiedad .files del input usando DataTransfer
     * Esto actualiza el input para que contenga todos los archivos acumulados
     */
    function rebuildInputFiles(input) {
        if (!input || typeof window.DataTransfer === 'undefined') {
            return;
        }

        var state = getUploadState(input);
        var dataTransfer = new window.DataTransfer();

        state.files.forEach(function (file) {
            try {
                dataTransfer.items.add(file);
            } catch (error) {
                console.warn('Error adding file to DataTransfer:', file.name, error);
            }
        });

        try {
            input.files = dataTransfer.files;
        } catch (error) {
            console.warn('Error updating input.files:', error);
        }
    }

    /**
     * Renderiza la lista visual de archivos seleccionados
     */
    function renderFileList(input) {
        if (!input) {
            return;
        }

        var listTargetId = input.getAttribute('data-file-list-target');
        if (!listTargetId) {
            return;
        }

        var listTarget = document.getElementById(listTargetId);
        if (!listTarget) {
            return;
        }

        var state = getUploadState(input);
        listTarget.innerHTML = '';

        if (!state.files || state.files.length === 0) {
            var emptyMsg = document.createElement('div');
            emptyMsg.className = 'text-muted small';
            emptyMsg.textContent = 'Sin archivos nuevos seleccionados.';
            listTarget.appendChild(emptyMsg);
            return;
        }

        // Crear tabla o lista de archivos
        var list = document.createElement('div');
        list.className = 'cumulative-file-list';

        state.files.forEach(function (file, index) {
            var row = document.createElement('div');
            row.className = 'cumulative-file-row d-flex justify-content-between align-items-center p-2 border-bottom';
            row.style.backgroundColor = index % 2 === 0 ? '#f8f9fa' : '#ffffff';

            // Información del archivo
            var info = document.createElement('div');
            info.className = 'cumulative-file-info flex-grow-1';

            var nameEl = document.createElement('span');
            nameEl.className = 'cumulative-file-name font-weight-500';
            nameEl.textContent = file.name || 'Archivo sin nombre';
            nameEl.title = file.name;

            var metaEl = document.createElement('span');
            metaEl.className = 'cumulative-file-meta text-muted small ml-2';
            metaEl.textContent = formatFileSize(file.size);

            info.appendChild(nameEl);
            info.appendChild(metaEl);

            // Botón eliminar
            var removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'btn btn-sm btn-outline-danger ml-2';
            removeBtn.innerHTML = '<i class="fas fa-trash mr-1"></i>Quitar';
            removeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                
                // Eliminar del estado
                state.files.splice(index, 1);
                
                // Reconstruir y re-renderizar
                rebuildInputFiles(input);
                renderFileList(input);
                updateFileSummary(input);
            });

            row.appendChild(info);
            row.appendChild(removeBtn);
            list.appendChild(row);
        });

        listTarget.appendChild(list);
    }

    /**
     * Actualiza el resumen de archivos seleccionados
     */
    function updateFileSummary(input) {
        if (!input) {
            return;
        }

        var summaryTargetId = input.getAttribute('data-file-summary-target');
        if (!summaryTargetId) {
            return;
        }

        var summaryTarget = document.getElementById(summaryTargetId);
        if (!summaryTarget) {
            return;
        }

        var state = getUploadState(input);
        var existingSummary = summaryTarget.getAttribute('data-existing-summary') || '';
        
        if (!state.files || state.files.length === 0) {
            summaryTarget.textContent = existingSummary;
            return;
        }

        var newFileNames = state.files.map(function (f) { return f.name; }).join(', ');
        var newSummary = state.files.length > 1
            ? 'Nuevos archivos seleccionados: ' + newFileNames
            : 'Nuevo archivo seleccionado: ' + newFileNames;

        summaryTarget.textContent = existingSummary
            ? existingSummary + ' | ' + newSummary
            : newSummary;
    }

    /**
     * Acumula archivos nuevamente seleccionados sin reemplazar los anteriores
     */
    function accumulateFiles(input) {
        if (!input || !input.files) {
            return;
        }

        var state = getUploadState(input);
        var existingIdentities = {};

        // Registrar identidades existentes
        state.files.forEach(function (file) {
            existingIdentities[getFileIdentity(file)] = true;
        });

        // Agregar nuevos archivos (evitando duplicados)
        var newFilesCount = 0;
        var duplicatesCount = 0;

        Array.prototype.slice.call(input.files).forEach(function (file) {
            var identity = getFileIdentity(file);
            if (identity && !existingIdentities[identity]) {
                state.files.push(file);
                existingIdentities[identity] = true;
                newFilesCount++;
            } else if (identity && existingIdentities[identity]) {
                duplicatesCount++;
            }
        });

        // Mostrar advertencia de duplicados si es necesario
        if (duplicatesCount > 0) {
            var duplicateMsg = duplicatesCount === 1
                ? 'Se ignoró 1 archivo duplicado.'
                : 'Se ignoraron ' + duplicatesCount + ' archivos duplicados.';
            
            showNotification(duplicateMsg, 'warning', input);
        }

        // Reconstruir y re-renderizar
        rebuildInputFiles(input);
        renderFileList(input);
        updateFileSummary(input);

        // Limpiar el input para que pueda seleccionar los mismos archivos nuevamente
        input.value = '';
    }

    /**
     * Limpia todos los archivos acumulados
     */
    function clearUploadState(input) {
        if (!input) {
            return;
        }

        var state = getUploadState(input);
        state.files = [];
        input.value = '';
        rebuildInputFiles(input);
        renderFileList(input);
        updateFileSummary(input);
    }

    /**
     * Formatea el tamaño del archivo en unidades legibles
     */
    function formatFileSize(bytes) {
        if (!bytes || bytes === 0) return '0 B';
        
        var units = ['B', 'KB', 'MB', 'GB'];
        var size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.length - 1) {
            size /= 1024;
            unitIndex++;
        }

        return Math.round(size * 100) / 100 + ' ' + units[unitIndex];
    }

    /**
     * Muestra una notificación al usuario
     */
    function showNotification(message, type, nearElement) {
        type = type || 'info';
        
        var alertClass = {
            'success': 'alert-success',
            'warning': 'alert-warning',
            'danger': 'alert-danger',
            'info': 'alert-info'
        }[type] || 'alert-info';

        var alert = document.createElement('div');
        alert.className = 'alert ' + alertClass + ' alert-dismissible fade show';
        alert.role = 'alert';
        alert.innerHTML = message + 
            '<button type="button" class="close" data-dismiss="alert" aria-label="Close">' +
            '<span aria-hidden="true">&times;</span></button>';

        if (nearElement && nearElement.parentNode) {
            nearElement.parentNode.insertBefore(alert, nearElement);
        } else {
            document.body.insertBefore(alert, document.body.firstChild);
        }

        // Auto-cerrar después de 5 segundos
        setTimeout(function () {
            if (alert.parentNode) {
                alert.remove();
            }
        }, 5000);
    }

    /**
     * Inicializa los manejadores de carga acumulativa
     */
    function initCumulativeUploadHandlers() {
        // Encontrar todos los inputs file con atributo data-cumulative-upload
        var fileInputs = document.querySelectorAll('input[type="file"][data-cumulative-upload="true"]');

        fileInputs.forEach(function (input) {
            // Evento change: acumular archivos
            input.addEventListener('change', function () {
                accumulateFiles(input);
            });

            // Renderizar lista inicial
            renderFileList(input);
            updateFileSummary(input);

            // Exponer API pública en el elemento
            input.clearCumulativeFiles = function () {
                clearUploadState(input);
            };

            input.getAccumulatedFiles = function () {
                return getUploadState(input).files.slice();
            };

            input.getFileCount = function () {
                return getUploadState(input).files.length;
            };
        });
    }

    /**
     * Versión jQuery para compatibilidad
     */
    if (typeof jQuery !== 'undefined') {
        jQuery.fn.initCumulativeUpload = function () {
            this.each(function () {
                var input = this;
                if (input.type === 'file') {
                    input.setAttribute('data-cumulative-upload', 'true');
                }
            });
            initCumulativeUploadHandlers();
            return this;
        };

        jQuery.fn.clearCumulativeFiles = function () {
            this.each(function () {
                if (this.clearCumulativeFiles && typeof this.clearCumulativeFiles === 'function') {
                    this.clearCumulativeFiles();
                }
            });
            return this;
        };

        jQuery.fn.getAccumulatedFiles = function () {
            if (this.length > 0 && this[0].getAccumulatedFiles && typeof this[0].getAccumulatedFiles === 'function') {
                return this[0].getAccumulatedFiles();
            }
            return [];
        };
    }

    // Exponer API pública
    window.AOCRCumulativeUpload = {
        init: initCumulativeUploadHandlers,
        getState: getUploadState,
        clearState: clearUploadState,
        rebuildFiles: rebuildInputFiles,
        formatSize: formatFileSize
    };

    // Auto-inicializar cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            initCumulativeUploadHandlers();
        });
    } else {
        // DOM ya está listo
        initCumulativeUploadHandlers();
    }

})(window);
