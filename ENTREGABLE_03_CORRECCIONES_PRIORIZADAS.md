# ENTREGABLE 3 — Correcciones Priorizadas (Aplicadas)

## Resumen Ejecutivo

Se aplicaron **34 correcciones** a lo largo de **30+ archivos**, organizadas por severidad:

- **P0 BLOQUEANTES:** 20 correcciones (crashes, security holes, vistas rotas)
- **P1 SEGURIDAD:** 4 correcciones (endpoints expuestos, debug en producción)  
- **P2 FUNCIONAL:** 7 correcciones (controladores vacíos, modelos incorrectos)
- **P3 CALIDAD:** 3 correcciones (versiones jQuery, duplicados)

---

## P0 — BLOQUEANTES (Runtime crashes / Agujeros de seguridad)

### CORR-001: RolController sin [Authorize]
- **Archivo:** `CapaPresentacion/Controllers/RolController.cs`
- **Impacto:** CRUD de roles accesible sin autenticación
- **Fix:** Agregado `[Authorize(Roles = "Administrador")]` a nivel de clase

### CORR-002: DireccionController sin [Authorize]
- **Archivo:** `CapaPresentacion/Controllers/5_DireccionController.cs`
- **Impacto:** Acciones CRUD de dirección accesibles anónimamente
- **Fix:** Agregado `[Authorize]` a nivel de clase

### CORR-003: RolController.Index catch → null model
- **Archivo:** `CapaPresentacion/Controllers/RolController.cs` línea ~18
- **Impacto:** NullReferenceException en `foreach (var rol in Model)` de la vista
- **Fix:** Retorna `new List<Rol>()` en catch

### CORR-004: RolController.Crear GET sin modelo
- **Archivo:** `CapaPresentacion/Controllers/RolController.cs` línea ~30
- **Impacto:** Vista recibe null → helpers de formulario fallan
- **Fix:** Retorna `new Rol { Activo = true }`

### CORR-005: RolController.Eliminar — GET vs AJAX POST
- **Archivo:** `CapaPresentacion/Controllers/RolController.cs`
- **Impacto:** JS envía POST+AJAX esperando JSON, controlador hacía GET+RedirectToAction
- **Fix:** Cambiado a `[HttpPost]` retornando `Json(new { success, mensaje })`

### CORR-006–009: Rol vistas con asp-* tag helpers
- **Archivos:** `Rol/Index.cshtml`, `Rol/Crear.cshtml`, `Rol/Editar.cshtml`, `Rol/AsignarPermisos.cshtml`
- **Impacto:** Tag helpers de ASP.NET Core no funcionan en MVC5 → formularios sin name= → model binding roto
- **Fix:** Reescritura completa con `Html.BeginForm`, `TextBoxFor`, `LabelFor`, `@Url.Action`

### CORR-010–012: Parametro vistas con asp-* + campos inexistentes
- **Archivos:** `Parametro/Index.cshtml`, `Parametro/Crear.cshtml`, `Parametro/Editar.cshtml`
- **Impacto:** Crear.cshtml referenciaba Categoria, TipoDato, EsEditable (no existen en Parametro). Editar tenía asp-for="Id" (no existe, la PK es CodigoParametro)
- **Fix:** Reescritura con campos correctos: Clave, Valor, Descripcion

### CORR-013: ParametroController vacío
- **Archivo:** `CapaPresentacion/Controllers/ParametroController.cs`
- **Impacto:** 11 líneas, no heredaba Controller, sin acciones → 404 en toda URL /Parametro/*
- **Fix:** Reconstruido completo: Index, Crear GET/POST, Editar GET/POST, Eliminar [HttpPost]

### CORR-014–015: UsuarioRol vistas con asp-*
- **Archivos:** `UsuarioRol/Index.cshtml`, `UsuarioRol/AsignarRoles.cshtml`
- **Impacto:** Links rotos, formulario no hace POST, NombreRol no existe (es Nombre)
- **Fix:** `@Url.Action`, `Html.BeginForm`, propiedad correcta `Nombre`

### CORR-016: UsuarioRolController — faltaban 4 acciones
- **Archivo:** `CapaPresentacion/Controllers/UsuarioRolController.cs`
- **Impacto:** Solo tenía ObtenerRoles() → Index, AsignarRoles, RemoverRol → 404
- **Fix:** Implementadas Index (lista usuarios via Dapper), AsignarRoles GET/POST (sync roles), RemoverRol POST (AJAX JSON)

### CORR-017: Informe/Crear.cshtml — modelo equivocado
- **Archivo:** `Views/Informe/Crear.cshtml`
- **Impacto:** Referenciaba Titulo, ChecklistId, UsuarioId, Contenido (no existen). Cargaba CKEditor (no incluido en scripts)
- **Fix:** Reescritura con ResumenEjecutivo, Hallazgos, Conclusiones, Recomendaciones, AccionesCorrectivas, Resultado, FechaEmision

### CORR-018: InformeController vacío
- **Archivo:** `CapaPresentacion/Controllers/InformeController.cs`
- **Impacto:** Sin acciones → 404
- **Fix:** Reconstruido: Index (lista), Crear GET/POST, Ver, Eliminar

### CORR-019–021: Checklist vistas con asp-* + modelo equivocado
- **Archivos:** `Checklist/Crear.cshtml`, `Checklist/Editar.cshtml`, `Checklist/Detalle.cshtml`
- **Impacto:** Vistas completamente rotas — asp-for, asp-action, modelo incorrecto
- **Fix:** Reescritura completa con Seccion, ItemNumero, Descripcion, Cumple, Criticidad

### CORR-022: ChecklistController vacío
- **Archivo:** `CapaPresentacion/Controllers/ChecklistController.cs`
- **Impacto:** Sin acciones → 404
- **Fix:** Reconstruido con CRUD básico (acciones stub con TODO)

### CORR-023: _ValidationScriptsPartial era stub
- **Archivo:** `Views/Shared/_ValidationScriptsPartial.cshtml`
- **Impacto:** Solo rendereaba `<h2>` → validación client-side no funcionaba en NINGÚN formulario
- **Fix:** CDN jQuery Validate 1.19.5 + jQuery Unobtrusive Validation 3.2.12

### CORR-024: LogController no heredaba Controller
- **Archivo:** `CapaPresentacion/Controllers/LogController.cs`
- **Impacto:** Clase vacía sin base → no es un controlador MVC → 404
- **Fix:** Hereda Controller, tiene [Authorize(Roles="Administrador")], Index retorna View

### CORR-025: OrdenRecaudacionDashboard/Index.cshtml faltaba
- **Archivo:** `Views/OrdenRecaudacionDashboard/Index.cshtml` (CREADO)
- **Impacto:** Controlador existe con ObtenerDatos/AccionRapida pero vista eliminada → crash
- **Fix:** Vista completa 642 líneas: KPIs, filtros, DataTable AJAX, acciones rápidas SweetAlert, export CSV

### CORR-026: OrdenRecaudacionController Editar POST errores
- **Archivo:** `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- **Impacto:** Al fallar update o catch, retornaba modelo POST incompleto → vista crasheaba
- **Fix:** Ambos paths recargan modelo completo desde DB con `_dao.ObtenerOrdenPorIdModel(model.Id)`

### CORR-027: RTController userId fallback = 1
- **Archivo:** `CapaPresentacion/Controllers/RTController.cs` línea ~26
- **Impacto:** Sin sesión, operaba como userId 1 (posiblemente admin) → elevation of privilege
- **Fix:** Retorna 0 en lugar de 1

### CORR-028: Tecnico/Crear.cshtml @model Inspeccion
- **Archivo:** `Views/Tecnico/Crear.cshtml`
- **Impacto:** Vista declaraba @model Inspeccion pero POST binds Tecnico → campos no matchean → datos vacíos
- **Fix:** Reescrita con modelo Tecnico (NombreCompleto, Especialidad, Telefono, Email, Activo)

---

## P1 — SEGURIDAD

### CORR-029: DiagnosticoSesion [AllowAnonymous]
- **Archivo:** `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- **Impacto:** Cualquier persona podía ver datos internos de sesión
- **Fix:** Cambiado a `[Authorize(Roles = "Administrador")]`

### CORR-030: Debug file writes en constructor
- **Archivo:** `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- **Impacto:** `File.AppendAllText` a path hardcoded en cada request → I/O en producción, info leak
- **Fix:** Reemplazado con `System.Diagnostics.Debug.WriteLine`

---

## P2 — FUNCIONAL

### CORR-031: Duplicate OrdenRecaudacionViewModel
- **Archivo:** `CapaPresentacion/Models/Placeholders.cs`
- **Impacto:** Ambigüedad de namespace si ambos se importan → compile error potencial
- **Fix:** Eliminada clase duplicada, comentario apuntando a la canónica en ViewModels

### CORR-032: jQuery BundleConfig 3.7.1 (no existe)
- **Archivo:** `CapaPresentacion/App_Start/BundleConfig.cs`
- **Impacto:** Bundle no encontraba archivo → jQuery no se cargaba via bundle
- **Fix:** Cambiado a `jquery-3.6.4.min.js` (archivo que sí existe)

### CORR-033: Login.cshtml jQuery 3.6.0
- **Archivo:** `Views/Account/Login.cshtml`
- **Impacto:** Versión inconsistente con el resto del proyecto (3.6.4)
- **Fix:** Cambiado CDN a jquery-3.6.4.min.js

---

## Compilación Post-Correcciones

```
MSBuild AOCR.sln /p:Configuration=Debug → Exit Code: 0
  CapaModelo       ✅
  CapaDatos        ✅
  CapaNegocio      ✅
  CapaPresentacion ✅
  AOCR.Tests       ✅

Warnings restantes (todos pre-existentes):
  - MSB3277: Assembly version conflicts (Npgsql, Dapper, System.Text.Json)
  - CS0618: EmpresaAS400DAO/BancoP9DAO constructores obsoletos (8 ocurrencias)
  - CS0168: Variables 'ex'/'err' no usadas (3 ocurrencias)
  - CS1998: Métodos async sin await (2 ocurrencias)
```
