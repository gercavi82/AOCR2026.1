using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrDcavRevisionServiceTests
    {
        [TestMethod]
        public void EsInformeSatisfactorio_AceptaResultadoSatisfactorio()
        {
            var informe = new InspeccionInformeTecnico { Resultado = "Satisfactorio" };

            Assert.IsTrue(AocrDcavRevisionService.EsInformeSatisfactorio(informe));
        }

        [TestMethod]
        public void EsInformeSatisfactorio_RechazaInsatisfactorio()
        {
            var informe = new InspeccionInformeTecnico { Resultado = "Insatisfactorio" };

            Assert.IsFalse(AocrDcavRevisionService.EsInformeSatisfactorio(informe));
        }

        [TestMethod]
        public void EstadosDcav_DeclaranNivelPrevioAFirmaDirectorGeneral()
        {
            Assert.AreEqual("PENDIENTE_REVISION_DCAV", AocrEstadosProceso.PendienteRevisionDcav);
            Assert.AreEqual("APROBADO_POR_DCAV", AocrEstadosProceso.AprobadoPorDcav);
            Assert.AreEqual("PENDIENTE_FIRMA_DIRDAC", AocrEstadosProceso.PendienteFirmaDirectorGeneral);
            Assert.AreEqual("PENDIENTE_FIRMA_DIRECTOR_GENERAL", AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy);
            Assert.AreEqual("FIRMADO_DIRECTOR_GENERAL", AocrEstadosProceso.FirmadoDirectorGeneral);
        }

        [TestMethod]
        public void EsInformeFirmadoValido_RechazaPdfGeneradoSinFirma()
        {
            var informe = new InspeccionInformeTecnico
            {
                Finalizado = true,
                RutaPdf = "/App_Data/Uploads/informe-generado.pdf",
                FirmadoInspector = false
            };

            Assert.IsFalse(AocrDcavRevisionService.EsInformeFirmadoValido(informe));
        }

        [TestMethod]
        public void EsInformeFirmadoValido_AceptaFirmaCompletaDelInspector()
        {
            var informe = new InspeccionInformeTecnico
            {
                Finalizado = true,
                FirmadoInspector = true,
                RutaDocumentoFirmado = "/App_Data/Uploads/informe-firmado.pdf",
                HashDocumento = "ABC123",
                FechaFirma1 = DateTime.Now
            };

            Assert.IsTrue(AocrDcavRevisionService.EsInformeFirmadoValido(informe));
        }
    }
}
