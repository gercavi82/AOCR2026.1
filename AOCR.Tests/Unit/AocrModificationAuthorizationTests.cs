using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrModificationAuthorizationTests
    {
        [TestMethod]
        public void SolicitudAocr_ModificationInspectorActions_ShouldRequireInspectorRole()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "MarcarRequiereInspeccionModificacion", "Inspector,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "CerrarFaseDocumentalNuevoAeropuertoModificacion", "Inspector,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "GenerarCondicionesLimitacionesModificacion", "Inspector,Administrador");
        }

        [TestMethod]
        public void SolicitudAocr_ModificationCoordinatorActions_ShouldRequireCoordinatorRole()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "RevisarCondicionesLimitacionesModificacion", "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "EnviarCondicionesLimitacionesDcav", "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador");
        }

        [TestMethod]
        public void SolicitudAocr_ModificationDownload_ShouldRequireAuthentication()
        {
            AssertAuthorizePresent("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "DescargarCondicionesLimitacionesModificacion");
        }

        [TestMethod]
        public void CoordinacionJefatura_DigitalSignatureBootstrap_ShouldRequireInstitutionalRoles()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "CargarDatosFirmaDigitalAocr", "DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador");
        }

        [TestMethod]
        public void CoordinacionJefatura_ModificationDocumentWorkflow_ShouldExposeExpectedRoleContracts()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "EditarDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "PreviewDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "GenerarDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "GuardarPosicionFirmaAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador");
        }

        [TestMethod]
        public void SolicitudAocr_GenerarAocr_ShouldUseAocrAuthorizeContract()
        {
            var declaration = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "GenerarAOCR");

            StringAssert.Contains(declaration, "[HttpPost]", "GenerarAOCR debe mantenerse como POST.");
            StringAssert.Contains(declaration, "[ValidateAntiForgeryToken]", "GenerarAOCR debe exigir AntiForgeryToken.");
            StringAssert.Contains(declaration, "[AocrAuthorize(Modulo = \"SolicitudAOCR\", Accion = \"Generar\", CodigoSolicitudParameter = \"id\")]", "GenerarAOCR debe estar protegido por el contrato AOCR institucional.");
            Assert.IsFalse(declaration.Contains("Authorize(Roles = \"Inspector"), "GenerarAOCR no debe habilitarse para Inspector mediante Authorize por roles.");
        }

        [TestMethod]
        public void AocrAuthorizationService_ShouldExposeSolicitudAocrGenerateForDirectionOnly()
        {
            var content = LeerArchivoRepositorio("CapaNegocio\\Services\\AocrAuthorizationService.cs");
            StringAssert.Contains(content, "{ \"SolicitudAOCR/Generar\", new[] { \"DireccionJefaturaTecnica\", \"Administrador\" } }", "La matriz AOCR debe restringir la generación a Dirección/Jefatura o Administrador.");
        }

        [TestMethod]
        public void AocrTransitions_ShouldUseRawRolesInsteadOfUnifiedRoleBuckets()
        {
            var context = LeerArchivoRepositorio("CapaNegocio\\Services\\AocrAuthorizationService.cs");
            var factory = LeerArchivoRepositorio("CapaPresentacion\\Infrastructure\\AocrAuthorizationContextFactory.cs");
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");

            StringAssert.Contains(context, "public IList<string> RawRoles { get; set; }", "El contexto AOCR debe conservar los roles crudos además de los unificados.");
            StringAssert.Contains(factory, "RawRoles = rawRoles,", "El factory AOCR debe poblar los roles crudos desde sesión, ticket y principal autenticado.");
            StringAssert.Contains(factory, "RoleGroupingHelper.BuildUnifiedRoles(rawRoles)", "Los roles unificados deben derivarse desde la colección cruda preservada.");
            StringAssert.Contains(controller, "var rolesActuales = contexto.RawRoles ?? contexto.Roles ?? new List<string>();", "La trazabilidad y las decisiones AOCR deben preferir roles crudos para evitar falsos negativos por buckets unificados.");
            StringAssert.Contains(controller, "var rolesActuales = (contextoAocr.RawRoles ?? contextoAocr.Roles ?? new List<string>()).ToList();", "Las transiciones AOCR deben evaluarse con roles crudos antes de caer al fallback unificado.");
        }

        [TestMethod]
        public void RevisionDireccion_View_ShouldExposeAocrStatusAndGenerateAction()
        {
            var content = LeerArchivoRepositorio("CapaPresentacion\\Views\\InformeTecnico\\RevisionDireccion.cshtml");
            StringAssert.Contains(content, "Estado AOCR", "La revisión institucional debe mostrar el estado AOCR.");
            StringAssert.Contains(content, "Generar AOCR", "La revisión institucional debe exponer la acción Generar AOCR cuando corresponda.");
        }

        [TestMethod]
        public void GeneracionAocrService_ShouldNotDependOnFinancialApprovalToEnableInstitutionalGeneration()
        {
            var content = LeerArchivoRepositorio("CapaNegocio\\Services\\GeneracionAOCRService.cs");
            Assert.IsFalse(content.Contains("TieneAprobacionFinancieraSolicitud"), "La habilitación institucional de AOCR no debe volver a depender de la aprobación financiera.");
            Assert.IsFalse(content.Contains("Pendiente de aprobación del pago por Financiero."), "La evaluación AOCR no debe bloquear la generación por aprobación financiera una vez aprobado el informe institucional.");
            StringAssert.Contains(content, "EstadoSolicitud.AceptacionDocumental", "La evaluación AOCR debe aceptar solicitudes aún no resincronizadas desde Aceptación Documental cuando el informe ya fue aprobado por Dirección.");
        }

        [TestMethod]
        public void HealthDashboard_ShouldExposeLegacyAocrResyncMaintenanceAction()
        {
            var declaration = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\HealthController.cs", "ResyncLegacyAocrCases");
            StringAssert.Contains(declaration, "[HttpPost]", "La resincronización AOCR legacy debe ejecutarse por POST.");
            StringAssert.Contains(declaration, "[Authorize(Roles = \"Administrador\")]", "La resincronización AOCR legacy debe quedar restringida a Administrador.");
            StringAssert.Contains(declaration, "[ValidateAntiForgeryToken]", "La resincronización AOCR legacy debe exigir AntiForgeryToken.");

            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\Health\\Dashboard.cshtml");
            StringAssert.Contains(view, "Resincronizar AOCR legacy", "El dashboard de salud debe exponer la herramienta administrativa de resincronización AOCR.");
            StringAssert.Contains(view, "resyncLegacyAocrCases()", "El dashboard de salud debe cablear la acción de resincronización AOCR legacy.");

            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\GeneracionAOCRService.cs");
            StringAssert.Contains(service, "ResincronizarCasosLegacyPendientesAocr", "El servicio AOCR debe exponer la resincronización masiva de casos legacy.");
        }

        [TestMethod]
        public void HealthDashboard_ShouldExposeReadOnlyLegacyAocrInventoryAction()
        {
            var declaration = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\HealthController.cs", "PreviewLegacyAocrCandidates");
            StringAssert.Contains(declaration, "[HttpPost]", "La consulta AOCR legacy debe ejecutarse por POST.");
            StringAssert.Contains(declaration, "[Authorize(Roles = \"Administrador\")]", "La consulta AOCR legacy debe quedar restringida a Administrador.");
            StringAssert.Contains(declaration, "[ValidateAntiForgeryToken]", "La consulta AOCR legacy debe exigir AntiForgeryToken.");

            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\Health\\Dashboard.cshtml");
            StringAssert.Contains(view, "Consultar candidatas", "El dashboard debe exponer la consulta solo lectura de candidatas AOCR legacy.");
            StringAssert.Contains(view, "previewLegacyAocrCandidates()", "El dashboard debe cablear la consulta solo lectura de AOCR legacy.");

            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\GeneracionAOCRService.cs");
            StringAssert.Contains(service, "InventariarCasosLegacyPendientesAocr", "El servicio AOCR debe exponer el inventario solo lectura de casos legacy.");
        }

        [TestMethod]
        public void LegacyAocrResync_ShouldReportOnlyCandidatesReadyForResync()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\GeneracionAOCRService.cs");
            StringAssert.Contains(service, "resultado.Candidatas = inventario.ListasParaResync;", "La resincronización AOCR legacy debe reportar solo candidatas realmente listas para procesar.");
            Assert.IsFalse(service.Contains("resultado.Candidatas = inventario.LegacyPendientes;"), "La resincronización AOCR legacy no debe inflar candidateCount con legacy pendientes no procesables.");
        }

        [TestMethod]
        public void InspeccionWorkflow_ShouldDeriveInspectionResultFromSignedTechnicalReport()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\InspeccionWorkflowService.cs");
            StringAssert.Contains(service, "var resultadoInformeNormalizado = NormalizarResultadoInformeTecnico(informe.Resultado);", "El workflow de inspección debe derivar el resultado desde el Informe Técnico firmado.");
            StringAssert.Contains(service, "El resultado de inspección debe coincidir con el resultado del Informe Técnico firmado.", "El backend debe rechazar intentos de registrar un resultado distinto al del Informe Técnico.");
        }

        [TestMethod]
        public void InspeccionDetalle_ShouldNotOfferFreeResultSelectionWhenTechnicalReportAlreadyDecidesOutcome()
        {
            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\Inspeccion\\Detalle.cshtml");
            StringAssert.Contains(view, "<input type=\"hidden\" name=\"resultado\" value=\"@resultadoInformeNormalizado\" />", "La vista de inspección debe publicar el resultado derivado del Informe Técnico y no una selección libre divergente.");
            Assert.IsFalse(view.Contains("<select name=\"resultado\" class=\"form-control result-select\" required>"), "La vista no debe permitir seleccionar manualmente un resultado distinto al del Informe Técnico firmado.");
        }

        [TestMethod]
        public void AocrGating_ShouldRequireApprovedTechnicalReportWithSatisfactoryOutcome()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs");
            StringAssert.Contains(controller, "_aocrFinalWorkflowService.ValidarInspeccionSatisfactoriaParaAocr(id)", "La compuerta AOCR del controlador debe delegar la validación de inspección satisfactoria al servicio institucional final.");

            var finalWorkflowService = LeerArchivoRepositorio("CapaNegocio\\Services\\AocrFinalWorkflowService.cs");
            StringAssert.Contains(finalWorkflowService, "if (!InformeResultadoSatisfactorio(informeCandidato.Resultado))", "La compuerta AOCR del servicio final debe exigir un Informe Técnico satisfactorio al seleccionar la inspección válida.");
            StringAssert.Contains(finalWorkflowService, "No se puede avanzar a AOCR final sin una inspección satisfactoria con Informe Técnico aprobado y resultado satisfactorio.", "El mensaje institucional debe explicar que AOCR requiere informe técnico satisfactorio.");

            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\GeneracionAOCRService.cs");
            StringAssert.Contains(service, "if (!InformeResultadoPermiteGeneracionAocr(informe))", "La evaluación AOCR debe bloquear informes aprobados cuyo resultado no sea satisfactorio.");
            StringAssert.Contains(service, "El informe técnico aprobado no tiene resultado satisfactorio para habilitar AOCR.", "La evaluación AOCR debe informar cuando el informe aprobado no habilita la generación por su resultado.");
        }

        [TestMethod]
        public void InspeccionWorkflow_ShouldMaterializeNcWhenTechnicalReportIsInsatisfactory()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\InspeccionWorkflowService.cs");
            StringAssert.Contains(service, "AsegurarNoConformidadDesdeInforme", "El workflow debe asegurar una NC base cuando el Informe Técnico resulte insatisfactorio.");
            StringAssert.Contains(service, "Resultado insatisfactorio registrado. Se generó la no conformidad base", "El workflow debe dejar explícito que la ruta insatisfactoria activa una NC en backend.");
            StringAssert.Contains(service, "El resultado insatisfactorio debe indicar si requiere nueva inspección o subsanación documental.", "El backend debe exigir el subtipo institucional del resultado insatisfactorio.");
        }

        [TestMethod]
        public void InspeccionDetalle_ShouldExposeSubtypeDrivenInsatisfactoryGuidance()
        {
            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\Inspeccion\\Detalle.cshtml");
            StringAssert.Contains(view, "Ruta no satisfactoria activa:", "La vista de inspección debe explicar que el branch insatisfactorio activa la ruta de NC.");
            StringAssert.Contains(view, "data-bs-target=\"#modalHallazgo\"", "La vista debe ofrecer acceso directo al registro o complemento de NC/hallazgos desde el resultado insatisfactorio.");
        }

        [TestMethod]
        public void InspeccionNcCoordinatorActions_ShouldRequireCoordinatorRole()
        {
            var solicitarNueva = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\InspeccionController.cs", "SolicitarNueva");
            StringAssert.Contains(solicitarNueva, "[Authorize(Roles = ROL_COORD + \",\" + ROL_COORD_ALIAS + \",\" + ROL_COORD_GRUPO + \",\" + ROL_JEFATURA + \",\" + ROL_ADMIN)]", "SolicitarNueva debe seguir restringido a coordinación/jefatura o administrador.");

            var aprobarNcSubsanacion = ObtenerDeclaracionMetodo("CapaPresentacion\\Controllers\\InspeccionController.cs", "AprobarNcSubsanacionDocumental");
            StringAssert.Contains(aprobarNcSubsanacion, "[Authorize(Roles = ROL_COORD + \",\" + ROL_COORD_ALIAS + \",\" + ROL_COORD_GRUPO + \",\" + ROL_JEFATURA + \",\" + ROL_ADMIN)]", "AprobarNcSubsanacionDocumental debe seguir restringido a coordinación/jefatura o administrador.");
        }

        [TestMethod]
        public void InspeccionWorkflow_ShouldRequireFormalNcApprovalBeforeRoutingRtOrNewInspection()
        {
            var service = LeerArchivoRepositorio("CapaNegocio\\Services\\InspeccionWorkflowService.cs");
            StringAssert.Contains(service, "ValidarAprobacionNoConformidad", "El workflow debe validar la NC antes de formalizar la ruta institucional del resultado insatisfactorio.");
            StringAssert.Contains(service, "No se puede aprobar la ruta de NC sin al menos una no conformidad abierta.", "La decisión institucional debe exigir una NC abierta previa.");
            StringAssert.Contains(service, "AprobarNoConformidadParaSubsanacionDocumental", "El workflow debe exponer una acción explícita para habilitar subsanación documental del RT.");
        }

        [TestMethod]
        public void InspeccionDetalle_ShouldExposeCoordinatorNcDecisionButtonsAndHideRtUploadUntilApproved()
        {
            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\Inspeccion\\Detalle.cshtml");
            StringAssert.Contains(view, "Aprobar NC y solicitar nueva inspección", "La vista debe exponer la decisión formal de coordinación para la ruta con nueva inspección.");
            StringAssert.Contains(view, "Aprobar NC y habilitar subsanación RT", "La vista debe exponer la decisión formal de coordinación para la ruta de subsanación documental.");
            StringAssert.Contains(view, "La subsanación documental del RT se habilitará cuando coordinación apruebe formalmente la NC", "La vista debe bloquear al RT hasta que coordinación apruebe la NC.");
        }

        [TestMethod]
        public void InspeccionController_ShouldBlockRtCorrectionUploadBeforeCoordinatorNcApproval()
        {
            var controller = LeerArchivoRepositorio("CapaPresentacion\\Controllers\\InspeccionController.cs");
            StringAssert.Contains(controller, "EstadoPermiteCargaCorreccionSolicitante", "El controlador debe validar el estado antes de aceptar correcciones del RT.");
            StringAssert.Contains(controller, "La subsanación documental del RT solo se habilita después de la aprobación formal de la no conformidad por coordinación.", "El backend debe rechazar la carga temprana de subsanaciones del RT.");
        }

        [TestMethod]
        public void SidebarCoordinatorMenu_ShouldExposeDirectNcShortcut()
        {
            var menu = LeerArchivoRepositorio("CapaPresentacion\\Views\\Shared\\Menus\\_MenuCoordinador.cshtml");
            StringAssert.Contains(menu, "Observaciones / NC", "El sidebar del coordinador debe exponer un acceso directo al panel de observaciones y no conformidades.");
            StringAssert.Contains(menu, "#pane-observaciones", "El acceso directo del coordinador debe abrir el panel Observaciones / NC del dashboard.");
        }

        [TestMethod]
        public void SidebarRtMenu_ShouldExposeFunctionalAocrModules()
        {
            var menu = LeerArchivoRepositorio("CapaPresentacion\\Views\\Shared\\Menus\\_MenuRT.cshtml");
            StringAssert.Contains(menu, "Solicitud Formal AOCR", "El sidebar RT debe separar la solicitud formal AOCR como módulo propio.");
            StringAssert.Contains(menu, "Modificación de Condiciones y Limitaciones", "El sidebar RT debe exponer explícitamente la ruta de modificación de condiciones y limitaciones.");
            StringAssert.Contains(menu, "tipoSolicitud = 3", "La navegación RT debe reutilizar la ruta real de modificación usando tipoSolicitud = 3.");
        }

        [TestMethod]
        public void RoleGroupingHelper_ShouldTreatDcavAsSpecificCertificationDirector()
        {
            var helper = LeerArchivoRepositorio("CapaPresentacion\\Helpers\\RoleGroupingHelper.cs");
            StringAssert.Contains(helper, "DirectorCertificacionesDcav", "La normalizacion de roles debe contemplar DCAV como rol institucional propio.");
        }

        [TestMethod]
        public void DashboardInspeccion_ShouldHonorHashShortcutForNcPane()
        {
            var view = LeerArchivoRepositorio("CapaPresentacion\\Views\\CoordinacionJefatura\\DashboardInspeccion.cshtml");
            StringAssert.Contains(view, "function resolvePaneFromHash()", "El dashboard de inspección debe poder resolver el pane inicial desde el hash de la URL.");
            StringAssert.Contains(view, "activateTab(resolvePaneFromHash());", "El dashboard debe activar el tab correcto cuando se abre desde un atajo del sidebar.");
        }

        private static void AssertAuthorizePresent(string relativeControllerPath, string actionName)
        {
            var declaration = ObtenerDeclaracionMetodo(relativeControllerPath, actionName);
            StringAssert.Contains(declaration, "[Authorize]", "La acción " + actionName + " debe estar protegida con Authorize.");
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

        private static string ObtenerRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        }
    }
}
