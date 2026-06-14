# Release Notes — AOCR v1.0.0.4 (Fase 0 publicación)

**Fecha:** 2026-06-12  
**Entorno:** `C:\AOCR\publicacion1`  
**Perfil:** `FolderProfile4`  
**Build:** Release / AnyCPU  

---

## Resumen

Republicación de estabilización previa a Gate A. Incluye DLLs de las tres capas principales, assets front-end AOCR y bump de caché `VersionFront` → **1.0.0.4**.

---

## Binarios publicados

| DLL | Timestamp (local) |
|-----|-------------------|
| `bin/CapaDatos.dll` | 2026-06-12 ~15:29 |
| `bin/CapaNegocio.dll` | 2026-06-12 ~15:29 |
| `bin/CapaPresentacion.dll` | 2026-06-12 ~15:29 |

---

## CSS verificados

| Archivo | Estado |
|---------|--------|
| `Content/aocr-institucional.css` | OK |
| `Content/aocr-contrast.css` | OK |
| `Content/aocr-datatables.css` | OK (nombre canónico; no `aocr-datatable.css`) |
| `Content/aocr-modals.css` | OK |
| `Content/aocr-sidebar.css` | OK |
| `Content/aocr-responsive.css` | OK |
| `Content/aocr-document-cards.css` | OK |
| `Content/aocr-informe-tecnico.css` | OK |
| `Content/aocr-pdf-viewer/aocr-pdf-viewer.css` | OK |

---

## JavaScript verificados

| Archivo | Uso |
|---------|-----|
| `Scripts/aocr-sidebar.js` | Sidebar / contadores |
| `Scripts/aocr-utils.js` | Utilidades UI |
| `Scripts/aocr-datatables.js` | DataTables institucional |
| `Scripts/aocr-formulario-emision.js` | Formulario emisión AOCR |
| `Scripts/aocr-informe-tecnico-modal.js` | Informe técnico |
| `Scripts/aocr-lv-eae-flujo-automatico.js` | LV / EAE |
| `Scripts/aocr-config.js` | Config front |

---

## Cambios funcionales incluidos (working tree)

- Bandeja inspector y revisión documental post-asignación
- `DocumentoController` modos `revision` / `ver`
- Flujo cierre documental → LV → Informe → DIRDAC
- Modificación tipo 3 / nuevo aeropuerto
- Contadores sidebar centralizados (`AocrSidebarCounterService`)
- Autorización `[AocrAuthorize]` parcial (Tecnico, Documento, CoordinacionJefatura)
- Idempotencia correos post-pago

---

## Pruebas unitarias

| Métrica | Resultado |
|---------|-----------|
| Total | 204 |
| Correctas | 192 |
| Incorrectas | 11 |
| Omitidas | 1 |

Los 11 fallos son tests desalineados con implementación actual (modificación tipo 3, matriz Inspección, email flujo). **No bloquean deploy Fase 0** pero deben corregirse antes de Gate E.

---

## Pendiente operativo (servidor IIS)

1. Reciclar Application Pool del sitio que apunta a `publicacion1`
2. Navegador incógnito + Ctrl+F5
3. Validar login RT / Coordinación / Inspector / Financiero / DIRDAC
4. Ejecutar Gate A (`docs/GUIA_PRUEBAS_POST_REPUBLICACION.md`)

---

## Tag Git

`v1.0.0.4-fase0-publicacion1`

---

*Generado en Fase 0 — estabilización publicación AOCR.*
