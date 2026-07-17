using CapaDatos.Models;

namespace CapaDatos.Interfaces
{
    public interface IUsuarioAS400DAO
    {
        string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario);
        UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario);
        string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario);
        string ObtenerCedulaPorCodigoUsuario(string codigoUsuario);
        bool UpsertUsuarioCompleto(UsuarioAs400Record record, out string error);
    }
}
