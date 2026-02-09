// ===================================================================
// SubsanacionDAO.cs
// ===================================================================
// Propósito: Acceso a datos para gestión de subsanaciones de solicitudes AOCR
// Tabla: aocr_tbsubsanacion
// 
// Operaciones CRUD:
//   - Insertar nueva subsanación
//   - Actualizar subsanación (cuando operador responde)
//   - Obtener por código de solicitud
//   - Obtener pendientes
//   - Obtener historial completo
//
// Fecha: 2025-01-05
// ===================================================================

using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Dapper;
using CapaModelo;
using CapaDatos.Infra;

namespace CapaDatos.DAOs
{
    public class SubsanacionDAO
    {
        private static NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        // ===============================================================
        // INSERTAR NUEVA SUBSANACION
        // ===============================================================
        public bool Insertar(Subsanacion subsanacion)
        {
            const string sql = @"
                INSERT INTO aocr_tbsubsanacion (
                    codigo_solicitud, 
                    fecha_solicitud, 
                    observaciones, 
                    codigo_usuario_solicitante,
                    estado,
                    created_at,
                    created_by
                )
                VALUES (
                    @CodigoSolicitud, 
                    @FechaSolicitud, 
                    @Observaciones, 
                    @CodigoUsuarioSolicitante,
                    @Estado,
                    CURRENT_TIMESTAMP,
                    @CreatedBy
                )
                RETURNING codigo_subsanacion;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    int codigo = cn.ExecuteScalar<int>(sql, new
                    {
                        subsanacion.CodigoSolicitud,
                        FechaSolicitud = subsanacion.FechaSolicitud ?? DateTime.Now,
                        subsanacion.Observaciones,
                        subsanacion.CodigoUsuarioSolicitante,
                        Estado = subsanacion.Estado ?? "PENDIENTE",
                        CreatedBy = subsanacion.CreatedBy ?? "SYSTEM"
                    });

                    subsanacion.CodigoSubsanacion = codigo;
                    return codigo > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al insertar subsanación: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // ACTUALIZAR SUBSANACION (cuando operador responde)
        // ===============================================================
        public bool Actualizar(Subsanacion subsanacion)
        {
            const string sql = @"
                UPDATE aocr_tbsubsanacion
                SET 
                    fecha_respuesta = @FechaRespuesta,
                    respuesta = @Respuesta,
                    codigo_usuario_respuesta = @CodigoUsuarioRespuesta,
                    estado = @Estado,
                    updated_at = CURRENT_TIMESTAMP,
                    updated_by = @UpdatedBy
                WHERE 
                    codigo_subsanacion = @CodigoSubsanacion;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    int rowsAffected = cn.Execute(sql, new
                    {
                        subsanacion.FechaRespuesta,
                        subsanacion.Respuesta,
                        subsanacion.CodigoUsuarioRespuesta,
                        subsanacion.Estado,
                        UpdatedBy = subsanacion.UpdatedBy ?? "SYSTEM",
                        subsanacion.CodigoSubsanacion
                    });

                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al actualizar subsanación: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // OBTENER POR ID
        // ===============================================================
        public Subsanacion ObtenerPorId(int codigoSubsanacion)
        {
            const string sql = @"
                SELECT 
                    codigo_subsanacion AS CodigoSubsanacion,
                    codigo_solicitud AS CodigoSolicitud,
                    fecha_solicitud AS FechaSolicitud,
                    observaciones AS Observaciones,
                    codigo_usuario_solicitante AS CodigoUsuarioSolicitante,
                    fecha_respuesta AS FechaRespuesta,
                    respuesta AS Respuesta,
                    codigo_usuario_respuesta AS CodigoUsuarioRespuesta,
                    estado AS Estado,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    created_by AS CreatedBy,
                    updated_by AS UpdatedBy
                FROM aocr_tbsubsanacion
                WHERE codigo_subsanacion = @id;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.QueryFirstOrDefault<Subsanacion>(sql, new { id = codigoSubsanacion });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener subsanación: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // OBTENER PENDIENTE POR SOLICITUD (última pendiente)
        // ===============================================================
        public Subsanacion ObtenerPendientePorSolicitud(int codigoSolicitud)
        {
            const string sql = @"
                SELECT 
                    codigo_subsanacion AS CodigoSubsanacion,
                    codigo_solicitud AS CodigoSolicitud,
                    fecha_solicitud AS FechaSolicitud,
                    observaciones AS Observaciones,
                    codigo_usuario_solicitante AS CodigoUsuarioSolicitante,
                    fecha_respuesta AS FechaRespuesta,
                    respuesta AS Respuesta,
                    codigo_usuario_respuesta AS CodigoUsuarioRespuesta,
                    estado AS Estado,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    created_by AS CreatedBy,
                    updated_by AS UpdatedBy
                FROM aocr_tbsubsanacion
                WHERE 
                    codigo_solicitud = @solicitudId
                    AND estado = 'PENDIENTE'
                ORDER BY fecha_solicitud DESC
                LIMIT 1;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.QueryFirstOrDefault<Subsanacion>(sql, new { solicitudId = codigoSolicitud });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener subsanación pendiente: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // OBTENER TODAS POR SOLICITUD (historial completo)
        // ===============================================================
        public List<Subsanacion> ObtenerPorSolicitud(int codigoSolicitud)
        {
            const string sql = @"
                SELECT 
                    codigo_subsanacion AS CodigoSubsanacion,
                    codigo_solicitud AS CodigoSolicitud,
                    fecha_solicitud AS FechaSolicitud,
                    observaciones AS Observaciones,
                    codigo_usuario_solicitante AS CodigoUsuarioSolicitante,
                    fecha_respuesta AS FechaRespuesta,
                    respuesta AS Respuesta,
                    codigo_usuario_respuesta AS CodigoUsuarioRespuesta,
                    estado AS Estado,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    created_by AS CreatedBy,
                    updated_by AS UpdatedBy
                FROM aocr_tbsubsanacion
                WHERE codigo_solicitud = @solicitudId
                ORDER BY fecha_solicitud DESC;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.Query<Subsanacion>(sql, new { solicitudId = codigoSolicitud }).AsList();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener subsanaciones por solicitud: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // OBTENER TODAS PENDIENTES (para dashboard)
        // ===============================================================
        public List<Subsanacion> ObtenerTodasPendientes()
        {
            const string sql = @"
                SELECT 
                    s.codigo_subsanacion AS CodigoSubsanacion,
                    s.codigo_solicitud AS CodigoSolicitud,
                    s.fecha_solicitud AS FechaSolicitud,
                    s.observaciones AS Observaciones,
                    s.codigo_usuario_solicitante AS CodigoUsuarioSolicitante,
                    s.fecha_respuesta AS FechaRespuesta,
                    s.respuesta AS Respuesta,
                    s.codigo_usuario_respuesta AS CodigoUsuarioRespuesta,
                    s.estado AS Estado,
                    s.created_at AS CreatedAt,
                    s.updated_at AS UpdatedAt,
                    s.created_by AS CreatedBy,
                    s.updated_by AS UpdatedBy
                FROM aocr_tbsubsanacion s
                WHERE s.estado = 'PENDIENTE'
                ORDER BY s.fecha_solicitud ASC;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.Query<Subsanacion>(sql).AsList();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener subsanaciones pendientes: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // OBTENER SUBSANACIONES CON DETALLES (JOIN con solicitud)
        // ===============================================================
        public List<dynamic> ObtenerConDetalles()
        {
            const string sql = @"
                SELECT 
                    s.codigo_subsanacion,
                    s.codigo_solicitud,
                    sol.numero_solicitud,
                    sol.nombre_operador,
                    s.fecha_solicitud,
                    s.observaciones,
                    s.estado,
                    CASE 
                        WHEN s.fecha_respuesta IS NULL THEN 
                            EXTRACT(DAY FROM CURRENT_TIMESTAMP - s.fecha_solicitud)::INTEGER
                        ELSE 0
                    END AS dias_pendiente,
                    u_sol.nombre || ' ' || u_sol.apellido AS tecnico_solicitante
                FROM aocr_tbsubsanacion s
                INNER JOIN aocr_tbsolicitud sol ON s.codigo_solicitud = sol.codigo_solicitud
                LEFT JOIN aocr_tbusuario u_sol ON s.codigo_usuario_solicitante = u_sol.codigo_usuario
                WHERE s.estado = 'PENDIENTE'
                ORDER BY s.fecha_solicitud ASC;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.Query(sql).AsList();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener subsanaciones con detalles: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // CONTAR PENDIENTES POR OPERADOR
        // ===============================================================
        public int ContarPendientesPorOperador(int codigoUsuario)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM aocr_tbsubsanacion s
                INNER JOIN aocr_tbsolicitud sol ON s.codigo_solicitud = sol.codigo_solicitud
                WHERE 
                    s.estado = 'PENDIENTE'
                    AND sol.codigo_usuario = @usuarioId;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.ExecuteScalar<int>(sql, new { usuarioId = codigoUsuario });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al contar subsanaciones pendientes: " + ex.Message, ex);
            }
        }

        // ===============================================================
        // ELIMINAR (lógico - marcar como cancelada)
        // ===============================================================
        public bool Eliminar(int codigoSubsanacion)
        {
            const string sql = @"
                UPDATE aocr_tbsubsanacion
                SET 
                    estado = 'CANCELADA',
                    updated_at = CURRENT_TIMESTAMP
                WHERE 
                    codigo_subsanacion = @id;";

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    return cn.Execute(sql, new { id = codigoSubsanacion }) > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al eliminar subsanación: " + ex.Message, ex);
            }
        }
    }
}
