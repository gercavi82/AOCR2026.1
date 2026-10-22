using System;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Validaciones de fases LV → Informe → AOCR → firma (backend).
    /// </summary>
    public interface IAocrFlujoValidacionService
    {
        bool PuedeGenerarInformeTecnico(int codigoInspeccion, out string motivo);
        bool PuedeFirmarInformeTecnico(int codigoInforme, out string motivo);
        bool PuedeGenerarAocr(int codigoSolicitud, int usuarioId, System.Collections.Generic.IList<string> roles, out string motivo);
        bool InformeEsSatisfactorio(int codigoInforme);
        bool InformeEsNoSatisfactorio(int codigoInforme);
    }

    public sealed class AocrFlujoValidacionService : IAocrFlujoValidacionService
    {
        private InspeccionInformeDAO _informeDao;
        private ListaVerificacionOperacionalEaeDAO _listaVerificacionDao;
        private GeneracionAOCRService _generacionAocrService;

        private InspeccionInformeDAO InformeDao => _informeDao ?? (_informeDao = new InspeccionInformeDAO());
        private ListaVerificacionOperacionalEaeDAO ListaVerificacionDao => _listaVerificacionDao ?? (_listaVerificacionDao = new ListaVerificacionOperacionalEaeDAO());
        private GeneracionAOCRService GeneracionAocr => _generacionAocrService ?? (_generacionAocrService = new GeneracionAOCRService());

        public bool PuedeGenerarInformeTecnico(int codigoInspeccion, out string motivo)
        {
            motivo = string.Empty;
            if (codigoInspeccion <= 0)
            {
                motivo = "Inspección inválida.";
                return false;
            }

            var inspeccion = new InspeccionDAO().ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                motivo = "Inspección no encontrada.";
                return false;
            }
            System.Collections.Generic.List<string> pendientes;
            if (!ListaVerificacionDao.TodasLasListasEstacionesFirmadas(inspeccion.CodigoSolicitud, codigoInspeccion, out pendientes))
            {
                motivo = "Debe finalizar y firmar la LV de cada estación antes de generar el Informe Técnico. Pendientes: " + string.Join(", ", pendientes);
                return false;
            }

            return true;
        }

        public bool PuedePrepararDocumentosFinales(int solicitudId, out string motivo, bool requiereAprobacion = false)
        {
            motivo = "Debe finalizar y firmar los informes satisfactorios de todas las estaciones antes de preparar los documentos finales.";
            if (solicitudId <= 0) return false;
            var inspecciones = new InspeccionDAO().ListarPorSolicitud(solicitudId);
            if (inspecciones == null || inspecciones.Count == 0) return false;
            var estaciones = new SolicitudEstacionDAO().ListarPorSolicitud(solicitudId);
            Func<InspeccionInformeTecnico, bool> cumple = informe => InformeListoParaPreparacion(informe)
                && (!requiereAprobacion || Array.Exists(new[] { "APROBADO_DIRECCION", "APROBADO_DIRDAC", "INFORME_APROBADO_DIRECCION",
                    "INFORME_TECNICO_APROBADO", "INFORME_TECNICO_APROBADO_DIRECCION", "INFORME_TECNICO_APROBADO_DIRDAC", "INFORME_TECNICO_APROBADO_DCAV" },
                    e => string.Equals(e, informe.EstadoInforme, StringComparison.OrdinalIgnoreCase)));
            foreach (var inspeccion in inspecciones)
            {
                var estacionesInspeccion = estaciones.FindAll(e => !e.InspeccionId.HasValue || e.InspeccionId == inspeccion.CodigoInspeccion);
                if (estacionesInspeccion.Count == 0)
                {
                    if (!cumple(InformeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion))) return false;
                }
                foreach (var estacion in estacionesInspeccion)
                    if (!cumple(InformeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion, estacion.Id))) return false;
            }
            motivo = string.Empty;
            return true;
        }

        public static bool InformeListoParaPreparacion(InspeccionInformeTecnico informe)
        {
            return informe != null && informe.Finalizado && informe.FirmadoInspector
                && !string.IsNullOrWhiteSpace(informe.RutaDocumentoFirmado)
                && string.Equals(informe.Resultado, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(informe.EstadoInforme, AocrEstadosProceso.InformeTecnicoDevueltoInspector, StringComparison.OrdinalIgnoreCase);
        }

        public bool PuedeFirmarInformeTecnico(int codigoInforme, out string motivo)
        {
            motivo = string.Empty;
            if (codigoInforme <= 0)
            {
                motivo = "Informe inválido.";
                return false;
            }

            var informe = InformeDao.ObtenerPorId(codigoInforme);
            if (informe == null)
            {
                motivo = "Informe técnico no encontrado.";
                return false;
            }

            if (informe.FirmadoInspector)
            {
                motivo = "El informe técnico ya fue firmado.";
                return false;
            }

            if (!informe.Finalizado)
            {
                motivo = "Debe finalizar el Informe Técnico antes de firmarlo.";
                return false;
            }

            var resultado = (informe.Resultado ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resultado))
            {
                motivo = "Debe seleccionar resultado satisfactorio o no satisfactorio.";
                return false;
            }

            if (EsResultadoNoSatisfactorio(resultado)
                && string.IsNullOrWhiteSpace(informe.NoConformidades)
                && string.IsNullOrWhiteSpace(informe.Observaciones))
            {
                motivo = "Para resultado no satisfactorio debe registrar hallazgos técnicos u observaciones.";
                return false;
            }

            return true;
        }

        public bool PuedeGenerarAocr(int codigoSolicitud, int usuarioId, System.Collections.Generic.IList<string> roles, out string motivo)
        {
            return GeneracionAocr.PuedeGenerarAocr(codigoSolicitud, usuarioId, roles, out motivo);
        }

        public bool InformeEsSatisfactorio(int codigoInforme)
        {
            var informe = InformeDao.ObtenerPorId(codigoInforme);
            return informe != null && EsResultadoSatisfactorio(informe.Resultado);
        }

        public bool InformeEsNoSatisfactorio(int codigoInforme)
        {
            var informe = InformeDao.ObtenerPorId(codigoInforme);
            return informe != null && EsResultadoNoSatisfactorio(informe.Resultado);
        }

        private static bool EsResultadoSatisfactorio(string resultado)
        {
            var token = (resultado ?? string.Empty).Trim().ToUpperInvariant();
            return token.Contains("SATISFACT") && !token.Contains("NO SATISFACT") && !token.Contains("NO_SATISFACT");
        }

        private static bool EsResultadoNoSatisfactorio(string resultado)
        {
            var token = (resultado ?? string.Empty).Trim().ToUpperInvariant();
            return token.Contains("NO SATISFACT") || token.Contains("NO_SATISFACT") || token.Contains("NO SATISFACTORIO");
        }
    }
}
