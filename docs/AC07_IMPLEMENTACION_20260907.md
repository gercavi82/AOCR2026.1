# AC-07: LV independiente por inspección y estación

## Auditoría previa

Se inspeccionaron el modelo C#, los DAO, los controladores, el formulario y el esquema
PostgreSQL configurado en `CapaPresentacion/Web.config`, mediante consultas de solo lectura.

El modelo real es:

```text
aocr_tbinspeccion.codigo_solicitud → trámite
aocr_tbsolicitud_estacion.solicitud_id → trámite
aocr_tbsolicitud_estacion.inspeccion_id → inspección (opcional)
aocr_tblv_operacional_eae.codigo_inspeccion → inspección
LV: cabecera, inspector, fecha, estado, items_json, PDF, firma y auditoría
```

La tabla LV consultada todavía no tenía `solicitud_id`, `estacion_id`, `tipo_lista`
ni `vigente`. Su índice de consulta era `(codigo_inspeccion, version DESC)`.
La tabla de estaciones sí tenía identificadores persistentes, inspector y relación
opcional con inspección. No se modificó la base de la aplicación.

Había una implementación parcial AC-07 y cambios locales previos, conservados.
Los problemas encontrados fueron:

- Estaciones virtuales históricas con ID negativo consultaban la LV general.
- Algunos enlaces de PDF perdían la estación y elegían la general o la primera.
- Un fallo de carga se ocultaba mostrando un borrador vacío.
- Había dos definiciones del mismo catálogo y carga de respuestas inconsistente.
- El guardado elegía la última LV sin contrastar el ID enviado, sin transacción.
- El índice propuesto de vigencia confundía distintas inspecciones del mismo trámite.
- Los nombres de PDF podían colisionar entre estaciones en el mismo segundo.
- El informe exigía una LV general aunque ya existieran LV por estación.

## Comportamiento implementado

El ámbito es `(codigo_inspeccion, estacion_id, tipo_lista)`, con una instancia vigente.
La inspección conserva el vínculo con el trámite. Solo se permite estación nula en
el caso histórico sin estaciones registradas. Los identificadores virtuales no se
utilizan como estaciones editables independientes.

La selección, guardado, finalización, firma, vista previa y descarga mantienen la
estación. Los formularios incluyen el ID de la LV; el servidor rechaza identidades
cruzadas y estaciones ajenas. Al cambiar de estación con avance pendiente se guarda
el borrador antes de navegar. Si falla, el formulario permanece abierto.

Se reutiliza `ListaVerificacionCatalogService`, tomando el catálogo ya existente;
no se inventaron preguntas ni respuestas para reemplazar listas vacías. Los nuevos
JSON contienen códigos, resultados y observaciones, sin repetir textos del catálogo.
Se admiten JSON históricos completos y respuestas antiguas por pregunta. Los JSON
corruptos o respuestas con códigos desconocidos se bloquean explícitamente, sin
reescribirlos como datos vacíos. Una orientación parcialmente respondida no completa
automáticamente sus orientaciones hermanas.

El DAO guarda en transacción, bloquea la inspección y la LV existente, verifica la
relación con solicitud/estación y conserva sus identificadores. Finalización y firma
usan actualizaciones condicionales. Una LV cerrada no se reemplaza por un borrador.
Una reinspección conserva las LV de la inspección anterior. Los archivos generados
incluyen un sufijo único; se conservan las rutas históricas.
Firmar una estación no adelanta el estado global del trámite ni abre el informe
mientras existan estaciones pendientes de firma.

Las escrituras exigen rol inspector y asignación a la inspección y a la estación
cuando tiene inspector específico. Se reutiliza la resolución institucional de
identidad. Los roles de consulta no reciben botones de edición/firma; las rutas
rechazan las operaciones igualmente. RT y Financiero no acceden a las LV.

## Migración

Antes de publicar, ejecutar **`scripts/sql/20260907_ac07_aislamiento_lv.sql`** y luego
`scripts/sql/20260907_ac07_aislamiento_lv_validate.sql` sobre la base correspondiente.
La nueva migración es autónoma y sustituye la migración parcial AC-07 del 03/09;
requiere que las tablas de inspecciones y estaciones ya existan.

La migración vincula un histórico solamente cuando existe una única estación
compatible y no hay una LV específica. Conserva respuestas, firmas y versiones;
marca como no vigentes las versiones antiguas del mismo ámbito. No clona LV.
Los históricos ambiguos quedan sin reasignar y se listan al final para revisión.
No deben atribuirse a una estación ni duplicarse sin evidencia de su origen.

Esta migración se probó en una base desechable, incluida su segunda ejecución.
**No se aplicó a la base de la aplicación ni se publicó el sistema.**

## Verificación reproducible

1. Compilar `AOCR.sln` con MSBuild de Visual Studio y configuración Debug.
2. Ejecutar `python scripts/test_ac07.py` (requiere `psycopg2` y permiso para crear
   bases de prueba). Crea `aocr_ac07_test_<uuid>`, aplica la migración dos veces,
   ejecuta AC-07 y la regresión AC-08, y elimina la base al terminar. No imprime
   credenciales. Resultado MSTest: `TestResults/ac07.trx`.
3. Ejecutar `node --test scripts/test_ac07_navigation.js`.

Las pruebas de integración usan persistencia y conexiones PostgreSQL reales; los
proveedores de asignación, solicitud y auditoría están aislados mediante dobles.
Cubren tres estaciones, avance parcial, recarga con nuevas conexiones/servicios,
modificación de LV3, firma exclusiva de LV2, catálogo compartido, permisos, creación
concurrente, identidad cruzada, reinspección y rollback ante fallo real de SQL.
Las pruebas JavaScript comprueban navegación después del guardado, errores,
sesión expirada, doble clic y conservación del contexto y token antifalsificación.

La compilación incluye Razor. Existe una advertencia de dependencias de framework
en `itext.commons` que ya aparecía antes de estos cambios.
Resultado: 43 pruebas MSTest AC-07/AC-08 y cinco pruebas JavaScript aprobadas.

No se ejecutó un cierre/inicio de sesión real en navegador ni firma criptográfica
con certificado institucional. Tras migrar/publicar, completar esa comprobación
con tres estaciones de prueba: avanzar en LV1, cambiar a LV2 y completarla, volver
a LV1 y guardar, cerrar sesión, regresar, modificar LV3 y firmar LV2. Comprobar que
los resultados, fechas, responsables y firmas de LV1/LV3 permanecen independientes.
