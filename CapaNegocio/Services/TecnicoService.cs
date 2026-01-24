using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class TecnicoService
    {
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly HallazgoDAO _hallazgoDAO;

        public TecnicoService()
        {
            // Importante: InspeccionDAO y HallazgoDAO NO deben ser estáticos
            _inspeccionDAO = new InspeccionDAO();
            _hallazgoDAO = new HallazgoDAO();
        }

        // 1. Asignar Inspector
        public bool AsignarInspector(int codigoSolicitud, int codigoInspector, DateTime fecha, string tipo, string lugar)
        {
            Inspeccion inspeccion = new Inspeccion();
            inspeccion.CodigoSolicitud = codigoSolicitud;
            inspeccion.CodigoInspector = codigoInspector;
            inspeccion.FechaProgramada = fecha;
            inspeccion.Tipo = tipo;
            inspeccion.Lugar = lugar;
            inspeccion.Estado = "PROGRAMADA";
            inspeccion.CreatedAt = DateTime.Now;

            // Retorna true si el ID generado es mayor a 0
            return _inspeccionDAO.Crear(inspeccion) > 0;
        }

        // 2. Programar o Reprogramar
        public bool ProgramarInspeccion(int codigoInspeccion, DateTime fecha, string lugar)
        {
            Inspeccion insp = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
            if (insp == null) return false;

            insp.FechaProgramada = fecha;
            insp.Lugar = lugar;
            insp.Estado = "PROGRAMADA";
            insp.UpdatedAt = DateTime.Now;

            return _inspeccionDAO.Actualizar(insp);
        }

        // 3. Registrar Hallazgos desde Lista de Chequeo
        public bool RegistrarListaChequeo(int codigoInspeccion, string[] items, string criticidad, string usuario)
        {
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Hallazgo h = new Hallazgo();
                    h.CodigoInspeccion = codigoInspeccion;
                    h.Descripcion = items[i];
                    h.Criticidad = criticidad;
                    h.Estado = "ABIERTO";
                    h.FechaDeteccion = DateTime.Now;
                    h.CreatedBy = usuario;
                    h.CreatedAt = DateTime.Now;

                    _hallazgoDAO.Insertar(h);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 4. Finalizar Inspección y generar resultado
        public bool FinalizarInspeccion(int codigoInspeccion, string resultado, string comentarios, string hallazgos)
        {
            Inspeccion insp = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
            if (insp == null) return false;

            insp.Resultado = resultado; // Ejemplo: "CUMPLE" / "NO CUMPLE"
            insp.Comentarios = comentarios;
            insp.HallazgosPrincipales = hallazgos;
            insp.Estado = "FINALIZADA";
            insp.UpdatedAt = DateTime.Now;

            return _inspeccionDAO.Actualizar(insp);
        }

        public Inspeccion ObtenerInspeccion(int id)
        {
            return _inspeccionDAO.ObtenerPorId(id);
        }
    }
}