using System;
using IBM.Data.DB2.iSeries;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class CD_UbicacionUsuario
    {
        private static CD_UbicacionUsuario _instancia;
        private readonly string _connectionString;

        private CD_UbicacionUsuario()
        {
            var configService = new SecureConfigurationService();
            var creds = configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
        }

        public static CD_UbicacionUsuario Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = new CD_UbicacionUsuario();
                }

                return _instancia;
            }
        }

        public UbicacionUsuarioRecord UbicacionUsuarioPorCiudad(string codCiudad)
        {
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                return null;
            }

            const string query = @"
                SELECT
                    COALESCE(OPUOID, 0) AS OidUbicacion,
                    COALESCE(TRIM(OPUEST), '') AS Estacion,
                    COALESCE(TRIM(OPUCOD), '') AS CodigoCiudad
                FROM OPUARC01
                WHERE TRIM(OPUCOD) = @codCiudad
                  AND OPUOID > 2
                FETCH FIRST 1 ROW ONLY";

            return EjecutarConsultaUbicacion(query, codCiudad);
        }

        public UbicacionUsuarioRecord UbicacionAeropuertoUsuarioPorCiudad(string codCiudad)
        {
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                return null;
            }

            const string query = @"
                SELECT
                    COALESCE(OIDOI2, 0) AS OidUbicacion,
                    COALESCE(TRIM(OIDNO2), '') AS Estacion,
                    COALESCE(TRIM(OIDCO3), '') AS CodigoCiudad
                FROM OIDAR2
                WHERE TRIM(OIDCO3) = @codCiudad
                FETCH FIRST 1 ROW ONLY";

            return EjecutarConsultaUbicacion(query, codCiudad);
        }

        private UbicacionUsuarioRecord EjecutarConsultaUbicacion(string query, string codCiudad)
        {
            try
            {
                using (var conexion = new iDB2Connection(_connectionString))
                using (var cmd = new iDB2Command(query, conexion))
                {
                    cmd.Parameters.Add("@codCiudad", iDB2DbType.iDB2VarChar).Value = codCiudad.Trim().ToUpperInvariant();
                    conexion.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                        {
                            return null;
                        }

                        return new UbicacionUsuarioRecord
                        {
                            OidUbicacion = dr["OidUbicacion"] != DBNull.Value
                                ? Convert.ToDecimal(dr["OidUbicacion"])
                                : 0m,
                            Estacion = dr["Estacion"] != DBNull.Value ? dr["Estacion"].ToString() : string.Empty,
                            CodigoCiudad = dr["CodigoCiudad"] != DBNull.Value ? dr["CodigoCiudad"].ToString() : string.Empty
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static string BuildConnectionString(AS400Credentials creds)
        {
            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                throw new InvalidOperationException("Servidor AS400 no configurado.");
            }

            var defaultCollection = !string.IsNullOrWhiteSpace(creds.Library)
                ? creds.Library
                : creds.Database;

            return string.Format(
                "DataSource={0};UserID={1};Password={2};DefaultCollection={3};",
                creds.Server,
                creds.UserId,
                creds.Password,
                defaultCollection);
        }
    }
}
