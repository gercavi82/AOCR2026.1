using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
 [TestClass] public class FirmaDocumentoInstitucionalIntegrationTests
 {
  [TestMethod]public void RolDireccionReal_RequiereProvisionDgac(){var p=new PerfilFirmanteService().ObtenerPerfil(47,"RECONOCIMIENTO");Assert.IsNotNull(p);Assert.IsFalse(p.AutorizadoParaDocumento);}
  [TestMethod]public void RolDireccionReal_NoEsPerfilCondiciones(){Assert.IsNull(new PerfilFirmanteService().ObtenerPerfil(47,"CONDICIONES_LIMITACIONES"));}
  [TestMethod]public void RolDcavReal_RequiereProvisionDcav(){var p=new PerfilFirmanteService().ObtenerPerfil(61,"CONDICIONES_LIMITACIONES");Assert.IsNotNull(p);Assert.IsFalse(p.AutorizadoParaDocumento);}
  [TestMethod]public void RolDcavReal_NoEsPerfilAocr(){Assert.IsNull(new PerfilFirmanteService().ObtenerPerfil(61,"RECONOCIMIENTO"));}
  [TestMethod]public void BaseActual_NoTieneEstadoFirmableFabricado(){Assert.IsNull(new FirmaDocumentoInstitucionalService().ObtenerEstadoFirmas(1,1));}
 }
}
