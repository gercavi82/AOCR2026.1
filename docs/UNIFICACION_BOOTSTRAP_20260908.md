# Unificacion visual AOCR

## Diagnostico previo

- Rama: `feat/flujo-institucional_v2`.
- HEAD inicial: `082e9267dc8eb80f0d6e2dbb67798a9ad585e7ca`.
- Worktree inicial limpio (los ajustes de la conversacion anterior ya forman parte de HEAD).
- Build Debug completo aprobado. El primer intento coincidio con VSTest y fallo por DLL bloqueada; repetido secuencialmente, termino correctamente.
- Base VSTest: 754 pruebas, 739 aprobadas, 15 omitidas. Evidencia: `TestResults/Unificacion/unificacion-baseline.trx`.
- CSS Bootstrap por CDN 5.3.0; JS por CDN 5.3.0-alpha1; archivos locales CSS/Bundle JS 5.3.8.
- AdminLTE CSS por CDN 3.2 y JS local 3.1.0. AdminLTE 3 requiere Bootstrap 4: https://adminlte.io/docs/3.2/dependencies.html.
- jQuery local 3.6.4 y fallback de la misma version.
- BundleConfig registra rutas inexistentes de AdminLTE y plugins; el layout no usa dichos bundles, pero Login usa el bundle auth.

La causa original era `html body.aocr-body table.table tbody td a` en `Content/aocr-datatables.css:361`: `color: #007bff !important`, especificidad (0,2,6). Un enlace con `btn btn-primary` quedaba azul sobre azul. HEAD ya excluye `.btn`, pero conserva parches globales que redefinen todas las variantes con `!important` al final de `aocr-contrast.css`. Esos parches impiden que Bootstrap controle sus estados.

Orden inicial: FontAwesome, Bootstrap CDN, AdminLTE completo, DataTables, Site, institucional, datatables AOCR, responsive, documentos, modales, sidebar, contrast-fix, headers, visor PDF, Styles de pagina, contrast global. JS: jQuery, DataTables, visor, validacion, Bootstrap alfa, utilidades, loader asincrono SweetAlert/AdminLTE/site/app/sidebar, Scripts de pagina.

Inventario completo de recursos Razor, estilos embebidos y atributos legacy: `UNIFICACION_BOOTSTRAP_INVENTARIO_20260908.json`.

## Impacto y estrategia antes de implementar

Se conserva Bootstrap 5, ya usado por los controladores visuales Modal, Tooltip y DataTables Bootstrap5. Se unifica a los archivos locales 5.3.8 existentes, sin migracion de version mayor.

La busqueda de consumidores de AdminLTE encontro solamente `data-widget="pushmenu"` en el layout y su integracion con `aocr-sidebar.js`. No hay consumidores de CardWidget, Treeview, Select2, bootstrap-select, small-box o info-box en las vistas activas. Se conservaran los estilos de estructura y sidebar de AdminLTE como un recurso limitado; su Bootstrap 4 embebido y sus plugins JS no se cargaran. El toggle del sidebar pasa al script AOCR que ya gestiona cierre, foco y acordeon. Los archivos originales de terceros permanecen en el repositorio.

Archivos afectados previstos: layout, BundleConfig, CSS globales y shell, script de sidebar y vistas concretas con atributos Bootstrap4. Las plantillas PDF, permisos, consultas, controllers y servicios quedan fuera de esta tarea. Se revisaran los atributos legacy por archivo; no se alteran modelos ni condiciones Razor.

Riesgos: estilos locales de vistas con selectores generales; diferencias de espaciado legacy; apertura de modales y sidebar movil. Se requieren pruebas de cascada, build, AC10/AC11, comprobacion del toggle y verificacion visual en las siete resoluciones. Sin navegador conectado no se certificaran pruebas responsive, consola o navegacion autenticada.

## Evidencia final

Pendiente de completar al finalizar la implementacion y sus verificaciones.
