(function (window, document) {
    'use strict';

    var registry = {};
    var modalId = 'aocrPdfViewerGlobalModal';
    var pdfjsLoadPromise = null;
    var dynamicImportFactory = null;

    function qs(root, selector) {
        return root ? root.querySelector(selector) : null;
    }

    function show(el, visible) {
        if (!el) return;
        if (visible) el.classList.remove('d-none');
        else el.classList.add('d-none');
    }

    function getPdfJs() {
        return window.pdfjsLib || window['pdfjs-dist/build/pdf'];
    }

    function logViewer(message, data) {
        if (!window.console || typeof window.console.log !== 'function') {
            return;
        }

        try {
            window.console.log('[PDF_VIEWER] ' + message, data || {});
        } catch (error) {
        }
    }

    function getViewerAssetBase() {
        if (window.AOCR_PDF_ASSET_BASE) {
            return String(window.AOCR_PDF_ASSET_BASE).replace(/\/?$/, '/');
        }

        var scripts = document.getElementsByTagName('script');
        for (var i = scripts.length - 1; i >= 0; i--) {
            var src = scripts[i].getAttribute('src') || '';
            var marker = '/Content/aocr-pdf-viewer/aocr-pdf-viewer.js';
            var markerIndex = src.indexOf(marker);
            if (markerIndex >= 0) {
                return src.substring(0, markerIndex + '/Content/aocr-pdf-viewer/'.length);
            }
        }

        var base = document.querySelector('base[href]');
        if (base && base.href) {
            return new URL('Content/aocr-pdf-viewer/', base.href).toString();
        }

        return new URL('Content/aocr-pdf-viewer/', window.location.origin + '/').toString();
    }

    function getPdfLibUrl() {
        return window.AOCR_PDF_LIB_URL || getViewerAssetBase() + 'pdf.min.js';
    }

    function getPdfWorkerUrl() {
        return window.AOCR_PDF_WORKER_URL || getViewerAssetBase() + 'pdf.worker.min.js';
    }

    function loadScript(url) {
        return new Promise(function (resolve, reject) {
            var existing = document.querySelector('script[data-aocr-pdfjs="true"]');
            if (existing) {
                if (getPdfJs()) {
                    resolve(getPdfJs());
                    return;
                }

                existing.addEventListener('load', function () { resolve(getPdfJs()); }, { once: true });
                existing.addEventListener('error', reject, { once: true });
                return;
            }

            var script = document.createElement('script');
            script.src = url;
            script.async = true;
            script.setAttribute('data-aocr-pdfjs', 'true');
            script.onload = function () { resolve(getPdfJs()); };
            script.onerror = function () { reject(new Error('No se pudo cargar pdf.js desde ' + url)); };
            document.head.appendChild(script);
        });
    }

    function appendCacheBuster(url) {
        if (!url || url === '#') {
            return url || '';
        }

        return url + (url.indexOf('?') >= 0 ? '&' : '?') + '_aocrViewerTs=' + encodeURIComponent(Date.now().toString());
    }

    function importPdfJsModule(moduleUrl) {
        if (!dynamicImportFactory) {
            dynamicImportFactory = Function('moduleUrl', 'return import(moduleUrl);');
        }
        return dynamicImportFactory(moduleUrl);
    }

    function ensurePdfJsLoaded() {
        var pdfjs = getPdfJs();
        if (pdfjs && pdfjs.getDocument) {
            setWorker();
            return Promise.resolve(pdfjs);
        }

        if (!pdfjsLoadPromise) {
            var libUrl = getPdfLibUrl();
            pdfjsLoadPromise = importPdfJsModule(libUrl).then(function (module) {
                return module && module.getDocument
                    ? module
                    : (module && module.default && module.default.getDocument ? module.default : null);
            }).catch(function () {
                return loadScript(libUrl);
            }).then(function (loaded) {

                if (!loaded || !loaded.getDocument) {
                    throw new Error('No se pudo inicializar pdf.js.');
                }

                window.pdfjsLib = loaded;
                setWorker();
                return loaded;
            }).catch(function (error) {
                pdfjsLoadPromise = null;
                if (error) {
                    error.aocrPdfJsLoad = true;
                }
                throw error;
            });
        }

        return pdfjsLoadPromise;
    }

    function setWorker() {
        var pdfjs = getPdfJs();
        if (pdfjs && pdfjs.GlobalWorkerOptions && !pdfjs.GlobalWorkerOptions.workerSrc) {
            pdfjs.GlobalWorkerOptions.workerSrc = getPdfWorkerUrl();
        }
    }

    function Viewer(el) {
        this.el = el;
        this.canvas = qs(el, '.aocr-pdf-canvas');
        this.ctx = this.canvas ? this.canvas.getContext('2d') : null;
        this.body = qs(el, '.aocr-pdf-body');
        this.loading = qs(el, '.aocr-pdf-loading');
        this.error = qs(el, '.aocr-pdf-error');
        this.errorMessage = qs(el, '.aocr-pdf-error-message');
        this.fallback = qs(el, '.aocr-pdf-fallback');
        this.pageInput = qs(el, '.aocr-pdf-page-input');
        this.pageTotal = qs(el, '.aocr-pdf-page-total');
        this.zoomLabel = qs(el, '.aocr-pdf-zoom-label');
        this.download = qs(el, '.aocr-pdf-download');
        this.printButton = qs(el, '.aocr-pdf-print');
        this.toolbarButtons = el.querySelectorAll('[data-aocr-pdf-action], .aocr-pdf-download');
        this.pdfDoc = null;
        this.pageNum = 1;
        this.scale = 1;
        this.fitWidth = true;
        this.rendering = false;
        this.pendingPage = null;
        this.pdfUrl = el.getAttribute('data-pdf-url') || '';
        this.downloadUrl = el.getAttribute('data-download-url') || this.pdfUrl;
        this.defaultErrorMessage = this.errorMessage ? this.errorMessage.textContent : 'No se pudo cargar el documento.';
        this.emptyMessage = el.getAttribute('data-empty-message') || 'Genere una vista previa o seleccione un PDF disponible para visualizar el documento.';
        this.bind();
    }

    Viewer.prototype.bind = function () {
        var self = this;
        this.el.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-aocr-pdf-action]');
            if (!btn) return;
            e.preventDefault();
            self.handle(btn.getAttribute('data-aocr-pdf-action'));
        });
        if (this.pageInput) {
            this.pageInput.addEventListener('change', function () {
                var n = parseInt(self.pageInput.value, 10);
                if (!isNaN(n)) self.queuePage(n);
            });
        }
        window.addEventListener('resize', function () {
            if (self.fitWidth && self.pdfDoc) self.queuePage(self.pageNum);
        });
    };

    Viewer.prototype.handle = function (action) {
        if (action === 'prev') this.queuePage(this.pageNum - 1);
        if (action === 'next') this.queuePage(this.pageNum + 1);
        if (action === 'zoom-in') this.setZoom(this.scale + 0.15, false);
        if (action === 'zoom-out') this.setZoom(this.scale - 0.15, false);
        if (action === 'fit-width') this.setZoom(1, true);
        if (action === 'reload') this.load(appendCacheBuster(this.pdfUrl));
        if (action === 'print') this.print();
    };

    Viewer.prototype.setZoom = function (scale, fitWidth) {
        if (!this.pdfDoc) return;
        this.fitWidth = !!fitWidth;
        this.scale = Math.max(0.45, Math.min(2.8, scale));
        this.queuePage(this.pageNum);
    };

    Viewer.prototype.updateToolbar = function () {
        if (this.pageInput) this.pageInput.value = this.pageNum;
        if (this.pageTotal) this.pageTotal.textContent = this.pdfDoc ? this.pdfDoc.numPages : '0';
        if (this.zoomLabel) this.zoomLabel.textContent = Math.round(this.scale * 100) + '%';
        if (this.download) this.download.setAttribute('href', this.downloadUrl || this.pdfUrl || '#');
        this.syncToolbarState();
    };

    Viewer.prototype.syncToolbarState = function () {
        var self = this;
        var hasUrl = !!(this.pdfUrl || this.downloadUrl);
        var hasPdfDoc = !!this.pdfDoc;

        if (this.pageInput) {
            this.pageInput.disabled = !hasPdfDoc;
            this.pageInput.max = hasPdfDoc ? this.pdfDoc.numPages : 1;
        }

        Array.prototype.forEach.call(this.toolbarButtons || [], function (button) {
            var action = button.getAttribute('data-aocr-pdf-action') || (button.classList.contains('aocr-pdf-download') ? 'download' : '');
            var enabled = true;

            if (action === 'download' || action === 'print' || action === 'reload') {
                enabled = hasUrl;
            } else {
                enabled = hasPdfDoc;
            }

            if (button.tagName === 'A') {
                if (!enabled) {
                    button.classList.add('is-disabled');
                    button.setAttribute('aria-disabled', 'true');
                    button.setAttribute('tabindex', '-1');
                    button.setAttribute('href', '#');
                } else {
                    button.classList.remove('is-disabled');
                    button.removeAttribute('aria-disabled');
                    button.removeAttribute('tabindex');
                    if (action === 'download') {
                        button.setAttribute('href', self.downloadUrl || self.pdfUrl || '#');
                    }
                }
                return;
            }

            button.disabled = !enabled;
        });
    };

    Viewer.prototype.load = function (url) {
        var self = this;
        this.pdfUrl = url || this.pdfUrl || this.el.getAttribute('data-pdf-url') || '';
        this.downloadUrl = this.el.getAttribute('data-download-url') || this.pdfUrl;
        this.pageNum = 1;
        this.pdfDoc = null;
        this.updateToolbar();
        show(this.loading, true);
        show(this.error, false);
        show(this.fallback, false);
        this.setErrorMessage('');

        if (this.canvas) this.canvas.classList.remove('d-none');
        if (this.fallback) this.fallback.removeAttribute('src');

        if (!this.pdfUrl) {
            this.showEmpty(this.emptyMessage);
            return;
        }

        logViewer('cargando_pdf', {
            viewerId: this.el.id || '',
            pdfUrl: this.pdfUrl,
            pdfJsUrl: getPdfLibUrl(),
            workerUrl: getPdfWorkerUrl()
        });

        ensurePdfJsLoaded().then(function (pdfjs) {
            if (!pdfjs || !pdfjs.getDocument || !self.canvas || !self.ctx) {
                self.useFallback();
                return null;
            }

            var task = pdfjs.getDocument({
                url: self.pdfUrl,
                withCredentials: true
            });

            return task.promise;
        }).then(function (pdfDoc) {
            if (!pdfDoc) {
                return;
            }
            self.pdfDoc = pdfDoc;
            self.updateToolbar();
            self.renderPage(1);
        }).catch(function (error) {
            logViewer('error_cargando_pdf', {
                viewerId: self.el.id || '',
                pdfUrl: self.pdfUrl,
                message: error && error.message ? error.message : ''
            });

            if (error && error.aocrPdfJsLoad) {
                self.useFallback();
                return;
            }

            self.fail(error);
        });
    };

    Viewer.prototype.showEmpty = function (message) {
        this.pdfDoc = null;
        this.pageNum = 1;
        show(this.loading, false);
        show(this.fallback, false);
        this.setErrorMessage(message || this.emptyMessage);
        show(this.error, true);
        if (this.canvas) this.canvas.classList.add('d-none');
        if (this.fallback) this.fallback.removeAttribute('src');
        this.updateToolbar();
    };

    Viewer.prototype.queuePage = function (num) {
        if (!this.pdfDoc) return;
        num = Math.max(1, Math.min(this.pdfDoc.numPages, num));
        if (this.rendering) {
            this.pendingPage = num;
            return;
        }
        this.renderPage(num);
    };

    Viewer.prototype.renderPage = function (num) {
        var self = this;
        if (!this.pdfDoc) return;
        this.rendering = true;
        this.pageNum = num;
        show(this.loading, true);
        show(this.error, false);

        this.pdfDoc.getPage(num).then(function (page) {
            var viewport = page.getViewport({ scale: 1 });
            var bodyWidth = self.body ? Math.max(280, self.body.clientWidth - 48) : viewport.width;
            var scale = self.fitWidth ? Math.min(2.2, bodyWidth / viewport.width) : self.scale;
            self.scale = scale;
            viewport = page.getViewport({ scale: scale });

            self.canvas.width = Math.floor(viewport.width);
            self.canvas.height = Math.floor(viewport.height);
            self.canvas.style.width = Math.floor(viewport.width) + 'px';
            self.canvas.style.height = Math.floor(viewport.height) + 'px';

            return page.render({
                canvasContext: self.ctx,
                viewport: viewport
            }).promise;
        }).then(function () {
            self.rendering = false;
            show(self.loading, false);
            self.updateToolbar();
            if (self.pendingPage !== null) {
                var pending = self.pendingPage;
                self.pendingPage = null;
                self.renderPage(pending);
            }
        }).catch(function () {
            self.rendering = false;
            self.fail();
        });
    };

    Viewer.prototype.useFallback = function () {
        if (!this.fallback) {
            this.fail();
            return;
        }
        if (this.canvas) this.canvas.classList.add('d-none');
        this.fallback.src = this.pdfUrl;
        show(this.fallback, true);
        show(this.loading, false);
        show(this.error, false);
        this.updateToolbar();
    };

    Viewer.prototype.setErrorMessage = function (message) {
        if (!this.errorMessage) return;
        this.errorMessage.textContent = message || this.defaultErrorMessage;
    };

    Viewer.prototype.resolveErrorMessage = function (error) {
        var text = error && error.message ? error.message : (typeof error === 'string' ? error : '');

        if (!text) {
            return this.defaultErrorMessage;
        }

        if (/Unexpected server response \(404\)|Missing PDF|not exist|no existe/i.test(text)) {
            return 'El documento ya no existe en el servidor. Regénere el informe o contacte al administrador.';
        }

        if (/Invalid PDF|invalid pdf|corrupt|format error/i.test(text)) {
            return 'El archivo disponible no es un PDF valido. Vuelva a generar el informe.';
        }

        return this.defaultErrorMessage;
    };

    Viewer.prototype.fail = function (error) {
        show(this.loading, false);
        this.setErrorMessage(this.resolveErrorMessage(error));
        show(this.error, true);
        show(this.fallback, false);
        if (this.canvas) this.canvas.classList.add('d-none');
        this.updateToolbar();
    };

    Viewer.prototype.print = function () {
        var url = this.downloadUrl || this.pdfUrl;
        if (!url) {
            this.showEmpty('No hay un PDF disponible para imprimir. Genere una vista previa primero.');
            return;
        }
        var frame = document.createElement('iframe');
        frame.style.position = 'fixed';
        frame.style.right = '0';
        frame.style.bottom = '0';
        frame.style.width = '1px';
        frame.style.height = '1px';
        frame.style.border = '0';
        frame.src = url;
        frame.onload = function () {
            try {
                frame.contentWindow.focus();
                frame.contentWindow.print();
            } catch (e) {
                window.open(url, '_blank', 'noopener');
            }
        };
        document.body.appendChild(frame);
        setTimeout(function () {
            if (frame.parentNode) frame.parentNode.removeChild(frame);
        }, 60000);
    };

    function init(root) {
        var scope = root || document;
        var viewers = scope.querySelectorAll('[data-aocr-pdf-viewer="true"]');
        Array.prototype.forEach.call(viewers, function (el) {
            if (!el.id) el.id = 'aocrPdfViewer_' + Math.random().toString(36).slice(2);
            if (!registry[el.id]) registry[el.id] = new Viewer(el);
            registry[el.id].load(el.getAttribute('data-pdf-url') || '');
        });
    }

    function ensureModal() {
        var modal = document.getElementById(modalId);
        if (modal) return modal;
        var html = '' +
            '<div class="modal fade aocr-pdf-modal" id="' + modalId + '" tabindex="-1" aria-hidden="true">' +
            '  <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable">' +
            '    <div class="modal-content">' +
            '      <div class="modal-header">' +
            '        <div><h5 class="modal-title">Vista previa del documento</h5><div class="text-muted small aocr-pdf-modal-subtitle"></div></div>' +
            '        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button>' +
            '      </div>' +
            '      <div class="modal-body">' +
            '        <div id="aocrPdfViewerModalInner" class="aocr-pdf-viewer" data-aocr-pdf-viewer="true" data-pdf-url="" data-allow-download="true" data-allow-print="true">' +
            '          <div class="aocr-pdf-header"><div class="aocr-pdf-title-wrap"><div class="aocr-pdf-title">Documento PDF</div><div class="aocr-pdf-help">Documento cargado mediante visor institucional AOCR.</div></div><span class="aocr-pdf-status">Vista previa</span></div>' +
            '          <div class="aocr-pdf-toolbar"><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="prev" title="Pagina anterior"><i class="fas fa-chevron-left"></i></button><span class="aocr-pdf-page-state"><input type="number" min="1" value="1" class="aocr-pdf-page-input" aria-label="Pagina actual" /><span>/</span><span class="aocr-pdf-page-total">0</span></span><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="next" title="Pagina siguiente"><i class="fas fa-chevron-right"></i></button><span class="aocr-pdf-toolbar-sep"></span><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="zoom-out" title="Reducir zoom"><i class="fas fa-search-minus"></i></button><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="fit-width" title="Ajustar al ancho"><i class="fas fa-arrows-alt-h"></i></button><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="zoom-in" title="Aumentar zoom"><i class="fas fa-search-plus"></i></button><span class="aocr-pdf-zoom-label">100%</span><span class="aocr-pdf-toolbar-spacer"></span><button type="button" class="aocr-pdf-btn" data-aocr-pdf-action="reload" title="Recargar documento"><i class="fas fa-sync-alt"></i></button><a class="aocr-pdf-btn aocr-pdf-download" href="#" target="_blank" rel="noopener" title="Descargar PDF"><i class="fas fa-download"></i></a><button type="button" class="aocr-pdf-btn aocr-pdf-print" data-aocr-pdf-action="print" title="Imprimir PDF"><i class="fas fa-print"></i></button></div>' +
            '          <div class="aocr-pdf-body"><div class="aocr-pdf-loading"><div class="aocr-pdf-spinner"></div><span>Cargando documento...</span></div><div class="aocr-pdf-error d-none"><i class="fas fa-exclamation-triangle"></i><span class="aocr-pdf-error-message">No se pudo cargar el documento. Verifique que el archivo exista o vuelva a generar la vista previa.</span></div><div class="aocr-pdf-canvas-wrap"><canvas class="aocr-pdf-canvas"></canvas></div><iframe class="aocr-pdf-fallback d-none" title="Documento PDF"></iframe></div>' +
            '        </div>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', html);
        return document.getElementById(modalId);
    }

    function open(options) {
        options = options || {};
        var modal = ensureModal();
        var viewerEl = document.getElementById('aocrPdfViewerModalInner');
        var title = options.title || 'Vista previa del documento';
        var subtitle = options.subtitle || '';
        var status = options.status || 'Vista previa';
        var url = options.url || options.pdfUrl || '';
        var downloadUrl = options.downloadUrl || url;
        qs(modal, '.modal-title').textContent = title;
        qs(modal, '.aocr-pdf-modal-subtitle').textContent = subtitle;
        qs(viewerEl, '.aocr-pdf-title').textContent = title;
        var subtitleEl = qs(viewerEl, '.aocr-pdf-subtitle');
        if (subtitleEl) subtitleEl.textContent = subtitle;
        qs(viewerEl, '.aocr-pdf-help').textContent = 'Documento cargado mediante visor institucional AOCR.';
        qs(viewerEl, '.aocr-pdf-status').textContent = status;
        viewerEl.setAttribute('data-pdf-url', url);
        viewerEl.setAttribute('data-download-url', downloadUrl);

        if (!registry[viewerEl.id]) registry[viewerEl.id] = new Viewer(viewerEl);
        var instance = registry[viewerEl.id];
        instance.load(url);

        if (window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(modal).show();
        } else {
            modal.style.display = 'block';
            modal.classList.add('show');
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        init(document);
        document.addEventListener('click', function (e) {
            var link = e.target.closest('.aocr-pdf-open');
            if (!link) {
                var candidate = e.target.closest('a[href]');
                var href = candidate ? candidate.getAttribute('href') : '';
                if (href && (href.indexOf('vistaPrevia=true') >= 0 || candidate.getAttribute('data-aocr-pdf') === 'true')) {
                    link = candidate;
                }
            }
            if (!link) return;
            var url = link.getAttribute('data-pdf-url') || link.getAttribute('href');
            if (!url) return;
            e.preventDefault();
            open({
                url: url,
                downloadUrl: link.getAttribute('data-download-url') || url,
                title: link.getAttribute('data-title') || link.getAttribute('title') || link.textContent.trim() || 'Vista previa del documento',
                subtitle: link.getAttribute('data-subtitle') || '',
                status: link.getAttribute('data-status') || 'Vista previa'
            });
        });
    });

    window.AOCRPdfViewer = {
        init: init,
        open: open,
        instances: registry
    };
})(window, document);
