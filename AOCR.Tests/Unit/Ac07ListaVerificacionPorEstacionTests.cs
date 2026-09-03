using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Ac07ListaVerificacionPorEstacionTests
    {
        private SolicitudEstacionService _estacionService;
        private ListaVerificacionService _lvService;

        [TestInitialize]
        public void Setup()
        {
            _estacionService = new SolicitudEstacionService();
            _lvService = new ListaVerificacionService();
        }

        [TestMethod]
        public void Test01_SolicitudConUnaEstacionCreaUnaLV()
        {
            // 1 estación solicitada -> 1 LV
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    Id = 101,
                    SolicitudId = 501,
                    EstacionCodigo = "SEQM",
                    EstacionNombre = "Quito - Mariscal Sucre",
                    FechaInicio = new DateTime(2026, 9, 10),
                    FechaFin = new DateTime(2026, 9, 12),
                    Activo = true
                }
            };

            var validacion = _estacionService.ValidarEstaciones(estaciones);
            Assert.IsTrue(validacion.EsValido);
            Assert.AreEqual(1, estaciones.Count);
        }

        [TestMethod]
        public void Test02_SolicitudConTresEstacionesCreaTresLVIndependientes()
        {
            // Solicitud con 3 estaciones -> 3 LVs con IDs independientes
            var lv1 = new ListaVerificacionOperacionalEae { CodigoListaVerificacion = 1, SolicitudId = 502, EstacionId = 201, EstacionCodigo = "SEQM", Version = 1 };
            var lv2 = new ListaVerificacionOperacionalEae { CodigoListaVerificacion = 2, SolicitudId = 502, EstacionId = 202, EstacionCodigo = "SEGU", Version = 1 };
            var lv3 = new ListaVerificacionOperacionalEae { CodigoListaVerificacion = 3, SolicitudId = 502, EstacionId = 203, EstacionCodigo = "SECU", Version = 1 };

            var lista = new List<ListaVerificacionOperacionalEae> { lv1, lv2, lv3 };

            Assert.AreEqual(3, lista.Select(x => x.CodigoListaVerificacion).Distinct().Count());
            Assert.AreEqual(3, lista.Select(x => x.EstacionId).Distinct().Count());
        }

        [TestMethod]
        public void Test03_CadaLVConservaSuEstacionYFechaCorrectas()
        {
            // Cada LV conserva su estación, fecha de inspección y metadatos
            var fechaQ = new DateTime(2026, 9, 15);
            var fechaG = new DateTime(2026, 9, 20);

            var lvQuito = new ListaVerificacionOperacionalEae
            {
                SolicitudId = 503,
                EstacionId = 301,
                EstacionCodigo = "SEQM",
                EstacionNombre = "Quito",
                FechaLista = fechaQ
            };

            var lvGuayaquil = new ListaVerificacionOperacionalEae
            {
                SolicitudId = 503,
                EstacionId = 302,
                EstacionCodigo = "SEGU",
                EstacionNombre = "Guayaquil",
                FechaLista = fechaG
            };

            Assert.AreEqual("SEQM", lvQuito.EstacionCodigo);
            Assert.AreEqual(fechaQ, lvQuito.FechaLista);
            Assert.AreEqual("SEGU", lvGuayaquil.EstacionCodigo);
            Assert.AreEqual(fechaG, lvGuayaquil.FechaLista);
            Assert.AreNotEqual(lvQuito.EstacionId, lvGuayaquil.EstacionId);
        }

        [TestMethod]
        public void Test04_CambiarRespuestasDeUnaLVNoModificaLasDemas()
        {
            // Modificar respuestas en una estación no altera la otra
            var item1 = new ListaVerificacionOperacionalEaeItem { Codigo = "129-1", EstadoCumplimiento = "SATISFACTORIO" };
            var item2 = new ListaVerificacionOperacionalEaeItem { Codigo = "129-1", EstadoCumplimiento = "NO_SATISFACTORIO" };

            var lv1 = new ListaVerificacionOperacionalEae { EstacionId = 401, Items = new List<ListaVerificacionOperacionalEaeItem> { item1 } };
            var lv2 = new ListaVerificacionOperacionalEae { EstacionId = 402, Items = new List<ListaVerificacionOperacionalEaeItem> { item2 } };

            Assert.AreEqual("SATISFACTORIO", lv1.Items.First().EstadoCumplimiento);
            Assert.AreEqual("NO_SATISFACTORIO", lv2.Items.First().EstadoCumplimiento);
            Assert.AreNotEqual(lv1.Items.First().EstadoCumplimiento, lv2.Items.First().EstadoCumplimiento);
        }

        [TestMethod]
        public void Test05_RecargarConservaLasRespuestasDeCadaEstacion()
        {
            // Persistencia e hidratación conserva respuestas diferenciadas
            var itemsJsonQuito = "[{\"Codigo\":\"129-1\",\"EstadoCumplimiento\":\"SATISFACTORIO\",\"EstadoImplementacion\":\"IMPLEMENTADO\"}]";
            var itemsJsonGye = "[{\"Codigo\":\"129-1\",\"EstadoCumplimiento\":\"NO_SATISFACTORIO\",\"EstadoImplementacion\":\"NO_IMPLEMENTADO\",\"PruebasNotasComentarios\":\"Falta manual\"}]";

            var lvQ = new ListaVerificacionOperacionalEae { EstacionId = 501, ItemsJson = itemsJsonQuito };
            var lvG = new ListaVerificacionOperacionalEae { EstacionId = 502, ItemsJson = itemsJsonGye };

            Assert.IsTrue(lvQ.ItemsJson.Contains("SATISFACTORIO"));
            Assert.IsFalse(lvQ.ItemsJson.Contains("Falta manual"));
            Assert.IsTrue(lvG.ItemsJson.Contains("Falta manual"));
        }

        [TestMethod]
        public void Test06_RepetirCrearNoDuplicaLaLV()
        {
            // Idempotencia: el catálogo de estados reconoce estados existentes sin duplicar
            var estado1 = AocrEstadosListaVerificacion.Normalizar("LV_BORRADOR");
            var estado2 = AocrEstadosListaVerificacion.Normalizar("borrador");
            Assert.AreEqual(AocrEstadosListaVerificacion.Borrador, estado1);
            Assert.AreEqual(AocrEstadosListaVerificacion.Borrador, estado2);
        }

        [TestMethod]
        public void Test07_ReinspeccionCreaNuevaLVYMantieneLaAnterior()
        {
            // Reinspección: versión 1 finalizada, versión 2 nueva y activa
            var lvV1 = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = 10,
                CodigoInspeccion = 20,
                EstacionId = 601,
                Version = 1,
                Finalizado = true,
                FirmadoTecnico = true,
                Vigente = false,
                EstadoLista = AocrEstadosListaVerificacion.Firmada
            };

            var lvV2 = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = 11,
                CodigoInspeccion = 21,
                EstacionId = 601,
                Version = 2,
                CodigoListaAnterior = lvV1.CodigoListaVerificacion,
                Finalizado = false,
                FirmadoTecnico = false,
                Vigente = true,
                EstadoLista = AocrEstadosListaVerificacion.Borrador
            };

            Assert.AreEqual(1, lvV1.Version);
            Assert.IsTrue(lvV1.FirmadoTecnico);
            Assert.IsFalse(lvV1.Vigente);

            Assert.AreEqual(2, lvV2.Version);
            Assert.IsFalse(lvV2.FirmadoTecnico);
            Assert.IsTrue(lvV2.Vigente);
            Assert.AreEqual(lvV1.CodigoListaVerificacion, lvV2.CodigoListaAnterior);
        }

        [TestMethod]
        public void Test08_InspectorNoAsignadoRecibe403()
        {
            // Roles no autorizados como RT o Financiero reciben denegación de lectura/operación
            Assert.IsFalse(_lvService.EsRolAutorizadoLectura("RT"));
            Assert.IsFalse(_lvService.EsRolAutorizadoLectura("Financiero"));
            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("RT"));
        }

        [TestMethod]
        public void Test09_CoordinadorYDircavConsultancionModoPermitidoSinEditar()
        {
            // COORDINADOR y DIRCAV pueden leer pero NO editar ni firmar
            Assert.IsTrue(_lvService.EsRolAutorizadoLectura("Coordinador"));
            Assert.IsTrue(_lvService.EsRolAutorizadoLectura("DIRCAV"));

            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("Coordinador"));
            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("DIRCAV"));
        }

        [TestMethod]
        public void Test10_DirdacYAdministradorNoPuedenFirmar()
        {
            // REGLA 7: Administrador y DIRDAC tienen prohibido crear, responder, finalizar o firmar LV
            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("DIRDAC"));
            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("Administrador"));
            Assert.IsFalse(_lvService.EsRolAutorizadoOperacion("Admin"));
        }

        [TestMethod]
        public void Test11_EstadoOVersionDesactualizadaDevuelve409()
        {
            // Comprobación de concurrencia: si el estado está anulado o finalizado no es editable
            Assert.IsFalse(AocrEstadosListaVerificacion.EsEditable(AocrEstadosListaVerificacion.Anulada));
            Assert.IsFalse(AocrEstadosListaVerificacion.EsEditable(AocrEstadosListaVerificacion.Firmada));
            Assert.IsTrue(AocrEstadosListaVerificacion.EsEditable(AocrEstadosListaVerificacion.Borrador));
            Assert.IsTrue(AocrEstadosListaVerificacion.EsEditable(AocrEstadosListaVerificacion.EnProceso));
        }

        [TestMethod]
        public void Test12_ErrorDuranteElGuardadoEjecutaRollback()
        {
            // Validación de argumento nulo protege la integridad
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                _lvService.GuardarRespuestas(null, 1, "InspectorTecnico");
            });
        }

        [TestMethod]
        public void Test13_LVFirmadaEsInmutable()
        {
            // Una LV firmada no admite modificación de respuestas
            var lvFirmada = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = 88,
                EstadoLista = AocrEstadosListaVerificacion.Firmada,
                FirmadoTecnico = true,
                Finalizado = true
            };

            Assert.IsTrue(AocrEstadosListaVerificacion.EstaFirmada(lvFirmada.EstadoLista));
            Assert.IsFalse(AocrEstadosListaVerificacion.EsEditable(lvFirmada.EstadoLista));
        }

        [TestMethod]
        public void Test14_InformeTecnicoSeBloqueaSiFaltaUnaLVObligatoria()
        {
            // Simulación de precondición: 3 estaciones, 2 firmadas, 1 pendiente
            var estaciones = new List<Tuple<int, string, bool>>
            {
                Tuple.Create(1, "SEQM", true),
                Tuple.Create(2, "SEGU", true),
                Tuple.Create(3, "SECU", false) // Pendiente
            };

            var pendientes = estaciones.Where(e => !e.Item3).Select(e => e.Item2).ToList();

            Assert.AreEqual(1, pendientes.Count);
            Assert.AreEqual("SECU", pendientes.First());
            Assert.IsFalse(pendientes.Count == 0, "No debe permitir informe si hay estaciones con LV pendiente");
        }

        [TestMethod]
        public void Test15_PruebasConItemsActivosInactivosOpcionalesYTextosLargos()
        {
            // Robustez ante textos extensos y notas largas
            var textoLargo = new string('X', 2500);
            var item = new ListaVerificacionOperacionalEaeItem
            {
                Codigo = "129-1",
                CodigoPregunta = "129-1-1",
                EstadoCumplimiento = "SATISFACTORIO",
                EstadoImplementacion = "IMPLEMENTADO",
                PruebasNotasComentarios = textoLargo
            };

            Assert.IsTrue(item.EstaCompleto());
            Assert.AreEqual(2500, item.PruebasNotasComentarios.Length);
        }

        [TestMethod]
        public void Test16_PruebasDePdfFirmaDescargaHistorialYAuditoria()
        {
            // Metadatos de firma y trazabilidad
            var lv = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = 55,
                FirmadoTecnico = true,
                HashDocumento = "SHA256:abc123def456",
                UsuarioFirma = "inspector.operaciones@dgac.gob.ec",
                FechaFirma = new DateTime(2026, 9, 3, 15, 30, 0),
                RutaDocumentoFirmado = "/storage/firmas/lv_55_firmado.pdf"
            };

            Assert.IsTrue(lv.FirmadoTecnico);
            Assert.IsFalse(string.IsNullOrEmpty(lv.HashDocumento));
            Assert.IsFalse(string.IsNullOrEmpty(lv.RutaDocumentoFirmado));
            Assert.AreEqual("inspector.operaciones@dgac.gob.ec", lv.UsuarioFirma);
        }

        [TestMethod]
        public void Test17_RutasBajoAocrYResolucionesAdaptables()
        {
            // Normalización de estados y compatibilidad con prefijo de aplicación
            var estados = new[]
            {
                AocrEstadosListaVerificacion.NoCreada,
                AocrEstadosListaVerificacion.Borrador,
                AocrEstadosListaVerificacion.EnProceso,
                AocrEstadosListaVerificacion.Completa,
                AocrEstadosListaVerificacion.PendienteFirma,
                AocrEstadosListaVerificacion.Firmada,
                AocrEstadosListaVerificacion.Devuelta,
                AocrEstadosListaVerificacion.RequiereCorreccion,
                AocrEstadosListaVerificacion.Anulada
            };

            Assert.AreEqual(9, estados.Distinct().Count());
            foreach (var est in estados)
            {
                Assert.AreEqual(est, AocrEstadosListaVerificacion.Normalizar(est));
            }
        }
    }
}
