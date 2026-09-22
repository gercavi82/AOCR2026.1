const fs = require('node:fs'), vm = require('node:vm'), assert = require('node:assert/strict');
const source = fs.readFileSync('CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml', 'utf8');
const ids = ['archivoCertificadoRuido','archivoCertificadoAeronavegabilidad','archivoPoderRepresentante','archivoOtro'];
const inputs = Object.fromEntries(ids.map(id => [id, { id, files: [], getAttribute: () => '.pdf,.jpg,.jpeg,.png' }]));
class Transfer { constructor() { this.files = []; this.items = { add: f => this.files.push(f) }; } }
const sandbox = { document: { getElementById: id => inputs[id] }, DataTransfer: Transfer, actualizarResumenArchivos: () => {}, Swal: { fire: () => {} } };
vm.createContext(sandbox);
vm.runInContext(source.slice(source.indexOf('    function esCargaAcumulativa('), source.indexOf('    function toggleCaptura(')), sandbox);
vm.runInContext(source.slice(source.indexOf('    function asignarArchivosInput('), source.indexOf('    function inicializarDropzoneCarga(')), sandbox);
const a = {name:'xxx.pdf',size:100,lastModified:1}, b = {name:'xxx1.pdf',size:200,lastModified:2}, c = {name:'xxx2.pdf',size:300,lastModified:3};
for (const id of ids) {
 const input = inputs[id];
 input.files = [a]; assert.equal(sandbox.validarArchivo(id),true);
 input.files = [b]; assert.equal(sandbox.validarArchivo(id),true); assert.equal(input.files.length,2);
 input.files = [b]; sandbox.validarArchivo(id); assert.equal(input.files.length,2);
 input.files = [{name:'demasiado.pdf',size:11*1024*1024,lastModified:4}]; assert.equal(sandbox.validarArchivo(id),false); assert.equal(input.files.length,2);
 input.files = [{name:'invalido.exe',size:1,lastModified:4}]; assert.equal(sandbox.validarArchivo(id),false); assert.equal(input.files.length,2);
 sandbox.asignarArchivosInput(id,[c]); assert.equal(input.files.length,3);
 input.files = []; sandbox.validarArchivo(id); assert.equal(input.files.length,3);
 assert.deepEqual(Array.from(input.files, f => f.name), ['xxx.pdf','xxx1.pdf','xxx2.pdf']);
}
console.log('OK: 5, 6, 7 y Otros acumulan selecciones y arrastre; rechazar invalidos o cancelar conserva los anteriores; no duplica seleccion repetida.');
