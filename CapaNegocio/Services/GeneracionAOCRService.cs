using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio institucional para la generación automática del documento AOCR.
    /// Reemplaza la carga manual del "Borrador AOCR" por generación controlada
    /// a partir de los datos del trámite y el informe técnico que completa la fase tecnica.
    ///
    /// Este servicio evalúa las reglas de habilitación y persiste el documento
    /// generado. La creación física del PDF se realiza en la capa de presentación
    /// (que tiene acceso a Rotativa / Razor ViewEngine).
    /// </summary>
    public class GeneracionAOCRService
    {
        /// <summary>Tipo de documento institucional para el AOCR generado por el sistema.</summary>
        public const string TIPO_DOCUMENTO_AOCR_GENERADO = "AOCR_GENERADO";

        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly DocumentoDAO _documentoDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly HistorialEstadoDAO _historialDao;

        public GeneracionAOCRService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _documentoDao = new DocumentoDAO();
            _inspeccionDao = new InspeccionDAO();
            _informeDao = new InspeccionInformeDAO();
            _historialDao = new HistorialEstadoDAO();
        }

        /// <summary>Resultado de la evaluación de disponibilidad para generar AOCR.</summary>
        public class Disponibilidad
        {
            public bool Habilitado { get; set; }
            public string Motivo { get; set; }
            public bool YaGenerado { get; set; }
            public Documento DocumentoGenerado { get; set; }
            public SolicitudAOCR Solicitud { get; set; }
            public InspeccionInformeTecnico InformeAprobado { get; set; }
        }

        /// <summary>Formato institucional del número AOCR: AOCR-YYYY-#### .</summary>
        public static string GenerarNumeroAOCR(int idSolicitud, DateTime? fecha = null)
        {
            var f = fecha ?? DateTime.Now;
            return "AOCR-" + f.Year.ToString("0000") + "-" + idSolicitud.ToString("0000");
        }

        /// <summary>Evalúa si un trámite puede generar su AOCR automáticamente.</summary>
        public Disponibilidad Evaluar(int codigoSolicitud)
        {
            var resultado = new Disponibilidad { Habilitado = false };

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            resultado.Solicitud = solicitud;
            if (solicitud == null)
            {
                resultado.Motivo = "La solicitud no existe.";
                return resultado;
            }

            Documento existente = ObtenerAocrGeneradoVigente(codigoSolicitud);
            if (existente != null)
            {
                resultado.YaGenerado = true;
                resultado.DocumentoGenerado = existente;
            }

            // Regla 1: estado del trámite debe permitir emisión AOCR
            string estado = (solicitud.Estado ?? string.Empty).Trim();
            bool estadoValido =
                string.Equals(estado, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(estado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);

            if (!estadoValido)
            {
                resultado.Motivo = "La AOCR estará disponible cuando la inspeccion sea satisfactoria y el informe tecnico quede firmado por el inspector.";
                return resultado;
            }

            // Regla 2: informe tecnico finalizado y con cierre valido del flujo tecnico AOCR.
            InspeccionInformeTecnico informe = ObtenerInformeAprobado(codigoSolicitud);
            resultado.InformeAprobado = informe;

            if (informe == null)
            {
                resultado.Motivo = "El informe técnico aún no ha sido generado.";
                return resultado;
            }
            if (!informe.Finalizado)
            {
                resultado.Motivo = "El informe técnico aún no ha sido finalizado por el inspector.";
                return resultado;
            }
            if (!informe.FirmadoInspector)
            {
                resultado.Motivo = "El informe técnico no ha sido firmado por el inspector.";
                return resultado;
            }
            if (!InformeCompletaFaseTecnicaAocr(informe))
            {
                resultado.Motivo = "El informe tecnico todavia no completa la fase tecnica que habilita la AOCR.";
                return resultado;
            }

            // Regla 3: no regenerar si ya existe uno vigente
            if (resultado.YaGenerado)
            {
                resultado.Motivo = "La AOCR ya fue generada para esta solicitud.";
                resultado.Habilitado = false;
                return resultado;
            }

            resultado.Habilitado = true;
            resultado.Motivo = "Listo para generar la AOCR.";
            return resultado;
        }

        /// <summary>Obtiene el documento AOCR generado vigente (si existe).</summary>
        public Documento ObtenerAocrGeneradoVigente(int codigoSolicitud)
        {
            try
            {
                var docs = _documentoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
                return docs
                    .Where(d => d != null && !string.IsNullOrEmpty(d.TipoDocumento))
                    .Where(d => string.Equals(d.TipoDocumento, TIPO_DOCUMENTO_AOCR_GENERADO, StringComparison.OrdinalIgnoreCase))
                    .Where(d => !string.Equals((d.Estado ?? string.Empty).Trim(), "RECHAZADO", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals((d.Estado ?? string.Empty).Trim(), "ANULADO", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private InspeccionInformeTecnico ObtenerInformeAprobado(int codigoSolicitud)
        {
            try
            {
                var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
                InspeccionInformeTecnico mejor = null;
                foreach (var ins in inspecciones)
                {
                    if (ins == null) continue;
                    var inf = _informeDao.ObtenerUltimoPorInspeccion(ins.CodigoInspeccion);
                    if (inf == null) continue;

                    if (mejor == null) { mejor = inf; continue; }
                    int scoreActual = (inf.Finalizado ? 1 : 0) + (inf.FirmadoInspector ? 1 : 0) + (InformeCompletaFaseTecnicaAocr(inf) ? 1 : 0);
                    int scoreMejor = (mejor.Finalizado ? 1 : 0) + (mejor.FirmadoInspector ? 1 : 0) + (InformeCompletaFaseTecnicaAocr(mejor) ? 1 : 0);
                    if (scoreActual > scoreMejor) mejor = inf;
                }
                return mejor;
            }
            catch
            {
                return null;
            }
        }

        private static bool InformeCompletaFaseTecnicaAocr(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            if (!informe.Finalizado || !informe.FirmadoInspector)
            {
                return false;
            }

            if (informe.FirmadoDirdac)
            {
                return true;
            }

            if (informe.FechaFirma2.HasValue && !string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                return true;
            }

            var estadoInforme = (informe.EstadoInforme ?? string.Empty).Trim().ToUpperInvariant();
            return estadoInforme == "APROBADO_DIRECCION"
                || estadoInforme == "FIRMADO_FINAL";
        }

        /// <summary>
        /// Persiste el documento AOCR generado (el archivo PDF ya debe existir en disco)
        /// y registra el evento en el historial institucional del trámite.
        /// </summary>
        public Documento RegistrarDocumentoGenerado(
            int codigoSolicitud,
            string rutaArchivo,
            string nombreArchivo,
            string numeroAOCR,
            int usuarioId,
            string usuarioNombre,
            out string mensaje)
        {
            mensaje = null;

            if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
            {
                mensaje = "No se encontró el archivo PDF generado.";
                return null;
            }

            long? tamano = null;
            try { tamano = new FileInfo(rutaArchivo).Length; } catch { /* opcional */ }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            string estadoAnterior = solicitud != null ? solicitud.Estado : null;

            var documento = new Documento
            {
                CodigoSolicitud = codigoSolicitud,
                TipoDocumento = TIPO_DOCUMENTO_AOCR_GENERADO,
                NombreArchivo = string.IsNullOrWhiteSpace(nombreArchivo) ? Path.GetFileName(rutaArchivo) : nombreArchivo,
                RutaArchivo = rutaArchivo,
                TamanioArchivo = tamano,
                Observaciones = "AOCR generada automáticamente por el sistema. N° " + (numeroAOCR ?? ""),
                UsuarioRegistro = string.IsNullOrEmpty(usuarioNombre) ? "sistema" : usuarioNombre,
                Estado = "APROBADO",
                Validado = true,
                Version = 1,
                FechaCarga = DateTime.Now
            };

            try
            {
                int idGenerado = _documentoDao.Crear(documento);
                if (idGenerado > 0)
                {
                    documento.CodigoDocumento = idGenerado;
                }
            }
            catch (Exception ex)
            {
                mensaje = "El PDF se generó pero no se pudo registrar en el expediente: " + ex.Message;
                return null;
            }

            try
            {
                _historialDao.RegistrarCambio(
                    codigoSolicitud,
                    estadoAnterior,
                    estadoAnterior,
                    usuarioId,
                    "Generación automática de AOCR (" + (numeroAOCR ?? "S/N") + "). Archivo: " + documento.NombreArchivo);
            }
            catch { /* no romper si el historial falla */ }

            mensaje = "AOCR generada correctamente" + (string.IsNullOrEmpty(numeroAOCR) ? "." : " (" + numeroAOCR + ").");
            return documento;
        }
    }
}
