using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class LogController : Controller
    {
        // GET: Log
        public ActionResult Index()
        {
            return View(LogBL.ObtenerUltimos(200));
        }
    }
}