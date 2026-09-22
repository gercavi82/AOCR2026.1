(function () {
    'use strict';

    function qs(selector, root) {
        return (root || document).querySelector(selector);
    }

    function setBusy(form, busy) {
        if (!form) {
            return;
        }

        form.classList.toggle('aocr-is-loading', !!busy);
        Array.prototype.forEach.call(form.querySelectorAll('button, input'), function (el) {
            el.disabled = !!busy;
        });
    }

    function showResult(message, ok, data) {
        var host = qs('#aocrFirmaResultado');
        if (!host) {
            return;
        }

        var html = '<div class="aocr-signature-result ' + (ok ? 'is-success' : '') + '">';
        html += '<strong>' + escapeHtml(message || (ok ? 'Operacion completada.' : 'No se pudo completar la accion.')) + '</strong>';
        if (data && data.rutaFirmada) {
            html += '<span>Ruta logica: ' + escapeHtml(data.rutaFirmada) + '</span>';
        }
        if (data && data.hash) {
            html += '<span>Hash: ' + escapeHtml(data.hash) + '</span>';
        }
        if (data && data.bytes) {
            html += '<span>Tamano: ' + String(data.bytes) + ' bytes</span>';
        }
        if (data && data.urlDescarga) {
            html += '<a class="aocr-btn aocr-btn-success" href="' + escapeAttribute(data.urlDescarga) + '">Descargar AOCR firmado</a>';
        }
        html += '</div>';
        host.innerHTML = html;
    }

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function escapeAttribute(value) {
        return escapeHtml(value).replace(/`/g, '&#96;');
    }

    function tokenFrom(form) {
        var token = qs('input[name="__RequestVerificationToken"]', form) || qs('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    function parseJson(response) {
        return response.text().then(function (text) {
            var payload = {};
            if (text) {
                try {
                    payload = JSON.parse(text);
                } catch (e) {
                    payload = { ok: false, message: text };
                }
            }
            if (!response.ok || payload.ok === false) {
                throw payload;
            }
            return payload;
        });
    }

    function installGenerate() {
        var form = qs('#frmGenerarPdfAocr');
        if (!form) {
            return;
        }

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (form.classList.contains('aocr-is-loading')) {
                return;
            }

            var page = qs('.aocr-signature-page');
            var url = page ? page.getAttribute('data-generar-url') : form.getAttribute('action');
            var solicitud = page ? page.getAttribute('data-solicitud-id') : '';
            var body = new FormData(form);
            if (!body.get('solicitudId') && solicitud) {
                body.append('solicitudId', solicitud);
            }

            setBusy(form, true);
            showResult('Generando PDF oficial AOCR...', true);

            fetch(url, {
                method: 'POST',
                body: body,
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenFrom(form)
                }
            })
                .then(parseJson)
                .then(function (payload) {
                    showResult(payload.message || 'PDF oficial AOCR generado correctamente.', true, payload.data);
                    window.setTimeout(function () {
                        window.location.reload();
                    }, 650);
                })
                .catch(function (payload) {
                    showResult(payload && payload.message ? payload.message : 'No se pudo generar el PDF oficial AOCR.', false);
                })
                .then(function () {
                    setBusy(form, false);
                });
        });
    }

    function installSignature() {
        var form = qs('#frmFirmaAocrInstitucional');
        if (!form) {
            return;
        }

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (form.classList.contains('aocr-is-loading')) {
                return;
            }

            var file = qs('input[name="certificadoDigital"]', form);
            var password = qs('input[name="passwordCertificado"]', form);
            if (!file || !file.files || !file.files.length) {
                showResult('Debe seleccionar el certificado digital .p12 o .pfx.', false);
                return;
            }

            var name = file.files[0].name || '';
            if (!/\.(p12|pfx)$/i.test(name)) {
                showResult('Solo se admiten certificados digitales .p12 o .pfx.', false);
                return;
            }

            if (!password || !password.value.trim()) {
                showResult('Debe ingresar la contrasena del certificado.', false);
                return;
            }

            var body = new FormData(form);
            setBusy(form, true);
            showResult('Firmando oficialmente el AOCR...', true);

            fetch(form.getAttribute('action'), {
                method: 'POST',
                body: body,
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenFrom(form)
                }
            })
                .then(parseJson)
                .then(function (payload) {
                    showResult(payload.message || 'AOCR firmada oficialmente por Direccion / DIRDAC.', true, payload.data);
                    window.setTimeout(function () {
                        window.location.reload();
                    }, 900);
                })
                .catch(function (payload) {
                    showResult(payload && payload.message ? payload.message : 'No se pudo firmar oficialmente el AOCR.', false);
                })
                .then(function () {
                    setBusy(form, false);
                });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        installGenerate();
        installSignature();
    });
}());
