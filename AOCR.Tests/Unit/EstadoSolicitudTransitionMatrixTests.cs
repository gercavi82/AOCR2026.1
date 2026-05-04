using CapaDatos.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class EstadoSolicitudTransitionMatrixTests
    {
        [DataTestMethod]
        [DataRow("Solicitud Creada", "Documentacion Pendiente")]
        [DataRow("Pendiente", "En Revision")]
        [DataRow("En Revision", "En Inspeccion")]
        [DataRow("Documentacion Pendiente", "Observada")]
        [DataRow("Observada", "Subsanada")]
        [DataRow("Subsanada", "Documentacion Pendiente")]
        [DataRow("Subsanada", "En Inspeccion")]
        [DataRow("Aceptacion Documental", "Pendiente Asignacion RT")]
        [DataRow("Aceptacion Documental", "Requiere Inspeccion")]
        [DataRow("Aceptacion Documental", "Generado Condiciones y Limitaciones")]
        [DataRow("Requiere Inspeccion", "Pendiente Asignacion RT")]
        [DataRow("Requiere Inspeccion", "En Inspeccion")]
        [DataRow("Generado Condiciones y Limitaciones", "En Revision Coordinador Final")]
        [DataRow("En Revision Coordinador Final", "Enviado DCAV")]
        [DataRow("Enviado DCAV", "Firmado DCAV")]
        [DataRow("Firmado DCAV", "Finalizado")]
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
        [DataRow("Aceptacion Documental", "Enviado DCAV")]
        [DataRow("Requiere Inspeccion", "Generado Condiciones y Limitaciones")]
        [DataRow("Generado Condiciones y Limitaciones", "Firmado DCAV")]
        [DataRow("Enviado DCAV", "Finalizado")]
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

        [DataTestMethod]
        [DataRow("DOCUMENTACION_ACEPTADA", "Aceptacion Documental")]
        [DataRow("ACEPTADO_INSPECTOR", "Aceptacion Documental")]
        [DataRow("REQUIERE_INSPECCION", "Requiere Inspeccion")]
        [DataRow("GENERADO_CONDICIONES_LIMITACIONES", "Generado Condiciones y Limitaciones")]
        [DataRow("EN_REVISION_COORDINADOR_FINAL", "En Revision Coordinador Final")]
        [DataRow("ENVIADO_DCAV", "Enviado DCAV")]
        [DataRow("FIRMADO_DCAV", "Firmado DCAV")]
        [DataRow("AUTORIZACION_FIRMADA", "Firmado Coordinador")]
        [DataRow("DEVUELTO", "Observada")]
        public void Matriz_NormalizacionEstadosModificacion_DebeMapearEstadosCanonicos(string legacy, string esperado)
        {
            var normalizado = EstadoSolicitud.Normalizar(legacy);

            Assert.AreEqual(esperado, normalizado, "El estado de modificación no se normalizó correctamente: {0}", legacy);
        }

        [TestMethod]
        public void Matriz_FlujoModificacionDirecta_DebeMantenerSecuenciaCompleta()
        {
            var flujo = new[]
            {
                EstadoSolicitud.AceptacionDocumental,
                EstadoSolicitud.GeneradoCondicionesLimitaciones,
                EstadoSolicitud.EnRevisionCoordinadorFinal,
                EstadoSolicitud.EnviadoDcav,
                EstadoSolicitud.FirmadoDcav,
                EstadoSolicitud.Finalizado
            };

            for (var index = 0; index < flujo.Length - 1; index++)
            {
                Assert.IsTrue(
                    EstadoSolicitud.EsTransicionValida(flujo[index], flujo[index + 1]),
                    "La secuencia directa de modificación debe permitir {0} -> {1}",
                    flujo[index],
                    flujo[index + 1]);
            }
        }

        [TestMethod]
        public void Matriz_FlujoModificacionConInspeccion_DebeDerivarAlModuloInspeccion()
        {
            var flujo = new[]
            {
                EstadoSolicitud.AceptacionDocumental,
                EstadoSolicitud.RequiereInspeccion,
                EstadoSolicitud.PendienteAsignacionRT,
                EstadoSolicitud.EnInspeccion
            };

            for (var index = 0; index < flujo.Length - 1; index++)
            {
                Assert.IsTrue(
                    EstadoSolicitud.EsTransicionValida(flujo[index], flujo[index + 1]),
                    "La derivación a inspección debe permitir {0} -> {1}",
                    flujo[index],
                    flujo[index + 1]);
            }
        }
    }
}
