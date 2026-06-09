# AOCR - Fase 2 Canon de Autorizacion y Estados 2026-06-01

## Objetivo

Reducir deriva entre filtros, roles de sesion, permisos por base de datos y estados operativos, sin cambiar el flujo funcional ya congelado en Fase 1.

## Fuentes canonicas vigentes

- Autorizacion contextual MVC: `CapaPresentacion/Filters/SecurityFilters.cs`
- Normalizacion y agrupacion de roles: `CapaPresentacion/Helpers/RoleGroupingHelper.cs`
- Permisos por codigo y fallback por roles: `CapaNegocio/SeguridadBL.cs`
- Estados canonicos de solicitud: `CapaDatos/Constants/EstadoConstants.cs`
- Reglas de transicion de solicitud: `CapaNegocio/SolicitudEstadoTransitionBL.cs`

## Alineacion aplicada en esta fase

- `RequirePermissionAttribute` deja de depender solo de un catalogo fijo de `IsInRole`.
- El filtro reconstruye el contexto de roles desde `Session["RolesRaw"]`, `Session["Roles"]` y `Session["Rol"]`.
- El resultado se unifica con `RoleGroupingHelper` para conservar tanto roles crudos como grupos normalizados.
- La evaluacion final sigue delegada en `SeguridadBL.UsuarioTienePermiso`, que mantiene prioridad para permisos persistidos y fallback por roles conocidos.
- `AocrPostPagoWorkflowService` y `NotificacionDestinatarioPolicyService` dejan de depender de `RolesAOCR.cs` y consultan destinatarios por los nombres de rol crudos que realmente resuelve `UsuarioDAO.ListarPorRol`.
- `UsuarioInternoRTDAO` deja de depender de `RolesAOCR.cs` para el fallback de inspectores y usa el rol crudo `Inspector`, que es el valor real consultado en `usuario` / `usuario_rol`.
- `RevisionDocumentalService` concentra ahora la decision de cierre documental (`estadoDestino` + `observacionCierre`) para los cierres masivos y finales, evitando que `SolicitudAOCRController` replique reglas de transicion inline.
- El mismo `RevisionDocumentalService` concentra ahora tambien la decision simple de `Aprobar` y `Observar`, dejando al controlador solo la orquestacion HTTP y el cambio persistido.

## Limites explicitados

- `RolesAOCR.cs` y `EstadosSolicitudAOCR.cs` no son la fuente de verdad del modulo MVC operativo.
- Mientras existan referencias residuales, deben permanecer acotadas a superficies heredadas o de soporte, no a filtros/controladores MVC principales.
- Cualquier nuevo control de acceso en `CapaPresentacion` debe tomar contexto desde sesion/principal real, no desde catalogos simplificados.
- `EstadosSolicitudAOCR.cs` ya quedo efectivamente aislado como diagrama legacy; las capas activas deben usar `EstadoConstants.cs` y `SolicitudEstadoTransitionBL.cs` para estados y transiciones.

## Cobertura agregada

- `AOCR.Tests/Unit/OperationalFlowCharacterizationTests.cs` congela que `RequirePermissionAttribute` use sesion + `RoleGroupingHelper`.
- La misma suite congela que la capa MVC no reintroduzca `RolesAOCR` ni `EstadosSolicitudAOCR`.
- La misma suite congela que `CapaNegocio/Services` no vuelva a depender de `RolesAOCR` para resolver notificaciones internas.
- La misma suite congela que `CapaDatos/DAOs` no vuelva a depender de `RolesAOCR` para resolver inspectores o catalogos activos.
- La misma suite congela que `CapaNegocio` y `CapaDatos/DAOs` no reintroduzcan `EstadosSolicitudAOCR` en flujo activo.
- La misma suite congela que la decision de cierre documental permanezca centralizada en `RevisionDocumentalService`.

## Criterio de salida de esta fase

- El proyecto `CapaPresentacion` compila con `MvcBuildViews`.
- El proyecto `CapaNegocio` compila sin depender de `RolesAOCR` en servicios de negocio activos.
- El proyecto `CapaDatos` compila sin depender de `RolesAOCR` en DAOs activos.
- `EstadosSolicitudAOCR.cs` permanece sin consumo en capas activas y cualquier reintroduccion falla por caracterizacion.
- Las pruebas de caracterizacion detectan una regresion si el filtro vuelve a depender solo de catalogos fijos o si la capa MVC empieza a usar constantes simplificadas como canon.