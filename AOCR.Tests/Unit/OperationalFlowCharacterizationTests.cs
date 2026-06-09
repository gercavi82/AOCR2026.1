using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OperationalFlowCharacterizationTests
    {
        [TestMethod]
        public void RevisionDocumental_ShouldKeepCurrentStateRoutingContracts()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\RevisionDocumentalService.cs");

            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionCierreMasivo(");
            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionCierreFinal(");
            StringAssert.Contains(controller, "_revisionDocumentalService.PrepararFirmaAceptacionDocumental(estadoActual, documentosRevision, revisiones, observacion)");
            StringAssert.Contains(service, "EstadoDestino = aprobarTodos ? EstadoSolicitud.AceptacionDocumental : EstadoSolicitud.Observada");
            StringAssert.Contains(service, "EstadoDestino = tieneDocumentosDevueltos ? EstadoSolicitud.Observada : EstadoSolicitud.AceptacionDocumental");
            StringAssert.Contains(service, "public RevisionDocumentalFirmaPlan PrepararFirmaAceptacionDocumental(");
            Assert.IsFalse(controller.Contains("La aceptación documental solo se puede firmar cuando el inspector haya aceptado toda la documentación."), "El controlador no debe validar inline la firma de aceptación documental.");
            Assert.IsFalse(controller.Contains("No se puede firmar la aceptación mientras existan documentos sin aceptar por el inspector."), "El controlador no debe validar inline documentos pendientes al firmar la aceptación documental.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.FirmadoCoordinador, observacionFirma"), "El controlador no debe hardcodear inline el paso a FirmadoCoordinador para la aceptación documental.");
        }

        [TestMethod]
        public void SolicitudEstadoTransition_ShouldKeepSafeGenericCorreoSuppressionScopeRestricted()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\SolicitudEstadoTransitionBL.cs");

            StringAssert.Contains(service, "string.Equals(eventoCorreoWorkflow, \"OBSERVADA\"", "OBSERVADA debe seguir dentro del alcance seguro de supresión del correo genérico.");
            StringAssert.Contains(service, "string.Equals(eventoCorreoWorkflow, \"ACEPTACION_DOCUMENTAL\"", "ACEPTACION_DOCUMENTAL debe seguir dentro del alcance seguro de supresión del correo genérico.");
            StringAssert.Contains(service, "string.Equals(eventoCorreoWorkflow, \"PAGO_APROBADO\"", "PAGO_APROBADO debe permanecer activo en código mientras se espera validación runtime real.");
            Assert.IsFalse(service.Contains("string.Equals(eventoCorreoWorkflow, \"PENDIENTE_ASIGNACION_INSPECTOR\""), "PENDIENTE_ASIGNACION_INSPECTOR no debe entrar al patrón de supresión segura sin decisión funcional explícita.");
            Assert.IsFalse(service.Contains("string.Equals(eventoCorreoWorkflow, \"SUBSANADA\""), "SUBSANADA no debe entrar al patrón de supresión segura mientras siga acoplada a SubsanarPost.");
            Assert.IsFalse(service.Contains("string.Equals(eventoCorreoWorkflow, \"AOCR_LEGALIZADO\""), "AOCR_LEGALIZADO no debe entrar al patrón de supresión segura en este slice.");
            Assert.IsFalse(service.Contains("string.Equals(eventoCorreoWorkflow, \"AOCR_EMITIDO_RECIBIDO\""), "AOCR_EMITIDO_RECIBIDO no debe entrar al patrón de supresión segura en este slice.");
        }

        [TestMethod]
        public void SolicitudEstadoTransition_ShouldPreserveInternalNotificationsAndSpecificWorkflowDispatch()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\SolicitudEstadoTransitionBL.cs");

            StringAssert.Contains(service, "NotificarCambioEstadoInternoSinCorreoGenericoSiCorresponde(", "La transición AOCR debe seguir preservando la campana interna cuando se omite el correo genérico.");
            StringAssert.Contains(service, "NotificacionBL.EnviarNotificacion(", "La transición AOCR debe seguir usando notificación interna explícita cuando se suprime el correo genérico.");
            StringAssert.Contains(service, "La solicitud #{codigoSolicitud} cambió a estado: {estadoDestino}", "La campana interna debe seguir manteniendo el mensaje operacional actual.");
            StringAssert.Contains(service, "try { DispatchCorreoEventoPorEstado(solicitud, estadoAnterior, estadoDestino, codigoHistorial); } catch { }", "La transición AOCR debe seguir despachando el correo funcional específico después de la notificación interna.");
            StringAssert.Contains(service, "RegistrarCambioYObtenerCodigo(", "La transición AOCR debe conservar la ocurrencia real de historial para los correos idempotentes.");
        }

        [TestMethod]
        public void SolicitudAocrCorreo_ShouldUseStrongEventKeyOnlyForObservedEvent()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\SolicitudAocrCorreoService.cs");

            StringAssert.Contains(service, "BuildAocrEventKey(evento, solicitud.CodigoSolicitud, codigoHistorial, correlationId, destinatario.Email)");
            StringAssert.Contains(service, "return \"SOLICITUD_OBSERVADA\";");
            StringAssert.Contains(service, "codigoHistorial.HasValue && codigoHistorial.Value > 0");
            StringAssert.Contains(service, "return string.Equals(eventoNormalizado, \"OBSERVADA\"", "El primer slice de idempotencia debe quedar limitado a OBSERVADA.");
            Assert.IsFalse(service.Contains("string.Equals(eventoNormalizado, \"SUBSANADA\""), "SUBSANADA no debe recibir EventKey desde este slice.");
            Assert.IsFalse(service.Contains("string.Equals(eventoNormalizado, \"PAGO_APROBADO\""), "PAGO_APROBADO no debe recibir EventKey desde este slice.");
            Assert.IsFalse(service.Contains("string.Equals(eventoNormalizado, \"AOCR_LEGALIZADO\""), "Legalización no debe recibir EventKey desde este slice.");
            Assert.IsFalse(service.Contains("string.Equals(eventoNormalizado, \"AOCR_EMITIDO_RECIBIDO\""), "Emisión/recepción no debe recibir EventKey desde este slice.");
        }

        [TestMethod]
        public void ModificationWorkflowDecision_ShouldBeCentralizedInBusinessService()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\AocrModificationWorkflowService.cs");

            StringAssert.Contains(service, "public sealed class AocrModificacionWorkflowResult");
            StringAssert.Contains(service, "public AocrModificationWorkflowPlan PrepararRequiereInspeccion(SolicitudAOCR solicitud, string observacion)");
            StringAssert.Contains(service, "public AocrModificationWorkflowPlan PrepararGeneracionCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)");
            StringAssert.Contains(service, "public AocrModificacionWorkflowResult EjecutarRequiereInspeccion(");
            StringAssert.Contains(service, "public AocrModificacionWorkflowResult EjecutarGeneracionCondicionesLimitaciones(");
            StringAssert.Contains(service, "public AocrModificationWorkflowPlan PrepararRevisionFinalCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)");
            StringAssert.Contains(service, "public AocrModificationWorkflowPlan PrepararEnvioDcavCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)");
            StringAssert.Contains(controller, "_aocrModificationWorkflowService.EjecutarRequiereInspeccion(");
            StringAssert.Contains(controller, "_aocrModificationWorkflowService.EjecutarGeneracionCondicionesLimitaciones(");
            StringAssert.Contains(controller, "_aocrModificationWorkflowService.PrepararRevisionFinalCondicionesLimitaciones(solicitud, observacion)");
            StringAssert.Contains(controller, "_aocrModificationWorkflowService.PrepararEnvioDcavCondicionesLimitaciones(solicitud, observacion)");
            Assert.IsFalse(controller.Contains("Solo puede derivar a inspección una modificación con documentación ya aceptada."), "El controlador no debe validar inline la derivación a inspección de la modificación AOCR.");
            Assert.IsFalse(controller.Contains("Solo puede generar Condiciones y Limitaciones cuando la documentación de la modificación ya fue aceptada."), "El controlador no debe validar inline la generación de Condiciones y Limitaciones de la modificación AOCR.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.RequiereInspeccion"), "El controlador no debe hardcodear inline el paso a REQUIERE_INSPECCION para modificaciones AOCR.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.GeneradoCondicionesLimitaciones"), "El controlador no debe hardcodear inline el paso a GENERADO_CONDICIONES_LIMITACIONES para modificaciones AOCR.");
            Assert.IsFalse(controller.Contains("var plan = _aocrModificationWorkflowService.PrepararRequiereInspeccion(solicitud, observacion);"), "El controlador no debe decidir localmente el plan de REQUIERE_INSPECCION para modificaciones AOCR.");
            Assert.IsFalse(controller.Contains("var plan = _aocrModificationWorkflowService.PrepararGeneracionCondicionesLimitaciones(solicitud, observacion);"), "El controlador no debe decidir localmente el plan de GENERADO_CONDICIONES_LIMITACIONES para modificaciones AOCR.");
            Assert.IsFalse(controller.Contains("Solo puede abrir revisión final desde el estado GENERADO_CONDICIONES_LIMITACIONES."), "El controlador no debe validar inline la entrada a revisión final de Condiciones y Limitaciones.");
            Assert.IsFalse(controller.Contains("Solo puede enviar a DCAV una modificación en revisión final de coordinación."), "El controlador no debe validar inline el envío a DCAV de la modificación AOCR.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.EnRevisionCoordinadorFinal"), "El controlador no debe hardcodear inline el paso a revisión final de coordinación para modificaciones AOCR.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.EnviadoDcav"), "El controlador no debe hardcodear inline el envío a DCAV para modificaciones AOCR.");
        }

        [TestMethod]
        public void SubsanacionRt_ShouldRemainVersionedAndReturnSolicitudToSubsanada()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\RevisionDocumentalService.cs");
            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\SolicitudAOCR\\Subsanar.cshtml");
            var controllerNormalizado = controller.Replace("\r\n", "\n");

            StringAssert.Contains(controller, "Estado = \"PENDIENTE_REVISION_SUBSANACION\"");
            StringAssert.Contains(service, "public IList<Documento> ObtenerDocumentosPendientesSubsanacion(");
            StringAssert.Contains(controller, "_revisionDocumentalService.ObtenerDocumentosPendientesSubsanacion(");
            StringAssert.Contains(controller, "ObtenerDocumentosElegiblesParaSubsanacion(");
            StringAssert.Contains(controller, "SeleccionarUltimosDocumentosPendientesSubsanacionPorGrupo(");
            StringAssert.Contains(controller, "CambiarEstadoSubsanadaDesdeSubsanarPost(codigoSolicitud, observacionCambio, out mensajeCambio)");
            StringAssert.Contains(controllerNormalizado, "out mensaje,\n                true,\n                true);");
            StringAssert.Contains(controller, "NotificarInspectorDocumentacionSubsanada");
            Assert.IsFalse(view.Contains(".Where(doc => string.Equals((doc.Estado ?? string.Empty).Trim(), \"OBSERVADO\""), "La vista Subsanar no debe volver a filtrar por estado; el backend ya entrega solo documentos pendientes.");
            Assert.IsFalse(controller.Contains("return decision == \"DEVUELTO\" || decision == \"OBSERVADO\";"), "El controlador no debe duplicar inline el criterio documental de subsanación pendiente.");
        }

        [TestMethod]
        public void SubsanacionRt_ShouldIncludeDocumentoDevueltoWhenRevisionDictionaryIsMissing()
        {
            var service = (RevisionDocumentalService)FormatterServices.GetUninitializedObject(typeof(RevisionDocumentalService));
            var documentos = new List<Documento>
            {
                new Documento
                {
                    CodigoDocumento = 101,
                    TipoDocumento = "MANUAL_OPERACIONES",
                    NombreArchivo = "manual.pdf",
                    Estado = "DEVUELTO"
                },
                new Documento
                {
                    CodigoDocumento = 102,
                    TipoDocumento = "FORMULARIO",
                    NombreArchivo = "formulario.pdf",
                    Estado = "APROBADO"
                }
            };

            var pendientes = service.ObtenerDocumentosPendientesSubsanacion(
                documentos,
                new Dictionary<int, Tuple<string, string>>());

            Assert.AreEqual(1, pendientes.Count);
            Assert.AreEqual(101, pendientes.Single().CodigoDocumento);
        }

        [TestMethod]
        public void DescargaFinalRt_ShouldRemainCoupledToFinalizadoInCurrentFlow()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");

            StringAssert.Contains(controller, "CambiarEstadoConReglasAocr(id, EstadoSolicitud.Finalizado, \"Aceptación documental descargada por el RT.\"");
            StringAssert.Contains(controller, "CambiarEstadoConReglasAocr(id, EstadoSolicitud.Finalizado, \"Descarga final de Condiciones y Limitaciones firmada por RT.\"");
        }

        [TestMethod]
        public void InstitutionalEndpoints_ShouldKeepCurrentAuthorizationContracts()
        {
            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\SolicitudAOCRController.cs",
                "FinalizarRevisionDocumental",
                "Inspector,Coordinador,CoordinadorInspecciones,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\SolicitudAOCRController.cs",
                "FirmarAceptacionDocumental",
                "Coordinador,CoordinadorInspecciones,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\SolicitudAOCRController.cs",
                "AprobarPorJefatura",
                "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\SolicitudAOCRController.cs",
                "ObservarPorJefatura",
                "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\SolicitudAOCRController.cs",
                "Legalizar",
                "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs",
                "ValidarAocr",
                "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");

            AssertAuthorizeRoles(
                "CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs",
                "DocumentoValidacionAocr",
                "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");

            var direccionAprobar = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\InspeccionController.cs", "DireccionAprobar");
            var direccionDevolver = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\InspeccionController.cs", "DireccionDevolver");

            StringAssert.Contains(direccionAprobar, "[AocrAuthorize(Roles = ROLES_ACCESO_DECISION_INSTITUCIONAL_FINAL)]");
            StringAssert.Contains(direccionDevolver, "[AocrAuthorize(Roles = ROLES_ACCESO_DECISION_INSTITUCIONAL_FINAL)]");
        }

        [TestMethod]
        public void DireccionAprobar_ShouldContinueSyncingSolicitudAocrAfterFinalDecision()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\InspeccionController.cs");

            StringAssert.Contains(controller, "var resultadoSincronizacionAocr = SincronizarSolicitudAocrTrasFirmaFinal(inspeccion, solicitud, informeAprobado, usuarioId, usuarioActual);");
            StringAssert.Contains(controller, "Se habilita la generación AOCR.");
            StringAssert.Contains(controller, "DIRDAC / Dirección - Jefatura aprobó el informe y la AOCR quedó habilitada para generación.");
        }

        [TestMethod]
        public void RequirePermission_ShouldResolveRolesFromSessionAndUnifiedRoleGrouping()
        {
            var filter = LeerArchivoRepositorio("CapaPresentacion\\Filters\\RequirePermissionAttribute.cs");

            StringAssert.Contains(filter, "var roles = ObtenerRoles(httpContext);");
            StringAssert.Contains(filter, "httpContext.Session[\"Rol\"]");
            StringAssert.Contains(filter, "httpContext.Session[\"Roles\"]");
            StringAssert.Contains(filter, "httpContext.Session[\"RolesRaw\"]");
            StringAssert.Contains(filter, "RoleGroupingHelper.ExtractRoles");
            StringAssert.Contains(filter, "RoleGroupingHelper.BuildUnifiedRoles");
            StringAssert.Contains(filter, "SeguridadBL.UsuarioTienePermiso(codigoUsuario, _codigoPermiso, roles);");
        }

        [TestMethod]
        public void MvcAuthorizationSurface_ShouldNotDependOnSimplifiedLegacyRoleOrStateConstants()
        {
            AssertRepositoryTreeDoesNotReference("CapaPresentacion", "*.cs", "RolesAOCR");
            AssertRepositoryTreeDoesNotReference("CapaPresentacion", "*.cs", "EstadosSolicitudAOCR");
        }

        [TestMethod]
        public void BusinessNotificationServices_ShouldNotDependOnSimplifiedRoleConstants()
        {
            AssertRepositoryTreeDoesNotReference("CapaNegocio\\Services", "*.cs", "RolesAOCR");
        }

        [TestMethod]
        public void DataAccessActiveDaos_ShouldNotDependOnSimplifiedRoleConstants()
        {
            AssertRepositoryTreeDoesNotReference("CapaDatos\\DAOs", "*.cs", "RolesAOCR");
        }

        [TestMethod]
        public void BusinessLayer_ShouldNotDependOnSimplifiedStateConstants()
        {
            AssertRepositoryTreeDoesNotReference("CapaNegocio", "*.cs", "EstadosSolicitudAOCR");
        }

        [TestMethod]
        public void DataAccessActiveDaos_ShouldNotDependOnSimplifiedStateConstants()
        {
            AssertRepositoryTreeDoesNotReference("CapaDatos\\DAOs", "*.cs", "EstadosSolicitudAOCR");
        }

        [TestMethod]
        public void LegacyCatalogs_ShouldBeExplicitlyMarkedAsNonCanonical()
        {
            var legacyRoles = LeerArchivoRepositorio("CapaDatos\\Constants\\RolesAOCR.cs");
            var legacyStates = LeerArchivoRepositorio("CapaDatos\\Constants\\EstadosSolicitudAOCR.cs");

            StringAssert.Contains(legacyRoles, "LEGACY: catálogo simplificado histórico de roles AOCR.");
            StringAssert.Contains(legacyRoles, "No es fuente canónica para capas activas");
            StringAssert.Contains(legacyStates, "LEGACY: diagrama simplificado histórico de estados AOCR.");
            StringAssert.Contains(legacyStates, "No es fuente canónica para flujo activo");
        }

        [TestMethod]
        public void RevisionDocumentalClosureDecision_ShouldBeCentralizedInBusinessService()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\RevisionDocumentalService.cs");

            StringAssert.Contains(service, "public RevisionDocumentalCierreDecision CrearDecisionCierreMasivo");
            StringAssert.Contains(service, "public RevisionDocumentalCierreDecision CrearDecisionCierreFinal");
            StringAssert.Contains(service, "public RevisionDocumentalCierreDecision CrearDecisionRevisionSimple");
            StringAssert.Contains(service, "public RevisionDocumentalValidacionResult ValidarChecklistParaAprobacion");
            StringAssert.Contains(service, "public RevisionDocumentalValidacionResult ValidarCierreRevisionDocumental");
            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionCierreMasivo(");
            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionCierreFinal(");
            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionRevisionSimple(true, null)");
            StringAssert.Contains(controller, "_revisionDocumentalService.CrearDecisionRevisionSimple(false, observacion)");
            StringAssert.Contains(controller, "_revisionDocumentalService.ValidarChecklistParaAprobacion(");
            StringAssert.Contains(controller, "_revisionDocumentalService.ValidarCierreRevisionDocumental(documentosRevision, revisiones)");
            Assert.IsFalse(controller.Contains("var estadoDestino = tipoAccionNorm == \"APROBAR_TODOS\""), "El controlador no debe recalcular inline el estado destino del cierre masivo.");
            Assert.IsFalse(controller.Contains("var estadoDestino = tieneDocumentosDevueltos"), "El controlador no debe recalcular inline el estado destino del cierre final.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(idSolicitud, EstadoSolicitud.AceptacionDocumental, \"Aprobado por inspector\""), "El controlador no debe hardcodear inline la aprobación simple de revisión documental.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(idSolicitud, EstadoSolicitud.Observada, observacion ?? string.Empty"), "El controlador no debe hardcodear inline la observación simple de revisión documental.");
            Assert.IsFalse(controller.Contains("Checklist incompleto: Total="), "El controlador no debe construir inline la validación previa del checklist documental.");
            Assert.IsFalse(controller.Contains("No se puede enviar la revisión documental. Faltan decisiones en:"), "El controlador no debe validar inline decisiones pendientes del cierre documental.");
            Assert.IsFalse(controller.Contains("No se puede enviar la revisión documental. Debe registrar observación en:"), "El controlador no debe validar inline observaciones pendientes del cierre documental.");
        }

        [TestMethod]
        public void AocrFinalWorkflowDecision_ShouldBeCentralizedInBusinessService()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\AocrFinalWorkflowService.cs");
            var securityFilters = LeerArchivoRepositorio("CapaPresentacion\\Filters\\SecurityFilters.cs");
            var contextFactory = LeerArchivoRepositorio("CapaPresentacion\\Infrastructure\\AocrAuthorizationContextFactory.cs");

            StringAssert.Contains(service, "public AocrFinalWorkflowValidationResult ValidarEnvioRevisionInstitucional(bool tieneAocrGenerada, string estadoActual)");
            StringAssert.Contains(service, "public AocrFinalWorkflowValidationResult ValidarLegalizacion(bool tieneAocrGenerada)");
            StringAssert.Contains(service, "public AocrFinalWorkflowValidationResult ValidarEmision(bool tieneAocrGenerada)");
            StringAssert.Contains(service, "public AocrFinalWorkflowValidationResult ValidarInspeccionSatisfactoriaParaAocr(int codigoSolicitud)");
            StringAssert.Contains(service, "public AocrFinalWorkflowLegalizacionPlan PrepararLegalizacion(bool tieneAocrGenerada, string observacionLegal)");
            StringAssert.Contains(service, "public AocrFinalWorkflowEmisionPlan PrepararEmision(bool tieneAocrGenerada, string observacion)");
            StringAssert.Contains(service, "public AocrFinalWorkflowElaboracionPlan PrepararElaboracion(int codigoSolicitud, string observacion)");
            StringAssert.Contains(service, "public AocrFinalWorkflowRevisionPlan PrepararEnvioRevisionInstitucional(bool tieneAocrGenerada, string estadoActual, string observacion)");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionEnvioRevisionInstitucional(string observacion)");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionAprobacionJefatura()");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionObservacionJefatura(string observaciones)");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionLegalizacion(string observacionLegal)");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionEmision(string observacion)");
            StringAssert.Contains(service, "public AocrFinalWorkflowDecision CrearDecisionElaboracion(string observacion)");
            StringAssert.Contains(service, "public ResultadoOperacion NotificarLegalizacion(SolicitudAOCR solicitudActualizada, AocrFinalWorkflowLegalizacionPlan legalizacionPlan)");
            StringAssert.Contains(service, "public ResultadoOperacion NotificarEmision(SolicitudAOCR solicitudActualizada, AocrFinalWorkflowEmisionPlan emisionPlan)");
            StringAssert.Contains(service, "public bool UsuarioPuedeTransicionarEstadoAocr(string estadoDestino, IEnumerable<string> rolesActuales, bool usuarioAutenticado)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.ValidarInspeccionSatisfactoriaParaAocr(id)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.PrepararElaboracion(id, observacion)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.PrepararEnvioRevisionInstitucional(");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.PrepararLegalizacion(aocrGenerada != null, observacionLegal)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.PrepararEmision(aocrGenerada != null, observacion)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.CrearDecisionAprobacionJefatura()");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.CrearDecisionObservacionJefatura(observaciones)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.NotificarLegalizacion(solicitudActualizada, legalizacionPlan)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.NotificarEmision(solicitudActualizada, emisionPlan)");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(");
            StringAssert.Contains(controller, "_aocrAuthorizationService.PuedeEjecutarAccion(\"Generar\"");
            StringAssert.Contains(controller, "RegistrarTrazaAocrCoordinacion(");
            StringAssert.Contains(controller, "private AocrAuthorizationContext CrearContextoAutorizacionAocr()");
            StringAssert.Contains(controller, "AocrAuthorizationContextFactory.Build(HttpContext)");
            StringAssert.Contains(securityFilters, "AocrAuthorizationContextFactory.Build(httpContext)");
            StringAssert.Contains(contextFactory, "RoleGroupingHelper.BuildUnifiedRoles(");
            StringAssert.Contains(contextFactory, "AuthTicketRoleDataHelper.Deserialize(authTicket.UserData)");
            StringAssert.Contains(controller, "RoleGroupingHelper.ToDisplayName(rolCanonico)");
            Assert.IsFalse(controller.Contains("Debe generar primero el documento AOCR antes de enviarlo a revisión."), "El controlador no debe validar inline la existencia del AOCR previo al envío institucional.");
            Assert.IsFalse(controller.Contains("La AOCR ya fue enviada a DIRDAC y permanece pendiente de revisión institucional."), "El controlador no debe validar inline el reenvío institucional cuando la AOCR ya está en revisión.");
            Assert.IsFalse(controller.Contains("La AOCR solo puede enviarse a DIRDAC cuando el documento se encuentra en elaboración y listo para revisión."), "El controlador no debe validar inline el estado permitido para el envío institucional.");
            Assert.IsFalse(controller.Contains("No se puede legalizar sin documento AOCR generado en el expediente."), "El controlador no debe validar inline la existencia del AOCR previo a la legalización.");
            Assert.IsFalse(controller.Contains("No se puede emitir AOCR sin documento AOCR generado y vigente."), "El controlador no debe validar inline la existencia del AOCR previo a la emisión.");
            Assert.IsFalse(controller.Contains("SolicitudTieneInspeccionSatisfactoria(id, out mensajeInspeccion)"), "El controlador no debe conservar inline la compuerta de inspección satisfactoria para AOCR final.");
            Assert.IsFalse(controller.Contains("_solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, \"AOCR_LEGALIZADO\", observacionLegal)"), "El controlador no debe notificar inline la legalización AOCR.");
            Assert.IsFalse(controller.Contains("_solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, \"AOCR_EMITIDO_RECIBIDO\", observacion)"), "El controlador no debe notificar inline la emisión AOCR.");
            Assert.IsFalse(controller.Contains("_aocrFinalWorkflowService.ValidarLegalizacion(aocrGenerada != null)"), "El controlador no debe separar inline la validación de legalización cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("_aocrFinalWorkflowService.ValidarEnvioRevisionInstitucional("), "El controlador no debe separar inline la validación de envío a revisión cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EmitidoRecibido, observacion ?? \"AOCR emitido/recibido\""), "El controlador no debe hardcodear inline la emisión AOCR cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EnElaboracion, observacion ?? \"AOCR en elaboración\""), "El controlador no debe hardcodear inline el paso a elaboración AOCR cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EnRevision, observacion ?? \"AOCR en revisión\""), "El controlador no debe hardcodear inline el envío AOCR a revisión institucional.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Validado, \"Validado por Dirección / Jefatura\""), "El controlador no debe hardcodear inline la validación institucional final.");
            Assert.IsFalse(controller.Contains("Debe registrar una observación obligatoria para solicitar modificación al Inspector."), "El controlador no debe validar inline la observación obligatoria del retorno institucional.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.Observada, observaciones.Trim()"), "El controlador no debe hardcodear inline la devolución institucional al inspector.");
            Assert.IsFalse(controller.Contains("CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Legalizado, observacionLegal ?? \"Legalizado por Coordinación Legal\""), "El controlador no debe hardcodear inline la legalización AOCR.");
            Assert.IsFalse(controller.Contains("_aocrFinalWorkflowService.CrearDecisionEnvioRevisionInstitucional(observacion)"), "El controlador no debe separar inline la decisión de envío a revisión cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("_aocrFinalWorkflowService.CrearDecisionLegalizacion(observacionLegal)"), "El controlador no debe separar inline la decisión de legalización cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("_aocrFinalWorkflowService.CrearDecisionEmision(observacion)"), "El controlador no debe separar inline la decisión de emisión cuando ya existe un plan compuesto.");
            Assert.IsFalse(controller.Contains("private bool UsuarioActualPuedeTransicionarAocr("), "El controlador no debe conservar inline la matriz de roles para transiciones AOCR.");
            Assert.IsFalse(controller.Contains("if (destino == EstadoSolicitud.AOCR_EnRevision || destino == EstadoSolicitud.AOCR_Validado)"), "El controlador no debe conservar inline la matriz institucional de roles por estado AOCR.");
            Assert.IsFalse(controller.Contains("var roles = new[]\r\n            {\r\n                \"Coordinador\","), "La traza AOCR no debe reconstruir el rol principal con un catálogo manual en el controlador.");
            Assert.IsFalse(controller.Contains("PuedeRevisar=True PuedeSolicitarModificacion=True PuedeEnviarDIRDAC=True PuedeGenerarPdfFirma=True"), "La traza AOCR no debe hardcodear inline todos los flags institucionales en el controlador.");
            Assert.IsFalse(controller.Contains("PuedeEnviarDIRDAC=False PuedeSolicitarModificacion=False PuedeGenerarPdfFirma=True"), "La traza AOCR no debe mantener combinaciones fijas de flags por acción en el controlador.");
            Assert.IsFalse(controller.Contains("private IEnumerable<string> ObtenerRolesActualesAocr("), "El controlador no debe conservar el fallback manual de roles AOCR basado en User.IsInRole.");
            Assert.IsFalse(controller.Contains("private AuthTicketRoleData LeerRolesTicketAocr("), "El controlador no debe duplicar la lectura del ticket de autenticación AOCR.");
            Assert.IsFalse(securityFilters.Contains("private static AuthTicketRoleData ReadFormsTicketRoleData("), "SecurityFilters no debe conservar un builder local duplicado del contexto AOCR.");
            Assert.IsFalse(securityFilters.Contains("private static IList<string> ReadPrincipalRoles("), "SecurityFilters no debe conservar lectores locales duplicados de roles para el contexto AOCR.");
        }

        private static void AssertAuthorizeRoles(string relativeControllerPath, string actionName, string expectedRoles)
        {
            var declaration = ObtenerDeclaracionMetodo(relativeControllerPath, actionName);
            StringAssert.Contains(declaration, "[Authorize(Roles = \"" + expectedRoles + "\")]", "Roles inesperados en la acción " + actionName + ".");
        }

        private static string ObtenerDeclaracionMetodo(string relativeControllerPath, string actionName)
        {
            var content = LeerArchivoRepositorio(relativeControllerPath);
            var controllerPath = Path.Combine(ObtenerRepoRoot(), relativeControllerPath);
            var pattern = @"(?ms)(\[[^\]]+\]\s*)+public\s+(?:JsonResult|ActionResult)\s+" + Regex.Escape(actionName) + @"\s*\(";
            var match = Regex.Match(content, pattern);
            Assert.IsTrue(match.Success, "No se encontró la declaración pública de la acción " + actionName + " en " + controllerPath);

            return match.Value;
        }

        private static string LeerArchivoRepositorio(string relativePath)
        {
            var repoRoot = ObtenerRepoRoot();
            var absolutePath = Path.Combine(repoRoot, relativePath);
            Assert.IsTrue(File.Exists(absolutePath), "No se encontró el archivo: " + absolutePath);
            return File.ReadAllText(absolutePath);
        }

        private static void AssertRepositoryTreeDoesNotReference(string relativeDirectory, string searchPattern, string forbiddenToken)
        {
            var absoluteDirectory = Path.Combine(ObtenerRepoRoot(), relativeDirectory);
            Assert.IsTrue(Directory.Exists(absoluteDirectory), "No se encontró el directorio: " + absoluteDirectory);

            foreach (var filePath in Directory.GetFiles(absoluteDirectory, searchPattern, SearchOption.AllDirectories))
            {
                if (filePath.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    filePath.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var content = File.ReadAllText(filePath);
                Assert.IsFalse(
                    content.Contains(forbiddenToken),
                    "Se encontró la referencia '" + forbiddenToken + "' en " + filePath + ". La capa MVC debe seguir usando las fuentes canónicas reales.");
            }
        }

        private static string ObtenerRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        }
    }
}
