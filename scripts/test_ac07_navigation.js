// Ejecutar: node --test scripts/test_ac07_navigation.js
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../CapaPresentacion/Scripts/aocr-lv-eae-flujo-automatico.js'), 'utf8');

function page(fetchResult) {
    const listeners = new Map();
    const node = () => ({
        attrs: {}, disabled: false,
        addEventListener(name, cb) { listeners.set(this, { ...listeners.get(this), [name]: cb }); },
        getAttribute(name) { return this.attrs[name]; },
        setAttribute(name, value) { this.attrs[name] = value; },
        removeAttribute(name) { delete this.attrs[name]; },
        querySelector() { return null; }, querySelectorAll() { return []; }
    });
    const form = node(), link = node(), input = node();
    form.action = '/AOCR/Inspeccion/GuardarListaVerificacionOperacionalEae';
    form.method = 'post';
    form.fields = { lvEstacionId: '1', lvCodigoLista: '10', lvSubmitAction: 'finalizar', finalizar: 'true', respuesta: 'Avance A', __RequestVerificationToken: 'token' };
    form.querySelectorAll = selector => selector === 'input, textarea, select' ? [input] : [];
    link.href = '/AOCR/Inspeccion/Detalle/800?estacionId=2';
    const requests = [], navigations = [], errors = [];
    const document = {
        readyState: 'complete', getElementById() { return null; }, querySelector() { return null; },
        querySelectorAll(selector) {
            if (selector === '.aocr-estaciones-nav a') return [link];
            return selector.includes('GuardarListaVerificacionOperacionalEae') ? [form] : [];
        }
    };
    const window = { location: { href: '/AOCR/Inspeccion/Detalle/800?estacionId=1', assign(url) { navigations.push(url); } },
        alert(message) { errors.push(message); }, validarComentariosLvEae() { return true; } };
    vm.runInNewContext(source, { window, document,
        FormData: class extends Map { constructor(f) { super(Object.entries(f.fields)); } },
        fetch(url, options) { requests.push({ url, options }); return fetchResult(); }
    });
    const fire = (target, name) => {
        const event = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
        listeners.get(target)[name](event);
        return event;
    };
    return { form, link, input, fire, requests, navigations, errors };
}
const response = (ok, payload) => Promise.resolve({ ok, headers: { get() { return 'application/json'; } }, json: () => Promise.resolve(payload) });
const settled = () => new Promise(resolve => setImmediate(resolve));

test('Cambiar estación guarda el avance A con identidad y token de A, sin finalizar', async () => {
    const p = page(() => response(true, { success: true, codigoLista: 10 }));
    p.fire(p.form, 'input');
    assert.equal(p.fire(p.link, 'click').defaultPrevented, true);
    assert.equal(p.requests[0].options.body.get('lvEstacionId'), '1');
    assert.equal(p.requests[0].options.body.get('lvCodigoLista'), '10');
    assert.equal(p.requests[0].options.body.get('respuesta'), 'Avance A');
    assert.equal(p.requests[0].options.body.get('__RequestVerificationToken'), 'token');
    assert.equal(p.requests[0].options.body.get('finalizar'), 'false');
    assert.equal(p.requests[0].options.body.get('lvSubmitAction'), 'guardar');
    assert.equal(p.input.disabled, true);
    await settled();
    assert.deepEqual(p.navigations, [p.link.href]);
});

test('Error de guardado conserva la estación y permite reintentar sin perder campos', async () => {
    const p = page(() => response(false, { success: false, message: 'Error de conexión' }));
    p.fire(p.form, 'change'); p.fire(p.link, 'click');
    await settled();
    assert.equal(p.navigations.length, 0);
    assert.equal(p.input.disabled, false);
    assert.equal(p.errors.length, 1);
    p.fire(p.link, 'click');
    assert.equal(p.requests.length, 2);
    await settled();
});

test('Sesión expirada o LV firmada no descartan el avance al navegar', async () => {
    for (const payload of [{ success: false, code: 401 }, { success: true, signed: true, finalized: true }]) {
        const p = page(() => response(true, payload));
        p.fire(p.form, 'input'); p.fire(p.link, 'click');
        await settled();
        assert.equal(p.navigations.length, 0);
        assert.equal(p.errors.length, 1);
        assert.equal(p.input.disabled, false);
    }
});

test('Doble clic no envía dos guardados ni permite finalizar durante el guardado', async () => {
    let resolve;
    const p = page(() => new Promise(r => { resolve = r; }));
    p.fire(p.form, 'input'); p.fire(p.link, 'click'); p.fire(p.link, 'click');
    assert.equal(p.requests.length, 1);
    assert.equal(p.fire(p.form, 'submit').defaultPrevented, true);
    resolve(await response(true, { success: true }));
    await settled();
    assert.equal(p.navigations.length, 1);
});

test('Navegación sin cambios no crea ni modifica una LV', () => {
    const p = page(() => { throw new Error('No debe guardar'); });
    assert.equal(p.fire(p.link, 'click').defaultPrevented, false);
    assert.equal(p.requests.length, 0);
});
