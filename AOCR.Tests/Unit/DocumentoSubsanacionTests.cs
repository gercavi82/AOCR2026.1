using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DocumentoSubsanacionTests
    {
    public class FakeUsuarioAS400DAO : CapaDatos.Interfaces.IUsuarioAS400DAO
    {
        public string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario) => "UIO";
        public CapaDatos.Models.UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario) => new CapaDatos.Models.UsuarioInternoAs400Info();
        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario) => "1790000000001";
        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario) => "1700000000";
        public bool UpsertUsuarioCompleto(CapaDatos.Models.UsuarioAs400Record record, out string error) { error = null; return true; }
    }

    public class FakeEmpresaAS400DAO : CapaDatos.Interfaces.IEmpresaAS400DAO
    {
        public bool TestConnection() => true;
        public System.Collections.Generic.List<CapaDatos.DAOs.Empresa> ObtenerEmpresas() => new System.Collections.Generic.List<CapaDatos.DAOs.Empresa>();
        public CapaDatos.DAOs.Empresa ObtenerEmpresaPorCodigo(string codigoOaci) => new CapaDatos.DAOs.Empresa { CodigoOaci = codigoOaci };
    }
        private readonly DocumentoSubsanacionService _service = new DocumentoSubsanacionService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());

        [TestMethod]
        public void PuedeRtSubsanarDocumento_SoloDevueltoPorInspector_RetornaTrue()
        {
            var revisiones = new Dictionary<int, Tuple<string, string>>
            {
                { 101, Tuple.Create("DEVUELTO", "Falta firma") },
                { 102, Tuple.Create("ACEPTADO", string.Empty) }
            };

            var devuelto = new Documento { CodigoDocumento = 101, Estado = "RECHAZADO" };
            var aceptado = new Documento { CodigoDocumento = 102, Estado = "APROBADO" };

            Assert.IsTrue(_service.PuedeRtSubsanarDocumento(devuelto, revisiones, EstadoSolicitud.Observada, true));
            Assert.IsFalse(_service.PuedeRtSubsanarDocumento(aceptado, revisiones, EstadoSolicitud.Observada, true));
        }

        [TestMethod]
        public void ClasificarDocumentosParaRt_SeparatesDevueltosAndBloqueados()
        {
            var revisiones = new Dictionary<int, Tuple<string, string>>
            {
                { 1, Tuple.Create("ACEPTADO", string.Empty) },
                { 2, Tuple.Create("ACEPTADO", string.Empty) },
                { 3, Tuple.Create("ACEPTADO", string.Empty) },
                { 4, Tuple.Create("ACEPTADO", string.Empty) },
                { 5, Tuple.Create("ACEPTADO", string.Empty) },
                { 6, Tuple.Create("DEVUELTO", "Observación 1") },
                { 7, Tuple.Create("DEVUELTO", "Observación 2") }
            };

            var documentos = revisiones.Keys.Select(id => new Documento
            {
                CodigoDocumento = id,
                TipoDocumento = "DOC_" + id,
                Estado = id <= 5 ? "APROBADO" : "RECHAZADO"
            }).ToList();

            var clasificacion = _service.ClasificarDocumentosParaRt(documentos, revisiones, EstadoSolicitud.Observada);

            Assert.AreEqual(2, clasificacion.DocumentosDevueltos.Count);
            Assert.AreEqual(5, clasificacion.DocumentosBloqueados.Count);
        }

        [TestMethod]
        public void ValidarCargaSubsanacionRt_DocumentoAceptado_RetornaErrorInstitucional()
        {
            var revisiones = new Dictionary<int, Tuple<string, string>>
            {
                { 50, Tuple.Create("ACEPTADO", string.Empty) }
            };

            var documento = new Documento { CodigoDocumento = 50, Estado = "APROBADO" };
            var validacion = _service.ValidarCargaSubsanacionRt(documento, revisiones, EstadoSolicitud.Observada, true);

            Assert.IsFalse(validacion.EsValido);
            StringAssert.Contains(validacion.Mensaje, "no fue devuelto por el Inspector");
        }

        [TestMethod]
        public void ConstruirEventKeyDocumentosDevueltos_EsDeterministico()
        {
            var key1 = _service.ConstruirEventKeyDocumentosDevueltos(12, new[] { 102, 101 });
            var key2 = _service.ConstruirEventKeyDocumentosDevueltos(12, new[] { 101, 102 });

            Assert.AreEqual("DOCUMENTOS_DEVUELTOS_INSPECTOR_12_101_102", key1);
            Assert.AreEqual(key1, key2);
        }

        [TestMethod]
        public void EstadoDocumentoInstitucional_NormalizaLegacyAInstitucional()
        {
            Assert.AreEqual(EstadoDocumentoInstitucional.Aceptado, EstadoDocumentoInstitucional.Normalizar("APROBADO"));
            Assert.AreEqual(EstadoDocumentoInstitucional.DevueltoInspector, EstadoDocumentoInstitucional.Normalizar("DEVUELTO"));
            Assert.AreEqual(EstadoDocumentoInstitucional.VersionAnterior, EstadoDocumentoInstitucional.Normalizar("VERSION_ANTERIOR"));
        }
    }
}
