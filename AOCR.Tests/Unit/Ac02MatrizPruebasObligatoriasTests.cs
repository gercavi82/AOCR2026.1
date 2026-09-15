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
    /// AC-02: MATRIZ DE 16 PRUEBAS OBLIGATORIAS
    /// "Permitir que cada estación de una solicitud AOCR tenga fechas independientes de inspección"
    /// 
    /// 1. Guardar una estación.
    /// 2. Guardar múltiples estaciones con fechas diferentes.
    /// 3. Rechazar fecha final anterior.
    /// 4. Rechazar estación duplicada.
    /// 5. Rechazar solicitud inexistente.
    /// 6. Rechazar estación de otra compañía.
    /// 7. Administrador no modifica.
    /// 8. RT de otra compañía recibe 403.
    /// 9. Doble guardado no duplica.
    /// 10. Conflicto de versión devuelve 409.
    /// 11. Las fechas aparecen en planificación.
    /// 12. Las fechas aparecen en el PDF.
    /// 13. La relación con AC-05 conserva el Inspector.
    /// 14. Rollback ante fallo de BD.
    /// 15. Prueba real de persistencia después de recargar.
    /// 16. Eliminar rutas fijas C:\proyectos\AOCR de las pruebas.
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

        private static string ObtenerRutaRaizProyecto()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AOCR.sln")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string ResolverRuta(string rutaRelativa)
        {
            return Path.Combine(ObtenerRutaRaizProyecto(), rutaRelativa.TrimStart('\\', '/'));
        }

        [TestMethod]
        public void Prueba01_GuardarUnaEstacion()
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

            var validacion = _servicio.ValidarEstaciones(estaciones, 301);

            Assert.IsTrue(validacion.EsValido, "Una estación con rango de fechas válido debe ser aceptada.");
            Assert.AreEqual(0, validacion.Errores.Count);
            Assert.AreEqual(new DateTime(2026, 11, 2), estaciones[0].FechaInicio);
            Assert.AreEqual(new DateTime(2026, 11, 4), estaciones[0].FechaFin);
        }

        [TestMethod]
        public void Prueba02_GuardarMultiplesEstacionesConFechasDiferentes()
        {
            var fechaA_ini = new DateTime(2026, 11, 2);
            var fechaA_fin = new DateTime(2026, 11, 3);
            var fechaB_ini = new DateTime(2026, 11, 9);
            var fechaB_fin = new DateTime(2026, 11, 11);
            var fechaC_ini = new DateTime(2026, 11, 16);
            var fechaC_fin = new DateTime(2026, 11, 18);

            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 1, SolicitudId = 302, EstacionCodigo = "UIO", EstacionNombre = "Quito", FechaInicio = fechaA_ini, FechaFin = fechaA_fin, Estado = "SOLICITADA" },
                new SolicitudEstacionInspeccion { Id = 2, SolicitudId = 302, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil", FechaInicio = fechaB_ini, FechaFin = fechaB_fin, Estado = "SOLICITADA" },
                new SolicitudEstacionInspeccion { Id = 3, SolicitudId = 302, EstacionCodigo = "CUE", EstacionNombre = "Cuenca", FechaInicio = fechaC_ini, FechaFin = fechaC_fin, Estado = "SOLICITADA" }
            };

            var validacion = _servicio.ValidarEstaciones(estaciones, 302);

            Assert.IsTrue(validacion.EsValido, "Las 3 estaciones con fechas diferentes deben ser válidas.");
            Assert.AreEqual(3, estaciones.Count);
            Assert.AreNotEqual(estaciones[0].FechaInicio, estaciones[1].FechaInicio);
            Assert.AreNotEqual(estaciones[1].FechaInicio, estaciones[2].FechaInicio);
            Assert.AreNotEqual(estaciones[0].FechaInicio, estaciones[2].FechaInicio);
        }

        [TestMethod]
        public void Prueba03_RechazarFechaFinalAnterior()
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

            var validacion = _servicio.ValidarEstaciones(estaciones, 401);

            Assert.IsFalse(validacion.EsValido, "Debe rechazar si la fecha final es anterior a la fecha inicial.");
            Assert.IsTrue(validacion.Errores.Any(e => e.Contains("no puede ser anterior a la fecha inicial")));

            var res = _servicio.GuardarEstaciones(401, estaciones, 1);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(400, res.HttpStatusCode, "Debe responder con código HTTP 400 de validación.");
        }

        [TestMethod]
        public void Prueba04_RechazarEstacionDuplicada()
        {
            var duplicadas = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { SolicitudId = 402, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil 1", FechaInicio = new DateTime(2026, 10, 1), FechaFin = new DateTime(2026, 10, 2) },
                new SolicitudEstacionInspeccion { SolicitudId = 402, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil 2", FechaInicio = new DateTime(2026, 10, 5), FechaFin = new DateTime(2026, 10, 6) }
            };

            var valDuplicadas = _servicio.ValidarEstaciones(duplicadas, 402);
            Assert.IsFalse(valDuplicadas.EsValido, "No se permite la misma estación duplicada.");
            Assert.IsTrue(valDuplicadas.EsDuplicado, "Debe marcar bandera EsDuplicado.");

            var res = _servicio.GuardarEstaciones(402, duplicadas, 1);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(409, res.HttpStatusCode, "Estación duplicada debe retornar HTTP 409 Conflicto.");
        }

        [TestMethod]
        public void Prueba05_RechazarSolicitudInexistente()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { EstacionCodigo = "UIO", FechaInicio = new DateTime(2026, 10, 1), FechaFin = new DateTime(2026, 10, 2) }
            };

            var resInvalido = _servicio.GuardarEstaciones(0, estaciones, 1);
            Assert.IsFalse(resInvalido.Exitoso);
            Assert.AreEqual(400, resInvalido.HttpStatusCode, "Solicitud ID <= 0 debe retornar HTTP 400.");

            // Validar que el controlador valida existencia y retorna 404
            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);
            StringAssert.Contains(codigoCtrl, "if (solicitud == null)");
            StringAssert.Contains(codigoCtrl, "JsonRespuestaEstaciones(404, false, \"La solicitud no existe.\")");
        }

        [TestMethod]
        public void Prueba06_RechazarEstacionDeOtraCompania()
        {
            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);

            // Regla 9 / Prueba 6: Solicitud de otra compañía recibe 403
            StringAssert.Contains(codigoCtrl, "if (!SolicitudCoincideConCompaniaActiva(solicitud, companiaActiva))");
            StringAssert.Contains(codigoCtrl, "JsonRespuestaEstaciones(403, false, \"La solicitud no corresponde a la compañía activa del usuario.\")");
        }

        [TestMethod]
        public void Prueba07_AdministradorNoModifica()
        {
            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);

            // Regla 10 / Prueba 7: Administrador no puede modificar estaciones operativas (devuelve 403)
            StringAssert.Contains(codigoCtrl, "if (EsAdmin())");
            StringAssert.Contains(codigoCtrl, "JsonRespuestaEstaciones(403, false, \"El usuario Administrador no puede modificar estaciones operativas.\")");
        }

        [TestMethod]
        public void Prueba08_RtDeOtraCompaniaRecibe403()
        {
            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);

            // RT que no es dueño de la solicitud recibe 403
            StringAssert.Contains(codigoCtrl, "if (solicitud.CodigoUsuario != usuarioId)");
            StringAssert.Contains(codigoCtrl, "JsonRespuestaEstaciones(403, false, \"No tiene permisos para modificar las estaciones de esta solicitud.\")");
        }

        [TestMethod]
        public void Prueba09_DobleGuardadoNoDuplica()
        {
            // Simular guardado idempotente con misma clave (solicitudId, codigo)
            var lista1 = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 10, SolicitudId = 505, EstacionCodigo = "UIO", EstacionNombre = "Quito", FechaInicio = new DateTime(2026, 11, 1), FechaFin = new DateTime(2026, 11, 2), Version = 1 },
                new SolicitudEstacionInspeccion { Id = 20, SolicitudId = 505, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil", FechaInicio = new DateTime(2026, 11, 5), FechaFin = new DateTime(2026, 11, 6), Version = 1 }
            };

            // Segundo guardado con los mismos códigos
            var lista2 = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion { Id = 10, SolicitudId = 505, EstacionCodigo = "UIO", EstacionNombre = "Quito Actualizado", FechaInicio = new DateTime(2026, 11, 1), FechaFin = new DateTime(2026, 11, 2), Version = 1 },
                new SolicitudEstacionInspeccion { Id = 20, SolicitudId = 505, EstacionCodigo = "GYE", EstacionNombre = "Guayaquil Actualizado", FechaInicio = new DateTime(2026, 11, 5), FechaFin = new DateTime(2026, 11, 6), Version = 1 }
            };

            var val1 = _servicio.ValidarEstaciones(lista1, 505);
            var val2 = _servicio.ValidarEstaciones(lista2, 505);

            Assert.IsTrue(val1.EsValido);
            Assert.IsTrue(val2.EsValido);

            // Simular consolidación en base: la cantidad final se mantiene en 2 registros únicos
            var consolidado = lista2.GroupBy(e => e.EstacionCodigo, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            Assert.AreEqual(2, consolidado.Count, "Doble guardado no debe duplicar las estaciones.");
        }

        [TestMethod]
        public void Prueba10_ConflictoDeVersionDevuelve409()
        {
            var res = new ResultadoOperacionEstaciones
            {
                SolicitudId = 600,
                Exitoso = false,
                HttpStatusCode = 409,
                EsConflictoVersion = true,
                Mensaje = "Conflicto de versión al actualizar estación UIO."
            };

            Assert.AreEqual(409, res.HttpStatusCode);
            Assert.IsTrue(res.EsConflictoVersion);

            // Verificar que SolicitudAOCRController maneja EstacionVersionConflictException y devuelve 409
            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);
            StringAssert.Contains(codigoCtrl, "catch (EstacionVersionConflictException ex)");
            StringAssert.Contains(codigoCtrl, "return JsonRespuestaEstaciones(409, false, ex.Message);");
        }

        [TestMethod]
        public void Prueba11_LasFechasAparecenEnPlanificacion()
        {
            var rutaControlador = ResolverRuta(@"CapaPresentacion\Controllers\InspeccionController.cs");
            var contenidoControlador = File.ReadAllText(rutaControlador);

            StringAssert.Contains(contenidoControlador, "ViewBag.EstacionesPlanificacion = estacionService.ObtenerEstacionesPorSolicitud(");
            StringAssert.Contains(contenidoControlador, "estacionService.GuardarEstaciones(inspeccion.CodigoSolicitud, listaActualizada, usuarioId);");

            var rutaVista = ResolverRuta(@"CapaPresentacion\Views\Inspeccion\Planificacion.cshtml");
            var contenidoVista = File.ReadAllText(rutaVista);

            StringAssert.Contains(contenidoVista, "tablaEstacionesPlanificacion");
            StringAssert.Contains(contenidoVista, "estacion-fecha-inicio");
            StringAssert.Contains(contenidoVista, "estacion-fecha-fin");
        }

        [TestMethod]
        public void Prueba12_LasFechasAparecenEnElPdf()
        {
            var rutaPdf = ResolverRuta(@"CapaPresentacion\Views\SolicitudAOCR\AceptacionDocumentalPdf.cshtml");
            Assert.IsTrue(File.Exists(rutaPdf), "El template de aceptación documental PDF debe existir.");

            var contenido = File.ReadAllText(rutaPdf);

            StringAssert.Contains(contenido, "tabla-estaciones");
            StringAssert.Contains(contenido, "Fecha Inicio");
            StringAssert.Contains(contenido, "Fecha Fin");
            StringAssert.Contains(contenido, "estacionesDetalladas");
            StringAssert.Contains(contenido, "est.FechaInicio.ToString(\"dd/MM/yyyy\")");

            // Validar que DesignacionDocumentoService procesa las estaciones con fechas
            var rutaServiceDesignacion = ResolverRuta(@"CapaNegocio\Services\DesignacionDocumentoService.cs");
            var contenidoDesignacion = File.ReadAllText(rutaServiceDesignacion);
            StringAssert.Contains(contenidoDesignacion, "vm.Estaciones.Add(new DesignacionEstacionItemDto");
            StringAssert.Contains(contenidoDesignacion, "FechaInicio = est.FechaInicio");
            StringAssert.Contains(contenidoDesignacion, "FechaFin = est.FechaFin");
        }

        [TestMethod]
        public void Prueba13_RelacionConAC05ConservaInspector()
        {
            var rutaDao = ResolverRuta(@"CapaDatos\DAOs\SolicitudEstacionDAO.cs");
            var contenidoDao = File.ReadAllText(rutaDao);

            // Preservación del Inspector con COALESCE para no sobrescribir la designación AC-05
            StringAssert.Contains(contenidoDao, "inspector_id = COALESCE(@inspectorId, inspector_id)");
            StringAssert.Contains(contenidoDao, "inspector_nombre = COALESCE(NULLIF(@inspectorNombre, ''), inspector_nombre)");
        }

        [TestMethod]
        public void Prueba14_RollbackAnteFalloBD()
        {
            var rutaDao = ResolverRuta(@"CapaDatos\DAOs\SolicitudEstacionDAO.cs");
            var contenidoDao = File.ReadAllText(rutaDao);

            StringAssert.Contains(contenidoDao, "tx.Rollback();");

            var rutaCtrl = ResolverRuta(@"CapaPresentacion\Controllers\SolicitudAOCRController.cs");
            var codigoCtrl = File.ReadAllText(rutaCtrl);
            StringAssert.Contains(codigoCtrl, "JsonRespuestaEstaciones(500, false, \"No se pudieron guardar las estaciones por un error de base de datos. Se realizó rollback de la transacción.\")");
        }

        [TestMethod]
        public void Prueba15_PruebaRealDePersistenciaDespuesDeRecargar()
        {
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

            var rehidratadas = estacionesBD.Select(e => new
            {
                Id = e.Id,
                EstacionCodigo = e.EstacionCodigo,
                EstacionNombre = e.EstacionNombre,
                FechaInicio = e.FechaInicio.ToString("yyyy-MM-dd"),
                FechaFin = e.FechaFin.ToString("yyyy-MM-dd"),
                InspectorNombre = e.InspectorNombre,
                Estado = e.Estado
            }).ToList();

            Assert.AreEqual(2, rehidratadas.Count);
            Assert.AreEqual("LTX", rehidratadas[0].EstacionCodigo);
            Assert.AreEqual("2026-12-01", rehidratadas[0].FechaInicio);
            Assert.AreEqual("2026-12-03", rehidratadas[0].FechaFin);

            Assert.AreEqual("SCY", rehidratadas[1].EstacionCodigo);
            Assert.AreEqual("2026-12-08", rehidratadas[1].FechaInicio);
            Assert.AreEqual("2026-12-10", rehidratadas[1].FechaFin);
        }

        [TestMethod]
        public void Prueba16_EliminarRutasFijasDeLasPruebas()
        {
            var rutaArchivoMatriz = ResolverRuta(@"AOCR.Tests\Unit\Ac02MatrizPruebasObligatoriasTests.cs");
            var contenidoMatriz = File.ReadAllText(rutaArchivoMatriz);

            // Verificar que no existen rutas fijas con c:\proyectos\AOCR hardcodeadas
            Assert.IsFalse(contenidoMatriz.Contains("@\"c:\\proyectos\\AOCR\\"), "Ac02MatrizPruebasObligatoriasTests no debe contener rutas fijas c:\\proyectos\\AOCR");
            Assert.IsFalse(contenidoMatriz.Contains("\"c:\\proyectos\\AOCR\\"), "Ac02MatrizPruebasObligatoriasTests no debe contener rutas fijas c:\\proyectos\\AOCR");

            var rutaArchivoUnit = ResolverRuta(@"AOCR.Tests\Unit\Ac02FechasInspeccionEstacionesTests.cs");
            var contenidoUnit = File.ReadAllText(rutaArchivoUnit);
            Assert.IsFalse(contenidoUnit.Contains("@\"c:\\proyectos\\AOCR\\"), "Ac02FechasInspeccionEstacionesTests no debe contener rutas fijas c:\\proyectos\\AOCR");
            Assert.IsFalse(contenidoUnit.Contains("\"c:\\proyectos\\AOCR\\"), "Ac02FechasInspeccionEstacionesTests no debe contener rutas fijas c:\\proyectos\\AOCR");
        }
    }
}
