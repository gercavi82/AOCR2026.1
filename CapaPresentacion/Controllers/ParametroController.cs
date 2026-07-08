using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ParametroController : Controller
    {
        // GET: Parametro
        public ActionResult Index()
        {
            try
            {
                var lista = ParametroBL.ListarTodos();
                return View(lista ?? new List<Parametro>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar parámetros: " + ex.Message;
                return View(new List<Parametro>());
            }
        }

        // GET: Parametro/Crear
        public ActionResult Crear()
        {
            return View(new Parametro { Activo = true });
        }

        // POST: Parametro/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Parametro parametro)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok = ParametroBL.Crear(parametro, codigoUsuario, out mensaje);

                if (ok)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction("Index");
                }

                TempData["Error"] = mensaje;
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear parámetro: " + ex.Message;
                return View(parametro);
            }
        }

        // GET: Parametro/Editar/5
        public ActionResult Editar(int id)
        {
            try
            {
                var parametro = ParametroBL.ObtenerPorId(id);
                if (parametro == null)
                {
                    TempData["Error"] = "Parámetro no encontrado.";
                    return RedirectToAction("Index");
                }
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar parámetro: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: Parametro/EditarPorClave?clave=TARIFA_EMI_AOCR
        public ActionResult EditarPorClave(string clave)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(clave))
                {
                    TempData["Error"] = "Clave de parámetro inválida.";
                    return RedirectToAction("Index");
                }

                var parametro = ParametroBL.ObtenerPorClave(clave.Trim());
                if (parametro == null)
                {
                    TempData["Error"] = "Parámetro no encontrado.";
                    return RedirectToAction("Index");
                }

                return View("Editar", parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar parámetro: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Parametro/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Parametro parametro)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok;

                if (parametro != null && parametro.CodigoParametro > 0)
                {
                    ok = ParametroBL.Actualizar(parametro, codigoUsuario, out mensaje);
                }
                else
                {
                    ok = ParametroBL.UpsertPorClave(parametro, codigoUsuario, out mensaje);
                }

                if (ok)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction("Index");
                }

                TempData["Error"] = mensaje;
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar parámetro: " + ex.Message;
                return View(parametro);
            }
        }

        // POST: Parametro/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok = ParametroBL.EliminarSoft(id, codigoUsuario, out mensaje);

                return Json(new { success = ok, mensaje });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error al eliminar: " + ex.Message });
            }
        }

        // POST: Parametro/AplicarTarifasAocrOficiales
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AplicarTarifasAocrOficiales()
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                var tarifas = ObtenerTarifasOficiales();
                var errores = new List<string>();

                foreach (var item in tarifas)
                {
                    string mensajeGuardar;
                    if (!ParametroBL.UpsertPorClave(item, codigoUsuario, out mensajeGuardar))
                    {
                        errores.Add(item.Clave + ": " + mensajeGuardar);
                    }
                }

                if (errores.Any())
                {
                    TempData["Error"] = "Se aplicaron tarifas con observaciones: " + string.Join(" | ", errores);
                }
                else
                {
                    TempData["Success"] = "Tarifas oficiales AOCR aplicadas correctamente.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error aplicando tarifas oficiales AOCR: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Parametro/SincronizarConceptosAocr
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SincronizarConceptosAocr()
        {
            try
            {
                var conceptoDao = new ConceptoDAO();
                var conceptos = ObtenerConceptosAocrConfigurables();
                foreach (var c in conceptos)
                {
                    conceptoDao.Upsert(c);
                }

                TempData["Success"] = "Conceptos AOCR sincronizados correctamente desde parametros.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error sincronizando conceptos AOCR: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        private static IEnumerable<Parametro> ObtenerTarifasOficiales()
        {
            return new[]
            {
                new Parametro
                {
                    Clave = "TARIFA_EMI_AOCR",
                    Valor = "3300.00",
                    Descripcion = "Tarifa para Emision / Renovacion / Modificacion AOCR"
                },
                new Parametro
                {
                    Clave = "TARIFA_REN_AOCR",
                    Valor = "3300.00",
                    Descripcion = "Tarifa para Renovacion AOCR"
                },
                new Parametro
                {
                    Clave = "TARIFA_MOD_AOCR_INC",
                    Valor = "1600.00",
                    Descripcion = "Tarifa para Modificacion AOCR con inclusion de aeronaves distinto modelo y tipo"
                },
                new Parametro
                {
                    Clave = "TARIFA_MOD_AOCR_SIN_INC",
                    Valor = "80.00",
                    Descripcion = "Tarifa para Modificacion AOCR que no implique incremento de aeronaves"
                },
                new Parametro
                {
                    Clave = "TARIFA_INSPECCION_EXT",
                    Valor = "500.00",
                    Descripcion = "Tarifa por estacion para inspeccion requerida por Operador Aereo Extranjero"
                },
                new Parametro
                {
                    Clave = "TARIFA_VIATICOS_INSPECTOR",
                    Valor = "80.00",
                    Descripcion = "Tarifa de viaticos por dia para inspectores"
                },
                new Parametro
                {
                    Clave = "PORCENTAJE_ADMIN_VIATICOS",
                    Valor = "8.00",
                    Descripcion = "Porcentaje de gastos administrativos sobre viaticos"
                }
            };
        }

        private IEnumerable<CapaDatos.Models.ConceptoModel> ObtenerConceptosAocrConfigurables()
        {
            return new List<CapaDatos.Models.ConceptoModel>
            {
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "EMI_AOCR",
                    Nombre = "Emision / Renovacion / Modificacion AOCR",
                    TipoCalculo = "FIJO",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_EMI_AOCR", 3300m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_EMI_AOCR", 0m),
                    Activo = true,
                    Orden = 1,
                    Descripcion = "Emision / Renovacion / Modificacion AOCR",
                    PorEstacion = false,
                    PorDia = false,
                    EsViatico = false
                },
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "REN_AOCR",
                    Nombre = "Renovacion AOCR",
                    TipoCalculo = "FIJO",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_REN_AOCR", 3300m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_REN_AOCR", 0m),
                    Activo = true,
                    Orden = 2,
                    Descripcion = "Renovacion AOCR",
                    PorEstacion = false,
                    PorDia = false,
                    EsViatico = false
                },
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "MOD_AOCR_INC",
                    Nombre = "Modificacion AOCR (Inclusion aeronaves distinto modelo y tipo)",
                    TipoCalculo = "FIJO",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_MOD_AOCR_INC", 1600m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_MOD", 0m),
                    Activo = true,
                    Orden = 3,
                    Descripcion = "Modificacion AOCR (Inclusion aeronaves distinto modelo y tipo)",
                    PorEstacion = false,
                    PorDia = false,
                    EsViatico = false
                },
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "MOD_AOCR_SIN_INC",
                    Nombre = "Modificacion AOCR (Que no implique incremento de aeronaves)",
                    TipoCalculo = "FIJO",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_MOD_AOCR_SIN_INC", 80m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_MOD", 0m),
                    Activo = true,
                    Orden = 4,
                    Descripcion = "Modificacion AOCR (Que no implique incremento de aeronaves)",
                    PorEstacion = false,
                    PorDia = false,
                    EsViatico = false
                },
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "INSPECCION_EXT",
                    Nombre = "Inspeccion requerida por el Operador Aereo Extranjero",
                    TipoCalculo = "POR_ESTACION",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_INSPECCION_EXT", 500m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_INSPECCION", 0m),
                    Activo = true,
                    Orden = 5,
                    Descripcion = "Inspeccion requerida por el Operador Aereo Extranjero (por estacion)",
                    PorEstacion = true,
                    PorDia = false,
                    EsViatico = false
                },
                new CapaDatos.Models.ConceptoModel
                {
                    Codigo = "VIATICOS_INSPECTOR",
                    Nombre = "Viaticos a Sres. Inspectores",
                    TipoCalculo = "POR_DIA",
                    ValorBase = ObtenerDecimalDesdeParametro("TARIFA_VIATICOS_INSPECTOR", 80m),
                    PorcentajeAdmin = ObtenerDecimalDesdeParametro("PORCENTAJE_ADMIN_VIATICOS", 8m),
                    Activo = true,
                    Orden = 6,
                    Descripcion = "Viaticos por dia (mas gastos administrativos)",
                    PorEstacion = false,
                    PorDia = true,
                    EsViatico = true
                }
            };
        }

        private static decimal ObtenerDecimalDesdeParametro(string clave, decimal valorPorDefecto)
        {
            var parametro = ParametroBL.ObtenerPorClave(clave);
            if (parametro == null || !parametro.Activo || string.IsNullOrWhiteSpace(parametro.Valor))
            {
                return valorPorDefecto;
            }

            var texto = parametro.Valor.Trim().Replace("$", "").Replace("USD", "").Replace(" ", "");

            decimal valor;
            if (decimal.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor))
            {
                return valor;
            }

            texto = texto.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor))
            {
                return valor;
            }

            return valorPorDefecto;
        }

        private int ObtenerCodigoUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int id))
            {
                return id;
            }
            return 0;
        }
    }
}
