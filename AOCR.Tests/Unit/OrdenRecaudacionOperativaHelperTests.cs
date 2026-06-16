using CapaDatos.Constants;
using CapaNegocio.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenRecaudacionOperativaHelperTests
    {
        [TestMethod]
        public void EsOrdenCerradaPostAprobacionFinanciera_IncluyeEstadosFinales()
        {
            Assert.IsTrue(OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(EstadoOrden.Facturada));
            Assert.IsTrue(OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(EstadoOrden.OrdenCerradaPorSolicitud));
            Assert.IsFalse(OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(EstadoOrden.EnRevisionFinanciera));
        }

        [TestMethod]
        public void PermiteSubirComprobante_BloqueaOrdenCerrada()
        {
            Assert.IsFalse(OrdenRecaudacionOperativaHelper.PermiteSubirComprobante(EstadoOrden.OrdenCerradaPorSolicitud));
            Assert.IsTrue(OrdenRecaudacionOperativaHelper.PermiteSubirComprobante(EstadoOrden.Generada));
        }

        [TestMethod]
        public void FinancialOrderStateHelper_NoCuentaOrdenCerradaComoPendiente()
        {
            Assert.IsFalse(FinancialOrderStateHelper.EsPendienteGestion(
                EstadoOrden.OrdenCerradaPorSolicitud,
                EstadoPago.Aprobado,
                false,
                true));
        }

        [TestMethod]
        public void ResolverEstadoPagoPostAprobacion_RetornaAprobadoPermitidoEnBd()
        {
            var estado = OrdenRecaudacionOperativaHelper.ResolverEstadoPagoPostAprobacion();
            Assert.AreEqual(EstadoPago.Aprobado, estado);
            Assert.IsTrue(EstadoPago.EsPermitidoEnBaseDatos(estado));
        }

        [TestMethod]
        public void ValidarOPrepararEstadoPersistencia_MapeaPagoAprobadoLegacyAAprobado()
        {
            var estado = EstadoPago.ValidarOPrepararEstadoPersistencia(EstadoPago.PagoAprobado);
            Assert.AreEqual(EstadoPago.Aprobado, estado);
        }

        [TestMethod]
        public void ResolverEstadoOrdenPostAprobacion_RetornaCompletadaPermitidaEnBd()
        {
            var estado = OrdenRecaudacionOperativaHelper.ResolverEstadoOrdenPostAprobacion();
            Assert.AreEqual(EstadoOrden.Completada, estado);
            Assert.IsTrue(EstadoOrden.EsPermitidoEnBaseDatos(estado));
        }

        [TestMethod]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void ValidarOPrepararEstadoPersistencia_RechazaEstadoAs400()
        {
            EstadoPago.ValidarOPrepararEstadoPersistencia("ENVIADO_AS400");
        }
    }
}
