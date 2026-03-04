# ENTREGABLE 1 — Inventario y Mapa del Sistema AOCR

## 1. Stack Tecnológico

| Componente | Tecnología | Versión |
|------------|-----------|---------|
| Framework | ASP.NET MVC 5 | .NET Framework 4.7.2 |
| DB Principal | PostgreSQL | v14+ (172.20.16.55:5432, dgac_des) |
| DB Legado | DB2 / AS400 | 172.20.16.14 (DGAC) / 190.152.8.185 |
| ORM | Dapper | 2.1.66 |
| DI | Unity | via UnityConfig.cs |
| Auth | Forms Authentication | ticket.UserData con roles |
| Frontend | Bootstrap 5.3.0, jQuery 3.6.4, AdminLTE 3.2, SweetAlert2, DataTables 1.13.4, FontAwesome 6.4.0 |
| PDF | iTextSharp | 5.x |
| CI/CD | Azure Pipelines | azure-pipelines.yml |

---

## 2. Arquitectura de Capas

```
CapaPresentacion  →  CapaNegocio (BL)  →  CapaDatos (DAO)  →  PostgreSQL / AS400
     │                                         │
     ├── Controllers/                           ├── DAOs/
     ├── Views/                                 ├── Entidades/
     ├── Models/ViewModels/                     └── Models/
     ├── App_Start/
     ├── Scripts/
     └── Content/
```

---

## 3. Mapa de Controladores

### Módulos Core

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| AccountController | 454 | Mixto | Login/Logout/Registro | ✅ Funcional |
| HomeController | ~30 | Sí | Dashboard landing | ✅ Funcional |
| DashboardController | ~80 | Sí | Dashboard principal | ✅ Funcional |
| ErrorController | ~50 | No (intencional) | Páginas de error | ✅ Funcional |

### Módulo: Orden de Recaudación

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| OrdenRecaudacionController | 2474 | Sí | CRUD Órdenes | ✅ Corregido |
| OrdenRecaudacionDashboardController | ~120 | Sí | Dashboard KPIs | ✅ Corregido (view creada) |
| OrdenRecaudacionDashboardEmpresarialController | ~80 | Sí | Dashboard empresas | ✅ Funcional |
| OrdenController | ~100 | Sí | MisOrdenes, Detalle | ✅ Funcional |
| PagoController | ~200 | Sí | Registro/Validación pagos | ⚠️ DI sin container |
| FinancieroController | ~150 | Sí | Flujo financiero | ✅ Funcional |

### Módulo: Solicitud AOCR

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| SolicitudAOCRController | 766 | Sí | Formulario emisión | ⚠️ Usuario fake |
| SolicitudController | 423 | Sí | Gestión solicitudes | ✅ Funcional |

### Módulo: Inspección y Técnicos

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| InspeccionController | 515 | Sí | CRUD inspecciones | ✅ Funcional |
| TecnicoController | 183 | Sí | Gestión técnicos | ✅ Corregido (vista) |
| ChecklistController | ~120 | Sí (Admin) | Checklists | ⚠️ Acciones stub |

### Módulo: Administración

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| RolController | 180 | ✅ Admin | CRUD roles | ✅ Corregido |
| UsuarioRolController | ~200 | ✅ Admin | Asignación roles | ✅ Corregido |
| AdminUsuariosController | 424 | ✅ Admin | Gestión usuarios | ✅ Funcional |
| ParametroController | ~140 | ✅ Admin | Parámetros sistema | ✅ Corregido |
| LogController | ~30 | ✅ Admin | Logs sistema | ✅ Corregido |

### Módulo: Empresas y Usuarios

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| EmpresaController | 83 | AllowAnonymous | API JSON empresas | ✅ Funcional |
| UsuarioController | 1064 | AllowAnonymous | Registro usuario | ⚠️ Hash hardcoded |
| RTController | 272 | No (pendiente) | Representante Técnico | ✅ userId corregido |

### Módulo: Workflow

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| DireccionController | 285 | ✅ Corregido | Aprobación dirección | ✅ Corregido |
| CoordinacionLegalController | 27 | Sí | Coord. legal | ✅ Funcional |
| InformeController | ~120 | Sí | Informes inspección | ✅ Corregido |

### Otros

| Controlador | Líneas | [Authorize] | Módulo | Estado |
|-------------|--------|------------|--------|--------|
| HealthController | ~20 | No (intencional) | Health check | ✅ |
| ConfigApiController | ~40 | Admin | Config API | ✅ |
| SyncAdminController | ~60 | Admin | Sincronización | ✅ |
| NotificacionController | ~50 | Sí | Notificaciones | ✅ |
| ReportesFinancierosController | ~80 | Sí | Reportes | ✅ |
| HistorialController | ~50 | Sí | Historial | ✅ |
| HistorialEstadoController | ~50 | Sí | Estados historial | ✅ |
| DocumentoController | ~60 | Sí | Documentos | ✅ |
| CertificadoController | ~40 | Sí | Certificados | ✅ |
| ControlFR3Controller | ~30 | Sí | Control FR3 | ✅ |
| AdministradorController | ~20 | Admin | Admin panel | ✅ |
| OperadorController | ~30 | Sí | Operador | ✅ |

---

## 4. Mapa de Vistas

```
Views/
├── Account/          Login.cshtml, ForgotPassword, ResetPassword, CambioObligatorio
├── AdminUsuarios/    Index, Crear, Editar
├── Checklist/        Crear✅, Editar✅, Detalle✅
├── Dashboard/        Index
├── Direccion/        (usa VIEWS_TECNICO pattern — verificar)
├── Error/            Error, NotFound, Unauthorized
├── Home/             Index
├── Informe/          Crear✅, EmitirAOCR☠, Legalizar☠, ValidacionFinal☠
├── Inspeccion/       Realizar, Detalle, Programar, ListaInspecciones, etc.
├── Orden/            MisOrdenes, Detalle
├── OrdenRecaudacion/ Index, Crear, Editar, Detalles, Obligatoria, TodasOrdenes, etc.
├── OrdenRecaudacionDashboard/ Index✅(creada)
├── Parametro/        Index✅, Crear✅, Editar✅, EmitirAOCR☠, Legalizar☠, ValidacionFinal☠
├── Rol/              Index✅, Crear✅, Editar✅, AsignarPermisos✅
├── Shared/           _LayoutAOCR, _LayoutLogin, _ValidationScriptsPartial✅
├── SolicitudAOCR/    FormularioEmisionAOCR
├── Tecnico/          Index, Crear✅, Editar
├── Usuario/          RegistroExterno, DesignacionRT, ListaRT, etc.
├── UsuarioRol/       Index✅, AsignarRoles✅
└── _ViewStart.cshtml → ~/Views/Shared/_LayoutAOCR.cshtml
```

☠ = Vistas huérfanas/misplaced (pertenecen a workflow de certificación, no al módulo indicado)

---

## 5. Mapa de Modelos (CapaModelo)

| Modelo | Propiedades Clave |
|--------|------------------|
| Usuario | Id, CodigoUsuario, NombreUsuario, NombreCompleto, Email, Rol, Activo |
| Rol | IdRol, CodigoRol(string), Nombre, Descripcion, Activo |
| UsuarioRol | Id, CodigoUsuario(int), CodigoRol(int), FechaAsignacion |
| Parametro | CodigoParametro, Clave, Valor, Descripcion, Activo |
| Checklist | CodigoChecklist, CodigoInspeccion, Seccion, ItemNumero, Descripcion, Cumple, Criticidad |
| Inspeccion | CodigoInspeccion, CodigoSolicitud, CodigoInspector, FechaProgramada, Estado, Resultado |
| Informe | CodigoInforme, CodigoInspeccion, ResumenEjecutivo, Conclusiones, Hallazgos |
| Tecnico | CodigoTecnico, NombreCompleto, Especialidad, Telefono, Email, Activo |
| OrdenRecaudacion (Entity) | En CapaDatos.Entidades — mapeo directo a tabla |
| OrdenRecaudacionModel | En CapaPresentacion.Models — ViewModel para edición |
| OrdenRecaudacionViewModel | En CapaPresentacion.Models.ViewModels — ViewModel para listados |

---

## 6. Conexiones a Base de Datos

| Nombre | Servidor | Base | Uso |
|--------|----------|------|-----|
| AOCRConnection | 172.20.16.55:5432 | dgac_des | PostgreSQL principal |
| PostgreSQL | 172.20.16.55:5432 | dgac_des | Alias idéntico |
| P9ConnectionString | 172.20.16.14 | DGAC | DB2/AS400 bancos |
| AS400 (config) | 190.152.8.185 | DGACDAT | AS400 empresas/usuarios |

---

## 7. Configuración de Rutas

| Nombre | Patrón | Default |
|--------|--------|---------|
| Dashboard | Dashboard/{action}/{id} | Dashboard/Index |
| OrdenDetalle | Orden/Detalle/{id} | - |
| Default | {controller}/{action}/{id} | Home/Index |

---

## 8. Filtros Globales

- `GlobalExceptionFilter` — Captura excepciones no manejadas
- `AuditActionFilter` — Auditoría de acciones
- `GlobalSecurityFilter` — En SecurityFilters.cs
- `HandleErrorAttribute` — Manejo estándar MVC
