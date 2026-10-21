const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../CapaPresentacion/Scripts/aocr-lv-eae-flujo-automatico.js'), 'utf8');
const fn = source.slice(source.indexOf('    function submitFormAsAjax('), source.indexOf('    function bindStationNavigation('));
let payload = { success: true, codigoLista: 2 };
let submitted;
const signedQuito = { value: 1 };
const identity = { value: 0 };
const form = {
    action: '/guardar', method: 'post',
    querySelector: () => null,
    querySelectorAll: () => [identity],
    getAttribute: () => null
};
const context = {
    document: { querySelector: () => null }, window: {},
    FormData: class {
        constructor() { this.data = new Map([['lvCodigoLista', identity.value]]); }
        set(k, v) { this.data.set(k, v); }
        has(k) { return this.data.has(k); }
        append(k, v) { this.data.set(k, v); }
    },
    fetch: async (url, options) => { submitted = options.body.data.get('lvCodigoLista'); return { ok: true }; },
    readResponsePayload: async () => payload,
    handleUnauthorizedPayload: () => false
};
vm.createContext(context);
vm.runInContext(fn, context);
(async () => {
    await context.submitFormAsAjax(form, null, false);
    assert.equal(submitted, 0);
    assert.equal(identity.value, 2);
    assert.equal(signedQuito.value, 1);
    await context.submitFormAsAjax(form, { name: 'lvSubmitAction', value: 'finalizar' }, false);
    assert.equal(submitted, 2, 'Finalizar debe enviar el ID devuelto por Guardar');
    payload = { success: false, codigoLista: 99, message: 'conflicto' };
    await assert.rejects(context.submitFormAsAjax(form, null, false));
    assert.equal(identity.value, 2, 'Un error no reemplaza la identidad de la estación');
    console.log('OK: Guardar y finalizar conservan identidad; otras LV y errores no la alteran.');
})().catch(error => { console.error(error); process.exitCode = 1; });
