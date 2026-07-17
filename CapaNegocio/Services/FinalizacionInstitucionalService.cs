using System;
using System.Configuration;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Interfaces;
using Npgsql;
using System.Linq;

namespace CapaNegocio.Services
{
    public sealed class FinalizacionInstitucionalService : IFinalizacionInstitucionalService
    {
        private static readonly string Cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;

        public FirmaDocumentoResultado Finalizar(int solicitudId, int usuarioId, string tipoDocumentoActual, Func<string, bool> rutaExiste)
        {
            var resultado = new FirmaDocumentoResultado { Exitoso = false };

            if (solicitudId <= 0 || usuarioId <= 0)
            {
                resultado.Mensaje = "Solicitud o usuario inválido.";
                return resultado;
            }

            // Validar que no hay NC abiertas
            if (ExistenNoConformidadesAbiertas(solicitudId))
            {
                resultado.Mensaje = "Existen No Conformidades pendientes. No se puede finalizar el expediente.";
                return resultado;
            }

            // Obtener firmas actuales
            var firmaAocr = ObtenerUltimaFirma(solicitudId, "RECONOCIMIENTO");
            var firmaCondiciones = ObtenerUltimaFirma(solicitudId, "CONDICIONES_LIMITACIONES") ?? ObtenerUltimaFirma(solicitudId, "CONDICIONES");

            var aocrFirmada = DocumentoFirmadoValido(firmaAocr, solicitudId, rutaExiste);
            var condicionesFirmadas = DocumentoFirmadoValido(firmaCondiciones, solicitudId, rutaExiste);
            
            var esModificacion = EsTramiteModificacion(solicitudId);

            // Módulo 8 (Modificación): Solo requiere Condiciones
            if (esModificacion)
            {
                if (!condicionesFirmadas)
                {
                    resultado.EstadoSolicitudNuevo = "PENDIENTE_FIRMAS_INSTITUCIONALES";
                    resultado.EstadoAocrNuevo = "CONDICIONES_PENDIENTES_DCAV";
                    resultado.Exitoso = true;
                    resultado.Mensaje = "Pendiente de firma de Condiciones y Limitaciones.";
                    return resultado;
                }
                
                // Finalizar Módulo 8 (Solo Condiciones)
                return EjecutarTransaccionCierre(solicitudId, usuarioId, null, firmaCondiciones);
            }
            else // Módulo 7 (Emisión/Renovación): Requiere ambos
            {
                if (!aocrFirmada && !condicionesFirmadas)
                {
                    resultado.EstadoSolicitudNuevo = "PENDIENTE_FIRMAS_INSTITUCIONALES";
                    resultado.Exitoso = true;
                    resultado.Mensaje = "Pendiente de firmas institucionales.";
                    return resultado;
                }

                if (!aocrFirmada || !condicionesFirmadas)
                {
                    resultado.EstadoSolicitudNuevo = "FIRMA_PARCIAL_INSTITUCIONAL";
                    resultado.Exitoso = true;
                    resultado.Mensaje = "Firma parcial registrada. Expediente bloqueado para firma final.";
                    return resultado;
                }
                
                // Módulo 7 Completo
                return EjecutarTransaccionCierre(solicitudId, usuarioId, firmaAocr, firmaCondiciones);
            }
        }

        private FirmaDocumentoResultado EjecutarTransaccionCierre(int solicitudId, int usuarioId, AocrFirmaDocumento firmaAocr, AocrFirmaDocumento firmaCondiciones)
        {
            var resultado = new FirmaDocumentoResultado { Exitoso = false };

            using (var cn = new NpgsqlConnection(Cs))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        var estadoNuevo = "FINALIZADO";

                        // Cambiar estado solicitud
                        const string sqlEstado = "UPDATE public.aocr_tbsolicitud SET estado = @estado, updated_by = @usuario, updated_at = NOW() WHERE codigo_solicitud = @solicitud;";
                        using (var cmd = new NpgsqlCommand(sqlEstado, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@estado", estadoNuevo);
                            cmd.Parameters.AddWithValue("@usuario", usuarioId);
                            cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                            cmd.ExecuteNonQuery();
                        }

                        // Historial
                        const string sqlHistorial = "INSERT INTO public.aocr_tbhistorial_estado (codigo_solicitud, estado_nuevo, codigo_usuario, observacion, created_at) VALUES (@solicitud, @estado, @usuario, 'Liberación final por firma institucional completa.', NOW());";
                        using (var cmd = new NpgsqlCommand(sqlHistorial, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                            cmd.Parameters.AddWithValue("@estado", estadoNuevo);
                            cmd.Parameters.AddWithValue("@usuario", usuarioId);
                            cmd.ExecuteNonQuery();
                        }

                        // Evento idempotente (Auditoría / Workflow)
                        var vAocr = firmaAocr?.CodigoFirma ?? 0;
                        var vCondiciones = firmaCondiciones?.CodigoFirma ?? 0;
                        var eventKey = $"DOCUMENTOS_FINALES_RT:{solicitudId}:{vAocr}:{vCondiciones}:RT";
                        
                        const string sqlEvento = "INSERT INTO public.aocr_evento_workflow (event_key, evento, solicitud_id, correlation_id, resultado, intentos, created_at) VALUES (@key, 'DOCUMENTOS_LIBERADOS_RT', @solicitud, @corr, 'PENDIENTE', 0, NOW()) ON CONFLICT (event_key) DO NOTHING RETURNING 1;";
                        using (var cmd = new NpgsqlCommand(sqlEvento, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@key", eventKey);
                            cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                            cmd.Parameters.AddWithValue("@corr", "FINAL_RT_" + solicitudId);
                            var insertado = cmd.ExecuteScalar();
                            if (insertado == null)
                            {
                                // Ya se insertó, ignoramos o bloqueamos
                            }
                        }

                        tx.Commit();
                        resultado.Exitoso = true;
                        resultado.EstadoSolicitudNuevo = estadoNuevo;
                        resultado.Mensaje = "Documentos finales liberados exitosamente.";
                        return resultado;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        resultado.Mensaje = "Error en transacción de cierre: " + ex.Message;
                        return resultado;
                    }
                }
            }
        }

        private bool DocumentoFirmadoValido(AocrFirmaDocumento firma, int solicitudId, Func<string, bool> rutaExiste)
        {
            return firma != null
                && firma.CodigoSolicitud == solicitudId
                && firma.FechaFirma > DateTime.MinValue
                && !string.IsNullOrWhiteSpace(firma.HashDocumento)
                && firma.TamanioPdfFirmado.GetValueOrDefault() > 0
                && !string.IsNullOrWhiteSpace(firma.RutaDocumento)
                && rutaExiste != null
                && rutaExiste(firma.RutaDocumento);
        }

        private bool EsTramiteModificacion(int solicitudId)
        {
            using (var cn = new NpgsqlConnection(Cs))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand("SELECT tipo_solicitud FROM public.aocr_tbsolicitud WHERE codigo_solicitud = @id;", cn))
                {
                    cmd.Parameters.AddWithValue("@id", solicitudId);
                    var v = cmd.ExecuteScalar();
                    if (v != null && v != DBNull.Value)
                    {
                        var tipo = Convert.ToInt32(v);
                        return tipo == 3; // 3 = Modificacion
                    }
                    return false;
                }
            }
        }

        private AocrFirmaDocumento ObtenerUltimaFirma(int solicitudId, string tipo)
        {
            return new CapaDatos.DAOs.AocrFirmaDocumentoDAO().ObtenerUltimoPorSolicitudTipo(solicitudId, tipo);
        }

        private bool ExistenNoConformidadesAbiertas(int solicitudId)
        {
            var ncVigentes = new CapaDatos.DAOs.NoConformidadDAO().ListarPorSolicitud(solicitudId);
            if (ncVigentes == null) return false;
            
            return ncVigentes.Any(nc => 
            {
                var estado = (nc.Estado ?? string.Empty).Trim().ToUpperInvariant();
                return estado != "CERRADA" && estado != "CERRADO" && estado != "ANULADA";
            });
        }
    }
}
