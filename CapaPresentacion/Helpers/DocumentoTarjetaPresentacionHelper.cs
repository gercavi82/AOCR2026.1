using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.Models;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Helpers
{
    public static class DocumentoTarjetaPresentacionHelper
    {
        private static readonly CultureInfo CulturaEsEc = CapaNegocio.Helpers.CultureHelper.GetAocrCulture();

        private static readonly Dictionary<string, string> ConceptosVisibles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "INSPECCION_EXT", "Inspección requerida por el Operador Aéreo Extranjero" }
        };

        public static DocumentoTarjetaViewModel CrearTarjeta(DocumentoModel documento, int ordenId, UrlHelper url)
        {
            if (documento == null)
            {
                return new DocumentoTarjetaViewModel();
            }

            var metadata = ParsearMetadata(documento.Observaciones);
            var tipoCanonico = RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(documento.TipoDocumento);
            var esPdf = string.Equals(documento.Extension, ".pdf", StringComparison.OrdinalIgnoreCase)
                || (documento.NombreArchivo ?? string.Empty).EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            var tarjeta = new DocumentoTarjetaViewModel
            {
                CodigoDocumento = documento.CodigoDocumento,
                NombreArchivoCompleto = (documento.NombreArchivo ?? "Documento").Trim(),
                NombreArchivo = FormatearNombreArchivoVisible(documento.NombreArchivo),
                TipoDocumentoVisible = ObtenerTipoDocumentoVisible(documento.TipoDocumento, tipoCanonico),
                ConceptoVisible = ObtenerConceptoVisible(metadata),
                AeropuertosVisible = ObtenerAeropuertosVisible(metadata),
                FechaCargaVisible = FormatearFecha(documento.FechaCarga),
                EstadoVisible = ObtenerEstadoVisible(documento, metadata, tipoCanonico),
                EstadoBadgeCss = ObtenerEstadoBadgeCss(documento, metadata, tipoCanonico),
                TamanoVisible = documento.TamanoFormateado,
                IconoCss = esPdf ? "fas fa-file-pdf text-danger" : "fas fa-file-alt text-primary",
                MostrarDescarga = !string.IsNullOrWhiteSpace(documento.RutaGuardada) || EsDocumentoInspeccionExt(tipoCanonico),
                UrlDescarga = ResolverUrlDescarga(documento, ordenId, tipoCanonico, url)
            };

            return tarjeta;
        }

        public static IEnumerable<DocumentoTarjetaViewModel> CrearTarjetas(IEnumerable<DocumentoModel> documentos, int ordenId, UrlHelper url)
        {
            return (documentos ?? Enumerable.Empty<DocumentoModel>())
                .Where(doc => doc != null)
                .Select(doc => CrearTarjeta(doc, ordenId, url));
        }

        private static Dictionary<string, string> ParsearMetadata(string observaciones)
        {
            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(observaciones) || !EsMetadataTecnica(observaciones))
            {
                return resultado;
            }

            var partes = observaciones.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var parte in partes)
            {
                var segmento = parte.Trim();
                var indice = segmento.IndexOf('=');
                if (indice <= 0)
                {
                    continue;
                }

                var clave = segmento.Substring(0, indice).Trim();
                var valor = segmento.Substring(indice + 1).Trim();
                if (!string.IsNullOrWhiteSpace(clave))
                {
                    resultado[clave] = valor;
                }
            }

            return resultado;
        }

        private static bool EsMetadataTecnica(string observaciones)
        {
            return observaciones.IndexOf('=') >= 0
                && (observaciones.IndexOf("OrdenId=", StringComparison.OrdinalIgnoreCase) >= 0
                    || observaciones.IndexOf("CodigoConcepto=", StringComparison.OrdinalIgnoreCase) >= 0
                    || observaciones.IndexOf("EstadoDocumento=", StringComparison.OrdinalIgnoreCase) >= 0
                    || observaciones.IndexOf("Aeropuertos=", StringComparison.OrdinalIgnoreCase) >= 0
                    || observaciones.IndexOf("HashArchivo=", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string FormatearNombreArchivoVisible(string nombreArchivo)
        {
            var limpio = (nombreArchivo ?? "Documento").Trim();
            if (limpio.Length <= 48)
            {
                return limpio;
            }

            var extension = System.IO.Path.GetExtension(limpio);
            var baseNombre = System.IO.Path.GetFileNameWithoutExtension(limpio) ?? limpio;
            var maxBase = Math.Max(20, 48 - (extension != null ? extension.Length : 0) - 1);
            if (baseNombre.Length <= maxBase)
            {
                return limpio;
            }

            return baseNombre.Substring(0, maxBase).TrimEnd('.', '_', '-', ' ') + "…" + (extension ?? string.Empty);
        }

        private static string ObtenerTipoDocumentoVisible(string tipoDocumento, string tipoCanonico)
        {
            if (string.Equals(tipoCanonico, "SOLICITUD_INSPECCION_EXT", StringComparison.OrdinalIgnoreCase))
            {
                return "Solicitud de Inspecciones";
            }

            if (string.Equals(tipoCanonico, "SOLICITUD_INSPECCIONES_FIRMADA", StringComparison.OrdinalIgnoreCase))
            {
                return "Solicitud de Inspecciones firmada";
            }

            return RevisionDocumentalDisplayHelper.GetDocumentDisplayName(tipoDocumento);
        }

        private static string ObtenerConceptoVisible(Dictionary<string, string> metadata)
        {
            string codigoConcepto;
            if (!metadata.TryGetValue("CodigoConcepto", out codigoConcepto) || string.IsNullOrWhiteSpace(codigoConcepto))
            {
                return string.Empty;
            }

            string visible;
            if (ConceptosVisibles.TryGetValue(codigoConcepto.Trim(), out visible))
            {
                return visible;
            }

            return codigoConcepto.Trim().Replace('_', ' ');
        }

        private static string ObtenerAeropuertosVisible(Dictionary<string, string> metadata)
        {
            string aeropuertos;
            if (!metadata.TryGetValue("Aeropuertos", out aeropuertos) || string.IsNullOrWhiteSpace(aeropuertos))
            {
                return string.Empty;
            }

            var items = aeropuertos
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => FormatearTitulo(item.Trim()))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            if (items.Count == 0)
            {
                return FormatearTitulo(aeropuertos.Trim());
            }

            return string.Join(", ", items);
        }

        private static string ObtenerEstadoVisible(DocumentoModel documento, Dictionary<string, string> metadata, string tipoCanonico)
        {
            string estadoDocumento;
            if (metadata.TryGetValue("EstadoDocumento", out estadoDocumento) && !string.IsNullOrWhiteSpace(estadoDocumento))
            {
                return FormatearEstado(estadoDocumento);
            }

            if (documento.Validado == true)
            {
                return "Validado";
            }

            if (string.Equals(tipoCanonico, "SOLICITUD_INSPECCIONES_FIRMADA", StringComparison.OrdinalIgnoreCase))
            {
                return FormatearEstado(documento.Estado) ?? "Firmado";
            }

            return FormatearEstado(documento.Estado) ?? "Cargado";
        }

        private static string ObtenerEstadoBadgeCss(DocumentoModel documento, Dictionary<string, string> metadata, string tipoCanonico)
        {
            var estado = (ObtenerEstadoVisible(documento, metadata, tipoCanonico) ?? string.Empty).Trim().ToUpperInvariant();

            if (estado == "VALIDADO" || estado == "APROBADO")
            {
                return "badge bg-success";
            }

            if (estado == "GENERADO")
            {
                return "badge bg-info text-dark";
            }

            if (estado == "FIRMADO" || estado == "CARGADO")
            {
                return "badge bg-primary";
            }

            if (estado == "OBSERVADO" || estado == "RECHAZADO" || estado == "DEVUELTO")
            {
                return "badge bg-danger";
            }

            if (estado == "PENDIENTE")
            {
                return "badge bg-warning text-dark";
            }

            return RevisionDocumentalDisplayHelper.GetBadgeClass(documento.Estado);
        }

        private static string FormatearEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return string.Empty;
            }

            var normalizado = estado.Trim().Replace('_', ' ').ToLowerInvariant();
            return CulturaEsEc.TextInfo.ToTitleCase(normalizado);
        }

        private static string FormatearTitulo(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            var lower = valor.Trim().ToLowerInvariant();
            return CulturaEsEc.TextInfo.ToTitleCase(lower);
        }

        private static string FormatearFecha(DateTime? fecha)
        {
            return fecha.HasValue
                ? fecha.Value.ToString("dd/MM/yyyy HH:mm", CulturaEsEc)
                : "No registrada";
        }

        private static bool EsDocumentoInspeccionExt(string tipoCanonico)
        {
            return string.Equals(tipoCanonico, "SOLICITUD_INSPECCION_EXT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipoCanonico, "SOLICITUD_INSPECCIONES_FIRMADA", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolverUrlDescarga(DocumentoModel documento, int ordenId, string tipoCanonico, UrlHelper url)
        {
            if (url == null)
            {
                return documento != null ? documento.RutaGuardada : string.Empty;
            }

            if (ordenId > 0 && string.Equals(tipoCanonico, "SOLICITUD_INSPECCION_EXT", StringComparison.OrdinalIgnoreCase))
            {
                return url.Action("DescargarSolicitudInspeccion", "OrdenRecaudacion", new { id = ordenId });
            }

            if (ordenId > 0 && string.Equals(tipoCanonico, "SOLICITUD_INSPECCIONES_FIRMADA", StringComparison.OrdinalIgnoreCase))
            {
                return url.Action("VerSolicitudInspeccionFirmada", "OrdenRecaudacion", new { id = ordenId });
            }

            if (!string.IsNullOrWhiteSpace(documento.RutaGuardada))
            {
                return url.Content(documento.RutaGuardada);
            }

            return string.Empty;
        }
    }
}
