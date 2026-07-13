using System;
using System.Configuration;
using System.Globalization;
using CapaDatos.DAOs;
using CapaNegocio.DTOs;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class PerfilFirmanteService : IPerfilFirmanteService
    {
        public PerfilFirmanteDto ObtenerPerfil(int usuarioId, string tipoDocumento)
        {
            if (usuarioId <= 0) return null;
            var tipo = NormalizarTipo(tipoDocumento);
            var rolEsperado = tipo == TiposDocumentoFirmaInstitucional.Aocr ? "Direccion" : "DIRECTOR_CERTIFICACIONES_DCAV";
            var prefijo = tipo == TiposDocumentoFirmaInstitucional.Aocr ? "AOCR.Signature.DGAC" : "AOCR.Signature.DCAV";
            var codigoRolEsperado = Entero(Leer(prefijo + ".RoleCode"));
            if (string.IsNullOrWhiteSpace(tipo) || codigoRolEsperado <= 0) return null;
            using (var cn = new NpgsqlConnection(ConexionDAO.CadenaConexion))
            using (var cmd = new NpgsqlCommand(@"
SELECT u.idusuario,TRIM(COALESCE(u.nombreusuario,'')||' '||COALESCE(u.apellidousuario,'')) nombre,
       NULLIF(TRIM(u.cargo),'') cargo,r.codigorol,r.descripcion,
       CASE WHEN UPPER(COALESCE(u.estadoactividad,'1')) NOT IN ('0','INACTIVO','BLOQUEADO','ELIMINADO')
                  AND COALESCE(r.activo,TRUE) THEN TRUE ELSE FALSE END activo
FROM public.usuario u
JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE)
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE u.idusuario=@u AND r.codigorol=@codigo_rol AND UPPER(TRIM(r.descripcion))=UPPER(@rol)
ORDER BY ur.fechaasignacion DESC NULLS LAST LIMIT 1;", cn))
            {
                cn.Open();
                cmd.Parameters.AddWithValue("@u", usuarioId);
                cmd.Parameters.AddWithValue("@rol", rolEsperado);
                cmd.Parameters.AddWithValue("@codigo_rol", codigoRolEsperado);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    var perfil = new PerfilFirmanteDto
                    {
                        UsuarioId = Convert.ToInt32(rd["idusuario"]),
                        NombreCompleto = Convert.ToString(rd["nombre"]),
                        Cargo = rd["cargo"] == DBNull.Value ? Leer(prefijo + ".Cargo") : Convert.ToString(rd["cargo"]),
                        CodigoRol = Convert.ToInt32(rd["codigorol"]),
                        Rol = Convert.ToString(rd["descripcion"]),
                        FirmaImagenId = Entero(Leer(prefijo + ".ImageId")),
                        RutaInternaFirma = Leer(prefijo + ".ImagePath"),
                        HashFirma = Leer(prefijo + ".ImageSha256"),
                        Activo = Convert.ToBoolean(rd["activo"]),
                        VigenteDesde = Fecha(Leer(prefijo + ".ValidFrom")),
                        VigenteHasta = Fecha(Leer(prefijo + ".ValidTo")),
                        AutorizadoParaDocumento = Entero(Leer(prefijo + ".AuthorizedUserId")) == usuarioId
                    };
                    var ahora = DateTime.Now;
                    perfil.Activo = perfil.Activo
                        && (!perfil.VigenteDesde.HasValue || perfil.VigenteDesde.Value <= ahora)
                        && (!perfil.VigenteHasta.HasValue || perfil.VigenteHasta.Value >= ahora);
                    return perfil;
                }
            }
        }

        private static string NormalizarTipo(string tipo)
        {
            var valor = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            if (valor == "AOCR" || valor == "RECONOCIMIENTO") return TiposDocumentoFirmaInstitucional.Aocr;
            return valor == "CONDICIONES" || valor == "CONDICIONES_LIMITACIONES" ? TiposDocumentoFirmaInstitucional.Condiciones : string.Empty;
        }

        private static string Leer(string clave)
        {
            var entorno = Environment.GetEnvironmentVariable(clave.Replace('.', '_').ToUpperInvariant());
            return !string.IsNullOrWhiteSpace(entorno) ? entorno.Trim() : (ConfigurationManager.AppSettings[clave] ?? string.Empty).Trim();
        }

        private static int Entero(string valor) { int x; return int.TryParse(valor, out x) ? x : 0; }
        private static DateTime? Fecha(string valor)
        {
            DateTime x;
            return DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out x) ? x : (DateTime?)null;
        }
    }

    public sealed class ConfiguracionPosicionFirmaService : IConfiguracionPosicionFirmaService
    {
        public ConfiguracionPosicionFirmaDto Obtener(string tipoDocumento, int versionPlantilla)
        {
            var tipo = (tipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
            if (tipo == "AOCR") tipo = TiposDocumentoFirmaInstitucional.Aocr;
            if (tipo == "CONDICIONES") tipo = TiposDocumentoFirmaInstitucional.Condiciones;
            if (tipo != TiposDocumentoFirmaInstitucional.Aocr && tipo != TiposDocumentoFirmaInstitucional.Condiciones) return null;
            var clave = "AOCR.Signature.Position." + tipo + ".V" + Math.Max(1, versionPlantilla);
            var raw = ConfigurationManager.AppSettings[clave];
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var p = raw.Split(',');
            if (p.Length < 14) return null;
            int pagina; decimal x, y, ancho, alto, margen, nombreY, cargoY, fechaY, qrX, qrY, qrTamano; bool qr;
            if (!int.TryParse(p[0], out pagina)
                || !decimal.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                || !decimal.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                || !decimal.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out ancho)
                || !decimal.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out alto)
                || !decimal.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out margen)
                || !bool.TryParse(p[6], out qr)
                || !decimal.TryParse(p[8], NumberStyles.Float, CultureInfo.InvariantCulture, out nombreY)
                || !decimal.TryParse(p[9], NumberStyles.Float, CultureInfo.InvariantCulture, out cargoY)
                || !decimal.TryParse(p[10], NumberStyles.Float, CultureInfo.InvariantCulture, out fechaY)
                || !decimal.TryParse(p[11], NumberStyles.Float, CultureInfo.InvariantCulture, out qrX)
                || !decimal.TryParse(p[12], NumberStyles.Float, CultureInfo.InvariantCulture, out qrY)
                || !decimal.TryParse(p[13], NumberStyles.Float, CultureInfo.InvariantCulture, out qrTamano)) return null;
            return new ConfiguracionPosicionFirmaDto
            {
                ConfiguracionId = Math.Max(1, versionPlantilla), TipoDocumento = tipo, VersionPlantilla = Math.Max(1, versionPlantilla),
                Pagina = pagina, XRatio = x, YRatio = y, AnchoRatio = ancho, AltoRatio = alto,
                MargenRatio = margen, MostrarQr = qr, Alineacion = p[7].Trim().ToUpperInvariant(),
                NombreYRatio=nombreY,CargoYRatio=cargoY,FechaYRatio=fechaY,QrXRatio=qrX,QrYRatio=qrY,QrTamanioRatio=qrTamano
            };
        }
    }
}
