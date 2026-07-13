using System;

namespace CapaPresentacion.Services
{
    public sealed class UsuarioContextoInvalidoException : Exception
    {
        public UsuarioContextoInvalidoException(string message)
            : base(message)
        {
        }

        public UsuarioContextoInvalidoException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
