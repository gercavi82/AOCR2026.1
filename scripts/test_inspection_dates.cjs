// npm install --prefix App_Data/inspection-dom-tests jsdom jquery@3.7.1
// node scripts/test_inspection_dates.cjs
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
const end = check => check.data('inspection-box').find('[data-range=fin]');
const date = check => check.data('inspection-box').find('[data-range=inicio]');
assert.equal(date(quito).val(), '2026-09-22');
assert.equal(manager.getEstaciones()[0].Version, 3);
assert.equal(date(gye).prop('disabled'), true);
gye.prop('checked', true).trigger('change');
assert.equal(date(gye).prop('required'), true);
assert.equal(manager.validateAllAirportDates(), false);
date(gye).val('2026-09-22');
end(gye).val('2026-09-21').trigger('input');
assert.equal(manager.validateAllAirportDates(), false);
end(gye).val('2026-09-25').trigger('input');
assert.equal(manager.validateAllAirportDates(), true);
assert.equal(manager.getEstaciones()[0].FechaInicio, '2026-09-22');
assert.equal(manager.getEstaciones()[1].FechaFin, '2026-09-25');
manager.prepareSubmission($('form'));
assert.equal($('[name="Estaciones[1].FechaFin"]').val(), '2026-09-25');
gye.prop('checked', false).trigger('change');
assert.equal(date(gye).val(), '');
assert.equal(date(gye).prop('required'), false);
assert.equal(date(gye).prop('disabled'), true);
manager.prepareSubmission($('form'));
assert.equal($('[name="Estaciones[1].FechaFin"]').length, 0);
gye.prop('checked', true).trigger('change');
assert.equal(manager.validateAllAirportDates(), false);
gye.prop('checked', false).trigger('change');
other.prop('checked', true).trigger('change');
date(other).val('2026-09-28'); end(other).val('2026-09-28');
assert.equal(manager.validateAllAirportDates(), false);
$('#ProvinciaInspeccion').val('Portoviejo').trigger('input');
assert.equal(manager.validateAllAirportDates(), true);
assert.equal(manager.getEstaciones()[1].EstacionNombre, 'Portoviejo');
assert.match($('#inspection-place-preview').text(), /Portoviejo28\/09\/2026/);
other.prop('checked', false).trigger('change');
assert.equal($('#ProvinciaInspeccion').val(), '');
manager.updateSaved([{EstacionCodigo:'UIO',Id:7,Version:4,Estado:'PROGRAMADA'}]);
assert.equal(manager.getEstaciones()[0].Version, 4);
assert.equal(manager.getEstaciones()[0].Estado, 'PROGRAMADA');
manager.init();
assert.equal($('.inspection-date-control').length, 3);
dom.window.close();
const readonly = page('<table id="inspection-place-preview"><tbody><tr><td>Quito 22/09/2026</td></tr></tbody></table>');
assert.match(readonly.window.document.body.textContent, /Quito 22\/09\/2026/);
readonly.window.close();
console.log('OK: fechas independientes, recuperacion, obligatoriedad, desmarcado, reeleccion, localidad y vista de lectura.');
