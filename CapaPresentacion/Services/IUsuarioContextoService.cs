namespace CapaPresentacion.Services
{
    public interface IUsuarioContextoService
    {
        UsuarioContextoDto ObtenerContextoActual();
        bool TryObtenerContextoActual(out UsuarioContextoDto contexto);
        int ObtenerUsuarioId();
        void InvalidarCache();
    }
}
