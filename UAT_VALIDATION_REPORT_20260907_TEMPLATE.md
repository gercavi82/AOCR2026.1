# UAT VALIDATION REPORT — AOCR Bloque 1+2 (AC-01 a AC-08)

**Fecha Reporte:** 2026-09-07  
**Ambiente:** STAGING  
**Versión AOCR:** [VERSION_NUMBER]  
**Responsable UAT:** [QA_TEAM]  

---

## RESUMEN EJECUTIVO

| Métrica | Objetivo | Resultado | Status |
|---|---|---|---|
| **Plan Validación Completado** | 6/6 fases | X/6 | ⏳ EN PROGRESO |
| **Casos de Prueba Pasados** | 30+ | X/Y | ⏳ |
| **Bloqueadores Encontrados** | 0 | X | ⏳ |
| **Recomendación Final** | PASS → Producción | PENDIENTE | ⏳ |

---

## FASE 1: PREPARACIÓN (30 min)

### Precondiciones

- [ ] Usuarios creados:
  - [ ] Operador (cedula=12345678, email=operador@aocr.cl)
  - [ ] Coordinador (cedula=87654321, email=coordinador@aocr.cl)
  - [ ] DIRCAV (cedula=11111111, email=dircav@aocr.cl)
  - [ ] Inspector 1 (cedula=22222222, email=inspector1@aocr.cl)
  - [ ] Inspector 2 (cedula=33333333, email=inspector2@aocr.cl)
  - [ ] Inspector 3 (cedula=44444444, email=inspector3@aocr.cl)

- [ ] Estaciones creadas:
  - [ ] SCEL (Santiago)
  - [ ] SCVD (Valdivia)
  - [ ] SCERDO (Coyhaique)

- [ ] Operadores maestros:
  - [ ] Operador Test 1 (AOC Número 123)
  - [ ] Operador Test 2 (AOC Número 456)

- [ ] Base de datos STAGING:
  - [ ] Migraciones SQL aplicadas (AC-01 a AC-10)
  - [ ] Tablas aocr_* verificadas
  - [ ] Índices creados

- [ ] Aplicación STAGING:
  - [ ] AOCR.sln compilado Release
  - [ ] Publicado a servidor STAGING
  - [ ] Accesible: http://staging.aocr.local/

### Notas de Preparación

```
Fecha/Hora Inicio: ____________________
Datos maestros creados por: ____________________
Validación precondiciones: ✓ / ✗
Observaciones:
_________________________________________________________________
_________________________________________________________________
```

---

## FASE 2: VALIDACIÓN OPERADOR (30 min)

### Objetivo
Verificar que Operador puede crear solicitud AOC con email RT válido (AC-01) y estaciones con fechas independientes (AC-02).

### Caso 1: AC-01 Email RT — Validación Contextual

**Precondición:** Operador logueado

**Pasos:**
1. Ir a "Nueva Solicitud AOC"
2. Seleccionar Operador: "Operador Test 1"
3. Ingresar Email RT: operador@aocr.cl
4. Sistema valida: Email válido (primer uso)
5. Guardar solicitud
6. Solicitud estado: "DATOS_COMPLETOS"

**Resultado Esperado:**
- ✓ Email aceptado
- ✓ Solicitud creada
- ✓ Solicitud en estado correcto

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase2_ac01_email_rt.png`

---

### Caso 2: AC-02 Estaciones Independientes

**Precondición:** Solicitud en estado "DATOS_COMPLETOS"

**Pasos:**
1. En sección "Estaciones de Operación"
2. Agregar Estación 1:
   - Código: SCEL
   - Nombre: Santiago
   - Fecha Inicio: 2026-10-01
   - Fecha Fin: 2026-10-15
3. Agregar Estación 2:
   - Código: SCVD
   - Nombre: Valdivia
   - Fecha Inicio: 2026-10-05
   - Fecha Fin: 2026-10-30
4. Agregar Estación 3:
   - Código: SCERDO
   - Nombre: Coyhaique
   - Fecha Inicio: 2026-10-10
   - Fecha Fin: 2026-10-25
5. Guardar estaciones
6. Solicitud pasa a "PENDIENTE_COORDINADOR"

**Resultado Esperado:**
- ✓ 3 estaciones guardadas
- ✓ Fechas independientes sin sobreposición obligatoria
- ✓ Cada estación conserva sus propias fechas
- ✓ Solicitud en estado correcto

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación BD:**
```sql
SELECT estacion_codigo, fecha_inicio, fecha_fin FROM aocr_tbsolicitud_estacion 
WHERE solicitud_id = [SOLICITUD_ID] ORDER BY estacion_codigo;

-- Resultado esperado:
-- SCERDO  | 2026-10-10 | 2026-10-25
-- SCEL    | 2026-10-01 | 2026-10-15
-- SCVD    | 2026-10-05 | 2026-10-30
```

**Screenshot:** `fase2_ac02_estaciones.png`

---

## FASE 3: VALIDACIÓN COORDINADOR (30 min)

### Objetivo
Verificar que Coordinador recibe solicitud en bandeja, revisa documentos (AC-04) y puede remitir a DIRCAV.

### Caso 3: AC-04 Coordinador Revisión — Happy Path

**Precondición:** Coordinador logueado, solicitud en "PENDIENTE_COORDINADOR"

**Pasos:**
1. Acceder bandeja "Revisión Documental"
2. Solicitud visible con estado "PENDIENTE_COORDINADOR"
3. Revisar datos:
   - ✓ Operador: "Operador Test 1"
   - ✓ Estación 1: SCEL (2026-10-01 a 2026-10-15)
   - ✓ Estación 2: SCVD (2026-10-05 a 2026-10-30)
   - ✓ Estación 3: SCERDO (2026-10-10 a 2026-10-25)
4. Acción: "Remitir a DIRCAV"
5. Solicitud pasa a "PENDIENTE_DIRCAV"

**Resultado Esperado:**
- ✓ Solicitud visible en bandeja Coordinador
- ✓ Estaciones con fechas independientes visibles
- ✓ Remisión exitosa
- ✓ Solicitud en bandeja DIRCAV

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase3_ac04_coordinador.png`

---

### Caso 4: AC-04 Coordinador Devolución (Opcional - Email Liberado AC-01)

**Precondición:** Coordinador logueado, solicitud en "PENDIENTE_COORDINADOR"

**Pasos:**
1. Acceder bandeja "Revisión Documental"
2. Acción: "Devolver al Operador"
3. Ingresar comentario (obligatorio): "Revisar documentación de AOC"
4. Guardar
5. Solicitud pasa a "DEVUELTO_OPERADOR"
6. Email notificación enviado a Operador

**Resultado Esperado:**
- ✓ Devolución exitosa
- ✓ Operador recibe notificación
- ✓ Email RT estado: "devuelto" (liberado para reutilizar)

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación AC-01 (Email Liberado):**
```sql
SELECT correo, estado_designacion_rt, correo_liberado 
FROM usuario WHERE correo = 'operador@aocr.cl';

-- Resultado esperado:
-- operador@aocr.cl | devuelto | TRUE
```

**Screenshot:** `fase3_ac04_devolucion.png`

---

## FASE 4: VALIDACIÓN DIRCAV (1 hora)

### Objetivo
Verificar DIRCAV aceptación documental (AC-05), designación inspector con reasignación (AC-05), y generación PDF (AC-06).

### Caso 5: AC-05 DIRCAV Aceptación Documental

**Precondición:** DIRCAV logueado, solicitud en "PENDIENTE_DIRCAV"

**Pasos:**
1. Acceder bandeja "Solicitudes Pendientes"
2. Sección "Doc. Pendiente Aceptación"
3. Solicitud visible con estado "PENDIENTE_DIRCAV"
4. Acción: "Aceptar Documentación"
5. Solicitud pasa a "DOCUMENTACION_ACEPTADA_DIRCAV"

**Resultado Esperado:**
- ✓ Solicitud visible en bandeja DIRCAV
- ✓ Aceptación exitosa
- ✓ Solicitud en sección "Designaciones Pendientes"

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase4_ac05_aceptacion.png`

---

### Caso 6: AC-05 DIRCAV Designación Inspector + Reasignación

**Precondición:** DIRCAV logueado, solicitud en "DOCUMENTACION_ACEPTADA_DIRCAV"

**Pasos:**

**6a. Designación Inicial:**
1. Acceder sección "Designaciones Pendientes"
2. Acción: "Designar Inspector"
3. Estación 1 (SCEL):
   - Inspector Principal: "Inspector 1"
   - Inspector Apoyo: "Inspector 2"
4. Estación 2 (SCVD):
   - Inspector Principal: "Inspector 1"
   - Inspector Apoyo: "Inspector 3"
5. Estación 3 (SCERDO):
   - Inspector Principal: "Inspector 2"
   - Inspector Apoyo: "Inspector 1"
6. Guardar designación
7. Solicitud pasa a "DESIGNACION_PENDIENTE_FIRMA_DIRCAV"

**Resultado Esperado:**
- ✓ 3 estaciones con inspectores designados
- ✓ Designación vigente creada (vigente=TRUE, v1)
- ✓ Solicitud en estado correcto

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación BD:**
```sql
SELECT solicitud_id, estacion_id, inspector_id, vigente, version 
FROM aocr_tbdesignacion_inspector 
WHERE solicitud_id = [SOLICITUD_ID] ORDER BY estacion_id, version DESC;

-- Resultado esperado: 3 filas, todas vigente=TRUE, version=1
```

**Screenshot:** `fase4_ac05_designacion.png`

---

**6b. Reasignación Inspector (AC-05):**
1. En sección "Designaciones Pendientes"
2. Estación 1 (SCEL): Cambiar Inspector Apoyo "Inspector 2" → "Inspector 3"
3. Guardar reasignación
4. Sistema:
   - Crea v2: vigente=TRUE (Inspector 3 nuevo)
   - Marca v1: vigente=FALSE (Inspector 2 antiguo)

**Resultado Esperado:**
- ✓ Reasignación exitosa
- ✓ Versionado funciona (v1 histórica, v2 vigente)
- ✓ Inspector 3 ahora es vigente para Estación 1

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación BD (Reasignación):**
```sql
SELECT solicitud_id, estacion_id, inspector_id, vigente, version 
FROM aocr_tbdesignacion_inspector 
WHERE solicitud_id = [SOLICITUD_ID] AND estacion_id = [SCEL_ESTACION_ID] 
ORDER BY version DESC;

-- Resultado esperado: 2 filas
--   [SCEL_ID] | 44444444 (Inspector 3) | TRUE  | 2
--   [SCEL_ID] | 33333333 (Inspector 2) | FALSE | 1
```

**Screenshot:** `fase4_ac05_reasignacion.png`

---

### Caso 7: AC-06 PDF Designación DIRCAV

**Precondición:** DIRCAV logueado, solicitud con designación vigente, estado "DESIGNACION_PENDIENTE_FIRMA_DIRCAV"

**Pasos:**

**7a. Vista Previa PDF:**
1. Acceder "Detalle Designación"
2. Acción: "Vista Previa"
3. PDF generado (Firefox/Chrome)
4. Validar contenido:
   - ✓ Membrete DGAC
   - ✓ Estación 1 SCEL (2026-10-01 a 2026-10-15)
   - ✓ Estación 2 SCVD (2026-10-05 a 2026-10-30)
   - ✓ Estación 3 SCERDO (2026-10-10 a 2026-10-25)
   - ✓ Inspector 1, Inspector 2, Inspector 3 listados
   - ✓ Marca de agua "VISTA PREVIA"

**Resultado Esperado:**
- ✓ PDF generado exitosamente
- ✓ Estaciones + fechas independientes visibles
- ✓ Inspectores vigentes (v2, no v1) en PDF
- ✓ Marca de agua presente

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase4_ac06_vista_previa.png`

---

**7b. Firma PDF:**
1. Acción: "Firmar Designación"
2. Ingresar certificado DIRCAV + contraseña
3. Sistema:
   - Genera SHA-256 del PDF
   - Firma digitalmente
   - Estado: "DESIGNACION_PENDIENTE_FIRMA_DIRCAV" → "FIRMADO"
   - Persistencia: ruta_pdf, hash_documento, usuario_firma, fecha_firma
4. Solicitud pasa a "PENDIENTE_INSPECCION"

**Resultado Esperado:**
- ✓ Firma exitosa
- ✓ PDF firmado digitalmente
- ✓ Solicitud en "PENDIENTE_INSPECCION"
- ✓ Inspectores notificados (email)

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación BD:**
```sql
SELECT solicitud_id, estado, fecha_firma, usuario_firma, hash_documento 
FROM aocr_tbdesignacion_inspector 
WHERE solicitud_id = [SOLICITUD_ID] AND vigente = TRUE;

-- Resultado esperado:
-- [SOLICITUD_ID] | FIRMADO | 2026-09-07 14:35:22 | dircav | a3c5e7f9...
```

**Screenshot:** `fase4_ac06_firma.png`

---

## FASE 5: VALIDACIÓN INSPECTOR — LVs INDEPENDIENTES (2 horas)

### Objetivo
Verificar que Inspector puede crear LVs independientes por estación (AC-07), completarlas con validación (AC-08), y firmarlas.

### Caso 8: AC-07 LV Independencia Multi-Estación

**Precondición:** Inspector 1 logueado, inspección asignada, solicitud en "PENDIENTE_INSPECCION"

**Pasos:**

**8a. Estación 1 (SCEL) — LV Completa:**
1. Acceder "Bandeja Inspector"
2. Seleccionar Inspección
3. Ir sección "Lista de Verificación"
4. Cambiar a pestaña "Estación 1 (SCEL)"
5. Sistema: ObtenerOInicializarListaParaEstacion (idempotente, no duplica)
6. LV estado: "LV_BORRADOR"
7. Cargar catálogo RDAC 129 (14 preguntas):
   - 129-1: Formulario solicitud
   - 129-2: Descripción operación
   - ... (14 preguntas)
8. Responder todos los ítems:
   - Pregunta 129-1:
     - Cumplimiento: SATISFACTORIO
     - Implementacion: IMPLEMENTADO
   - Pregunta 129-2:
     - Cumplimiento: NO_SATISFACTORIO
     - Implementacion: IMPLEMENTADO
     - Observación: "Falta manual actualizado — se requiere en próxima inspección"
   - Preguntas 129-3 a 129-14: Completar con estados válidos
9. Acción: "Guardar Respuestas"
10. Sistema valida AC-08:
    - Cabecera obligatoria ✓
    - EstadoCumplimiento + EstadoImplementacion ✓
    - Observación obligatoria para NO_* ✓
    - Observación NO sustituye resultado ✓
11. LV estado: "LV_COMPLETA"
12. Acción: "Finalizar"
13. LV estado: "LV_FINALIZADA"
14. Acción: "Firmar"
15. Ingresar certificado Inspector 1 + contraseña
16. LV estado: "LV_FIRMADA"

**Resultado Esperado (Est.1):**
- ✓ LV creada (idempotencia validada)
- ✓ 14 preguntas RDAC 129 cargadas
- ✓ AC-08 validación exhaustiva passou
- ✓ LV firmada exitosamente
- ✓ Respuestas conservadas en BD

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación BD (Est.1):**
```sql
SELECT codigo_lista_verificacion, estado_lista, solicitud_id, estacion_id, vigente, version
FROM aocr_tblv_operacional_eae 
WHERE solicitud_id = [SOLICITUD_ID] AND estacion_id = [SCEL_ESTACION_ID];

-- Resultado esperado: 1 fila
-- [LV_CODE] | LV_FIRMADA | [SOLICITUD_ID] | [SCEL_ID] | TRUE | 1
```

**Screenshot:** `fase5_ac07_est1_completa.png`

---

**8b. Estación 2 (SCVD) — LV Independencia:**
1. Cambiar a pestaña "Estación 2 (SCVD)"
2. Sistema: ObtenerOInicializarListaParaEstacion (crea LV nueva, no reutiliza Est.1)
3. LV estado: "LV_BORRADOR"
4. Responder con respuestas DIFERENTES a Est.1:
   - Pregunta 129-1: NO_SATISFACTORIO (vs. SATISFACTORIO en Est.1)
   - Pregunta 129-2: SATISFACTORIO (vs. NO_SATISFACTORIO en Est.1)
   - Observaciones: Diferentes
   - Resto: Estados independientes
5. Guardar, finalizar, firmar (igual a Est.1)
6. LV estado: "LV_FIRMADA"

**Resultado Esperado (Est.2 — Independencia):**
- ✓ LV2 separada de LV1
- ✓ Respuestas diferentes entre Est.1 y Est.2
- ✓ Contexto cambio → no contaminación de datos
- ✓ LV2 firmada exitosamente

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Verificación Independencia:**
```sql
-- Est.1 respuesta pregunta 129-1
SELECT codigo_lista_verificacion, estado_cumplimiento, estado_implementacion 
FROM aocr_tblv_operacional_eae_item 
WHERE codigo_pregunta = '129-1' AND codigo_lista_verificacion IN (
  SELECT codigo_lista_verificacion FROM aocr_tblv_operacional_eae 
  WHERE solicitud_id = [SOLICITUD_ID]
) ORDER BY codigo_lista_verificacion;

-- Resultado esperado: 2 filas, DIFERENTES cumplimiento
-- [LV1_CODE] | SATISFACTORIO      | IMPLEMENTADO
-- [LV2_CODE] | NO_SATISFACTORIO   | IMPLEMENTADO
```

**Screenshot:** `fase5_ac07_est2_independencia.png`

---

**8c. Regresión Est.1 — Inmutabilidad:**
1. Cambiar a pestaña "Estación 1 (SCEL)"
2. Validar: LV1 aún en "LV_FIRMADA"
3. Respuestas de Est.1 INTACTAS:
   - 129-1: SATISFACTORIO (no cambió)
   - 129-2: NO_SATISFACTORIO + observación (no cambió)
4. Intentar editar pregunta 129-1 → BLOQUEADO (LV firmada)
5. Mensaje: "No se puede editar LV firmada"

**Resultado Esperado (Inmutabilidad):**
- ✓ LV1 no contaminada por LV2
- ✓ Datos preservados exactamente
- ✓ LV1 inmutable (no editable post-firma)
- ✓ 403 Forbidden al intentar editar

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase5_ac07_est1_inmutable.png`

---

**8d. Estación 3 (SCERDO) — Reinspección:**
1. Cambiar a pestaña "Estación 3 (SCERDO)"
2. Sistema: ObtenerOInicializarListaParaEstacion
3. Detecta: reinspección (misma estación que prior)
4. Crea: LV nueva (v2), marca LV anterior como histórica (v1)
5. LV3 estado: "LV_BORRADOR"
6. Responder, completar, firmar (igual)
7. LV3 estado: "LV_FIRMADA"

**Resultado Esperado (Est.3 — Reinspección):**
- ✓ LV3 versión 2 creada
- ✓ LV3v1 preservada como histórica
- ✓ Versionado funciona (vigente=TRUE para v2)
- ✓ LV3v2 firmada exitosamente

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Validación Versionado:**
```sql
SELECT codigo_lista_verificacion, version, vigente, estado_lista 
FROM aocr_tblv_operacional_eae 
WHERE solicitud_id = [SOLICITUD_ID] AND estacion_id = [SCERDO_ESTACION_ID]
ORDER BY version DESC;

-- Resultado esperado: 2 filas
-- [LV3v2_CODE] | 2 | TRUE  | LV_FIRMADA
-- [LV3v1_CODE] | 1 | FALSE | LV_FIRMADA (histórica)
```

**Screenshot:** `fase5_ac07_est3_reinspeccion.png`

---

### Caso 9: AC-08 Validación Obligatoria — Observación Obligatoria

**Precondición:** Inspector logueado, LV en edición, pregunta con NO_SATISFACTORIO/NO_IMPLEMENTADO

**Pasos:**
1. En LV cualquiera, seleccionar pregunta
2. Cumplimiento: NO_SATISFACTORIO
3. Implementacion: IMPLEMENTADO
4. Dejar observación VACÍA
5. Acción: "Guardar Respuestas"
6. Sistema valida AC-08:
   - NO_SATISFACTORIO SIN observación
   - Validación FALLA
7. Mensaje de error: "Seleccione una observación para NO_SATISFACTORIO"
8. LV estado: EN_PROCESO (no COMPLETA)
9. Agregar observación: "Falta documento X"
10. Guardar nuevamente
11. Validación PASSA
12. LV estado: LV_COMPLETA

**Resultado Esperado (AC-08):**
- ✓ Observación obligatoria para negativos
- ✓ Validación exhaustiva, no bypasseable
- ✓ Error claro para usuario
- ✓ Después de agregar observación, pasa validación

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase5_ac08_observacion_obligatoria.png`

---

### Caso 10: AC-08 Validación — Observación NO Sustituye Resultado

**Precondición:** Inspector logueado, LV en edición

**Pasos:**
1. Seleccionar pregunta
2. EstadoCumplimiento: VACÍO
3. EstadoImplementacion: IMPLEMENTADO
4. Observación: "Esta es la implementación" (llenar)
5. Guardar respuestas
6. Sistema valida: EstadoCumplimiento vacío
7. Validación FALLA
8. Mensaje: "Seleccione resultado de cumplimiento (observación no sustituye resultado)"
9. LV estado: EN_PROCESO
10. Agregar EstadoCumplimiento: SATISFACTORIO
11. Guardar nuevamente → PASSA

**Resultado Esperado (AC-08):**
- ✓ Observación NO reemplaza resultado vacío
- ✓ Ambos campos necesarios
- ✓ Error claro
- ✓ Sistema rechaza bypass

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase5_ac08_observacion_no_sustituye.png`

---

## FASE 6: VALIDACIÓN INTEGRACIÓN FINAL (30 min)

### Objetivo
Verificar que flujo end-to-end funciona: LVs firmadas desbloquean Informe Técnico (AC-06 + AC-08 integración).

### Caso 11: AC-06 + AC-08 Bloqueo Informe Técnico

**Precondición:** Inspector con 3 LVs, 2 FIRMADAS y 1 EN_PROCESO

**Pasos:**
1. Inspector intenta generar "Informe Técnico"
2. Sistema valida: ¿Todas LVs FIRMADAS?
3. Resultado: 2 FIRMADAS, 1 EN_PROCESO
4. Validación FALLA
5. HTTP 409 Conflict
6. Mensaje: "No se puede generar Informe Técnico: hay LVs pendientes. Estaciones pendientes: [Est.3]. Completa y firma todas las LVs para continuar."

**Resultado Esperado (Bloqueo):**
- ✓ Informe bloqueado si LV EN_PROCESO
- ✓ Mensaje detallado de qué estaciones faltan
- ✓ 409 Conflict HTTP status
- ✓ Inspector sabe exactamente qué debe completar

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase6_ac06_bloqueo_409.png`

---

### Caso 12: AC-06 + AC-08 Desbloqueo Informe Técnico

**Precondición:** Inspector completa y firma última LV (Est.3)

**Pasos:**
1. Inspector: Cambiar a Est.3, completar las respuestas pendientes
2. Validación AC-08: PASSA (completa)
3. LV3 estado: LV_COMPLETA → LV_FINALIZADA → LV_FIRMADA
4. Todas 3 LVs: FIRMADAS ✓✓✓
5. Inspector intenta generar "Informe Técnico" nuevamente
6. Sistema valida: ¿Todas LVs FIRMADAS?
7. Resultado: SÍ, todas FIRMADAS
8. Validación PASSA
9. Informe Técnico generado
10. Solicitud continúa a siguiente fase

**Resultado Esperado (Desbloqueo):**
- ✓ Informe desbloqueado
- ✓ Generación exitosa
- ✓ Flujo puede continuar
- ✓ AC-06 + AC-08 integración verificada

**Resultado Real:**
- ☐ ✓ / ☐ ✗
- Observaciones: _________________________________________________________________

**Screenshot:** `fase6_ac06_desbloqueo_ok.png`

---

## RESUMEN RESULTADOS

### Matriz de Casos Pasados

| Caso # | AC | Descripción | Status | Observaciones |
|---|---|---|---|---|
| 1 | AC-01 | Email RT — Validación contextual | ☐ PASS ☐ FAIL | |
| 2 | AC-02 | Estaciones independientes (3) | ☐ PASS ☐ FAIL | |
| 3 | AC-04 | Coordinador revisión — remitir | ☐ PASS ☐ FAIL | |
| 4 | AC-04 | Coordinador — devolución + email liberado | ☐ PASS ☐ FAIL | |
| 5 | AC-05 | DIRCAV aceptación documental | ☐ PASS ☐ FAIL | |
| 6 | AC-05 | DIRCAV designación + reasignación | ☐ PASS ☐ FAIL | |
| 7 | AC-06 | PDF generación + firma DIRCAV | ☐ PASS ☐ FAIL | |
| 8 | AC-07 | LV independencia multi-estación (8a-8d) | ☐ PASS ☐ FAIL | |
| 9 | AC-08 | Observación obligatoria | ☐ PASS ☐ FAIL | |
| 10 | AC-08 | Observación NO sustituye resultado | ☐ PASS ☐ FAIL | |
| 11 | AC-06+08 | Bloqueo Informe si LV EN_PROCESO | ☐ PASS ☐ FAIL | |
| 12 | AC-06+08 | Desbloqueo Informe si todas FIRMADAS | ☐ PASS ☐ FAIL | |

**Total Casos:** 12  
**Casos Pasados:** _____ / 12  
**Casos Fallados:** _____ / 12  
**Pass Rate:** ______ %

---

## ANÁLISIS DE BLOQUEADORES

### Bloqueadores Críticos Encontrados

| # | Descripción | Severidad | Resolución | Status |
|---|---|---|---|---|
| - | - | - | - | - |

**Nota:** Si no hay bloqueadores críticos, marcar "CERO BLOQUEADORES"

### Bloqueadores No-Críticos (Mejoras)

| # | Descripción | Prioridad | Notas |
|---|---|---|---|
| - | - | - | - |

---

## MÉTRICAS FINALES

| Métrica | Objetivo | Real | Status |
|---|---|---|---|
| **Casos Pasados** | 12/12 | ___/12 | ⏳ |
| **Pass Rate** | 100% | ___% | ⏳ |
| **Bloqueadores Críticos** | 0 | ___ | ⏳ |
| **Migraciones SQL** | 7/7 aplicadas | ✓ | ✅ |
| **Compilación** | Release exitosa | ✓ | ✅ |
| **Tiempo UAT** | 4-6 horas | __h __m | ⏳ |

---

## RECOMENDACIÓN FINAL

### ☐ PASS — Recomendar promoción a PRE-PRODUCCIÓN
- Todas pruebas pasadas
- Cero bloqueadores críticos
- Migraciones exitosas
- Compilación sin errores

**Acciones Post-PASS:**
- [ ] Auditoría AC-09 a AC-12 (Bloque 3)
- [ ] Preparar deployment PRE-PRODUCCIÓN
- [ ] Crear plan rollback PRE-PRODUCCIÓN

---

### ☐ FAIL — Requiere correcciones
- X casos fallados
- Bloqueadores críticos encontrados: ______________________

**Acciones Post-FAIL:**
- [ ] Análisis causa raíz (RCA)
- [ ] Crear tickets en Redmine
- [ ] Fix + re-test (ciclo 2)
- [ ] Documentar lecciones aprendidas

---

## ANEXOS

### Anexo A: Screenshots Recolectadas

```
fase2_ac01_email_rt.png
fase2_ac02_estaciones.png
fase3_ac04_coordinador.png
fase3_ac04_devolucion.png
fase4_ac05_aceptacion.png
fase4_ac05_designacion.png
fase4_ac05_reasignacion.png
fase4_ac06_vista_previa.png
fase4_ac06_firma.png
fase5_ac07_est1_completa.png
fase5_ac07_est2_independencia.png
fase5_ac07_est1_inmutable.png
fase5_ac07_est3_reinspeccion.png
fase5_ac08_observacion_obligatoria.png
fase5_ac08_observacion_no_sustituye.png
fase6_ac06_bloqueo_409.png
fase6_ac06_desbloqueo_ok.png
```

---

### Anexo B: Queries de Validación BD

Todas las queries SQL usadas durante validación están documentadas en secciones de cada caso.

---

### Anexo C: Logs del Sistema

- Logs compilación: migration_20260907_*.log
- Logs aplicación: C:\staging\aocr_app\logs\*
- Logs base de datos: PostgreSQL logs

---

## FIRMA

**Responsable UAT:** ________________________  
**Fecha Inicio:** ________________________  
**Fecha Fin:** ________________________  
**Duración Total:** ________________________  

**Aprobación QA/UAT:**
- ☐ Paso / ☐ No pasó

**Observaciones Finales:**

_________________________________________________________________

_________________________________________________________________

_________________________________________________________________

---

**Reporte Preparado:** 2026-09-07  
**Versión:** 1.0 Template  
**Status:** ⏳ EN EJECUCIÓN
