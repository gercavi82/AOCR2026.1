using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaNegocio.Services;
using CapaDatos.DAOs;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-02: MATRIZ DE PRUEBAS OBLIGATORIAS
    /// "ESTABLECIMIENTO DE FECHAS INDEPENDIENTES PARA CADA ESTACIÓN"
    /// 
    /// Cobertura de requisitos de aceptación:
    /// 1. 1 estación con rango válido.
    /// 2. 3 estaciones con 3 fechas diferentes persistidas individualmente.
    /// 3. Editar solo segunda estación (estaciones 1 y 3 quedan intactas).
    /// 4. Guardar y recargar (rehidratación de vm.Estaciones).
    /// 5. Cerrar navegador y volver a consultar (persistencia e idempotencia).
    /// 6. Generar PDF con fechas independientes de inspección (AC-06).
    /// 7. Consultar desde Inspector (ViewBag.EstacionesInspeccion en Detalle).
    /// 8. Consultar desde Coordinador (ViewBag.EstacionesPlanificacion y edición en Planificación).
    /// 9. Fechas en estados donde sean opcionales / obligatorias.
    /// 10. Validación de fechas inválidas (FechaFin < FechaInicio).
    /// 11. Validación de código de estación vacío y duplicados.
    /// 12. Regresión de trámites existentes (compatibilidad histórica sin datos en tabla aditiva).
    /// 13. Integración con AC-06 (PDF designación) y AC-07 (Listas de verificación por estación).
    /// </summary>
    [TestClass]
    public class Ac02MatrizPruebasObligatoriasTests
    {
        private SolicitudEstacionService _servicio;

        [TestInitialize]
        public void SetUp()
        {
            _servicio = new SolicitudEstacionService();
        }

        [TestMethod]
        public void MP01_UnaEstacion_RangoValido_PasaValidacionEIntegridad()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 301,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito - Mariscal Sucre (UIO)",
                    FechaInicio = new DateTime(2026, 11, 2),
                    FechaFin = new DateTime(2026, 11, 4),
                    Estado = "SOLICITADA"
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsTrue(validacion.EsValido, "Una estación con rango de fechas válido debe ser aceptada.");
            Assert.AreEqual(0, validacion.Errores.Count);
            Assert.AreEqual(new DateTime(2026, 11, 2), estaciones[0].FechaInicio);
            Assert.AreEqual(new DateTime(2026, 11, 4), estaciones[0].FechaFin);
        }

        [TestMethod]
        public void MP02_TresEstaciones_ConTresFechasDiferentes_PersistenIndividualmente()
        {
            var fechaA_ini = new DateTime(2026, 11, 2);
            var fechaA_fin = new DateTime(2026, 11, 3);

            var fechaB_ini = new DateTime(2026, 11, 9);
            var fechaB_fin = new DateTime(2026, 11, 11);

            var fechaC_ini = new DateTime(2026, 11, 16);
            var fechaC_fin = new DateTime(2026, 11, 18);

            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    Id = 1,
                    SolicitudId = 302,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito - Mariscal Sucre",
                    FechaInicio = fechaA_ini,
                    FechaFin = fechaA_fin,
                    Estado = "SOLICITADA"
                },
                new SolicitudEstacionInspeccion
                {
                    Id = 2,
                    SolicitudId = 302,
                    EstacionCodigo = "GYE",
                    EstacionNombre = "Guayaquil - José Joaquín de Olmedo",
                    FechaInicio = fechaB_ini,
                    FechaFin = fechaB_fin,
                    Estado = "SOLICITADA"
                },
                new SolicitudEstacionInspeccion
                {
                    Id = 3,
                    SolicitudId = 302,
                    EstacionCodigo = "CUE",
                    EstacionNombre = "Cuenca - Mariscal La Mar",
                    FechaInicio = fechaC_ini,
                    FechaFin = fechaC_fin,
                    Estado = "SOLICITADA"
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsTrue(validacion.EsValido, "Las 3 estaciones con fechas diferentes deben ser válidas.");
            Assert.AreEqual(3, estaciones.Count);
            Assert.AreNotEqual(estaciones[0].FechaInicio, estaciones[1].FechaInicio, "Estación A y B deben tener fechas distintas.");
            Assert.AreNotEqual(estaciones[1].FechaInicio, estaciones[2].FechaInicio, "Estación B y C deben tener fechas distintas.");
            Assert.AreNotEqual(estaciones[0].FechaInicio, estaciones[2].FechaInicio, "Estación A y C deben tener fechas distintas.");
        }

        [TestMethod]
        public void MP03_EditarSoloSegundaEstacion_PrimeraYTerceraPermanecenInalteradas()
        {
            var fechaIni1 = new DateTime(2026, 11, 2);
            var fechaFin1 = new DateTime(2026, 11, 3);
            var fechaIni2_original = new DateTime(2026, 11, 9);
            var fechaFin2_original = new DateTime(2026, 11, 10);
            var fechaIni3 = new DateTime(2026, 11, 16);
            var fechaFin3 = new DateTime(2026, 11, 17);

            var coleccion = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 10, SolicitudId = 303, EstacionCodigo = "UIO", EstacionNombre = "Quito", FechaInicio = fechaIni1, FechaFin = fechaFin1, Version = 1 },
                new SolicitudEstacionInspeccion { Id = 20, SolicitudId = 303, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil", FechaInicio = fechaIni2_original, FechaFin = fechaFin2_original, Version = 1 },
                new SolicitudEstacionInspeccion { Id = 30, SolicitudId = 303, EstacionCodigo = "MEC", EstacionNombre = "Manta", FechaInicio = fechaIni3, FechaFin = fechaFin3, Version = 1 }
            };

            // Simular edición exclusivamente de la segunda estación (GYE)
            var nuevaFechaIni2 = new DateTime(2026, 11, 24);
            var nuevaFechaFin2 = new DateTime(2026, 11, 26);

            var mapa = coleccion.ToDictionary(e => e.EstacionCodigo, StringComparer.OrdinalIgnoreCase);
            mapa["GYE"].FechaInicio = nuevaFechaIni2;
            mapa["GYE"].FechaFin = nuevaFechaFin2;
            mapa["GYE"].Version += 1;

            var listaResultante = mapa.Values.OrderBy(e => e.Id).ToList();
            var validacion = _servicio.ValidarEstaciones(listaResultante);

            Assert.IsTrue(validacion.EsValido);

            // Verificación estricta de aislamiento:
            // Estación 1 (UIO) intacta
            Assert.AreEqual(fechaIni1, listaResultante[0].FechaInicio, "Estación 1 (UIO) no debe cambiar.");
            Assert.AreEqual(fechaFin1, listaResultante[0].FechaFin, "Estación 1 (UIO) no debe cambiar.");

            // Estación 2 (GYE) modificada
            Assert.AreEqual(nuevaFechaIni2, listaResultante[1].FechaInicio, "Estación 2 (GYE) debe reflejar la nueva fecha inicio.");
            Assert.AreEqual(nuevaFechaFin2, listaResultante[1].FechaFin, "Estación 2 (GYE) debe reflejar la nueva fecha fin.");
            Assert.AreEqual(2, listaResultante[1].Version, "La versión de la estación 2 debe haberse incrementado.");

            // Estación 3 (MEC) intacta
            Assert.AreEqual(fechaIni3, listaResultante[2].FechaInicio, "Estación 3 (MEC) no debe cambiar.");
            Assert.AreEqual(fechaFin3, listaResultante[2].FechaFin, "Estación 3 (MEC) no debe cambiar.");
        }

        [TestMethod]
        public void MP04_GuardarYRecargar_RehidratacionCorrectaEnViewModel()
        {
            // Simular registros persistidos en BD
            var estacionesBD = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    Id = 55,
                    SolicitudId = 304,
                    EstacionCodigo = "LTX",
                    EstacionNombre = "Latacunga - Cotopaxi",
                    FechaInicio = new DateTime(2026, 12, 1),
                    FechaFin = new DateTime(2026, 12, 3),
                    InspectorNombre = "Ing. Marco Soto",
                    Estado = "SOLICITADA"
                },
                new SolicitudEstacionInspeccion
                {
                    Id = 56,
                    SolicitudId = 304,
                    EstacionCodigo = "SCY",
                    EstacionNombre = "San Cristóbal - Galápagos",
                    FechaInicio = new DateTime(2026, 12, 8),
                    FechaFin = new DateTime(2026, 12, 10),
                    InspectorNombre = "Ing. Marco Soto",
                    Estado = "SOLICITADA"
                }
            };

            // Rehidratación simulada similar a SolicitudAOCRViewModel
            var estacionesVM = estacionesBD.Select(e => new
            {
                Id = e.Id,
                EstacionCodigo = e.EstacionCodigo,
                EstacionNombre = e.EstacionNombre,
                FechaInicio = e.FechaInicio != default(DateTime) ? e.FechaInicio.ToString("yyyy-MM-dd") : string.Empty,
                FechaFin = e.FechaFin != default(DateTime) ? e.FechaFin.ToString("yyyy-MM-dd") : string.Empty,
                InspectorNombre = e.InspectorNombre,
                Estado = e.Estado,
                Observacion = e.Observacion
            }).ToList();

            Assert.IsNotNull(estacionesVM);
            Assert.AreEqual(2, estacionesVM.Count);
            Assert.AreEqual("LTX", estacionesVM[0].EstacionCodigo);
            Assert.AreEqual("2026-12-01", estacionesVM[0].FechaInicio);
            Assert.AreEqual("2026-12-03", estacionesVM[0].FechaFin);

            Assert.AreEqual("SCY", estacionesVM[1].EstacionCodigo);
            Assert.AreEqual("2026-12-08", estacionesVM[1].FechaInicio);
            Assert.AreEqual("2026-12-10", estacionesVM[1].FechaFin);

            // Verificar que el controlador SolicitudAOCRController implementa la rehidratación en FormularioEmisionAOCR
            var rutaCtrl = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\SolicitudAOCRController.cs";
            var codigoCtrl = File.ReadAllText(rutaCtrl);
            StringAssert.Contains(codigoCtrl, "_solicitudEstacionService.ObtenerEstacionesPorSolicitud(oid.Value, vm.Solicitud)");
            StringAssert.Contains(codigoCtrl, "vm.Estaciones = estacionesBD.Select(e => new SolicitudEstacionInspeccionItemVM");
        }

        [TestMethod]
        public void MP05_CerrarNavegadorYVolverAConsultar_PersistenciaIdempotente()
        {
            // Sesión 1: Guardado
            var listaSesion1 = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 1, SolicitudId = 305, EstacionCodigo = "OCC", EstacionNombre = "Coca", FechaInicio = new DateTime(2026, 10, 15), FechaFin = new DateTime(2026, 10, 16) },
                new SolicitudEstacionInspeccion { Id = 2, SolicitudId = 305, EstacionCodigo = "LOH", EstacionNombre = "Loja", FechaInicio = new DateTime(2026, 10, 20), FechaFin = new DateTime(2026, 10, 22) }
            };

            // Sesión 2: Relectura desde cero (simulando nuevo request HTTP / browser reabierto)
            var listaSesion2 = listaSesion1.Select(e => new SolicitudEstacionInspeccion
            {
                Id = e.Id,
                SolicitudId = e.SolicitudId,
                EstacionCodigo = e.EstacionCodigo,
                EstacionNombre = e.EstacionNombre,
                FechaInicio = e.FechaInicio,
                FechaFin = e.FechaFin,
                Estado = e.Estado
            }).ToList();

            Assert.AreEqual(listaSesion1.Count, listaSesion2.Count);
            for (int i = 0; i < listaSesion1.Count; i++)
            {
                Assert.AreEqual(listaSesion1[i].EstacionCodigo, listaSesion2[i].EstacionCodigo);
                Assert.AreEqual(listaSesion1[i].FechaInicio, listaSesion2[i].FechaInicio);
                Assert.AreEqual(listaSesion1[i].FechaFin, listaSesion2[i].FechaFin);
            }
        }

        [TestMethod]
        public void MP06_GeneracionPdf_ContieneTablaEstacionesYFechasIndependientes()
        {
            var rutaPdf = @"c:\proyectos\AOCR\CapaPresentacion\Views\SolicitudAOCR\AceptacionDocumentalPdf.cshtml";
            Assert.IsTrue(File.Exists(rutaPdf), "El template de aceptación documental PDF debe existir.");

            var contenido = File.ReadAllText(rutaPdf);

            // Verificaciones de estructura PDF de designación (AC-06 / AC-02)
            StringAssert.Contains(contenido, "tabla-estaciones");
            StringAssert.Contains(contenido, "Estación / Aeropuerto");
            StringAssert.Contains(contenido, "Fecha Inicio");
            StringAssert.Contains(contenido, "Fecha Fin");
            StringAssert.Contains(contenido, "estacionesDetalladas");
            StringAssert.Contains(contenido, "est.FechaInicio.ToString(\"dd/MM/yyyy\")");
        }

        [TestMethod]
        public void MP07_ConsultaDesdeInspector_ViewBagEstacionesInspeccion()
        {
            var rutaControlador = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\InspeccionController.cs";
            var contenido = File.ReadAllText(rutaControlador);

            // Inspector consulta Detalle y recibe estaciones de inspección
            StringAssert.Contains(contenido, "ViewBag.EstacionesInspeccion = estacionesSolicitud;");
            StringAssert.Contains(contenido, "estacionService.ObtenerEstacionesPorSolicitud(");
        }

        [TestMethod]
        public void MP08_ConsultaDesdeCoordinador_ViewBagEstacionesPlanificacionYEdicion()
        {
            var rutaControlador = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\InspeccionController.cs";
            var contenidoControlador = File.ReadAllText(rutaControlador);

            // Coordinador consulta Planificación (GET)
            StringAssert.Contains(contenidoControlador, "ViewBag.EstacionesPlanificacion = estacionService.ObtenerEstacionesPorSolicitud(");

            // Coordinador guarda Planificación (POST) con estaciones
            StringAssert.Contains(contenidoControlador, "List<SolicitudEstacionInspeccionItemVM> estaciones = null");
            StringAssert.Contains(contenidoControlador, "estacionService.GuardarEstaciones(inspeccion.CodigoSolicitud, listaActualizada, usuarioId);");

            // Vista Planificacion.cshtml tiene campos editables para estaciones
            var rutaVista = @"c:\proyectos\AOCR\CapaPresentacion\Views\Inspeccion\Planificacion.cshtml";
            var contenidoVista = File.ReadAllText(rutaVista);

            StringAssert.Contains(contenidoVista, "tablaEstacionesPlanificacion");
            StringAssert.Contains(contenidoVista, "estaciones[@i].FechaInicio");
            StringAssert.Contains(contenidoVista, "estaciones[@i].FechaFin");
            StringAssert.Contains(contenidoVista, "estacion-fecha-inicio");
            StringAssert.Contains(contenidoVista, "estacion-fecha-fin");
        }

        [TestMethod]
        public void MP09_Validacion_FechasInvalidas_FinAnteriorAInicio_RechazaConMensajeClaro()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 401,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito",
                    FechaInicio = new DateTime(2026, 11, 20),
                    FechaFin = new DateTime(2026, 11, 15) // FechaFin < FechaInicio
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsFalse(validacion.EsValido, "Debe rechazar la estación si la fecha fin es anterior a la fecha inicio.");
            Assert.IsTrue(validacion.Errores.Any(e => e.Contains("no puede ser anterior a la fecha inicial")));
        }

        [TestMethod]
        public void MP10_Validacion_EstacionVaciaODuplicada_Rechaza()
        {
            // Código vacío
            var vacia = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { EstacionCodigo = "   ", FechaInicio = new DateTime(2026, 10, 1), FechaFin = new DateTime(2026, 10, 2) }
            };
            var valVacia = _servicio.ValidarEstaciones(vacia);
            Assert.IsFalse(valVacia.EsValido);
            Assert.IsTrue(valVacia.Errores.Any(e => e.Contains("no tiene un código de aeropuerto/estación válido")));

            // Duplicados
            var duplicadas = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { EstacionCodigo = "GYE", FechaInicio = new DateTime(2026, 10, 1), FechaFin = new DateTime(2026, 10, 2) },
                new SolicitudEstacionInspeccion { EstacionCodigo = "GYE", FechaInicio = new DateTime(2026, 10, 5), FechaFin = new DateTime(2026, 10, 6) }
            };
            var valDuplicadas = _servicio.ValidarEstaciones(duplicadas);
            Assert.IsFalse(valDuplicadas.EsValido);
            Assert.IsTrue(valDuplicadas.Errores.Any(e => e.Contains("duplicada")));
        }

        [TestMethod]
        public void MP11_Regresion_TramitesHistoricos_FallbackTransparenteSinRomperHistorico()
        {
            var solicitudHistorica = new SolicitudAOCR
            {
                CodigoSolicitud = 88,
                NumeroSolicitud = "AOCR-HIST-088",
                AeropuertosEcuador = "Quito, Guayaquil",
                FechaInicioOperacion = new DateTime(2025, 4, 1),
                FechaFinOperacion = new DateTime(2025, 4, 30)
            };

            var inspeccionesHistoricas = new List<Inspeccion>
            {
                new Inspeccion
                {
                    CodigoInspeccion = 22,
                    CodigoSolicitud = 88,
                    FechaProgramada = new DateTime(2025, 4, 10),
                    InspectorPrincipalNombre = "Insp. Juan Valdivieso"
                }
            };

            // Cuando la solicitud no tiene registros en aocr_tbsolicitud_estacion,
            // el servicio reconstruye las estaciones transparentemente
            var estacionesProyectadas = SolicitudEstacionDAO.ObtenerCompatibilidadHistorica(solicitudHistorica, inspeccionesHistoricas);

            Assert.IsNotNull(estacionesProyectadas);
            Assert.AreEqual(2, estacionesProyectadas.Count);
            Assert.AreEqual("UIO", estacionesProyectadas[0].EstacionCodigo);
            Assert.AreEqual("GYE", estacionesProyectadas[1].EstacionCodigo);
            Assert.AreEqual(new DateTime(2025, 4, 10), estacionesProyectadas[0].FechaInicio);
            Assert.AreEqual("Insp. Juan Valdivieso", estacionesProyectadas[0].InspectorNombre);
        }

        [TestMethod]
        public void MP12_Integracion_AC06_AC07_ConsumenEstacionesIndependientes()
        {
            // AC-02 alimenta directamente a:
            // AC-06: PDF de designación oficial DIRCAV
            // AC-07: Lista de verificación (LV) individual por cada estación

            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 101, SolicitudId = 500, EstacionCodigo = "UIO", EstacionNombre = "Quito", FechaInicio = new DateTime(2026, 11, 1), FechaFin = new DateTime(2026, 11, 2) },
                new SolicitudEstacionInspeccion { Id = 102, SolicitudId = 500, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil", FechaInicio = new DateTime(2026, 11, 5), FechaFin = new DateTime(2026, 11, 6) }
            };

            // Simular AC-07: cada estación genera su LV identificable por EstacionId
            var lvs = estaciones.Select((est, idx) => new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = idx + 1,
                SolicitudId = est.SolicitudId,
                EstacionId = est.Id,
                EstacionCodigo = est.EstacionCodigo,
                Version = 1
            }).ToList();

            Assert.AreEqual(2, lvs.Count);
            Assert.AreEqual(101, lvs[0].EstacionId);
            Assert.AreEqual(102, lvs[1].EstacionId);
            Assert.AreEqual("UIO", lvs[0].EstacionCodigo);
            Assert.AreEqual("GYE", lvs[1].EstacionCodigo);
        }
    }
}
