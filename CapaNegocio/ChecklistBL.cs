using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de negocio para la gestión de Checklists y sus ítems.
    /// </summary>
    public static class ChecklistBL
    {
        // =====================================================
        // CRUD de Checklist (encabezado)
        // =====================================================

        public static List<Checklist> ObtenerTodos()
        {
            return ChecklistDAO.ObtenerActivos();
        }

        public static Checklist ObtenerPorId(int id)
        {
            if (id <= 0) return null;
            return ChecklistDAO.ObtenerPorId(id);
        }

        public static bool Insertar(Checklist modelo, out string mensaje)
        {
            mensaje = "";

            if (!Validar(modelo, out mensaje))
                return false;

            try
            {
                ChecklistDAO.Insertar(modelo);
                mensaje = "Checklist registrado correctamente.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al registrar checklist: " + ex.Message;
                return false;
            }
        }

        public static bool Actualizar(Checklist modelo, out string mensaje)
        {
            mensaje = "";

            if (modelo == null || modelo.CodigoChecklist <= 0)
            {
                mensaje = "Checklist inválido.";
                return false;
            }

            try
            {
                ChecklistDAO.Actualizar(modelo);
                mensaje = "Checklist actualizado correctamente.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al actualizar checklist: " + ex.Message;
                return false;
            }
        }

        public static bool EliminarLogico(int id, string usuario, out string mensaje)
        {
            mensaje = "";

            if (id <= 0)
            {
                mensaje = "ID inválido para eliminación.";
                return false;
            }

            try
            {
                ChecklistDAO.EliminarLogico(id, usuario);
                mensaje = "Checklist eliminado correctamente.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al eliminar checklist: " + ex.Message;
                return false;
            }
        }

        private static bool Validar(Checklist modelo, out string mensaje)
        {
            mensaje = "";

            if (modelo == null)
            {
                mensaje = "Checklist no puede ser nulo.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modelo.Seccion))
            {
                mensaje = "La sección del checklist es obligatoria.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modelo.Descripcion))
            {
                mensaje = "La descripción del checklist es obligatoria.";
                return false;
            }

            return true;
        }

        // =====================================================
        // Gestión de ítems de Checklist (ChecklistItem)
        // =====================================================

        public static bool InsertarItem(ChecklistItem item, out string mensaje)
        {
            mensaje = string.Empty;

            if (!ValidarItem(item, out mensaje))
                return false;

            try
            {
                ChecklistDAO.Insertar(item);
                mensaje = "Ítem registrado correctamente.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al registrar ítem: " + ex.Message;
                return false;
            }
        }

        public static bool InsertarMasivo(List<ChecklistItem> items, int codigoSolicitud, out string mensaje)
        {
            mensaje = string.Empty;

            if (items == null || items.Count == 0)
            {
                mensaje = "No se recibieron ítems de checklist.";
                return false;
            }

            try
            {
                foreach (var item in items)
                {
                    item.CodigoSolicitud = codigoSolicitud;

                    if (!ValidarItem(item, out mensaje))
                        return false;

                    ChecklistDAO.Insertar(item);
                }

                mensaje = "Checklist guardado correctamente.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al guardar checklist: " + ex.Message;
                return false;
            }
        }

        public static List<ChecklistItem> ObtenerPorSolicitud(int codigoSolicitud)
        {
            return ChecklistDAO.ObtenerPorSolicitud(codigoSolicitud);
        }

        public static Dictionary<string, int> ObtenerEstadisticas(int codigoSolicitud)
        {
            return ChecklistDAO.ObtenerEstadisticasPorSolicitud(codigoSolicitud);
        }

        private static bool ValidarItem(ChecklistItem item, out string mensaje)
        {
            mensaje = string.Empty;

            if (item == null)
            {
                mensaje = "El ítem de checklist no puede ser nulo.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Descripcion))
            {
                mensaje = "La descripción del ítem es obligatoria.";
                return false;
            }

            // Validación opcional de 'Cumple'
            var valoresValidos = new[] { "Si", "No", "N/A", null };
            if (!string.IsNullOrWhiteSpace(item.Cumple) && Array.IndexOf(valoresValidos, item.Cumple) < 0)
            {
                mensaje = "El campo 'Cumple' debe ser 'Si', 'No' o 'N/A'.";
                return false;
            }

            return true;
        }
    }
}
