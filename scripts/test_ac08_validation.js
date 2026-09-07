const { test } = require('node:test');
const assert = require('node:assert/strict');
const validation = require('../CapaPresentacion/Scripts/aocr-lv-eae-validacion.js');
const values = { cumplimientos: ['SATISFACTORIO', 'NO_SATISFACTORIO', 'NO_APLICABLE'], implementaciones: ['IMPLEMENTADO', 'NO_IMPLEMENTADO', 'NO_APLICABLE'] };
const items = () => [1, 2, 3].map(n => ({ codigo: '129-' + n + '-01', pregunta: '129-' + n, cumplimiento: 'SATISFACTORIO', implementacion: 'IMPLEMENTADO', comentarios: '' }));
const evaluate = list => validation.evaluar(list, [{ nombre: 'Inspector', valor: 'Asignado' }], values);

test('Todos completos y NO_APLICABLE se aceptan con sus valores reales', () => {
    const list = items();
    assert.equal(evaluate(list).esValida, true);
    list[0].cumplimiento = list[0].implementacion = 'NO_APLICABLE';
    assert.equal(evaluate(list).cantidadPendientes, 0);
});
test('Una observación nunca sustituye un resultado vacío o manipulado', () => {
    for (const valor of ['', ' ', 'INSATISFACTORIO', 'NO APLICA', 'EVADIR_VALIDACION']) {
        const list = items(); list[0].cumplimiento = valor; list[0].comentarios = 'Comentario presente';
        const result = evaluate(list);
        assert.equal(result.esValida, false);
        assert.equal(result.cantidadPendientes, 1);
        assert.equal(result.pendientes[0].codigo, list[0].codigo);
    }
});
test('Varios pendientes se cuentan por elemento, no por campos fallidos', () => {
    const list = items();
    list[0].cumplimiento = list[0].implementacion = ''; list[1].implementacion = 'MANIPULADO';
    assert.equal(evaluate(list).cantidadPendientes, 2);
});
test('Resultados negativos requieren observación según requisito y orientación', () => {
    const list = items(); list[0].cumplimiento = 'NO_SATISFACTORIO';
    assert.equal(evaluate(list).esValida, false);
    list[0].comentarios = 'Evidencia del hallazgo';
    assert.equal(evaluate(list).esValida, true);
    list[1].implementacion = 'NO_IMPLEMENTADO';
    assert.equal(evaluate(list).cantidadPendientes, 1);
    list[1].comentarios = 'Pendiente de implementar';
    assert.equal(evaluate(list).esValida, true);
});
test('Cabecera vacía y catálogo vacío impiden el cierre', () => {
    assert.equal(validation.evaluar(items(), [{ nombre: 'Inspector', valor: '' }], values).esValida, false);
    assert.equal(evaluate([]).esValida, false);
});
test('Marca todas las filas pendientes, cuenta e identifica visualmente; se limpia al corregir', () => {
    const classes = new Set(), attrs = {}, badge = { textContent: '' }, summary = { classList: { toggle() {} } };
    const field = { setAttribute(key, value) { attrs[key] = value; }, removeAttribute(key) { delete attrs[key]; } };
    const row = {
        getAttribute() { return '129-1-01'; }, classList: { toggle(k, v) { v ? classes.add(k) : classes.delete(k); } },
        querySelector() { return badge; }, querySelectorAll() { return [field]; }
    };
    const form = { querySelectorAll() { return [row]; }, querySelector() { return summary; } };
    const list = items(); list[0].cumplimiento = '';
    validation.mostrar(form, evaluate(list), false);
    assert.equal(classes.has('lv-item-pendiente'), true);
    assert.equal(attrs['aria-invalid'], 'true');
    assert.match(badge.textContent, /129-1-01/);
    assert.match(summary.textContent, /1 ítem/);
    validation.mostrar(form, evaluate(items()), false);
    assert.equal(classes.size, 0);
    assert.equal(attrs['aria-invalid'], undefined);
    assert.equal(badge.textContent, '');
});
