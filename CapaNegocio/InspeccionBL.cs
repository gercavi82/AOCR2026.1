using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de negocio para Inspecciones.
    /// NO static para permitir inyección y test.
    /// </summary>
    public class InspeccionBL
    {
        private readonly InspeccionDAO _dao;

        public InspeccionBL()
        {
            _dao = new InspeccionDAO();
        }

        public InspeccionBL(InspeccionDAO dao)
        {
            _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        }

        public Inspeccion ObtenerPorId(int id)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            return _dao.ObtenerPorId(id);
        }

        public List<Inspeccion> ListarTodas()
        {
            // Si tu DAO aún no tiene ListarTodas(), lo implementamos abajo
            return _dao.ListarTodas() ?? new List<Inspeccion>();
        }

        public List<Inspeccion> ListarPorInspector(int codigoInspector)
        {
            if (codigoInspector <= 0) return new List<Inspeccion>();
            return _dao.ListarPorInspector(codigoInspector) ?? new List<Inspeccion>();
        }

        public int Crear(Inspeccion model, int codigoUsuario)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.CodigoSolicitud <= 0) throw new Exception("Código de solicitud inválido.");

            model.CreatedBy = codigoUsuario;
            model.UpdatedBy = codigoUsuario;

            if (string.IsNullOrWhiteSpace(model.Estado))
                model.Estado = "CREADA";

            int id = _dao.Crear(model);
            model.CodigoInspeccion = id;
            return id;
        }

        public bool Actualizar(Inspeccion model, int updatedBy)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.CodigoInspeccion <= 0) throw new Exception("Código de inspección inválido.");

            model.UpdatedBy = updatedBy;
            model.UpdatedAt = DateTime.Now;

            return _dao.Actualizar(model);
        }

        public bool CambiarEstado(int id, string estado, int updatedBy)
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(estado)) throw new Exception("Estado requerido.");
            return _dao.CambiarEstado(id, estado, updatedBy);
        }

        public bool GuardarInforme(int id, string rutaInforme, int updatedBy)
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(rutaInforme)) throw new Exception("Ruta de informe requerida.");
            return _dao.GuardarInforme(id, rutaInforme, updatedBy);
        }

        public bool CerrarInspeccion(int id, string resultado, int updatedBy)
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(resultado)) throw new Exception("Resultado requerido.");
            return _dao.Cerrar(id, resultado, updatedBy);
        }
    }
}
