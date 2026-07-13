using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.DAOs;

namespace AOCR.Tests.Integration
{
 [TestClass] public class RevisionDocumentosDcavSegundaBandejaIntegrationTests
 {
  [TestMethod] public void ConsultaReal_NoFallaNiOcultaNpgsql(){var dao=new AocrDcavDocumentosDAO();var rows=dao.ObtenerPendientesRevisionDocumentos();Assert.IsNotNull(rows);}
  [TestMethod] public void ContadorReal_CoincideConFilas(){var dao=new AocrDcavDocumentosDAO();Assert.AreEqual(dao.ObtenerPendientesRevisionDocumentos().Count,dao.ContarPendientesRevisionDocumentos());}
 }
}
