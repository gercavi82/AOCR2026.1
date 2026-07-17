using System;
using System.Linq;
using CapaModelo;
using CapaNegocio.Interfaces;

namespace CapaNegocio.Services
{
    public sealed class DocumentoFirmaService : IDocumentoFirmaService
    {
        public FirmaDocumentoResultado Firmar(FirmaDocumentoRequest request)
        {
            var resultado = new FirmaDocumentoResultado { Exitoso = false };

            try
            {
                if (request == null || request.SolicitudId <= 0 || request.UsuarioId <= 0)
                {
                    resultado.Mensaje = "Petición de firma inválida o usuario no autenticado.";
                    return resultado;
                }

                var tipo = (request.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
                var roles = (request.RolSolicitado ?? string.Empty).ToUpperInvariant();

                // 1. AOCR firmado exclusivamente por DIRDAC
                if (tipo == "RECONOCIMIENTO" || tipo == "AOCR")
                {
                    if (!roles.Contains("DIRDAC") && !roles.Contains("DIRECCION"))
                    {
                        resultado.Mensaje = "El documento AOCR debe ser firmado exclusivamente por DIRDAC.";
                        return resultado;
                    }
                }
                // 2. Condiciones y Limitaciones firmado exclusivamente por DCAV
                else if (tipo == "CONDICIONES_LIMITACIONES" || tipo == "CONDICIONES")
                {
                    if (!roles.Contains("DCAV") && !roles.Contains("DIRECTORCERTIFICACIONESDCAV") && !roles.Contains("JEFATURATECNICA"))
                    {
                        resultado.Mensaje = "El documento Condiciones y Limitaciones debe ser firmado exclusivamente por DCAV.";
                        return resultado;
                    }
                }
                else
                {
                    resultado.Mensaje = "Tipo de documento no soportado para firma institucional.";
                    return resultado;
                }

                resultado.Exitoso = true;
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Exitoso = false;
                resultado.Mensaje = "Error en la validación de firma: " + ex.Message;
                return resultado;
            }
        }
    }
}
