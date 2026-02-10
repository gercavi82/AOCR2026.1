# Revisión del flujo: módulo de órdenes de recaudación

## Objetivo
Mejorar la claridad y eficiencia del flujo de trabajo para **órdenes de recaudación** en las vistas `Index`, `Nueva`, `Editar`, `Detalles` y `Obligatoria`, reduciendo pasos innecesarios y evitando errores por ambigüedad de estado.

---

## Flujo recomendado (alto nivel)
1. **Entrada única**
   - El usuario aterriza en **Index** con un resumen (totales, pagadas, pendientes) y CTA principal.
2. **Creación guiada**
   - Desde Index → **Nueva** con datos mínimos requeridos y validaciones claras.
3. **Revisión y confirmación**
   - Después de guardar, enviar al usuario a **Detalles** con un resumen y acciones disponibles según rol/estado.
4. **Seguimiento del estado**
   - La orden avanza por estados: *Borrador → Generada → Enviada → Pagada/Anulada*.
5. **Visibilidad de pendientes**
   - En **Index** y **Obligatoria**, destacar pendientes con filtros preaplicados y un “siguiente paso” recomendado.

---

## Ajustes de UX propuestos
### 1) Index: priorizar acciones claras
- **CTA principal**: “Crear nueva orden” visible siempre y deshabilitado solo si existe una orden en borrador.
- **CTA secundarias**: “Revisar pendientes”, “Ver pagadas”, “Ver anuladas”.
- **Resumen contextual**: mostrar “pendientes” con enlace directo a filtro activo.

### 2) Nueva: creación con pasos cortos
- **Paso 1**: Datos obligatorios (cliente, monto, concepto, fecha).
- **Paso 2**: Adjuntos (opcional, arrastrar/soltar).
- **Paso 3**: Confirmación previa con resumen editable.
- **Acción principal**: “Guardar borrador” y “Generar orden”.

### 3) Editar: evitar cambios conflictivos
- Bloquear edición si la orden no está en **Borrador** o **Generada**.
- Mostrar advertencia clara si el usuario no puede editar (motivo + acción sugerida).

### 4) Detalles: enfoque en decisiones
- **Bloque superior**: estado actual + próximas acciones disponibles por rol.
- **Historial**: línea de tiempo de eventos (creación, edición, envío, pago, anulación).
- **Acciones**: “Enviar”, “Marcar pagada”, “Anular” con confirmación explícita.

### 5) Obligatoria: urgencias visibles
- Ordenar por **fecha límite** y resaltar vencidas.
- Un botón “Resolver ahora” que lleve a Detalles con foco en la acción requerida.

---

## Reglas de negocio (sugeridas)
- **Borrador**: editable, no visible para financiero.
- **Generada**: visible para financiero, permite envío.
- **Enviada**: visible para seguimiento, permite marcar pagada/anular.
- **Pagada/Anulada**: solo lectura.

---

## Mensajería y validaciones
- **Validaciones inline** con mensajes cortos (“El monto debe ser mayor a 0”).
- **Confirmaciones explícitas** en acciones irreversibles (anular, marcar pagada).
- **Estados vacíos útiles**: “No hay órdenes pendientes” + CTA para crear.

---

## Checklist de implementación
- [ ] Confirmar estados disponibles y reglas por rol.
- [ ] Unificar CTA principal en Index.
- [ ] Crear resumen de orden al finalizar Nueva.
- [ ] Bloqueo de edición según estado.
- [ ] Historial de eventos en Detalles.
- [ ] Filtros rápidos en Index/Obligatoria.

---

## Métricas sugeridas
- **Tiempo medio de creación** de una orden.
- **% de órdenes en borrador** sin avanzar.
- **% de órdenes vencidas** en Obligatoria.
- **Errores de validación** por campo.
