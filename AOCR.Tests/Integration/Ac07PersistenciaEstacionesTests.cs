using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Ac07PersistenciaEstacionesTests
    {
        private string _cs;
        private ListaVerificacionOperacionalEaeDAO Dao() => new ListaVerificacionOperacionalEaeDAO(_cs);
        private ListaVerificacionService Servicio() => new ListaVerificacionService(
            Dao(), new Estaciones(), new Inspecciones(), new Solicitudes(), new AuditoriaPrueba(), resolverIdentidad: id =>
                new InspectorIdentityInfo { Ids = new HashSet<int> { id }, Identificadores = new HashSet<string> { id.ToString() } });

        private sealed class AuditoriaPrueba : AuditoriaDAO
        {
            public override void Registrar(Auditoria log) { }
        }
        private sealed class Solicitudes : SolicitudAOCRDAO
        {
            public override SolicitudAOCR ObtenerPorId(int id) => new SolicitudAOCR { CodigoSolicitud = id };
        }

        private sealed class Inspecciones : InspeccionDAO
        {
            public override Inspeccion ObtenerPorId(int id) => new Inspeccion
            { CodigoInspeccion = id, CodigoSolicitud = 700, CodigoInspector = 900, InspectorApoyoCedula = "901" };
        }
        private sealed class Estaciones : SolicitudEstacionDAO
        {
            public override List<SolicitudEstacionInspeccion> ListarPorSolicitud(int id) =>
                Enumerable.Range(1, 3).Select(e => new SolicitudEstacionInspeccion
                { Id = e, SolicitudId = 700, Activo = true, InspectorId = e == 3 ? 901 : 900 }).ToList();
        }

        [TestInitialize]
        public void Preparar()
        {
            _cs = Environment.GetEnvironmentVariable("AOCR_AC07_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(_cs)) Assert.Inconclusive("Ejecute scripts/test_ac07.py: requiere PostgreSQL aislado.");
            var builder = new NpgsqlConnectionStringBuilder(_cs);
            Assert.IsTrue(builder.Database.StartsWith("aocr_ac07_test_"), "Solo se permiten bases desechables AC07.");
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand("DELETE FROM public.aocr_tblv_operacional_eae;", cn)) cmd.ExecuteNonQuery();
            }
        }

        private ListaVerificacionOperacionalEae Nueva(int estacion, bool completa = false)
        {
            var items = new ListaVerificacionCatalogService().ObtenerCatalogoPreguntas();
            foreach (var item in completa ? items : items.Take(1))
            {
                item.EstadoCumplimiento = "SATISFACTORIO";
                item.EstadoImplementacion = "IMPLEMENTADO";
                item.PruebasNotasComentarios = "Observación estación " + estacion;
            }
            return new ListaVerificacionOperacionalEae
            {
                CodigoInspeccion = 800, SolicitudId = 700, EstacionId = estacion,
                NombreEae = "Operador de prueba", NumeroAocFechaValidez = "AOC prueba",
                DireccionEstadoExplotador = "Dirección prueba", DireccionEstadoReconocimiento = "Dirección local",
                TiposAeronaves = "Tipo prueba", TipoOperacion = "Operación prueba", InspectorResponsable = "Inspector 900",
                FechaLista = new DateTime(2026, 9, 1).AddDays(estacion - 1), Items = items
            };
        }

        [TestMethod]
        public void TresEstaciones_PersistenTrasNuevaSesion_YFirmaAislada()
        {
            var servicio = Servicio();
            var a = servicio.GuardarRespuestas(Nueva(1), 900, "InspectorTecnico");
            var b = servicio.GuardarRespuestas(Nueva(2, true), 900, "InspectorTecnico");
            var recargada = Servicio().ObtenerOIniciarListaParaEstacion(700, 800, 1, 900, "InspectorTecnico", "Inspector");
            Assert.AreEqual("Observación estación 1", recargada.Items[0].PruebasNotasComentarios);
            Assert.IsTrue(string.IsNullOrEmpty(recargada.Items[2].EstadoCumplimiento), "La carga no completa orientaciones sin responder.");
            recargada.ObservacionesGenerales = "Avance conservado";
            servicio.GuardarRespuestas(recargada, 900, "InspectorTecnico");

            // Nueva instancia de DAO/servicio y nueva conexión: ningún dato proviene de sesión.
            var nuevaSesion = Servicio();
            var tercera = Nueva(3);
            tercera.Items[0].PruebasNotasComentarios = "Solo estación 3";
            var c = nuevaSesion.GuardarRespuestas(tercera, 901, "InspectorTecnico");
            nuevaSesion.FinalizarLista(b.CodigoListaVerificacion, 900, "InspectorTecnico");
            nuevaSesion.FirmarLista(b.CodigoListaVerificacion, "Inspector 900", "hash-prueba", "firma-lv2.pdf", 900, "InspectorTecnico");
            var da = Dao().ObtenerPorId(a.CodigoListaVerificacion);
            var db = Dao().ObtenerPorId(b.CodigoListaVerificacion);
            var dc = Dao().ObtenerPorId(c.CodigoListaVerificacion);
            Assert.AreEqual("Avance conservado", da.ObservacionesGenerales);
            Assert.AreEqual(new DateTime(2026, 9, 1), da.FechaLista);
            Assert.AreEqual("Inspector 900", da.InspectorResponsable);
            Assert.IsFalse(da.FirmadoTecnico);
            Assert.IsTrue(db.FirmadoTecnico);
            Assert.AreEqual("hash-prueba", db.HashDocumento);
            Assert.AreEqual("firma-lv2.pdf", db.RutaDocumentoFirmado);
            Assert.IsFalse(dc.FirmadoTecnico);
            Assert.IsTrue(dc.ItemsJson.Contains("Solo estación 3"));
            Assert.IsFalse(da.ItemsJson.Contains("Solo estación 3"));
            Assert.IsFalse(db.ItemsJson.Contains("Solo estación 3"));
            Assert.IsFalse(da.ItemsJson.Contains("PreguntaRequisito"), "Las nuevas respuestas no duplican el catálogo.");
            List<string> pendientes;
            Assert.IsFalse(Dao().TodasLasListasEstacionesFirmadas(700, 800, out pendientes));
            Assert.AreEqual(2, pendientes.Count);
            Assert.ThrowsException<InvalidOperationException>(() => Dao().GuardarBorrador(db, 900));
        }

        [TestMethod]
        public void IdentidadDeOtraEstacion_NoSobrescribeRespuestas()
        {
            var a = Servicio().GuardarRespuestas(Nueva(1), 900, "InspectorTecnico");
            var b = Servicio().GuardarRespuestas(Nueva(2), 900, "InspectorTecnico");
            a.EstacionId = 2;
            a.ItemsJson = "[]";
            Assert.ThrowsException<InvalidOperationException>(() => Dao().GuardarBorrador(a, 900));
            Assert.IsTrue(Dao().ObtenerPorId(b.CodigoListaVerificacion).ItemsJson.Contains("Observación estación 2"));
        }

        [TestMethod]
        public void AmbitoInvalido_RechazaEstacionAjenaYListaGeneral()
        {
            var lista = Nueva(99);
            Assert.ThrowsException<InvalidOperationException>(() => Dao().GuardarBorrador(lista, 900));
            lista.EstacionId = null;
            Assert.ThrowsException<InvalidOperationException>(() => Dao().GuardarBorrador(lista, 900));
            lista.EstacionId = 1; lista.SolicitudId = 701;
            Assert.ThrowsException<InvalidOperationException>(() => Dao().GuardarBorrador(lista, 900));
        }

        [TestMethod]
        public void Permisos_InspectorAjenoYRolesDeConsulta_NoPuedenEscribirNiFirmar()
        {
            var servicio = Servicio();
            Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.GuardarRespuestas(Nueva(1), 902, "InspectorTecnico"));
            Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.GuardarRespuestas(Nueva(3), 900, "InspectorTecnico"));
            var lv = servicio.GuardarRespuestas(Nueva(1), 900, "InspectorTecnico");
            Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.ObtenerOIniciarListaParaEstacion(700, 800, 1, 902, "InspectorTecnico", "Ajeno"));
            Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.FinalizarLista(lv.CodigoListaVerificacion, 902, "InspectorTecnico"));
            Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.FirmarLista(lv.CodigoListaVerificacion, "Ajeno", "hash", "ruta", 902, "InspectorTecnico"));
            foreach (var rol in new[] { "DIRCAV", "Coordinador", "Administrador", "DIRDAC", "RT", "Financiero" })
            {
                Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.GuardarRespuestas(Nueva(2), 900, rol));
                Assert.ThrowsException<UnauthorizedAccessException>(() => servicio.FirmarLista(lv.CodigoListaVerificacion, "X", "X", "X", 900, rol));
            }
        }

        [TestMethod]
        public void CreacionConcurrente_NoDuplicaNiSobrescribeElPrimerGuardado()
        {
            Parallel.For(0, 6, i =>
            {
                try { Dao().GuardarBorrador(Nueva(1), 900); }
                catch (InvalidOperationException) { /* Conflicto esperado: recargar la LV ya creada. */ }
            });
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand("SELECT count(*) FROM public.aocr_tblv_operacional_eae WHERE codigo_inspeccion=800 AND estacion_id=1", cn))
                    Assert.AreEqual(1L, (long)cmd.ExecuteScalar());
            }
        }

        [TestMethod]
        public void Reinspeccion_NoInactivaLaListaFirmadaDeOtraInspeccion()
        {
            var a = Servicio().GuardarRespuestas(Nueva(1, true), 900, "InspectorTecnico");
            Servicio().FinalizarLista(a.CodigoListaVerificacion, 900, "InspectorTecnico");
            Servicio().FirmarLista(a.CodigoListaVerificacion, "Inspector", "hash", "firma.pdf", 900, "InspectorTecnico");
            var nueva = Nueva(1); nueva.CodigoInspeccion = 801;
            Dao().GuardarBorrador(nueva, 900);
            Assert.IsTrue(Dao().ObtenerPorId(a.CodigoListaVerificacion).FirmadoTecnico);
            Assert.IsTrue(Dao().ObtenerPorId(a.CodigoListaVerificacion).Vigente);
        }

        [TestMethod]
        public void JsonCorrupto_NoSeOcultaConRespuestasVacias()
        {
            Assert.ThrowsException<InvalidOperationException>(() => new ListaVerificacionCatalogService().HidratarRespuestas("{invalido"));
            var catalogo = new ListaVerificacionCatalogService();
            var a = catalogo.HidratarRespuestas("[]");
            var b = catalogo.HidratarRespuestas("[]");
            Assert.IsTrue(a.Count > 0);
            a[0].PruebasNotasComentarios = "Solo A";
            Assert.IsTrue(string.IsNullOrEmpty(b[0].PruebasNotasComentarios));
        }

        [TestMethod]
        public void IniciarTresListas_EsIdempotente_YNoComparteInstancias()
        {
            var servicio = Servicio();
            var a = servicio.ObtenerOIniciarListaParaEstacion(700, 800, 1, 900, "InspectorTecnico", "Inspector 900");
            var b = servicio.ObtenerOIniciarListaParaEstacion(700, 800, 2, 900, "InspectorTecnico", "Inspector 900");
            var c = servicio.ObtenerOIniciarListaParaEstacion(700, 800, 3, 901, "InspectorTecnico", "Inspector 901");
            var otra = Servicio().ObtenerOIniciarListaParaEstacion(700, 800, 1, 900, "InspectorTecnico", "Inspector 900");
            Assert.AreEqual(a.CodigoListaVerificacion, otra.CodigoListaVerificacion);
            Assert.AreEqual(3, new[] { a.CodigoListaVerificacion, b.CodigoListaVerificacion, c.CodigoListaVerificacion }.Distinct().Count());
            a.Items[0].PruebasNotasComentarios = "Solo A";
            Assert.IsTrue(string.IsNullOrEmpty(b.Items[0].PruebasNotasComentarios));
            Assert.IsTrue(string.IsNullOrEmpty(c.Items[0].PruebasNotasComentarios));
        }

        [TestMethod]
        public void Catalogo_HistoricoPorPreguntaSeRecupera_PeroCodigoDesconocidoNoSeBorra()
        {
            var catalogo = new ListaVerificacionCatalogService();
            var items = catalogo.HidratarRespuestas("[{\"Codigo\":\"129-1\",\"EstadoCumplimiento\":\"SATISFACTORIO\",\"PruebasNotasComentarios\":\"Histórico\"}]");
            Assert.AreEqual("Histórico", items[0].PruebasNotasComentarios);
            Assert.ThrowsException<InvalidOperationException>(() => catalogo.HidratarRespuestas(
                "[{\"Codigo\":\"desconocido\",\"PruebasNotasComentarios\":\"No perder\"}]"));
        }

        [TestMethod]
        public void ErrorDeBaseDuranteGuardado_RevierteElCambioYPermiteReintentar()
        {
            var a = Servicio().GuardarRespuestas(Nueva(1), 900, "InspectorTecnico");
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand("ALTER TABLE public.aocr_tblv_operacional_eae ADD CONSTRAINT fallo_ac07 CHECK (updated_by <> 999)", cn)) cmd.ExecuteNonQuery();
                try
                {
                    a.ObservacionesGenerales = "Cambio que debe revertirse";
                    Assert.ThrowsException<PostgresException>(() => Dao().GuardarBorrador(a, 999));
                    Assert.AreNotEqual(a.ObservacionesGenerales, Dao().ObtenerPorId(a.CodigoListaVerificacion).ObservacionesGenerales);
                    Dao().GuardarBorrador(a, 900);
                    Assert.AreEqual(a.ObservacionesGenerales, Dao().ObtenerPorId(a.CodigoListaVerificacion).ObservacionesGenerales);
                }
                finally
                {
                    using (var cmd = new NpgsqlCommand("ALTER TABLE public.aocr_tblv_operacional_eae DROP CONSTRAINT fallo_ac07", cn)) cmd.ExecuteNonQuery();
                }
            }
        }

        [TestMethod]
        public void Ac08_PostDirectoSinJavascript_NoCompletaNiCierraConUnoOVariosPendientes()
        {
            var catalogo = new ListaVerificacionCatalogService();
            foreach (var numeroPendientes in new[] { 1, 3 })
            {
                var estacion = numeroPendientes == 1 ? 1 : 2;
                var lista = Nueva(estacion, true);
                var form = new NameValueCollection { { "finalizar", "true" }, { "estadoLista", "LV_COMPLETADA" } };
                foreach (var item in lista.Items.Where(i => !i.EsNotaOrientacion))
                {
                    form["lvItem_" + item.Codigo + "_cumplimiento"] = "SATISFACTORIO";
                    form["lvItem_" + item.Codigo + "_implementacion"] = "IMPLEMENTADO";
                    form["lvItem_" + item.Codigo + "_comentarios"] = "La observación no reemplaza el resultado";
                }
                foreach (var item in lista.Items.Where(i => !i.EsNotaOrientacion).Take(numeroPendientes))
                    form.Remove("lvItem_" + item.Codigo + "_cumplimiento");
                // Mismo lector de campos que consume el endpoint POST del controlador.
                lista.Items = catalogo.LeerRespuestasFormulario(form);
                var resultado = Servicio().EvaluarCompletitud(lista);
                Assert.AreEqual(numeroPendientes, resultado.CantidadPendientes);
                var guardada = Servicio().GuardarRespuestas(lista, 900, "InspectorTecnico");
                Assert.AreEqual("LV_EN_PROCESO", guardada.EstadoLista);
                Assert.ThrowsException<ListaVerificacionIncompletaException>(() =>
                    Dao().MarcarFinalizada(guardada.CodigoListaVerificacion, "pdf.pdf", "LV_COMPLETADA", 900));
                Assert.IsFalse(Dao().ObtenerPorId(guardada.CodigoListaVerificacion).Finalizado);
            }
        }

        [TestMethod]
        public void Ac08_ValoresManipuladosYMetadatosDelCliente_NoEvadenElCatalogo()
        {
            foreach (var valor in new[] { "", " ", "INSATISFACTORIO", "NO APLICA", "EVADIR" })
            {
                var lista = Nueva(1, true);
                var item = lista.Items.First(i => !i.EsNotaOrientacion);
                item.EstadoCumplimiento = valor;
                item.EsNotaOrientacion = true; // No confiar en este indicador recibido.
                var resultado = Servicio().EvaluarCompletitud(lista);
                Assert.IsFalse(resultado.EsValida);
                Assert.AreEqual(1, resultado.CantidadPendientes);
                Assert.AreEqual(item.Codigo, resultado.Pendientes[0].Codigo);
            }
            var omitida = Nueva(1, true);
            omitida.Items.RemoveAt(0);
            Assert.AreEqual(1, Servicio().EvaluarCompletitud(omitida).CantidadPendientes);
            var soloPreguntas = Nueva(1, true);
            soloPreguntas.Items = soloPreguntas.Items.Where(i => !i.EsNotaOrientacion)
                .GroupBy(i => i.CodigoPregunta).Select(group => new ListaVerificacionOperacionalEaeItem
                {
                    Codigo = group.Key, CodigoPregunta = group.Key,
                    EstadoCumplimiento = "SATISFACTORIO", EstadoImplementacion = "IMPLEMENTADO"
                }).ToList();
            Assert.IsFalse(Servicio().EvaluarCompletitud(soloPreguntas).EsValida,
                "Un resultado por pregunta no acredita todas las orientaciones obligatorias.");
        }

        [TestMethod]
        public void Ac08_JsonPersistidoIncompleto_NoPuedeForzarseACompletaNiFirmarse()
        {
            var lista = Nueva(1, true);
            // Items completo no puede encubrir un JSON vacío que sería el persistido.
            lista.ItemsJson = "[]";
            lista.EstadoLista = "LV_COMPLETADA";
            Assert.ThrowsException<ListaVerificacionIncompletaException>(() => Dao().GuardarBorrador(lista, 900));
            lista.EstadoLista = "LV_BORRADOR";
            var historica = Dao().GuardarBorrador(lista, 900);
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand("UPDATE public.aocr_tblv_operacional_eae SET finalizado=true, estado_lista='LV_COMPLETADA' WHERE codigo_lv=@id", cn))
                {
                    cmd.Parameters.AddWithValue("@id", historica.CodigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
            Assert.ThrowsException<ListaVerificacionIncompletaException>(() => Dao().MarcarFirmada(
                historica.CodigoListaVerificacion, "firma.pdf", "hash", "Inspector", DateTime.Now, "LV_FIRMADA", 900));
            Assert.ThrowsException<ListaVerificacionIncompletaException>(() => Dao().RegistrarFirmaTecnico(
                historica.CodigoListaVerificacion, "firma.pdf", "hash", DateTime.Now, "Inspector", "LV_FIRMADA", 900));
            Assert.IsFalse(Dao().ObtenerPorId(historica.CodigoListaVerificacion).FirmadoTecnico);
        }

        [TestMethod]
        public void Ac08_NoAplicableEsValido_NegativosExigenObservaciones()
        {
            var lista = Nueva(1, true);
            var primero = lista.Items.First(i => !i.EsNotaOrientacion);
            primero.EstadoCumplimiento = primero.EstadoImplementacion = "NO_APLICABLE";
            primero.PruebasNotasComentarios = "";
            Assert.IsTrue(Servicio().EvaluarCompletitud(lista).EsValida);
            foreach (var item in lista.Items) item.PruebasNotasComentarios = "";
            primero = lista.Items.First(i => !i.EsNotaOrientacion);
            primero.EstadoCumplimiento = "NO_SATISFACTORIO";
            primero.EstadoImplementacion = "NO_IMPLEMENTADO";
            Assert.AreEqual(1, Servicio().EvaluarCompletitud(lista).CantidadPendientes);
            lista.Items.First(i => !i.EsNotaOrientacion).PruebasNotasComentarios = "Evidencia del hallazgo";
            Assert.IsTrue(Servicio().EvaluarCompletitud(lista).EsValida);
        }
    }
}
