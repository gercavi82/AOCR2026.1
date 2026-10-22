using System.Linq;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFlujoValidacionServiceTests
    {
        [TestMethod]
        public void Lv_CatalogoActualAdmiteLiteralesRetiradosSinPerderHistoria()
        {
            var catalogo = new CapaDatos.Catalogos.ListaVerificacionCatalogo();
            const string historico = "[{\"Codigo\":\"129-5-05\",\"EstadoImplementacion\":\"IMPLEMENTADO\",\"PruebasNotasComentarios\":\"Evidencia anterior\"},{\"Codigo\":\"129-5-04\",\"EstadoCumplimiento\":\"SATISFACTORIO\"}]";
            var items = catalogo.HidratarRespuestas(historico);
            Assert.AreEqual(4, items.Count(i => i.CodigoPregunta == "129-5"));
            Assert.AreEqual("SATISFACTORIO", items.Single(i => i.Codigo == "129-5-04").EstadoCumplimiento);
            var guardado = CapaDatos.Catalogos.ListaVerificacionCatalogo.ConservarRespuestasHistoricasRetiradas(
                CapaDatos.Catalogos.ListaVerificacionCatalogo.SerializarRespuestas(items), historico);
            StringAssert.Contains(guardado, "Evidencia anterior");
            Assert.AreEqual(items.Count, catalogo.HidratarRespuestas(guardado).Count);
            Assert.ThrowsException<System.InvalidOperationException>(() => catalogo.HidratarRespuestas(
                "[{\"Codigo\":\"129-5-99\",\"EstadoImplementacion\":\"IMPLEMENTADO\"}]"));
        }

        [TestMethod]
        public void PreparacionDocumentos_ExigeInformeFirmadoSatisfactorio()
        {
            var informe = new CapaModelo.InspeccionInformeTecnico { Finalizado=true, FirmadoInspector=true,
                RutaDocumentoFirmado="/prueba/firmado.pdf", Resultado="SATISFACTORIO" };
            Assert.IsTrue(AocrFlujoValidacionService.InformeListoParaPreparacion(informe));
            informe.FirmadoInspector=false;
            Assert.IsFalse(AocrFlujoValidacionService.InformeListoParaPreparacion(informe));
            informe.FirmadoInspector=true; informe.Resultado="INSATISFACTORIO";
            Assert.IsFalse(AocrFlujoValidacionService.InformeListoParaPreparacion(informe));
            informe.Resultado="SATISFACTORIO"; informe.EstadoInforme="INFORME_TECNICO_DEVUELTO_INSPECTOR";
            Assert.IsFalse(AocrFlujoValidacionService.InformeListoParaPreparacion(informe));
        }

        [TestMethod]
        public void Lv_IdentificadorPorEstacionYCatalogoCorregido()
        {
            var quito = new CapaModelo.ListaVerificacionOperacionalEae { CodigoInspeccion=2, EstacionId=36 };
            var gye = new CapaModelo.ListaVerificacionOperacionalEae { CodigoInspeccion=2, EstacionId=37 };
            Assert.AreNotEqual(quito.NumeroInspeccionEstacion, gye.NumeroInspeccionEstacion);
            var items = new CapaDatos.Catalogos.ListaVerificacionCatalogo().ObtenerCatalogoPreguntas();
            Assert.AreEqual(4, items.Count(i => i.CodigoPregunta=="129-5"));
            Assert.IsFalse(items.Any(i => i.CodigoPregunta=="129-5" && i.Codigo.EndsWith("-05")));
        }

        private static AocrFlujoValidacionService CrearServicio()
        {
            return new AocrFlujoValidacionService();
        }

        [TestMethod]
        public void PuedeGenerarInformeTecnico_InspeccionInvalida_DebeBloquear()
        {
            string motivo;
            Assert.IsFalse(CrearServicio().PuedeGenerarInformeTecnico(0, out motivo));
            StringAssert.Contains(motivo, "Inspección inválida");
        }

        [TestMethod]
        public void AocrEmailFlujoService_DebeExponerEventosInstitucionales()
        {
            var eventos = AocrEmailFlujoService.EventosFlujoInstitucionales;
            Assert.IsTrue(eventos.Count >= 20);
            Assert.IsTrue(eventos.Contains("INSPECTOR_ASIGNADO"));
            Assert.IsTrue(eventos.Contains("AOCR_FIRMADO"));
        }
    }
}
