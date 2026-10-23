# Observaciones de LV e informes — 22/10/2026

Referencia: `17. LV de pruebas (21 oct 2026).pdf`, proporcionado por el usuario.
Actualización de flujo: `18. LV de pruebas (22 oct 2026).pdf`, implementada a solicitud del usuario.

- LV identificada por inspección y estación, sin modificar documentos históricos firmados.
- Numeral 4 editable, sin completar datos institucionales supuestos. El usuario confirmó dejar el campo editable.
- Aeronaves inspeccionadas ingresadas por el inspector, sin precargar toda la flota.
- Eliminada la estación duplicada del numeral 6.
- 129-5 conserva los numerales 1, 2 y literales a, b; 129-6 usa «publica»; referencias LAR sustituidas por RDAC.
- 129-9 reúne visualmente la introducción y el literal a, manteniendo las claves de respuestas guardadas.
- Inspector puede preparar documentos finales cuando todos los informes de estaciones estén finalizados, firmados y satisfactorios. Las firmas institucionales siguen requiriendo aprobación.
- La firma de cada informe conserva el trabajo de su estación y notifica el avance al RT. Al completar todos los informes satisfactorios, se habilita generar CL y AOCR. El paquete se envía al Coordinador, quien revisa y remite a DCAV o devuelve al Inspector; ya no se remite cada informe directamente desde el Inspector a DCAV.
- DCAV y Dirección General disponen de enlaces de lectura a LV, informes firmados y documentos generados. Las decisiones de informe y firma de Condiciones corresponden a DCAV; la firma del AOCR requiere remisión formal de DCAV a Dirección General.

Validación inicial de LV: compilación de aplicación y vistas Razor; 162 pruebas correctas, 15 omitidas por depender de un entorno específico. La validación del nuevo flujo, incluida PostgreSQL temporal, consta a continuación.

Pendiente: cotejar la redacción completa del numeral 129-5 con el MIO original, no disponible entre los archivos locales revisados. Los cambios realizados corresponden a las observaciones explícitas del PDF. No se alteraron PDF históricos firmados ni se enviaron correos reales de prueba.

## Corrección de apertura de AOCR003

El log del 22/10/2026, a las 14:38–14:39, registra que las respuestas históricas de la LV no eran reconocidas por el catálogo actualizado. El controlador devuelve HTTP 503 cuando falla esta carga; esto explica el error de la captura.

Se admiten exclusivamente los códigos retirados `129-5-05`, `129-5-06` y `129-5-07` como antecedentes, sin mostrarlos entre los cuatro elementos vigentes. Al guardar un borrador, el DAO conserva sus respuestas desde la versión persistida dentro de la transacción. Los códigos desconocidos siguen siendo rechazados y las LV firmadas mantienen su bloqueo.

Validación de esta corrección: Release compilado; 66 pruebas correctas y 14 omitidas. Incluye regresión de apertura, conservación de evidencia histórica y rechazo de códigos desconocidos. DLL sincronizadas en `public/public1/bin`; pendiente publicar en IIS y comprobar con la sesión del inspector. No requiere cambios de esquema ni borrar respuestas.

## Flujo del documento 18 implementado

1. El detalle identifica la estación de cada LV y permite cambiar entre LV e informe de cada estación. La firma de una LV habilita su propio informe sin esperar a las otras estaciones.
2. Al firmar todos los informes satisfactorios, el Inspector dispone de **Generar CL y AOCR**. Los formularios y firmas institucionales conservan sus validaciones de pago, documentación y no conformidades.
3. **Enviar documentos al Coordinador** bloquea las versiones vigentes y crea avisos al Coordinador y al RT dentro de la transacción. No envía directamente el AOCR a DIRDAC.
4. La nueva bandeja **CL y AOCR por revisar** permite al Coordinador consultar LV, informes y documentos generados. Puede devolver el paquete con motivo obligatorio o remitirlo a DCAV. Se valida la versión para rechazar una revisión obsoleta.
5. DCAV revisa los informes, firma exclusivamente CL y remite formalmente el AOCR a DIRDAC. DIRDAC conserva la opción de devolverlo; la firma queda bloqueada hasta una nueva remisión de DCAV.
6. El seguimiento muestra generación, revisión coordinadora, revisión DCAV, CL firmado, remisión del AOCR, AOCR firmado y entrega encolada. También muestra las actuaciones y devoluciones. El seguimiento está disponible para Inspector, RT y autoridades desde sus pantallas autorizadas.
7. Cada hito genera el aviso correspondiente al RT. La entrega final existente a RT e Inspector se amplía con copias de ambos PDF firmados a las jefaturas configuradas de las estaciones involucradas. Los reintentos son idempotentes.

### Publicación y datos necesarios

- Ejecutar **antes de publicar** `scripts/sql/20261022_revision_paquete_coordinador.sql`. Amplía la restricción de estados sin alterar expedientes anteriores.
- Ejecutar `scripts/sql/20261022_destinatarios_jefaturas_aeropuertos.sql` y registrar los correos institucionales confirmados por estación en `aocr_jefatura_aeropuerto_correo`. No se inventaron direcciones. Los contactos siguen pendientes de confirmación del usuario.
- Publicar la carpeta local `public/public1` actualizada. No se realizó publicación remota ni modificación de la base del servidor en esta tarea.
- Si falta un contacto aeroportuario, se registra **ENTREGA_JEFATURAS_PENDIENTE**; no se marca esa distribución como completada ni se impide la disponibilidad de los documentos finales para RT e Inspector. El reintento de entrega final permite encolar los contactos configurados posteriormente sin duplicar las copias anteriores.

### Verificación del nuevo flujo

- Aplicación y vistas Razor compiladas.
- Selección de pruebas del flujo: **217 correctas y 15 omitidas**.
- `scripts/test_remision_dcav.ps1`: **105 comprobaciones correctas** en PostgreSQL local desechable, sin SMTP. Incluye estaciones independientes, bloqueo por informe pendiente, remisión al Coordinador, doble clic, versión obsoleta, rechazo de otro rol, devolución, reenvío, avisos al RT, copias a jefaturas, dos adjuntos y deduplicación.
- Ambas migraciones se ejecutaron sobre la base temporal; la ampliación de estados se probó dos veces para comprobar su repetibilidad.
- La ejecución de la suite general mostró fallos adicionales fuera de esta selección. No se declara aprobada la suite completa.
- Se conservaron las LV e informes firmados existentes. No se enviaron correos reales de prueba.
