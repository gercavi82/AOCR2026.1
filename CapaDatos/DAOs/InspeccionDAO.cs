using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class InspeccionDAO
    {
        private static NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        // =========================================================
        // LISTAR POR SOLICITUD
        // =========================================================
        public static List<Inspeccion> ObtenerPorSolicitud(int codigoSolicitud)
        {
            using (var con = CrearConexion())
            {
                con.Open();

                const string sql = @"
                SELECT
                    codigo_inspeccion       AS CodigoInspeccion,
                    codigo_solicitud        AS CodigoSolicitud,
                    numero_inspeccion       AS NumeroInspeccion,
                    tipo                    AS Tipo,
                    fecha_programada        AS FechaProgramada,
                    hora_programada         AS HoraProgramada,
                    fecha_realizada         AS FechaRealizada,
                    hora_inicio             AS HoraInicio,
                    hora_fin                AS HoraFin,
                    codigo_inspector        AS CodigoInspector,
                    lugar                   AS Lugar,
                    resultado               AS Resultado,
                    comentarios             AS Comentarios,
                    observaciones_generales AS ObservacionesGenerales,
                    hallazgos_principales   AS HallazgosPrincipales,
                    recomendaciones         AS Recomendaciones,
                    estado                  AS Estado,
                    completada              AS Completada,
                    aprobada                AS Aprobada,
                    created_at              AS CreatedAt,
                    updated_at              AS UpdatedAt,
                    created_by              AS CreatedBy,
                    updated_by              AS UpdatedBy
                FROM aocr_tbinspeccion
                WHERE codigo_solicitud = @codigoSolicitud
                ORDER BY codigo_inspeccion DESC;";

                return con.Query<Inspeccion>(sql, new { codigoSolicitud }).ToList();
            }
        }

        // =========================================================
        // OBTENER POR ID
        // =========================================================
        public static Inspeccion ObtenerPorId(int codigoInspeccion)
        {
            using (var con = CrearConexion())
            {
                con.Open();

                const string sql = @"
                SELECT
                    codigo_inspeccion       AS CodigoInspeccion,
                    codigo_solicitud        AS CodigoSolicitud,
                    numero_inspeccion       AS NumeroInspeccion,
                    tipo                    AS Tipo,
                    fecha_programada        AS FechaProgramada,
                    hora_programada         AS HoraProgramada,
                    fecha_realizada         AS FechaRealizada,
                    hora_inicio             AS HoraInicio,
                    hora_fin                AS HoraFin,
                    codigo_inspector        AS CodigoInspector,
                    lugar                   AS Lugar,
                    resultado               AS Resultado,
                    comentarios             AS Comentarios,
                    observaciones_generales AS ObservacionesGenerales,
                    hallazgos_principales   AS HallazgosPrincipales,
                    recomendaciones         AS Recomendaciones,
                    estado                  AS Estado,
                    completada              AS Completada,
                    aprobada                AS Aprobada,
                    created_at              AS CreatedAt,
                    updated_at              AS UpdatedAt,
                    created_by              AS CreatedBy,
                    updated_by              AS UpdatedBy
                FROM aocr_tbinspeccion
                WHERE codigo_inspeccion = @codigoInspeccion;";

                return con.QueryFirstOrDefault<Inspeccion>(sql, new { codigoInspeccion });
            }
        }

        // =========================================================
        // CREAR INSPECCIÓN
        // =========================================================
        public static int Crear(Inspeccion i)
        {
            using (var con = CrearConexion())
            {
                con.Open();

                if (!i.CreatedAt.HasValue)
                    i.CreatedAt = DateTime.Now;

                if (string.IsNullOrWhiteSpace(i.Estado))
                    i.Estado = "SOLICITADA";

                const string sql = @"
                INSERT INTO aocr_tbinspeccion
                (
                    codigo_solicitud,
                    numero_inspeccion,
                    tipo,
                    fecha_programada,
                    hora_programada,
                    codigo_inspector,
                    lugar,
                    estado,
                    completada,
                    aprobada,
                    created_at,
                    created_by
                )
                VALUES
                (
                    @CodigoSolicitud,
                    @NumeroInspeccion,
                    @Tipo,
                    @FechaProgramada,
                    @HoraProgramada,
                    @CodigoInspector,
                    @Lugar,
                    @Estado,
                    COALESCE(@Completada,false),
                    COALESCE(@Aprobada,false),
                    @CreatedAt,
                    @CreatedBy
                )
                RETURNING codigo_inspeccion;";

                return con.ExecuteScalar<int>(sql, i);
            }
        }

        // =========================================================
        // ACTUALIZAR INSPECCIÓN (PLANIFICACIÓN)
        // =========================================================
        public static bool Actualizar(Inspeccion i)
        {
            if (i == null || i.CodigoInspeccion <= 0)
                return false;

            using (var con = CrearConexion())
            {
                con.Open();

                const string sql = @"
                UPDATE aocr_tbinspeccion
                SET
                    tipo                    = @Tipo,
                    fecha_programada        = @FechaProgramada,
                    hora_programada         = @HoraProgramada,
                    lugar                   = @Lugar,
                    comentarios             = @Comentarios,
                    observaciones_generales = @ObservacionesGenerales,
                    estado                  = @Estado,
                    updated_at              = @UpdatedAt,
                    updated_by              = @UpdatedBy
                WHERE codigo_inspeccion = @CodigoInspeccion;";

                return con.Execute(sql, i) > 0;
            }
        }

        // =========================================================
        // CERRAR INSPECCIÓN
        // =========================================================
        public static int CerrarInspeccion(int codigoInspeccion, string resultado, bool aprobada, int codigoUsuario)
        {
            using (var con = CrearConexion())
            {
                con.Open();

                const string sql = @"
                UPDATE aocr_tbinspeccion
                SET
                    resultado = @resultado,
                    aprobada = @aprobada,
                    estado = 'CERRADA',
                    updated_at = CURRENT_TIMESTAMP,
                    updated_by = @codigoUsuario
                WHERE codigo_inspeccion = @codigoInspeccion;";

                return con.Execute(sql, new { codigoInspeccion, resultado, aprobada, codigoUsuario });
            }
        }

        // =========================================================
        // GUARDAR INFORME PDF
        // =========================================================
        public static int GuardarInforme(int idInspeccion, string informePdf, int codigoUsuario)
        {
            using (var con = CrearConexion())
            {
                con.Open();

                const string sql = @"
                UPDATE aocr_tbinspeccion
                SET
                    informe_pdf = @informePdf,
                    updated_at = CURRENT_TIMESTAMP,
                    updated_by = @codigoUsuario
                WHERE codigo_inspeccion = @idInspeccion;";

                return con.Execute(sql, new { idInspeccion, informePdf, codigoUsuario });
            }
        }
    }
}
