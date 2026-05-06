using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de negocio para Hallazgos.
    /// Alineado a HallazgoDAO (Insertar, Actualizar, ObtenerPorInspeccion, CerrarHallazgo, ObtenerEstadisticas).
    /// </summary>
    public class HallazgoBL
    {
        private readonly HallazgoDAO _hallazgoDAO;
        private readonly InspeccionDAO _inspeccionDAO;

        public HallazgoBL()
        {
            _hallazgoDAO = new HallazgoDAO();
            _inspeccionDAO = new InspeccionDAO(); // OJO: ahora es instancia (no static)
        }

        // Constructor para testing / inyección
        public HallazgoBL(HallazgoDAO hallazgoDAO, InspeccionDAO inspeccionDAO)
        {
            _hallazgoDAO = hallazgoDAO ?? throw new ArgumentNullException(nameof(hallazgoDAO));
            _inspeccionDAO = inspeccionDAO ?? throw new ArgumentNullException(nameof(inspeccionDAO));
        }

        /// <summary>
        /// Lista hallazgos por inspección.
        /// </summary>
        public List<Hallazgo> ObtenerPorInspeccion(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0) return new List<Hallazgo>();
            return _hallazgoDAO.ObtenerPorInspeccion(codigoInspeccion) ?? new List<Hallazgo>();
        }

        /// <summary>
        /// Inserta hallazgo (usa HallazgoDAO.Insertar).
        /// </summary>
        public int Crear(Hallazgo hallazgo, string usuario)
        {
            if (hallazgo == null) throw new ArgumentNullException(nameof(hallazgo));
            if (hallazgo.CodigoInspeccion <= 0) throw new Exception("Código de inspección inválido.");
            if (string.IsNullOrWhiteSpace(hallazgo.Descripcion)) throw new Exception("La descripción es obligatoria.");

            // Validar que exista la inspección (InspeccionDAO es NO estático)
            var inspeccion = _inspeccionDAO.ObtenerPorId(hallazgo.CodigoInspeccion);
            if (inspeccion == null) throw new Exception("No existe la inspección asociada al hallazgo.");

            // Defaults de negocio
            if (string.IsNullOrWhiteSpace(hallazgo.Criticidad)) hallazgo.Criticidad = "MEDIA";
            if (string.IsNullOrWhiteSpace(hallazgo.Estado)) hallazgo.Estado = "ABIERTO";

            // Auditoría (tu Hallazgo tiene CreatedBy/UpdatedBy como string)
            hallazgo.CreatedBy = string.IsNullOrWhiteSpace(hallazgo.CreatedBy) ? (usuario ?? "SISTEMA") : hallazgo.CreatedBy;
            hallazgo.UpdatedBy = string.IsNullOrWhiteSpace(hallazgo.UpdatedBy) ? (usuario ?? "SISTEMA") : hallazgo.UpdatedBy;

            // Nota: en tu DAO, FechaDeteccion es DateTime? (en DAO), pero en el modelo es DateTime (no nullable)
            // Si tu modelo aún tiene DateTime no-null, asegúrate de setearlo:
            if (hallazgo.FechaDeteccion == default(DateTime))
                hallazgo.FechaDeteccion = DateTime.Now;

            int id = _hallazgoDAO.Insertar(hallazgo);
            if (id <= 0) throw new Exception("No se pudo insertar el hallazgo.");

            hallazgo.CodigoHallazgo = id;
            return id;
        }

        /// <summary>
        /// Actualiza hallazgo (usa HallazgoDAO.Actualizar).
        /// </summary>
        public bool Actualizar(Hallazgo hallazgo, string usuario)
        {
            if (hallazgo == null) throw new ArgumentNullException(nameof(hallazgo));
            if (hallazgo.CodigoHallazgo <= 0) throw new Exception("Código de hallazgo inválido.");

            hallazgo.UpdatedBy = string.IsNullOrWhiteSpace(usuario) ? (hallazgo.UpdatedBy ?? "SISTEMA") : usuario;
            hallazgo.UpdatedAt = DateTime.Now;

            return _hallazgoDAO.Actualizar(hallazgo);
        }

        /// <summary>
        /// Cierra hallazgo (usa HallazgoDAO.CerrarHallazgo).
        /// </summary>
        public bool Cerrar(int codigoHallazgo, string accionCorrectiva, string responsable, string usuario)
        {
            if (codigoHallazgo <= 0) throw new Exception("Código de hallazgo inválido.");
            if (string.IsNullOrWhiteSpace(accionCorrectiva)) throw new Exception("Acción correctiva es obligatoria.");
            if (string.IsNullOrWhiteSpace(responsable)) throw new Exception("Responsable es obligatorio.");

            return _hallazgoDAO.CerrarHallazgo(
                codigoHallazgo,
                accionCorrectiva,
                responsable,
                string.IsNullOrWhiteSpace(usuario) ? "SISTEMA" : usuario
            );
        }

        /// <summary>
        /// Estadísticas (usa HallazgoDAO.ObtenerEstadisticas).
        /// </summary>
        public Dictionary<string, int> ObtenerEstadisticas(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0)
                return new Dictionary<string, int> { { "TOTAL", 0 } };

            return _hallazgoDAO.ObtenerEstadisticas(codigoInspeccion)
                   ?? new Dictionary<string, int> { { "TOTAL", 0 } };
        }

        // =========================================================
        // ✅ Métodos opcionales para compatibilidad
        // (Si tu BL anterior exigía ObtenerPorId / Eliminar)
        // =========================================================

        /// <summary>
        /// Obtiene un hallazgo por su identificador.
        /// </summary>
        public Hallazgo ObtenerPorId(int codigoHallazgo)
        {
            if (codigoHallazgo <= 0) return null;
            return _hallazgoDAO.ObtenerPorId(codigoHallazgo);
        }

        /// <summary>
        /// Compat: si necesitas ObtenerPorId, lo resolvemos consultando por inspección
        /// y filtrando (no ideal, pero compila sin tocar DAO).
        /// RECOMENDADO: implementar un método real ObtenerPorId en el DAO.
        /// </summary>
        public Hallazgo ObtenerPorId(int codigoHallazgo, int codigoInspeccion)
        {
            if (codigoHallazgo <= 0) return null;
            if (codigoInspeccion <= 0) return null;

            var lista = _hallazgoDAO.ObtenerPorInspeccion(codigoInspeccion);
            if (lista == null) return null;

            foreach (var h in lista)
                if (h != null && h.CodigoHallazgo == codigoHallazgo)
                    return h;

            return null;
        }

        /// <summary>
        /// Compat: si tu BL antiguo tenía Eliminar, aquí puedes hacer "soft delete"
        /// si tu tabla tiene deleted_at/deleted_by. Como NO lo tienes en tu DAO,
        /// lo dejo como excepción controlada para no romper producción.
        /// </summary>
        public bool Eliminar(int codigoHallazgo, string usuario)
        {
            // Si realmente necesitas eliminar:
            // - agrega columnas deleted_at/deleted_by
            // - implementa EliminarSoft en DAO
            // Por ahora NO lo invento para no tocar BD sin tu aprobación.
            throw new NotSupportedException("Eliminar no está implementado en HallazgoDAO. Implementa un soft delete si lo necesitas.");
        }
    }
}
