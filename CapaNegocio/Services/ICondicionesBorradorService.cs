using CapaNegocio.DTOs;
using Npgsql;

namespace CapaNegocio.Services
{
    public interface ICondicionesBorradorService
    {
        ResultadoBorradorDocumento ObtenerOCrearBorrador(NpgsqlConnection connection, NpgsqlTransaction transaction, BorradorDocumentoRequest request);
    }
}
