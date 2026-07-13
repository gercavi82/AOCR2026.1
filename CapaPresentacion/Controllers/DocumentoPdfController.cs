using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using CapaNegocio.DTOs.DocumentosPdf;
using CapaNegocio.Services;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public sealed class DocumentoPdfController : AocrBaseController
    {
        public DocumentoPdfController(IUsuarioContextoService usuarios) : base(usuarios) { }

        [HttpGet]
        public ActionResult Descargar(int id)
        {
            try
            {
                var servicio = CrearServicio();
                var documento = servicio.ObtenerPorId(id);
                if (documento == null) return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Documento PDF inexistente.");
                var stream = servicio.ObtenerArchivoAutorizado(id, UsuarioActualId);
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                return File(stream, "application/pdf", documento.NombreArchivo);
            }
            catch (DocumentoPdfException ex) { return new HttpStatusCodeResult(ex.Codigo, ex.Message); }
        }

        private IDocumentoPdfService CrearServicio()
        {
            return new DocumentoPdfService(Server.MapPath("~/App_Data/AOCR"), AutorizarLectura);
        }

        private bool AutorizarLectura(int usuarioId, DocumentoPdfDto documento)
        {
            var usuario = UsuarioActual;
            if (usuario == null || !usuario.EstaAutenticado || !usuario.EsValido || usuario.UsuarioId != usuarioId) return false;
            if (usuario.EsInspectorTecnico)
                return new AocrAuthorizationService().PuedeInspectorAbrirInspeccion(documento.InspeccionId, usuarioId);
            if (usuario.EsAdministrador || usuario.EsCoordinacion || usuario.EsDireccionJefaturaTecnica) return true;
            var rol = Normalizar(usuario.RolActivo);
            if (new[] { "DCAV", "DIRECTORGENERAL", "DIRDAC", "JEFATURATECNICA" }.Contains(rol)) return true;
            return usuario.EsSolicitante && !string.IsNullOrWhiteSpace(usuario.CompaniaCodigo)
                && string.Equals(usuario.CompaniaCodigo, documento.CodigoCompania, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalizar(string valor)
        {
            return new string((valor ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
