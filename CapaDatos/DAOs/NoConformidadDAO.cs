using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class NoConformidadDAO
    {
        private string CS => ConexionDAO.CadenaConexion;
        private const string TABLA = "public.aocr_tbnoconformidad";

        private static bool TryGetOrdinal(NpgsqlDataReader dr, string column, out int ordinal)
        {
            ordinal = -1;
            if (dr == null || string.IsNullOrWhiteSpace(column)) return false;
            try
            {
                ordinal = dr.GetOrdinal(column);
                return ordinal >= 0;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
        }

        private static string LeerTextoOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return dr.GetValue(ordinal).ToString();
        }

        private static DateTime? LeerFechaOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return Convert.ToDateTime(dr.GetValue(ordinal));
        }

        private static int LeerEnteroObligatorio(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return 0;
            return Convert.ToInt32(dr.GetValue(ordinal));
        }
        
        private static int? LeerEnteroOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return Convert.ToInt32(dr.GetValue(ordinal));
        }
        
        private static bool LeerBooleanObligatorio(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return false;
            return Convert.ToBoolean(dr.GetValue(ordinal));
        }

        private NoConformidad MapearDesdeDataReader(NpgsqlDataReader dr)
        {
            try
            {
                return new NoConformidad
                {
                    CodigoNoConformidad = LeerEnteroObligatorio(dr, "codigo_no_conformidad"),
                    CodigoInspeccion = LeerEnteroObligatorio(dr, "codigo_inspeccion"),
                    CodigoInforme = LeerEnteroObligatorio(dr, "codigo_informe"),
                    CodigoSolicitud = LeerEnteroObligatorio(dr, "codigo_solicitud"),
                    TipoRuta = LeerTextoOpcional(dr, "tipo_ruta"),
                    Estado = LeerTextoOpcional(dr, "estado"),
                    NumeroNoConformidad = LeerTextoOpcional(dr, "numero_no_conformidad"),
                    Resumen = LeerTextoOpcional(dr, "resumen"),
                    Detalle = LeerTextoOpcional(dr, "detalle"),FundamentoTecnico = LeerTextoOpcional(dr, "fundamento_tecnico"),
                    AccionesRequeridas = LeerTextoOpcional(dr, "acciones_requeridas"),PlazoSubsanacion = LeerEnteroOpcional(dr, "plazo_subsanacion"),
                    RequiereNuevaInspeccion = LeerBooleanObligatorio(dr, "requiere_nueva_inspeccion"),Version = LeerEnteroObligatorio(dr, "version"),
                    RutaPdf = LeerTextoOpcional(dr, "ruta_pdf"),RutaPdfFirmadoInspector = LeerTextoOpcional(dr, "ruta_pdf_firmado_inspector"),
                    RutaPdfFirmadoCoordinador = LeerTextoOpcional(dr, "ruta_pdf_firmado_coordinador"),RutaPdfSubsanacionRt = LeerTextoOpcional(dr, "ruta_pdf_subsanacion_rt"),
                    HashDocumento = LeerTextoOpcional(dr, "hash_documento"),FechaGeneracion = LeerFechaOpcional(dr, "fecha_generacion"),
                    FechaFirmaInspector = LeerFechaOpcional(dr, "fecha_firma_inspector"),FechaEnvioCoordinador = LeerFechaOpcional(dr, "fecha_envio_coordinador"),
                    FechaDevolucion = LeerFechaOpcional(dr, "fecha_devolucion"),FechaFirmaCoordinador = LeerFechaOpcional(dr, "fecha_firma_coordinador"),
                    FechaNotificacionRt = LeerFechaOpcional(dr, "fecha_notificacion_rt"),FechaSubsanacionRt = LeerFechaOpcional(dr, "fecha_subsanacion_rt"),
                    UsuarioCreacion = LeerEnteroOpcional(dr, "usuario_creacion"),UsuarioFirmaInspector = LeerEnteroOpcional(dr, "usuario_firma_inspector"),
                    UsuarioFirmaCoordinador = LeerEnteroOpcional(dr, "usuario_firma_coordinador"),ObservacionDevolucion = LeerTextoOpcional(dr, "observacion_devolucion"),
                    CreatedAt = LeerFechaOpcional(dr, "created_at"),UpdatedAt = LeerFechaOpcional(dr, "updated_at")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al mapear NoConformidad: {ex.Message}");
                return null;
            }
        }

        public NoConformidad Insertar(NoConformidad entidad, NpgsqlTransaction trx = null)
        {
            string query = $@"
                INSERT INTO {TABLA} (
                    codigo_inspeccion, codigo_informe, codigo_solicitud, tipo_ruta, estado, numero_no_conformidad,
                    resumen, detalle, fundamento_tecnico, acciones_requeridas, plazo_subsanacion, requiere_nueva_inspeccion,
                    version, ruta_pdf, ruta_pdf_firmado_inspector, ruta_pdf_firmado_coordinador, ruta_pdf_subsanacion_rt, hash_documento,
                    fecha_generacion, fecha_firma_inspector, fecha_envio_coordinador, fecha_devolucion,
                    fecha_firma_coordinador, fecha_notificacion_rt, fecha_subsanacion_rt, usuario_creacion, usuario_firma_inspector,
                    usuario_firma_coordinador, observacion_devolucion, created_at
                ) VALUES (
                    @codigo_inspeccion, @codigo_informe, @codigo_solicitud, @tipo_ruta, @estado, @numero_no_conformidad,
                    @resumen, @detalle, @fundamento_tecnico, @acciones_requeridas, @plazo_subsanacion, @requiere_nueva_inspeccion,
                    @version, @ruta_pdf, @ruta_pdf_firmado_inspector, @ruta_pdf_firmado_coordinador, @ruta_pdf_subsanacion_rt, @hash_documento,
                    @fecha_generacion, @fecha_firma_inspector, @fecha_envio_coordinador, @fecha_devolucion,
                    @fecha_firma_coordinador, @fecha_notificacion_rt, @fecha_subsanacion_rt, @usuario_creacion, @usuario_firma_inspector,
                    @usuario_firma_coordinador, @observacion_devolucion, NOW()
                ) RETURNING codigo_no_conformidad;";

            bool closeConnection = false;
            NpgsqlConnection conn = trx?.Connection;

            if (conn == null)
            {
                conn = new NpgsqlConnection(CS);
                conn.Open();
                closeConnection = true;
            }

            try
            {
                using (var cmd = new NpgsqlCommand(query, conn, trx))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", entidad.CodigoInspeccion);
                    cmd.Parameters.AddWithValue("@codigo_informe", entidad.CodigoInforme);
                    cmd.Parameters.AddWithValue("@codigo_solicitud", entidad.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_ruta", entidad.TipoRuta);
                    cmd.Parameters.AddWithValue("@estado", entidad.Estado);
                    cmd.Parameters.AddWithValue("@numero_no_conformidad", (object)entidad.NumeroNoConformidad ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resumen", (object)entidad.Resumen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@detalle", (object)entidad.Detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fundamento_tecnico", (object)entidad.FundamentoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@acciones_requeridas", (object)entidad.AccionesRequeridas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plazo_subsanacion", (object)entidad.PlazoSubsanacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@requiere_nueva_inspeccion", entidad.RequiereNuevaInspeccion);
                    cmd.Parameters.AddWithValue("@version", entidad.Version);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)entidad.RutaPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_inspector", (object)entidad.RutaPdfFirmadoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_coordinador", (object)entidad.RutaPdfFirmadoCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_subsanacion_rt", (object)entidad.RutaPdfSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)entidad.HashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_generacion", (object)entidad.FechaGeneracion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_inspector", (object)entidad.FechaFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_envio_coordinador", (object)entidad.FechaEnvioCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_devolucion", (object)entidad.FechaDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_coordinador", (object)entidad.FechaFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_notificacion_rt", (object)entidad.FechaNotificacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_subsanacion_rt", (object)entidad.FechaSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_creacion", (object)entidad.UsuarioCreacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_inspector", (object)entidad.UsuarioFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_coordinador", (object)entidad.UsuarioFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_devolucion", (object)entidad.ObservacionDevolucion ?? DBNull.Value);

                    var id = cmd.ExecuteScalar();
                    if (id != null)
                    {
                        entidad.CodigoNoConformidad = Convert.ToInt32(id);
                        return entidad;
                    }
                    return null;
                }
            }
            finally
            {
                if (closeConnection)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public bool RegistrarSubsanacionRt(int codigoNoConformidad,string ruta,DateTime fecha)
        {
            if(codigoNoConformidad<=0||string.IsNullOrWhiteSpace(ruta))return false;
            var sql=$@"UPDATE {TABLA} SET ruta_pdf_subsanacion_rt=@ruta,fecha_subsanacion_rt=@fecha,estado='SUBSANADA_RT',observacion_devolucion=NULL,updated_at=NOW()
WHERE codigo_no_conformidad=@id AND UPPER(tipo_ruta)='SIN_INSPECCION' AND estado IN ('FIRMADA_COORDINADOR','EN_SUBSANACION');";
            using(var cn=new NpgsqlConnection(CS))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@ruta",ruta);cmd.Parameters.AddWithValue("@fecha",fecha);cmd.Parameters.AddWithValue("@id",codigoNoConformidad);cn.Open();return cmd.ExecuteNonQuery()==1;}
        }

        public bool ReabrirSubsanacionRt(int codigoNoConformidad)
        {
            var sql = $@"UPDATE {TABLA}
SET ruta_pdf_subsanacion_rt=NULL, fecha_subsanacion_rt=NULL, estado='EN_SUBSANACION', updated_at=NOW()
WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';";
            using (var cn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoNoConformidad);
                cn.Open();
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public bool CerrarSubsanacion(int codigoNoConformidad)
        {
            var sql=$@"UPDATE {TABLA} SET estado='CERRADA',updated_at=NOW() WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';";
            using(var cn=new NpgsqlConnection(CS))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@id",codigoNoConformidad);cn.Open();return cmd.ExecuteNonQuery()==1;}
        }

        public NoConformidad DevolverSubsanacionComoNuevaVersion(int codigoNoConformidad,string observacion)
        {
            if(codigoNoConformidad<=0||string.IsNullOrWhiteSpace(observacion))return null;
            using(var cn=new NpgsqlConnection(CS)){cn.Open();using(var tx=cn.BeginTransaction()){
                using(var close=new NpgsqlCommand($"UPDATE {TABLA} SET estado='SUBSANACION_DEVUELTA',observacion_devolucion=@obs,updated_at=NOW() WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';",cn,tx)){close.Parameters.AddWithValue("@obs",observacion.Trim());close.Parameters.AddWithValue("@id",codigoNoConformidad);if(close.ExecuteNonQuery()!=1)return null;}
                int nuevoId;using(var insert=new NpgsqlCommand($@"INSERT INTO {TABLA}(codigo_inspeccion,codigo_informe,codigo_solicitud,tipo_ruta,estado,numero_no_conformidad,resumen,detalle,fundamento_tecnico,acciones_requeridas,plazo_subsanacion,requiere_nueva_inspeccion,version,ruta_pdf,ruta_pdf_firmado_inspector,ruta_pdf_firmado_coordinador,hash_documento,fecha_generacion,fecha_firma_inspector,fecha_envio_coordinador,fecha_firma_coordinador,fecha_notificacion_rt,usuario_creacion,usuario_firma_inspector,usuario_firma_coordinador,observacion_devolucion,created_at)
SELECT codigo_inspeccion,codigo_informe,codigo_solicitud,tipo_ruta,'EN_SUBSANACION',numero_no_conformidad,resumen,detalle,fundamento_tecnico,acciones_requeridas,plazo_subsanacion,requiere_nueva_inspeccion,version+1,ruta_pdf,ruta_pdf_firmado_inspector,ruta_pdf_firmado_coordinador,hash_documento,fecha_generacion,fecha_firma_inspector,fecha_envio_coordinador,fecha_firma_coordinador,fecha_notificacion_rt,usuario_creacion,usuario_firma_inspector,usuario_firma_coordinador,@obs,NOW() FROM {TABLA} WHERE codigo_no_conformidad=@id RETURNING codigo_no_conformidad;",cn,tx)){insert.Parameters.AddWithValue("@obs",observacion.Trim());insert.Parameters.AddWithValue("@id",codigoNoConformidad);nuevoId=Convert.ToInt32(insert.ExecuteScalar());}
                tx.Commit();return ObtenerPorId(nuevoId);
            }}
        }

        public bool Actualizar(NoConformidad entidad, NpgsqlTransaction trx = null)
        {
            string query = $@"
                UPDATE {TABLA} SET 
                    estado = @estado,
                    numero_no_conformidad = @numero_no_conformidad,
                    resumen = @resumen,
                    detalle = @detalle,
                    fundamento_tecnico = @fundamento_tecnico,
                    acciones_requeridas = @acciones_requeridas,
                    plazo_subsanacion = @plazo_subsanacion,
                    ruta_pdf = @ruta_pdf,
                    ruta_pdf_firmado_inspector = @ruta_pdf_firmado_inspector,
                    ruta_pdf_firmado_coordinador = @ruta_pdf_firmado_coordinador,
                    ruta_pdf_subsanacion_rt = @ruta_pdf_subsanacion_rt,
                    hash_documento = @hash_documento,
                    fecha_firma_inspector = @fecha_firma_inspector,
                    fecha_envio_coordinador = @fecha_envio_coordinador,
                    fecha_devolucion = @fecha_devolucion,
                    fecha_firma_coordinador = @fecha_firma_coordinador,
                    fecha_notificacion_rt = @fecha_notificacion_rt,
                    fecha_subsanacion_rt = @fecha_subsanacion_rt,
                    usuario_firma_inspector = @usuario_firma_inspector,
                    usuario_firma_coordinador = @usuario_firma_coordinador,
                    observacion_devolucion = @observacion_devolucion,
                    updated_at = NOW()
                WHERE codigo_no_conformidad = @codigo_no_conformidad;";

            bool closeConnection = false;
            NpgsqlConnection conn = trx?.Connection;

            if (conn == null)
            {
                conn = new NpgsqlConnection(CS);
                conn.Open();
                closeConnection = true;
            }

            try
            {
                using (var cmd = new NpgsqlCommand(query, conn, trx))
                {
                    cmd.Parameters.AddWithValue("@codigo_no_conformidad", entidad.CodigoNoConformidad);
                    cmd.Parameters.AddWithValue("@estado", entidad.Estado);
                    cmd.Parameters.AddWithValue("@numero_no_conformidad", (object)entidad.NumeroNoConformidad ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resumen", (object)entidad.Resumen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@detalle", (object)entidad.Detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fundamento_tecnico", (object)entidad.FundamentoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@acciones_requeridas", (object)entidad.AccionesRequeridas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plazo_subsanacion", (object)entidad.PlazoSubsanacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)entidad.RutaPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_inspector", (object)entidad.RutaPdfFirmadoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_coordinador", (object)entidad.RutaPdfFirmadoCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_subsanacion_rt", (object)entidad.RutaPdfSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)entidad.HashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_inspector", (object)entidad.FechaFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_envio_coordinador", (object)entidad.FechaEnvioCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_devolucion", (object)entidad.FechaDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_coordinador", (object)entidad.FechaFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_notificacion_rt", (object)entidad.FechaNotificacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_subsanacion_rt", (object)entidad.FechaSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_inspector", (object)entidad.UsuarioFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_coordinador", (object)entidad.UsuarioFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_devolucion", (object)entidad.ObservacionDevolucion ?? DBNull.Value);

                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
            finally
            {
                if (closeConnection)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public NoConformidad ObtenerPorId(int codigoNoConformidad)
        {
            string query = $"SELECT * FROM {TABLA} WHERE codigo_no_conformidad = @id LIMIT 1;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoNoConformidad);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        return MapearDesdeDataReader(dr);
                    }
                }
            }
            return null;
        }

        public List<NoConformidad> ObtenerPorInspeccion(int codigoInspeccion)
        {
            var lista = new List<NoConformidad>();
            string query = $"SELECT * FROM {TABLA} WHERE codigo_inspeccion = @id ORDER BY version DESC;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoInspeccion);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var item = MapearDesdeDataReader(dr);
                        if (item != null) lista.Add(item);
                    }
                }
            }
            return lista;
        }

        public List<NoConformidad> ListarPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<NoConformidad>();
            string query = $"SELECT * FROM {TABLA} WHERE codigo_solicitud = @id ORDER BY version DESC, codigo_no_conformidad DESC;";
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var item = MapearDesdeDataReader(dr);
                        if (item != null) lista.Add(item);
                    }
                }
            }
            return lista;
        }
        
        public NoConformidad ObtenerUltimaPorInforme(int codigoInforme)
        {
            string query = $"SELECT * FROM {TABLA} WHERE codigo_informe = @id ORDER BY version DESC LIMIT 1;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoInforme);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        return MapearDesdeDataReader(dr);
                    }
                }
            }
            return null;
        }
    }
}
