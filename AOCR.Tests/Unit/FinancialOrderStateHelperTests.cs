using CapaDatos.Constants;
using CapaNegocio.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class FinancialOrderStateHelperTests
    {
        [TestMethod]
        public void EsPendienteGestion_EnRevisionFinancieraConComprobante_DebeSerTrue()
        {
            Assert.IsTrue(FinancialOrderStateHelper.EsPendienteGestion(
                EstadoOrden.EnRevisionFinanciera, null, false, true));
        }

        [TestMethod]
        public void EsPendienteGestion_EnRevisionFinancieraSinComprobante_DebeSerFalse()
        {
            Assert.IsFalse(FinancialOrderStateHelper.EsPendienteGestion(
                EstadoOrden.EnRevisionFinanciera, null, false, false));
        }

        [TestMethod]
        public void EsPendienteGestion_GeneradaSinComprobante_DebeSerFalse()
        {
            Assert.IsFalse(FinancialOrderStateHelper.EsPendienteGestion(
                EstadoOrden.Generada, null, false, false));
            Assert.IsTrue(FinancialOrderStateHelper.DebeOcultarDeBandejaFinanciera(EstadoOrden.Generada, false));
        }

        [TestMethod]
        public void EsPendienteGestion_Facturada_DebeSerFalse()
        {
            Assert.IsFalse(FinancialOrderStateHelper.EsPendienteGestion(
                EstadoOrden.Facturada, EstadoPago.Validado, true, true));
        }

        [TestMethod]
        public void CoincideFiltro_PendientesFinanciero_DebeCoincidirConContadorSidebar()
        {
            Assert.IsTrue(FinancialOrderStateHelper.CoincideFiltro(
                EstadoOrden.EnRevisionFinanciera,
                null,
                false,
                FinancialOrderStateHelper.PendientesFinanciero,
                true));

            Assert.IsFalse(FinancialOrderStateHelper.CoincideFiltro(
                EstadoOrden.Generada,
                null,
                false,
                FinancialOrderStateHelper.PendientesFinanciero,
                false));

            Assert.IsFalse(FinancialOrderStateHelper.CoincideFiltro(
                EstadoOrden.Facturada,
                EstadoPago.Validado,
                true,
                FinancialOrderStateHelper.PendientesFinanciero,
                true));
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
