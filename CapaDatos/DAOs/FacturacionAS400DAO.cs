using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class FacturacionAS400DAO : AS400BaseDAO
    {
        private readonly string _schema;
        private readonly string _tablaCabecera;
        private readonly string _tablaDetalle;
        private readonly string _tablaSecuencial;

        public FacturacionAS400DAO(ISecureConfigurationService configService) : base(configService)
        {
            var creds = configService.GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaCabecera = GetSetting("AS400:Facturacion:OPCAR5Table", "OPCAR5").Trim().ToUpperInvariant();
            _tablaDetalle = GetSetting("AS400:Facturacion:OPCAR6Table", "OPCAR6").Trim().ToUpperInvariant();
            _tablaSecuencial = GetSetting("AS400:Facturacion:OPSARCTable", "OPSARC").Trim().ToUpperInvariant();
        }

        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public FacturacionAS400DAO() : base(new SecureConfigurationService())
        {
            var creds = new SecureConfigurationService().GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaCabecera = GetSetting("AS400:Facturacion:OPCAR5Table", "OPCAR5").Trim().ToUpperInvariant();
            _tablaDetalle = GetSetting("AS400:Facturacion:OPCAR6Table", "OPCAR6").Trim().ToUpperInvariant();
            _tablaSecuencial = GetSetting("AS400:Facturacion:OPSARCTable", "OPSARC").Trim().ToUpperInvariant();
        }

        public bool RegistrarFactura(FacturaAs400Record record, out string error)
        {
            error = null;

            if (record == null)
            {
                error = "Registro de factura vacío.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.Aeropuerto))
            {
                error = "Aeropuerto requerido para facturación AS400.";
                return false;
            }

            try
            {
                string localError = null;
                ExecuteWithConnection(conn =>
                {
                    var colsCabecera = GetColumnas(conn, _schema, _tablaCabecera);
                    if (colsCabecera.Count == 0)
                    {
                        throw new InvalidOperationException($"No se encontraron columnas en {_schema}.{_tablaCabecera}.");
                    }

                    var colsDetalle = GetColumnas(conn, _schema, _tablaDetalle);

                    if (FacturaExiste(conn, colsCabecera, record))
                    {
                        localError = "La factura ya existe en AS400.";
                        return;
                    }

                    var secuencial = ObtenerSecuencial(conn, record.Aeropuerto);

                    var valoresCabecera = ConstruirValoresCabecera(record, secuencial);
                    InsertarRegistro(conn, _schema, _tablaCabecera, valoresCabecera, colsCabecera);

                    if (colsDetalle.Count > 0)
                    {
                        var secDetalle = 1;
                        foreach (var det in record.Detalles)
                        {
                            var valoresDetalle = ConstruirValoresDetalle(record, det, secuencial, secDetalle);
                            InsertarRegistro(conn, _schema, _tablaDetalle, valoresDetalle, colsDetalle);
                            secDetalle++;
                        }
                    }

                    // Actualizar tabla de secuenciales si existe
                    TryActualizarSecuencial(conn, record.Aeropuerto, secuencial);
                });

                if (!string.IsNullOrWhiteSpace(localError))
                {
                    error = localError;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private decimal ObtenerSecuencial(OdbcConnection conn, string aeropuerto)
        {
            var sql = $"SELECT COALESCE(MAX(OPCSEC), 0) + 1 AS Secuencial FROM {_schema}.{_tablaCabecera} WHERE OPCAER = ?";
            using (var cmd = new OdbcCommand(sql, conn))
            {
                AddParameter(cmd, aeropuerto, OdbcType.VarChar);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 1m;
                }
                return Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            }
        }

        private void TryActualizarSecuencial(OdbcConnection conn, string aeropuerto, decimal secuencial)
        {
            try
            {
                var cols = GetColumnas(conn, _schema, _tablaSecuencial);
                if (!cols.Contains("OPSSEC") || !cols.Contains("OPSAER"))
                {
                    return;
                }

                var sql = $"UPDATE {_schema}.{_tablaSecuencial} SET OPSSEC = ? WHERE OPSAER = ?";
                using (var cmd = new OdbcCommand(sql, conn))
                {
                    AddParameter(cmd, secuencial, OdbcType.Numeric);
                    AddParameter(cmd, aeropuerto, OdbcType.VarChar);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // best-effort
            }
        }

        private bool FacturaExiste(OdbcConnection conn, HashSet<string> colsCabecera, FacturaAs400Record record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.NumeroFactura))
            {
                return false;
            }

            if (!colsCabecera.Contains("OPCNUM"))
            {
                return false;
            }

            var filtros = new List<string> { "OPCNUM = ?" };
            var parametros = new List<object> { record.NumeroFactura.Trim() };

            if (colsCabecera.Contains("OPCAER") && !string.IsNullOrWhiteSpace(record.Aeropuerto))
            {
                filtros.Add("OPCAER = ?");
                parametros.Add(record.Aeropuerto.Trim());
            }

            if (colsCabecera.Contains("OPCANO") && !string.IsNullOrWhiteSpace(record.Anio))
            {
                filtros.Add("OPCANO = ?");
                parametros.Add(record.Anio.Trim());
            }

            var where = string.Join(" AND ", filtros);
            var sql = $"SELECT 1 FROM {_schema}.{_tablaCabecera} WHERE {where} FETCH FIRST 1 ROWS ONLY";

            using (var cmd = new OdbcCommand(sql, conn))
            {
                foreach (var p in parametros)
                {
                    AddParameter(cmd, p, GetOdbcType(p));
                }
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        private Dictionary<string, object> ConstruirValoresCabecera(FacturaAs400Record record, decimal secuencial)
        {
            var fechaControl = string.IsNullOrWhiteSpace(record.FechaControl)
                ? record.FechaEmision.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                : record.FechaControl.Trim();

            var anio = string.IsNullOrWhiteSpace(record.Anio)
                ? record.FechaEmision.ToString("yyyy", CultureInfo.InvariantCulture)
                : record.Anio.Trim();

            var observacion = string.IsNullOrWhiteSpace(record.Observaciones)
                ? $"FACTURA {record.NumeroFactura}"
                : record.Observaciones;

            var fechaRecepcion = string.IsNullOrWhiteSpace(record.FechaRecepcion)
                ? fechaControl
                : record.FechaRecepcion.Trim();

            var ahora = DateTime.Now;
            var fechaCreacion = ahora.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var horaCreacion = ahora.ToString("HHmmss", CultureInfo.InvariantCulture);

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["OPCSEC"] = secuencial,
                ["OPCAER"] = SafeString(record.Aeropuerto),
                ["OPCANO"] = SafeString(anio),
                ["OPCFE4"] = SafeString(fechaControl),
                ["OPCTIP"] = SafeString(record.TipoOperacion),
                ["OPCRUT"] = SafeString(record.Ruta),
                ["OPCNRO"] = record.NumAterrizaPais,
                ["OPCSUB"] = record.Subtotal,
                ["OPCTOT"] = record.Total,
                ["OPCGRA"] = record.Total,
                ["OPCSON"] = SafeString(record.GranTotalLetras),
                ["OPCAUT"] = SafeString(string.IsNullOrWhiteSpace(record.Autorizacion) ? record.AutorizacionFactura : record.Autorizacion),
                ["OPCOBS"] = SafeString(observacion),
                ["OPCOID"] = record.OidCiaAviacion.HasValue ? (object)record.OidCiaAviacion.Value : 0m,
                ["OPCORI"] = SafeString(record.Origen),
                ["OPCDE7"] = SafeString(record.Destino),
                ["OPCRET"] = SafeString(record.Retorno),
                ["OPCCAL"] = SafeString(record.Callsign),
                ["OPCRU1"] = SafeString(record.Ruc),
                ["OPCEM1"] = SafeString(record.Correo),
                ["OPCNAC"] = SafeString(record.NacInter),
                ["OPCTE1"] = SafeString(record.Telefono),
                ["OPCNO4"] = SafeString(record.Compania),
                ["OPCNO5"] = SafeString(record.Compania),
                ["OPCDA4"] = fechaCreacion,
                ["OPCH01"] = horaCreacion,
                ["OPCOI1"] = record.IdAeropuerto.HasValue ? (object)record.IdAeropuerto.Value : 0m,
                ["OPCOI2"] = record.OidUbicacionCliente.HasValue ? (object)record.OidUbicacionCliente.Value : 0m,
                ["OPCOI3"] = record.OidUbicacion.HasValue ? (object)record.OidUbicacion.Value : 0m,
                ["OPCFOR"] = SafeString(record.FormaPago),
                ["OPCBAN"] = SafeString(record.CodigoBanco),
                ["OPCCHE"] = SafeString(record.Deposito),
                ["OPCNUM"] = SafeString(record.NumeroFactura),
                ["OPCFE9"] = SafeString(fechaRecepcion),
                ["OPCVA6"] = record.Total,
                ["OPCEST"] = "S",
                ["OPCUS7"] = SafeString(record.UsuarioRegistro, "AOCR"),
                ["OPCMOD"] = SafeString(record.Modelo),
                ["OPCPES"] = record.PesoMatricula.HasValue ? (object)record.PesoMatricula.Value : 0m,
                ["OPCC08"] = SafeString(record.CodigoOACICia),
                ["OPCNO6"] = SafeString(record.NombreAeropuerto),
                ["OPCEM2"] = SafeString(record.EmailUsuarioDGAC),
                ["OPCMAT"] = SafeString(record.Matricula),
                ["OPCPRO"] = SafeString(record.Procesado, "E"),
                ["OPCDI2"] = 0m
            };
        }

        private Dictionary<string, object> ConstruirValoresDetalle(
            FacturaAs400Record record,
            FacturaAs400Detalle detalle,
            decimal secuencial,
            int secuencialDetalle)
        {
            var anio = string.IsNullOrWhiteSpace(record.Anio)
                ? record.FechaEmision.ToString("yyyy", CultureInfo.InvariantCulture)
                : record.Anio.Trim();

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["OPCSE2"] = secuencial,
                ["OPCAE1"] = SafeString(record.Aeropuerto),
                ["OPCAN1"] = SafeString(anio),
                ["OPCSE1"] = secuencialDetalle,
                ["OPCTI1"] = SafeString(detalle.TipoCobro, "01"),
                ["OPCOI4"] = detalle.OidFormulario.HasValue ? (object)detalle.OidFormulario.Value : 0m,
                ["OPCC05"] = SafeString(detalle.CodigoContable),
                ["OPCDE8"] = SafeString(detalle.Descripcion),
                ["OPCCAN"] = detalle.Cantidad,
                ["OPCVA1"] = detalle.Valor,
                ["OPCHAC"] = SafeString(detalle.HacerDescuento, "N"),
                ["OPCCOB"] = SafeString(detalle.CobrarImpuesto, "N"),
                ["OPCING"] = SafeString(detalle.IngresarCantidad, "S"),
                ["OPCD01"] = SafeString(detalle.DescripcionCuenta),
                ["OPCC06"] = SafeString(detalle.Codigo, "FITEM"),
                ["OPCTO1"] = detalle.Total
            };
        }

        private void InsertarRegistro(
            OdbcConnection conn,
            string schema,
            string table,
            Dictionary<string, object> valores,
            HashSet<string> columnasDisponibles)
        {
            var columnas = valores.Keys
                .Where(c => columnasDisponibles.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (columnas.Count == 0)
            {
                throw new InvalidOperationException($"No hay columnas válidas para insertar en {schema}.{table}.");
            }

            var colsInsert = string.Join(", ", columnas);
            var placeholders = string.Join(", ", columnas.Select(_ => "?"));
            var sqlInsert = $"INSERT INTO {schema}.{table} ({colsInsert}) VALUES ({placeholders})";

            using (var cmd = new OdbcCommand(sqlInsert, conn))
            {
                foreach (var col in columnas)
                {
                    AddParameter(cmd, valores[col], GetOdbcType(valores[col]));
                }
                cmd.ExecuteNonQuery();
            }
        }

        private HashSet<string> GetColumnas(OdbcConnection conn, string schema, string table)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var sql = @"
                    SELECT COLUMN_NAME
                    FROM QSYS2.SYSCOLUMNS
                    WHERE TABLE_SCHEMA = ?
                      AND TABLE_NAME = ?";
                using (var cmd = new OdbcCommand(sql, conn))
                {
                    AddParameter(cmd, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                columnas.Add(reader.GetString(0).Trim().ToUpperInvariant());
                            }
                        }
                    }
                }
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return columnas;
        }

        private static string SafeString(string value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback ?? string.Empty;
            }
            return value.Trim();
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static OdbcType GetOdbcType(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return OdbcType.VarChar;
            }

            if (value is int || value is long || value is short)
            {
                return OdbcType.Int;
            }

            if (value is decimal || value is float || value is double)
            {
                return OdbcType.Numeric;
            }

            if (value is DateTime)
            {
                return OdbcType.DateTime;
            }

            return OdbcType.VarChar;
        }
    }
}
