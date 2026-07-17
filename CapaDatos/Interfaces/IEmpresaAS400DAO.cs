using System.Collections.Generic;
using CapaDatos.Models;

namespace CapaDatos.Interfaces
{
    public interface IEmpresaAS400DAO
    {
        bool TestConnection();
        System.Collections.Generic.List<CapaDatos.DAOs.Empresa> ObtenerEmpresas();
        CapaDatos.DAOs.Empresa ObtenerEmpresaPorCodigo(string codigoOaci);
    }
}
