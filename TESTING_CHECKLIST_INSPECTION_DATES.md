# Testing Checklist - Inspection Dates Consolidation (Opción A)

**Versión**: 1.0  
**Fecha**: 2026-09-22  
**Status**: Ready to Test

---

## Pre-Testing Setup

- [ ] Compilar solución (Build → Build Solution en Visual Studio)
- [ ] Verificar que NO hay errores de compilación
- [ ] Ejecutar aplicación localmente
- [ ] Acceder a módulo "Solicitud AOCR" → Crear Nueva

---

## Test Suite 1: UI Básica

### Test 1.1: Visibilidad de Sección
```
Objetivo: Verificar que la sección "Lugares de Inspección" está correctamente 
          reemplazada sin la duplicación anterior
```
- [ ] La sección "Lugares de Inspección" es visible en el formulario
- [ ] Subtitle: "Selecciona los aeropuertos donde requieres inspección..."
- [ ] Badge "AC-02 (Consolidado)" visible
- [ ] **NO** aparece la sección "Estaciones y Fechas Independientes" (Punto 3)
- [ ] La tabla anterior con selector de estación/rango está **eliminada**

### Test 1.2: Checkboxes Visibles
```
Objetivo: Verificar que los checkboxes de aeropuertos están presentes
```
- [ ] Checkbox "Quito (UIO)" visible
- [ ] Checkbox "Guayaquil (GYE)" visible
- [ ] Checkbox "Manta (MEC)" visible
- [ ] Checkbox "Latacunga (LTX)" visible
- [ ] Checkbox "Otros" visible
- [ ] Todos los checkboxes están **desmarcados** por defecto

### Test 1.3: Date Pickers Inicialmente Ocultos
```
Objetivo: Verificar que los date pickers NO aparecen al cargar la página
```
- [ ] Date picker de Quito está **oculto**
- [ ] Date picker de Guayaquil está **oculto**
- [ ] Date picker de Manta está **oculto**
- [ ] Date picker de Latacunga está **oculto**
- [ ] Date picker de Otros está **oculto**

### Test 1.4: Input de "Otros" Oculto
```
Objetivo: Verificar que el input de texto para "Otros" está oculto por defecto
```
- [ ] Input "Detalle de otros aeropuertos" está **oculto** al cargar

---

## Test Suite 2: Interacción con Checkboxes

### Test 2.1: Mostrar Date Picker al Marcar
```
Objetivo: Verificar que marcar un checkbox muestra el date picker correspondiente
```
**Pasos:**
1. Marcar checkbox "Quito"

**Esperado:**
- [ ] Date picker de Quito aparece con animación suave (fade-in)
- [ ] Label "Fecha requerida de inspección *" visible
- [ ] Help text "Selecciona la fecha en que requieres la inspección" visible
- [ ] Input de fecha está vacío (listo para seleccionar)

**Pasos:**
2. Marcar checkbox "Guayaquil"

**Esperado:**
- [ ] Date picker de Guayaquil aparece (sin afectar Quito)
- [ ] El date picker de Quito sigue visible
- [ ] Cada date picker es **independiente**

### Test 2.2: Ocultar Date Picker al Desmarcar
```
Objetivo: Verificar que desmarcar un checkbox oculta el date picker y limpia datos
```
**Pasos:**
1. Marcar "Quito" + Seleccionar fecha (ej: 2026-09-22)
2. Desmarcar "Quito"

**Esperado:**
- [ ] Date picker de Quito desaparece con animación suave (fade-out)
- [ ] Fecha seleccionada se **limpia**
- [ ] Si vuelves a marcar "Quito", el input está vacío nuevamente

### Test 2.3: Múltiples Aeropuertos Independientes
```
Objetivo: Verificar que puedes seleccionar varios aeropuertos 
          con fechas diferentes e independientes
```
**Pasos:**
1. Marcar: Quito, Guayaquil, Manta
2. Seleccionar fechas diferentes:
   - Quito: 2026-09-22
   - Guayaquil: 2026-09-25
   - Manta: 2026-09-26

**Esperado:**
- [ ] Todos 3 date pickers visibles simultáneamente
- [ ] Cada uno muestra su fecha independiente
- [ ] Los valores NO se interfieren entre sí

---

## Test Suite 3: Input "Otros"

### Test 3.1: Mostrar Input de Texto
```
Objetivo: Verificar que el input "Otros" aparece al marcar ese checkbox
```
**Pasos:**
1. Marcar checkbox "Otros"

**Esperado:**
- [ ] Input "Detalle de otros aeropuertos" aparece
- [ ] Placeholder: "Detalle de otros aeropuertos"
- [ ] Máximo 200 caracteres
- [ ] Date picker de "Otros" también aparece

### Test 3.2: Input de Texto + Date Picker
```
Objetivo: Verificar que puedes llenar texto y seleccionar fecha para "Otros"
```
**Pasos:**
1. Marcar "Otros"
2. Escribir en el input: "Cuenca (CUE)"
3. Seleccionar fecha: 2026-09-23

**Esperado:**
- [ ] Texto se guarda en el input
- [ ] Date picker está disponible
- [ ] Fecha se selecciona correctamente
- [ ] Ambos valores persisten juntos

### Test 3.3: Ocultar Input al Desmarcar
```
Objetivo: Verificar que al desmarcar "Otros" se ocultan texto y date picker
```
**Pasos:**
1. Marcar "Otros" + llenar datos
2. Desmarcar "Otros"

**Esperado:**
- [ ] Input de texto desaparece
- [ ] Date picker de "Otros" desaparece

---

## Test Suite 4: Validación

### Test 4.1: Validación al Guardar (Falta de Fechas)
```
Objetivo: Verificar que NO puedes guardar sin fechas en aeropuertos seleccionados
```
**Pasos:**
1. Marcar: Quito, Guayaquil
2. Seleccionar fecha solo para Quito (2026-09-22)
3. **NO** seleccionar fecha para Guayaquil
4. Hacer clic en "Guardar Operaciones"

**Esperado:**
- [ ] Formulario **NO** se envía
- [ ] El input de fecha de Guayaquil se destaca en **ROJO** (.is-invalid)
- [ ] Aparece un alert: "Todos los aeropuertos seleccionados deben tener fecha..."

### Test 4.2: Validación Exitosa
```
Objetivo: Verificar que puedes guardar cuando todas las fechas están completas
```
**Pasos:**
1. Marcar: Quito, Guayaquil, Manta
2. Seleccionar fecha para CADA uno:
   - Quito: 2026-09-22
   - Guayaquil: 2026-09-25
   - Manta: 2026-09-26
3. Hacer clic en "Guardar Operaciones"

**Esperado:**
- [ ] Inputs de fecha **NO** muestran rojo
- [ ] Formulario se envía (sin error)
- [ ] Los datos se guardan en la BD

### Test 4.3: Mensajes de Error Visuales
```
Objetivo: Verificar que los inputs se marcan en rojo cuando faltan fechas
```
**Pasos:**
1. Marcar Quito
2. Dejar el input de fecha vacío
3. Intentar guardar

**Esperado:**
- [ ] Input de fecha tiene clase `.is-invalid`
- [ ] Borde rojo + sombra
- [ ] El campo es claramente identificable como error

---

## Test Suite 5: Integración con Formulario

### Test 5.1: Guardar y Recuperar Datos
```
Objetivo: Verificar que los datos se guardan correctamente en la BD 
          y se recuperan al editar
```
**Pasos:**
1. Crear solicitud con:
   - Quito: 2026-09-22
   - Guayaquil: 2026-09-25
2. Guardar (Guardar Operaciones)
3. Recargar la página / Abrir formulario nuevamente
4. Editar la misma solicitud

**Esperado:**
- [ ] Los checkboxes están marcados según datos guardados
- [ ] Los date pickers muestran las fechas guardadas
- [ ] Datos están correctamente precargados

### Test 5.2: Edición de Datos
```
Objetivo: Verificar que puedes editar fechas de inspección
```
**Pasos:**
1. Abrir solicitud existente con datos precargados
2. Cambiar fecha de Quito: 2026-09-22 → 2026-09-30
3. Marcar un nuevo aeropuerto: Latacunga
4. Seleccionar fecha: 2026-09-27
5. Guardar

**Esperado:**
- [ ] Cambios se guardan correctamente
- [ ] Al recargar, se muestran los datos nuevos
- [ ] Nuevo aeropuerto está marcado

### Test 5.3: Verificación de Datos en BD (Opcional)
```
Objetivo: Verificar estructura de datos guardados en BD
```
**Pasos:**
1. Ejecutar SQL: SELECT * FROM aocr_tbsolicitud_estacion_inspeccion WHERE solicitud_id = <id>
2. Verificar que FechaInspeccion está poblada

**Esperado:**
- [ ] Columna FechaInspeccion contiene las fechas seleccionadas
- [ ] EstacionCodigo = "UIO", "GYE", etc.
- [ ] EstacionNombre = "Quito", "Guayaquil", etc.

---

## Test Suite 6: Responsividad

### Test 6.1: Desktop (1920x1080)
```
Objetivo: Verificar layout en desktop
```
- [ ] Checkboxes + date pickers en flex horizontal
- [ ] Date picker visible a la derecha del checkbox
- [ ] Responsive, sin overflow horizontal

### Test 6.2: Tablet (768px)
```
Objetivo: Verificar layout en tablet
```
- [ ] Layout se adapta a 768px
- [ ] Date picker debajo del checkbox (flex-column)
- [ ] No hay overflow horizontal

### Test 6.3: Mobile (375px)
```
Objetivo: Verificar layout en móvil
```
- [ ] Layout completamente apilado (vertical)
- [ ] Checkbox → Label → Date picker apilados
- [ ] Tap en date picker abre calendario nativo (si disponible)
- [ ] No hay overflow horizontal

---

## Test Suite 7: Browser Compatibility

- [ ] Chrome 60+ (último + -1)
- [ ] Firefox 55+ (último + -1)
- [ ] Safari 11+ (si aplica)
- [ ] Edge 79+
- [ ] ⚠️ IE 11: Date input puede requerir polifill

---

## Test Suite 8: Casos Edge Case

### Test 8.1: Marcar/Desmarcar Múltiples Veces
```
Objetivo: Verificar que el estado se mantiene correcto
```
- [ ] Marcar Quito → Seleccionar fecha
- [ ] Desmarcar Quito → Fecha se limpia
- [ ] Marcar Quito nuevamente → Input está vacío (listo para nueva fecha)

### Test 8.2: Cambiar de Fecha Múltiples Veces
```
Objetivo: Verificar que puedes cambiar fecha sin problemas
```
- [ ] Seleccionar fecha 1: 2026-09-22
- [ ] Cambiar a fecha 2: 2026-09-30
- [ ] Cambiar a fecha 3: 2026-09-15
- [ ] Todas las operaciones funcionan sin error

### Test 8.3: Seleccionar Mismo Aeropuerto + Otros
```
Objetivo: Verificar que la lógica de "Otros" funciona junto con otros
```
- [ ] Marcar Quito + seleccionar fecha
- [ ] Marcar Otros + escribir "Baltra" + seleccionar fecha
- [ ] Ambos se guardan correctamente

### Test 8.4: Formulario con Otros Campos
```
Objetivo: Verificar que cambios en date pickers NO afectan otros campos
```
- [ ] Llenar otros campos del formulario (compañía, operaciones, etc.)
- [ ] Interactuar con date pickers
- [ ] Verificar que otros campos NO se modifican

---

## Test Suite 9: Console & Debugging

### Test 9.1: Logs en Console
```
Objetivo: Verificar que el debugging funciona
```
**Pasos:**
1. Abrir DevTools (F12)
2. Ir a Console
3. Interactuar con checkboxes y date pickers

**Esperado:**
- [ ] Logs aparecer con prefijo `[AOCR]`
- [ ] Ejemplo: "[AOCR] Inspection Date Manager initialized"
- [ ] Cambios en selectedDates se loguean

### Test 9.2: Sin Errores en Console
```
Objetivo: Verificar que no hay errores de JavaScript
```
- [ ] Console está limpia (sin red X)
- [ ] Sin errores de "undefined variable"
- [ ] Sin warnings (solo info logs)

---

## Test Suite 10: Performance

### Test 10.1: Carga de Página
```
Objetivo: Verificar que los nuevos scripts/CSS no ralentizan la carga
```
- [ ] Página carga en <3 segundos
- [ ] Sin lag perceptible al abrir formulario

### Test 10.2: Interacción Suave
```
Objetivo: Verificar que show/hide de date picker es suave
```
- [ ] Click en checkbox → date picker aparece sin delay
- [ ] Animación de fade-in es suave (<200ms)

---

## Test Suite 11: Backward Compatibility

### Test 11.1: Solicitudes Antiguas (Si existen)
```
Objetivo: Verificar que solicitudes con datos legacy siguen funcionando
```
**Pasos (si aplica):**
1. Abrir solicitud creada ANTES de este cambio
2. Editar (sin cambiar fechas)
3. Guardar

**Esperado:**
- [ ] Los datos legacy (FechaInicio/FechaFin) se respetan
- [ ] RangoFechasTexto muestra correctamente
- [ ] No hay error en BD

---

## Post-Testing Checklist

- [ ] Todos los tests pasaron ✅
- [ ] No hay errores en console
- [ ] Documentar cualquier issue encontrado
- [ ] Si hay issues: aplicar fix + re-test
- [ ] Marcar como "READY FOR DEPLOYMENT"

---

## Bugs Encontrados & Resolutivos

| # | Descripción | Severidad | Status | Fix |
|---|-------------|-----------|--------|-----|
|   |             |           |        |     |

---

## Notas Generales

- Punto 3 "Estaciones y Fechas Independientes" fue **completamente eliminado**
- No hay cambios en BD (Opción A)
- Todos los datos se envían como `List<SolicitudEstacionInspeccionItemVM>` via model binding
- Browser date input nativo se usa (compatible con todos los navegadores modernos)

---

**Generado**: 2026-09-22  
**Testing Status**: PENDING ⏳  
**Sign-off**: _________________ (QA/Dev)

