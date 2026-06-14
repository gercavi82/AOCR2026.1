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

            var lista = ListaVerificacionDao.ObtenerUltimaPorInspeccion(codigoInspeccion);
            if (lista == null || !lista.Finalizado || !lista.FirmadoTecnico)
            {
                motivo = "Debe finalizar y firmar la Lista de Verificación antes de generar el Informe Técnico.";
                return false;
            }

            return true;
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
