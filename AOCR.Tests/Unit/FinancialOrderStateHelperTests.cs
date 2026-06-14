using CapaDatos.Constants;
using CapaNegocio.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class FinancialOrderStateHelperTests
    {
        [TestMethod]
        public void EsPendienteGestion_EnRevisionFinanciera_DebeSerTrue()
        {
            Assert.IsTrue(FinancialOrderStateHelper.EsPendienteGestion(EstadoOrden.EnRevisionFinanciera, null, false));
        }

        [TestMethod]
        public void EsPendienteGestion_Facturada_DebeSerFalse()
        {
            Assert.IsFalse(FinancialOrderStateHelper.EsPendienteGestion(EstadoOrden.Facturada, EstadoPago.Validado, true));
        }

        [TestMethod]
        public void CoincideFiltro_PendientesFinanciero_DebeCoincidirConContadorSidebar()
        {
            Assert.IsTrue(FinancialOrderStateHelper.CoincideFiltro(
                EstadoOrden.EnRevisionFinanciera,
                null,
                false,
                FinancialOrderStateHelper.PendientesFinanciero));

            Assert.IsFalse(FinancialOrderStateHelper.CoincideFiltro(
                EstadoOrden.Facturada,
                EstadoPago.Validado,
                true,
                FinancialOrderStateHelper.PendientesFinanciero));
        }

        [TestMethod]
        public void NormalizarFiltro_PendientesAlias_DebeUnificar()
        {
            Assert.AreEqual(
                FinancialOrderStateHelper.PendientesFinanciero,
                FinancialOrderStateHelper.NormalizarFiltro("PAGOS_PENDIENTES"));
        }
    }
}
