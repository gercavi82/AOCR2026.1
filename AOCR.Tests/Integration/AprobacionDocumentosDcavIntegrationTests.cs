using CapaDatos.DAOs;
using CapaNegocio.Services;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
 [TestClass]public class AprobacionDocumentosDcavIntegrationTests
 {
  [TestMethod]public void ConsultaBandejaFirmaReal_NoFalla(){Assert.IsNotNull(new FirmaInstitucionalAocrDAO().ObtenerPendientes());}
  [TestMethod]public void ContadorFirma_CoincideConBandeja(){var d=new FirmaInstitucionalAocrDAO();Assert.AreEqual(d.ObtenerPendientes().Count,d.ContarPendientes());}
  [TestMethod]public void BandejaDcav_ConservaConsultaExclusiva(){var d=new AocrDcavDocumentosDAO();Assert.AreEqual(d.ObtenerPendientesRevisionDocumentos().Count,d.ContarPendientesRevisionDocumentos());}
  [TestMethod]public void UsuarioDireccionReal_NoPuedeAprobarComoDcav(){Assert.AreEqual(403,Servicio().Validar(1,1,6).Codigo);}
  [TestMethod]public void UsuarioDcavReal_SuperaAutorizacion(){Assert.AreNotEqual(403,Servicio().Validar(1,1,61).Codigo);}
  private static AprobacionDocumentosDcavService Servicio(){return new AprobacionDocumentosDcavService(new AocrDcavDocumentosDAO(),new AprobacionDocumentosDcavDAO(),new DocumentoPdfService(Path.GetTempPath()));}
 }
}
