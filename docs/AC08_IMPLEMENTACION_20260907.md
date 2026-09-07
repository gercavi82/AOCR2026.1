# AC-08: validación obligatoria de la LV

## Auditoría

Se revisaron el catálogo compartido de AC-07, el modelo, los lectores de formulario,
el servicio, todas las llamadas a guardar/finalizar/firmar, la reevaluación y el
formulario. La reevaluación crea un borrador, sin firma ni cierre.

La consulta de solo lectura a PostgreSQL encontró tres LV: una `LV_FIRMADA` y dos
`LV_COMPLETADA`. Los JSON contenían 180 registros con `SATISFACTORIO` e `IMPLEMENTADO`,
sin errores de sintaxis JSON. No se alteró la base de la aplicación.

Los valores reales del formulario son:

| Campo | Valores admitidos |
|---|---|
| Cumplimiento del requisito | `SATISFACTORIO`, `NO_SATISFACTORIO`, `NO_APLICABLE` |
| Implementación de la orientación | `IMPLEMENTADO`, `NO_IMPLEMENTADO`, `NO_APLICABLE` |

«Insatisfactorio» y «No aplica» son denominaciones funcionales; los códigos que
acepta esta LV son `NO_SATISFACTORIO` y `NO_APLICABLE`. No se agregaron alias nuevos
de resultados. Se toleran espacios exteriores y diferencias de mayúsculas.
Para estados de la LV se reconoce además el alias histórico `LV_COMPLETADA` como
`LV_COMPLETA`.

La brecha principal era que una observación permitía omitir los resultados.
También se aceptaban valores no vacíos sin comprobar el catálogo y algunas rutas
de persistencia confiaban exclusivamente en la validación previa del controlador.

## Regla aplicada

Cada orientación obligatoria debe tener cumplimiento e implementación válidos.
Las notas informativas del catálogo quedan excluidas. La obligatoriedad se obtiene
del catálogo del servidor, nunca de `EsNotaOrientacion` ni de la cantidad de elementos
enviada por el cliente. Omitir un elemento genera un pendiente.

Se conservan las reglas de observación existentes: `NO_SATISFACTORIO` exige una
observación en el requisito y `NO_IMPLEMENTADO` exige observación en esa orientación.
Ninguna observación sustituye un resultado. No se añadió una exigencia general de
comentario para `NO_APLICABLE` que no existía en el validador anterior.

El cumplimiento histórico por requisito puede recuperarse, pero una implementación
histórica por pregunta no acredita automáticamente todas sus orientaciones. Estas
deben tener respuestas explícitas antes de completar o firmar. Los datos históricos
no se reescriben, desfirman ni completan automáticamente.

## Controles

- `ValidadorListaVerificacion` devuelve todos los pendientes, sus códigos, motivos,
  errores de cabecera y cantidad, sin detenerse en el primer error.
- `ListaVerificacionCatalogo` es la única definición del catálogo. Se comparte con
  negocio y persistencia; `ListaVerificacionCatalogService` mantiene compatibilidad.
- El POST reconstruye las respuestas desde los códigos del catálogo. Campos
  manipulados de estado/obligatoriedad no autorizan el cierre.
- Guardar un avance incompleto conserva `LV_EN_PROCESO`. Forzar `LV_COMPLETA` o su
  alias con JSON incompleto se rechaza incluso llamando directamente al DAO.
- Finalizar y firmar bloquean la fila y validan el JSON persistido dentro de la
  transacción. Se impide que un objeto `Items` completo encubra JSON incompleto.
- `RegistrarFirmaTecnico` pasa por la misma validación definitiva. Se conservan los
  permisos y la inmutabilidad de AC-07, incluidos registros firmados o anulados.
- Las respuestas de validación incluyen `cantidadPendientes`, `pendientes` y
  `erroresCabecera`. Los rechazos de completitud usan HTTP 400; los conflictos de
  identidad/estado conservan HTTP 409 y los permisos HTTP 403.

El formulario muestra un contador, códigos de pendientes, filas resaltadas,
indicadores de texto y `aria-invalid`. La lista se actualiza al corregir datos.
Se bloquea el envío de cierre incompleto y se oculta la firma cuando la LV
finalizada no cumple la validación. El servidor aplica las mismas reglas aunque
JavaScript esté deshabilitado o se altere el formulario.

## Verificación

Compilar `AOCR.sln` con MSBuild/Debug y ejecutar:

```text
python scripts/test_ac07.py
node --test scripts/test_ac07_navigation.js scripts/test_ac08_validation.js
```

El primer script ejecuta AC-07 y AC-08 en PostgreSQL desechable, que elimina al
terminar. Incluye pruebas del lector real de campos POST sin JavaScript, cierre y
firma a través del DAO real, valores manipulados, JSON divergente, elementos
omitidos, catálogo falseado, permisos e inmutabilidad. Los proveedores de identidad
y auditoría permanecen aislados mediante dobles de prueba.

También se comprueba que cada elemento obligatorio tenga controles de cumplimiento
e implementación en la definición real del formulario Razor. Las pruebas JavaScript
cubren conteos, resaltado, corrección, observaciones y navegación AC-07.

La compilación incluye las vistas Razor. La advertencia preexistente de framework
de `itext.commons` no pertenece a AC-08.
Resultado final: 48 pruebas MSTest y 11 pruebas JavaScript aprobadas, sin fallos.

AC-08 no necesita una migración adicional. Para publicar se mantiene el requisito
de aplicar la migración AC-07 si aún no está instalada. No se desplegó la aplicación
ni se realizó una prueba HTTP de extremo a extremo con sesión y certificado reales.
