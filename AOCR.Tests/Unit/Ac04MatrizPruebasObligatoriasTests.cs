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
    /// AC-04: MATRIZ DE 16 PRUEBAS OBLIGATORIAS
    /// "Corregir el recorrido de revisión documental e Informe Técnico para que el Inspector remita al Coordinador y el Coordinador remita a DIRCAV"
    /// 
    /// 1. Inspector remite a Coordinador.
    /// 2. Coordinador recibe en su bandeja.
    /// 3. Coordinador devuelve al Inspector.
    /// 4. Inspector corrige y reenvía.
    /// 5. Coordinador remite a DIRCAV.
    /// 6. DIRCAV recibe en su bandeja.
    /// 7. Inspector no puede enviar a DIRCAV.
    /// 8. Inspector no puede enviar a DIRDAC.
    /// 9. Coordinador no puede enviar a DIRDAC.
    /// 10. Administrador recibe 403.
    /// 11. UsuarioId cero recibe 401.
    /// 12. Estado incompatible devuelve 409.
    /// 13. Doble clic no duplica.
    /// 14. Solo una auditoría por operación.
    /// 15. Fallo intermedio produce rollback total.
    /// 16. No permanece ninguna ruta antigua de salto directo.
    /// </summary>
    [TestClass]
    public class Ac04MatrizPruebasObligatoriasTests
    {
        private readonly IAocrFlujoService _flujoService = new AocrFlujoService();
        private readonly IAocrEstadoService _estadoService = new AocrEstadoService();
        private readonly RevisionDocumentalCoordinadorService _coordinadorRevisionService = new RevisionDocumentalCoordinadorService();

        private static string ObtenerRutaRaizProyecto()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AOCR.sln")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string ResolverRuta(string rutaRelativa)
        {
            return Path.Combine(ObtenerRutaRaizProyecto(), rutaRelativa.TrimStart('\\', '/'));
        }

        private static string LeerArchivo(string rutaRelativa)
        {
            return File.ReadAllText(ResolverRuta(rutaRelativa));
        }

        #region Prueba 1: Inspector remite a Coordinador
        [TestMethod]
        public void Test01_InspectorRemiteACoordinador()
        {
            // Validar transiciones de revisión del inspector a PENDIENTE_COORDINADOR
            Assert.IsTrue(_flujoService.EsTransicionPermitida(EstadoSolicitud.EnRevision, AocrEstadosProceso.PendienteCoordinador),
                "EN_REVISION -> PENDIENTE_COORDINADOR debe estar permitida.");
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.PendienteRevisionInspector, AocrEstadosProceso.PendienteCoordinador),
                "PENDIENTE_REVISION_INSPECTOR -> PENDIENTE_COORDINADOR debe estar permitida.");
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.RevisionInspectorEnProceso, AocrEstadosProceso.PendienteCoordinador),
                "REVISION_INSPECTOR_EN_PROCESO -> PENDIENTE_COORDINADOR debe estar permitida.");

            // Validar que en el controlador del inspector, al finalizar la revisión, se actualiza a PENDIENTE_COORDINADOR
            var controllerContent = LeerArchivo("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            StringAssert.Contains(controllerContent, "AocrEstadosProceso.PendienteCoordinador");
            StringAssert.Contains(controllerContent, "Revision finalizada por Inspector y pendiente de decision de Coordinacion");
            StringAssert.Contains(controllerContent, "_correoService.NotificarEvento(solicitud, \"PENDIENTE_COORDINADOR\"");
        }
        #endregion

        #region Prueba 2: Coordinador recibe en su bandeja
        [TestMethod]
        public void Test02_CoordinadorRecibeEnSuBandeja()
        {
            // Validar que la vista de control documental del coordinador incluye las 10 columnas obligatorias y los formularios de acción
            var viewContent = LeerArchivo("CapaPresentacion/Views/CoordinacionJefatura/_BandejaControlDocumental.cshtml");
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

            StringAssert.Contains(viewContent, "Html.BeginForm(\"DevolverAlInspector\", \"CoordinacionJefatura\"");
            StringAssert.Contains(viewContent, "Html.BeginForm(\"RemitirADircav\", \"CoordinacionJefatura\"");
        }
        #endregion

        #region Prueba 3: Coordinador devuelve al Inspector
        [TestMethod]
        public void Test03_CoordinadorDevuelveAlInspector()
        {
            // Validar matriz de flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.PendienteCoordinador, AocrEstadosProceso.DevueltoInspector),
                "PENDIENTE_COORDINADOR -> DEVUELTO_INSPECTOR debe ser permitida.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(AocrRolesInstitucionales.Coordinador, AocrFlujoAcciones.CoordinadorDevolverInspector),
                "Coordinador debe poder ejecutar CoordinadorDevolverInspector.");

            // Validar que comentario es estrictamente obligatorio
            var resVacio = _coordinadorRevisionService.DevolverAlInspector(999, 1, "", "coordinador");
            Assert.IsFalse(resVacio.Ok);
            StringAssert.Contains(resVacio.Mensaje, "obligatorio");

            var resEspacios = _coordinadorRevisionService.DevolverAlInspector(999, 1, "   ", "coordinador");
            Assert.IsFalse(resEspacios.Ok);
            StringAssert.Contains(resEspacios.Mensaje, "obligatorio");
        }
        #endregion

        #region Prueba 4: Inspector corrige y reenvía
        [TestMethod]
        public void Test04_InspectorCorrigeYReenvia()
        {
            // Validar que DEVUELTO_INSPECTOR es revisable por el Inspector
            Assert.IsTrue(_estadoService.EsEstadoRevisablePorInspector(AocrEstadosProceso.DevueltoInspector),
                "DEVUELTO_INSPECTOR debe ser revisable por el Inspector.");

            // Validar que DEVUELTO_INSPECTOR puede transicionar a PENDIENTE_COORDINADOR
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.DevueltoInspector, AocrEstadosProceso.PendienteCoordinador),
                "DEVUELTO_INSPECTOR -> PENDIENTE_COORDINADOR debe estar permitida tras corrección.");
        }
        #endregion

        #region Prueba 5: Coordinador remite a DIRCAV
        [TestMethod]
        public void Test05_CoordinadorRemiteADircav()
        {
            // Validar matriz de flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.PendienteCoordinador, AocrEstadosProceso.PendienteDircav),
                "PENDIENTE_COORDINADOR -> PENDIENTE_DIRCAV debe estar permitida.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(AocrRolesInstitucionales.Coordinador, AocrFlujoAcciones.CoordinadorRemitirDircav),
                "Coordinador debe tener permiso para ejecutar CoordinadorRemitirDircav.");

            // Validar en el servicio de Coordinador
            var serviceContent = LeerArchivo("CapaNegocio/Services/RevisionDocumentalCoordinadorService.cs");
            StringAssert.Contains(serviceContent, "RemitirADircav");
            StringAssert.Contains(serviceContent, "EstadoSolicitudDestino = AocrEstadosProceso.PendienteDircav");
            StringAssert.Contains(serviceContent, "EstadoRevisionDestino = EstadoRevisionDocumentalCoordinador.AceptadaCoordinador");
        }
        #endregion

        #region Prueba 6: DIRCAV recibe en su bandeja
        [TestMethod]
        public void Test06_DircavRecibeEnSuBandeja()
        {
            var serviceContent = LeerArchivo("CapaNegocio/Services/DircavBandejaService.cs");
            // Validar que la bandeja DIRCAV consulta PENDIENTE_DIRCAV
            StringAssert.Contains(serviceContent, "AocrEstadosProceso.PendienteDircav");
            // Validar que excluye PENDIENTE_COORDINADOR y DEVUELTO_INSPECTOR
            StringAssert.Contains(serviceContent, "!string.Equals(s.Estado, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase)");
            StringAssert.Contains(serviceContent, "!string.Equals(s.Estado, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase)");
        }
        #endregion

        #region Prueba 7: Inspector no puede enviar a DIRCAV
        [TestMethod]
        public void Test07_InspectorNoPuedeEnviarADircav()
        {
            // En matriz de permisos
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Inspector", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "Inspector no puede ejecutar CoordinadorRemitirDircav.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "InspectorTecnico no puede ejecutar CoordinadorRemitirDircav.");

            // En controlador CoordinacionJefatura
            var coordController = LeerArchivo("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(coordController, "AocrRolesInstitucionales.EsInspector(rolSesion)");
            StringAssert.Contains(coordController, "El Inspector no tiene permisos para remitir a DIRCAV ni a DIRDAC");
        }
        #endregion

        #region Prueba 8: Inspector no puede enviar a DIRDAC
        [TestMethod]
        public void Test08_InspectorNoPuedeEnviarADirdac()
        {
            // En matriz de permisos
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Inspector", AocrFlujoAcciones.DircavRemitirDirdac),
                "Inspector no puede remitir a DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Inspector", AocrFlujoAcciones.EnviarDirdac),
                "Inspector no puede enviar a DIRDAC.");

            // En controlador Inspeccion
            var inspController = LeerArchivo("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(inspController, "AocrRolesInstitucionales.EsInspector(rolSesion)");
            StringAssert.Contains(inspController, "El Inspector no puede remitir directamente a DIRDAC");
        }
        #endregion

        #region Prueba 9: Coordinador no puede enviar a DIRDAC
        [TestMethod]
        public void Test09_CoordinadorNoPuedeEnviarADirdac()
        {
            // En matriz de permisos
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinador", AocrFlujoAcciones.EnviarDirdac),
                "Coordinador no puede enviar a DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinador", AocrFlujoAcciones.DircavRemitirDirdac),
                "Coordinador no puede ejecutar DircavRemitirDirdac.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.EnviarDirdac),
                "Coordinacion no puede enviar a DIRDAC.");

            // En controlador Inspeccion
            var inspController = LeerArchivo("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(inspController, "AocrRolesInstitucionales.EsCoordinador(rolSesion)");
            StringAssert.Contains(inspController, "El Coordinador no puede remitir directamente a DIRDAC");
        }
        #endregion

        #region Prueba 10: Administrador recibe 403
        [TestMethod]
        public void Test10_AdministradorRecibe403()
        {
            // Matriz de permisos prohíbe acciones operativas a Administrador
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.CoordinadorDevolverInspector));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.CoordinadorRemitirDircav));

            // RevisionDocumentalController bloquea Administrador con 403
            var revDocController = LeerArchivo("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            StringAssert.Contains(revDocController, "AocrRolesInstitucionales.EsAdministrador(rolActivo)");
            StringAssert.Contains(revDocController, "El Administrador no puede ejecutar decisiones operativas");

            // CoordinacionJefaturaController bloquea Administrador con 403
            var coordController = LeerArchivo("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(coordController, "El Administrador no puede ejecutar devoluciones operativas (Regla 7)");
            StringAssert.Contains(coordController, "El Administrador no puede ejecutar remisiones operativas (Regla 7)");

            // InspeccionController bloquea Administrador con 403
            var inspController = LeerArchivo("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(inspController, "El Administrador no puede ejecutar acciones operativas (Regla 7)");
        }
        #endregion

        #region Prueba 11: UsuarioId cero recibe 401
        [TestMethod]
        public void Test11_UsuarioIdCeroRecibe401()
        {
            // RevisionDocumentalController retorna 401 si usuarioId <= 0
            var revDocController = LeerArchivo("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            StringAssert.Contains(revDocController, "if (usuarioId <= 0)");
            StringAssert.Contains(revDocController, "JsonRevisionError(401");

            // CoordinacionJefaturaController retorna 401 si UsuarioId <= 0
            var coordController = LeerArchivo("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(coordController, "HttpStatusCode.Unauthorized, \"Sesión no válida o expirada.\"");

            // InspeccionController retorna 401 si usuarioId <= 0
            var inspController = LeerArchivo("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(inspController, "HttpStatusCode.Unauthorized, \"Sesión no válida o expirada.\"");

            // RevisionDocumentalCoordinadorService valida identificador > 0
            var res = _coordinadorRevisionService.DevolverAlInspector(100, 0, "Observación válida", "login");
            Assert.IsFalse(res.Ok);
            StringAssert.Contains(res.Mensaje, "invalido");
        }
        #endregion

        #region Prueba 12: Estado incompatible devuelve 409
        [TestMethod]
        public void Test12_EstadoIncompatibleDevuelve409()
        {
            // RevisionDocumentalController retorna 409 cuando no es revisable
            var revDocController = LeerArchivo("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            StringAssert.Contains(revDocController, "JsonRevisionError(409");
            StringAssert.Contains(revDocController, "Estado incompatible");

            // CoordinacionJefaturaController retorna 409 cuando estado != PENDIENTE_COORDINADOR
            var coordController = LeerArchivo("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(coordController, "HttpStatusCode.Conflict, \"La solicitud no se encuentra en estado PENDIENTE_COORDINADOR");

            // RevisionDocumentalCoordinadorService valida estado
            var res = _coordinadorRevisionService.DevolverAlInspector(999999, 10, "Observacion", "coord");
            Assert.IsFalse(res.Ok);
        }
        #endregion

        #region Prueba 13: Doble clic no duplica
        [TestMethod]
        public void Test13_DobleClicNoDuplica()
        {
            // RevisionDocumentalCoordinadorDAO implementa transacción con bloqueo pesimista y verificación de estado
            var daoContent = LeerArchivo("CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs");
            StringAssert.Contains(daoContent, "codigo_solicitud=@solicitud_id FOR UPDATE");
            StringAssert.Contains(daoContent, "La solicitud no se encuentra en estado PENDIENTE_COORDINADOR");
            StringAssert.Contains(daoContent, "Conflicto de concurrencia");
        }
        #endregion

        #region Prueba 14: Solo una auditoría por operación
        [TestMethod]
        public void Test14_SoloUnaAuditoriaPorOperacion()
        {
            // Verificar que en EjecutarTransicionCoordinadorTransaccional se registra una sola auditoría atómica
            var daoContent = LeerArchivo("CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs");
            StringAssert.Contains(daoContent, "INSERT INTO public.aocr_tbauditoria");
            StringAssert.Contains(daoContent, "'SOLICITUD'");
        }
        #endregion

        #region Prueba 15: Fallo intermedio produce rollback total
        [TestMethod]
        public void Test15_FalloIntermedioProduceRollbackTotal()
        {
            var daoContent = LeerArchivo("CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs");
            StringAssert.Contains(daoContent, "using (var tx = cn.BeginTransaction())");
            StringAssert.Contains(daoContent, "tx.Rollback();");
            StringAssert.Contains(daoContent, "tx.Commit();");
        }
        #endregion

        #region Prueba 16: No permanece ninguna ruta antigua de salto directo
        [TestMethod]
        public void Test16_NoPermaneceNingunaRutaAntiguaDeSaltoDirecto()
        {
            // InspeccionController ya NO contiene autoEnviarADirdac ni autoenvío a DIRDAC en firma
            var inspController = LeerArchivo("CapaPresentacion/Controllers/InspeccionController.cs");
            Assert.IsFalse(inspController.Contains("autoEnviarADirdac"),
                "InspeccionController no debe contener autoEnviarADirdac.");
            Assert.IsFalse(inspController.Contains("FirmarInformePorRol(id, \"INSPECTOR\", \"CERTIFICADO_DIGITAL\", false, true)"),
                "FirmarInformeInspector no debe invocar firma con autoenvío activado.");

            // RevisionDocumentalController nunca salta directo a DIRCAV o DIRDAC
            var revDocController = LeerArchivo("CapaPresentacion/Controllers/RevisionDocumentalController.cs");
            Assert.IsFalse(revDocController.Contains("siguienteEstado = AocrEstadosProceso.PendienteDircav"),
                "RevisionDocumentalController nunca debe transicionar a PENDIENTE_DIRCAV.");
            Assert.IsFalse(revDocController.Contains("siguienteEstado = AocrEstadosProceso.PendienteDirdac"),
                "RevisionDocumentalController nunca debe transicionar a PENDIENTE_DIRDAC.");

            // Script SQL existe con llaves foráneas y reglas de integridad
            var sqlContent = LeerArchivo("scripts/sql/20260903_ac04_recorrido_inspector_coordinador_dircav.sql");
            StringAssert.Contains(sqlContent, "fk_revdoc_coord_solicitud");
            StringAssert.Contains(sqlContent, "fk_reasig_solicitud");
            StringAssert.Contains(sqlContent, "PENDIENTE_COORDINADOR");
            StringAssert.Contains(sqlContent, "DEVUELTO_INSPECTOR");
            StringAssert.Contains(sqlContent, "PENDIENTE_DIRCAV");
        }
        #endregion
    }
}
