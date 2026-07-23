using System;
using System.IO;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentalCoordinadorFlowTests
    {
        [TestMethod]
        public void Inspector_TieneObservacionGeneralYFinalizacionExplicita()
        {
            var view = Read("CapaPresentacion/Views/Documento/Lista.cshtml");
            var controller = Read("CapaPresentacion/Controllers/RevisionDocumentalController.cs");

            StringAssert.Contains(view, "id=\"ObservacionRevisionDocumental\"");
            StringAssert.Contains(view, "maxlength=\"2000\"");
            StringAssert.Contains(view, "id=\"btnFinalizarRevisionDocumental\"");
            StringAssert.Contains(view, "finalizar: window.__finalizarRevisionDocumentalActual === true");
            StringAssert.Contains(controller, "pendientes <= 0 && request.Finalizar");
            StringAssert.Contains(controller, "PENDIENTE_REVISION_COORDINADOR");
            StringAssert.Contains(controller, "GenerarYPersistirOficioRevisionDocumental");
        }

        [TestMethod]
        public void LvEInforme_ExigenAceptacionCoordinadorEInspectorConfirmado()
        {
            var service = Read("CapaNegocio/Services/RevisionDocumentalService.cs");
            var controller = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            var cierre = Read("CapaNegocio/SolicitudAocrInfraBL.cs");

            StringAssert.Contains(service, "RequiereAceptacionCoordinador");
            StringAssert.Contains(service, "EstaAceptadaParaInspector");
            StringAssert.Contains(controller, "_revisionDocumentalService.PuedeInspectorAbrirFaseOperativaLv(inspeccion, solicitud)");
            StringAssert.Contains(cierre, "resultado.HabilitaLv = false;");
            StringAssert.Contains(cierre, "Motivo=Requiere aceptaci");
        }

        [TestMethod]
        public void Coordinador_PuedeObservarMantenerOReasignarConAuditoria()
        {
            var controller = Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");
            var view = Read("CapaPresentacion/Views/SolicitudAOCR/Detalle.cshtml");
            var dao = Read("CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs");

            StringAssert.Contains(controller, "decisionCoordinador = \"ACEPTAR\"");
            StringAssert.Contains(controller, "_revisionDocumentalCoordinadorService.Observar");
            StringAssert.Contains(controller, "_revisionDocumentalCoordinadorService.Aceptar");
            StringAssert.Contains(view, "value=\"MANTENER\"");
            StringAssert.Contains(view, "value=\"REASIGNAR\"");
            StringAssert.Contains(view, "value=\"OBSERVAR\"");
            StringAssert.Contains(dao, "aocr_inspector_reasignacion_historial");
            StringAssert.Contains(dao, "estado_documental='ACEPTADA'");
        }

        [TestMethod]
        public void Oficio_EsInstitucionalPersistidoEIdempotente()
        {
            var pdf = Read("CapaPresentacion/Views/SolicitudAOCR/AceptacionDocumentalPdf.cshtml");
            var dao = Read("CapaDatos/DAOs/RevisionDocumentalCoordinadorDAO.cs");
            var migration = Read("scripts/sql/20260722_revision_documental_coordinador.sql");

            StringAssert.Contains(pdf, "Oficio Nro.");
            StringAssert.Contains(pdf, "Atentamente");
            StringAssert.Contains(pdf, "Anexos");
            StringAssert.Contains(pdf, "page-break anexos");
            StringAssert.Contains(dao, "ON CONFLICT (solicitud_id) DO UPDATE");
            StringAssert.Contains(migration, "solicitud_id INTEGER NOT NULL UNIQUE");
            StringAssert.Contains(migration, "fecha_habilitacion_lv");
            StringAssert.Contains(migration, "fecha_habilitacion_informe");
        }

        [TestMethod]
        public void Observacion_RechazaHtmlLongitudExcesivaYRecortaEspacios()
        {
            Assert.AreEqual("observacion valida", RevisionDocumentalCoordinadorService.NormalizarObservacion("  observacion valida  "));
            Assert.IsNull(RevisionDocumentalCoordinadorService.NormalizarObservacion("<script>alert(1)</script>"));
            Assert.IsNull(RevisionDocumentalCoordinadorService.NormalizarObservacion(new string('x', 2001)));
        }

        private static string Read(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var absolutePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(absolutePath), "No se encontro el archivo: " + absolutePath);
            return File.ReadAllText(absolutePath);
        }
    }
}
