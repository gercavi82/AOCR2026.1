using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CapaDatos.DAOs;
using CapaNegocio.DTOs.DocumentosPdf;

namespace CapaNegocio.Services
{
    public sealed class DocumentoPdfConsistenciaService
    {
        private readonly string _root;
        private readonly DocumentoPdfDAO _dao;
        private readonly IDocumentoPdfService _pdf;

        public DocumentoPdfConsistenciaService(string almacenamientoProtegido)
        {
            _root = Path.GetFullPath(almacenamientoProtegido);
            _dao = new DocumentoPdfDAO();
            _pdf = new DocumentoPdfService(_root);
        }

        public DocumentoPdfConsistenciaResultado Ejecutar()
        {
            var result = new DocumentoPdfConsistenciaResultado();
            var registros = _dao.ObtenerTodosOficiales();
            result.RegistrosAnalizados = registros.Count;
            var rutasRegistradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registro in registros)
            {
                var validacion = _pdf.ValidarArchivo(registro.Id);
                if (!validacion.Valido) result.Hallazgos.Add(new DocumentoPdfConsistenciaHallazgo
                {
                    Codigo = validacion.Codigo == 404 ? "REGISTRO_SIN_ARCHIVO" : "INTEGRIDAD_INVALIDA",
                    DocumentoPdfId = registro.Id, RutaLogica = registro.RutaLogica, Detalle = validacion.Mensaje
                });
                try { rutasRegistradas.Add(Resolver(registro.RutaLogica)); }
                catch (Exception ex) { result.Hallazgos.Add(new DocumentoPdfConsistenciaHallazgo { Codigo="RUTA_INSEGURA",DocumentoPdfId=registro.Id,RutaLogica=registro.RutaLogica,Detalle=ex.Message }); }
            }

            var archivos = Directory.Exists(_root) ? Directory.GetFiles(_root, "*.pdf", SearchOption.AllDirectories) : new string[0];
            result.ArchivosAnalizados = archivos.Length;
            foreach (var archivo in archivos.Select(Path.GetFullPath))
                if (!rutasRegistradas.Contains(archivo)) result.Hallazgos.Add(new DocumentoPdfConsistenciaHallazgo { Codigo="ARCHIVO_SIN_REGISTRO",Detalle=archivo });

            foreach (var duplicado in registros.GroupBy(x => x.InspeccionId + "|" + (x.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant() + "|" + x.Version).Where(x => x.Count() > 1))
                result.Hallazgos.Add(new DocumentoPdfConsistenciaHallazgo { Codigo="VERSION_DUPLICADA",Detalle=duplicado.Key + ";Ids=" + string.Join(",", duplicado.Select(x => x.Id)) });
            Trace.TraceInformation("[PDF][CONSISTENCY] Registros=" + result.RegistrosAnalizados + ";Archivos=" + result.ArchivosAnalizados + ";Hallazgos=" + result.Hallazgos.Count);
            return result;
        }

        private string Resolver(string ruta)
        {
            var normalizada = (ruta ?? string.Empty).Replace('\\','/');
            const string a = "~/App_Data/AOCR/", b = "/App_Data/AOCR/";
            var relativa = normalizada.StartsWith(a,StringComparison.OrdinalIgnoreCase) ? normalizada.Substring(a.Length)
                : normalizada.StartsWith(b,StringComparison.OrdinalIgnoreCase) ? normalizada.Substring(b.Length) : null;
            if (relativa == null) throw new InvalidOperationException("Ruta fuera de App_Data/AOCR.");
            var full = Path.GetFullPath(Path.Combine(_root, relativa.Replace('/',Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path traversal detectado.");
            return full;
        }
    }
}
