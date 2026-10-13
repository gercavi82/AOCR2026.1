using System;
using System.Collections.Generic;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class LugaresFechasInspeccionTests
    {
        [TestMethod]
        public void SolicitudAocr_RechazaEdicionDirectaDeLugaresYFechas()
        {
            var controller = (CapaPresentacion.Controllers.SolicitudAOCRController)
                System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CapaPresentacion.Controllers.SolicitudAOCRController));
            var context = new System.Web.HttpContext(
                new System.Web.HttpRequest("", "http://localhost/", ""),
                new System.Web.HttpResponse(new System.IO.StringWriter()));
            controller.ControllerContext = new System.Web.Mvc.ControllerContext(
                new System.Web.HttpContextWrapper(context), new System.Web.Routing.RouteData(), controller);
            var result = controller.GuardarEstaciones(new CapaPresentacion.Controllers.SolicitudAOCRController.GuardarEstacionesRequest
            {
                CodigoSolicitud = 150,
                Estaciones = new List<SolicitudEstacionInspeccionItemVM>
                {
                    new SolicitudEstacionInspeccionItemVM { EstacionCodigo = "GYE", FechaInicio = "2026-09-22", FechaFin = "2026-09-25" }
                }
            });
            Assert.AreEqual(403, context.Response.StatusCode);
            Assert.AreEqual(false, result.Data.GetType().GetProperty("success").GetValue(result.Data));
        }

        private static SolicitudEstacionInspeccion Lugar(string codigo, string fecha)
        {
            return new SolicitudEstacionInspeccionItemVM { EstacionCodigo = codigo, FechaInspeccion = fecha, Version = 3 }.ToEntity(150, 95);
        }

        [TestMethod]
        public void Rt_SolicitaLugaresSinFechas_InspectorDebePlanificarlos()
        {
            var lugares = new[] { Lugar("UIO", ""), Lugar("GYE", "") };
            Assert.IsNull(SolicitudEstacionService.ValidarFechasPorLugar(lugares, "QUITO,GUAYAQUIL", null, false));
            Assert.AreEqual(default(DateTime), lugares[0].FechaInicio);
            Assert.IsNotNull(SolicitudEstacionService.ValidarPlanificacionAcordada(lugares));
            lugares[0].FechaInicio = lugares[0].FechaFin = new DateTime(2026, 10, 20);
            Assert.IsNotNull(SolicitudEstacionService.ValidarPlanificacionAcordada(lugares));
            lugares[1].FechaInicio = lugares[1].FechaFin = new DateTime(2026, 10, 23);
            Assert.IsNull(SolicitudEstacionService.ValidarPlanificacionAcordada(lugares));
            lugares[1].FechaFin = new DateTime(2026, 10, 22);
            Assert.IsNotNull(SolicitudEstacionService.ValidarPlanificacionAcordada(lugares));
        }

        [TestMethod]
        public void Rt_SinFechas_NoPuedeOmitirDuplicarOAgregarLugares()
        {
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("UIO", "") }, "QUITO,GUAYAQUIL", null, false));
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("UIO", ""), Lugar("UIO", "") }, "QUITO,GUAYAQUIL", null, false));
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("GYE", "") }, "QUITO", null, false));
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("OTROS", "") }, "OTRA_PROVINCIA", "", false));
            Assert.IsNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("OTROS", "") }, "OTRA_PROVINCIA", "Portoviejo", false));
        }

        [DataTestMethod]
        [DataRow(1, "emisión")]
        [DataRow(2, "renovación")]
        [DataRow(3, "modificación")]
        public void CorreoPago_IdentificaTipoSolicitud(int tipo, string esperado)
        {
            Assert.AreEqual(esperado, AocrPostPagoWorkflowService.TextoTipoSolicitud(tipo));
        }

        [TestMethod]
        public void CorreccionFinanciera_RecalculaTotalesSinConfiarEnValoresEnviados()
        {
            var conceptos = new[] { new CapaDatos.Models.ConceptoModel { Id = 7, Codigo = "INSPECCION_EXT", Nombre = "Inspección", Activo = true } };
            var detalle = new CapaDatos.Entidades.DetalleOrden { ConceptoId = 7, Cantidad = 3, ValorUnitario = 10.55m, PorcentajeAdmin = 10m, TotalLinea = 999m, ConceptoCodigo = "FALSO" };
            Assert.IsNull(OrdenRecaudacionService.PrepararCorreccionFinanciera(new[] { detalle }, conceptos, "Faltaban conceptos"));
            Assert.AreEqual(31.65m, detalle.Subtotal);
            Assert.AreEqual(3.17m, detalle.Admin);
            Assert.AreEqual(34.82m, detalle.TotalLinea);
            Assert.AreEqual("INSPECCION_EXT", detalle.ConceptoCodigo);
            detalle.Cantidad = -1;
            Assert.IsNotNull(OrdenRecaudacionService.PrepararCorreccionFinanciera(new[] { detalle }, conceptos, "Motivo"));
            detalle.Cantidad = 1;
            detalle.ConceptoId = 99;
            Assert.IsNotNull(OrdenRecaudacionService.PrepararCorreccionFinanciera(new[] { detalle }, conceptos, "Motivo"));
        }

        [TestMethod]
        public void FechasIndependientes_SeMapeanAlModeloPersistidoSinRangoGeneral()
        {
            var quito = Lugar("UIO", "2026-09-22");
            var gye = Lugar("GYE", "2026-09-26");
            Assert.IsNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { quito, gye }, "QUITO,GUAYAQUIL", null));
            Assert.AreEqual(new DateTime(2026, 9, 22), quito.FechaInicio);
            Assert.AreEqual(new DateTime(2026, 9, 26), gye.FechaInicio);
            Assert.AreEqual(quito.FechaInicio, quito.FechaFin);
            Assert.AreEqual(gye.FechaInicio, gye.FechaFin);
            Assert.AreEqual(3, quito.Version);
            Assert.AreEqual("22/09/2026", quito.RangoFechasTexto);
        }

        [TestMethod]
        public void LugarSeleccionadoSinFecha_SeRechaza()
        {
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("UIO", "") }, "QUITO", null));
        }

        [TestMethod]
        public void FechaInvalida_SeRechazaSinConvertirlaEnFechaGeneral()
        {
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("UIO", "2026-02-30") }, "QUITO", null));
        }

        [TestMethod]
        public void LugarDesmarcado_NoPuedeMantenerUnaFechaHuerfana()
        {
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { Lugar("UIO", "2026-09-22"), Lugar("GYE", "2026-09-26") }, "QUITO", null));
        }

        [TestMethod]
        public void OtraLocalidad_RequiereNombreYNoSeDuplica()
        {
            var lugar = Lugar("OTROS", "2026-09-28");
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { lugar }, "OTRA_PROVINCIA", ""));
            Assert.IsNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { lugar }, "OTRA_PROVINCIA", "Portoviejo"));
            Assert.AreEqual("Portoviejo", lugar.EstacionNombre);
            Assert.AreEqual("OTROS", lugar.EstacionCodigo);
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { lugar, lugar }, "OTRA_PROVINCIA", "Portoviejo"));
        }

        [TestMethod]
        public void RangoPorLugar_SeConservaParaLecturaYEdicion()
        {
            var historico = new SolicitudEstacionInspeccionItemVM { EstacionCodigo = "UIO", FechaInicio = "2026-09-22", FechaFin = "2026-09-25" }.ToEntity(150, 95);
            Assert.AreEqual("22/09/2026 al 25/09/2026", historico.RangoFechasTexto);
            Assert.IsNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { historico }, "QUITO", null));
        }

        [TestMethod]
        public void RangoIncompletoOInvertido_SeRechaza()
        {
            foreach (var fin in new[] { "", "2026-09-21" })
            {
                var lugar = new SolicitudEstacionInspeccionItemVM { EstacionCodigo = "GYE", FechaInicio = "2026-09-22", FechaFin = fin }.ToEntity(150, 95);
                Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { lugar }, "GUAYAQUIL", null));
            }
        }

        [TestMethod]
        public void VersionYRelacionSolicitud_SeConservanAlEditar()
        {
            var lugar = new SolicitudEstacionInspeccionItemVM { Id = 7, Version = 4, EstacionCodigo = "UIO", FechaInspeccion = "2026-09-22" }.ToEntity(150, 95);
            Assert.AreEqual(7, lugar.Id);
            Assert.AreEqual(4, lugar.Version);
            Assert.AreEqual(150, lugar.SolicitudId);
        }
    }
}
