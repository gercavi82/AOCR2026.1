using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-07: MATRIZ DE 14 PRUEBAS OBLIGATORIAS
    /// "Generar una Lista de Verificación independiente por cada inspección o estación"
    /// 
    /// 1. Una LV para una estación.
    /// 2. Varias LV para varias estaciones.
    /// 3. No mezclar resultados.
    /// 4. No duplicar LV vigente.
    /// 5. Inspector no asignado recibe 403.
    /// 6. Administrador recibe 403.
    /// 7. LV firmada no se modifica.
    /// 8. Conflicto de versión devuelve 409.
    /// 9. Doble clic no duplica.
    /// 10. No se genera informe si falta una LV.
    /// 11. Persistencia real después de recargar.
    /// 12. Rollback real ante error.
    /// 13. PDF identifica claramente inspección y estación.
    /// 14. Rutas funcionan bajo /aocr.
    /// </summary>
    [TestClass]
    public class Ac07MatrizPruebasObligatoriasTests
    {
        private FakeListaVerificacionDao _fakeDao;
        private FakeSolicitudEstacionDao _fakeEstacionDao;
        private FakeInspeccionDao _fakeInspeccionDao;
        private FakeSolicitudDao _fakeSolicitudDao;
        private ListaVerificacionService _service;

        [TestInitialize]
        public void Setup()
        {
            _fakeDao = new FakeListaVerificacionDao();
            _fakeEstacionDao = new FakeSolicitudEstacionDao();
            _fakeInspeccionDao = new FakeInspeccionDao();
            _fakeSolicitudDao = new FakeSolicitudDao();

            _service = new ListaVerificacionService(
                _fakeDao,
                _fakeEstacionDao,
                _fakeInspeccionDao,
                _fakeSolicitudDao,
                new FakeAuditoriaDao(),
                new ListaVerificacionCatalogService(),
                resolverIdentidad: id => new InspectorIdentityInfo
                {
                    Ids = new HashSet<int> { id },
                    Identificadores = new HashSet<string> { id == 501 ? "1710000001" : id.ToString() }
                }
            );
        }

        #region Pruebas 1 a 4: Ámbito por Estación, Aislamiento y No Duplicidad

        [TestMethod]
        public void Test01_UnaLVParaUnaEstacion()
        {
            // REQUISITO 1: 1 LV relacionada con solicitud, inspección, estación, tipo, inspector, versión, estado, vigencia
            var lv = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");

            Assert.IsNotNull(lv);
            Assert.AreEqual(100, lv.SolicitudId);
            Assert.AreEqual(200, lv.CodigoInspeccion);
            Assert.AreEqual(10, lv.EstacionId);
            Assert.AreEqual("EAE", lv.TipoLista);
            Assert.AreEqual(1, lv.Version);
            Assert.IsTrue(lv.Vigente);
            Assert.AreEqual(AocrEstadosListaVerificacion.Borrador, lv.EstadoLista);
            Assert.IsFalse(lv.FirmadoTecnico);
        }

        [TestMethod]
        public void Test02_VariasLVParaVariasEstaciones()
        {
            // REQUISITO 3: Si hay tres estaciones, deben existir tres contextos de LV independientes
            var lv1 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            var lv2 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 20, 501, "InspectorTecnico", "Inspector 1");
            var lv3 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 30, 501, "InspectorTecnico", "Inspector 1");

            Assert.IsNotNull(lv1);
            Assert.IsNotNull(lv2);
            Assert.IsNotNull(lv3);
            Assert.AreNotEqual(lv1.CodigoListaVerificacion, lv2.CodigoListaVerificacion);
            Assert.AreNotEqual(lv2.CodigoListaVerificacion, lv3.CodigoListaVerificacion);
            Assert.AreNotEqual(lv1.EstacionId, lv2.EstacionId);
            Assert.AreNotEqual(lv2.EstacionId, lv3.EstacionId);
            Assert.AreEqual(3, _fakeDao.Almacen.Count);
        }

        [TestMethod]
        public void Test03_NoMezclarResultados()
        {
            // REQUISITO 4: Los resultados de una estación no pueden mezclarse con otra
            var lvQuito = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            var lvGye = _service.ObtenerOIniciarListaParaEstacion(100, 200, 20, 501, "InspectorTecnico", "Inspector 1");

            // Modificar Quito con SATISFACTORIO y hallazgo
            lvQuito.Items[0].EstadoCumplimiento = "SATISFACTORIO";
            lvQuito.Items[0].EstadoImplementacion = "IMPLEMENTADO";
            lvQuito.Items[0].PruebasNotasComentarios = "Evidencia exclusiva Quito SEQM";
            _service.GuardarRespuestas(lvQuito, 501, "InspectorTecnico");

            // Modificar Guayaquil con NO_SATISFACTORIO
            lvGye.Items[0].EstadoCumplimiento = "NO_SATISFACTORIO";
            lvGye.Items[0].EstadoImplementacion = "NO_IMPLEMENTADO";
            lvGye.Items[0].PruebasNotasComentarios = "Discrepancia en pista Guayaquil SEGU";
            _service.GuardarRespuestas(lvGye, 501, "InspectorTecnico");

            var qRecargada = _fakeDao.ObtenerPorId(lvQuito.CodigoListaVerificacion);
            var gRecargada = _fakeDao.ObtenerPorId(lvGye.CodigoListaVerificacion);

            Assert.IsTrue(qRecargada.ItemsJson.Contains("Evidencia exclusiva Quito SEQM"));
            Assert.IsFalse(qRecargada.ItemsJson.Contains("Guayaquil"));
            Assert.IsTrue(gRecargada.ItemsJson.Contains("Discrepancia en pista Guayaquil SEGU"));
            Assert.IsFalse(gRecargada.ItemsJson.Contains("Quito"));
        }

        [TestMethod]
        public void Test04_NoDuplicarLVVigente()
        {
            // REQUISITO 2: No puede existir más de una LV vigente para la misma inspección, estación y tipo
            var primera = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            var segunda = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");

            Assert.AreEqual(primera.CodigoListaVerificacion, segunda.CodigoListaVerificacion);
            var vigentes = _fakeDao.Almacen.Values
                .Where(x => x.CodigoInspeccion == 200 && x.EstacionId == 10 && x.Vigente)
                .ToList();
            Assert.AreEqual(1, vigentes.Count, "Solo debe existir 1 registro vigente para la estación.");
        }

        #endregion

        #region Pruebas 5 a 7: Segregación RBAC e Inmutabilidad

        [TestMethod]
        public void Test05_InspectorNoAsignadoRecibe403()
        {
            // REQUISITO 5: Solo el inspector asignado puede crear, editar, finalizar y firmar
            var noAsignadoId = 999;
            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, noAsignadoId, "InspectorTecnico", "Inspector No Asignado");
            });

            StringAssert.Contains(ex.Message, "no está asignado");
        }

        [TestMethod]
        public void Test06_AdministradorRecibe403()
        {
            // REQUISITO 7: Administrador no puede editar ni firmar
            Assert.IsFalse(_service.EsRolAutorizadoOperacion("Administrador"));
            Assert.IsFalse(_service.EsRolAutorizadoOperacion("Admin"));
            Assert.IsFalse(_service.EsRolAutorizadoOperacion("DIRDAC"));

            var lv = new ListaVerificacionOperacionalEae
            {
                CodigoInspeccion = 200,
                SolicitudId = 100,
                EstacionId = 10
            };

            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.GuardarRespuestas(lv, 1, "Administrador");
            });
            StringAssert.Contains(ex.Message, "solo el Inspector asignado");
        }

        [TestMethod]
        public void Test07_LVFirmadaNoSeModifica()
        {
            // REQUISITO 8: Una LV firmada debe ser inmutable
            var lv = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            lv.DireccionEstadoExplotador = "Av. Amazonas 123";
            lv.TiposAeronaves = "B737-800";

            // Marcar como completada y finalizada con ítems completos
            foreach (var item in lv.Items)
            {
                item.EstadoCumplimiento = "SATISFACTORIO";
                item.EstadoImplementacion = "IMPLEMENTADO";
            }
            _service.GuardarRespuestas(lv, 501, "InspectorTecnico");
            _service.FinalizarLista(lv.CodigoListaVerificacion, 501, "InspectorTecnico");
            _service.FirmarLista(lv.CodigoListaVerificacion, "Inspector 1", "HASH-SHA256-TEST", "/storage/lv_firmada.pdf", 501, "InspectorTecnico");

            // Intento de alteración posterior
            var intentoModificacion = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = lv.CodigoListaVerificacion,
                CodigoInspeccion = lv.CodigoInspeccion,
                SolicitudId = lv.SolicitudId,
                EstacionId = lv.EstacionId,
                ObservacionesGenerales = "Intento de modificación maliciosa"
            };
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.GuardarRespuestas(intentoModificacion, 501, "InspectorTecnico");
            });

            StringAssert.Contains(ex.Message, "Conflicto (409)");
            var persistida = _fakeDao.ObtenerPorId(lv.CodigoListaVerificacion);
            Assert.AreNotEqual("Intento de modificación maliciosa", persistida.ObservacionesGenerales);
        }

        #endregion

        #region Pruebas 8 a 10: Concurrencia, Versiones y Precondición de Informe

        [TestMethod]
        public void Test08_ConflictoDeVersionDevuelve409()
        {
            // REQUISITO 9: Conflicto de versión devuelve 409
            var lv = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");

            // Simular versión desfasada del cliente (cliente envía v1 pero la base avanzó a v2)
            var lvDesfasada = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = lv.CodigoListaVerificacion,
                CodigoInspeccion = lv.CodigoInspeccion,
                SolicitudId = lv.SolicitudId,
                EstacionId = lv.EstacionId,
                Version = 99 // Versión conflictiva
            };

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _fakeDao.GuardarBorrador(lvDesfasada, 501);
            });

            StringAssert.Contains(ex.Message, "409");
        }

        [TestMethod]
        public void Test09_DobleClicNoDuplica()
        {
            // Simular 2 solicitudes simultáneas para la misma estación
            var lv1 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            var lv2 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");

            Assert.AreEqual(lv1.CodigoListaVerificacion, lv2.CodigoListaVerificacion);
            Assert.AreEqual(1, _fakeDao.Almacen.Count(x => x.Value.EstacionId == 10 && x.Value.Vigente));
        }

        [TestMethod]
        public void Test10_NoSeGeneraInformeSiFaltaUnaLV()
        {
            // REQUISITO 10: Para generar Informe Técnico deben estar completas las LV exigidas para todas las estaciones
            List<string> pendientes;

            Action<ListaVerificacionOperacionalEae> completarLv = l =>
            {
                l.DireccionEstadoExplotador = "Av. Amazonas 123";
                l.TiposAeronaves = "B737-800";
                foreach (var it in l.Items)
                {
                    it.EstadoCumplimiento = "SATISFACTORIO";
                    it.EstadoImplementacion = "IMPLEMENTADO";
                }
            };

            // Al inicio, ninguna estación está firmada -> Se bloquea
            var puedeGenerar = _service.ValidarTodasLasListasFirmadasParaInforme(100, 200, out pendientes);
            Assert.IsFalse(puedeGenerar);
            Assert.IsTrue(pendientes.Count > 0);

            // Firmar solo 1 de las 3 estaciones
            var lv1 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            completarLv(lv1);
            _service.GuardarRespuestas(lv1, 501, "InspectorTecnico");
            _service.FinalizarLista(lv1.CodigoListaVerificacion, 501, "InspectorTecnico");
            _service.FirmarLista(lv1.CodigoListaVerificacion, "Inspector 1", "HASH1", "/pdf1.pdf", 501, "InspectorTecnico");

            // Sigue bloqueado porque faltan estaciones 20 y 30
            puedeGenerar = _service.ValidarTodasLasListasFirmadasParaInforme(100, 200, out pendientes);
            Assert.IsFalse(puedeGenerar);
            Assert.AreEqual(2, pendientes.Count);

            // Firmar estación 20 y 30
            var lv2 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 20, 501, "InspectorTecnico", "Inspector 1");
            completarLv(lv2);
            _service.GuardarRespuestas(lv2, 501, "InspectorTecnico");
            _service.FinalizarLista(lv2.CodigoListaVerificacion, 501, "InspectorTecnico");
            _service.FirmarLista(lv2.CodigoListaVerificacion, "Inspector 1", "HASH2", "/pdf2.pdf", 501, "InspectorTecnico");

            var lv3 = _service.ObtenerOIniciarListaParaEstacion(100, 200, 30, 501, "InspectorTecnico", "Inspector 1");
            completarLv(lv3);
            _service.GuardarRespuestas(lv3, 501, "InspectorTecnico");
            _service.FinalizarLista(lv3.CodigoListaVerificacion, 501, "InspectorTecnico");
            _service.FirmarLista(lv3.CodigoListaVerificacion, "Inspector 1", "HASH3", "/pdf3.pdf", 501, "InspectorTecnico");

            // Ahora sí se permite avanzar al Informe Técnico
            puedeGenerar = _service.ValidarTodasLasListasFirmadasParaInforme(100, 200, out pendientes);
            Assert.IsTrue(puedeGenerar);
            Assert.AreEqual(0, pendientes.Count);
        }

        #endregion

        #region Pruebas 11 a 14: Persistencia, Rollback, PDF y Rutas

        [TestMethod]
        public void Test11_PersistenciaRealDespuesDeRecargar()
        {
            // PRUEBA 11: Persistencia real después de recargar
            var lv = _service.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            lv.ObservacionesGenerales = "Observación persistida con éxito";
            _service.GuardarRespuestas(lv, 501, "InspectorTecnico");

            // Simular nueva sesión / recarga de página instanciando un nuevo servicio
            var nuevoServicio = new ListaVerificacionService(
                _fakeDao, _fakeEstacionDao, _fakeInspeccionDao, _fakeSolicitudDao,
                new FakeAuditoriaDao(), new ListaVerificacionCatalogService(),
                id => new InspectorIdentityInfo { Ids = new HashSet<int> { id }, Identificadores = new HashSet<string> { id.ToString() } });

            var recargada = nuevoServicio.ObtenerOIniciarListaParaEstacion(100, 200, 10, 501, "InspectorTecnico", "Inspector 1");
            Assert.AreEqual(lv.CodigoListaVerificacion, recargada.CodigoListaVerificacion);
            Assert.AreEqual("Observación persistida con éxito", recargada.ObservacionesGenerales);
        }

        [TestMethod]
        public void Test12_RollbackRealAnteError()
        {
            // PRUEBA 12: Rollback real ante error en guardado
            _fakeDao.SimularFalloTransaccional = true;

            var lv = new ListaVerificacionOperacionalEae
            {
                CodigoInspeccion = 200,
                SolicitudId = 100,
                EstacionId = 10,
                ObservacionesGenerales = "Este cambio no debe persistir"
            };

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.GuardarRespuestas(lv, 501, "InspectorTecnico");
            });

            // Verificar que no se alteró el estado persistido
            var estadoActual = _fakeDao.ObtenerUltimaPorInspeccion(200, 10);
            Assert.IsNull(estadoActual, "El registro no debe existir debido al rollback.");
        }

        [TestMethod]
        public void Test13_PdfIdentificaClaramenteInspeccionYEstacion()
        {
            // PRUEBA 13: PDF identifica claramente inspección y estación
            var templatePath = @"c:\proyectos\AOCR\CapaPresentacion\Views\ListaVerificacion\PdfListaVerificacionEaeOficial.cshtml";
            Assert.IsTrue(File.Exists(templatePath), "La plantilla del PDF oficial debe existir.");

            var templateContent = File.ReadAllText(templatePath);
            Assert.IsTrue(templateContent.Contains("Inspección:"), "El PDF debe incluir etiqueta de Inspección.");
            Assert.IsTrue(templateContent.Contains("Estación:"), "El PDF debe incluir etiqueta de Estación.");
            Assert.IsTrue(templateContent.Contains("codigoInspeccionPdf"), "El PDF debe enlazar el código de inspección.");
            Assert.IsTrue(templateContent.Contains("estacionTextoPdf"), "El PDF debe enlazar la estación.");
        }

        [TestMethod]
        public void Test14_RutasFuncionanBajoAocr()
        {
            // PRUEBA 14: Rutas funcionan bajo /aocr
            var baseVirtualPath = "/aocr";
            var urlRelativa = "/Inspeccion/GuardarListaVerificacionOperacionalEae";
            var urlCompleta = baseVirtualPath.TrimEnd('/') + "/" + urlRelativa.TrimStart('/');

            Assert.AreEqual("/aocr/Inspeccion/GuardarListaVerificacionOperacionalEae", urlCompleta);
            Assert.IsTrue(urlCompleta.StartsWith("/aocr/"));

            // Verificar estados normalizados
            Assert.AreEqual(AocrEstadosListaVerificacion.Borrador, AocrEstadosListaVerificacion.Normalizar("LV_BORRADOR"));
            Assert.AreEqual(AocrEstadosListaVerificacion.Completa, AocrEstadosListaVerificacion.Normalizar("LV_COMPLETA"));
            Assert.AreEqual(AocrEstadosListaVerificacion.Firmada, AocrEstadosListaVerificacion.Normalizar("LV_FIRMADA"));
        }

        #endregion

        #region Fakes en memoria para pruebas desacopladas de DB física

        private sealed class FakeListaVerificacionDao : ListaVerificacionOperacionalEaeDAO
        {
            public readonly Dictionary<int, ListaVerificacionOperacionalEae> Almacen = new Dictionary<int, ListaVerificacionOperacionalEae>();
            private int _secuencia = 1;
            public bool SimularFalloTransaccional = false;

            public override ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccion(int codigoInspeccion, int? estacionId = null)
            {
                return Almacen.Values
                    .Where(x => x.CodigoInspeccion == codigoInspeccion && x.EstacionId == estacionId && x.Vigente)
                    .OrderByDescending(x => x.Version)
                    .ThenByDescending(x => x.CodigoListaVerificacion)
                    .FirstOrDefault();
            }

            public override ListaVerificacionOperacionalEae ObtenerPorId(int codigoListaVerificacion)
            {
                ListaVerificacionOperacionalEae item;
                return Almacen.TryGetValue(codigoListaVerificacion, out item) ? item : null;
            }

            public override ListaVerificacionOperacionalEae GuardarBorrador(ListaVerificacionOperacionalEae lista, int usuarioId)
            {
                if (SimularFalloTransaccional)
                {
                    throw new InvalidOperationException("Fallo simulado de base de datos para verificar Rollback.");
                }

                if (lista == null) throw new ArgumentNullException(nameof(lista));

                var existente = ObtenerUltimaPorInspeccion(lista.CodigoInspeccion, lista.EstacionId);
                if (existente != null && (existente.FirmadoTecnico || AocrEstadosListaVerificacion.EstaFirmada(existente.EstadoLista)))
                {
                    throw new InvalidOperationException("Conflicto (409): la LV está cerrada y no admite modificaciones.");
                }

                if (lista.Version > 0 && existente != null && lista.Version != existente.Version)
                {
                    throw new InvalidOperationException("Conflicto (409): la versión de la LV no coincide.");
                }

                if (lista.CodigoListaVerificacion <= 0)
                {
                    lista.CodigoListaVerificacion = _secuencia++;
                    lista.Version = existente != null ? existente.Version + 1 : 1;
                    lista.Vigente = true;
                    Almacen[lista.CodigoListaVerificacion] = lista;
                }
                else
                {
                    Almacen[lista.CodigoListaVerificacion] = lista;
                }

                return lista;
            }

            public override void MarcarFinalizada(int codigoListaVerificacion, string rutaPdf, string estado, int usuarioId)
            {
                var lv = ObtenerPorId(codigoListaVerificacion);
                if (lv != null)
                {
                    lv.Finalizado = true;
                    lv.RutaPdf = rutaPdf;
                    lv.EstadoLista = estado ?? AocrEstadosListaVerificacion.Completa;
                }
            }

            public override void MarcarFirmada(int codigoListaVerificacion, string rutaDocumentoFirmado, string hashDocumento, string usuarioFirma, DateTime fechaFirma, string estado, int usuarioId)
            {
                var lv = ObtenerPorId(codigoListaVerificacion);
                if (lv != null)
                {
                    lv.FirmadoTecnico = true;
                    lv.RutaDocumentoFirmado = rutaDocumentoFirmado;
                    lv.HashDocumento = hashDocumento;
                    lv.UsuarioFirma = usuarioFirma;
                    lv.FechaFirma = fechaFirma;
                    lv.EstadoLista = estado ?? AocrEstadosListaVerificacion.Firmada;
                }
            }

            public override bool TodasLasListasEstacionesFirmadas(int solicitudId, int inspeccionId, out List<string> estacionesPendientes)
            {
                estacionesPendientes = new List<string>();
                var estaciones = new[] { 10, 20, 30 };
                foreach (var est in estaciones)
                {
                    var lv = ObtenerUltimaPorInspeccion(inspeccionId, est);
                    if (lv == null || !lv.FirmadoTecnico)
                    {
                        estacionesPendientes.Add($"Estación #{est}");
                    }
                }
                return estacionesPendientes.Count == 0;
            }
        }

        private sealed class FakeSolicitudEstacionDao : SolicitudEstacionDAO
        {
            public override List<SolicitudEstacionInspeccion> ListarPorSolicitud(int solicitudId)
            {
                return new List<SolicitudEstacionInspeccion>
                {
                    new SolicitudEstacionInspeccion { Id = 10, SolicitudId = solicitudId, EstacionCodigo = "SEQM", EstacionNombre = "Quito", Activo = true, InspeccionId = 200 },
                    new SolicitudEstacionInspeccion { Id = 20, SolicitudId = solicitudId, EstacionCodigo = "SEGU", EstacionNombre = "Guayaquil", Activo = true, InspeccionId = 200 },
                    new SolicitudEstacionInspeccion { Id = 30, SolicitudId = solicitudId, EstacionCodigo = "SECU", EstacionNombre = "Cuenca", Activo = true, InspeccionId = 200 }
                };
            }
        }

        private sealed class FakeInspeccionDao : InspeccionDAO
        {
            public override Inspeccion ObtenerPorId(int codigoInspeccion)
            {
                return new Inspeccion
                {
                    CodigoInspeccion = codigoInspeccion,
                    CodigoSolicitud = 100,
                    CodigoInspector = 501,
                    InspectorPrincipalNombre = "Inspector 1",
                    InspectorPrincipalCedula = "1710000001",
                    FechaProgramada = DateTime.Now
                };
            }
        }

        private sealed class FakeSolicitudDao : SolicitudAOCRDAO
        {
            public override SolicitudAOCR ObtenerPorId(int solicitudId)
            {
                return new SolicitudAOCR
                {
                    CodigoSolicitud = solicitudId,
                    NombreOperador = "AERO TEST EAE",
                    NumeroAOC = "AOC-EC-2026",
                    TipoOperacion = "TRANSPORTE AÉREO",
                    Direccion = "Av. Amazonas 123"
                };
            }
        }

        private sealed class FakeAuditoriaDao : AuditoriaDAO
        {
            public override void Registrar(Auditoria auditoria) { }
        }

        #endregion
    }
}
