using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Punto único de normalización de estados AOCR (C# y claves institucionales).
    /// Delega en <see cref="EstadoSolicitud"/> y <see cref="EstadoSolicitudSql"/> como fuente canónica.
    /// </summary>
    public interface IAocrEstadoService
    {
        string Normalizar(string estado);
        string NormalizarClaveInstitucional(string estado);
        string ToSqlToken(string estado);
        bool EsTransicionCanonicaValida(string estadoActual, string estadoDestino);
        bool EstadoPermiteAsignacionInicial(string estado);
        bool EsEstadoFinal(string estado);
        bool PermiteEdicionRt(string estado);
        IReadOnlyList<string> EstadosInstitucionales { get; }
        string NormalizarDesdeLegacyCatalogo(string estadoLegacy);
    }

    public sealed class AocrEstadoService : IAocrEstadoService
    {
        public IReadOnlyList<string> EstadosInstitucionales { get; } = new[]
        {
            "BORRADOR", "ORDEN_GENERADA", "PAGO_PENDIENTE", "PAGO_OBSERVADO", "PAGO_APROBADO",
            "SOLICITUD_AOCR_HABILITADA", "DOCUMENTACION_EN_CARGA", "DOCUMENTACION_ENVIADA",
            "EN_REVISION_COORDINADOR", "DEVUELTO_RT_OBSERVACIONES", "SUBSANADA",
            "DOCUMENTACION_ACEPTADA_COORDINADOR", "PENDIENTE_ASIGNACION_INSPECTOR",
            "INSPECTOR_ASIGNADO", "EN_REVISION_TECNICA", "DOCUMENTACION_TECNICA_OBSERVADA",
            "INSPECCION_REQUERIDA", "SOLICITUD_INSPECCION_GENERADA", "PENDIENTE_CARGA_FIRMADA",
            "SOLICITUD_INSPECCION_FIRMADA", "EN_INSPECCION", "LV_EN_PROCESO", "LV_FINALIZADA",
            "LV_FIRMADA", "INFORME_TECNICO_EN_ELABORACION", "INFORME_TECNICO_FIRMADO",
            "INFORME_TECNICO_SATISFACTORIO", "INFORME_TECNICO_NO_SATISFACTORIO", "NC_GENERADA",
            "PENDIENTE_SUBSANACION", "NUEVA_INSPECCION_REQUERIDA", "AOCR_EN_ELABORACION",
            "AOCR_EN_REVISION_COORDINADOR", "AOCR_DEVUELTO_OBSERVACIONES", "AOCR_ENVIADO_DIRDAC",
            "AOCR_FIRMADO", "CONDICIONES_FIRMADAS", "DOCUMENTOS_FINALES_DISPONIBLES",
            "AOCR_LEGALIZADO", "CERRADO", "ANULADO"
        };

        public string Normalizar(string estado)
        {
            return EstadoSolicitud.Normalizar(estado);
        }

        public string NormalizarClaveInstitucional(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return "BORRADOR";
            }

            var canonico = Normalizar(estado);
            var clave = EstadoSolicitud.NormalizarClaveEdicion(canonico);
            return MapearClaveInstitucional(clave);
        }

        public string ToSqlToken(string estado)
        {
            return EstadoSolicitudSql.ToSqlToken(estado);
        }

        public bool EsTransicionCanonicaValida(string estadoActual, string estadoDestino)
        {
            return EstadoSolicitud.EsTransicionValida(estadoActual, estadoDestino);
        }

        public bool EstadoPermiteAsignacionInicial(string estado)
        {
            return EstadoSolicitudSql.EstadoPermiteAsignacionInicial(estado);
        }

        public bool EsEstadoFinal(string estado)
        {
            var normalizado = Normalizar(estado);
            return string.Equals(normalizado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        public bool PermiteEdicionRt(string estado)
        {
            return EstadoSolicitud.PermiteEdicionFormularioEmision(estado);
        }

        /// <summary>
        /// Traduce constantes del catálogo legacy simplificado al canon <see cref="EstadoSolicitud"/>.
        /// </summary>
        public string NormalizarDesdeLegacyCatalogo(string estadoLegacy)
        {
            if (string.IsNullOrWhiteSpace(estadoLegacy))
            {
                return Normalizar(null);
            }

            switch (estadoLegacy.Trim().ToUpperInvariant())
            {
                case "RECEPCIONADO":
                    return EstadoSolicitud.DocumentacionPendiente;
                case "ANALISIS_REQUISITOS":
                    return EstadoSolicitud.EnRevision;
                case "SUBSANACION":
                    return EstadoSolicitud.Observada;
                case "SUBSANADO":
                    return EstadoSolicitud.Subsanada;
                case "EN_EVALUACION_TECNICA":
                    return EstadoSolicitud.EnInspeccion;
                case "EN_EVALUACION_LEGAL":
                case "EN_APROBACION_COORDINADOR":
                    return EstadoSolicitud.EnRevisionCoordinadorFinal;
                case "EN_EVALUACION_FINANCIERA":
                    return EstadoSolicitud.PagoPendiente;
                case "EN_APROBACION_DIRECTOR":
                    return EstadoSolicitud.EnviadoDcav;
                case "APROBADO":
                    return EstadoSolicitud.AOCR_EnElaboracion;
                case "AOCR_EMITIDO":
                    return EstadoSolicitud.AOCR_EmitidoRecibido;
                case "AOCR_ENTREGADO":
                    return EstadoSolicitud.Finalizado;
                case "RECHAZADO":
                    return EstadoSolicitud.Anulada;
                default:
                    return Normalizar(estadoLegacy);
            }
        }

        private static string MapearClaveInstitucional(string claveEdicion)
        {
            switch ((claveEdicion ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "PENDIENTE":
                case "SOLICITUD_CREADA":
                    return "DOCUMENTACION_EN_CARGA";
                case "EN_REVISION":
                case "EN_REVISION_COORDINADOR":
                case "ENVIADO_COORDINADOR":
                case "PENDIENTE_CARGA_DOCUMENTAL_RT":
                case "PENDIENTE_REVISION_DOCUMENTAL":
                    return "EN_REVISION_COORDINADOR";
                case "OBSERVADA":
                case "DEVUELTO_RT":
                case "DEVUELTO_CON_OBSERVACIONES":
                    return "DEVUELTO_RT_OBSERVACIONES";
                case "SUBSANADA":
                case "SUBSANADO":
                    return "SUBSANADA";
                case "ACEPTACION_DOCUMENTAL":
                    return "DOCUMENTACION_ACEPTADA_COORDINADOR";
                case "PENDIENTE_ASIGNACION_RT":
                case "PENDIENTE_ASIGNACION":
                    return "PENDIENTE_ASIGNACION_INSPECTOR";
                case "EN_INSPECCION":
                case "INSPECCION_ASIGNADA":
                case "INSPECTOR_ASIGNADO":
                    return "INSPECTOR_ASIGNADO";
                case "AOCR_EN_ELABORACION":
                    return "AOCR_EN_ELABORACION";
                case "AOCR_EN_REVISION":
                case "AOCR_EN_REVISION_COORDINADOR":
                    return "AOCR_EN_REVISION_COORDINADOR";
                case "ENVIADO_DCAV":
                case "AOCR_ENVIADO_DIRDAC":
                    return "AOCR_ENVIADO_DIRDAC";
                case "FIRMADO_DCAV":
                case "AOCR_FIRMADO":
                    return "AOCR_FIRMADO";
                case "AOCR_LEGALIZADO":
                case "LEGALIZADO":
                    return "AOCR_LEGALIZADO";
                case "AOCR_EMITIDO":
                case "AOCR_EMITIDO/RECIBIDO":
                case "AOCR_EMITIDO_RECIBIDO":
                case "CERTIFICADO_EMITIDO":
                    return "DOCUMENTOS_FINALES_DISPONIBLES";
                case "FINALIZADO":
                case "CERRADO":
                    return "CERRADO";
                case "ANULADA":
                case "ANULADO":
                    return "ANULADO";
                default:
                    return claveEdicion;
            }
        }
    }
}
