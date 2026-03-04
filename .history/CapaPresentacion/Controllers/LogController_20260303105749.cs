using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class LogController : Controller
    {
        // GET: Log
        public ActionResult Index()
        {
            return View(new List<object>());
        }
    }
}