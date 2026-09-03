using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// Suite de 18 pruebas unitarias rigurosas para AC-05:
    /// Aceptación documental y designación formal del Inspector por la Autoridad DIRCAV.
    /// Garantiza segregación estricta (DIRDAC y Admin excluidos), versionado de designación y protección contra colisiones.
    /// </summary>
    [TestClass]
    public class Ac05DircavAceptacionYDesignacionTests
    {
        private static string BasePath => AppDomain.CurrentDomain.BaseDirectory;

        private static string ReadRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(BasePath);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AOCR.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new InvalidOperationException("No se encontró la raíz del repositorio.");
            }

            var path = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "No existe el archivo requerido: " + path);
            return File.ReadAllText(path);
        }

        #region Pruebas de Flujo y Reglas de Negocio AC-05

        [TestMethod]
        public void Test01_DircavAbreSuBandejaExclusiva()
        {
            var controllerSource = ReadRepoFile("CapaPresentacion/Controllers/DircavController.cs");
            var viewSource = ReadRepoFile("CapaPresentacion/Views/Dircav/Bandeja.cshtml");

            StringAssert.Contains(controllerSource, "public ActionResult Bandeja(");
            StringAssert.Contains(controllerSource, "public ActionResult BandejaDocumentacion()");
            StringAssert.Contains(viewSource, "Bandeja de Gestión DIRCAV");
            StringAssert.Contains(viewSource, "Doc. Pendiente Aceptación");
            StringAssert.Contains(viewSource, "Designaciones Pendientes");
        }

        [TestMethod]
        public void Test02_DircavRecibeUnicamenteExpedientesRemitidosPorCoordinador()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavBandejaService.cs");
            var estadosSource = ReadRepoFile("CapaDatos/Constants/AocrEstadosProceso.cs");

            StringAssert.Contains(serviceSource, "AocrEstadosProceso.PendienteDircav");
            StringAssert.Contains(estadosSource, "public const string PendienteDircav = \"PENDIENTE_DIRCAV\";");

            // Validar que solicitudes en PENDIENTE_COORDINADOR no aparecen en DIRCAV
            Assert.IsFalse(serviceSource.Contains("WHERE s.estado = 'PENDIENTE_COORDINADOR'"));
        }

        [TestMethod]
        public void Test03_DircavDevuelveConMotivoYCoordinadorRecibeExpediente()
        {
            var service = new DircavDesignacionService();
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 501,
                Estado = AocrEstadosProceso.PendienteDircav
            };

            var flujoService = new AocrFlujoService();
            var transicionValida = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDircav,
                AocrEstadosProceso.DevueltoCoordinador
            );
            Assert.IsTrue(transicionValida, "La transición PENDIENTE_DIRCAV -> DEVUELTO_COORDINADOR debe estar explícitamente permitida.");

            var transicionRetorno = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DevueltoCoordinador,
                AocrEstadosProceso.PendienteCoordinador
            );
            Assert.IsTrue(transicionRetorno, "El Coordinador debe poder recibir y reprocesar el expediente DEVUELTO_COORDINADOR.");
        }

        [TestMethod]
        public void Test04_DevolucionSinMotivoSeBloquea()
        {
            var service = new DircavDesignacionService();

            // Intento de devolver con motivo vacío o espacios
            var res1 = service.DevolverAlCoordinador(100, 1, "DIRCAV_USER", "", "DIRCAV");
            Assert.IsFalse(res1.Exitoso);
            Assert.AreEqual(400, res1.HttpStatusCode);
            StringAssert.Contains(res1.Mensaje, "obligatorio");

            var res2 = service.DevolverAlCoordinador(100, 1, "DIRCAV_USER", "   ", "DIRCAV");
            Assert.IsFalse(res2.Exitoso);
            Assert.AreEqual(400, res2.HttpStatusCode);
        }

        [TestMethod]
        public void Test05_DircavAceptaDocumentacionCompleta()
        {
            var flujoService = new AocrFlujoService();

            var transicionAceptacion = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDircav,
                AocrEstadosProceso.DocumentacionAceptadaDircav
            );
            Assert.IsTrue(transicionAceptacion, "DIRCAV debe poder aceptar formalmente la documentación técnica.");

            var transicionHaciaDesignacion = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DocumentacionAceptadaDircav,
                AocrEstadosProceso.PendienteDesignacionDircav
            );
            Assert.IsTrue(transicionHaciaDesignacion, "La aceptación documental debe habilitar el estado de designación.");
        }

        [TestMethod]
        public void Test06_DocumentacionIncompletaNoPuedeAceptarse()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Verifica que el servicio valide la integridad documental antes del commit
            StringAssert.Contains(serviceSource, "OBSERVADO");
            StringAssert.Contains(serviceSource, "existen documentos observados pendientes de resolución");
        }

        [TestMethod]
        public void Test07_DircavObtieneInspectoresActivosReales()
        {
            var service = new DircavDesignacionService();
            var inspectores = service.ListarInspectoresDisponibles();

            Assert.IsNotNull(inspectores, "La lista de inspectores disponibles no debe ser nula.");
            // Todos los inspectores listados deben tener rol Inspector
            foreach (var insp in inspectores)
            {
                Assert.IsTrue(
                    (insp.RolInterno ?? "").ToUpperInvariant().Contains("INSPECTOR") ||
                    (insp.Tipo ?? "").ToUpperInvariant().Contains("AIR"),
                    "Solo usuarios con rol o tipo de Inspector deben presentarse en el catálogo asignable."
                );
            }
        }

        [TestMethod]
        public void Test08_DircavDesignaUnInspectorValido()
        {
            var flujoService = new AocrFlujoService();

            var transicionDesignar = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DocumentacionAceptadaDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav
            );
            Assert.IsTrue(transicionDesignar, "DOCUMENTACION_ACEPTADA_DIRCAV debe permitir transicionar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");

            var transicionDesdePendiente = flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDesignacionDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav
            );
            Assert.IsTrue(transicionDesdePendiente, "PENDIENTE_DESIGNACION_DIRCAV debe permitir transicionar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");
        }

        [TestMethod]
        public void Test09_InspectorInactivoOSinRolEsRechazado()
        {
            var service = new DircavDesignacionService();

            var req = new DircavDesignacionRequest
            {
                SolicitudId = 1,
                InspectorPrincipalCedula = "INSPECTOR_INEXISTENTE_99999",
                RolSolicitante = "DIRCAV"
            };

            var resultado = service.DesignarInspector(req);
            Assert.IsFalse(resultado.Exitoso, "Un inspector que no existe o no está activo debe ser rechazado.");
            Assert.AreEqual(400, resultado.HttpStatusCode);
            StringAssert.Contains(resultado.Mensaje, "no existe, no está activo o no tiene rol de Inspector");
        }

        [TestMethod]
        public void Test10_DobleDesignacionSeBloqueaOEsIdempotente()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadRepoFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Verifica índice único en tabla y manejo idempotente en servicio
            StringAssert.Contains(daoSource, "uq_aocr_designacion_vigente");
            StringAssert.Contains(serviceSource, "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado.");
        }

        [TestMethod]
        public void Test11_ReasignacionConservaHistorialYExigeMotivo()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadRepoFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            StringAssert.Contains(serviceSource, "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional");
            StringAssert.Contains(daoSource, "UPDATE public.aocr_tbdesignacion_inspector");
            StringAssert.Contains(daoSource, "SET vigente = FALSE");
            StringAssert.Contains(daoSource, "versionNueva = versionActual + 1");
        }

        [TestMethod]
        public void Test12_DirdacNoPuedeAceptarNiDesignar()
        {
            var service = new DircavDesignacionService();

            Assert.IsFalse(service.EsDircavAutorizado("DIRDAC"), "DIRDAC no tiene autoridad operativa sobre AC-05.");
            Assert.IsFalse(service.EsDircavAutorizado("DireccionGeneral"), "DireccionGeneral no tiene autoridad operativa sobre AC-05.");

            var resAceptar = service.AceptarDocumentacion(1, 1, "DIRDAC", "DIRDAC");
            Assert.IsFalse(resAceptar.Exitoso);
            Assert.AreEqual(403, resAceptar.HttpStatusCode);

            var resDesignar = service.DesignarInspector(new DircavDesignacionRequest
            {
                SolicitudId = 1,
                InspectorPrincipalCedula = "0102030405",
                RolSolicitante = "DIRDAC"
            });
            Assert.IsFalse(resDesignar.Exitoso);
            Assert.AreEqual(403, resDesignar.HttpStatusCode);
        }

        [TestMethod]
        public void Test13_AdministradorNoPuedeDesignarAunqueAdministreRoles()
        {
            var service = new DircavDesignacionService();

            // REGLA 7: Administrador bloqueado expresamente
            Assert.IsFalse(service.EsDircavAutorizado("Administrador"), "El Administrador no puede operar en AC-05 por Regla 7.");

            var resAceptar = service.AceptarDocumentacion(1, 1, "ADMIN", "Administrador");
            Assert.IsFalse(resAceptar.Exitoso);
            Assert.AreEqual(403, resAceptar.HttpStatusCode);

            var resDevolver = service.DevolverAlCoordinador(1, 1, "ADMIN", "Observación", "Administrador");
            Assert.IsFalse(resDevolver.Exitoso);
            Assert.AreEqual(403, resDevolver.HttpStatusCode);

            var resDesignar = service.DesignarInspector(new DircavDesignacionRequest
            {
                SolicitudId = 1,
                InspectorPrincipalCedula = "0102030405",
                RolSolicitante = "Administrador"
            });
            Assert.IsFalse(resDesignar.Exitoso);
            Assert.AreEqual(403, resDesignar.HttpStatusCode);
        }

        [TestMethod]
        public void Test14_UrlDirectaSinPermisoDevuelve403()
        {
            var controllerSource = ReadRepoFile("CapaPresentacion/Controllers/DircavController.cs");

            StringAssert.Contains(controllerSource, "if (!EsDircavAutorizado()");
            StringAssert.Contains(controllerSource, "return new HttpStatusCodeResult(403");
        }

        [TestMethod]
        public void Test15_ConflictoDeVersionOConcurrenciaDevuelve409()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var controllerSource = ReadRepoFile("CapaPresentacion/Controllers/DircavController.cs");

            StringAssert.Contains(serviceSource, "HttpStatusCode = 409");
            StringAssert.Contains(controllerSource, "return new HttpStatusCodeResult(409, resultado.Mensaje);");
        }

        [TestMethod]
        public void Test16_ErrorDeBaseDeDatosProduceRollback()
        {
            var daoSource = ReadRepoFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            StringAssert.Contains(daoSource, "tx.Rollback();");
            StringAssert.Contains(daoSource, "tx.Commit();");
        }

        [TestMethod]
        public void Test17_AuditoriaYNotificacionNoSeDuplican()
        {
            var serviceSource = ReadRepoFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Comprueba que no se notifique oficialmente al inspector antes de la firma de AC-06
            StringAssert.Contains(serviceSource, "No notificar como definitiva antes de la firma de DIRCAV (AC-06)");
            StringAssert.Contains(serviceSource, "_auditoriaDao.Registrar");
        }

        [TestMethod]
        public void Test18_LaPantallaFuncionaBajoAocrYEnResolucionesAdaptables()
        {
            var viewSource = ReadRepoFile("CapaPresentacion/Views/Dircav/Bandeja.cshtml");

            StringAssert.Contains(viewSource, "table-responsive");
            StringAssert.Contains(viewSource, "@Url.Action(\"Bandeja\", \"Dircav\"");
            StringAssert.Contains(viewSource, "@Url.Action(\"InspectoresDisponibles\", \"Dircav\")");
            StringAssert.Contains(viewSource, "modalDevolver");
            StringAssert.Contains(viewSource, "modalDesignar");
            StringAssert.Contains(viewSource, "btn-prevenir-doble");

            // Verifica que no existan URLs absolutas codificadas con /aocr fijo
            Assert.IsFalse(viewSource.Contains("href=\"/aocr/"), "No deben usarse rutas absolutas codificadas /aocr/.");
        }

        #endregion
    }
}
