using System;
using System.IO;
using System.Linq;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// Pruebas unitarias de flujo y compuertas de seguridad para AC-04:
    /// El Coordinador recibe la revisión documental finalizada por el Inspector y la remite a DIRCAV.
    /// </summary>
    [TestClass]
    public class Ac04CoordinadorRevisionDocumentalTests
    {
        [DataTestMethod]
        [DataRow(0, 0, false)]
        [DataRow(3, 1, false)]
        [DataRow(3, 0, true)]
        [DataRow(3, -1, false)]
        public void FinalizacionDocumental_ExigeDocumentosConResultado(int documentos, int pendientes, bool esperado)
        {
            Assert.AreEqual(esperado, RevisionDocumentalCoordinadorService.PuedeFinalizarRevision(documentos, pendientes));
        }

        [DataTestMethod]
        [DataRow("PENDIENTE_COORDINADOR", true)]
        [DataRow("DEVUELTO_COORDINADOR", true)]
        [DataRow("DEVUELTO_COORDINADOR_POR_DIRCAV", true)]
        [DataRow("PENDIENTE_DIRCAV", false)]
        [DataRow("AOCR_PENDIENTE_DIRDAC", false)]
        [DataRow("DEVUELTO_INSPECTOR", false)]
        [DataRow(null, false)]
        public void BandejaYDecisionesCoordinador_CompartenEstadosDocumentales(string estado, bool esperado)
        {
            Assert.AreEqual(esperado, CoordinacionBandejaService.EsRevisionDocumentalPendiente(estado));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("0")]
        [DataRow("-1")]
        [DataRow("invalido")]
        public void DecisionesCoordinador_SinIdentidadDeSesion_Devuelven401(string usuarioId)
        {
            var controller = CrearControladorCoordinador(usuarioId, "COORDINADOR", true);
            Assert.AreEqual(401, ((System.Web.Mvc.HttpStatusCodeResult)controller.RemitirADircav(999, "Revisado")).StatusCode);
            Assert.AreEqual(401, ((System.Web.Mvc.HttpStatusCodeResult)controller.DevolverAlInspector(999, "Corregir")).StatusCode);
        }

        [TestMethod]
        public void DecisionesCoordinador_IdentidadSinAutenticacion_Devuelven401()
        {
            var controller = CrearControladorCoordinador("7", "COORDINADOR", false);
            Assert.AreEqual(401, ((System.Web.Mvc.HttpStatusCodeResult)controller.RemitirADircav(999, "Revisado")).StatusCode);
            Assert.AreEqual(401, ((System.Web.Mvc.HttpStatusCodeResult)controller.DevolverAlInspector(999, "Corregir")).StatusCode);
        }

        [TestMethod]
        public void DecisionesCoordinador_SesionCreadaPorLogin_LlegaAValidacionDeSolicitud()
        {
            var controller = CrearControladorCoordinador("7", "COORDINADOR", true);
            Assert.AreEqual(400, ((System.Web.Mvc.HttpStatusCodeResult)controller.RemitirADircav(0, "Revisado")).StatusCode);
            Assert.AreEqual(400, ((System.Web.Mvc.HttpStatusCodeResult)controller.DevolverAlInspector(0, "Corregir")).StatusCode);
        }

        [DataTestMethod]
        [DataRow("ADMINISTRADOR")]
        [DataRow("INSPECTOR")]
        [DataRow("DIRCAV")]
        [DataRow("DIRDAC")]
        [DataRow("DCAV")]
        [DataRow("Coordinacion")]
        public void DecisionesCoordinador_RolActivoIncorrectoAunquePrincipalTengaCoordinador_Devuelven403(string rol)
        {
            var controller = CrearControladorCoordinador("7", rol, true);
            Assert.AreEqual(403, ((System.Web.Mvc.HttpStatusCodeResult)controller.RemitirADircav(999, "Revisado")).StatusCode);
            Assert.AreEqual(403, ((System.Web.Mvc.HttpStatusCodeResult)controller.DevolverAlInspector(999, "Corregir")).StatusCode);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void ServicioDocumental_RechazaIdentidadInvalidaAntesDeAccederABase(int usuarioId)
        {
            var service = new RevisionDocumentalCoordinadorService();
            Assert.IsFalse(service.RemitirADircav(999, usuarioId, "Revisado", "usuario").Ok);
            Assert.IsFalse(service.DevolverAlInspector(999, usuarioId, "Corregir", "usuario").Ok);
            Assert.IsFalse(service.FinalizarRevisionDocumentalInspector(999, usuarioId, "Revisado").Ok);
            Assert.IsFalse(service.Observar(999, usuarioId, "Corregir").Ok);
            Assert.IsFalse(service.Aceptar(999, usuarioId, 8, "Revisado").Ok);
        }

        [TestMethod]
        public void Remision_ObservacionInvalida_NoSeSustituyePorAprobacionPredeterminada()
        {
            var service = new RevisionDocumentalCoordinadorService();
            foreach (var observacion in new[] { "<p>Revisado</p>", new string('a', 2001) })
            {
                var result = service.RemitirADircav(999, 7, observacion, "usuario");
                Assert.IsFalse(result.Ok);
                StringAssert.Contains(result.Mensaje, "2000");
            }
        }

        [DataTestMethod]
        [DataRow("DCAV")]
        [DataRow("DIRDAC")]
        [DataRow("ADMINISTRADOR")]
        public void Dircav_DecisionesDocumentales_NoAceptanAliasNiOtrosRoles(string rol)
        {
            var service = new DircavDesignacionService();
            Assert.AreEqual(403, service.AceptarDocumentacion(999, 7, "usuario", rol).HttpStatusCode);
            Assert.AreEqual(403, service.DevolverAlCoordinador(999, 7, "usuario", "Corregir", rol).HttpStatusCode);
        }

        [TestMethod]
        public void Dircav_DecisionesDocumentales_SinIdentidad_RechazanAntesDeConsultarExpediente()
        {
            var service = new DircavDesignacionService();
            Assert.AreEqual(401, service.AceptarDocumentacion(999, 0, "usuario", "DIRCAV").HttpStatusCode);
            Assert.AreEqual(401, service.DevolverAlCoordinador(999, 0, "usuario", "Corregir", "DIRCAV").HttpStatusCode);
        }

        private static CapaPresentacion.Controllers.CoordinacionJefaturaController CrearControladorCoordinador(string id, string rol, bool autenticado)
        {
            var session = new MockSession();
            session["UserId"] = id;
            session["Rol"] = rol;
            var identity = new MockIdentity { IsAuthenticated = autenticado, Name = "prueba" };
            var principal = new System.Security.Principal.GenericPrincipal(identity, new[] { "Coordinador" });
            var context = new MockHttpContext(principal, session);
            var controller = new CapaPresentacion.Controllers.CoordinacionJefaturaController();
            controller.ControllerContext = new System.Web.Mvc.ControllerContext(context, new System.Web.Routing.RouteData(), controller);
            return controller;
        }

        private readonly IAocrFlujoService _flujoService = new AocrFlujoService();
        private readonly IAocrEstadoService _estadoService = new AocrEstadoService();
        private readonly DircavBandejaService _dircavBandejaService = new DircavBandejaService();

        private static string Read(string relativePath)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, relativePath);
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "..", "..", "..", relativePath);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(@"c:\proyectos\AOCR", relativePath);
            }
            return File.ReadAllText(path);
        }

        [TestMethod]
        public void Test01_InspectorFinalizaRevision_PasaEstrictamenteAPendienteCoordinador()
        {
            // 1. Validar que la transición de EN_REVISION a PENDIENTE_COORDINADOR está permitida en la matriz de flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(EstadoSolicitud.EnRevision, AocrEstadosProceso.PendienteCoordinador),
                "La transición de EN_REVISION hacia PENDIENTE_COORDINADOR debe estar permitida.");

            // 2. Validar que en el controlador del inspector, al finalizar la revisión, se persiste PENDIENTE_COORDINADOR
            var controllerContent = Read("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            StringAssert.Contains(controllerContent, "AocrEstadosProceso.PendienteCoordinador");
            StringAssert.Contains(controllerContent, "_correoService.NotificarEvento(solicitud, \"PENDIENTE_COORDINADOR\"");
        }

        [TestMethod]
        public void Test02_CoordinadorDevuelveConComentario_PasaADevueltoInspector()
        {
            // 1. Validar que la transición de PENDIENTE_COORDINADOR a DEVUELTO_INSPECTOR está permitida
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.PendienteCoordinador, AocrEstadosProceso.DevueltoInspector),
                "El Coordinador debe poder devolver la solicitud pasando a DEVUELTO_INSPECTOR.");

            // 2. Validar que el rol Coordinacion tiene permiso para ejecutar la acción de devolución
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.CoordinadorDevolverInspector),
                "El rol Coordinación debe poder ejecutar la acción CoordinadorDevolverInspector.");
        }

        [TestMethod]
        public void Test03_CoordinadorDevuelveSinComentario_RechazaConError()
        {
            var service = new RevisionDocumentalCoordinadorService();
            // Comentario vacío o espacios debe fallar
            var resultadoVacio = service.DevolverAlInspector(999, 1, "", "coordinador");
            Assert.IsFalse(resultadoVacio.Ok);
            StringAssert.Contains(resultadoVacio.Mensaje, "obligatorio");

            var resultadoEspacios = service.DevolverAlInspector(999, 1, "   ", "coordinador");
            Assert.IsFalse(resultadoEspacios.Ok);
            StringAssert.Contains(resultadoEspacios.Mensaje, "obligatorio");
        }

        [TestMethod]
        public void Test04_InspectorCorrigeYReenvia_PasaAPendienteCoordinador()
        {
            // 1. Validar que DEVUELTO_INSPECTOR es revisable por el Inspector
            Assert.IsTrue(_estadoService.EsEstadoRevisablePorInspector("DEVUELTO_INSPECTOR"),
                "El Inspector debe poder revisar expedientes devueltos para subsanar.");

            // 2. Validar que DEVUELTO_INSPECTOR puede transicionar nuevamente a PENDIENTE_COORDINADOR tras la corrección
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.DevueltoInspector, AocrEstadosProceso.PendienteCoordinador),
                "El reenvío de DEVUELTO_INSPECTOR a PENDIENTE_COORDINADOR debe ser válido.");
        }

        [TestMethod]
        public void Test05_CoordinadorRemite_PasaAPendienteDircav()
        {
            // 1. Validar que la transición de PENDIENTE_COORDINADOR a PENDIENTE_DIRCAV está permitida
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.PendienteCoordinador, AocrEstadosProceso.PendienteDircav),
                "La remisión de PENDIENTE_COORDINADOR a PENDIENTE_DIRCAV debe ser válida.");

            // 2. Validar que el rol Coordinacion tiene permiso para ejecutar la remisión a DIRCAV
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "El rol Coordinación debe poder ejecutar la acción CoordinadorRemitirDircav.");
        }

        [TestMethod]
        public void Test06_InspectorIntentaRemitirADircavODirdac_Retorna403Forbidden()
        {
            // Inspector no puede ejecutar acciones de Coordinador ni de DIRCAV
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "El Inspector tiene prohibido remitir a DIRCAV.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.DircavRemitirDirdac),
                "El Inspector tiene prohibido remitir a DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.EnviarDirdac),
                "El Inspector tiene prohibido saltar a DIRDAC.");

            // Validar en controlador
            var controllerContent = Read("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(controllerContent, "AocrRolesInstitucionales.EsInspector(rolSesion)");
            StringAssert.Contains(controllerContent, "El Inspector no tiene permisos para remitir a DIRCAV ni a DIRDAC");
        }

        [TestMethod]
        public void Test07_CoordinadorIntentaRemitirADirdacDirectamente_Retorna403Forbidden()
        {
            // El Coordinador no puede saltarse a DIRCAV y remitir a DIRDAC directamente
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.EnviarDirdac),
                "El Coordinador NUNCA puede remitir directamente a DIRDAC en esta etapa.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.DircavRemitirDirdac),
                "El Coordinador no tiene rol DIRCAV para remitir a DIRDAC.");
        }

        [TestMethod]
        public void Test08_AdministradorIntentaDevolverORemitir_Retorna403Forbidden()
        {
            // Regla 7: El Administrador nunca firma ni remite operativamente
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.CoordinadorDevolverInspector),
                "El Administrador tiene prohibido devolver operativamente al Inspector (Regla 7).");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "El Administrador tiene prohibido remitir operativamente a DIRCAV (Regla 7).");

            var controllerContent = Read("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(controllerContent, "El Administrador no puede ejecutar devoluciones operativas (Regla 7)");
            StringAssert.Contains(controllerContent, "El Administrador no puede ejecutar remisiones operativas (Regla 7)");
        }

        [TestMethod]
        public void Test09_DobleClicOEstadoIncompatible_Retorna409Conflict()
        {
            var controllerContent = Read("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            // Validar que ambos endpoints comprueban el estado PENDIENTE_COORDINADOR y retornan HttpStatusCode.Conflict ante desincronización
            StringAssert.Contains(controllerContent, "HttpStatusCode.Conflict, \"La solicitud no se encuentra en estado PENDIENTE_COORDINADOR");
        }

        [TestMethod]
        public void Test10_BandejaDircav_NoMuestraSolicitudesEnPendienteCoordinadorNiDevueltoInspector()
        {
            var serviceContent = Read("CapaNegocio/Services/DircavBandejaService.cs");
            // Validar que la bandeja de DIRCAV excluye taxativamente PENDIENTE_COORDINADOR y DEVUELTO_INSPECTOR
            StringAssert.Contains(serviceContent, "!string.Equals(s.Estado, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase)");
            StringAssert.Contains(serviceContent, "!string.Equals(s.Estado, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase)");
        }

        [TestMethod]
        public void Test11_TransaccionAtómicaYAuditoriaUnica()
        {
            var serviceContent = Read("CapaNegocio/Services/RevisionDocumentalCoordinadorService.cs");
            StringAssert.Contains(serviceContent, "RegistrarEventoHistorialRevision");
            StringAssert.Contains(serviceContent, "\"DEVUELTO_INSPECTOR\"");
            StringAssert.Contains(serviceContent, "\"PENDIENTE_DIRCAV\"");
        }

        [TestMethod]
        public void Test12_BandejaCoordinador_EstructuraCompletaDeColumnasYAcciones()
        {
            var viewContent = Read("CapaPresentacion/Views/CoordinacionJefatura/_BandejaControlDocumental.cshtml");
            // Validar las 10 columnas obligatorias
            StringAssert.Contains(viewContent, "<th>Solicitud</th>");
            StringAssert.Contains(viewContent, "<th>Compañía</th>");
            StringAssert.Contains(viewContent, "<th>RT</th>");
            StringAssert.Contains(viewContent, "<th>Inspector</th>");
            StringAssert.Contains(viewContent, "<th>Resultado</th>");
            StringAssert.Contains(viewContent, "<th>Comentarios</th>");
            StringAssert.Contains(viewContent, "<th>Documentos</th>");
            StringAssert.Contains(viewContent, "<th>Fecha</th>");
            StringAssert.Contains(viewContent, "<th>Estado</th>");
            StringAssert.Contains(viewContent, "<th>Historial</th>");

            // Validar las 3 acciones obligatorias
            StringAssert.Contains(viewContent, "title=\"Ver expediente\"");
            StringAssert.Contains(viewContent, "title=\"Devolver al Inspector con comentario\"");
            StringAssert.Contains(viewContent, "title=\"Remitir oficialmente a DIRCAV\"");
            StringAssert.Contains(viewContent, "Html.BeginForm(\"DevolverAlInspector\", \"CoordinacionJefatura\"");
            StringAssert.Contains(viewContent, "Html.BeginForm(\"RemitirADircav\", \"CoordinacionJefatura\"");
        }
    }
}
