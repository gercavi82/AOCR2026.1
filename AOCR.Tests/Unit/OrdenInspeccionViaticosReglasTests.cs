using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenInspeccionViaticosReglasTests
    {
        [DataTestMethod]
        [DataRow("EMI_AOCR")]
        [DataRow("REN_AOCR")]
        [DataRow("MOD_AOCR_INC")]
        public void TramitesAocrDefinidos_RequierenInspeccionExterna(string codigo)
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.RequiereInspeccionExterna(codigo));
        }

        [DataTestMethod]
        [DataRow("QUITO")]
        [DataRow(" latacunga ")]
        public void QuitoYLatacunga_NoAplicanViaticos(string lugar)
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsLugarSinViaticos(lugar));
        }

        [DataTestMethod]
        [DataRow("GUAYAQUIL")]
        [DataRow("MANTA")]
        [DataRow("OTRA_PROVINCIA")]
        public void LugaresExternos_PermitenEvaluarViaticos(string lugar)
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsLugarPermitido(lugar));
            Assert.IsFalse(OrdenInspeccionViaticosReglas.EsLugarSinViaticos(lugar));
        }

        [TestMethod]
        public void SeleccionMultiple_SoloQuitoYLatacunga_NoAplicaViaticos()
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsLugarPermitido("QUITO,LATACUNGA"));
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsLugarSinViaticos("QUITO,LATACUNGA"));
            Assert.IsFalse(OrdenInspeccionViaticosReglas.TieneLugarConViaticos("QUITO,LATACUNGA"));
        }

        [TestMethod]
        public void SeleccionMultiple_ConLugarExterno_AplicaViaticos()
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsLugarPermitido("QUITO,GUAYAQUIL"));
            Assert.IsFalse(OrdenInspeccionViaticosReglas.EsLugarSinViaticos("QUITO,GUAYAQUIL"));
            Assert.IsTrue(OrdenInspeccionViaticosReglas.TieneLugarConViaticos("QUITO,GUAYAQUIL"));
        }

        [TestMethod]
        public void SeleccionMultiple_EliminaDuplicadosYRechazaValoresDesconocidos()
        {
            Assert.AreEqual("QUITO,MANTA", OrdenInspeccionViaticosReglas.UnirLugares(new[] { "quito", "MANTA", "QUITO" }));
            Assert.IsFalse(OrdenInspeccionViaticosReglas.EsLugarPermitido("QUITO,NO_EXISTE"));
        }

        [DataTestMethod]
        [DataRow(1, 0, 0)]
        [DataRow(2, 1, 80)]
        [DataRow(3, 2, 160)]
        public void Viaticos_SeCalculanConUnDiaMenos(int diasInspeccion, int diasPagados, int subtotal)
        {
            Assert.AreEqual(diasPagados, OrdenInspeccionViaticosReglas.CalcularDiasPagadosViatico(diasInspeccion));
            Assert.AreEqual((decimal)subtotal, OrdenInspeccionViaticosReglas.CalcularSubtotalViatico(diasInspeccion, 80m));
        }

        [TestMethod]
        public void OtraProvincia_ExigeTextoSeguro()
        {
            Assert.IsTrue(OrdenInspeccionViaticosReglas.RequiereProvinciaLocalidad("OTRA_PROVINCIA"));
            Assert.IsTrue(OrdenInspeccionViaticosReglas.EsProvinciaLocalidadSegura("Manabi / Portoviejo"));
            Assert.IsFalse(OrdenInspeccionViaticosReglas.EsProvinciaLocalidadSegura("<script>alert(1)</script>"));
        }
    }
}
