# ENTREGABLE 4 — Casos de Prueba (30 Casos)

## Convenciones
- **Pre:** Precondición
- **Pasos:** Acciones a ejecutar
- **Esperado:** Resultado esperado
- **Tipo:** Funcional / Seguridad / Regresión / UI
- **Prioridad:** P0 (crítico) / P1 (alto) / P2 (medio)

---

## Módulo: Autenticación y Seguridad

### TC-001: Login válido redirige a Dashboard
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Usuario activo con credenciales válidas
- **Pasos:** Navegar a /Account/Login → Ingresar usuario y contraseña → Click "Iniciar Sesión"
- **Esperado:** Redirect a /Home/Index o /Dashboard/Index. Session contiene UserId, Roles, NombreUsuario.

### TC-002: Login inválido muestra error
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Credenciales incorrectas
- **Pasos:** Navegar a /Account/Login → Ingresar datos incorrectos → Submit
- **Esperado:** Permanece en Login. Mensaje de error visible. Sin excepción.

### TC-003: RolController requiere autenticación
- **Tipo:** Seguridad | **Prioridad:** P0
- **Pre:** Usuario no autenticado (sin login)
- **Pasos:** Navegar directamente a /Rol/Index
- **Esperado:** Redirect a /Account/Login (no muestra datos de roles)

### TC-004: RolController requiere rol Administrador
- **Tipo:** Seguridad | **Prioridad:** P0
- **Pre:** Usuario autenticado con rol "Solicitante" (no admin)
- **Pasos:** Navegar a /Rol/Index
- **Esperado:** HTTP 403 Unauthorized o redirect a error page. No accede al CRUD.

### TC-005: DireccionController requiere autenticación
- **Tipo:** Seguridad | **Prioridad:** P0
- **Pre:** Usuario no autenticado
- **Pasos:** Navegar a /Direccion/Index
- **Esperado:** Redirect a /Account/Login

### TC-006: DiagnosticoSesion protegido
- **Tipo:** Seguridad | **Prioridad:** P1
- **Pre:** Usuario no autenticado
- **Pasos:** Navegar a /OrdenRecaudacion/DiagnosticoSesion
- **Esperado:** Redirect a login. No expone datos de sesión.

### TC-007: RTController sin sesión retorna userId 0
- **Tipo:** Seguridad | **Prioridad:** P1
- **Pre:** Sin sesión activa (o sesión expirada)
- **Pasos:** Invocar internamente ObtenerUsuarioId()
- **Esperado:** Retorna 0 (no 1). No ejecuta acciones como userId=1.

---

## Módulo: Roles (CRUD)

### TC-008: Listar roles muestra tabla
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Usuario admin autenticado, roles existen en BD
- **Pasos:** Navegar a /Rol/Index
- **Esperado:** Tabla con columnas Nombre, Descripción, Activo, Acciones. DataTable inicializado.

### TC-009: Listar roles — BD sin datos o error
- **Tipo:** Regresión | **Prioridad:** P0
- **Pre:** BD con tabla rol vacía o conexión fallida
- **Pasos:** Navegar a /Rol/Index
- **Esperado:** Vista se renderiza con tabla vacía. NO produce NullReferenceException.

### TC-010: Crear rol — formulario válido
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en /Rol/Crear
- **Pasos:** Llenar Nombre, Descripción, marcar Activo → Submit
- **Esperado:** Redirect a /Rol/Index con TempData["Success"]. Rol visible en lista.

### TC-011: Crear rol — formulario vacío
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en /Rol/Crear
- **Pasos:** Dejar campos vacíos → Submit
- **Esperado:** Validation messages visibles. Formulario no se envía (client-side) o retorna vista con errores.

### TC-012: Eliminar rol vía AJAX
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en /Rol/Index, rol existe
- **Pasos:** Click botón eliminar → SweetAlert → Confirmar
- **Esperado:** AJAX POST a Eliminar → JSON { success: true } → Fila se remueve de tabla.

---

## Módulo: Parámetros

### TC-013: Listar parámetros
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin autenticado
- **Pasos:** Navegar a /Parametro/Index
- **Esperado:** Lista de parámetros con Clave, Valor, Descripción. Links Crear y Editar funcionales.

### TC-014: Crear parámetro — validación Clave
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en /Parametro/Crear
- **Pasos:** Ingresar Clave (verifica conversión a mayúsculas), Valor, Descripción → Submit
- **Esperado:** Parámetro creado. Clave almacenada en mayúsculas.

### TC-015: Editar parámetro — Clave readonly
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en /Parametro/Editar/{id}
- **Pasos:** Verificar que campo Clave es readonly. Modificar Valor → Submit
- **Esperado:** Valor actualizado. Clave no cambia. HiddenFor CodigoParametro presente en form.

---

## Módulo: Usuario-Rol

### TC-016: Index lista usuarios activos
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Admin autenticado, usuarios activos en BD
- **Pasos:** Navegar a /UsuarioRol/Index
- **Esperado:** Tabla con ID, Usuario, Nombre, Email, botón "Gestionar Roles". DataTable funcional.

### TC-017: AsignarRoles — checkboxes pre-marcados
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Admin, usuario con roles asignados
- **Pasos:** Click "Gestionar Roles" en usuario → /UsuarioRol/AsignarRoles/{id}
- **Esperado:** Checkboxes de roles asignados están marcados. Roles no asignados desmarcados.

### TC-018: AsignarRoles POST — sincroniza roles
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Admin en AsignarRoles con 2 de 3 roles marcados
- **Pasos:** Desmarcar 1 rol, marcar otro nuevo → Submit
- **Esperado:** Redirect a AsignarRoles. Rol removido ya no aparece. Nuevo rol aparece marcado.

### TC-019: RemoverRol AJAX
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin en AsignarRoles, usuario tiene rol asignado
- **Pasos:** Click botón X rojo junto a rol → SweetAlert → Confirmar
- **Esperado:** AJAX POST → JSON { success: true } → Página recarga sin ese rol.

---

## Módulo: Orden de Recaudación

### TC-020: Dashboard KPIs cargan
- **Tipo:** Funcional | **Prioridad:** P0
- **Pre:** Usuario autenticado con permisos
- **Pasos:** Navegar a /OrdenRecaudacionDashboard/Index
- **Esperado:** 6 tarjetas KPI visibles. DataTable carga datos via AJAX. Filtros funcionales.

### TC-021: Editar orden — error en guardado no pierde datos
- **Tipo:** Regresión | **Prioridad:** P0
- **Pre:** Orden existente, simular fallo de BD en actualización
- **Pasos:** Abrir /OrdenRecaudacion/Editar/{id} → Modificar datos → Submit (fuerza error)
- **Esperado:** Vista se renderiza con datos COMPLETOS recargados de BD, no con modelo POST parcial.

### TC-022: Crear orden — flujo completo
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Usuario Financiero autenticado, solicitud válida
- **Pasos:** /OrdenRecaudacion/Crear → Seleccionar solicitud → Completar datos → Submit
- **Esperado:** Orden creada con estado inicial. Redirect a listado.

### TC-023: Constructor sin debug file writes
- **Tipo:** Regresión | **Prioridad:** P1
- **Pre:** Verificar que no se crean archivos debug_nueva.txt
- **Pasos:** Hacer cualquier request a /OrdenRecaudacion/*
- **Esperado:** No se crea ni escribe en C:\AOCR\...\debug_nueva.txt

---

## Módulo: Técnicos

### TC-024: Crear técnico — formulario correcto
- **Tipo:** Regresión | **Prioridad:** P0
- **Pre:** Admin en /Tecnico/Crear
- **Pasos:** Verificar campos del formulario
- **Esperado:** Campos: NombreCompleto, Especialidad, Telefono, Email, Activo (checkbox). NO muestra CodigoSolicitud, CodigoInspector, FechaProgramada.

### TC-025: Crear técnico — submit válido
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** Admin llena todos los campos de Crear
- **Pasos:** Submit formulario
- **Esperado:** Model binding a Tecnico funciona (NombreCompleto, etc. llegan al controlador). TecnicoBL.Insertar recibe datos correctos.

---

## Módulo: Inspecciones e Informes

### TC-026: Crear informe — campos correctos
- **Tipo:** Regresión | **Prioridad:** P0
- **Pre:** Usuario con permisos en /Informe/Crear
- **Pasos:** Verificar formulario
- **Esperado:** Campos: ResumenEjecutivo, Hallazgos, Conclusiones, Recomendaciones, AccionesCorrectivas, Resultado (select), FechaEmision (date). NO muestra Titulo, Contenido, CKEditor.

### TC-027: Checklist detalle — links funcionan
- **Tipo:** Regresión | **Prioridad:** P1
- **Pre:** Checklist con detalle visible
- **Pasos:** Click "Volver al listado", "Editar", "Crear Informe"
- **Esperado:** Todos los links navegan correctamente (@Url.Action, no asp-action).

---

## Módulo: Validación Client-Side

### TC-028: _ValidationScriptsPartial carga scripts
- **Tipo:** Regresión | **Prioridad:** P0
- **Pre:** Cualquier vista que incluye @Html.Partial("_ValidationScriptsPartial")
- **Pasos:** Inspeccionar fuente HTML en browser
- **Esperado:** `<script>` tags para jquery.validate.min.js y jquery.validate.unobtrusive.min.js presentes.

### TC-029: Validación client-side funciona en Rol/Crear
- **Tipo:** Funcional | **Prioridad:** P1
- **Pre:** /Rol/Crear con scripts de validación cargados
- **Pasos:** Dejar campo Nombre vacío → Click submit
- **Esperado:** Mensaje de validación aparece sin hacer POST. Formulario no se envía.

---

## Módulo: jQuery y Scripts

### TC-030: jQuery versión consistente
- **Tipo:** Regresión | **Prioridad:** P1
- **Pre:** Inspeccionar página principal y login
- **Pasos:** Ver source HTML → Verificar jQuery cargado
- **Esperado:** 
  - Layout: CDN jquery-3.6.4 (con fallback local)
  - Login: CDN jquery-3.6.4
  - BundleConfig: jquery-3.6.4.min.js
  - NO jQuery 3.7.1 o 3.6.0 en ningún lugar

---

## Resumen

| Módulo | Casos | P0 | P1 | P2 |
|--------|-------|-----|-----|-----|
| Auth/Seguridad | 7 | 3 | 4 | 0 |
| Roles | 5 | 2 | 3 | 0 |
| Parámetros | 3 | 0 | 3 | 0 |
| Usuario-Rol | 4 | 2 | 2 | 0 |
| Orden Recaudación | 4 | 2 | 2 | 0 |
| Técnicos | 2 | 1 | 1 | 0 |
| Inspecciones | 2 | 1 | 1 | 0 |
| Validación | 2 | 1 | 1 | 0 |
| jQuery/Scripts | 1 | 0 | 1 | 0 |
| **TOTAL** | **30** | **12** | **18** | **0** |
