# Observaciones de LV e informes — 22/10/2026

Referencia: `17. LV de pruebas (21 oct 2026).pdf`, proporcionado por el usuario.

- LV identificada por inspección y estación, sin modificar documentos históricos firmados.
- Numeral 4 editable, sin completar datos institucionales supuestos. El usuario confirmó dejar el campo editable.
- Aeronaves inspeccionadas ingresadas por el inspector, sin precargar toda la flota.
- Eliminada la estación duplicada del numeral 6.
- 129-5 conserva los numerales 1, 2 y literales a, b; 129-6 usa «publica»; referencias LAR sustituidas por RDAC.
- 129-9 reúne visualmente la introducción y el literal a, manteniendo las claves de respuestas guardadas.
- Inspector puede preparar documentos finales cuando todos los informes de estaciones estén finalizados, firmados y satisfactorios. Las firmas institucionales siguen requiriendo aprobación.
- La firma del informe remite a DCAV y genera sus notificaciones. Existe reintento sin volver a firmar.
- DCAV y Dirección General disponen de enlaces de lectura a LV, informes firmados y documentos generados. Las decisiones de informe y firma de Condiciones corresponden a DCAV; la firma del AOCR requiere remisión formal de DCAV a Dirección General.

Validación: compilación de aplicación y vistas Razor; 162 pruebas correctas, 15 omitidas por depender de un entorno específico. La ejecución adicional de la base temporal no se completó y no constituye evidencia de validación transaccional.

Pendiente: cotejar la redacción completa del numeral 129-5 con el MIO original, no disponible entre los archivos locales revisados. Los cambios realizados corresponden a las observaciones explícitas del PDF. No se alteraron PDF históricos firmados ni se enviaron correos reales de prueba.

## Corrección de apertura de AOCR003

El log del 22/10/2026, a las 14:38–14:39, registra que las respuestas históricas de la LV no eran reconocidas por el catálogo actualizado. El controlador devuelve HTTP 503 cuando falla esta carga; esto explica el error de la captura.

Se admiten exclusivamente los códigos retirados `129-5-05`, `129-5-06` y `129-5-07` como antecedentes, sin mostrarlos entre los cuatro elementos vigentes. Al guardar un borrador, el DAO conserva sus respuestas desde la versión persistida dentro de la transacción. Los códigos desconocidos siguen siendo rechazados y las LV firmadas mantienen su bloqueo.

Validación de esta corrección: Release compilado; 66 pruebas correctas y 14 omitidas. Incluye regresión de apertura, conservación de evidencia histórica y rechazo de códigos desconocidos. DLL sincronizadas en `public/public1/bin`; pendiente publicar en IIS y comprobar con la sesión del inspector. No requiere cambios de esquema ni borrar respuestas.
