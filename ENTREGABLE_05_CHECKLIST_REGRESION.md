# ENTREGABLE 5 — Checklist de Regresión

## Instrucciones
Ejecutar este checklist **antes de cada deploy** para verificar que las correcciones aplicadas no han regresionado.

Marcar cada ítem: ✅ Pasa | ❌ Falla | ⏭️ N/A

---

## A. Compilación y Deploy

| # | Verificación | Estado |
|---|-------------|--------|
| A1 | `MSBuild AOCR.sln /p:Configuration=Release` compila sin errores (warnings aceptables) | |
| A2 | Todas las DLL se generan: CapaModelo, CapaDatos, CapaNegocio, CapaPresentacion | |
| A3 | El sitio arranca en IIS Express / IIS sin errores 500 en pool | |
| A4 | _LayoutAOCR.cshtml carga correctamente (AdminLTE sidebar, navbar, footer visible) | |

---

## B. Autenticación

| # | Verificación | Estado |
|---|-------------|--------|
| B1 | /Account/Login renderiza formulario sin errores | |
| B2 | Login con credenciales válidas redirige a Dashboard | |
| B3 | Login con credenciales inválidas muestra mensaje de error | |
| B4 | Logout limpia sesión y redirige a Login | |
| B5 | Acceso a /Rol/Index sin login redirige a /Account/Login | |
| B6 | Acceso a /Direccion/Index sin login redirige a /Account/Login | |
| B7 | /OrdenRecaudacion/DiagnosticoSesion sin login redirige (no expone datos) | |

---

## C. Módulo Roles

| # | Verificación | Estado |
|---|-------------|--------|
| C1 | /Rol/Index muestra tabla de roles (DataTable funcional) | |
| C2 | /Rol/Index con BD vacía muestra tabla vacía (NO error 500) | |
| C3 | /Rol/Crear muestra formulario con campos: Nombre, Descripción, Activo | |
| C4 | Submit Crear con datos válidos inserta rol y redirige | |
| C5 | /Rol/Editar/{id} carga datos del rol existente | |
| C6 | Submit Editar actualiza y redirige | |
| C7 | Eliminar via AJAX POST retorna JSON y remueve fila | |
| C8 | Formularios tienen validación client-side (jQuery Validate) | |

---

## D. Módulo Parámetros

| # | Verificación | Estado |
|---|-------------|--------|
| D1 | /Parametro/Index muestra lista de parámetros | |
| D2 | /Parametro/Crear tiene campos: Clave, Valor, Descripción | |
| D3 | Clave se convierte a mayúsculas (JS) | |
| D4 | /Parametro/Editar/{id} muestra Clave como readonly | |
| D5 | Hidden field CodigoParametro presente en form Editar | |

---

## E. Módulo Usuario-Rol

| # | Verificación | Estado |
|---|-------------|--------|
| E1 | /UsuarioRol/Index muestra tabla de usuarios activos | |
| E2 | Botón "Gestionar Roles" navega a /UsuarioRol/AsignarRoles/{id} | |
| E3 | Checkboxes de roles pre-marcados coinciden con BD | |
| E4 | Submit AsignarRoles sincroniza roles (agrega nuevos, remueve desmarcados) | |
| E5 | Botón X rojo (RemoverRol) envía AJAX POST y recarga página | |
| E6 | AntiForgeryToken presente en formulario | |

---

## F. Módulo Orden de Recaudación

| # | Verificación | Estado |
|---|-------------|--------|
| F1 | /OrdenRecaudacionDashboard/Index renderiza sin error 500 | |
| F2 | KPI cards muestran datos (o 0 si no hay) | |
| F3 | DataTable carga datos via AJAX | |
| F4 | Filtros (estado, fecha, número) funcionan | |
| F5 | /OrdenRecaudacion/Editar/{id} carga datos completos | |
| F6 | Error en Editar POST re-renderiza con datos completos de BD | |
| F7 | NO existe archivo debug_nueva.txt tras requests | |

---

## G. Módulo Técnicos

| # | Verificación | Estado |
|---|-------------|--------|
| G1 | /Tecnico/Crear muestra: NombreCompleto, Especialidad, Telefono, Email, Activo | |
| G2 | NO muestra: CodigoSolicitud, CodigoInspector, FechaProgramada | |
| G3 | Submit con datos válidos llama TecnicoBL.Insertar correctamente | |

---

## H. Módulo Informes

| # | Verificación | Estado |
|---|-------------|--------|
| H1 | /Informe/Crear muestra: ResumenEjecutivo, Hallazgos, Conclusiones, etc. | |
| H2 | NO muestra: Titulo, Contenido, CKEditor | |
| H3 | Submit inserta informe correctamente | |

---

## I. Módulo Checklists

| # | Verificación | Estado |
|---|-------------|--------|
| I1 | /Checklist/Crear muestra: Seccion, ItemNumero, Descripcion, Cumple, Criticidad | |
| I2 | Selects de Cumple (Sí/No/N/A) y Criticidad (Alta/Media/Baja) funcionan | |
| I3 | /Checklist/Detalle/{id} links a Editar, Crear Informe, Volver funcionan | |

---

## J. Scripts y Validación

| # | Verificación | Estado |
|---|-------------|--------|
| J1 | jQuery 3.6.4 carga en Layout (inspeccionar `$.fn.jquery` en consola) | |
| J2 | jQuery 3.6.4 carga en Login | |
| J3 | _ValidationScriptsPartial incluye jquery.validate + unobtrusive | |
| J4 | Validación client-side funciona (campo vacío muestra mensaje sin POST) | |
| J5 | DataTables funcional en tablas principales | |
| J6 | SweetAlert2 funcional en confirmaciones de eliminar | |
| J7 | Bootstrap 5 CSS y JS cargan correctamente | |

---

## K. Vistas Generales

| # | Verificación | Estado |
|---|-------------|--------|
| K1 | CERO ocurrencias de `asp-for`, `asp-action`, `asp-controller` en HTML renderizado | |
| K2 | Todos los formularios generan `name=` attributes correctos (verificar F12→Network→Form Data) | |
| K3 | ViewBag.Title se muestra en `<title>` de cada página | |
| K4 | Links de navegación (`@Url.Action`) generan URLs correctas | |

---

## Resultado Global

| Sección | Total | Pass | Fail | N/A |
|---------|-------|------|------|-----|
| A. Compilación | 4 | | | |
| B. Autenticación | 7 | | | |
| C. Roles | 8 | | | |
| D. Parámetros | 5 | | | |
| E. Usuario-Rol | 6 | | | |
| F. Ord. Recaudación | 7 | | | |
| G. Técnicos | 3 | | | |
| H. Informes | 3 | | | |
| I. Checklists | 3 | | | |
| J. Scripts | 7 | | | |
| K. Vistas | 4 | | | |
| **TOTAL** | **57** | | | |

**Criterio de Deploy:** 0 Fail en secciones A, B, C, E, F. Máximo 2 Fail en otras secciones.
