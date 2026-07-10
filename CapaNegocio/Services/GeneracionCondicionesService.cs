using System;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class GeneracionCondicionesService
    {
        private readonly AocrDocumentoGeneradoDAO _documentoGeneradoDao;

        public GeneracionCondicionesService()
        {
            _documentoGeneradoDao = new AocrDocumentoGeneradoDAO();
        }

        public ResultadoDocumento ObtenerOCrearBorradorCondiciones(int solicitudId, int inspectorId, int usuarioDcavId)
        {
            try
            {
                var doc = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_BORRADOR");
                if (doc != null)
                {
                    return new ResultadoDocumento { Ok = true, Mensaje = "Borrador recuperado", Documento = doc };
                }

                doc = new AocrDocumentoGenerado
                {
                    CodigoSolicitud = solicitudId,
                    TipoDocumento = "CONDICIONES_BORRADOR",
                    NumeroAocr = "BORRADOR",
                    NombreArchivo = "",
                    RutaDocumento = "",
                    Estado = "BORRADOR_INSPECTOR",
                    FechaGeneracion = DateTime.Now,
                    CodigoUsuario = inspectorId,
                    UsuarioNombre = "Inspector"
                };

                _documentoGeneradoDao.Registrar(doc);
                doc = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_BORRADOR");
                return new ResultadoDocumento { Ok = true, Mensaje = "Borrador creado", Documento = doc };
            }
            catch (Exception ex)
            {
                return new ResultadoDocumento { Ok = false, Mensaje = "Error al crear borrador Condiciones: " + ex.Message };
            }
        }
    }
}
