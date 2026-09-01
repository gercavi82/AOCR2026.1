using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class InspectorFinalDocumentsTests
    {
        [TestMethod] public void Caso01_InspectorVeUnaSolaOpcion() { Assert.AreEqual(1, Count(InspectorBranch(), "\"ins-certificados\"")); }
        [TestMethod] public void Caso02_RtNoVeLaOpcion() { Assert.IsFalse(RoleBranch("else if (context.EsSolicitanteORT)", "group.Visible").Contains("ins-certificados")); }
        [TestMethod] public void Caso03_FinancieroNoVeLaOpcion() { Assert.IsFalse(RoleBranch("else if (context.EsFinancieroRol)", "else if (context.EsSolicitanteORT)").Contains("ins-certificados")); }
        [TestMethod] public void Caso04_CoordinadorNoVeLaOpcion() { Assert.IsFalse(RoleBranch("else if (context.EsCoordinadorRol", "else if (context.EsInspectorRol)").Contains("ins-certificados")); }
        [TestMethod] public void Caso05_DirdacNoVeLaOpcion() { Assert.IsFalse(RoleBranch("else if (context.EsDirdacRol)", "else if (context.EsCoordinadorRol").Contains("ins-certificados")); }
        [TestMethod] public void Caso06_DcavNoVeLaOpcion() { Assert.IsFalse(RoleBranch("if (context.EsDcavRol)", "else if (context.EsDirdacRol)").Contains("ins-certificados")); }

        [TestMethod]
        public void Caso07_RutaUsaUrlAction()
        {
            StringAssert.Contains(InspectorBranch(), "\"Inspeccion\", \"PendientesEmisionAocr\"");
            StringAssert.Contains(Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs"), "context.Url.Action(action, controller, routeValues)");
        }

        [TestMethod]
        public void Caso08_RutaToleraDirectorioVirtualAocr()
        {
            Assert.IsFalse(InspectorBranch().Contains("/aocr/"));
            Assert.IsFalse(InspectorBranch().Contains("href=\"/"));
        }

        [TestMethod] public void Caso09_EnlaceAbreBandejaReal() { StringAssert.Contains(InspectorBranch(), "\"PendientesEmisionAocr\""); }
        [TestMethod] public void Caso10_ActivoEnBandeja() { StringAssert.Contains(InspectorBranch(), "new[] { \"PendientesEmisionAocr\", \"RedactarEspecificaciones\" }"); }
        [TestMethod] public void Caso11_ActivoEnRedaccion() { Caso10_ActivoEnBandeja(); }

        [TestMethod]
        public void Caso12_ContadorUsaMismaConsultaQueBandeja()
        {
            var service = Read("CapaNegocio/Services/InspectorBandejaService.cs");
            StringAssert.Contains(service, "return ObtenerPendientesDocumentosFinales(context).Count;");
            StringAssert.Contains(service, "EmisionAocrCondiciones = ContarPendientesDocumentosFinales(context)");
        }

        [TestMethod]
        public void Caso13_FiltraAsignacionDelInspectorAutenticado()
        {
            var service = Read("CapaNegocio/Services/InspectorBandejaService.cs");
            StringAssert.Contains(service, "foreach (var inspeccion in ObtenerInspeccionesAsignadas(context))");
            StringAssert.Contains(service, "ResolverInspectorFilterIds(context)");
        }

        [TestMethod]
        public void Caso14_FiltraAprobacionDirdac()
        {
            var service = Read("CapaNegocio/Services/InspectorBandejaService.cs");
            StringAssert.Contains(service, "EsInformeAprobadoDirdac(informe)");
            StringAssert.Contains(service, "AocrEstadosProceso.InformeTecnicoAprobadoDirdac");
            StringAssert.Contains(service, "AocrEstadosProceso.DocumentosFinalesPorGenerar");
        }

        [TestMethod]
        public void Caso15_InformePendienteNoAparece()
        {
            var service = Read("CapaNegocio/Services/InspectorBandejaService.cs");
            StringAssert.Contains(service, "!informe.Finalizado || !informe.FirmadoInspector");
            StringAssert.Contains(service, "if (!EsInformeAprobadoDirdac(informe))");
        }

        [TestMethod]
        public void Caso16_NoConformidadAbiertaNoAparece()
        {
            StringAssert.Contains(Read("CapaNegocio/Services/InspectorBandejaService.cs"), "ContarAbiertasRelacionadasConInspeccion(inspeccion.CodigoInspeccion) > 0");
        }

        [TestMethod]
        public void Caso17_ExpedienteAjenoRetorna403SinExponerDatos()
        {
            var controller = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(controller, "[Authorize(Roles = ROL_INSPECTOR)]");
            StringAssert.Contains(controller, "La inspección no está asignada al Inspector autenticado.");
            StringAssert.Contains(controller, "new HttpStatusCodeResult(403");
        }

        [TestMethod]
        public void Caso18_EmisionRequiereAocrYCondiciones()
        {
            var plan = new AocrCierrePorTipoTramiteService().Resolver(new SolicitudAOCR { TipoSolicitud = 1 });
            Assert.IsTrue(plan.GenerarAocr && plan.GenerarCondiciones);
        }

        [TestMethod]
        public void Caso19_RenovacionRequiereAocrYCondiciones()
        {
            var plan = new AocrCierrePorTipoTramiteService().Resolver(new SolicitudAOCR { TipoSolicitud = 2 });
            Assert.IsTrue(plan.GenerarAocr && plan.GenerarCondiciones);
        }

        [TestMethod]
        public void Caso20_ModificacionRespetaModulo8()
        {
            var plan = new AocrCierrePorTipoTramiteService().Resolver(new SolicitudAOCR { TipoSolicitud = 3 });
            Assert.IsFalse(plan.GenerarAocr);
            Assert.IsTrue(plan.GenerarCondiciones);
            Assert.AreEqual("MODULO_8", plan.Modulo);
        }

        [TestMethod]
        public void Caso21_DobleClicNoDuplicaBorradorOPdf()
        {
            var controller = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            var dao = Read("CapaDatos/DAOs/AocrDocumentoGeneradoDAO.cs");
            StringAssert.Contains(controller, "if (docExistente != null)");
            StringAssert.Contains(dao, "pg_advisory_xact_lock");
            StringAssert.Contains(dao, "WHERE NOT EXISTS (SELECT 1 FROM misma_evidencia)");
        }

        [TestMethod]
        public void Caso22_VistaRazorEsTipadaYCompilable()
        {
            var view = Read("CapaPresentacion/Views/Inspeccion/PendientesEmisionAocr.cshtml");
            StringAssert.StartsWith(view.TrimStart(), "@model CapaPresentacion.Models.ViewModels.PendienteEmisionAocrViewModel");
            Assert.IsFalse(view.Contains("ViewBag.Inspecciones"));
        }

        [TestMethod]
        public void Caso23_BandejaNoIntroduceJavascript()
        {
            var view = Read("CapaPresentacion/Views/Inspeccion/PendientesEmisionAocr.cshtml");
            Assert.IsFalse(view.Contains("<script"));
            Assert.IsFalse(view.Contains("javascript:"));
        }

        [TestMethod]
        public void Caso24_OtrosRolesConservanSusEntradas()
        {
            var body = RoleMatrixBody();
            foreach (var key in new[] { "rt-solicitudes", "financiero-pagos-revisar", "coordinador-asignacion", "dirdac-revision-informes", "dcav-condiciones-pendientes" })
                Assert.AreEqual(1, Count(body, "\"" + key + "\""), key);
        }

        [TestMethod]
        public void Caso25_PdfFinalSeGuardaEnRepositorioInstitucionalConfigurado()
        {
            var service = Read("CapaPresentacion/Services/FirmaAocrServices.cs");
            StringAssert.Contains(service, "FileStorageHelper.GetPhysicalBasePath(carpetaRelativa)");
            StringAssert.Contains(service, "return FileStorageHelper.ResolvePhysicalPath(ruta);");
        }

        [TestMethod]
        public void Caso26_VisorPdfAceptaRepositorioInstitucionalConfigurado()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            var servirPdf = Slice(controller, "private ActionResult ServirPdf", "private SidebarPermissionSnapshot ObtenerPermisosRolActivo");
            StringAssert.Contains(servirPdf, "FileStorageHelper.GetAllowedStorageRoots()");
            StringAssert.Contains(servirPdf, "new DocumentoSeguroService(raicesPermitidas");
        }

        [TestMethod]
        public void Caso27_DescargaAgregaNombreSoloDespuesDeValidarArchivo()
        {
            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            var servirPdf = Slice(controller, "private ActionResult ServirPdf", "private SidebarPermissionSnapshot ObtenerPermisosRolActivo");
            var validar = servirPdf.IndexOf("if (!archivo.EsValido)", StringComparison.Ordinal);
            var descargar = servirPdf.IndexOf("return File(archivo.RutaFisica, archivo.Mime, nombre);", StringComparison.Ordinal);
            Assert.IsTrue(validar >= 0 && descargar > validar);
        }

        [TestMethod]
        public void Caso28_InspectorCapturaFirmantesInstitucionalesSeparados()
        {
            var view = Read("CapaPresentacion/Views/FirmaAocr/Index.cshtml");
            StringAssert.Contains(view, "name=\"NombreDirectorGeneral\"");
            StringAssert.Contains(view, "name=\"NombreDirectorCertificacion\"");
        }

        [TestMethod]
        public void Caso29_CadaPdfUsaElFirmanteQueCorrespondeAlCargo()
        {
            var reconocimiento = Read("CapaPresentacion/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml");
            var condiciones = Read("CapaPresentacion/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml");
            StringAssert.Contains(reconocimiento, "Model.NombreDirectorGeneral");
            StringAssert.Contains(condiciones, "Model.NombreDirectorCertificacion");
            StringAssert.Contains(condiciones, "Director of Aeronautical Certification and Continuous Surveillance");
        }

        [TestMethod]
        public void Caso30_MigracionPersisteAmbosFirmantesEnColumnasSeparadas()
        {
            var migration = Read("scripts/sql/20260828_certificado_firmantes_institucionales.sql");
            StringAssert.Contains(migration, "ADD COLUMN IF NOT EXISTS emitido_por");
            StringAssert.Contains(migration, "ADD COLUMN IF NOT EXISTS aprobado_por");

            var dao = Read("CapaDatos/DAOs/CertificadoDAO.cs");
            StringAssert.Contains(dao, "EmitidoPor = ResolverColumnaOpcional(cn, TablaCertificado, \"emitido_por\"");
            StringAssert.Contains(dao, "AprobadoPor = ResolverColumnaOpcional(cn, TablaCertificado, \"aprobado_por\"");
        }

        [TestMethod]
        public void Caso31_CamposEditablesSeNormalizanAMayusculasEnClienteYServidor()
        {
            var script = Read("CapaPresentacion/Scripts/firma-aocr.js");
            StringAssert.Contains(script, "input[type=\"text\"]:not([readonly])");
            StringAssert.Contains(script, "toLocaleUpperCase('es-EC')");

            var controller = Read("CapaPresentacion/Controllers/FirmaAocrController.cs");
            StringAssert.Contains(controller, "estadoExplotador = NormalizarMayusculas(estadoExplotador)");
            StringAssert.Contains(controller, "nombreDirectorGeneral = NormalizarMayusculas(nombreDirectorGeneral)");
            StringAssert.Contains(controller, "nombreDirectorCertificacion = NormalizarMayusculas(nombreDirectorCertificacion)");
            StringAssert.Contains(controller, "valor.Trim().ToUpperInvariant()");
        }

        private static string InspectorBranch() { return RoleBranch("else if (context.EsInspectorRol)", "else if (context.EsFinancieroRol)"); }
        private static string RoleBranch(string start, string end) { return Slice(RoleMatrixBody(), start, end); }
        private static string RoleMatrixBody() { return Slice(Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs"), "private static SidebarMenuGroupViewModel BuildRoleWorkMenuGroup", "private static int? PendingBadge"); }
        private static int Count(string source, string token) { return source.Split(new[] { token }, StringSplitOptions.None).Length - 1; }
        private static string Slice(string source, string start, string end)
        {
            var a = source.IndexOf(start, StringComparison.Ordinal);
            var b = source.IndexOf(end, a + start.Length, StringComparison.Ordinal);
            Assert.IsTrue(a >= 0 && b > a, "No se encontró el segmento: " + start);
            return source.Substring(a, b - a);
        }
        private static string Read(string path) { return File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Root() { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")); }
    }
}
