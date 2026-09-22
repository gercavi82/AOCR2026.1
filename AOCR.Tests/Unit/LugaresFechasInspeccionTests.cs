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
        private static SolicitudEstacionInspeccion Lugar(string codigo, string fecha)
        {
            return new SolicitudEstacionInspeccionItemVM { EstacionCodigo = codigo, FechaInspeccion = fecha, Version = 3 }.ToEntity(150, 95);
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
        public void RangoHistorico_SeConservaParaLecturaPeroRequiereFechaUnicaAlEditar()
        {
            var historico = new SolicitudEstacionInspeccionItemVM { EstacionCodigo = "UIO", FechaInicio = "2026-09-22", FechaFin = "2026-09-25" }.ToEntity(150, 95);
            Assert.AreEqual("22/09/2026 al 25/09/2026", historico.RangoFechasTexto);
            Assert.IsNotNull(SolicitudEstacionService.ValidarFechasPorLugar(new[] { historico }, "QUITO", null));
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
