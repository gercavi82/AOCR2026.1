using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;
using Npgsql; // Asegúrate de tener instalado el paquete NuGet Npgsql en este proyecto

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de negocio para la gestión de Checklists.
    /// </summary>
    public static class ChecklistBL
    {
        // =====================================================
        // GESTIÓN DE ENCABEZADOS (Checklist)
        // =====================================================

        public static List<Checklist> ObtenerTodos() => ChecklistDAO.ObtenerActivos();

        public static Checklist ObtenerPorId(int id)
        {
            if (id <= 0) return null;
            return ChecklistDAO.ObtenerPorId(id);
        }

        public static bool Insertar(Checklist modelo, out string mensaje)
        {
            if (!Validar(modelo, out mensaje)) return false;

            try
            {
                bool resultado = ChecklistDAO.Insertar(modelo);
                mensaje = resultado ? "Checklist creado con éxito." : "Error: El registro no pudo ser procesado.";
                return resultado;
            }
            catch (Exception ex)
            {
                mensaje = "Falla de infraestructura: " + ex.Message;
                return false;
            }
        }

        public static bool Actualizar(Checklist modelo, out string mensaje)
        {
            if (modelo == null || modelo.CodigoChecklist <= 0)
            {
                mensaje = "Error: Parámetros de actualización inválidos.";
                return false;
            }

            if (!Validar(modelo, out mensaje)) return false;

            try
            {
                return ChecklistDAO.Actualizar(modelo);
            }
            catch (Exception ex)
            {
                mensaje = "Error al actualizar: " + ex.Message;
                return false;
            }
        }

        public static bool EliminarLogico(int id, string usuario, out string mensaje)
        {
            if (id <= 0)
            {
                mensaje = "Identificador de registro inválido.";
                return false;
            }

            // Nota: Asegúrate que tu DAO reciba el parámetro 'usuario' si lo vas a registrar
            bool resultado = ChecklistDAO.EliminarLogico(id, usuario);
            mensaje = resultado ? "Registro dado de baja correctamente." : "No se pudo eliminar el registro.";
            return resultado;
        }

        // =====================================================
        // GESTIÓN DE ÍTEMS (ChecklistItem) CON TRANSACCIONES
        // =====================================================

        public static bool InsertarMasivo(List<ChecklistItem> items, int codigoSolicitud, out string mensaje)
        {
            mensaje = string.Empty;

            if (items == null || items.Count == 0)
            {
                mensaje = "La carga de datos está vacía.";
                return false;
            }

            // Usamos la ConnectionString que ahora es PUBLIC en el DAO
            using (var cn = new NpgsqlConnection(ChecklistDAO.ConnectionString))
            {
                cn.Open();
                using (var trans = cn.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            item.CodigoSolicitud = codigoSolicitud;

                            if (!ValidarItem(item, out mensaje)) return false;

                            // Llamada al DAO pasando la conexión y transacción activa
                            if (!ChecklistDAO.InsertarResultado(item, cn, trans))
                            {
                                mensaje = $"Error al insertar ítem: {item.Descripcion}";
                                trans.Rollback();
                                return false;
                            }
                        }

                        trans.Commit();
                        mensaje = "Checklist almacenado correctamente.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (cn.State == System.Data.ConnectionState.Open) trans.Rollback();
                        mensaje = "Error crítico al guardar checklist: " + ex.Message;
                        return false;
                    }
                }
            }
        }

        public static List<ChecklistItem> ObtenerPorSolicitud(int codigoSolicitud) =>
            ChecklistDAO.ObtenerPorSolicitud(codigoSolicitud);

        public static Dictionary<string, int> ObtenerEstadisticas(int codigoSolicitud) =>
            ChecklistDAO.ObtenerEstadisticasPorSolicitud(codigoSolicitud);

        // =====================================================
        // VALIDACIONES
        // =====================================================

        private static bool ValidarItem(ChecklistItem item, out string mensaje)
        {
            mensaje = string.Empty;
            if (item == null) { mensaje = "Datos nulos."; return false; }
            if (string.IsNullOrWhiteSpace(item.Descripcion)) { mensaje = "Descripción obligatoria."; return false; }
            return true;
        }

        private static bool Validar(Checklist modelo, out string mensaje)
        {
            mensaje = "";
            if (modelo == null) return false;
            if (string.IsNullOrWhiteSpace(modelo.Seccion)) { mensaje = "Sección obligatoria."; return false; }
            return true;
        }
    }
}