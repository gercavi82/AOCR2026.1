using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Services;

namespace CapaNegocio
{
    public class OrdenRecaudacionBL
    {
        private readonly OrdenRecaudacionDAO _dao;

        public OrdenRecaudacionBL()
        {
            var config = new SecureConfigurationService();
            var connStr = config.GetConnectionString("PostgreSQL");
            _dao = new OrdenRecaudacionDAO(connStr);
        }

        public OrdenRecaudacionBL(string connectionString)
        {
            _dao = new OrdenRecaudacionDAO(connectionString);
        }

        public List<OrdenRecaudacion> ListarPorUsuario(string usuario)
        {
            var result = _dao.ObtenerTodosAsync().Result;
            return new List<OrdenRecaudacion>(result);
        }

        public OrdenRecaudacion ObtenerPorId(int id)
        {
            return _dao.ObtenerPorIdAsync(id).Result;
        }

        public int Insertar(OrdenRecaudacion orden)
        {
            return _dao.CrearAsync(orden).Result;
        }

        public bool Actualizar(OrdenRecaudacion orden)
        {
            return _dao.ActualizarAsync(orden).Result;
        }

        public bool CambiarEstado(int id, string nuevoEstado, string usuario)
        {
            return _dao.ActualizarEstadoAsync(id, nuevoEstado, usuario).Result;
        }
    }
}
