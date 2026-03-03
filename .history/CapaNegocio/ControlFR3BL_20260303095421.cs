using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Services;

namespace CapaNegocio
{
    /// <summary>
    /// Capa de negocio para Control FR3 (vuelos charter/especiales)
    /// </summary>
    public class ControlFR3BL
    {
        private readonly ControlFR3DAO _dao;

        public ControlFR3BL()
        {
            var config = new SecureConfigurationService();
            var connectionString = config.GetConnectionString("PostgreSQL");
            _dao = new ControlFR3DAO(connectionString);
        }

        public ControlFR3BL(string connectionString)
        {
            _dao = new ControlFR3DAO(connectionString);
        }

        #region Consultas

        /// <summary>
        /// Obtiene un control FR3 por su ID
        /// </summary>
        public async Task<ControlFR3> ObtenerPorIdAsync(int id)
        {
            return await _dao.ObtenerPorIdAsync(id);
        }

        /// <summary>
        /// Obtiene un control FR3 por secuencial, aeropuerto y año
        /// </summary>
        public ControlFR3 ObtenerPorSecuencial(decimal secuencial, string aeropuerto, string anio)
        {
            return _dao.ObtenerPorSecuencial(secuencial, aeropuerto, anio);
        }

        /// <summary>
        /// Lista controles FR3 con filtros opcionales
        /// </summary>
        public List<ControlFR3> Listar(string aeropuerto = null, string anio = null, string estado = null)
        {
            return _dao.Listar(aeropuerto, anio, estado);
        }

        /// <summary>
        /// Lista controles FR3 por matrícula de aeronave
        /// </summary>
        public List<ControlFR3> ListarPorMatricula(string matricula)
        {
            if (string.IsNullOrWhiteSpace(matricula))
                return new List<ControlFR3>();

            return _dao.ListarPorMatricula(matricula);
        }

        /// <summary>
        /// Obtiene estadísticas de FR3
        /// </summary>
        public Dictionary<string, int> ObtenerEstadisticas(string aeropuerto = null)
        {
            return _dao.ObtenerEstadisticas(aeropuerto);
        }

        /// <summary>
        /// Obtiene los detalles de un control FR3
        /// </summary>
        public List<ControlFR3Detalle> ObtenerDetalles(int controlFR3Id)
        {
            return _dao.ObtenerDetallesPorControlId(controlFR3Id);
        }

        #endregion

        #region Operaciones

        /// <summary>
        /// Crea un nuevo control FR3 con validación de negocio
        /// </summary>
        public async Task<int> CrearAsync(ControlFR3 control)
        {
            // Validaciones de negocio
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (string.IsNullOrWhiteSpace(control.Aeropuerto))
                throw new ArgumentException("El aeropuerto es obligatorio.");

            if (string.IsNullOrWhiteSpace(control.Matricula))
                throw new ArgumentException("La matrícula es obligatoria.");

            if (string.IsNullOrWhiteSpace(control.Ruc))
                throw new ArgumentException("El RUC es obligatorio.");

            if (control.GranTotal <= 0)
                throw new ArgumentException("El gran total debe ser mayor a cero.");

            // Asignar año si no tiene
            if (string.IsNullOrWhiteSpace(control.Anio))
                control.Anio = DateTime.Now.Year.ToString();

            // Asignar NacInter por defecto
            if (string.IsNullOrWhiteSpace(control.NacInter))
                control.NacInter = "N"; // Nacional por defecto

            return await _dao.InsertarAsync(control);
        }

        /// <summary>
        /// Crea un nuevo control FR3 (síncrono)
        /// </summary>
        public int Crear(ControlFR3 control)
        {
            return CrearAsync(control).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Actualiza un control FR3 existente
        /// </summary>
        public async Task<bool> ActualizarAsync(ControlFR3 control)
        {
            if (control == null || control.Id <= 0)
                throw new ArgumentException("El control FR3 no es válido.");

            return await _dao.ActualizarAsync(control);
        }

        /// <summary>
        /// Cambia el estado de un control FR3
        /// </summary>
        public bool CambiarEstado(int id, string nuevoEstado)
        {
            if (id <= 0)
                throw new ArgumentException("ID no válido.");

            if (string.IsNullOrWhiteSpace(nuevoEstado))
                throw new ArgumentException("El nuevo estado es obligatorio.");

            return _dao.CambiarEstado(id, nuevoEstado);
        }

        /// <summary>
        /// Elimina lógicamente un control FR3
        /// </summary>
        public bool Eliminar(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID no válido.");

            return _dao.Eliminar(id);
        }

        /// <summary>
        /// Agrega un detalle adicional a un control FR3 existente
        /// </summary>
        public int AgregarDetalle(ControlFR3Detalle detalle)
        {
            if (detalle == null)
                throw new ArgumentNullException(nameof(detalle));

            if (detalle.ControlFR3Id <= 0)
                throw new ArgumentException("Debe indicar el ID del control FR3.");

            if (!detalle.EsValido())
                throw new ArgumentException("El detalle no es válido.");

            return _dao.InsertarDetalle(detalle);
        }

        #endregion
    }
}
