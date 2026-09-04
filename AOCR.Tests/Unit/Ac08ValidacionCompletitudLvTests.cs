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
    public class Ac08ValidacionCompletitudLvTests
    {
        private FakeListaVerificacionDAO _fakeDao;
        private ListaVerificacionService _service;

        [TestInitialize]
        public void Setup()
        {
            _fakeDao = new FakeListaVerificacionDAO();
            _service = new ListaVerificacionService(_fakeDao, null, null, null);
        }

        private ListaVerificacionOperacionalEae CrearLvValidaCompleta()
        {
            return new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = 10,
                CodigoInspeccion = 100,
                SolicitudId = 200,
                EstacionId = 300,
                NombreEae = "AEROVIAS DEL SUR S.A.",
                NumeroAocFechaValidez = "AOC-129-2026 / 31-12-2027",
                DireccionEstadoExplotador = "Av. de los Shyris y Naciones Unidas",
                DireccionEstadoReconocimiento = "Direccion General de Aviacion Civil del Ecuador",
                TiposAeronaves = "B737-800; A320-200",
                TipoOperacion = "Transporte aéreo comercial de pasajeros y carga",
                InspectorResponsable = "Juan Inspector",
                CargoInspector = "Inspector de Operaciones",
                FechaLista = DateTime.Now,
                Items = new List<ListaVerificacionOperacionalEaeItem>
                {
                    new ListaVerificacionOperacionalEaeItem
                    {
                        Codigo = "129-1",
                        CodigoPregunta = "129-1",
                        PreguntaRequisito = "Requisito 1",
                        OrientacionEvidencia = "Orientación 1",
                        EstadoCumplimiento = "SATISFACTORIO",
                        EstadoImplementacion = "IMPLEMENTADO",
                        PruebasNotasComentarios = "Verificado en manual de operaciones",
                        EsNotaOrientacion = false
                    },
                    new ListaVerificacionOperacionalEaeItem
                    {
                        Codigo = "129-2",
                        CodigoPregunta = "129-2",
                        PreguntaRequisito = "Requisito 2",
                        OrientacionEvidencia = "Orientación 2",
                        EstadoCumplimiento = "SATISFACTORIO",
                        EstadoImplementacion = "IMPLEMENTADO",
                        PruebasNotasComentarios = "Documentación completa",
                        EsNotaOrientacion = false
                    }
                }
            };
        }

        [TestMethod]
        public void Test01_LvSinItems_NoEsCompleta()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items = new List<ListaVerificacionOperacionalEaeItem>();

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("no contiene ítems configurados")));
        }

        [TestMethod]
        public void Test02_LvConCabeceraIncompleta_NoEsCompleta()
        {
            var lv = CrearLvValidaCompleta();
            lv.NombreEae = "   ";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("Complete el campo de cabecera")));
        }

        [TestMethod]
        public void Test03_LvConTodosLosCamposYItemsCompletos_EsCompleta()
        {
            var lv = CrearLvValidaCompleta();

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsTrue(resultado);
            Assert.IsTrue(lv.EstaCompleta());
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public void Test04_ItemSinEstadoCumplimiento_NiObservacion_NoEsCompleto()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "";
            lv.Items[0].PruebasNotasComentarios = "";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("Debe seleccionar el estado de cumplimiento")));
        }

        [TestMethod]
        public void Test05_ItemSinEstadoImplementacion_NiObservacion_NoEsCompleto()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoImplementacion = "";
            lv.Items[0].PruebasNotasComentarios = "";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("Debe seleccionar el estado de cumplimiento/implementación")));
        }

        [TestMethod]
        public void Test06_ItemSinEstados_PeroConObservacion_EsCompleto()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "";
            lv.Items[0].EstadoImplementacion = "";
            lv.Items[0].PruebasNotasComentarios = "Observación de campo registrada en la columna 14";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsTrue(resultado);
            Assert.IsTrue(lv.EstaCompleta());
        }

        [TestMethod]
        public void Test07_ItemNoSatisfactorio_SinComentario_NoEsCompleto()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "NO_SATISFACTORIO";
            lv.Items[0].PruebasNotasComentarios = "";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("Ingrese una observación en Pruebas / Notas / Comentarios")));
        }

        [TestMethod]
        public void Test08_ItemNoImplementado_SinComentario_NoEsCompleto()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoImplementacion = "NO_IMPLEMENTADO";
            lv.Items[0].PruebasNotasComentarios = "";

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsFalse(resultado);
            Assert.IsFalse(lv.EstaCompleta());
            Assert.IsTrue(errores.Any(e => e.Contains("Ingrese una observación en Pruebas / Notas / Comentarios")));
        }

        [TestMethod]
        public void Test09_NotaDeOrientacion_SeExcluyeDeValidacion()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items.Add(new ListaVerificacionOperacionalEaeItem
            {
                Codigo = "129-Nota",
                CodigoPregunta = "129-1",
                PreguntaRequisito = "Nota de guía",
                OrientacionEvidencia = "Texto informativo de orientación",
                EstadoCumplimiento = "",
                EstadoImplementacion = "",
                PruebasNotasComentarios = "",
                EsNotaOrientacion = true
            });

            List<string> errores;
            var resultado = lv.ValidarCompletitud(out errores);

            Assert.IsTrue(resultado);
            Assert.IsTrue(lv.EstaCompleta());
        }

        [TestMethod]
        public void Test10_GuardarRespuestas_Incompleta_EstadoEnProceso()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "";
            lv.Items[0].PruebasNotasComentarios = "";

            var guardada = _service.GuardarRespuestas(lv, 99, AocrRolesInstitucionales.Inspector);

            Assert.IsNotNull(guardada);
            Assert.AreEqual(AocrEstadosListaVerificacion.EnProceso, guardada.EstadoLista);
        }

        [TestMethod]
        public void Test11_GuardarRespuestas_Completa_EstadoCompleta()
        {
            var lv = CrearLvValidaCompleta();

            var guardada = _service.GuardarRespuestas(lv, 99, AocrRolesInstitucionales.Inspector);

            Assert.IsNotNull(guardada);
            Assert.AreEqual(AocrEstadosListaVerificacion.Completa, guardada.EstadoLista);
        }

        [TestMethod]
        public void Test12_FinalizarLista_Incompleta_LanzaExcepcion()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "";
            lv.Items[0].PruebasNotasComentarios = "";
            _fakeDao.Store[lv.CodigoListaVerificacion] = lv;

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 99, AocrRolesInstitucionales.Inspector);
            });

            Assert.IsTrue(ex.Message.Contains("Debe seleccionar el estado de cumplimiento") || ex.Message.Contains("incomplet"));
        }

        [TestMethod]
        public void Test13_FirmarLista_Incompleta_LanzaExcepcion()
        {
            var lv = CrearLvValidaCompleta();
            lv.Items[0].EstadoCumplimiento = "";
            lv.Items[0].PruebasNotasComentarios = "";
            lv.Finalizado = true;
            _fakeDao.Store[lv.CodigoListaVerificacion] = lv;

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Inspector", "HASH123", "ruta.pdf", 99, AocrRolesInstitucionales.Inspector);
            });

            Assert.IsTrue(ex.Message.Contains("Debe seleccionar el estado de cumplimiento") || ex.Message.Contains("incomplet"));
        }

        [TestMethod]
        public void Test14_RolNoAutorizado_NoPuedeFinalizarNiFirmar()
        {
            var lv = CrearLvValidaCompleta();
            lv.Finalizado = false;
            _fakeDao.Store[lv.CodigoListaVerificacion] = lv;

            // Admin no puede finalizar
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 1, AocrRolesInstitucionales.Administrador);
            });

            // DIRDAC no puede firmar
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "DIRDAC", "HASH", "ruta", 2, AocrRolesInstitucionales.Dirdac);
            });

            // Coordinador no puede firmar
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Coord", "HASH", "ruta", 3, AocrRolesInstitucionales.Coordinador);
            });

            // DIRCAV no puede firmar
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Dircav", "HASH", "ruta", 4, AocrRolesInstitucionales.Dircav);
            });
        }

        [TestMethod]
        public void Test15_LvYaFirmada_EsInmutable()
        {
            var lv = CrearLvValidaCompleta();
            lv.Finalizado = true;
            lv.FirmadoTecnico = true;
            _fakeDao.Store[lv.CodigoListaVerificacion] = lv;

            // Finalizar sobre LV firmada arroja InvalidOperationException
            var ex1 = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 99, AocrRolesInstitucionales.Inspector);
            });
            Assert.IsTrue(ex1.Message.Contains("ya está firmada") || ex1.Message.Contains("inmutable"));

            // Firmar sobre LV firmada arroja InvalidOperationException
            var ex2 = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Inspector", "HASH", "ruta", 99, AocrRolesInstitucionales.Inspector);
            });
            Assert.IsTrue(ex2.Message.Contains("ya se encuentra firmada") || ex2.Message.Contains("inmutable"));
        }

        private class FakeListaVerificacionDAO : ListaVerificacionOperacionalEaeDAO
        {
            public readonly Dictionary<int, ListaVerificacionOperacionalEae> Store = new Dictionary<int, ListaVerificacionOperacionalEae>();

            public override ListaVerificacionOperacionalEae ObtenerPorId(int codigoListaVerificacion)
            {
                ListaVerificacionOperacionalEae lv;
                return Store.TryGetValue(codigoListaVerificacion, out lv) ? lv : null;
            }

            public override ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccion(int codigoInspeccion, int? estacionId = null)
            {
                return Store.Values.FirstOrDefault(x => x.CodigoInspeccion == codigoInspeccion && x.EstacionId == estacionId);
            }

            public override ListaVerificacionOperacionalEae GuardarBorrador(ListaVerificacionOperacionalEae lista, int usuarioId)
            {
                if (lista.CodigoListaVerificacion <= 0)
                {
                    lista.CodigoListaVerificacion = Store.Count + 1;
                }
                Store[lista.CodigoListaVerificacion] = lista;
                return lista;
            }

            public override void MarcarFinalizada(int codigoListaVerificacion, string rutaPdf, string estadoLista, int usuarioId)
            {
                if (Store.ContainsKey(codigoListaVerificacion))
                {
                    Store[codigoListaVerificacion].Finalizado = true;
                    Store[codigoListaVerificacion].EstadoLista = estadoLista;
                    Store[codigoListaVerificacion].RutaPdf = rutaPdf;
                }
            }

            public override void MarcarFirmada(int codigoListaVerificacion, string rutaDocumentoFirmado, string hashDocumento, string usuarioFirma, DateTime fechaFirma, string estadoLista, int usuarioId)
            {
                if (Store.ContainsKey(codigoListaVerificacion))
                {
                    Store[codigoListaVerificacion].FirmadoTecnico = true;
                    Store[codigoListaVerificacion].Finalizado = true;
                    Store[codigoListaVerificacion].UsuarioFirma = usuarioFirma;
                    Store[codigoListaVerificacion].FechaFirma = fechaFirma;
                    Store[codigoListaVerificacion].HashDocumento = hashDocumento;
                    Store[codigoListaVerificacion].RutaDocumentoFirmado = rutaDocumentoFirmado;
                    Store[codigoListaVerificacion].EstadoLista = estadoLista;
                }
            }
        }
    }
}
