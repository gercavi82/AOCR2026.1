(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports) module.exports = api;
    if (root && root.document) {
        root.AOCRValidacionLv = api;
        var init = function () {
            root.document.querySelectorAll('form[data-lv-resultados]').forEach(function (form) {
                api.validar(form, false);
                form.addEventListener('input', function () { api.validar(form, false); });
                form.addEventListener('change', function () { api.validar(form, false); });
            });
        };
        if (root.document.readyState === 'loading') root.document.addEventListener('DOMContentLoaded', init);
        else init();
    }
})(typeof window === 'undefined' ? null : window, function () {
    function token(value) { return (value || '').trim().toUpperCase(); }
    function evaluar(items, cabecera, opciones) {
        var comentarios = {};
        items.forEach(function (item) {
            if ((item.comentarios || '').trim()) comentarios[item.pregunta] = true;
        });
        var pendientes = [];
        items.forEach(function (item) {
            var errores = [];
            var cumplimiento = token(item.cumplimiento), implementacion = token(item.implementacion);
            if (opciones.cumplimientos.indexOf(cumplimiento) < 0) errores.push('Seleccione un resultado de cumplimiento válido.');
            if (opciones.implementaciones.indexOf(implementacion) < 0) errores.push('Seleccione un resultado de implementación válido.');
            if (cumplimiento === 'NO_SATISFACTORIO' && !comentarios[item.pregunta]) errores.push('Ingrese una observación para el requisito.');
            if (implementacion === 'NO_IMPLEMENTADO' && !(item.comentarios || '').trim()) errores.push('Ingrese una observación para esta orientación.');
            if (errores.length) pendientes.push({ codigo: item.codigo, errores: errores });
        });
        var erroresCabecera = cabecera.filter(function (campo) { return !(campo.valor || '').trim(); })
            .map(function (campo) { return 'Complete ' + campo.nombre + '.'; });
        if (!items.length) erroresCabecera.push('No hay elementos obligatorios configurados.');
        var esValida = !pendientes.length && !erroresCabecera.length;
        return {
            esValida: esValida, cantidadPendientes: pendientes.length, pendientes: pendientes, erroresCabecera: erroresCabecera,
            message: esValida ? 'Todos los elementos obligatorios están completos.'
                : pendientes.length + ' ítem(s) incompleto(s). No puede completar, finalizar ni firmar la LV. ' + erroresCabecera.join(' ')
        };
    }
    function evaluarFormulario(form) {
        var values = {};
        form.querySelectorAll('.lv-server-field').forEach(function (field) {
            var code = field.getAttribute('data-item-code');
            if (!values[code]) values[code] = { codigo: code, pregunta: field.getAttribute('data-question-code') || code };
            values[code][field.getAttribute('data-field')] = field.value;
        });
        var cabecera = Array.prototype.map.call(form.querySelectorAll('[required]'), function (field) {
            return { nombre: field.id || field.name, valor: field.value };
        });
        var opciones = JSON.parse(form.getAttribute('data-lv-resultados'));
        return evaluar(Object.keys(values).map(function (code) { return values[code]; }), cabecera, opciones);
    }
    function mostrar(form, resultado, enfocar) {
        var pendientes = {};
        (resultado.pendientes || []).forEach(function (p) { pendientes[p.codigo] = p; });
        var primero;
        form.querySelectorAll('[data-lv-item-codes]').forEach(function (row) {
            var missing = (row.getAttribute('data-lv-item-codes') || '').split('|').filter(function (code) { return pendientes[code]; });
            row.classList.toggle('lv-item-pendiente', missing.length > 0);
            var badge = row.querySelector('[data-lv-pending-badge]');
            if (missing.length && !badge) {
                badge = row.ownerDocument.createElement('span');
                badge.setAttribute('data-lv-pending-badge', 'true');
                badge.className = 'lv-pending-badge';
                row.querySelector('td').appendChild(badge);
            }
            if (badge) badge.textContent = missing.length ? 'Pendiente: ' + missing.join(', ') : '';
            row.querySelectorAll('input, textarea').forEach(function (field) {
                if (missing.length) field.setAttribute('aria-invalid', 'true');
                else field.removeAttribute('aria-invalid');
            });
            if (!primero && missing.length) primero = row;
        });
        var resumen = form.querySelector('[data-lv-pendientes]');
        if (resumen) {
            resumen.textContent = resultado.message;
            resumen.classList.toggle('alert-warning', !resultado.esValida);
            resumen.classList.toggle('alert-success', !!resultado.esValida);
        }
        if (enfocar && primero) {
            primero.scrollIntoView({ block: 'center', behavior: 'smooth' });
            var input = primero.querySelector('input:not([type="hidden"]), textarea');
            if (input) input.focus({ preventScroll: true });
        }
    }
    function validar(form, enfocar) {
        var resultado = evaluarFormulario(form);
        mostrar(form, resultado, enfocar);
        return resultado;
    }
    return { evaluar: evaluar, evaluarFormulario: evaluarFormulario, mostrar: mostrar, validar: validar };
});
