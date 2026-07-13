using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IPerfilFirmanteService
    {
        PerfilFirmanteDto ObtenerPerfil(int usuarioId, string tipoDocumento);
    }

    public interface IConfiguracionPosicionFirmaService
    {
        ConfiguracionPosicionFirmaDto Obtener(string tipoDocumento, int versionPlantilla);
    }
}
