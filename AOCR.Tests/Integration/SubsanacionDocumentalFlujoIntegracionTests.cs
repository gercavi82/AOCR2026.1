using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    /// <summary>
    /// Prueba de integración contra PostgreSQL real (dgac_des).
    /// Replica el flujo: RT crea solicitud → coordinación asigna inspector →
    /// revisión documental masiva (Observada) → RT subsana (Subsanada).
    /// </summary>
    [TestClass]
    public class SubsanacionDocumentalFlujoIntegracionTests
    {
        private const int UsuarioRtId = 45;
        private const string InspectorCedula = "1709565459";
        private const string InspectorUsuarioId = "43";

        private static string _connectionString;
        private static string _dbProbeError;

        private static bool ProbarConexionBd(out string error)
        {
            error = null;
            try
            {
                _connectionString = ObtenerConnectionString();
                if (string.IsNullOrWhiteSpace(_connectionString))
                {
                    error = "No se encontró AOCRConnection (env, app.config ni AOCR.Tests.dll.config).";
                    return false;
                }

                AsegurarConnectionStringConfig();
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = FormatearExcepcion(ex);
                return false;
            }
        }

        private static string FormatearExcepcion(Exception ex)
        {
            var partes = new List<string>();
            var actual = ex;
            while (actual != null)
            {
                partes.Add(actual.GetType().Name + ": " + actual.Message);
                actual = actual.InnerException;
            }

            return string.Join(" -> ", partes);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [Description("E2E integración: solicitud → inspector → Observada → subsanación RT → Subsanada")]
        public void FlujoSubsanacionDocumental_Completo_EnBaseReal()
        {
            if (!ProbarConexionBd(out _dbProbeError))
            {
                Assert.Inconclusive(
                    "BD de integración no disponible. Configure AOCR_INTEGRATION_CONNECTION o app.config. Detalle: "
                    + (_dbProbeError ?? "desconocido"));
            }

            AsegurarConnectionStringConfig();

            var solicitudDao = new SolicitudAOCRDAO();
            var documentoDao = new DocumentoDAO();
            var infra = new SolicitudAocrInfraBL();
            var revisionService = new RevisionDocumentalService();
            var subsanacionService = new DocumentoSubsanacionService();
            var transitionBl = new SolicitudEstadoTransitionBL();

            int solicitudId = 0;
            int inspeccionId = 0;
            var documentoIds = new List<int>();

            try
            {
                // ── 1. RT crea solicitud ──
                solicitudId = solicitudDao.InsertarConReturn(CrearSolicitudPrueba());
                Assert.IsTrue(solicitudId > 0, "No se creó la solicitud de prueba.");

                Assert.IsTrue(
                    solicitudDao.CambiarEstado(solicitudId, EstadoSolicitud.EnRevision, UsuarioRtId, "E2E: envío a coordinación"),
                    "No se pudo pasar a En Revision.");

                // ── 2. Documentos habilitantes (3) ──
                documentoIds.Add(CrearDocumento(documentoDao, solicitudId, "MANUAL_OPS", "manual_ops_v1.pdf", "CARGADO"));
                documentoIds.Add(CrearDocumento(documentoDao, solicitudId, "MEL", "mel_v1.pdf", "CARGADO"));
                documentoIds.Add(CrearDocumento(documentoDao, solicitudId, "AOC", "aoc_v1.pdf", "CARGADO"));
                Assert.AreEqual(3, documentoIds.Count);

                // ── 3. Coordinación asigna inspector ──
                string msgAsignacion;
                var okAsignacion = SolicitudAOCRBL.AsignarInspectores(
                    solicitudId,
                    InspectorCedula,
                    null,
                    DateTime.Now,
                    "E2E asignación inspector",
                    "OPS",
                    "GEN_COORDINACION",
                    out msgAsignacion);
                Assert.IsTrue(okAsignacion, "Asignación inspector falló: " + msgAsignacion);

                var solicitudTrasAsignacion = solicitudDao.ObtenerPorId(solicitudId);
                Assert.AreEqual(EstadoSolicitud.EnInspeccion, EstadoSolicitud.Normalizar(solicitudTrasAsignacion.Estado));

                var inspecciones = infra.ListarInspeccionesPorSolicitud(solicitudId) ?? new List<Inspeccion>();
                Assert.IsTrue(inspecciones.Count > 0, "No se creó inspección.");
                inspeccionId = inspecciones[0].CodigoInspeccion;

                // ── 4. Inspector: revisión documental masiva (1 aceptado, 2 devueltos) ──
                var revisiones = new Dictionary<int, Tuple<string, string>>
                {
                    { documentoIds[0], Tuple.Create("ACEPTADO", string.Empty) },
                    { documentoIds[1], Tuple.Create("DEVUELTO", "Falta firma en página 2 del documento") },
                    { documentoIds[2], Tuple.Create("DEVUELTO", "Formato incorrecto según instructivo DGAC") }
                };

                var documentos = documentoDao.ObtenerPorSolicitud(solicitudId)
                    .Where(d => documentoIds.Contains(d.CodigoDocumento))
                    .ToList();

                foreach (var doc in documentos)
                {
                    var rev = revisiones[doc.CodigoDocumento];
                    var decision = rev.Item1;
                    doc.Estado = decision == "ACEPTADO" ? "APROBADO" : "RECHAZADO";
                    doc.Validado = decision == "ACEPTADO";
                    doc.Observaciones = decision == "ACEPTADO" ? null : rev.Item2;
                    doc.FechaValidacion = DateTime.Now;
                    doc.ValidadoPor = "1709565459";
                    Assert.IsTrue(documentoDao.Actualizar(doc), "No se actualizó documento " + doc.CodigoDocumento);

                    infra.RegistrarRevisionDocumental(
                        solicitudId,
                        doc.CodigoDocumento,
                        decision == "DEVUELTO" ? "DEVUELTO" : decision,
                        rev.Item2,
                        int.Parse(InspectorUsuarioId),
                        "1709565459");
                }

                var validacionCierre = revisionService.ValidarCierreRevisionDocumental(documentos, revisiones);
                Assert.IsTrue(validacionCierre.EsValido, validacionCierre.Mensaje);
                Assert.IsTrue(validacionCierre.TieneDocumentosDevueltos);

                var decisionCierre = revisionService.CrearDecisionCierreFinal(
                    validacionCierre.TieneDocumentosDevueltos,
                    "E2E: 2 documentos devueltos para subsanación RT.");

                string msgEstado;
                var okObservada = transitionBl.CambiarEstadoConReglasAocr(
                    solicitudId,
                    decisionCierre.EstadoDestino,
                    decisionCierre.ObservacionCierre,
                    int.Parse(InspectorUsuarioId),
                    null,
                    out msgEstado);
                Assert.IsTrue(okObservada, "Transición a Observada falló: " + msgEstado);
                Assert.AreEqual(EstadoSolicitud.Observada, EstadoSolicitud.Normalizar(solicitudDao.ObtenerPorId(solicitudId).Estado));

                // ── 5. Correo consolidado RT ──
                var solicitudObservada = solicitudDao.ObtenerPorId(solicitudId);
                var itemsCorreo = documentos
                    .Where(d => revisiones[d.CodigoDocumento].Item1 == "DEVUELTO")
                    .Select(d => new DocumentoDevueltoNotificacionItem
                    {
                        CodigoDocumento = d.CodigoDocumento,
                        Etiqueta = d.TipoDocumento,
                        Observacion = revisiones[d.CodigoDocumento].Item2
                    })
                    .ToList();

                var eventKey = subsanacionService.ConstruirEventKeyDocumentosDevueltos(
                    solicitudId,
                    itemsCorreo.Select(x => x.CodigoDocumento));
                Assert.AreEqual(
                    "DOCUMENTOS_DEVUELTOS_INSPECTOR_" + solicitudId + "_" + documentoIds[1] + "_" + documentoIds[2],
                    eventKey);

                var resultadoCorreo = subsanacionService.EncolarCorreoDocumentosDevueltosInspector(
                    solicitudObservada,
                    itemsCorreo,
                    "Inspector E2E",
                    "https://aocr.test/Subsanar/" + solicitudId,
                    eventKey);
                Assert.IsTrue(resultadoCorreo.Exitoso, resultadoCorreo.Mensaje);

                Assert.IsTrue(ExisteEmailQueue(solicitudId, "DOCUMENTOS_DEVUELTOS_INSPECTOR", eventKey),
                    "No se encoló correo consolidado DOCUMENTOS_DEVUELTOS_INSPECTOR.");

                // ── 6. RT subsana solo devueltos ──
                var revisionesBd = infra.ObtenerUltimasRevisionesPorSolicitud(solicitudId);
                var clasificacion = subsanacionService.ClasificarDocumentosParaRt(
                    documentoDao.ObtenerPorSolicitud(solicitudId),
                    revisionesBd,
                    EstadoSolicitud.Observada);

                Assert.AreEqual(2, clasificacion.DocumentosDevueltos.Count, "Deben quedar 2 documentos devueltos.");
                Assert.AreEqual(1, clasificacion.DocumentosBloqueados.Count, "Debe quedar 1 documento bloqueado.");

                foreach (var docDevuelto in clasificacion.DocumentosDevueltos)
                {
                    var validacion = subsanacionService.ValidarCargaSubsanacionRt(
                        docDevuelto,
                        revisionesBd,
                        EstadoSolicitud.Observada,
                        true);
                    Assert.IsTrue(validacion.EsValido, validacion.Mensaje);
                }

                foreach (var docBloqueado in clasificacion.DocumentosBloqueados)
                {
                    var validacionBloqueo = subsanacionService.ValidarCargaSubsanacionRt(
                        docBloqueado,
                        revisionesBd,
                        EstadoSolicitud.Observada,
                        true);
                    Assert.IsFalse(validacionBloqueo.EsValido);
                    StringAssert.Contains(validacionBloqueo.Mensaje, "no fue devuelto");
                }

                foreach (var docDevuelto in clasificacion.DocumentosDevueltos)
                {
                    var versionAnterior = docDevuelto.Version ?? 1;
                    var nuevo = new Documento
                    {
                        CodigoSolicitud = solicitudId,
                        TipoDocumento = docDevuelto.TipoDocumento,
                        NombreArchivo = "subsanado_" + docDevuelto.CodigoDocumento + ".pdf",
                        RutaGuardada = "~/App_Data/Uploads/AOCR/" + solicitudId + "/Documentos/e2e.pdf",
                        Extension = ".pdf",
                        TamanoBytes = 1024,
                        Estado = "PENDIENTE_REVISION_SUBSANACION",
                        Validado = false,
                        FechaCarga = DateTime.Now,
                        Observaciones = "E2E subsanación RT",
                        Version = versionAnterior + 1,
                        UsuarioRegistro = "GACAJAS"
                    };
                    var nuevoId = documentoDao.Crear(nuevo);
                    Assert.IsTrue(nuevoId > 0);

                    docDevuelto.Estado = EstadoDocumentoInstitucional.ResolverEstadoVersionAnterior();
                    Assert.IsTrue(documentoDao.Actualizar(docDevuelto));

                    infra.RegistrarRevisionDocumental(
                        solicitudId,
                        nuevoId,
                        "PENDIENTE_REVISION_SUBSANACION",
                        "E2E subsanación RT",
                        UsuarioRtId,
                        "GACAJAS");
                }

                var okSubsanada = transitionBl.CambiarEstadoConReglasAocr(
                    solicitudId,
                    EstadoSolicitud.Subsanada,
                    "E2E: RT subsanó documentación devuelta.",
                    UsuarioRtId,
                    null,
                    out msgEstado,
                    omitirCorreoGenericoCambioEstado: true,
                    omitirCorreoWorkflowEstado: true);
                Assert.IsTrue(okSubsanada, "Transición a Subsanada falló: " + msgEstado);
                Assert.AreEqual(EstadoSolicitud.Subsanada, EstadoSolicitud.Normalizar(solicitudDao.ObtenerPorId(solicitudId).Estado));

                // ── 7. Verificaciones finales ──
                var docsFinales = documentoDao.ObtenerPorSolicitud(solicitudId);
                Assert.IsTrue(
                    docsFinales.Any(d => EstadoDocumentoInstitucional.Normalizar(d.Estado) == EstadoDocumentoInstitucional.VersionAnterior),
                    "Debe existir al menos un documento VERSION_ANTERIOR.");
                Assert.AreEqual(
                    2,
                    docsFinales.Count(d => EstadoDocumentoInstitucional.Normalizar(d.Estado) == EstadoDocumentoInstitucional.PendienteRevisionSubsanacion),
                    "Deben existir 2 documentos pendientes de revisión tras subsanación.");
                Assert.AreEqual(
                    1,
                    docsFinales.Count(d => EstadoDocumentoInstitucional.Normalizar(d.Estado) == EstadoDocumentoInstitucional.Aceptado),
                    "Debe permanecer 1 documento aprobado/bloqueado.");
            }
            finally
            {
                if (solicitudId > 0)
                {
                    LimpiarSolicitudPrueba(solicitudId);
                }
            }
        }

        private static SolicitudAOCR CrearSolicitudPrueba()
        {
            var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return new SolicitudAOCR
            {
                NumeroSolicitud = "E2E-SUB-" + suffix,
                FechaSolicitud = DateTime.Now,
                TipoSolicitud = 1,
                NombreOperador = "Operador E2E Subsanacion",
                Ruc = "1799999999001",
                RazonSocial = "Compania E2E Subsanacion SA",
                Email = "mancho2002@hotmail.com",
                CorreoRepresentanteTecnico = "mancho2002@hotmail.com",
                Telefono = "0999999999",
                Direccion = "Quito",
                RepresentanteLegal = "RT E2E",
                CedulaRepresentante = "1711111111",
                TipoOperacion = "Transporte Aereo Comercial",
                DescripcionOperacion = "Prueba integracion subsanacion documental",
                Observaciones = "Generado por SubsanacionDocumentalFlujoIntegracionTests",
                CodigoUsuario = UsuarioRtId,
                Estado = EstadoSolicitud.Pendiente
            };
        }

        private static int CrearDocumento(DocumentoDAO dao, int solicitudId, string tipo, string nombre, string estado)
        {
            return dao.Crear(new Documento
            {
                CodigoSolicitud = solicitudId,
                TipoDocumento = tipo,
                NombreArchivo = nombre,
                RutaGuardada = "~/App_Data/Uploads/AOCR/" + solicitudId + "/Documentos/" + nombre,
                Extension = ".pdf",
                TamanoBytes = 2048,
                Estado = estado,
                Validado = false,
                FechaCarga = DateTime.Now,
                Version = 1,
                UsuarioRegistro = "GACAJAS"
            });
        }

        private static bool ExisteEmailQueue(int solicitudId, string tipoNotificacion, string eventKey)
        {
            var emailService = new EmailQueueService();
            return emailService
                .ExisteNotificacionAsync(tipoNotificacion, eventKey, solicitudId)
                .GetAwaiter()
                .GetResult();
        }

        private static void LimpiarSolicitudPrueba(int solicitudId)
        {
            try
            {
                new CapaDatos.Repositories.SolicitudRepository().Eliminar(solicitudId);
            }
            catch
            {
                // Limpieza best-effort en prueba de integración.
            }
        }

        private static string ObtenerConnectionString()
        {
            var env = Environment.GetEnvironmentVariable("AOCR_INTEGRATION_CONNECTION");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            var fromConfig = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                return fromConfig;
            }

            return LeerConnectionStringDesdeArchivoConfig();
        }

        private static string LeerConnectionStringDesdeArchivoConfig()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AOCR.Tests.dll.config");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var doc = new XmlDocument();
            doc.Load(configPath);
            var node = doc.SelectSingleNode("//connectionStrings/add[@name='AOCRConnection']");
            return node?.Attributes?["connectionString"]?.Value;
        }

        private static void AsegurarConnectionStringConfig()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                _connectionString = ObtenerConnectionString();
            }

            if (ConfigurationManager.ConnectionStrings["AOCRConnection"] != null
                && ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString == _connectionString)
            {
                return;
            }

            var field = typeof(ConfigurationElementCollection).GetField("bReadOnly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var collection = ConfigurationManager.ConnectionStrings;
            if (field != null)
            {
                field.SetValue(collection, false);
            }

            if (ConfigurationManager.ConnectionStrings["AOCRConnection"] == null)
            {
                collection.Add(new ConnectionStringSettings("AOCRConnection", _connectionString, "Npgsql"));
            }

            if (ConfigurationManager.ConnectionStrings["PostgreSQL"] == null)
            {
                collection.Add(new ConnectionStringSettings("PostgreSQL", _connectionString, "Npgsql"));
            }
        }
    }
}
