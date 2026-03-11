using System;
using System.Collections.Generic;
using System.Data;
using CapaDatos.Models;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class UsuarioInternoRTDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        public UsuarioInternoRTRegistro ObtenerActivoPorCodigoUsuario(string codigoUsuario)
        {
            var codigo = NormalizarCodigo(codigoUsuario);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                return ObtenerActivoPorCodigoUsuario(cn, null, codigo);
            }
        }

        public int? ObtenerUsuarioIdPorCodigoUsuario(string codigoUsuario)
        {
            var codigo = NormalizarCodigo(codigoUsuario);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                return ObtenerUsuarioIdPorCodigoUsuario(cn, null, codigo);
            }
        }

        public bool GuardarRegistro(UsuarioInternoRTRegistro registro, string actor, out string mensaje)
        {
            mensaje = string.Empty;

            if (registro == null)
            {
                mensaje = "No se recibio informacion para registrar el usuario interno RT.";
                return false;
            }

            var codigo = NormalizarCodigo(registro.CodigoUsuario);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                mensaje = "Debe indicar un codigo de usuario valido.";
                return false;
            }

            if (registro.CodigoFinanciero <= 0m)
            {
                mensaje = "El codigo financiero (usuoid) es obligatorio.";
                return false;
            }

            var aeropuerto = NormalizarCodigo(registro.Opcar5, 10);
            if (string.IsNullOrWhiteSpace(aeropuerto))
            {
                mensaje = "Debe seleccionar un aeropuerto.";
                return false;
            }

            var ciudad = NormalizarCodigo(registro.CiudadCodigo, 10);
            if (string.IsNullOrWhiteSpace(ciudad))
            {
                mensaje = "No se pudo resolver la ciudad del usuario desde AS400.";
                return false;
            }

            var actorRegistro = string.IsNullOrWhiteSpace(actor) ? "sistema" : actor.Trim();

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        AsegurarEstructuraBasica(cn, tx);

                        var existente = ObtenerActivoPorCodigoUsuario(cn, tx, codigo);
                        if (existente != null)
                        {
                            mensaje = "El usuario ya tiene un registro interno RT activo.";
                            tx.Rollback();
                            return false;
                        }

                        var usuarioId = registro.UsuarioId;
                        if (!usuarioId.HasValue || usuarioId.Value <= 0)
                        {
                            usuarioId = ObtenerUsuarioIdPorCodigoUsuario(cn, tx, codigo);
                        }

                        const string sql = @"
INSERT INTO aocr_usuario_interno_rt
    (usuario_id, codigo_usuario, ciudad_codigo, codigo_financiero, opcar5, opcaer, opcoi3, activo, created_at, created_by)
VALUES
    (@UsuarioId, @CodigoUsuario, @CiudadCodigo, @CodigoFinanciero, @Opcar5, @Opcaer, @Opcoi3, TRUE, NOW(), @Actor);";

                        cn.Execute(sql, new
                        {
                            UsuarioId = usuarioId,
                            CodigoUsuario = codigo,
                            CiudadCodigo = ciudad,
                            CodigoFinanciero = registro.CodigoFinanciero,
                            Opcar5 = aeropuerto,
                            Opcaer = aeropuerto,
                            Opcoi3 = registro.CodigoFinanciero,
                            Actor = actorRegistro
                        }, tx);

                        tx.Commit();
                        mensaje = "Usuario interno RT registrado correctamente.";
                        return true;
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23505")
                    {
                        tx.Rollback();
                        mensaje = "El usuario ya tiene un registro interno RT activo.";
                        return false;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "Error al guardar usuario interno RT: " + ex.Message;
                        return false;
                    }
                }
            }
        }

        private static UsuarioInternoRTRegistro ObtenerActivoPorCodigoUsuario(
            NpgsqlConnection cn,
            IDbTransaction tx,
            string codigoUsuario)
        {
            const string sql = @"
SELECT
    id                 AS Id,
    usuario_id         AS UsuarioId,
    codigo_usuario     AS CodigoUsuario,
    ciudad_codigo      AS CiudadCodigo,
    codigo_financiero  AS CodigoFinanciero,
    opcar5             AS Opcar5,
    opcaer             AS Opcaer,
    opcoi3             AS Opcoi3,
    activo             AS Activo,
    created_at         AS CreatedAt,
    created_by         AS CreatedBy,
    updated_at         AS UpdatedAt,
    updated_by         AS UpdatedBy
FROM aocr_usuario_interno_rt
WHERE UPPER(TRIM(codigo_usuario)) = UPPER(TRIM(@codigoUsuario))
  AND activo = TRUE
ORDER BY id DESC
LIMIT 1;";

            return cn.QueryFirstOrDefault<UsuarioInternoRTRegistro>(
                sql,
                new { codigoUsuario },
                tx);
        }

        private static int? ObtenerUsuarioIdPorCodigoUsuario(
            NpgsqlConnection cn,
            IDbTransaction tx,
            string codigoUsuario)
        {
            const string sql = @"
SELECT idusuario
FROM usuario
WHERE UPPER(TRIM(codigousuario)) = UPPER(TRIM(@codigoUsuario))
LIMIT 1;";

            return cn.QueryFirstOrDefault<int?>(sql, new { codigoUsuario }, tx);
        }

        private static void AsegurarEstructuraBasica(NpgsqlConnection cn, IDbTransaction tx)
        {
            const string ddl = @"
CREATE TABLE IF NOT EXISTS aocr_usuario_interno_rt
(
    id               SERIAL PRIMARY KEY,
    usuario_id       INT NULL REFERENCES usuario(idusuario) ON DELETE SET NULL,
    codigo_usuario   VARCHAR(64) NOT NULL,
    ciudad_codigo    VARCHAR(10) NOT NULL,
    codigo_financiero NUMERIC(18,0) NOT NULL,
    opcar5           VARCHAR(10) NOT NULL,
    opcaer           VARCHAR(10) NOT NULL,
    opcoi3           NUMERIC(18,0) NOT NULL,
    activo           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by       VARCHAR(120) NOT NULL DEFAULT 'sistema',
    updated_at       TIMESTAMP NULL,
    updated_by       VARCHAR(120) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_interno_rt_codigo_activo
    ON aocr_usuario_interno_rt (UPPER(TRIM(codigo_usuario)))
    WHERE activo = TRUE;

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_usuario_id
    ON aocr_usuario_interno_rt (usuario_id);

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_opcaer
    ON aocr_usuario_interno_rt (opcaer);";

            cn.Execute(ddl, transaction: tx);
        }

        private static string NormalizarCodigo(string value, int maxLength = 64)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalizado = value.Trim().ToUpperInvariant();
            if (normalizado.Length > maxLength)
            {
                normalizado = normalizado.Substring(0, maxLength);
            }

            return normalizado;
        }
    }
}
