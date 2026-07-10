using System;
using System.IO;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IEstadoTecnicoInspeccionService
    {
        EstadoTecnicoInspeccion ObtenerEstadoTecnico(int codigoInspeccion);
    }

    public class EstadoTecnicoInspeccionService : IEstadoTecnicoInspeccionService
    {
        private readonly InspeccionDAO _inspeccionDao;
        private readonly ListaVerificacionOperacionalEaeDAO _lvDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly AocrProcesoEstadoDAO _estadoProcesoDao;
        private readonly LoggingService _logger;

        public EstadoTecnicoInspeccionService()
        {
            _inspeccionDao = new InspeccionDAO();
            _lvDao = new ListaVerificacionOperacionalEaeDAO();
            _informeDao = new InspeccionInformeDAO();
            _estadoProcesoDao = new AocrProcesoEstadoDAO();
            _logger = new LoggingService("EstadoTecnicoInspeccionService");
        }

        public EstadoTecnicoInspeccionService(
            InspeccionDAO inspeccionDao,
            ListaVerificacionOperacionalEaeDAO lvDao,
            InspeccionInformeDAO informeDao,
            AocrProcesoEstadoDAO estadoProcesoDao,
            LoggingService logger)
        {
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _lvDao = lvDao ?? new ListaVerificacionOperacionalEaeDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
            _estadoProcesoDao = estadoProcesoDao ?? new AocrProcesoEstadoDAO();
            _logger = logger ?? new LoggingService("EstadoTecnicoInspeccionService");
        }

        public EstadoTecnicoInspeccion ObtenerEstadoTecnico(int codigoInspeccion)
        {
            var dto = new EstadoTecnicoInspeccion
            {
                CodigoInspeccion = codigoInspeccion,
                PuedeCrearInforme = false,
                PuedeEditarInforme = false,
                PuedeFirmarInforme = false,
                PuedeVerInforme = false
            };

            if (codigoInspeccion <= 0)
            {
                dto.MotivoBloqueo = "Código de inspección inválido.";
                return dto;
            }

            var inspeccion = _inspeccionDao.ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                dto.MotivoBloqueo = "La inspección no existe.";
                return dto;
            }

            dto.CodigoSolicitud = inspeccion.CodigoSolicitud;
            dto.InspectorId = inspeccion.CodigoInspector;

            // 1. Obtener Estado de LV
            try
            {
                var lv = _lvDao.ObtenerUltimaPorInspeccion(codigoInspeccion);
                if (lv != null)
                {
                    dto.LvExiste = true;
                    dto.LvFinalizada = lv.Finalizado;
                    dto.LvFirmada = lv.FirmadoTecnico;
                    dto.RutaLvFirmada = lv.RutaDocumentoFirmado;
                    // Opcionalmente verificar archivo fisico si fuese necesario, pero por defecto confiamos en FirmadoTecnico.
                    dto.ArchivoLvFirmadoExiste = !string.IsNullOrWhiteSpace(lv.RutaDocumentoFirmado);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[LV][ESTADO_QUERY_ERROR] Error obteniendo LV para inspeccion " + codigoInspeccion + ": " + ex.Message);
            }

            // 2. Obtener Estado del Informe
            try
            {
                var informe = _informeDao.ObtenerUltimoPorInspeccion(codigoInspeccion);
                if (informe != null)
                {
                    dto.InformeExiste = true;
                    dto.EstadoInforme = (informe.EstadoInforme ?? string.Empty).Trim().ToUpperInvariant();
                    dto.RutaInformeFirmado = informe.RutaDocumentoFirmado;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[INFORME][ESTADO_QUERY_ERROR] Error obteniendo Informe para inspeccion " + codigoInspeccion + ": " + ex.Message);
            }

            // 3. Obtener Estado Central
            try
            {
                var estadoActual = _estadoProcesoDao.ObtenerActivoPorSolicitud(inspeccion.CodigoSolicitud);
                if (estadoActual != null)
                {
                    dto.EstadoCentral = (estadoActual.EstadoActual ?? string.Empty).Trim().ToUpperInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[INFORME][ESTADO_CENTRAL] Error obteniendo estado central para solicitud " + inspeccion.CodigoSolicitud + ": " + ex.Message);
            }

            // 4. Aplicar Reglas de Negocio (Matriz de estados)
            AplicarReglasDeNegocio(dto);

            _logger.LogInfo("[LV][ESTADO_RESUELTO] InspeccionId=" + codigoInspeccion + 
                "; LvFirmada=" + dto.LvFirmada + 
                "; InformeExiste=" + dto.InformeExiste + 
                "; EstadoInforme=" + dto.EstadoInforme + 
                "; PuedeVer=" + dto.PuedeVerInforme + 
                "; PuedeEditar=" + dto.PuedeEditarInforme);

            return dto;
        }

        private void AplicarReglasDeNegocio(EstadoTecnicoInspeccion dto)
        {
            if (!dto.LvExiste || !dto.LvFinalizada)
            {
                dto.MotivoBloqueo = "Debe finalizar la Lista de Verificación antes de gestionar el Informe Técnico.";
                return;
            }

            if (!dto.LvFirmada)
            {
                dto.MotivoBloqueo = "Debe firmar la Lista de Verificación antes de gestionar el Informe Técnico.";
                return;
            }

            // A partir de aqui, la LV está firmada.
            if (!dto.InformeExiste)
            {
                // Caso: LV firmada, informe no existe -> Puede crearlo
                dto.PuedeCrearInforme = true;
                dto.PuedeEditarInforme = true;
                return;
            }

            // Evaluar según estado del informe
            switch (dto.EstadoInforme)
            {
                case "BORRADOR_INFORME":
                    dto.PuedeEditarInforme = true;
                    break;

                case "FINALIZADO_INFORME":
                    // El informe está finalizado pero no firmado, puede firmarlo.
                    dto.PuedeFirmarInforme = true;
                    break;

                case "FIRMADO_INSPECTOR":
                case "INFORME_TECNICO_APROBADO_DCAV":
                    // El informe ya fue firmado o aprobado, solo se puede ver
                    dto.PuedeVerInforme = true;
                    break;

                case "INFORME_TECNICO_OBSERVADO_DCAV":
                    // El informe fue observado por DCAV, se puede editar/corregir y volver a firmar
                    dto.PuedeEditarInforme = true;
                    dto.PuedeFirmarInforme = true;
                    break;

                default:
                    // Por defecto, si hay informe pero estado desconocido, permitir ver.
                    dto.PuedeVerInforme = true;
                    break;
            }

            // Ajuste por si el estado central indica que ya está en pasos posteriores (fallback).
            if (dto.EstadoCentral == "PENDIENTE_REVISION_INFORME_DCAV" && dto.EstadoInforme == "FIRMADO_INSPECTOR")
            {
                dto.PuedeVerInforme = true;
            }
        }
    }
}
