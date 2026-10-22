using System.Linq;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFlujoValidacionServiceTests
    {
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
