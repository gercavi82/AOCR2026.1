# ENTREGABLE 2 — Auditoría Capa por Capa

## Capa de Presentación (Controllers)

### Hallazgos BLOQUEANTES Corregidos

| # | Controlador | Problema | Corrección Aplicada |
|---|------------|----------|---------------------|
| 1 | RolController | Sin [Authorize] → CRUD accesible sin login | Agregado `[Authorize(Roles="Administrador")]` |
| 2 | RolController.Index | catch retornaba null → NullReferenceException | Retorna `new List<Rol>()` |
| 3 | RolController.Crear | GET retornaba View() sin modelo | Retorna `new Rol { Activo = true }` |
| 4 | RolController.Eliminar | GET + RedirectToAction vs AJAX esperaba JSON | Cambiado a [HttpPost] + Json |
| 5 | DireccionController | Sin [Authorize] → CRUD público | Agregado `[Authorize]` a nivel de clase |
| 6 | ParametroController | Clase vacía, no heredaba Controller | Reconstruido con CRUD completo |
| 7 | InformeController | Clase vacía | Reconstruido con Index/Crear/Ver/Eliminar |
| 8 | ChecklistController | Clase vacía | Reconstruido con CRUD básico (stubs → TODO) |
| 9 | LogController | No heredaba Controller | Reconstruido con Index + [Authorize] |
| 10 | UsuarioRolController | Solo tenía ObtenerRoles() | Agregados Index/AsignarRoles/RemoverRol |
| 11 | OrdenRecaudacionController.Editar POST | Error paths retornaban modelo incompleto | Recarga modelo completo desde DB |
| 12 | OrdenRecaudacionController constructor | File.AppendAllText debug en producción | Reemplazado con Debug.WriteLine |
| 13 | OrdenRecaudacionController.DiagnosticoSesion | [AllowAnonymous] exponía datos de sesión | Cambiado a [Authorize(Roles="Administrador")] |
| 14 | RTController.ObtenerUsuarioId | Fallback `return 1` → operaba como userId 1 | Cambiado a `return 0` |
| 15 | TecnicoController.Crear | View usaba @model Inspeccion vs controlador Tecnico | Vista reescrita con modelo Tecnico |

### Hallazgos Pendientes (no bloqueantes)

| # | Controlador | Problema | Severidad |
|---|------------|----------|-----------|
| P1 | UsuarioController | Hash de contraseña hardcoded en registro legacy | P1 - Security |
| P2 | AccountController | Whitelist de emails hardcoded para bypass RT | P1 - Security |
| P3 | AccountController | USU_ADMIN hardcoded como superusuario | P1 - Security |
| P4 | PagoController | Constructor DI sin container Unity registrado | P2 - Runtime |
| P5 | SolicitudAOCRController | Crea usuario fake cuando no encuentra real | P2 - Data |
| P6 | ChecklistController | Acciones son stubs (no persisten datos) | P2 - Funcionalidad |
| P7 | OrdenRecaudacionController | 2474 líneas — necesita descomposición | P3 - Mantenibilidad |

---

## Capa de Presentación (Vistas)

### Hallazgos BLOQUEANTES Corregidos

| # | Vista | Problema | Corrección |
|---|-------|----------|----------|
| 1 | Rol/Index.cshtml | asp-action "Crear", "Editar" + ViewData | → @Url.Action + ViewBag |
| 2 | Rol/Crear.cshtml | asp-for, asp-action, `<partial>` | Reescrita completa MVC5 |
| 3 | Rol/Editar.cshtml | asp-for, asp-action, `<partial>` | Reescrita completa MVC5 |
| 4 | Rol/AsignarPermisos.cshtml | asp-action + ViewData | → @Url.Action + ViewBag |
| 5 | Parametro/Index.cshtml | asp-action + ViewData | → @Url.Action + ViewBag |
| 6 | Parametro/Crear.cshtml | asp-for, campos inexistentes (Categoria, TipoDato) | Reescrita con Clave, Valor, Descripcion |
| 7 | Parametro/Editar.cshtml | asp-for="Id" (no existe), campos equivocados | Reescrita con CodigoParametro, Valor |
| 8 | UsuarioRol/Index.cshtml | asp-action + ViewData | → @Url.Action + ViewBag |
| 9 | UsuarioRol/AsignarRoles.cshtml | asp-action form, NombreRol (no existe), </form> | → Html.BeginForm, Nombre, } |
| 10 | Informe/Crear.cshtml | asp-for, campos inexistentes (Titulo, Contenido, CKEditor) | Reescrita con ResumenEjecutivo, Hallazgos |
| 11 | Checklist/Crear.cshtml | asp-for, modelo equivocado | Reescrita con Seccion, ItemNumero, Cumple |
| 12 | Checklist/Editar.cshtml | asp-for, modelo equivocado | Reescrita con selects correctos |
| 13 | Checklist/Detalle.cshtml | asp-action links + ViewData | → @Url.Action + ViewBag |
| 14 | Tecnico/Crear.cshtml | @model Inspeccion vs controlador Tecnico | Reescrita con NombreCompleto, Especialidad |
| 15 | _ValidationScriptsPartial.cshtml | Stub con solo `<h2>` heading | CDN jQuery Validate + Unobtrusive |
| 16 | OrdenRecaudacionDashboard/Index.cshtml | Archivo faltante | Creada (642 líneas, KPIs, DataTable, AJAX) |

### Vistas Huérfanas (no bloqueantes)

| Vista | Ubicación | Pertenece a |
|-------|-----------|-------------|
| EmitirAOCR.cshtml | Views/Parametro/ y Views/Informe/ | Workflow certificación (misplaced) |
| Legalizar.cshtml | Views/Parametro/ y Views/Informe/ | Workflow certificación (misplaced) |
| ValidacionFinal.cshtml | Views/Parametro/ y Views/Informe/ | Workflow certificación (misplaced) |

---

## Capa de Presentación (Layout y Scripts)

### _LayoutAOCR.cshtml (831 líneas) — OK
- Cadena de carga correcta: jQuery → Bootstrap → aocr-utils → DataTables → SweetAlert → AdminLTE → site.js → app.js
- CDN con fallback local para jQuery

### BundleConfig.cs — CORREGIDO
- jquery-3.7.1.min.js referenciado pero no existía → Cambiado a jquery-3.6.4.min.js
- Login.cshtml usaba 3.6.0 → Cambiado a 3.6.4

---

## Capa de Presentación (Modelos/ViewModels)

### Hallazgo Corregido: Duplicate OrdenRecaudacionViewModel
- **Archivo 1:** `Models/ViewModels/OrdenRecaudacionMV.cs` — completo, con validación (ACTIVO)
- **Archivo 2:** `Models/Placeholders.cs` — ligero, sin validación (ELIMINADO)
- Las vistas activas solo usaban el de ViewModels → safe to remove

---

## Capa de Negocio (BL)

| Clase BL | Métodos | Estado |
|---------|---------|--------|
| RolBL | ObtenerTodos, ObtenerActivos, ObtenerPorId, Insertar, Actualizar, Eliminar, CambiarEstado | ✅ OK |
| ParametroBL | ListarTodos, ListarActivos, ObtenerPorId, ObtenerPorClave, Crear, Actualizar, EliminarSoft | ✅ OK |
| UsuarioBL | Autenticar, ListarTecnicos, ObtenerInspectores, RestablecerContraseña | ✅ OK |
| UsuarioRolBL | Asignar (solo 1 método) | ⚠️ Limitado |
| InformeBL | Insertar, ObtenerPorId, Listar | ✅ OK |
| OrdenRecaudacionBL | CRUD completo + workflow | ✅ OK |
| SolicitudAOCRBL | CRUD + ObtenerPendientesAsignacion | ✅ OK |
| TecnicoBL | ObtenerPorId, Insertar, Actualizar, Eliminar | ✅ OK |
| DireccionBL | ObtenerTodos + workflow | ✅ OK |
| ChecklistBL | Insertar, ObtenerPorSolicitud | ⚠️ Sin CRUD standalone |

---

## Capa de Datos (DAO)

| Clase DAO | Patrón | Estado |
|----------|--------|--------|
| ConexionDAO | Static, NpgsqlConnection | ✅ OK |
| OrdenRecaudacionDAO | Static, Dapper + raw NpgsqlCommand | ⚠️ Mixto |
| UsuarioDAO | Static, Dapper | ✅ OK |
| UsuarioRolDAO | Static, Dapper | ✅ OK |
| ParametroDAO | Instance, Dapper | ✅ OK |
| ChecklistDAO | Instance, Dapper | ✅ OK |
| InspeccionDAO | Static, Dapper | ✅ OK |
| BancoP9DAO | AS400 ODBC | ✅ OK |
| EmpresaAS400DAO | AS400 ODBC | ⚠️ Constructor obsoleto |
| AdminUsuariosDAO | Static, Dapper | ✅ OK |

### Seguridad SQL
- Todos los DAOs usan parámetros Dapper (`@param`) → **Sin SQL injection**
- Excepciones: queries con concat de strings no encontradas en DAOs activos

---

## Capa de Datos (Entidades)

- `CapaDatos.Entidades.OrdenRecaudacion` — mapeo directo a tabla PostgreSQL
- Pattern: Entity para DAO, ViewModel para capa presentación ✅

---

## Configuración

| Archivo | Estado |
|---------|--------|
| Web.config | ✅ Connection strings, Forms Auth, custom errors |
| RouteConfig.cs | ✅ 3 rutas (Dashboard, OrdenDetalle, Default) |
| FilterConfig.cs | ✅ GlobalExceptionFilter, Audit, Security, HandleError |
| BundleConfig.cs | ✅ Corregido jQuery version |
| UnityConfig.cs | ✅ Registra DAOs e interfaces |
| Global.asax | ✅ Dapper MatchNamesWithUnderscores = true |

---

## Resumen de Auditoría

| Capa | Hallazgos Críticos | Corregidos | Pendientes |
|------|-------------------|------------|------------|
| Controllers | 15 | 15 | 7 (P1-P3) |
| Vistas | 16 | 16 | 3 huérfanas |
| Modelos | 1 duplicado | 1 | 0 |
| BL | 0 | 0 | 2 limitados |
| DAO | 0 | 0 | 1 constructor obsoleto |
| Config | 2 (jQuery, Bundle) | 2 | 0 |
| **TOTAL** | **34** | **34** | **13** |
