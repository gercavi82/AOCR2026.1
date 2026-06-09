# Checklist de Regresion AOCR - Estados y Subsanacion 2026-06-02

## Objetivo

Validar que los estados canonicos del flujo AOCR se reflejen de forma consistente en transiciones, bandejas, badges y pantallas de detalle, con enfasis en el ciclo `Observada -> Subsanada`.

## Alcance minimo

- Solicitudes en `Observada`
- Solicitudes en `Subsanada`
- Solicitudes en `Aceptacion Documental`
- Solicitudes en `En Inspeccion`
- Bandeja `MisSolicitudes`
- Badge lateral `Ver subsanaciones`
- Pantallas `FormularioEmisionAOCR`, `Subsanar` y `Detalle`

## Datos de prueba sugeridos

| Caso | Solicitud | Estado inicial | Nota |
|------|-----------|----------------|------|
| RT-01 | AOCR007 | Observada | Caso controlado de subsanacion documental |
| RT-02 | Cualquier solicitud con aceptacion documental | Aceptacion Documental | Validar salida de fase documental |
| RT-03 | Cualquier solicitud con inspeccion activa | En Inspeccion | Validar continuidad operativa |

---

## Caso 1. Observada en bandeja y formulario general

### Precondicion
- La solicitud tiene al menos un documento con ultima decision `OBSERVADO` o `DEVUELTO`.
- El estado persistido de la solicitud es `Observada`.

### Pasos
1. Ingresar como solicitante/RT.
2. Abrir `SolicitudAOCR/MisSolicitudes`.
3. Verificar que la bandeja aterrice en filtro `observado`.
4. Abrir la solicitud observada desde la bandeja.
5. Ir a `FormularioEmisionAOCR?oid={id}`.
6. Abrir el tab `4. Comprobante / Anexos`.

### Resultado esperado
- La solicitud aparece en la bandeja de observados.
- El badge lateral `Ver subsanaciones` muestra al menos `1`.
- El formulario general no muestra todos los anexos.
- Solo aparece el aviso de modo subsanacion o los inputs estrictamente pendientes.
- Si el documento pendiente no se corrige en ese formulario, se muestra enlace a `Subsanar`.

---

## Caso 2. Pantalla Subsanar enfocada

### Precondicion
- La solicitud sigue en `Observada`.

### Pasos
1. Abrir `SolicitudAOCR/Subsanar/{id}`.
2. Revisar el bloque `Corrección de Documentos`.

### Resultado esperado
- La pantalla muestra solo documentos pendientes de subsanacion.
- El contador visible coincide con la cantidad real de documentos observados/devueltos.
- Existe un solo input de carga por cada documento pendiente.
- No se listan documentos ya aceptados ni anexos no relacionados.

---

## Caso 3. Reenvio de subsanacion

### Precondicion
- Existe al menos un documento pendiente de subsanacion.

### Pasos
1. Cargar un archivo valido en `Subsanar`.
2. Ingresar comentario opcional.
3. Pulsar `Reenviar Corrección`.
4. Confirmar `Sí, reenviar`.

### Resultado esperado
- El sistema redirige a `SolicitudAOCR/Detalle/{id}`.
- La solicitud deja de estar en `Observada` y pasa a `Subsanada`.
- El badge `Ver subsanaciones` disminuye o llega a `0` si no quedan pendientes.
- La solicitud ya no aparece en la bandeja filtrada por observados.
- Se registra una nueva revision documental `PENDIENTE_REVISION_SUBSANACION` para el documento reenviado.

---

## Caso 4. Detalle posterior a Subsanada

### Precondicion
- La solicitud fue reenviada desde `Subsanar`.

### Pasos
1. Abrir `SolicitudAOCR/Detalle/{id}`.
2. Revisar el estado visible del expediente.
3. Revisar alertas operativas y observaciones.

### Resultado esperado
- El estado visible muestra `SUBSANADA`.
- No se muestra `EN_REVISION_INSPECTOR` como sustituto visual del estado persistido.
- No aparece el bloque rojo `Documentación obligatoria pendiente` por la subsanacion ya reenviada.
- La observacion del detalle refleja el comentario de reenvio o la observacion operacional correspondiente.

---

## Caso 5. Revisión inspector posterior a Subsanada

### Precondicion
- La solicitud ya está en `Subsanada`.

### Pasos
1. Ingresar como inspector.
2. Abrir el expediente o la bandeja documental correspondiente.
3. Verificar el documento reenviado.

### Resultado esperado
- El inspector ve la solicitud como pendiente de revision documental posterior a subsanacion.
- El documento reenviado aparece con decision o estado `PENDIENTE_REVISION_SUBSANACION`.
- No se pierde el historial de observaciones previas.

---

## Caso 6. Aceptacion documental

### Precondicion
- Todos los documentos vigentes tienen decision final `ACEPTADO`.

### Pasos
1. Abrir el detalle como coordinacion o rol autorizado.
2. Firmar o confirmar la aceptacion documental.

### Resultado esperado
- El estado visible cambia a `ACEPTADO_INSPECTOR` o `AUTORIZACION_FIRMADA` segun corresponda.
- No aparecen alertas de documentos faltantes.
- No se habilita subsanacion para el RT.

---

## Caso 7. En inspeccion

### Precondicion
- La solicitud ya superó la fase documental y se encuentra en `En Inspeccion`.

### Pasos
1. Abrir el detalle de la solicitud.
2. Revisar el estado visible y acciones disponibles.

### Resultado esperado
- El estado visible muestra `EN INSPECCION` o el equivalente operativo esperado.
- No aparece la alerta de documentos obligatorios pendientes por artefactos de fases anteriores.
- Las acciones visibles corresponden a inspeccion, no a subsanacion documental.

---

## Caso 8. Coherencia transversal de estado

### Pasos
1. Tomar una solicitud en cada uno de estos estados: `Observada`, `Subsanada`, `Aceptacion Documental`, `En Inspeccion`.
2. Comparar el mismo expediente en estas superficies:
   - Bandeja `MisSolicitudes`
   - Badge lateral
   - `Detalle`
   - Cualquier formulario accionable del flujo

### Resultado esperado
- El mismo estado funcional no cambia de significado entre superficies.
- Ninguna pantalla interpreta `Subsanada` como `Observada`.
- Ninguna pantalla de detalle muestra alertas de faltantes incompatibles con el estado persistido.

---

## Criterio de salida

- Todos los casos criticos pasan sin desviaciones funcionales.
- El ciclo `Observada -> Subsanada` se valida de punta a punta.
- La UI muestra el mismo estado funcional que la base de datos.
- Las bandejas y badges quedan alineados con el estado persistido.