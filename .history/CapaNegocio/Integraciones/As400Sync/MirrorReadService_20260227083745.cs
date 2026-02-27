using System;
using System.Collections.Generic;
using Npgsql;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class MirrorUsuarioDto
    {
        public string CodigoUsuario { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Correo { get; set; }
        public string EstadoActividad { get; set; }
        public string CodigoRol { get; set; }
        public string CodigoCiudad { get; set; }
        public string Cargo { get; set; }
        public string NombreCorto { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorCompaniaDto
    {
        public string CodigoOaci { get; set; }
        public string CodigoIata { get; set; }
        public string CodigoNumeroCia { get; set; }
        public string NombreCompania { get; set; }
    }

    public class MirrorFr3CabeceraDto
    {
        public decimal Secuencial { get; set; }
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public string FechaControlVuelo { get; set; }
        public string TipoOperacion { get; set; }
        public string RutaPlanVuelo { get; set; }
        public int NumAterrizaPais { get; set; }
        public decimal Total { get; set; }
        public decimal GranTotal { get; set; }
        public string Autorizacion { get; set; }
        public string Observacion { get; set; }
        public string Ruc { get; set; }
        public string NombreCliente { get; set; }
        public string Estado { get; set; }
        public string NacInter { get; set; }
        public string NombreCia { get; set; }
        public string Matricula { get; set; }
        public decimal ValorCharter { get; set; }
        public string FormaPago { get; set; }
        public string CodigoBanco { get; set; }
        public string Deposito { get; set; }
        public string NumeroFactura { get; set; }
        public string FechaCreacion { get; set; }
        public string Procesado { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorSyncStatusDto
    {
        public string Tabla { get; set; }
        public string Estado { get; set; }
        public DateTime? UltimaSync { get; set; }
        public string UltimaClaveSync { get; set; }
        public string UltimoError { get; set; }
        public DateTime ActualizadoEn { get; set; }
    }

    public class MirrorReadService
    {
        private readonly string _connectionString;

        public MirrorReadService()
        {
            var env = As400MirrorSyncOptionsFactory.Create();
            _connectionString = env.PostgresMirrorConnectionString;
        }

        public MirrorUsuarioDto ObtenerUsuarioPorCodigo(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            const string sql = @"
                SELECT u.usucod, u.usunom, u.usuape, u.usucor, u.usuest, u.usuco4, u.usuco5,
                       a.usucar, a.usuno1, u._source_updated_at, u._mirror_synced_at
                  FROM mirror_raw.usuarc u
             LEFT JOIN mirror_raw.usuar1 a ON a.usuco8 = u.usucod
                 WHERE u.usucod = @codigo
                   AND COALESCE(u._is_deleted, false) = false
                 LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", codigoUsuario.Trim());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;

                        return new MirrorUsuarioDto
                        {
                            CodigoUsuario = rd.IsDBNull(0) ? null : rd.GetString(0),
                            Nombres = rd.IsDBNull(1) ? null : rd.GetString(1),
                            Apellidos = rd.IsDBNull(2) ? null : rd.GetString(2),
                            Correo = rd.IsDBNull(3) ? null : rd.GetString(3),
                            EstadoActividad = rd.IsDBNull(4) ? null : rd.GetString(4),
                            CodigoRol = rd.IsDBNull(5) ? null : rd.GetString(5),
                            CodigoCiudad = rd.IsDBNull(6) ? null : rd.GetString(6),
                            Cargo = rd.IsDBNull(7) ? null : rd.GetString(7),
                            NombreCorto = rd.IsDBNull(8) ? null : rd.GetString(8),
                            SourceUpdatedAt = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                            MirrorSyncedAt = rd.IsDBNull(10) ? DateTime.MinValue : rd.GetDateTime(10)
                        };
                    }
                }
            }
            catch (PostgresException ex)
            {
                // Mirror no desplegado todavía: fallback silencioso para no romper AOCR
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerUsuarioPorCodigo no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error consultando usuario en espejo", ex.ToString(), "MirrorReadService");
                return null;
            }
        }

        public IList<MirrorCompaniaDto> ListarCompaniasActivas(int take)
        {
            var list = new List<MirrorCompaniaDto>();
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return list;
            }

            if (take <= 0) take = 100;

            const string sql = @"
                SELECT ciacod, ciaco2, ciaco3, cianom
                  FROM mirror_raw.ciaarc
                 WHERE COALESCE(_is_deleted, false) = false
                   AND TRIM(COALESCE(ciaest, '')) = 'AC'
              ORDER BY cianom
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorCompaniaDto
                            {
                                CodigoOaci = rd.IsDBNull(0) ? null : rd.GetString(0),
                                CodigoIata = rd.IsDBNull(1) ? null : rd.GetString(1),
                                CodigoNumeroCia = rd.IsDBNull(2) ? null : rd.GetString(2),
                                NombreCompania = rd.IsDBNull(3) ? null : rd.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarCompaniasActivas no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }
    }
}
