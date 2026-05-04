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
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "GenerarCondicionesLimitacionesModificacion", "Inspector,Administrador");
        }

        [TestMethod]
        public void SolicitudAocr_ModificationCoordinatorActions_ShouldRequireCoordinatorRole()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "RevisarCondicionesLimitacionesModificacion", "Coordinador,CoordinadorInspecciones,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "EnviarCondicionesLimitacionesDcav", "Coordinador,CoordinadorInspecciones,Administrador");
        }

        [TestMethod]
        public void SolicitudAocr_ModificationDownload_ShouldRequireAuthentication()
        {
            AssertAuthorizePresent("CapaPresentacion\\Controllers\\SolicitudAOCRController.cs", "DescargarCondicionesLimitacionesModificacion");
        }

        [TestMethod]
        public void CoordinacionJefatura_DigitalSignatureBootstrap_ShouldRequireInstitutionalRoles()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "CargarDatosFirmaDigitalAocr", "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");
        }

        [TestMethod]
        public void CoordinacionJefatura_ModificationDocumentWorkflow_ShouldExposeExpectedRoleContracts()
        {
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "EditarDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "PreviewDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "GenerarDocumentoValidacionAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");
            AssertAuthorizeRoles("CapaPresentacion\\Controllers\\CoordinacionJefaturaController.cs", "GuardarPosicionFirmaAocr", "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador");
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
            var repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var controllerPath = Path.Combine(repoRoot, relativeControllerPath);
            Assert.IsTrue(File.Exists(controllerPath), "No se encontró el archivo del controlador: " + controllerPath);

            var content = File.ReadAllText(controllerPath);
            var pattern = @"(?ms)(\[[^\]]+\]\s*)+public\s+(?:JsonResult|ActionResult)\s+" + Regex.Escape(actionName) + @"\s*\(";
            var match = Regex.Match(content, pattern);
            Assert.IsTrue(match.Success, "No se encontró la declaración pública de la acción " + actionName + " en " + controllerPath);

            return match.Value;
        }
    }
}