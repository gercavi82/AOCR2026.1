using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using Newtonsoft.Json;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// API Controller para configuraciones del sistema
    /// Provee valores configurables para eliminar hardcoded values
    /// </summary>
    [Authorize(Roles = "Administrador,Inspector,Coordinador,DIRDAC,Financiero,RT")]
    public class ConfigApiController : Controller
    {
        private readonly ParametroDAO _parametroDAO;

        public ConfigApiController()
        {
            _parametroDAO = new ParametroDAO();
        }

        /// <summary>
        /// Obtiene valores de test configurables para JavaScript
        /// GET /ConfigApi/TestValues
        /// </summary>
        [HttpGet]
        public ActionResult TestValues()
        {
            try
            {
                var valores = _parametroDAO.ObtenerValoresTest();

                var response = new
                {
                    success = true,
                    data = new
                    {
                        operadorDefecto = valores.ContainsKey("TEST_OPERADOR_DEFECTO") 
                            ? valores["TEST_OPERADOR_DEFECTO"] 
                            : "EMPRESA DEMO S.A.",
                        
                        representanteDefecto = valores.ContainsKey("TEST_REPRESENTANTE_DEFECTO") 
                            ? valores["TEST_REPRESENTANTE_DEFECTO"] 
                            : "Juan Carlos Pérez Demo",
                        
                        cedulaDefecto = valores.ContainsKey("TEST_CEDULA_DEFECTO") 
                            ? valores["TEST_CEDULA_DEFECTO"] 
                            : "0999999999",
                        
                        direccionDefecto = valores.ContainsKey("TEST_DIRECCION_DEFECTO") 
                            ? valores["TEST_DIRECCION_DEFECTO"] 
                            : "Av. Amazonas N24-03 y Colón, Quito, Ecuador",
                        
                        telefonoDefecto = valores.ContainsKey("TEST_TELEFONO_DEFECTO") 
                            ? valores["TEST_TELEFONO_DEFECTO"] 
                            : "02-2234567",
                        
                        emailDefecto = valores.ContainsKey("TEST_EMAIL_DEFECTO") 
                            ? valores["TEST_EMAIL_DEFECTO"] 
                            : "demo@ejemplo-dgac.gob.ec",
                        
                        rucDefecto = valores.ContainsKey("TEST_RUC_DEFECTO") 
                            ? valores["TEST_RUC_DEFECTO"] 
                            : "1790000000001",
                        
                        razonSocialDefecto = valores.ContainsKey("TEST_RAZON_SOCIAL_DEFECTO") 
                            ? valores["TEST_RAZON_SOCIAL_DEFECTO"] 
                            : "EMPRESA DEMO SERVICIOS AÉREOS S.A.",
                        
                        descripcionDefecto = valores.ContainsKey("TEST_DESCRIPCION_DEFECTO") 
                            ? valores["TEST_DESCRIPCION_DEFECTO"] 
                            : "Operaciones de demostración y pruebas del sistema AOCR",
                        
                        observacionesDefecto = valores.ContainsKey("TEST_OBSERVACIONES_DEFECTO") 
                            ? valores["TEST_OBSERVACIONES_DEFECTO"] 
                            : "Datos de prueba - No usar en producción"
                    },
                    timestamp = DateTime.Now
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    message = "Error al obtener valores de test: " + ex.Message,
                    timestamp = DateTime.Now
                };

                return Json(errorResponse, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Obtiene configuración de PDF para eliminar hardcoded en generación
        /// GET /ConfigApi/PdfConfig
        /// </summary>
        [HttpGet]
        public ActionResult PdfConfig()
        {
            try
            {
                var config = _parametroDAO.ObtenerConfiguracionPDF();

                var response = new
                {
                    success = true,
                    data = config,
                    timestamp = DateTime.Now
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    message = "Error al obtener configuración PDF: " + ex.Message,
                    timestamp = DateTime.Now
                };

                return Json(errorResponse, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Obtiene montos de demostración configurables
        /// GET /ConfigApi/DemoAmounts
        /// </summary>
        [HttpGet]
        public ActionResult DemoAmounts()
        {
            try
            {
                var montos = _parametroDAO.ObtenerMontosDemo();

                var response = new
                {
                    success = true,
                    data = montos,
                    timestamp = DateTime.Now
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    message = "Error al obtener montos de demo: " + ex.Message,
                    timestamp = DateTime.Now
                };

                return Json(errorResponse, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Obtiene todas las configuraciones en una sola llamada
        /// GET /ConfigApi/All
        /// </summary>
        [HttpGet]
        public ActionResult All()
        {
            try
            {
                var valoresTest = _parametroDAO.ObtenerValoresTest();
                var configPdf = _parametroDAO.ObtenerConfiguracionPDF();
                var montosDemo = _parametroDAO.ObtenerMontosDemo();

                var response = new
                {
                    success = true,
                    data = new
                    {
                        testValues = valoresTest,
                        pdfConfig = configPdf,
                        demoAmounts = montosDemo
                    },
                    timestamp = DateTime.Now
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    message = "Error al obtener configuraciones: " + ex.Message,
                    timestamp = DateTime.Now
                };

                return Json(errorResponse, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Health check para verificar disponibilidad del API
        /// GET /ConfigApi/Health
        /// </summary>
        [HttpGet]
        public ActionResult Health()
        {
            return Json(new 
            { 
                status = "OK", 
                service = "ConfigApi", 
                version = "1.0.0",
                timestamp = DateTime.Now 
            }, JsonRequestBehavior.AllowGet);
        }
    }
}