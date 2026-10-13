const assert = require('node:assert/strict');
const fs = require('node:fs');
const { JSDOM } = require('../App_Data/inspection-dom-tests/node_modules/jsdom');
const jquery = require('../App_Data/inspection-dom-tests/node_modules/jquery');
const script = fs.readFileSync('CapaPresentacion/Scripts/aocr-inspection-dates-inline.js', 'utf8');
function page(html) {
    const dom = new JSDOM(html, { runScripts: 'outside-only' });
    dom.window.jQuery = dom.window.$ = jquery(dom.window);
    dom.window.eval(script);
    dom.window.AocrInspectionDateManager.init();
    return dom;
}
const dom = page(`<form><div data-inspection-stations='[{"Id":7,"Version":3,"EstacionCodigo":"UIO","FechaInspeccion":"2026-09-22"}]'></div>
    <label><input type="checkbox" class="lugar-inspeccion-check" value="QUITO">Quito</label>
    <label><input type="checkbox" class="lugar-inspeccion-check" value="GUAYAQUIL">Guayaquil</label>
    <label><input type="checkbox" class="lugar-inspeccion-check" value="OTRA_PROVINCIA">Otra</label>
    <div id="divProvinciaInspeccion"><input id="ProvinciaInspeccion"></div>
    <table id="inspection-place-preview"><tbody></tbody></table><input id="FechasInspeccion"></form>`);
const $ = dom.window.$, manager = dom.window.AocrInspectionDateManager;
const quito = $('[value=QUITO]'), gye = $('[value=GUAYAQUIL]'), other = $('[value=OTRA_PROVINCIA]');
assert.equal($('input[type=date]').length, 0, 'El RT no ingresa fechas');
assert.equal(quito.prop('checked'), true);
assert.equal(manager.getEstaciones()[0].Version, 3);
assert.equal(manager.validateAllAirportDates(), true);
gye.prop('checked', true).trigger('change');
manager.prepareSubmission($('form'));
assert.equal($('[name="Estaciones[1].EstacionCodigo"]').val(), 'GYE');
assert.equal($('[name$=".FechaInicio"], [name$=".FechaFin"]').length, 0);
assert.equal($('#FechasInspeccion').val(), 'Por coordinar con el Inspector');
gye.prop('checked', false).trigger('change');
manager.prepareSubmission($('form'));
assert.equal($('[name="Estaciones[1].EstacionCodigo"]').length, 0);
other.prop('checked', true).trigger('change');
assert.equal(manager.validateAllAirportDates(), false);
$('#ProvinciaInspeccion').val('Portoviejo').trigger('input');
assert.equal(manager.validateAllAirportDates(), true);
assert.equal(manager.getEstaciones()[1].EstacionNombre, 'Portoviejo');
assert.match($('#inspection-place-preview').text(), /PortoviejoPor coordinar/);
other.prop('checked', false).trigger('change');
assert.equal($('#ProvinciaInspeccion').val(), '');
quito.prop('checked', false).trigger('change');
assert.equal(manager.validateAllAirportDates(), false);
manager.init();
assert.equal($('.inspection-date-control').length, 3);
dom.window.close();
const readonly = page('<table id="inspection-place-preview"><tbody><tr><td>Quito 22/09/2026</td></tr></tbody></table>');
assert.match(readonly.window.document.body.textContent, /Quito 22\/09\/2026/);
readonly.window.close();
console.log('OK: RT solicita lugares sin fechas; localidad, recuperacion, desmarcado y lectura historica.');
