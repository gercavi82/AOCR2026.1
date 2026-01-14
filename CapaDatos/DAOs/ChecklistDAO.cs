using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class ChecklistDAO
    {
        public static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
            ?? throw new InvalidOperationException("Conexión no configurada.");

        // ========================================================
        // MÉTODOS SOLICITADOS POR BL (CORREGIDOS)
        // ========================================================

        public static List<Checklist> ObtenerActivos()
        {
            var lista = new List<Checklist>();
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                const string sql = "SELECT * FROM aocr_tbchecklist WHERE activo = true";
                var cmd = new NpgsqlCommand(sql, cn);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read()) { /* Tu lógica de mapeo */ }
                }
            }
            return lista;
        }

        // FIX CS0117: ObtenerPorId
        public static Checklist ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                const string sql = "SELECT * FROM aocr_tbchecklist WHERE codigo_checklist = @id";
                var cmd = new NpgsqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                // Retornar objeto mapeado
                return null; // Cambiar por tu mapeador
            }
        }

        // FIX CS0117: ObtenerPorSolicitud
        public static List<ChecklistItem> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<ChecklistItem>();
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                const string sql = "SELECT * FROM aocr_tbchecklist_item WHERE codigo_solicitud = @id";
                var cmd = new NpgsqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                cn.Open();
                using (var rd = cmd.ExecuteReader()) { /* Mapear */ }
            }
            return lista;
        }

        // FIX CS0117: ObtenerEstadisticasPorSolicitud
        public static Dictionary<string, int> ObtenerEstadisticasPorSolicitud(int id)
        {
            var stats = new Dictionary<string, int> { { "Cumple", 0 }, { "NoCumple", 0 }, { "Pendiente", 0 } };
            // Tu lógica de conteo aquí...
            return stats;
        }

        // FIX CS1501: EliminarLogico con 2 argumentos
        public static bool EliminarLogico(int id, string usuario)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                const string sql = "UPDATE aocr_tbchecklist SET activo = false WHERE codigo_checklist = @id";
                var cmd = new NpgsqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // FIX CS0117: InsertarResultado
        public static bool InsertarResultado(ChecklistItem item, NpgsqlConnection cn, NpgsqlTransaction trans)
        {
            const string sql = "INSERT INTO aocr_tbchecklist_item (codigo_solicitud, descripcion, cumple) VALUES (@sol, @des, @cum)";
            using (var cmd = new NpgsqlCommand(sql, cn, trans))
            {
                cmd.Parameters.AddWithValue("@sol", item.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@des", item.Descripcion);
                cmd.Parameters.AddWithValue("@cum", (object)item.Cumple ?? DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public static bool Insertar(Checklist obj) { /* Tu código */ return true; }
        public static bool Actualizar(Checklist obj) { /* Tu código */ return true; }
    }
}