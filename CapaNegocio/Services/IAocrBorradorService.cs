using CapaNegocio.DTOs;
using Npgsql;

namespace CapaNegocio.Services
{
    public interface IAocrBorradorService
    {
        ResultadoBorradorDocumento ObtenerOCrearBorrador(NpgsqlConnection connection, NpgsqlTransaction transaction, BorradorDocumentoRequest request);
    }
}
