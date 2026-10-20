using System;
using System.Collections.Generic;
using System.Reflection;
using CapaModelo;
using CapaDatos.Entidades;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AOCR.Tests.Unit
{
    [TestClass]
    public class CambiosAocrOctubreTests
    {
        private static object Invoke(Type type, string method, params object[] args)
        {
            return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        }
        [TestMethod]
        public void OficioDcav_IncluyeAceptacionTransporteYFechasPorEstacion()
        {
            var vm = new CapaModelo.DTOs.DesignacionPdfViewModel
            {
                SolicitudId = 7, NumeroSolicitud = "DGAC-GOP-2026-AOCR003", NumeroDesignacion = "DCAV-2026-0007",
                Compania = "QATAR AIRWAYS Q.C.S.C.", NombreOperador = "QATAR AIRWAYS Q.C.S.C.", PaisOperador = "PAIS DE PRUEBA",
                InspectorPrincipalCargo = "Inspector de Operaciones",
                InspectorPrincipalNombre = "CATOTA TORRES LUIS WILKIR", InspectorPrincipalCedula = "1709565459",
                AutoridadDircavNombre = "Autoridad DCAV", AutoridadDircavCargo = "Director DCAV",
                FechaEmision = new DateTime(2026, 10, 16), Estado = "PENDIENTE_FIRMA"
            };
            vm.Estaciones.Add(new CapaModelo.DTOs.DesignacionEstacionItemDto
            {
                CodigoOaci = "SEQM", NombreCiudad = "Quito", FechaInicio = new DateTime(2026, 10, 21), FechaFin = new DateTime(2026, 10, 21)
            });
            var bytes = new DesignacionDocumentoService().GenerarPdfOficial(vm, true);
            using (var reader = new iTextSharp.text.pdf.PdfReader(bytes))
            {
                var text = new System.Text.StringBuilder();
                for (int page = 1; page <= reader.NumberOfPages; page++)
                    text.Append(iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, page));
                StringAssert.Contains(text.ToString(), "ACEPTACION DE DOCUMENTACION");
                StringAssert.Contains(text.ToString(), "traslados aéreos y terrestres");
                StringAssert.Contains(text.ToString(), "PAIS DE PRUEBA");
                Assert.IsFalse(text.ToString().Contains("EQUIPO INSPECTOR"));
                Assert.IsFalse(text.ToString().Contains("Aeronavegabilidad"));
                Assert.IsTrue(text.ToString().IndexOf("Esta Dirección") < text.ToString().IndexOf("En cumplimiento"));
                Assert.IsTrue(text.ToString().IndexOf("En cumplimiento") < text.ToString().IndexOf("Los traslados"));
                StringAssert.Contains(text.ToString(), "21/10/2026");
            }
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".tmp", "pdfs"));
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "oficio-dcav-cambios.pdf"), bytes);
        }
        [TestMethod]
        public void Generacion_RechazaRolesSinInspectorYContextosVacios()
        {
            foreach (var rol in new[] { "Administrador", "DIRCAV", "DIRDAC", "Coordinador", "RT", "" })
                Assert.IsFalse((bool)Invoke(typeof(GeneracionAOCRService), "UsuarioPuedeGenerarAocr", 7, new[] { rol }));
            Assert.IsFalse((bool)Invoke(typeof(GeneracionAOCRService), "UsuarioPuedeGenerarAocr", 7, null));
            Assert.IsFalse((bool)Invoke(typeof(GeneracionAOCRService), "UsuarioPuedeGenerarAocr", 0, new[] { "Inspector" }));
            Assert.IsTrue((bool)Invoke(typeof(GeneracionAOCRService), "UsuarioPuedeGenerarAocr", 7, new[] { "Inspector" }));
        }
        [TestMethod]
        public void Comprobante_ConfirmacionRtTieneClavePorPagoYDestinatario()
        {
            var t = typeof(OrdenRecaudacionCorreoService);
            var orden = new OrdenRecaudacion { Id = 3 };
            var key = Invoke(t, "ConstruirEventKey", "COMPROBANTE_CARGADO_RT", orden, 7, "rt@example.test", "21");
            var otherPayment = Invoke(t, "ConstruirEventKey", "COMPROBANTE_CARGADO_RT", orden, 7, "rt@example.test", "22");
            var otherRecipient = Invoke(t, "ConstruirEventKey", "COMPROBANTE_CARGADO_RT", orden, 7, "otro@example.test", "21");
            Assert.AreNotEqual(key, otherPayment);
            Assert.AreNotEqual(key, otherRecipient);
        }
        [TestMethod]
        public void Asignacion_ConservaFechasYRetiraInstruccionInformal()
        {
            var actual = (string)Invoke(typeof(SolicitudAocrCorreoService), "LimpiarObservacionAsignacion", "Inspector: Luis. Fecha: 21/10/2026. Observacion: Estimado Luchito favor proceder");
            Assert.AreEqual("Inspector: Luis. Fecha: 21/10/2026.", actual);
            Assert.IsNull(Invoke(typeof(SolicitudAocrCorreoService), "LimpiarObservacionAsignacion", " "));
        }
        [TestMethod]
        public void RevisionCompletada_PlantillaNoAnunciaInspeccionHabilitada()
        {
            var solicitud = new SolicitudAOCR { CodigoSolicitud = 7 };
            var t = typeof(SolicitudAocrCorreoService);
            var method = t.GetMethod("ConstruirPlantilla", BindingFlags.NonPublic | BindingFlags.Static);
            var model = method.Invoke(null, new object[] { solicitud, "REVISION_DOCUMENTAL_APROBADA" });
            Assert.AreEqual("Para revision de la Coordinacion", model.GetType().GetProperty("EstadoVisible").GetValue(model));
            StringAssert.Contains((string)model.GetType().GetProperty("Titulo").GetValue(model), "completada");
        }
    }
}
