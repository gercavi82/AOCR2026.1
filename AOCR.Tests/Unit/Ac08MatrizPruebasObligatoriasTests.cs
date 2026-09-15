using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-08: MATRIZ DE 14 PRUEBAS OBLIGATORIAS
    /// "Impedir guardar, finalizar o firmar una Lista de Verificación cuando existan elementos sin resultado"
    /// 
    /// 1. LV completa se guarda.
    /// 2. Resultado faltante devuelve 400.
    /// 3. Resultado inválido devuelve 400.
    /// 4. Observación requerida faltante devuelve 400.
    /// 5. No se firma LV incompleta.
    /// 6. No se finaliza LV incompleta.
    /// 7. No se genera Informe Técnico con LV incompleta.
    /// 8. Manipulación del JavaScript no evita la validación.
    /// 9. Inspector no asignado recibe 403.
    /// 10. Administrador recibe 403.
    /// 11. Doble clic no duplica.
    /// 12. Todos los mensajes son comprensibles.
    /// 13. Pruebas negativas por cada campo obligatorio.
    /// 14. Persistencia confirmada después de recargar.
    /// </summary>
    [TestClass]
    public class Ac08MatrizPruebasObligatoriasTests
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

        private ListaVerificacionOperacionalEae CrearLvCompletaValida(int codigoLv = 10, int inspeccionId = 200, int solicitudId = 100, int? estacionId = 10)
        {
            var catalogo = new ListaVerificacionCatalogService().ObtenerCatalogoPreguntas();
            foreach (var item in catalogo.Where(i => !i.EsNotaOrientacion))
            {
                item.EstadoCumplimiento = "SATISFACTORIO";
                item.EstadoImplementacion = "IMPLEMENTADO";
                item.PruebasNotasComentarios = string.Empty;
            }

            return new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = codigoLv,
                CodigoInspeccion = inspeccionId,
                SolicitudId = solicitudId,
                EstacionId = estacionId,
                EstacionCodigo = "UIO",
                EstacionNombre = "Quito - Aeropuerto Mariscal Sucre",
                TipoLista = "EAE",
                Version = 1,
                Vigente = true,
                EstadoLista = AocrEstadosListaVerificacion.Borrador,
                NombreEae = "AEROVIAS DEL SUR S.A.",
                NumeroAocFechaValidez = "AOC-129-2026 / 31-12-2027",
                DireccionEstadoExplotador = "Av. de los Shyris y Naciones Unidas",
                DireccionEstadoReconocimiento = "Direccion General de Aviacion Civil del Ecuador",
                TiposAeronaves = "B737-800; A320-200",
                TipoOperacion = "Transporte aéreo comercial de pasajeros y carga",
                InspectorResponsable = "Inspector 1",
                CargoInspector = "Inspector de Operaciones",
                FechaLista = DateTime.Now,
                Items = catalogo,
                ItemsJson = ListaVerificacionCatalogService.SerializarRespuestas(catalogo),
                Finalizado = false,
                FirmadoTecnico = false
            };
        }

        #region Prueba 1: LV Completa se Guarda

        [TestMethod]
        public void Test01_LvCompletaSeGuarda()
        {
            // PRUEBA 1: LV completa se guarda exitosamente
            var lv = CrearLvCompletaValida();

            var validacion = ValidadorListaVerificacion.Evaluar(lv);
            Assert.IsTrue(validacion.EsValida, "La LV completa debe ser válida.");
            Assert.AreEqual(0, validacion.CantidadPendientes);
            Assert.AreEqual(0, validacion.ErroresCabecera.Count);

            var guardada = _service.GuardarRespuestas(lv, 501, "InspectorTecnico");
            Assert.IsNotNull(guardada);
            Assert.AreEqual(AocrEstadosListaVerificacion.Completa, guardada.EstadoLista);
            Assert.IsTrue(guardada.Vigente);
        }

        #endregion

        #region Prueba 2: Resultado Faltante Devuelve 400

        [TestMethod]
        public void Test02_ResultadoFaltanteDevuelve400()
        {
            // PRUEBA 2: Resultado faltante devuelve 400 con lista de pendientes
            var lv = CrearLvCompletaValida();
            var primerItem = lv.Items.First(i => !i.EsNotaOrientacion);
            primerItem.EstadoCumplimiento = ""; // Faltante

            var validacion = ValidadorListaVerificacion.Evaluar(lv);
            Assert.IsFalse(validacion.EsValida, "No debe ser válida si falta un resultado.");
            Assert.IsTrue(validacion.CantidadPendientes > 0);
            Assert.IsTrue(validacion.Pendientes.Any(p => p.Codigo == primerItem.Codigo));
            Assert.IsTrue(validacion.Mensaje.Contains(primerItem.Codigo));

            // El validador del servicio hidratado contra el catálogo del servidor detecta el faltante
            lv.ItemsJson = ListaVerificacionCatalogService.SerializarRespuestas(lv.Items);
            var validacionServicio = _service.EvaluarCompletitud(lv);
            Assert.IsFalse(validacionServicio.EsValida);
            Assert.IsTrue(validacionServicio.Pendientes.Any(p => p.Codigo == primerItem.Codigo));
        }

        #endregion

        #region Prueba 3: Resultado Inválido Devuelve 400

        [TestMethod]
        public void Test03_ResultadoInvalidoDevuelve400()
        {
            // PRUEBA 3: Resultado inválido (fuera del catálogo) devuelve 400
            var valoresInvalidos = new[] { "VALOR_INVENTADO", "ALEATORIO", "NO APLICA", "INSATISFACTORIO", "REGULAR" };

            foreach (var valor in valoresInvalidos)
            {
                var lv = CrearLvCompletaValida();
                var item = lv.Items.First(i => !i.EsNotaOrientacion);
                item.EstadoCumplimiento = valor;

                var validacion = ValidadorListaVerificacion.Evaluar(lv);
                Assert.IsFalse(validacion.EsValida, $"El valor '{valor}' no debe ser admitido.");
                Assert.IsTrue(validacion.Pendientes.Any(p => p.Codigo == item.Codigo));
                Assert.IsTrue(validacion.Pendientes.First(p => p.Codigo == item.Codigo).Errores
                    .Any(e => e.Contains("resultado de cumplimiento válido")));
            }
        }

        #endregion

        #region Prueba 4: Observación Requerida Faltante Devuelve 400

        [TestMethod]
        public void Test04_ObservacionRequeridaFaltanteDevuelve400()
        {
            // PRUEBA 4: Para incumplimientos debe exigirse observación cuando la regla funcional lo determine
            // Caso A: NO_SATISFACTORIO sin comentario en el requisito
            var lvA = CrearLvCompletaValida();
            var itemA = lvA.Items.First(i => !i.EsNotaOrientacion);
            itemA.EstadoCumplimiento = "NO_SATISFACTORIO";
            itemA.PruebasNotasComentarios = "   "; // Vacío

            var validacionA = ValidadorListaVerificacion.Evaluar(lvA);
            Assert.IsFalse(validacionA.EsValida);
            Assert.IsTrue(validacionA.Pendientes.Any(p => p.Codigo == itemA.Codigo && p.Errores.Any(e => e.Contains("Ingrese una observación"))));

            // Caso B: NO_IMPLEMENTADO sin comentario en la orientación
            var lvB = CrearLvCompletaValida();
            var itemB = lvB.Items.First(i => !i.EsNotaOrientacion);
            itemB.EstadoImplementacion = "NO_IMPLEMENTADO";
            itemB.PruebasNotasComentarios = "";

            var validacionB = ValidadorListaVerificacion.Evaluar(lvB);
            Assert.IsFalse(validacionB.EsValida);
            Assert.IsTrue(validacionB.Pendientes.Any(p => p.Codigo == itemB.Codigo && p.Errores.Any(e => e.Contains("Ingrese una observación"))));

            // Con la observación registrada, se subsana
            itemB.PruebasNotasComentarios = "Hallazgo debidamente sustentado en columna 14.";
            Assert.IsTrue(ValidadorListaVerificacion.Evaluar(lvB).EsValida);
        }

        #endregion

        #region Prueba 5: No se Firma LV Incompleta

        [TestMethod]
        public void Test05_NoSeFirmaLvIncompleta()
        {
            // PRUEBA 5: No permitir firmar si quedan resultados obligatorios pendientes
            var lv = CrearLvCompletaValida();
            lv.Finalizado = true;
            var item = lv.Items.First(i => !i.EsNotaOrientacion);
            item.EstadoCumplimiento = ""; // Incompleto
            _fakeDao.Almacen[lv.CodigoListaVerificacion] = lv;

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Inspector", "HASH123", "ruta.pdf", 501, "InspectorTecnico");
            });

            Assert.IsTrue(ex.Message.Contains("incompleto") || ex.Message.Contains("resultado de cumplimiento válido"));
            Assert.IsFalse(_fakeDao.Almacen[lv.CodigoListaVerificacion].FirmadoTecnico);
        }

        #endregion

        #region Prueba 6: No se Finaliza LV Incompleta

        [TestMethod]
        public void Test06_NoSeFinalizaLvIncompleta()
        {
            // PRUEBA 6: No permitir finalizar si quedan resultados obligatorios pendientes
            var lv = CrearLvCompletaValida();
            var item = lv.Items.First(i => !i.EsNotaOrientacion);
            item.EstadoImplementacion = ""; // Incompleto
            _fakeDao.Almacen[lv.CodigoListaVerificacion] = lv;

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 501, "InspectorTecnico");
            });

            Assert.IsTrue(ex.Message.Contains("incompleto") || ex.Message.Contains("resultado de implementación válido"));
            Assert.IsFalse(_fakeDao.Almacen[lv.CodigoListaVerificacion].Finalizado);
        }

        #endregion

        #region Prueba 7: No se Genera Informe Técnico con LV Incompleta

        [TestMethod]
        public void Test07_NoSeGeneraInformeTecnicoConLvIncompleta()
        {
            // PRUEBA 7: No permitir generar el Informe Técnico si quedan resultados o LVs pendientes de firmar
            var lv = CrearLvCompletaValida();
            lv.Finalizado = true;
            lv.FirmadoTecnico = false; // Aún sin firmar
            _fakeDao.Almacen[lv.CodigoListaVerificacion] = lv;

            List<string> pendientes;
            var puedeGenerar = _service.ValidarTodasLasListasFirmadasParaInforme(100, 200, out pendientes);

            Assert.IsFalse(puedeGenerar, "No debe permitir avanzar al Informe Técnico con LV no firmada.");
            Assert.IsNotNull(pendientes);
            Assert.IsTrue(pendientes.Count > 0);
        }

        #endregion

        #region Prueba 8: Manipulación de JavaScript no Evita la Validación

        [TestMethod]
        public void Test08_ManipulacionJavascriptNoEvitaValidacion()
        {
            // PRUEBA 8: La validación del servidor es obligatoria. Manipulación del cliente no elude la regla.
            var lv = CrearLvCompletaValida();
            var itemObligatorio = lv.Items.First(i => !i.EsNotaOrientacion);
            itemObligatorio.EstadoCumplimiento = ""; // Vacío

            // El cliente manipula el flag para fingir que es una nota no evaluable
            itemObligatorio.EsNotaOrientacion = true;

            // Al evaluar en el servidor mediante EvaluarCompletitud, se rehidrata desde el catálogo de confianza
            lv.ItemsJson = ListaVerificacionCatalogService.SerializarRespuestas(lv.Items);
            var resultadoServidor = _service.EvaluarCompletitud(lv);

            Assert.IsFalse(resultadoServidor.EsValida, "El servidor no debe confiar en EsNotaOrientacion manipulado por el cliente.");
            Assert.IsTrue(resultadoServidor.Pendientes.Any(p => p.Codigo == itemObligatorio.Codigo));
        }

        #endregion

        #region Prueba 9: Inspector no Asignado Recibe 403

        [TestMethod]
        public void Test09_InspectorNoAsignadoRecibe403()
        {
            // PRUEBA 9: Validar que el Inspector sea el asignado (Inspector no asignado recibe 403)
            var lv = CrearLvCompletaValida();
            _fakeDao.Almacen[lv.CodigoListaVerificacion] = lv;

            // Inspector 999 no está asignado a la inspección 200 (asignado es 501)
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.GuardarRespuestas(lv, 999, "InspectorTecnico");
            });

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 999, "InspectorTecnico");
            });

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Inspector 999", "HASH", "ruta.pdf", 999, "InspectorTecnico");
            });
        }

        #endregion

        #region Prueba 10: Administrador Recibe 403

        [TestMethod]
        public void Test10_AdministradorRecibe403()
        {
            // PRUEBA 10: Administrador no puede omitir validaciones ni editar/firmar la LV
            var lv = CrearLvCompletaValida();
            _fakeDao.Almacen[lv.CodigoListaVerificacion] = lv;

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.GuardarRespuestas(lv, 1, AocrRolesInstitucionales.Administrador);
            });

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FinalizarLista(lv.CodigoListaVerificacion, 1, AocrRolesInstitucionales.Administrador);
            });

            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                _service.FirmarLista(lv.CodigoListaVerificacion, "Admin", "HASH", "ruta.pdf", 1, AocrRolesInstitucionales.Administrador);
            });
        }

        #endregion

        #region Prueba 11: Doble Clic no Duplica

        [TestMethod]
        public void Test11_DobleClicNoDuplica()
        {
            // PRUEBA 11: Evitar doble envío (doble clic no duplica)
            var lv = CrearLvCompletaValida();

            var primera = _service.GuardarRespuestas(lv, 501, "InspectorTecnico");
            Assert.IsNotNull(primera);
            var idGenerado = primera.CodigoListaVerificacion;

            // Segundo guardado inmediato para la misma inspección y estación
            var segunda = _service.GuardarRespuestas(primera, 501, "InspectorTecnico");
            Assert.IsNotNull(segunda);
            Assert.AreEqual(idGenerado, segunda.CodigoListaVerificacion, "El guardado debe ser idempotente y actualizar la misma LV.");
            Assert.AreEqual(1, _fakeDao.Almacen.Count, "No deben crearse registros duplicados por doble envío.");
        }

        #endregion

        #region Prueba 12: Todos los Mensajes son Comprensibles

        [TestMethod]
        public void Test12_TodosLosMensajesSonComprensibles()
        {
            // PRUEBA 12: Todos los mensajes de validación son comprensibles, en español y orientados al usuario
            var lv = CrearLvCompletaValida();
            var item = lv.Items.First(i => !i.EsNotaOrientacion);
            item.EstadoCumplimiento = "";
            item.EstadoImplementacion = "";

            var validacion = ValidadorListaVerificacion.Evaluar(lv);
            Assert.IsFalse(validacion.EsValida);

            var mensaje = validacion.Mensaje;
            Assert.IsTrue(mensaje.Contains("incompleto"), "Debe indicar claramente que hay ítems incompletos.");
            Assert.IsTrue(mensaje.Contains("No puede completar, finalizar ni firmar la LV"), "Debe advertir la restricción funcional.");
            Assert.IsTrue(mensaje.Contains(item.Codigo), "Debe especificar los códigos pendientes.");

            var errorItem = validacion.Pendientes.First(p => p.Codigo == item.Codigo).Errores;
            Assert.IsTrue(errorItem.Any(e => e.Contains("Seleccione un resultado de cumplimiento válido")), "Mensaje de cumplimiento comprensible.");
            Assert.IsTrue(errorItem.Any(e => e.Contains("Seleccione un resultado de implementación válido")), "Mensaje de implementación comprensible.");
        }

        #endregion

        #region Prueba 13: Pruebas Negativas por Cada Campo Obligatorio

        [TestMethod]
        public void Test13_PruebasNegativasPorCadaCampoObligatorio()
        {
            // PRUEBA 13: Pruebas negativas por cada campo obligatorio de cabecera
            var campos = new Dictionary<string, Action<ListaVerificacionOperacionalEae>>
            {
                { "Nombre del EAE", x => x.NombreEae = " " },
                { "N AOC / Fecha de expedición / Validez", x => x.NumeroAocFechaValidez = "" },
                { "Dirección en el Estado del explotador", x => x.DireccionEstadoExplotador = null },
                { "Dirección en el Estado de reconocimiento", x => x.DireccionEstadoReconocimiento = "  " },
                { "Tipos de aeronaves", x => x.TiposAeronaves = "" },
                { "Tipo de operación", x => x.TipoOperacion = " " },
                { "Inspector responsable", x => x.InspectorResponsable = null }
            };

            foreach (var kvp in campos)
            {
                var lv = CrearLvCompletaValida();
                kvp.Value(lv);

                var validacion = ValidadorListaVerificacion.Evaluar(lv);
                Assert.IsFalse(validacion.EsValida, $"Debe fallar cuando falta el campo de cabecera: {kvp.Key}");
                Assert.IsTrue(validacion.ErroresCabecera.Any(e => e.Contains(kvp.Key)),
                    $"El mensaje de error debe mencionar específicamente el campo: {kvp.Key}");
            }
        }

        #endregion

        #region Prueba 14: Persistencia Confirmada Después de Recargar

        [TestMethod]
        public void Test14_PersistenciaConfirmadaDespuesDeRecargar()
        {
            // PRUEBA 14: Persistencia confirmada después de recargar
            var lv = CrearLvCompletaValida();
            var itemTest = lv.Items.First(i => !i.EsNotaOrientacion);
            itemTest.EstadoCumplimiento = "SATISFACTORIO";
            itemTest.EstadoImplementacion = "IMPLEMENTADO";
            itemTest.PruebasNotasComentarios = "Verificación técnica en rampa UIO.";

            var guardada = _service.GuardarRespuestas(lv, 501, "InspectorTecnico");
            Assert.IsNotNull(guardada);

            // Simular recarga desde persistencia (deserialización de JSON e hidratación de respuestas)
            var catalogo = new ListaVerificacionCatalogService();
            var itemsRecargados = catalogo.HidratarRespuestas(guardada.ItemsJson);

            var itemRecargado = itemsRecargados.FirstOrDefault(i => i.Codigo == itemTest.Codigo);
            Assert.IsNotNull(itemRecargado, "El ítem debe persistir al recargar.");
            Assert.AreEqual("SATISFACTORIO", itemRecargado.EstadoCumplimiento);
            Assert.AreEqual("IMPLEMENTADO", itemRecargado.EstadoImplementacion);
            Assert.AreEqual("Verificación técnica en rampa UIO.", itemRecargado.PruebasNotasComentarios);

            var lvRecargada = _fakeDao.ObtenerPorId(guardada.CodigoListaVerificacion);
            Assert.IsNotNull(lvRecargada);
            Assert.AreEqual(AocrEstadosListaVerificacion.Completa, lvRecargada.EstadoLista);
            Assert.AreEqual(10, lvRecargada.EstacionId);
            Assert.AreEqual(200, lvRecargada.CodigoInspeccion);
        }

        #endregion

        #region Fakes en Memoria para Pruebas Desacopladas

        private sealed class FakeListaVerificacionDao : ListaVerificacionOperacionalEaeDAO
        {
            public readonly Dictionary<int, ListaVerificacionOperacionalEae> Almacen = new Dictionary<int, ListaVerificacionOperacionalEae>();
            private int _secuencia = 1;

            public override ListaVerificacionOperacionalEae ObtenerPorId(int codigoListaVerificacion)
            {
                ListaVerificacionOperacionalEae lv;
                return Almacen.TryGetValue(codigoListaVerificacion, out lv) ? lv : null;
            }

            public override ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccion(int codigoInspeccion, int? estacionId = null)
            {
                return Almacen.Values.FirstOrDefault(x => x.CodigoInspeccion == codigoInspeccion && x.EstacionId == estacionId && x.Vigente);
            }

            public override ListaVerificacionOperacionalEae GuardarBorrador(ListaVerificacionOperacionalEae lista, int usuarioId)
            {
                if (lista.CodigoListaVerificacion <= 0)
                {
                    lista.CodigoListaVerificacion = _secuencia++;
                }
                Almacen[lista.CodigoListaVerificacion] = lista;
                return lista;
            }

            public override void MarcarFinalizada(int codigoListaVerificacion, string rutaPdf, string estadoLista, int usuarioId)
            {
                if (Almacen.ContainsKey(codigoListaVerificacion))
                {
                    Almacen[codigoListaVerificacion].Finalizado = true;
                    Almacen[codigoListaVerificacion].EstadoLista = estadoLista;
                    Almacen[codigoListaVerificacion].RutaPdf = rutaPdf;
                }
            }

            public override void MarcarFirmada(int codigoListaVerificacion, string rutaDocumentoFirmado, string hashDocumento, string usuarioFirma, DateTime fechaFirma, string estadoLista, int usuarioId)
            {
                if (Almacen.ContainsKey(codigoListaVerificacion))
                {
                    Almacen[codigoListaVerificacion].FirmadoTecnico = true;
                    Almacen[codigoListaVerificacion].Finalizado = true;
                    Almacen[codigoListaVerificacion].UsuarioFirma = usuarioFirma;
                    Almacen[codigoListaVerificacion].FechaFirma = fechaFirma;
                    Almacen[codigoListaVerificacion].HashDocumento = hashDocumento;
                    Almacen[codigoListaVerificacion].RutaDocumentoFirmado = rutaDocumentoFirmado;
                    Almacen[codigoListaVerificacion].EstadoLista = estadoLista;
                }
            }

            public override bool TodasLasListasEstacionesFirmadas(int solicitudId, int inspeccionId, out List<string> estacionesPendientes)
            {
                estacionesPendientes = new List<string>();
                var listas = Almacen.Values.Where(lv => lv.SolicitudId == solicitudId && lv.CodigoInspeccion == inspeccionId && lv.Vigente).ToList();
                if (listas.Count == 0)
                {
                    estacionesPendientes.Add("Sin LVs registradas");
                    return false;
                }
                foreach (var lv in listas)
                {
                    if (!lv.FirmadoTecnico)
                    {
                        estacionesPendientes.Add(lv.EstacionCodigo ?? lv.EstacionId?.ToString() ?? "General");
                    }
                }
                return estacionesPendientes.Count == 0;
            }
        }

        private sealed class FakeSolicitudEstacionDao : SolicitudEstacionDAO
        {
            public override List<SolicitudEstacionInspeccion> ListarPorSolicitud(int id)
            {
                return new List<SolicitudEstacionInspeccion>
                {
                    new SolicitudEstacionInspeccion { Id = 10, SolicitudId = id, InspeccionId = 200, EstacionCodigo = "UIO", EstacionNombre = "Quito", Activo = true }
                };
            }
        }

        private sealed class FakeInspeccionDao : InspeccionDAO
        {
            public override Inspeccion ObtenerPorId(int id)
            {
                return new Inspeccion
                {
                    CodigoInspeccion = id,
                    CodigoSolicitud = 100,
                    CodigoInspector = 501,
                    InspectorPrincipalNombre = "Inspector 1",
                    InspectorPrincipalCedula = "1710000001",
                    Estado = "PROGRAMADA"
                };
            }
        }

        private sealed class FakeSolicitudDao : SolicitudAOCRDAO
        {
            public override SolicitudAOCR ObtenerPorId(int id)
            {
                return new SolicitudAOCR
                {
                    CodigoSolicitud = id,
                    NombreOperador = "AEROVIAS DEL SUR S.A.",
                    RazonSocial = "AEROVIAS DEL SUR S.A.",
                    NumeroAOC = "AOC-129-2026",
                    Direccion = "Av. de los Shyris y Naciones Unidas",
                    Pais = "ECUADOR",
                    TipoOperacion = "TRANSPORTE AÉREO"
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
