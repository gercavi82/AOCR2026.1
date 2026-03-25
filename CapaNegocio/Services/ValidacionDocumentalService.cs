using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class ResultadoValidacionDocumental
    {
        public bool EsValido { get; set; }
        public List<string> Errores { get; set; }
        public List<string> Advertencias { get; set; }
        public List<string> DocumentosFaltantes { get; set; }

        public ResultadoValidacionDocumental()
        {
            EsValido = true;
            Errores = new List<string>();
            Advertencias = new List<string>();
            DocumentosFaltantes = new List<string>();
        }
    }

    public class ValidacionDocumentalService
    {
        private static readonly string[] ExtensionesPermitidas =
        {
            ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".xls", ".xlsx"
        };

        private const long TamanoMaximoBytes = 10L * 1024L * 1024L;

        private readonly DocumentoDAO _documentoDao;
        private readonly ParametroDAO _parametroDao;

        public ValidacionDocumentalService()
        {
            _documentoDao = new DocumentoDAO();
            _parametroDao = new ParametroDAO();
        }

        public ResultadoValidacionDocumental ValidarDocumento(Documento documento)
        {
            var resultado = new ResultadoValidacionDocumental();

            if (documento == null)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("Documento inválido.");
                return resultado;
            }

            if (string.IsNullOrWhiteSpace(documento.NombreArchivo))
            {
                resultado.EsValido = false;
                resultado.Errores.Add("El documento debe tener nombre legible.");
            }

            var extension = (documento.Extension ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(documento.NombreArchivo))
            {
                var index = documento.NombreArchivo.LastIndexOf('.');
                extension = index >= 0 ? documento.NombreArchivo.Substring(index).ToLowerInvariant() : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(extension) || Array.IndexOf(ExtensionesPermitidas, extension) < 0)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("Tipo de archivo no permitido. Permitidos: " + string.Join(", ", ExtensionesPermitidas));
            }

            if (documento.TamanoBytes.HasValue && documento.TamanoBytes.Value > TamanoMaximoBytes)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("El tamaño del documento excede el máximo permitido (10 MB).");
            }

            if (documento.CodigoSolicitud <= 0)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("El documento debe estar vinculado a una solicitud válida.");
            }

            if (documento.CodigoSolicitud > 0)
            {
                var existentes = _documentoDao.ObtenerPorSolicitud(documento.CodigoSolicitud) ?? new List<Documento>();
                var duplicado = existentes.Any(d =>
                    d != null &&
                    d.CodigoDocumento != documento.CodigoDocumento &&
                    string.Equals((d.NombreArchivo ?? string.Empty).Trim(), (documento.NombreArchivo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

                if (duplicado)
                {
                    resultado.Advertencias.Add("Existe un documento con el mismo nombre para la solicitud.");
                }
            }

            return resultado;
        }

        public ResultadoValidacionDocumental ValidarDocumentosObligatorios(int codigoSolicitud, string etapa)
        {
            var resultado = new ResultadoValidacionDocumental();

            if (codigoSolicitud <= 0)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("Código de solicitud inválido.");
                return resultado;
            }

            var requeridos = ObtenerDocumentosRequeridos(etapa);
            var docs = _documentoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
            var docsActivos = docs.Where(d => d != null && !string.Equals(d.Estado, "ELIMINADO", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var requerido in requeridos)
            {
                var existe = docsActivos.Any(d => string.Equals((d.TipoDocumento ?? string.Empty).Trim(), requerido, StringComparison.OrdinalIgnoreCase));
                if (!existe)
                {
                    resultado.DocumentosFaltantes.Add(requerido);
                }
            }

            if (resultado.DocumentosFaltantes.Count > 0)
            {
                resultado.EsValido = false;
                resultado.Errores.Add("Faltan documentos obligatorios para la etapa " + (etapa ?? "N/A") + ".");
            }

            foreach (var doc in docsActivos)
            {
                var validacionDoc = ValidarDocumento(doc);
                if (!validacionDoc.EsValido)
                {
                    resultado.EsValido = false;
                    resultado.Errores.AddRange(validacionDoc.Errores);
                }

                resultado.Advertencias.AddRange(validacionDoc.Advertencias);
            }

            return resultado;
        }

        public ResultadoValidacionDocumental PuedeAvanzarEtapa(int codigoSolicitud, string etapa)
        {
            return ValidarDocumentosObligatorios(codigoSolicitud, etapa);
        }

        public ResultadoValidacionDocumental ObtenerAlertasDocumentales(int codigoSolicitud, string etapa)
        {
            var resultado = ValidarDocumentosObligatorios(codigoSolicitud, etapa);
            if (resultado.EsValido && resultado.Advertencias.Count == 0)
            {
                resultado.Advertencias.Add("No existen alertas documentales para la etapa actual.");
            }

            return resultado;
        }

        private List<string> ObtenerDocumentosRequeridos(string etapa)
        {
            var defaults = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "CIERRE_INSPECCION", new List<string> { "INFORME_TECNICO", "CHECKLIST_INSPECCION" } },
                { "APROBACION_INSPECCION", new List<string> { "INFORME_TECNICO", "ACTA_INSPECCION" } },
                { "ENVIO_DIRDAC", new List<string> { "INFORME_TECNICO", "MEMORANDO_DIRDAC" } },
                { "HABILITAR_OR", new List<string> { "INFORME_TECNICO", "DOCUMENTO_FINANCIERO" } },
                { "PAGO_FINAL", new List<string> { "COMPROBANTE_PAGO" } }
            };

            var etapaNormalizada = string.IsNullOrWhiteSpace(etapa) ? "CIERRE_INSPECCION" : etapa.Trim().ToUpperInvariant();
            var claveParametro = "DOC_REQ_" + etapaNormalizada;

            try
            {
                var parametro = _parametroDao.ObtenerPorClave(claveParametro);
                if (parametro != null && !string.IsNullOrWhiteSpace(parametro.Valor))
                {
                    return parametro.Valor
                        .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
                // No bloquear validación si falla lectura de parámetros.
            }

            if (defaults.ContainsKey(etapaNormalizada))
            {
                return defaults[etapaNormalizada];
            }

            return new List<string> { "INFORME_TECNICO" };
        }
    }
}
