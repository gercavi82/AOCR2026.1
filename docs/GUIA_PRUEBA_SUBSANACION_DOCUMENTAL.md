# Guía de prueba — Subsanación documental v2

**Fecha:** 2026-06-13  
**Entorno:** `C:\AOCR\publicacion1`  
**Base de datos:** `dgac_des` @ `172.20.16.55` (limpia operativamente; usuarios conservados)

---

## 0. Preparación (obligatorio)

### 0.1 Republicación

Última publicación Release → `FolderProfile4`:

- `bin\CapaPresentacion.dll` — verificar timestamp reciente
- `Views\Documento\Lista.cshtml`
- `Views\SolicitudAOCR\Subsanar.cshtml`

### 0.2 Reciclar IIS

1. Confirmar que el sitio IIS apunta a `C:\AOCR\publicacion1`.
2. Reciclar el **Application Pool** del sitio (o `iisreset` en el servidor).
3. Navegador: ventana **incógnito** + **Ctrl+F5** en cada pantalla.

### 0.3 Usuarios de prueba (BD actual)

| Rol | `codigousuario` | Nombre | Correo |
|-----|-----------------|--------|--------|
| RT / Solicitante | `GACAJAS` | GERMAN ALBERTO CAJAS | mancho2002@hotmail.com |
| RT alternativo | `JAJARAMIL1` | JORGE ANDRÉS JARAMILLO | jorgandres_@hotmail.com |
| Inspector | `1709565459` | LUIS WILKIR CATOTA | luis.catota@aviacioncivil.gob.ec |
| Inspector alt. | `1709061814` | EDISON JAVIER AVILA | edison.avila@aviaiconcivil.gob.ec |
| Financiero | `GEN_FINANCIERO` | Financiero Genérico | german.cajas@aviacioncivil.gob.ec |
| Coordinación | `GEN_COORDINACION` | Coordinación Genérica | gcajas1955@utm.edu.ec |
| Admin (todos los roles) | `USU_ADMIN` | German Cajas | gercavi82@gmail.com |

> Use la clave que ya tenga configurada cada usuario en el entorno. Si no recuerda alguna, restablezca desde administración de usuarios.

### 0.4 Log de evidencias

`C:\AOCR\publicacion1\App_Data\Logs\AOCR_YYYYMMDD.log`

Consulta BD post-prueba:

```sql
SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud = :id;
SELECT codigo_documento, tipo_documento, estado, version FROM aocr_tbdocumento WHERE codigo_solicitud = :id ORDER BY codigo_documento;
SELECT tipo_notificacion, event_key, estado FROM email_queue WHERE solicitud_id = :id ORDER BY id DESC;
```

---

## 1. Preparar solicitud hasta revisión del Inspector

Como la BD está vacía, hay que crear un trámite nuevo. Resumen mínimo:

| Paso | Rol | Acción | URL |
|------|-----|--------|-----|
| 1 | RT (`GACAJAS`) | Crear solicitud emisión | `/SolicitudAOCR/FormularioEmisionAOCR?tipoSolicitud=1` |
| 2 | RT | Crear orden recaudación + comprobante | `/OrdenRecaudacion/Nueva` |
| 3 | Financiero | Aprobar pago | `/Financiero/Index` |
| 4 | RT | Subir **≥ 3 documentos** distintos | `/Documento/Subir?solicitudId={id}` |
| 5 | RT | Enviar a coordinación | Detalle solicitud → acción envío |
| 6 | Coordinación | Revisar y firmar aceptación documental | `/CoordinacionJefatura/RevisionVerificacion` |
| 7 | Coordinación | Asignar inspector `1709565459` | `/Tecnico/Index` → Asignar |

**Anotar:** `{SOLICITUD_ID}` e `{INSPECCION_ID}` generados.

Estado esperado antes de la prueba de subsanación: solicitud en **En Inspección**, inspector asignado.

---

## 2. Escenario principal — Inspector devuelve parte de los documentos

### Paso 2.1 — Abrir revisión documental (Inspector)

| Campo | Valor |
|-------|-------|
| Usuario | `1709565459` |
| URL correcta | `/Documento/Lista?solicitudId={SOLICITUD_ID}&modo=revision&origen=revision-documental` |
| URL incorrecta ❌ | `/Documento/Lista?solicitudId={SOLICITUD_ID}&modo=ver` (solo lectura) |

**Verificar en pantalla:**

- Combos por fila: **Aceptado** / **Devuelto** / **Observado**
- Botón **Guardar revisión documental** visible al pie
- **No** deben aparecer chips "Solo lectura"

### Paso 2.2 — Decidir documentos

Ejemplo con 3 documentos cargados:

| Documento | Decisión | Observación |
|-----------|----------|-------------|
| Doc 1 | **Aceptado** | (vacía) |
| Doc 2 | **Devuelto** | "Falta firma en página 2" |
| Doc 3 | **Devuelto** | "Formato incorrecto" |

Clic en **Guardar revisión documental**.

**Resultado esperado:**

- Mensaje: solicitud devuelta al RT con observaciones
- Estado solicitud → **Observada**
- Docs 1 → `APROBADO`; Docs 2 y 3 → `RECHAZADO` / devueltos

### Paso 2.3 — Correo al RT (BD)

En `email_queue`:

| Campo | Esperado |
|-------|----------|
| `tipo_notificacion` | `DOCUMENTOS_DEVUELTOS_INSPECTOR` |
| `event_key` | `DOCUMENTOS_DEVUELTOS_INSPECTOR_{SOLICITUD_ID}_{id_doc2}_{id_doc3}` (IDs ordenados) |
| Duplicados | **1 fila por destinatario**; repetir guardado no debe duplicar |

Asunto: `Sistema AOCR - Documentos devueltos para subsanación`

---

## 3. Escenario RT — Subsanar solo lo devuelto

### Paso 3.1 — Login RT

Usuario: `GACAJAS` (propietario de la solicitud creada en paso 1).

### Paso 3.2 — Pantalla Subsanar

URL: `/SolicitudAOCR/Subsanar/{SOLICITUD_ID}`

**Verificar dos secciones:**

1. **Documentos devueltos** — inputs de carga habilitados (Doc 2 y Doc 3)
2. **Documentos bloqueados** — Doc 1 visible, **sin** input de carga, mensaje institucional

### Paso 3.3 — Bloqueos backend

| Prueba | Acción | Esperado |
|--------|--------|----------|
| B1 | RT intenta `/Documento/Subir?solicitudId={id}` | Redirect a `/Subsanar/{id}` con mensaje de error |
| B2 | RT sube archivo solo para Doc 2 y Doc 3 en Subsanar | OK |
| B3 | RT intenta omitir un documento devuelto | Error: debe subsanar todos los devueltos |

### Paso 3.4 — Enviar subsanación

Subir PDFs corregidos para Doc 2 y Doc 3 → **Enviar subsanación**.

**Resultado esperado:**

- Estado solicitud → **Subsanada**
- Doc 1 sigue bloqueado / aceptado
- Docs 2 y 3: versión anterior marcada `VERSION_ANTERIOR`; nueva versión `PENDIENTE_REVISION_SUBSANACION`
- Correo inspector: `DOCUMENTACION_SUBSANADA_RT` (sin duplicar `AOCR_CAMBIO_ESTADO`)

---

## 4. Checklist de cierre (marcar al terminar)

| # | Criterio | ☐ |
|---|----------|---|
| 1 | Inspector usa `modo=revision` y ve combos + botón guardar | ☐ |
| 2 | RT en `modo=ver` con Observada redirige a Subsanar | ☐ |
| 3 | Subsanar muestra devueltos vs bloqueados | ☐ |
| 4 | RT no puede subir por `/Documento/Subir` en Observada | ☐ |
| 5 | Correo RT consolidado con `event_key` único | ☐ |
| 6 | Versionamiento: anterior `VERSION_ANTERIOR`, nueva pendiente revisión | ☐ |
| 7 | Solicitud pasa a `Subsanada` tras envío RT | ☐ |
| 8 | Inspector recibe notificación de documentación subsanada | ☐ |

**Subsanación v2 = 100% operativo** cuando los 8 ítems están marcados.

---

## 5. Problemas frecuentes

| Síntoma | Causa probable | Solución |
|---------|----------------|----------|
| Banner "Visualización sincronizada" / Solo lectura | URL con `modo=ver` | Usar `modo=revision` o botón **Abrir revisión documental** |
| No aparecen combos | Sitio IIS no apunta a `publicacion1` o caché | Reciclar App Pool + Ctrl+F5 |
| Pantalla distinta (columnas ADR, pestañas extra) | Instancia IIS distinta al deploy | Verificar physical path del sitio |
| Sin correo | Cola `email_queue` no procesada | Revisar procesador SMTP / filas PENDIENTE |

---

## 6. Referencias

- `docs/REPORTE_SUBSANACION_DOCUMENTAL_V2.md` — entrega técnica
- `docs/GUIA_INSPECTOR_SOLICITUD_12.md` — flujo inspector (adaptar `{SOLICITUD_ID}`)
- `docs/GUIA_PRUEBAS_POST_REPUBLICACION.md` — escenarios previos al inspector
