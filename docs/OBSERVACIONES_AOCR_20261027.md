# Observaciones AOCR y Condiciones — 27/10/2026

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
