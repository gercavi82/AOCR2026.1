# Observaciones AOCR y Condiciones — 27/10/2026

Actualización 28/10: el usuario confirmó que el AOCR debe conservar el consecutivo del trámite original. Esa regla reemplaza el contador independiente descrito en este registro del 27/10. Implementación y regularización de borradores: `docs/NUMERACION_AOCR_20261028.md`.

## Verificación de remisión al Coordinador

Consulta de solo lectura del intento de AOCR003 registrado a las 15:58:52: el paquete no quedó remitido. Los PDF permanecen desbloqueados, el proceso continúa en `PENDIENTE_REVISION_INFORME_DCAV` y no existen correos `*_REVISION_COORDINADOR` del expediente. El usuario 90 tiene correo configurado y roles activos `CoordinadorInspecciones` y `CoordinacionLegal`. Se verificaron mediante consultas y validadores de lectura los informes firmados, la asignación del Inspector, ausencia de no conformidades, integridad de archivos y resolución del RT y Coordinador. No se ejecutó la transición ni SMTP en el servidor.

No hay evidencia suficiente para atribuir el rechazo anterior a una causa concreta: el servicio descartaba la excepción técnica al devolver el error genérico. Se incorporó registro persistente y Trace con solicitud, inspección y referencia `ENVIO-COORD-{id}`, además de registrar el resultado confirmado y rechazos del controlador. El cliente ahora confirma el envío al Coordinador y renueva la versión del JavaScript; ya no anuncia remisión directa a DIRDAC/DCAV.

Prueba local con rol `CoordinadorInspecciones`: ambos avisos internos y ambos correos encolados correctamente; fallo forzado en el segundo correo revierte todo el paquete y sus notificaciones, y el reintento posterior funciona sin duplicar avisos. **127 comprobaciones PostgreSQL y 80 pruebas automatizadas correctas**. Compilación Release preparada en `public/public1` (DLL de Datos, Negocio y Presentación, script e Index).

**Pendiente de diagnóstico en servidor:** publicar la compilación preparada y revisar el mensaje exacto del siguiente intento; se solicitó al usuario el texto del aviso anterior. No se afirma que el envío real esté corregido o que los correos hayan sido entregados. La bandeja de revisión corresponde a **CL y AOCR por revisar** (`FirmaAocr/PaquetesCoordinador`); `Tecnico/Index` corresponde a asignación de Inspector y no sustituye esa bandeja.

El usuario confirmó que vio «mensaje enviado al coordinador». La nueva consulta mantuvo el mismo estado sin envío ni avisos; por ello esa confirmación no acredita una transacción completada. Se reforzó el cliente: exige `ok=true` y `estadoExpediente=DOCUMENTOS_FINALES_COORDINADOR`, muestra progreso sin estilo de éxito, conserva el resultado visible y evita repetir el envío confirmado. Cuatro escenarios JavaScript ejecutados correctamente: respuesta válida, estado distinto, respuesta incompleta y error de transacción. Copia local del script actualizada. La causa concreta de la discrepancia del intento anterior sigue pendiente; no se atribuye al SMTP ni se confirma una remisión real.

## Incidencia posterior: los PDF no mostraban la actualización

La consulta de solo lectura de los registros confirmó dos fallos al generar AOCR003 el 27/10, a las 15:51:59 y 15:52:05: PostgreSQL `23505`, índice `uq_aocr_doc_vigente_expediente_tipo`. Ambos documentos vigentes seguían apuntando a los archivos del 26/10. Los datos nuevos sí estaban guardados; la nueva versión del archivo no llegó a registrarse.

Corregido `AocrDocumentoGeneradoDAO.Registrar`: bloqueo por expediente, lectura de versión vigente, retiro de la anterior e inserción de la nueva en comandos secuenciales dentro de una transacción. El CTE anterior no garantizaba retirar la versión antes de comprobar el índice único durante la inserción. Se mantienen los índices y los archivos históricos; un error revierte la operación completa. No se permite reemplazar versiones enviadas o firmadas. La misma evidencia no duplica versiones. `FirmaAocrController` agrega cabeceras para evitar reutilizar una respuesta PDF en caché.

Validación de esta corrección: **125 comprobaciones PostgreSQL correctas**, con los dos índices únicos reales reproducidos en una base temporal; incluyen regeneración de ambos tipos, reintento, reversión y protección de enviados/firmados. **69 pruebas automatizadas correctas**, incluida la generación de seis PDF con plantillas reales. Compilación Release y copia local de `CapaDatos.dll` y `CapaPresentacion.dll` a `public/public1/bin` verificadas por hash. Evidencias: `.tmp/test-pdf-version-pg.log`, `.tmp/test-results/pdf-version.trx`.

Para que se vea en el servidor: publicar esas DLL de la misma compilación y, con el Inspector asignado, guardar los datos y pulsar **Regenerar PDF** en AOCR y Condiciones. Guardar datos no regenera automáticamente archivos existentes. Esta corrección no necesita SQL adicional ni eliminar índices. No se publicaron DLL ni se regeneraron documentos en producción durante esta revisión.

Referencia: `19. LV de pruebas (26 oct 2026).pdf` y solicitud pegada del usuario. Implementación local; no se publicaron cambios ni se modificó la base de producción.

## Diagnóstico y solución

Las plantillas reutilizaban datos del AOC para el permiso CNAC, del inspector para el RT y de contactos en Ecuador para la base del explotador. La tabla de aeronaves precargaba autorizaciones por estación. Se incorporaron datos documentales independientes, capturados por el inspector, validados en servidor y persistidos en la solicitud.

Se reutiliza `aocr_tbsolicitud.numero_aocr`. Las emisiones nuevas reciben `DGAC-129-OACI-000`; las renovaciones y modificaciones referencian la emisión del mismo explotador y conservan exactamente su número. Si hay varios originales, se exige seleccionar uno. Los números existentes no se recalculan. La referencia original queda en los datos documentales y los documentos conservan sus versiones existentes. FR3 conserva su numeración.

La inspección de solo lectura del esquema real confirmó que `aocr_tbcertificado.numero_certificado` tenía una restricción única global, incompatible con versiones del mismo reconocimiento. La migración elimina esa restricción concreta, conserva el índice de búsqueda y la unicidad del certificado por solicitud. La emisión original tiene un índice único para números nuevos y un disparador impide cambiar números ya asignados. No se reescriben filas históricas.

## Matriz de verificación

| Observación | Estado | Evidencia |
|---|---|---|
| Permiso CNAC, emisión y vencimiento independientes | Corregido | Guardado y recarga PostgreSQL; PDF real |
| Número original persistente para emisión, renovación y modificación | Corregido | DAO real, idempotencia, concurrencia, misma numeración en tres certificados |
| País de bandera en «Emitido por» | Corregido | Proyección y ambas plantillas |
| Gerente y RT con nombre, dirección, teléfono y correo propios | Corregido | Enlace MVC, validación, recarga y PDF con datos diferenciados |
| Explotador y representante legal Ecuador separados | Corregido | Recarga y PDF sin contactos del inspector ni dirección anterior |
| Vigencia AOC propia en declaración ES/EN | Corregido | Misma fecha comprobada en ambas declaraciones |
| Una sola marca de tipo de trámite | Corregido | Seis PDF, tres tipos, una marca por documento; tipo tomado de solicitud |
| CL: RT EAE, modelos sin matrículas y columnas por estación | Corregido | Plantilla real con modelos repetidos, matrículas centinela ausentes y estaciones UIO/GYE |
| R / NR / RC / ET / A independientes, sin autorización automática | Corregido | R y ET persisten; blanco, AUTORIZADO y estación ajena rechazados |
| Firmados e históricos protegidos | Corregido | Escritura rechazada y datos originales conservados en PostgreSQL local |
| Flujo de aprobación y firmas | Requiere validación institucional | Regresión automatizada correcta; no se firmó con certificados reales ni se enviaron correos |
| FR3 de extremo a extremo | Requiere validación en pruebas | Generador anterior sin cambios y contrato automatizado correcto; sin nueva operación financiera real |
| Publicación y migración del servidor | Pendiente | No ejecutadas en producción |

## Archivos modificados

- Persistencia y reglas: `CapaModelo/Certificado.cs`, `CapaModelo/Common/CertificadoAOCRViewModel.cs`, `CapaDatos/DAOs/CertificadoDAO.cs`, `CapaNegocio/Services/GeneracionAOCRService.cs`.
- Preparación: `CapaPresentacion/Models/CoordinacionJefaturaRevisionViewModel.cs`, `Controllers/FirmaAocrController.cs`, `Services/FirmaAocrServices.cs`, `Views/FirmaAocr/Index.cshtml`, nuevo `_DatosDocumentales.cshtml`, `Scripts/firma-aocr.js`.
- PDF principales: `Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml` y `AocrCondicionesLimitacionesPdf.cshtml`.
- Proyecciones heredadas: `Controllers/6_CertificadoController.cs`, `CoordinacionJefaturaController.cs`, `SolicitudAOCRController.cs`, `Views/Certificado/CertificadoAOCR.cshtml`.
- SQL: `scripts/sql/20261027_datos_documentales_certificado.sql`.
- Pruebas: `AOCR.Tests/Integration/Observaciones19PdfTests.cs`, inclusión en su proyecto y `scripts/test_remision_dcav.cs`.

## Validación reproducible

- 5 pruebas nuevas correctas: enlace MVC de campos anidados, validaciones y ejecución de las dos plantillas Razor reales para emisión, renovación y modificación, con generación mediante wkhtmltopdf.
- 246 pruebas de regresión correctas; 15 omitidas por dependencias de entorno. Es una selección del flujo, no una afirmación sobre toda la suite.
- 118 comprobaciones correctas contra PostgreSQL temporal local mediante `scripts/test_remision_dcav.ps1`; incluye migración repetida, almacenamiento, numeración, concurrencia y protección de firmados. Sin correo externo.
- Seis PDF de ejemplo en `output/pdf/observaciones-19`, una página cada uno; revisión visual de todas las páginas sin cortes ni superposición. Son datos ficticios, no documentos oficiales.
- Evidencias locales: `.tmp/test-results/observaciones19-final.trx`, `.tmp/test-results/regresion19.trx`, `.tmp/test19-postgres-final.log`.

## Instalación y límites

Aplicar primero la migración SQL en el entorno de pruebas con respaldo y después publicar la aplicación y sus vistas. Los expedientes existentes sin datos nuevos requieren completar el formulario antes de generar documentos; no se inventan contactos, fechas ni autorizaciones.

Se mantiene el plan de documentos vigente: modificación continúa con CL cuando así lo determina el flujo actual. La plantilla AOCR soporta los tres tipos, pero este cambio no obliga al flujo a emitir un AOCR adicional en modificación. Confirmar institucionalmente si se desea cambiar esa regla.

Las rutas heredadas de vista previa conservan compatibilidad; la asignación persistente se realiza en la generación institucional. Los documentos firmados históricos se descargan sin regenerarlos. Validar en pruebas institucionales la captura con datos largos, firma real, notificaciones y FR3 antes de producción.
