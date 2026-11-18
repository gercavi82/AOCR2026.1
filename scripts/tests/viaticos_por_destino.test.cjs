// Ejecutar: node scripts/tests/viaticos_por_destino.test.cjs
const fs = require('fs');
const vm = require('vm');
const assert = require('node:assert/strict');
const path = require('path');
const source = fs.readFileSync(path.join(__dirname, '../../CapaPresentacion/Views/OrdenRecaudacion/Nueva.cshtml'), 'utf8');
function extract(start, end) {
    return source.slice(source.indexOf('function ' + start + '('), source.indexOf('function ' + end + '('));
}
let selected = [], province = '', renders = 0;
const option = {
    length: 1, val: () => 7,
    data: key => ({ codigo: 'VIATICOS_INSPECTOR', nombre: 'Viáticos', valor: 80, porcentaje: 8 })[key],
    text: () => '', prop() { return this; }
};
const chain = {
    toggleClass() { return this; }, toggle() { return this; }, hide() { return this; },
    show() { return this; }, text() { return this; }, prop() { return this; }, val() { return ''; }
};
const context = {
    detalles: [{ ConceptoCodigo: 'INSPECCION_EXT' }], agregandoConceptoObligatorio: false,
    $: selector => selector === '#ProvinciaInspeccion' ? { val: () => province } : chain,
    toNumber: Number, esConceptoObligatorio: () => false, requiereInspeccionExterna: () => true,
    sincronizarLugaresYAeropuertos: () => selected,
    lugaresSoloQuitoLatacunga: places => places.length > 0 && places.every(x => ['QUITO', 'LATACUNGA'].includes(x)),
    obtenerOpcionConcepto: () => option, actualizarTotales() {}, actualizarEtiquetaCantidadSeleccionada() {},
    renderTabla() { assert(++renders < 50, 'Recursión al sincronizar'); context.actualizarReglasInspeccionSeguras(); }
};
vm.createContext(context);
vm.runInContext(extract('crearDetalleDesdeOpcion', 'agregarDetalleDesdeOpcion')
    + extract('actualizarReglasInspeccionSeguras', 'actualizarEtiquetaCantidadSeleccionada'), context);
function sync() {
    renders = 0;
    context.actualizarReglasInspeccionSeguras();
    return context.detalles.filter(x => x.ConceptoCodigo === 'VIATICOS_INSPECTOR');
}
selected = ['QUITO', 'LATACUNGA'];
assert.equal(sync().length, 0);
selected = ['QUITO', 'GUAYAQUIL', 'MANTA'];
let rows = sync();
assert.equal(rows.length, 2);
assert.equal(rows[0].LugarInspeccion, 'GUAYAQUIL');
assert.equal(rows[1].LugarInspeccion, 'MANTA');
rows[0].NumeroDiasInspeccion = 2;
rows[1].NumeroDiasInspeccion = 3;
rows = sync();
assert.equal(rows[0].Subtotal, 80);
assert.equal(rows[1].Subtotal, 160);
assert.equal((rows[0].TotalLinea + rows[1].TotalLinea).toFixed(2), '259.20');
selected = ['MANTA'];
rows = sync();
assert.equal(rows.length, 1);
assert.equal(rows[0].NumeroDiasInspeccion, 3);
selected = ['MANTA', 'OTRA_PROVINCIA'];
province = 'Azuay';
rows = sync();
assert.equal(rows.length, 2);
assert(rows[1].ConceptoNombre.includes('Azuay'));
province = 'Loja';
rows = sync();
assert(rows[1].ConceptoNombre.includes('Loja'));
assert.equal(rows[0].NumeroDiasInspeccion, 3);
selected = ['LATACUNGA'];
assert.equal(sync().length, 0);
console.log('OK: destinos, exclusiones, días independientes, totales, retiro y cambio de localidad.');
