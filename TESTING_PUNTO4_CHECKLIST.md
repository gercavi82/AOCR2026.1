# CHECKLIST DE PRUEBA – Punto 4: Carga Múltiple de Archivos

## Instrucciones Generales

1. **Ambiente:** Usar navegador Chrome/Firefox/Edge moderno
2. **Usuario:** Usar una cuenta con permisos de crear solicitudes AOCR
3. **Documentos:** Preparar archivos PDF/DOC de diferentes tamaños y carpetas
4. **Registro:** Anotar cualquier error o comportamiento inesperado

---

## SERIE DE PRUEBAS A: Creación de Solicitud AOCR

### Test A1: Acumulación Básica en Creación

**Objetivo:** Verificar que múltiples selecciones de archivo se acumulan sin reemplazo

**Pasos:**
1. Ir a: SolicitudAOCR → Crear Solicitud
2. Hacer clic en "Documentación de Respaldo" → "Seleccionar archivos"
3. Seleccionar 2 PDF: `prueba1.pdf`, `prueba2.pdf`
4. Hacer clic en "Abrir"
5. **VERIFICAR:** Se muestra lista con 2 archivos
   - ✓ Nombre visible: "prueba1.pdf", "prueba2.pdf"
   - ✓ Tamaño visible (ej: "256 KB")
   - ✓ Botón "Quitar" visible para cada uno

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Detalles:   ___________________________________________
```

---

### Test A2: Agregar Más Archivos (Segunda Selección)

**Objetivo:** Verificar que nueva selección ACUMULA en lugar de REEMPLAZAR

**Pasos:**
1. Desde estado Test A1 (ya hay 2 archivos)
2. Hacer clic nuevamente en campo de archivo
3. Navegare a CARPETA DIFERENTE
4. Seleccionar 1 PDF: `prueba3.pdf`
5. Hacer clic en "Abrir"
6. **VERIFICAR:** Se muestra lista con 3 archivos:
   - ✓ "prueba1.pdf" sigue ahí (no desapareció)
   - ✓ "prueba2.pdf" sigue ahí (no desapareció)
   - ✓ "prueba3.pdf" nuevo aparece al final
7. **NO ESPERADO:** Lista vacía, solo "prueba3.pdf", notificación de error

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Detalles:   ___________________________________________
Archivos visibles: [ ] 3   [ ] 2   [ ] 1   [ ] 0
```

---

### Test A3: Tercera Selección (Múltiple)

**Objetivo:** Verificar acumulación con múltiples archivos en tercera selección

**Pasos:**
1. Desde Test A2 (hay 3 archivos)
2. Hacer clic en campo de archivo
3. Seleccionar 2 PDF simultáneamente: `prueba4.pdf`, `prueba5.pdf`
4. Hacer clic en "Abrir"
5. **VERIFICAR:** Se muestra lista con 5 archivos totales (A1's 2 + A2's 1 + A3's 2)
   - ✓ Total 5 archivos en lista
   - ✓ Orden: prueba1, prueba2, prueba3, prueba4, prueba5

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Total archivos: [ ] 5   [ ] 4   [ ] 3   [ ] 2   [ ] 1
```

---

### Test A4: Eliminar Archivo Individual

**Objetivo:** Verificar que se puede quitar un archivo sin afectar los demás

**Pasos:**
1. Desde Test A3 (hay 5 archivos)
2. Localizar fila de "prueba2.pdf" en la lista
3. Hacer clic en botón "Quitar" de esa fila
4. **VERIFICAR:**
   - ✓ prueba2.pdf desaparece
   - ✓ Quedan 4 archivos: prueba1, prueba3, prueba4, prueba5
   - ✓ Otros archivos no se afectaron
5. Eliminar otro: hacerclic en "Quitar" de prueba4.pdf
6. **VERIFICAR:** Quedan 3 archivos

**Registro:**
```
Resultado después de quitar 1: [ ] Paso (4 archivos)   [ ] Fallo
Resultado después de quitar 2: [ ] Paso (3 archivos)   [ ] Fallo
```

---

### Test A5: Envío Completo

**Objetivo:** Verificar que todos los archivos acumulados se envían correctamente

**Pasos:**
1. Desde Test A4 (hay 3 archivos: prueba1, prueba3, prueba5)
2. Llenar campos requeridos de solicitud:
   - Nombre operador: "TESTE AIRLINES"
   - RUC: "17XXXXXXXXX001"
   - Tipo: "EMISIÓN"
   - Agregar una aeronave (marca, modelo, matrícula, config)
3. Hacer clic en "GUARDAR SOLICITUD"
4. **VERIFICAR:**
   - ✓ Formulario se envía sin errores
   - ✓ Página se recarga y muestra la nueva solicitud
   - ✓ En detalle de solicitud, verificar que se guardaron 3 documentos

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Documentos guardados en DB: [ ] 3   [ ] 2   [ ] 1   [ ] 0
Errores en consola: [ ] No   [ ] Sí (describir)
```

---

## SERIE DE PRUEBAS B: Detección de Duplicados

### Test B1: Duplicado por Nombre + Tamaño

**Objetivo:** Verificar que sistema detecta y rechaza duplicados

**Pasos:**
1. Ir a: SolicitudAOCR → Crear Solicitud
2. Seleccionar archivo: `documento.pdf` (256 KB)
3. **VERIFICAR:** Aparece en lista
4. Hacer clic nuevamente en seleccionar
5. Elegir EL MISMO `documento.pdf` desde OTRA CARPETA (o sin moverse)
6. **VERIFICAR:**
   - ✓ Notificación amarilla: "Se ignoró 1 archivo duplicado."
   - ✓ Lista sigue con 1 solo archivo (no agregó duplicado)
   - ✓ No hay error, es comportamiento esperado

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Notificación mostrada: [ ] Sí   [ ] No
Archivo en lista: [ ] 1   [ ] 2   (esperado 1)
```

---

### Test B2: Archivos Diferentes con Mismo Nombre

**Objetivo:** Verificar que archivos con mismo nombre pero diferente contenido SÍ se agregan

**Pasos:**
1. Ir a: SolicitudAOCR → Crear Solicitud
2. Seleccionar: `reporte.pdf` (v1, 100 KB, modificado hoy)
3. **VERIFICAR:** En lista
4. Hacer clic nuevamente
5. Seleccionar: `reporte.pdf` (v2, 150 KB, modificado ayer) de diferente carpeta
6. **VERIFICAR:**
   - ✓ Se agrega (no dice duplicado)
   - ✓ Lista tiene 2 archivos, ambos se llaman `reporte.pdf`
   - ✓ Se distinguen por tamaño diferente (100 KB vs 150 KB)

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Archivos en lista: [ ] 2   [ ] 1
Tamaños distintos mostrados: [ ] Sí   [ ] No
```

---

## SERIE DE PRUEBAS C: Subsanación de Documentos

### Test C1: Subsanación con Múltiples Versiones

**Objetivo:** Verificar acumulación en subsanación (permite múltiples versiones del mismo doc)

**Pasos:**
1. Ir a una solicitud AOCR con documentos en estado "Subsanar"
2. Localizar documento "Cert. Operacional" pendiente de subsanación
3. Hacer clic en "Subsanar documento" → seleccionar archivos
4. Seleccionar: `cert_op_v1.pdf`
5. **VERIFICAR:** Aparece en lista debajo de "Cert. Operacional"
6. Hacer clic nuevamente para seleccionar
7. Seleccionar: `cert_op_v2.pdf`
8. **VERIFICAR:**
   - ✓ Ambas versiones en lista
   - ✓ Nombres diferentes: "cert_op_v1.pdf", "cert_op_v2.pdf"
   - ✓ Tarjeta de documento se resalta (indica "tiene archivo")

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Versiones visibles: [ ] 2   [ ] 1
Tarjeta resaltada: [ ] Sí   [ ] No
```

---

### Test C2: Subsanación de Múltiples Documentos

**Objetivo:** Verificar que múltiples documentos pueden acumular archivos independientemente

**Pasos:**
1. En una solicitud con 2 documentos pendientes de subsanación:
   - Doc 1: "Certificado Operacional"
   - Doc 2: "Manual de Operaciones"
2. Para Doc 1: Seleccionar 2 archivos (`cert_v1.pdf`, `cert_v2.pdf`)
3. Para Doc 2: Seleccionar 1 archivo (`manual_v1.pdf`)
4. **VERIFICAR:**
   - ✓ Doc 1: muestra 2 archivos en su lista
   - ✓ Doc 2: muestra 1 archivo en su lista
   - ✓ No se mezclan las listas
   - ✓ Total visible: 3 archivos (2 para Doc1, 1 para Doc2)

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Doc 1 archivos: [ ] 2   [ ] 1   [ ] 0
Doc 2 archivos: [ ] 1   [ ] 0
Listas separadas: [ ] Sí   [ ] No
```

---

### Test C3: Envío de Subsanación

**Objetivo:** Verificar que todos los archivos de todos los documentos se envían

**Pasos:**
1. Desde Test C2 (Doc1 tiene 2 archivos, Doc2 tiene 1)
2. Llenar campo "Comentario adicional" (opcional)
3. Hacer clic en "Enviar subsanación al Inspector"
4. **VERIFICAR:**
   - ✓ Formulario se envía sin errores
   - ✓ Sistema muestra mensaje de éxito
   - ✓ En detalles de solicitud, verificar que se guardaron:
     - 2 versiones nuevas del Certificado
     - 1 versión nueva del Manual
   - ✓ Las versiones anteriores se conservan en historial

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Mensaje de éxito: [ ] Mostrado   [ ] No
Versiones guardadas:
  - Cert.: [ ] 2 nuevas   [ ] 1   [ ] 0
  - Manual: [ ] 1 nueva   [ ] 0
Historial conservado: [ ] Sí   [ ] No
```

---

## SERIE DE PRUEBAS D: Casos Extremos

### Test D1: Selección Cancelada

**Objetivo:** Verificar comportamiento si usuario presiona "Cancelar" en diálogo

**Pasos:**
1. Crear solicitud AOCR
2. Seleccionar 2 archivos (se acumulan OK)
3. Hacer clic nuevamente para seleccionar
4. En diálogo de selección, presionar "CANCELAR" (no seleccionar nada)
5. **VERIFICAR:**
   - ✓ Diálogo cierra
   - ✓ Lista sigue mostrando 2 archivos originales (no cambió)
   - ✓ Sin errores en consola

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Archivos preservados: [ ] Sí (2)   [ ] No
```

---

### Test D2: Archivos Grandes

**Objetivo:** Verificar funcionamiento con archivos próximos al límite

**Pasos:**
1. Crear solicitud AOCR
2. Seleccionar archivo grande: 8 MB PDF (cercano a límite típico de 10 MB)
3. **VERIFICAR:** Se agrega a lista normalmente
4. Seleccionar segundo archivo grande: 7 MB
5. **VERIFICAR:**
   - ✓ Se acumula
   - ✓ Tamaño total visible (~15 MB)
6. Intentar agregar tercer archivo 5 MB
7. **ESPERADO:** Se agrega o se muestra advertencia (depende config servidor)

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Archivo 8 MB: [ ] Aceptado   [ ] Rechazado
Archivo 7 MB: [ ] Aceptado   [ ] Rechazado
Archivo 5 MB: [ ] Aceptado   [ ] Rechazado
Total: [ ] ~20 MB   [ ] Limitado a 10 MB
```

---

### Test D3: Muchos Archivos Pequeños

**Objetivo:** Verificar rendimiento con 20+ archivos pequeños

**Pasos:**
1. Crear solicitud AOCR
2. Seleccionar 10 archivos pequeños (100 KB cada uno)
3. **VERIFICAR:** Se acumulan, lista no es lenta
4. Seleccionar otros 10 archivos pequeños
5. **VERIFICAR:**
   - ✓ Total 20 archivos en lista
   - ✓ Desplazamiento dentro de la lista es fluido
   - ✓ Sin mensajes de error
   - ✓ Botones "Quitar" funcionan sin lag

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Archivos acumulados: [ ] 20   [ ] 10   [ ] Menos
Performance: [ ] Buena   [ ] Lenta   [ ] Error
```

---

### Test D4: Diferentes Tipos de Archivo

**Objetivo:** Verificar que funciona con distintos formatos

**Pasos:**
1. Crear solicitud AOCR
2. Seleccionar archivos mezclados:
   - `documento.pdf` (PDF)
   - `informe.docx` (Word)
   - `datos.xlsx` (Excel)
   - `foto.jpg` (Imagen)
3. **VERIFICAR:**
   - ✓ Todos se acumulan en lista
   - ✓ Extensiones visibles (ej: .pdf, .docx, .xlsx, .jpg)
   - ✓ Todos se envían correctamente

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Archivos acumulados: [ ] 4   [ ] 3   [ ] Menos
Extensiones visibles: [ ] Sí   [ ] No
Envío exitoso: [ ] Sí   [ ] No
```

---

## SERIE DE PRUEBAS E: Validación Visual y UX

### Test E1: Responsive en Móvil

**Objetivo:** Verificar que interfaz funciona en pantalla pequeña

**Pasos:**
1. Abrir navegador Chrome en modo "Responsive Design" (F12)
2. Seleccionar dispositivo "iPhone 12" o similar
3. Ir a crear solicitud AOCR
4. Seleccionar 3 archivos
5. **VERIFICAR:**
   - ✓ Lista de archivos es legible en pantalla pequeña
   - ✓ Botón "Quitar" es clickeable (no muy pequeño)
   - ✓ Nombres de archivo no se sobrelapan
   - ✓ Sin scroll horizontal innecesario

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Legibilidad: [ ] Buena   [ ] Deficiente
Botones clickeables: [ ] Sí   [ ] No
Overflow horizontal: [ ] No   [ ] Sí
```

---

### Test E2: Notificaciones

**Objetivo:** Verificar que notificaciones se muestran correctamente

**Pasos:**
1. Crear solicitud AOCR
2. Seleccionar archivo `test.pdf`
3. Intentar seleccionar el MISMO `test.pdf` nuevamente
4. **VERIFICAR:**
   - ✓ Aparece notificación amarilla/alerta
   - ✓ Mensaje dice: "Se ignoró 1 archivo duplicado."
   - ✓ Notificación desaparece después de 5 segundos
5. Si hay error real, verificar que se muestra notificación roja/error

**Registro:**
```
Resultado:  [ ] Paso   [ ] Fallo
Notificación duplicado: [ ] Mostrada   [ ] No
Auto-cierre: [ ] Sí (5 seg)   [ ] No
Color: [ ] Amarillo   [ ] Otro
```

---

## REPORTE FINAL DE PRUEBAS

### Resumen de Series

| Serie | Descripción | Tests | Pasaron | Fallaron | Status |
|-------|-------------|-------|---------|----------|--------|
| A | Creación de solicitud | 5 | [ ] | [ ] | [ ] OK / [ ] Fallo |
| B | Duplicados | 2 | [ ] | [ ] | [ ] OK / [ ] Fallo |
| C | Subsanación | 3 | [ ] | [ ] | [ ] OK / [ ] Fallo |
| D | Casos extremos | 4 | [ ] | [ ] | [ ] OK / [ ] Fallo |
| E | Validación UX | 2 | [ ] | [ ] | [ ] OK / [ ] Fallo |

### Total
- **Total tests ejecutados:** [ ] / 16
- **Pasaron:** [ ] / 16
- **Fallaron:** [ ] / 16
- **% Éxito:** [ ] %

### Observaciones Generales
```
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
```

### Firma
**Tester:** _________________________ **Fecha:** ______________

**Aprobación:** ☐ APROBADO para Producción  ☐ RECHAZO (requiere fixes)

---

## Notas Finales

- Todos los tests pasados = Listo para producción ✓
- Si hay fallos: Documentar errores exactos y reporte a dev
- Revisar console del navegador (F12 → Console) para errores JavaScript
- Si es lento: Revisar performance en Network tab (F12 → Network)

**Gracias por probar la funcionalidad de carga múltiple de archivos.**
