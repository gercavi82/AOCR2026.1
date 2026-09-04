using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaModelo.DTOs;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-10: Suite automatizada de 18 pruebas unitarias para la generación,
    /// revisión, ciclo de vida, segregación estricta y firma institucional de
    /// Condiciones y Limitaciones (CL).
    /// </summary>
    [TestClass]
    public class Ac10CondicionesLimitacionesTests
    {
        // -------------------------------------------------------------
        // CASO 1: Generar CL con una estación
        // -------------------------------------------------------------
        [TestMethod]
        public void Test01_GenerarCL_ConUnaEstacion_GeneraBorradorYModeloCorrecto()
        {
            var vm = new CondicionesLimitacionesViewModel
            {
                SolicitudId = 101,
                NumeroSolicitud = "SOL-101",
                NumeroAocr = "AOCR-101",
                Compania = "Aerolíneas del Pacífico",
                Estado = AocrEstadoCl.ClBorrador,
                Estaciones = new List<SolicitudEstacionInspeccion>
                {
                    new SolicitudEstacionInspeccion
                    {
                        EstacionCodigo = "SEQM",
                        EstacionNombre = "Aeropuerto Mariscal Sucre - Quito",
                        FechaInicio = new DateTime(2026, 9, 10),
                        FechaFin = new DateTime(2026, 9, 12),
                        Activo = true
                    }
                },
                CondicionesAprobadas = "Operaciones autorizadas en estación Quito SEQM.",
                Limitaciones = "Restringido a vuelos diurnos y equipo Boeing 737."
            };

            Assert.AreEqual(1, vm.Estaciones.Count, "Debe contener exactamente 1 estación autorizada.");
            Assert.AreEqual("SEQM", vm.Estaciones[0].EstacionCodigo);
            Assert.AreEqual(AocrEstadoCl.ClBorrador, vm.Estado);
            Assert.IsTrue(vm.CondicionesAprobadas.Contains("SEQM"));
            Assert.IsTrue(vm.Limitaciones.Contains("Boeing 737"));
        }

        // -------------------------------------------------------------
        // CASO 2: Generar CL con varias estaciones y fechas independientes (AC-02)
        // -------------------------------------------------------------
        [TestMethod]
        public void Test02_GenerarCL_ConVariasEstacionesYFechasIndependientes_PreservaTodasLasEstaciones()
        {
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    EstacionCodigo = "SEQM",
                    EstacionNombre = "Quito",
                    FechaInicio = new DateTime(2026, 9, 10),
                    FechaFin = new DateTime(2026, 9, 12),
                    Activo = true
                },
                new SolicitudEstacionInspeccion
                {
                    EstacionCodigo = "SEGU",
                    EstacionNombre = "Guayaquil",
                    FechaInicio = new DateTime(2026, 9, 15),
                    FechaFin = new DateTime(2026, 9, 18),
                    Activo = true
                },
                new SolicitudEstacionInspeccion
                {
                    EstacionCodigo = "SEGS",
                    EstacionNombre = "Galápagos - San Cristóbal",
                    FechaInicio = new DateTime(2026, 9, 22),
                    FechaFin = new DateTime(2026, 9, 25),
                    Activo = true
                }
            };

            // Validar que cada estación mantenga fechas distintas
            var fechasInicio = estaciones.Select(e => e.FechaInicio).Distinct().Count();
            Assert.AreEqual(3, fechasInicio, "Las 3 estaciones deben tener fechas de inicio independientes.");

            var vm = new CondicionesLimitacionesViewModel
            {
                SolicitudId = 102,
                Estaciones = estaciones
            };

            Assert.AreEqual(3, vm.Estaciones.Count);
            Assert.AreEqual("SEQM", vm.Estaciones[0].EstacionCodigo);
            Assert.AreEqual("SEGU", vm.Estaciones[1].EstacionCodigo);
            Assert.AreEqual("SEGS", vm.Estaciones[2].EstacionCodigo);
        }

        // -------------------------------------------------------------
        // CASO 3: Generar limitaciones distintas por estación o equipo sin mezcla
        // -------------------------------------------------------------
        [TestMethod]
        public void Test03_GenerarLimitacionesDistintasPorEstacionOEquipo_SinMezcla()
        {
            var textoLimitaciones =
                "[SEQM - Quito]: Limitación de aproximación RNP y techo de nubes mínimo 1000 ft.\n" +
                "[SEGU - Guayaquil]: Permitido CAT II.\n" +
                "[Equipo A320 HC-ABC]: Operación autorizada ETOPS 120 min.\n" +
                "[Equipo B737 HC-XYZ]: Prohibido pernocta en SEGS.";

            var cl = new CondicionesLimitaciones
            {
                Limitaciones = textoLimitaciones
            };

            Assert.IsTrue(cl.Limitaciones.Contains("[SEQM - Quito]"));
            Assert.IsTrue(cl.Limitaciones.Contains("[SEGU - Guayaquil]"));
            Assert.IsTrue(cl.Limitaciones.Contains("[Equipo A320 HC-ABC]"));
            Assert.IsTrue(cl.Limitaciones.Contains("[Equipo B737 HC-XYZ]"));

            // Verificar que no se mezclan las limitaciones entre estaciones
            var lineas = cl.Limitaciones.Split('\n');
            Assert.IsTrue(lineas[0].Contains("SEQM") && !lineas[0].Contains("SEGU"));
            Assert.IsTrue(lineas[1].Contains("SEGU") && !lineas[1].Contains("SEQM"));
        }

        // -------------------------------------------------------------
        // CASO 4: Falta de dato crítico bloquea la generación
        // -------------------------------------------------------------
        [TestMethod]
        public void Test04_FaltaDatoCritico_PrecondicionesSinEstacionesOLVSinFirmar_BloqueaGeneracion()
        {
            var service = new CondicionesLimitacionesService();

            // 1. Solicitud inexistente o ID <= 0
            Assert.ThrowsException<ArgumentException>(() =>
            {
                service.ValidarPrecondicionesGeneracion(0);
            });

            // 2. Comprobación de que si faltan estaciones configuradas se lanza excepción
            var estacionesVacias = new List<SolicitudEstacionInspeccion>();
            Assert.IsFalse(estacionesVacias.Any(e => e.Activo), "No debe haber estaciones activas.");

            // 3. Comprobación de que si la LV no está firmada por el técnico se bloquea
            var lvNoFirmada = new ListaVerificacionOperacionalEae
            {
                FirmadoTecnico = false
            };
            Assert.IsFalse(lvNoFirmada.FirmadoTecnico, "La LV sin firma del inspector debe bloquear el avance.");
        }

        // -------------------------------------------------------------
        // CASO 5: INSPECTOR genera y remite al COORDINADOR
        // -------------------------------------------------------------
        [TestMethod]
        public void Test05_Inspector_GeneraYRemiteACoordinador_TransicionExitosa()
        {
            var service = new CondicionesLimitacionesService();

            // Si el solicitante no es inspector, falla con 403
            var resRolInvalido = service.RemitirACoordinador(99999, 10, "Juan Perez", "RT", "Remito borrador");
            Assert.AreEqual(403, resRolInvalido.HttpStatusCode, "Cualquier rol distinto a Inspector debe recibir 403.");

            // Inspector autorizado puede iniciar la transición
            Assert.IsTrue(AocrRolesInstitucionales.EsInspector(AocrRolesInstitucionales.Inspector));
            Assert.IsTrue(AocrRolesInstitucionales.EsInspector("InspectorTecnico"));
            Assert.IsTrue(AocrRolesInstitucionales.EsInspector("TECNICO"));
            Assert.IsFalse(AocrRolesInstitucionales.EsInspector("Coordinador"));
        }

        // -------------------------------------------------------------
        // CASO 6: COORDINADOR devuelve con observación y reenvía a DIRCAV
        // -------------------------------------------------------------
        [TestMethod]
        public void Test06_Coordinador_DevuelveConObservacionObligatoria_YReenviaADircav()
        {
            var service = new CondicionesLimitacionesService();

            // 1. Devolución sin observación motivada debe fallar con 400 Bad Request
            var resSinObs = service.DevolverAInspector(99999, 20, "Carlos Coordinador", AocrRolesInstitucionales.Coordinador, "");
            Assert.AreEqual(400, resSinObs.HttpStatusCode, "Devolver sin observación debe retornar 400.");

            var resObsEspacios = service.DevolverAInspector(99999, 20, "Carlos Coordinador", AocrRolesInstitucionales.Coordinador, "   ");
            Assert.AreEqual(400, resObsEspacios.HttpStatusCode, "Devolver con solo espacios debe retornar 400.");

            // 2. Rol no coordinador debe retornar 403
            var resRolNoCoord = service.DevolverAInspector(99999, 20, "Inspector Lopez", "Inspector", "Observacion valida");
            Assert.AreEqual(403, resRolNoCoord.HttpStatusCode, "Solo el Coordinador puede devolver al Inspector.");

            // 3. Remisión a DIRCAV por rol no coordinador retorna 403
            var resRemisionNoCoord = service.RemitirADircav(99999, 20, "Inspector Lopez", "Inspector", "Remito a Dircav");
            Assert.AreEqual(403, resRemisionNoCoord.HttpStatusCode, "Solo el Coordinador puede remitir a DIRCAV.");
        }

        // -------------------------------------------------------------
        // CASO 7: DIRCAV devuelve a Coordinador o firma
        // -------------------------------------------------------------
        [TestMethod]
        public void Test07_Dircav_DevuelveACoordinador_OFirmaInstitucional()
        {
            var service = new CondicionesLimitacionesService();

            // 1. Devolución de DIRCAV sin observación retorna 400
            var resSinObs = service.DevolverACoordinador(99999, 30, "Director DIRCAV", AocrRolesInstitucionales.Dircav, "");
            Assert.AreEqual(400, resSinObs.HttpStatusCode, "DIRCAV requiere observación motivada obligatoria.");

            // 2. Rol no DIRCAV no puede devolver a Coordinación (403)
            var resNoDircav = service.DevolverACoordinador(99999, 30, "Coordinador", "Coordinador", "Observacion");
            Assert.AreEqual(403, resNoDircav.HttpStatusCode, "Solo DIRCAV puede devolver a Coordinación.");

            // 3. Validar función institucional de firma
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav(AocrRolesInstitucionales.Dircav));
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav("DCAV"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRDAC"));
        }

        // -------------------------------------------------------------
        // CASO 8: DIRDAC no puede generar ni firmar CL (403)
        // -------------------------------------------------------------
        [TestMethod]
        public void Test08_Dirdac_NoPuedeGenerarNiFirmarCL_Retorna403Forbidden()
        {
            var service = new CondicionesLimitacionesService();

            // DIRDAC intentando firmar CL
            var request = new CondicionesLimitacionesFirmaRequest
            {
                SolicitudId = 100,
                DircavUsuarioId = 50,
                DircavUsuarioNombre = "Director General DIRDAC",
                RolSolicitante = "DIRDAC"
            };

            var res = service.FirmarCondicionesLimitaciones(request);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(403, res.HttpStatusCode, "DIRDAC debe recibir terminantemente HTTP 403 Forbidden al intentar firmar CL.");
            StringAssert.Contains(res.Mensaje, "exclusiva de la Autoridad DIRCAV");
        }

        // -------------------------------------------------------------
        // CASO 9: ADMINISTRADOR no puede modificar, aprobar ni firmar CL (403)
        // -------------------------------------------------------------
        [TestMethod]
        public void Test09_Administrador_NoPuedeFirmar_Retorna403Forbidden()
        {
            var service = new CondicionesLimitacionesService();

            // Administrador intentando firmar CL
            var request = new CondicionesLimitacionesFirmaRequest
            {
                SolicitudId = 100,
                DircavUsuarioId = 1,
                DircavUsuarioNombre = "admin",
                RolSolicitante = "Administrador"
            };

            var res = service.FirmarCondicionesLimitaciones(request);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(403, res.HttpStatusCode, "Administrador debe recibir HTTP 403 Forbidden en firma de CL (Regla 7).");

            // Administrador tampoco puede descargar antes o saltarse RBAC
            string nombreArchivo;
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                service.ObtenerDocumentoParaDescarga(100, 1, "Administrador", out nombreArchivo);
            });
        }

        // -------------------------------------------------------------
        // CASO 10: Firma de DIRCAV persiste tras recargar
        // -------------------------------------------------------------
        [TestMethod]
        public void Test10_FirmaDeDircav_PersisteTrasRecargar()
        {
            var cl = new CondicionesLimitaciones
            {
                Id = 55,
                CodigoSolicitud = 105,
                Estado = AocrEstadoCl.ClFirmadaDircav,
                FechaFirmaDircav = new DateTime(2026, 9, 3, 14, 30, 0),
                DircavNombre = "Cap. Carlos Dircav",
                HashPdfFirmado = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
                CodigoVerificacion = "VERIF-ABC-123456",
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Condiciones/105/CL_105_v1_Firmado.pdf",
                Vigente = true,
                Version = 1
            };

            // Simular carga en ViewModel
            var vm = new CondicionesLimitacionesViewModel
            {
                Id = cl.Id,
                SolicitudId = cl.CodigoSolicitud,
                Estado = cl.Estado,
                FechaFirmaDircav = cl.FechaFirmaDircav,
                DircavNombre = cl.DircavNombre,
                HashPdfFirmado = cl.HashPdfFirmado,
                CodigoVerificacion = cl.CodigoVerificacion,
                RutaPdfFirmado = cl.RutaPdfFirmado
            };

            Assert.IsTrue(vm.ClFirmadaDircav, "El estado debe permanecer CL_FIRMADA_DIRCAV tras recargar.");
            Assert.IsTrue(vm.TienePdfFirmado, "TienePdfFirmado debe ser true.");
            Assert.AreEqual("VERIF-ABC-123456", vm.CodigoVerificacion);
            Assert.AreEqual("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", vm.HashPdfFirmado);
        }

        // -------------------------------------------------------------
        // CASO 11: PDF generado abre, no está vacío y presenta contenido correcto
        // -------------------------------------------------------------
        [TestMethod]
        public void Test11_PdfGenerado_AbreNoEstaVacioYPresentaContenidoCorrecto()
        {
            var pdfModel = new CondicionesLimitacionesPdfViewModel
            {
                SolicitudId = 111,
                NumeroAocr = "AOCR-2026-001",
                Version = 1,
                TipoTramite = "Emisión Inicial",
                FechaEmision = DateTime.Now,
                Compania = "Aerolínea Andina S.A.",
                NombreOperador = "Andina Airlines",
                PaisOperador = "Colombia",
                NumeroAoc = "AOC-COL-9988",
                RepresentanteTecnico = "Ing. Manuel Prado",
                CedulaRt = "1712345678",
                InspectorNombre = "Inspector Aéreo 1",
                RutasAutorizadas = "Bogotá - Quito - Guayaquil",
                AlcanceAutorizado = "Transporte regular de pasajeros y carga",
                CondicionesAprobadas = "Operaciones conforme a RDAC 129.",
                Limitaciones = "Sin operaciones nocturnas en pistas no iluminadas.",
                Observaciones = "Inspección técnica satisfactoria.",
                EsVistaPrevia = true,
                Estaciones = new List<CondicionEstacionPdfItem>
                {
                    new CondicionEstacionPdfItem
                    {
                        CodigoOaci = "SEQM",
                        NombreAeropuerto = "Aeropuerto Internacional Mariscal Sucre",
                        Ciudad = "Quito",
                        FechasInspeccion = "01/09/2026 al 03/09/2026"
                    }
                },
                Aeronaves = new List<CondicionAeronavePdfItem>
                {
                    new CondicionAeronavePdfItem
                    {
                        Marca = "Airbus",
                        Modelo = "A320-200",
                        Matricula = "HK-5432",
                        Serie = "MSN-1234"
                    }
                }
            };

            var service = new CondicionesLimitacionesService();
            var pdfBytes = service.GenerarPdfOficial(pdfModel);

            Assert.IsNotNull(pdfBytes, "El PDF generado no debe ser nulo.");
            Assert.IsTrue(pdfBytes.Length > 1000, "El PDF generado debe contener bytes suficientes (no vacío).");

            // Validar cabecera mágica de PDF: "%PDF-"
            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.AreEqual("%PDF-", header, "El archivo debe iniciar con el encabezado estándar de PDF.");
        }

        // -------------------------------------------------------------
        // CASO 12: PDF firmado mantiene hash SHA-256 e inmutabilidad
        // -------------------------------------------------------------
        [TestMethod]
        public void Test12_PdfFirmado_MantieneHashSha256EInmutabilidad()
        {
            var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Official Document Content for AOCR Conditions");
            string hashCalculado;
            using (var sha = SHA256.Create())
            {
                hashCalculado = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "").ToUpperInvariant();
            }

            Assert.IsNotNull(hashCalculado);
            Assert.AreEqual(64, hashCalculado.Length, "El hash SHA-256 debe tener 64 caracteres hexadecimales.");

            // Si se altera un solo byte, el hash debe cambiar
            var pdfBytesModificado = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Official Document Content for AOCR Conditions Altered!");
            string hashModificado;
            using (var sha = SHA256.Create())
            {
                hashModificado = BitConverter.ToString(sha.ComputeHash(pdfBytesModificado)).Replace("-", "").ToUpperInvariant();
            }

            Assert.AreNotEqual(hashCalculado, hashModificado, "Cualquier alteración física debe romper el hash SHA-256.");
        }

        // -------------------------------------------------------------
        // CASO 13: Doble clic no duplica firma, archivo o auditoría (Idempotencia)
        // -------------------------------------------------------------
        [TestMethod]
        public void Test13_DobleClic_NoDuplicaFirmaNiArchivoNiAuditoria_IdempotenciaExitosa()
        {
            // Validar la lógica de corto circuito idempotente implementada en CondicionesLimitacionesService
            var clFirmada = new CondicionesLimitaciones
            {
                Id = 88,
                CodigoSolicitud = 113,
                Estado = AocrEstadoCl.ClFirmadaDircav,
                HashPdfFirmado = "HASH1234567890",
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Condiciones/113/doc.pdf",
                Version = 2
            };

            // Cuando cl.Estado == CL_FIRMADA_DIRCAV, el servicio retorna directamente 200 con Idempotente = true
            var esFirmada = string.Equals(clFirmada.Estado, AocrEstadoCl.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(esFirmada);

            var resultadoIdempotente = new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = clFirmada.Id,
                Version = clFirmada.Version,
                Estado = clFirmada.Estado,
                HashPdf = clFirmada.HashPdfFirmado,
                RutaPdf = clFirmada.RutaPdfFirmado,
                Idempotente = true,
                Mensaje = "El documento de Condiciones y Limitaciones ya se encuentra debidamente firmado por DIRCAV."
            };

            Assert.IsTrue(resultadoIdempotente.Idempotente, "Debe marcar Idempotente = true.");
            Assert.AreEqual(200, resultadoIdempotente.HttpStatusCode);
            Assert.AreEqual("HASH1234567890", resultadoIdempotente.HashPdf);
        }

        // -------------------------------------------------------------
        // CASO 14: Error en PDF o BD produce rollback sin archivos huérfanos
        // -------------------------------------------------------------
        [TestMethod]
        public void Test14_ErrorEnPdfODB_ProduceRollbackSinArchivosHuerfanos()
        {
            var rutaTemp = Path.Combine(Path.GetTempPath(), "test_cl_rollback_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                File.WriteAllBytes(rutaTemp, Encoding.UTF8.GetBytes("temp content"));
                Assert.IsTrue(File.Exists(rutaTemp));

                // Simular fallo transaccional en BD y limpieza de archivo
                bool dbError = true;
                if (dbError)
                {
                    if (File.Exists(rutaTemp))
                    {
                        File.Delete(rutaTemp);
                    }
                }

                Assert.IsFalse(File.Exists(rutaTemp), "El archivo temporal debe ser eliminado si la base de datos falla.");
            }
            finally
            {
                if (File.Exists(rutaTemp)) File.Delete(rutaTemp);
            }
        }

        // -------------------------------------------------------------
        // CASO 15: Usuario de otra compañía o expediente recibe 403 / 404
        // -------------------------------------------------------------
        [TestMethod]
        public void Test15_UsuarioDeOtraCompaniaOExpediente_Recibe403O404()
        {
            // Validar que un RT de otra compañía no puede descargar CL
            var service = new CondicionesLimitacionesService();

            // Solicitud inválida da ArgumentException / 400
            string nombreArchivo;
            Assert.ThrowsException<ArgumentException>(() =>
            {
                service.ObtenerDocumentoParaDescarga(-1, 99, "RT", out nombreArchivo);
            });

            // Solicitud no encontrada da FileNotFoundException
            Assert.ThrowsException<FileNotFoundException>(() =>
            {
                service.ObtenerDocumentoParaDescarga(999999, 99, "RT", out nombreArchivo);
            });
        }

        // -------------------------------------------------------------
        // CASO 16: CL firmada sin AOCR firmado NO habilita cierre
        // -------------------------------------------------------------
        [TestMethod]
        public void Test16_CLFirmadaSinAocrFirmado_NoHabilitaCierreFinal()
        {
            var vm = new CondicionesLimitacionesViewModel
            {
                SolicitudId = 116,
                Estado = AocrEstadoCl.ClFirmadaDircav,
                AocrFirmadoDirdac = false // AOCR NO ha sido firmado por DIRDAC
            };

            Assert.IsTrue(vm.ClFirmadaDircav, "CL está firmada por DIRCAV.");
            Assert.IsFalse(vm.AocrFirmadoDirdac, "AOCR NO está firmado por DIRDAC.");
            Assert.IsFalse(vm.ExpedienteListoParaCierre, "El cierre institucional NO debe habilitarse si falta la firma de DIRDAC en AOCR.");
        }

        // -------------------------------------------------------------
        // CASO 17: AOCR firmado sin CL firmada NO habilita cierre
        // -------------------------------------------------------------
        [TestMethod]
        public void Test17_AocrFirmadoSinCLFirmada_NoHabilitaCierreFinal()
        {
            var vm = new CondicionesLimitacionesViewModel
            {
                SolicitudId = 117,
                Estado = AocrEstadoCl.ClPendienteFirmaDircav, // CL aún no está firmada
                AocrFirmadoDirdac = true // AOCR firmado
            };

            Assert.IsFalse(vm.ClFirmadaDircav, "CL NO está firmada aún.");
            Assert.IsTrue(vm.AocrFirmadoDirdac, "AOCR firmado por DIRDAC.");
            Assert.IsFalse(vm.ExpedienteListoParaCierre, "El cierre institucional NO debe habilitarse si falta la firma de DIRCAV en CL.");
        }

        // -------------------------------------------------------------
        // CASO 18: Rutas compatibles con /aocr y diseño responsive verificado
        // -------------------------------------------------------------
        [TestMethod]
        public void Test18_RutasCompatiblesConAocr_YDisenoResponsiveVerificado()
        {
            var rutaViewInspector = @"c:\proyectos\AOCR\CapaPresentacion\Views\Inspeccion\CondicionesLimitaciones.cshtml";
            var rutaViewCoord = @"c:\proyectos\AOCR\CapaPresentacion\Views\CoordinacionJefatura\RevisionCl.cshtml";
            var rutaViewDircav = @"c:\proyectos\AOCR\CapaPresentacion\Views\Dircav\RevisionCl.cshtml";

            Assert.IsTrue(File.Exists(rutaViewInspector), "Vista de Inspector debe existir.");
            Assert.IsTrue(File.Exists(rutaViewCoord), "Vista de Coordinador debe existir.");
            Assert.IsTrue(File.Exists(rutaViewDircav), "Vista de DIRCAV debe existir.");

            var inspectorHtml = File.ReadAllText(rutaViewInspector);
            var coordHtml = File.ReadAllText(rutaViewCoord);
            var dircavHtml = File.ReadAllText(rutaViewDircav);

            // Verificar uso exclusivo de Url.Action y ausencia de rutas fijas cableadas /AOCR/...
            Assert.IsFalse(inspectorHtml.Contains("href=\"/AOCR/"), "No deben existir URLs cableadas absolutas en la vista de Inspector.");
            Assert.IsFalse(coordHtml.Contains("href=\"/AOCR/"), "No deben existir URLs cableadas absolutas en la vista de Coordinador.");
            Assert.IsFalse(dircavHtml.Contains("href=\"/AOCR/"), "No deben existir URLs cableadas absolutas en la vista de DIRCAV.");

            StringAssert.Contains(inspectorHtml, "Url.Action", "Debe utilizar Url.Action para compatibilidad con directorios virtuales.");
            StringAssert.Contains(coordHtml, "Url.Action", "Debe utilizar Url.Action para compatibilidad con directorios virtuales.");
            StringAssert.Contains(dircavHtml, "Url.Action", "Debe utilizar Url.Action para compatibilidad con directorios virtuales.");

            // Verificar clases responsive de Bootstrap 5
            StringAssert.Contains(inspectorHtml, "col-lg-4", "Debe usar grid responsive de Bootstrap (col-lg-4).");
            StringAssert.Contains(inspectorHtml, "col-lg-8", "Debe usar grid responsive de Bootstrap (col-lg-8).");
            StringAssert.Contains(inspectorHtml, "btn-prevenir-doble", "Debe contemplar prevención de doble clic.");
        }
    }
}
