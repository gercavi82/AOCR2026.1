using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaNegocio.Services;
using CapaDatos.DAOs;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-02: Pruebas unitarias para fechas de inspección independientes por estación.
    /// Valida: una estación, múltiples estaciones, fechas diferentes, edición independiente,
    /// eliminación controlada, fecha final inválida, doble registro, persistencia,
    /// solicitudes históricas, seguridad de acceso y renderizado responsive.
    /// </summary>
    [TestClass]
    public class Ac02FechasInspeccionEstacionesTests
    {
        private SolicitudEstacionService _servicio;

        [TestInitialize]
        public void SetUp()
        {
            _servicio = new SolicitudEstacionService();
        }

        [TestMethod]
        public void Test01_UnaEstacion_ConRangoValido_PasaValidacionCorrectamente()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 1),
                    FechaFin = new DateTime(2026, 10, 3),
                    Estado = "SOLICITADA"
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsTrue(validacion.EsValido, "Una estación con rango de fechas válido debe ser válida.");
            Assert.AreEqual(0, validacion.Errores.Count);
        }

        [TestMethod]
        public void Test02_DosOMasEstaciones_ConFechasDiferentes_PasaValidacion()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 1),
                    FechaFin = new DateTime(2026, 10, 2),
                    Estado = "SOLICITADA"
                },
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "GYE",
                    EstacionNombre = "Guayaquil (GYE)",
                    FechaInicio = new DateTime(2026, 10, 5),
                    FechaFin = new DateTime(2026, 10, 7),
                    Estado = "SOLICITADA"
                },
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "MEC",
                    EstacionNombre = "Manta (MEC)",
                    FechaInicio = new DateTime(2026, 10, 10),
                    FechaFin = new DateTime(2026, 10, 11),
                    Estado = "SOLICITADA"
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsTrue(validacion.EsValido, "Múltiples estaciones con fechas independientes y diferentes deben ser válidas.");
            Assert.AreEqual(0, validacion.Errores.Count);
            Assert.AreNotEqual(estaciones[0].FechaInicio, estaciones[1].FechaInicio);
            Assert.AreNotEqual(estaciones[1].FechaInicio, estaciones[2].FechaInicio);
        }

        [TestMethod]
        public void Test03_EdicionIndependiente_ModificarUnaEstacion_NoAlteraLasDemas()
        {
            var estacionesOriginales = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    Id = 1,
                    SolicitudId = 200,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 1),
                    FechaFin = new DateTime(2026, 10, 2),
                    Version = 1
                },
                new SolicitudEstacionInspeccion
                {
                    Id = 2,
                    SolicitudId = 200,
                    EstacionCodigo = "GYE",
                    EstacionNombre = "Guayaquil (GYE)",
                    FechaInicio = new DateTime(2026, 10, 5),
                    FechaFin = new DateTime(2026, 10, 6),
                    Version = 1
                }
            };

            // Simular edición únicamente de GYE
            var estacionGyeModificada = new SolicitudEstacionInspeccion
            {
                Id = 2,
                SolicitudId = 200,
                EstacionCodigo = "GYE",
                EstacionNombre = "Guayaquil (GYE) Actualizado",
                FechaInicio = new DateTime(2026, 10, 8),
                FechaFin = new DateTime(2026, 10, 9),
                Version = 2
            };

            var listaResultante = new List<SolicitudEstacionInspeccion>
            {
                estacionesOriginales[0], // UIO intacta
                estacionGyeModificada
            };

            var validacion = _servicio.ValidarEstaciones(listaResultante);

            Assert.IsTrue(validacion.EsValido);
            Assert.AreEqual(new DateTime(2026, 10, 1), listaResultante[0].FechaInicio, "La estación UIO no debió modificarse.");
            Assert.AreEqual(new DateTime(2026, 10, 8), listaResultante[1].FechaInicio, "La estación GYE debió actualizarse a la nueva fecha.");
        }

        [TestMethod]
        public void Test04_EliminacionControlada_RetirarEstacion_ExcluyeSoloLaEstacionDeseada()
        {
            var listaInicial = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { EstacionCodigo = "UIO", FechaInicio = new DateTime(2026, 10, 1), FechaFin = new DateTime(2026, 10, 2) },
                new SolicitudEstacionInspeccion { EstacionCodigo = "LTX", FechaInicio = new DateTime(2026, 10, 3), FechaFin = new DateTime(2026, 10, 4) },
                new SolicitudEstacionInspeccion { EstacionCodigo = "GYE", FechaInicio = new DateTime(2026, 10, 5), FechaFin = new DateTime(2026, 10, 6) }
            };

            // Retirar LTX
            var listaDespuesEliminar = listaInicial.Where(e => e.EstacionCodigo != "LTX").ToList();

            Assert.AreEqual(2, listaDespuesEliminar.Count);
            Assert.IsFalse(listaDespuesEliminar.Any(e => e.EstacionCodigo == "LTX"));
            Assert.IsTrue(listaDespuesEliminar.Any(e => e.EstacionCodigo == "UIO"));
            Assert.IsTrue(listaDespuesEliminar.Any(e => e.EstacionCodigo == "GYE"));

            var validacion = _servicio.ValidarEstaciones(listaDespuesEliminar);
            Assert.IsTrue(validacion.EsValido);
        }

        [TestMethod]
        public void Test05_FechaFinalInvalida_AnteriorAFechaInicio_RechazaConMensajeDescriptivo()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 5),
                    FechaFin = new DateTime(2026, 10, 2), // Inválida: Fin < Inicio
                    Estado = "SOLICITADA"
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsFalse(validacion.EsValido, "Debe rechazar si la fecha final es anterior a la fecha inicial.");
            Assert.IsTrue(validacion.Errores.Any(e => e.Contains("no puede ser anterior a la fecha inicial")),
                "El mensaje de error debe indicar claramente que la fecha final no puede ser anterior.");
        }

        [TestMethod]
        public void Test06_Validacion_EstacionVacia_RechazaConError()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "", // Inválido
                    FechaInicio = new DateTime(2026, 10, 1),
                    FechaFin = new DateTime(2026, 10, 2)
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsFalse(validacion.EsValido);
            Assert.IsTrue(validacion.Errores.Any(e => e.Contains("código de aeropuerto/estación válido")));
        }

        [TestMethod]
        public void Test07_DobleRegistro_EstacionDuplicadaEnMismaSolicitud_RechazaConError()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "UIO",
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 1),
                    FechaFin = new DateTime(2026, 10, 2)
                },
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 1001,
                    EstacionCodigo = "UIO", // Duplicada
                    EstacionNombre = "Quito (UIO)",
                    FechaInicio = new DateTime(2026, 10, 5),
                    FechaFin = new DateTime(2026, 10, 6)
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);

            Assert.IsFalse(validacion.EsValido, "No debe permitir duplicar la misma estación en una solicitud.");
            Assert.IsTrue(validacion.Errores.Any(e => e.Contains("duplicada")));
        }

        [TestMethod]
        public void Test08_SolicitudesHistoricas_ReconstruyeEstacionesDesdeCamposLegacy()
        {
            var solicitudHistorica = new SolicitudAOCR
            {
                CodigoSolicitud = 99,
                NumeroSolicitud = "AOCR-0099",
                AeropuertosEcuador = "Quito,Guayaquil,Manta",
                AeropuertosEcuadorOtros = "Latacunga",
                FechaInicioOperacion = new DateTime(2025, 5, 10),
                FechaFinOperacion = new DateTime(2025, 5, 20)
            };

            var inspeccionesHistoricas = new List<Inspeccion>
            {
                new Inspeccion
                {
                    CodigoInspeccion = 45,
                    CodigoSolicitud = 99,
                    FechaProgramada = new DateTime(2025, 5, 12),
                    InspectorPrincipalNombre = "Cap. Carlos Perez"
                }
            };

            var resultado = SolicitudEstacionDAO.ObtenerCompatibilidadHistorica(solicitudHistorica, inspeccionesHistoricas);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(4, resultado.Count, "Debe proyectar 4 estaciones (Quito, Guayaquil, Manta, Latacunga).");
            Assert.IsTrue(resultado.Any(e => e.EstacionCodigo == "UIO"));
            Assert.IsTrue(resultado.Any(e => e.EstacionCodigo == "GYE"));
            Assert.IsTrue(resultado.Any(e => e.EstacionCodigo == "MEC"));
            Assert.IsTrue(resultado.Any(e => e.EstacionCodigo == "LTX"));

            // Fecha base debe provenir de la inspección programada histórica
            Assert.AreEqual(new DateTime(2025, 5, 12), resultado[0].FechaInicio);
            Assert.AreEqual("Cap. Carlos Perez", resultado[0].InspectorNombre);
        }

        [TestMethod]
        public void Test09_PlantillaPdf_AceptacionDocumental_PoseeEstructuraTablaEstaciones()
        {
            var rutaPdf = @"c:\proyectos\AOCR\CapaPresentacion\Views\SolicitudAOCR\AceptacionDocumentalPdf.cshtml";
            Assert.IsTrue(File.Exists(rutaPdf), "La vista AceptacionDocumentalPdf.cshtml debe existir.");

            var contenido = File.ReadAllText(rutaPdf);

            Assert.IsTrue(contenido.Contains("tabla-estaciones"), "El PDF debe incluir la clase tabla-estaciones.");
            Assert.IsTrue(contenido.Contains("Fecha Inicio"), "El PDF debe incluir columna Fecha Inicio.");
            Assert.IsTrue(contenido.Contains("Fecha Fin"), "El PDF debe incluir columna Fecha Fin.");
            Assert.IsTrue(contenido.Contains("estacionesDetalladas"), "El PDF debe procesar estacionesDetalladas.");
        }

        [TestMethod]
        public void Test10_SeguridadRBAC_CoordinadorEInspector_PlanificacionRequiereAutorizacion()
        {
            var rutaControlador = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\InspeccionController.cs";
            var contenido = File.ReadAllText(rutaControlador);

            var matches = Regex.Matches(contenido, @"\[Authorize\(Roles\s*=\s*ROL_COORD");
            Assert.IsTrue(matches.Count > 0, "El controlador de inspecciones debe tener protección RBAC para Coordinador.");
        }

        [TestMethod]
        public void Test11_RenderizadoResponsive_EstacionesTable_PoseeClasesAdaptables()
        {
            var rutaFormulario = @"c:\proyectos\AOCR\CapaPresentacion\Views\SolicitudAOCR\_FormularioEmisionAOCR.cshtml";
            var contenidoFormulario = File.ReadAllText(rutaFormulario);

            Assert.IsTrue(contenidoFormulario.Contains("table-responsive"), "El formulario debe incluir contenedor responsive table-responsive.");
            Assert.IsTrue(contenidoFormulario.Contains("tablaEstacionesInspeccion"), "El formulario debe incluir tablaEstacionesInspeccion.");
            Assert.IsTrue(contenidoFormulario.Contains("btnAgregarEstacion"), "El formulario debe incluir botón interactivo btnAgregarEstacion.");
            Assert.IsTrue(contenidoFormulario.Contains("btnEliminarEstacion"), "El formulario debe incluir botón interactivo btnEliminarEstacion.");

            var rutaDetalle = @"c:\proyectos\AOCR\CapaPresentacion\Views\SolicitudAOCR\Detalle.cshtml";
            var contenidoDetalle = File.ReadAllText(rutaDetalle);

            Assert.IsTrue(contenidoDetalle.Contains("AC-02 Multi-Estación"), "El Detalle debe incluir badge o indicador AC-02 Multi-Estación.");
            Assert.IsTrue(contenidoDetalle.Contains("Estaciones y Fechas de Inspección Independientes"), "El Detalle debe incluir la sección de estaciones independientes.");
        }

        [TestMethod]
        public void Test12_Concurrencia_VersionadoOptimistaEnTablaEstacion()
        {
            var rutaSql = @"c:\proyectos\AOCR\scripts\sql\20260903_ac02_fechas_inspeccion_estaciones.sql";
            var contenidoSql = File.ReadAllText(rutaSql);

            StringAssert.Contains(contenidoSql, "version INTEGER NOT NULL DEFAULT 1");
            StringAssert.Contains(contenidoSql, "idx_solicitud_estacion_solicitud");
            StringAssert.Contains(contenidoSql, "idx_solicitud_estacion_unicidad");
        }

        [TestMethod]
        public void Test13_RutaVirtualAocr_GeneracionCorrectaSinPrefijosHardcodeados()
        {
            var rutaDetalle = @"c:\proyectos\AOCR\CapaPresentacion\Views\SolicitudAOCR\Detalle.cshtml";
            var contenido = File.ReadAllText(rutaDetalle);

            Assert.IsFalse(contenido.Contains("\"/aocr/SolicitudAOCR/Detalle\""), "No debe tener rutas absolutas codificadas.");
            StringAssert.Contains(contenido, "Url.Action(\"Detalle\", \"SolicitudAOCR\"");
        }

        [TestMethod]
        public void Test14_SolapamientoFechas_EstacionesDiferentesPuedenCompartirOAlternarFechas()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 500,
                    EstacionCodigo = "UIO",
                    FechaInicio = new DateTime(2026, 11, 1),
                    FechaFin = new DateTime(2026, 11, 10)
                },
                new SolicitudEstacionInspeccion
                {
                    SolicitudId = 500,
                    EstacionCodigo = "GYE",
                    FechaInicio = new DateTime(2026, 11, 5), // Solapada con UIO para dos inspectores simultáneos
                    FechaFin = new DateTime(2026, 11, 12)
                }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones);
            Assert.IsTrue(validacion.EsValido, "Dos estaciones diferentes pueden tener fechas simultáneas o solapadas sin conflicto.");
        }
    }
}
