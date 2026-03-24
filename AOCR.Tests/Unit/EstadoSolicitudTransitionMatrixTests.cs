using CapaDatos.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class EstadoSolicitudTransitionMatrixTests
    {
        [DataTestMethod]
        [DataRow("Solicitud Creada", "Documentacion Pendiente")]
        [DataRow("Documentacion Pendiente", "Observada")]
        [DataRow("Observada", "Subsanada")]
        [DataRow("Subsanada", "Documentacion Pendiente")]
        [DataRow("Aceptacion Documental", "Pendiente Asignacion RT")]
        [DataRow("Pendiente Asignacion RT", "En Inspeccion")]
        [DataRow("En Inspeccion", "AOCR En Elaboracion")]
        [DataRow("AOCR En Elaboracion", "AOCR En Revision")]
        [DataRow("AOCR En Revision", "AOCR Validado")]
        [DataRow("AOCR Validado", "AOCR Legalizado")]
        [DataRow("AOCR Legalizado", "AOCR Emitido/Recibido")]
        public void Matriz_TransicionesValidas_DebePermitir(string estadoActual, string estadoDestino)
        {
            var resultado = EstadoSolicitud.EsTransicionValida(estadoActual, estadoDestino);
            Assert.IsTrue(resultado, "Transicion esperada como valida: {0} -> {1}", estadoActual, estadoDestino);
        }

        [DataTestMethod]
        [DataRow("Solicitud Creada", "AOCR Legalizado")]
        [DataRow("Documentacion Pendiente", "AOCR En Revision")]
        [DataRow("Observada", "AOCR En Elaboracion")]
        [DataRow("Pendiente Asignacion RT", "AOCR En Revision")]
        [DataRow("En Inspeccion", "AOCR Legalizado")]
        [DataRow("AOCR En Elaboracion", "AOCR Emitido/Recibido")]
        [DataRow("AOCR En Revision", "AOCR Emitido/Recibido")]
        [DataRow("AOCR Validado", "En Inspeccion")]
        [DataRow("AOCR Emitido/Recibido", "AOCR En Revision")]
        public void Matriz_TransicionesInvalidas_DebeBloquear(string estadoActual, string estadoDestino)
        {
            var resultado = EstadoSolicitud.EsTransicionValida(estadoActual, estadoDestino);
            Assert.IsFalse(resultado, "Transicion esperada como invalida: {0} -> {1}", estadoActual, estadoDestino);
        }

        [TestMethod]
        public void Matriz_NormalizacionLegacy_DebeMapearYValidar()
        {
            var actualLegacy = "ENVIADO_A_JEFATURA";
            var destino = EstadoSolicitud.AOCR_Validado;

            var actualNormalizado = EstadoSolicitud.Normalizar(actualLegacy);
            Assert.AreEqual(EstadoSolicitud.AOCR_EnRevision, actualNormalizado);
            Assert.IsTrue(EstadoSolicitud.EsTransicionValida(actualNormalizado, destino));
        }
    }
}
