using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CapaNegocio.Services
{
    public enum DocumentoSeguroError { Ninguno, Prohibido, NoEncontrado, Vacio, Extension, Contenido }

    public sealed class DocumentoSeguroResultado
    {
        public bool EsValido { get; set; }
        public DocumentoSeguroError Error { get; set; }
        public string RutaFisica { get; set; }
        public string NombreDescarga { get; set; }
        public string Mime { get; set; }
        public long Longitud { get; set; }
        public string MensajePublico { get; set; }
    }

    /// <summary>Validador único fail-closed para documentos institucionales descargables.</summary>
    public sealed class DocumentoSeguroService
    {
        private readonly IList<string> _raices;
        private readonly Action<string> _auditoria;

        public DocumentoSeguroService(IEnumerable<string> raicesPermitidas, Action<string> auditoria = null)
        {
            _raices = (raicesPermitidas ?? Enumerable.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(Path.GetFullPath).Select(r => r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _auditoria = auditoria;
        }

        public DocumentoSeguroResultado Resolver(int documentoId, int solicitudEsperadaId, int solicitudDocumentoId,
            string rutaPersistida, string nombreSugerido, Func<string, string> resolverVirtual)
        {
            if (documentoId <= 0 || solicitudEsperadaId <= 0 || solicitudDocumentoId != solicitudEsperadaId)
                return Error(DocumentoSeguroError.Prohibido, "El documento no pertenece al expediente solicitado.");
            if (string.IsNullOrWhiteSpace(rutaPersistida) || ContieneControl(rutaPersistida) || rutaPersistida.Contains(".."))
                return Error(DocumentoSeguroError.Prohibido, "Documento no disponible.");
            try
            {
                string candidata;
                if ((rutaPersistida.StartsWith("~/", StringComparison.Ordinal)
                        || rutaPersistida.StartsWith("/", StringComparison.Ordinal))
                    && resolverVirtual != null)
                    candidata = resolverVirtual(rutaPersistida);
                else if (Path.IsPathRooted(rutaPersistida)) candidata = rutaPersistida;
                else if (resolverVirtual != null) candidata = resolverVirtual(rutaPersistida);
                else return Error(DocumentoSeguroError.Prohibido, "Documento no disponible.");

                var full = Path.GetFullPath(candidata);
                if (full.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) || !_raices.Any(r => EsDescendiente(full, r)))
                    return Error(DocumentoSeguroError.Prohibido, "Documento no disponible.");
                if (!File.Exists(full)) return Error(DocumentoSeguroError.NoEncontrado, "Documento no encontrado.");
                if (TieneReparsePoint(full)) return Error(DocumentoSeguroError.Prohibido, "Documento no disponible.");
                var info = new FileInfo(full);
                if ((info.Attributes & FileAttributes.Directory) != 0 || info.Length <= 0)
                    return Error(DocumentoSeguroError.Vacio, "Documento vacío o no disponible.");

                var ext = (Path.GetExtension(full) ?? string.Empty).ToLowerInvariant();
                var mime = MimePermitido(ext);
                if (mime == null) return Error(DocumentoSeguroError.Extension, "Tipo de documento no permitido.");
                if (!ContenidoCompatible(full, ext)) return Error(DocumentoSeguroError.Contenido, "El contenido no corresponde al tipo de documento.");

                var result = new DocumentoSeguroResultado { EsValido = true, RutaFisica = full,
                    NombreDescarga = NormalizarNombreDescarga(nombreSugerido, ext), Mime = mime, Longitud = info.Length };
                _auditoria?.Invoke("DESCARGA_DOCUMENTO_OK;DocumentoId=" + documentoId + ";SolicitudId=" + solicitudEsperadaId + ";Bytes=" + info.Length);
                return result;
            }
            catch
            {
                return Error(DocumentoSeguroError.Prohibido, "Documento no disponible.");
            }
        }

        public static string NormalizarNombreDescarga(string nombre, string extension)
        {
            var ext = (extension ?? string.Empty).Trim();
            if (!ext.StartsWith(".")) ext = "." + ext.TrimStart('.');
            var candidato = new string((nombre ?? "documento").Where(c => !char.IsControl(c)).ToArray())
                .Replace('\\', '/');
            candidato = candidato.Substring(candidato.LastIndexOf('/') + 1);
            var ultimoPunto = candidato.LastIndexOf('.');
            var baseName = ultimoPunto > 0 ? candidato.Substring(0, ultimoPunto) : candidato;
            var sb = new StringBuilder();
            foreach (var c in baseName)
                if (!char.IsControl(c) && c != '"' && c != '\'' && c != '\\' && c != '/' && c != ':' && c != ';') sb.Append(c);
            var limpio = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(limpio)) limpio = "documento";
            return limpio.Length > 100 ? limpio.Substring(0, 100) + ext : limpio + ext;
        }

        private static bool EsDescendiente(string full, string root) => string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static bool ContieneControl(string value) => value.Any(char.IsControl);
        private static bool TieneReparsePoint(string full)
        {
            var actual = new FileInfo(full).Directory;
            while (actual != null)
            {
                if ((actual.Attributes & FileAttributes.ReparsePoint) != 0) return true;
                actual = actual.Parent;
            }
            return (new FileInfo(full).Attributes & FileAttributes.ReparsePoint) != 0;
        }
        private static string MimePermitido(string ext)
        {
            switch (ext) { case ".pdf": return "application/pdf"; case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png"; case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel"; case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; default: return null; }
        }
        private static bool ContenidoCompatible(string path, string ext)
        {
            var b = new byte[8]; int n; using (var s = File.OpenRead(path)) n = s.Read(b, 0, b.Length);
            if (ext == ".pdf") return n >= 5 && Encoding.ASCII.GetString(b, 0, 5) == "%PDF-";
            if (ext == ".jpg" || ext == ".jpeg") return n >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            if (ext == ".png") return n >= 8 && b.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
            if (ext == ".doc" || ext == ".xls") return n >= 8 && b.Take(8).SequenceEqual(new byte[] {208,207,17,224,161,177,26,225});
            return (ext == ".docx" || ext == ".xlsx") && n >= 4 && b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04;
        }
        private static DocumentoSeguroResultado Error(DocumentoSeguroError error, string message) =>
            new DocumentoSeguroResultado { EsValido = false, Error = error, MensajePublico = message };
    }
}
