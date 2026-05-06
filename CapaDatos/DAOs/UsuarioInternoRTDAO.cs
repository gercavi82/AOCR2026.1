using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.Models;
using CapaModelo;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class UsuarioInternoRTDAO
    {
        private static readonly string[] RolesInspectorCompatibles =
        {
            RolesAOCR.INSPECTOR,
            "Inspectores"
        };

        private const string SelectUsuarioInterno = @"
SELECT
    id                               AS Id,
    usuario_id                       AS UsuarioId,
    tecnico_id                       AS TecnicoId,
    codigo_usuario                   AS CodigoUsuario,
    COALESCE(identificacion,'')      AS Identificacion,
    COALESCE(nombres,'')             AS Nombres,
    COALESCE(apellidos,'')           AS Apellidos,
    COALESCE(nombre_completo,'')     AS NombreCompleto,
    COALESCE(tipo,'')                AS Tipo,
    COALESCE(estado_as400,'')        AS EstadoAs400,
    COALESCE(ciudad_codigo,'')       AS CiudadCodigo,
    COALESCE(codigo_financiero, 0)   AS CodigoFinanciero,
    COALESCE(opcar5,'')              AS Opcar5,
    COALESCE(opcaer,'')              AS Opcaer,
    COALESCE(opcoi3, 0)              AS Opcoi3,
    COALESCE(correo_institucional,'') AS CorreoInstitucional,
    COALESCE(rol_interno,'')         AS RolInterno,
    COALESCE(observaciones,'')       AS Observaciones,
    activo                           AS Activo,
    created_at                       AS CreatedAt,
    created_by                       AS CreatedBy,
    updated_at                       AS UpdatedAt,
    updated_by                       AS UpdatedBy
FROM aocr_usuario_interno_rt";

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

            var ciudad = NormalizarCodigo(registro.CiudadCodigo, 10);
            var codigoFinanciero = registro.CodigoFinanciero > 0m ? registro.CodigoFinanciero : 0m;
            var aeropuerto = NormalizarCodigo(registro.Opcar5, 10);
            var aeropuertoEspejo = NormalizarCodigo(registro.Opcaer, 10);
            var opcoi3 = registro.Opcoi3 > 0m ? registro.Opcoi3 : 0m;

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
    (usuario_id, tecnico_id, codigo_usuario, identificacion, nombres, apellidos, nombre_completo, tipo, estado_as400, ciudad_codigo, codigo_financiero, opcar5, opcaer, opcoi3, correo_institucional, rol_interno, observaciones, activo, created_at, created_by)
VALUES
    (@UsuarioId, @TecnicoId, @CodigoUsuario, @Identificacion, @Nombres, @Apellidos, @NombreCompleto, @Tipo, @EstadoAs400, @CiudadCodigo, @CodigoFinanciero, @Opcar5, @Opcaer, @Opcoi3, @CorreoInstitucional, @RolInterno, @Observaciones, TRUE, NOW(), @Actor);";

                        cn.Execute(sql, new
                        {
                            UsuarioId = usuarioId,
                            TecnicoId = registro.TecnicoId,
                            CodigoUsuario = codigo,
                            Identificacion = (registro.Identificacion ?? string.Empty).Trim(),
                            Nombres = (registro.Nombres ?? string.Empty).Trim(),
                            Apellidos = (registro.Apellidos ?? string.Empty).Trim(),
                            NombreCompleto = (registro.NombreCompleto ?? string.Empty).Trim(),
                            Tipo = (registro.Tipo ?? string.Empty).Trim(),
                            EstadoAs400 = (registro.EstadoAs400 ?? "AC").Trim(),
                            CiudadCodigo = ciudad,
                            CodigoFinanciero = codigoFinanciero,
                            Opcar5 = aeropuerto,
                            Opcaer = aeropuertoEspejo,
                            Opcoi3 = opcoi3,
                            CorreoInstitucional = NormalizarTexto(registro.CorreoInstitucional, 200),
                            RolInterno = NormalizarTexto(registro.RolInterno, 100),
                            Observaciones = NormalizarTexto(registro.Observaciones, 2000),
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
            var sql = SelectUsuarioInterno + @"
WHERE (
        UPPER(TRIM(codigo_usuario)) = UPPER(TRIM(@codigoUsuario))
        OR UPPER(TRIM(COALESCE(identificacion, ''))) = UPPER(TRIM(@codigoUsuario))
      )
  AND activo = TRUE
ORDER BY id DESC
LIMIT 1;";

            return cn.QueryFirstOrDefault<UsuarioInternoRTRegistro>(
                sql,
                new { codigoUsuario },
                tx);
        }

        public int? ObtenerUsuarioIdPorCorreo(string correo)
        {
            var correoNorm = (correo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correoNorm))
                return null;

            using (var cn = CrearConexion())
            {
                cn.Open();
                const string sql = @"
SELECT idusuario
FROM usuario
WHERE UPPER(TRIM(correo)) = UPPER(TRIM(@correo))
LIMIT 1;";
                return cn.QueryFirstOrDefault<int?>(sql, new { correo = correoNorm });
            }
        }

        public int? ObtenerUsuarioIdPorTecnicoId(int tecnicoId)
        {
            if (tecnicoId <= 0)
                return null;

            using (var cn = CrearConexion())
            {
                cn.Open();

                if (!ExisteTabla(cn, null, "aocr_tbtecnico"))
                    return null;

                const string sql = @"
SELECT t.codigousuario
FROM aocr_tbtecnico t
WHERE t.codigotecnico = @tecnicoId
LIMIT 1;";
                return cn.QueryFirstOrDefault<int?>(sql, new { tecnicoId });
            }
        }

        public bool VincularCuentaAcceso(int idRegistro, int usuarioId, string actor, out string mensaje)
        {
            mensaje = string.Empty;

            if (idRegistro <= 0)
            {
                mensaje = "Identificador de usuario interno RT invalido.";
                return false;
            }

            if (usuarioId <= 0)
            {
                mensaje = "Identificador de usuario AOCR invalido.";
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        AsegurarEstructuraBasica(cn, tx);

                        const string sqlUsuario = @"
SELECT COUNT(1)
FROM usuario
WHERE idusuario = @usuarioId;";
                        var existeUsuario = cn.ExecuteScalar<int>(sqlUsuario, new { usuarioId }, tx) > 0;
                        if (!existeUsuario)
                        {
                            tx.Rollback();
                            mensaje = "No existe la cuenta AOCR que se intenta vincular.";
                            return false;
                        }

                        const string sqlUpdate = @"
UPDATE aocr_usuario_interno_rt
SET usuario_id = @usuarioId,
    updated_at = NOW(),
    updated_by = @actor
WHERE id = @id;";

                        var rows = cn.Execute(sqlUpdate, new
                        {
                            id = idRegistro,
                            usuarioId,
                            actor = string.IsNullOrWhiteSpace(actor) ? "sistema" : actor.Trim()
                        }, tx);

                        if (rows <= 0)
                        {
                            tx.Rollback();
                            mensaje = "No se encontro el registro interno RT para vincular la cuenta.";
                            return false;
                        }

                        tx.Commit();
                        mensaje = "Cuenta AOCR vinculada al inspector correctamente.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "Error al vincular cuenta AOCR con inspector RT: " + ex.Message;
                        return false;
                    }
                }
            }
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
    nombre_completo  VARCHAR(200) NULL,
    tipo             VARCHAR(10) NULL,
    estado_as400     VARCHAR(10) NULL,
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
    ON aocr_usuario_interno_rt (opcaer);

DO $$ BEGIN
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS nombre_completo VARCHAR(200) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS tipo VARCHAR(10) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS estado_as400 VARCHAR(10) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS tecnico_id INT NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS identificacion VARCHAR(32) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS nombres VARCHAR(120) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS apellidos VARCHAR(120) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS correo_institucional VARCHAR(200) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS rol_interno VARCHAR(100) NULL;
    ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS observaciones TEXT NULL;
EXCEPTION WHEN OTHERS THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS aocr_asignacion_rt
(
    id               SERIAL PRIMARY KEY,
    codigo_solicitud INT NOT NULL,
    rt_cedula        VARCHAR(64) NOT NULL,
    rt_nombre        VARCHAR(200) NULL,
    rt_tipo          VARCHAR(10) NULL,
    fecha_asignacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_asigna   VARCHAR(120) NOT NULL,
    observacion      TEXT NULL,
    activo           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_aocr_asignacion_rt_solicitud
    ON aocr_asignacion_rt (codigo_solicitud);
";

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

        private static string NormalizarTipoInspectorFiltro(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return string.Empty;
            }

            var tipo = tipoInspector.Trim().ToUpperInvariant();
            return tipo == "OPS" || tipo == "AIR" ? tipo : string.Empty;
        }

        public List<UsuarioInternoRTRegistro> ListarActivos()
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                var sql = SelectUsuarioInterno + @"
WHERE activo = TRUE
ORDER BY COALESCE(nombre_completo, codigo_usuario);";
                return cn.Query<UsuarioInternoRTRegistro>(sql).AsList();
            }
        }

        public List<UsuarioInternoRTRegistro> ListarInspectoresAsignables(string tipoInspector = null)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);

                var tipoNormalizado = NormalizarTipoInspectorFiltro(tipoInspector);
                var sql = SelectUsuarioInterno + @"
WHERE activo = TRUE
  AND UPPER(TRIM(COALESCE(rol_interno, ''))) LIKE 'INSPECTOR%'
  AND (@tipo = '' OR UPPER(TRIM(COALESCE(tipo, ''))) = @tipo)
ORDER BY COALESCE(nombre_completo, codigo_usuario), codigo_usuario;";

                                var inspectoresRt = cn.Query<UsuarioInternoRTRegistro>(sql, new { tipo = tipoNormalizado }).AsList();
                                var inspectoresFallback = ObtenerInspectoresDesdeUsuarios(tipoNormalizado);
                                return CombinarCatalogosInspectores(inspectoresRt, inspectoresFallback);
            }
        }

        public List<UsuarioInternoRTRegistro> ListarTodos()
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                var sql = SelectUsuarioInterno + @"
ORDER BY activo DESC, COALESCE(nombre_completo, codigo_usuario);";
                return cn.Query<UsuarioInternoRTRegistro>(sql).AsList();
            }
        }

        public UsuarioInternoRTRegistro ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                return ObtenerPorId(cn, null, id);
            }
        }

        public List<TecnicoInternoDisponible> BuscarTecnicosDisponibles(string filtro, int? excluirUsuarioInternoId = null)
        {
            var criterio = (filtro ?? string.Empty).Trim();
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);

                if (!ExisteTabla(cn, null, "aocr_tbtecnico") || !ExisteTabla(cn, null, "usuario"))
                {
                    return new List<TecnicoInternoDisponible>();
                }

                var sql = @"
SELECT
    t.codigotecnico AS CodigoTecnico,
    u.idusuario AS UsuarioId,
    COALESCE(u.codigousuario, '') AS CodigoUsuario,
    COALESCE(NULLIF(TRIM(u.codigousuario), ''), '') AS Identificacion,
    COALESCE(u.nombreusuario, '') AS Nombres,
    COALESCE(u.apellidousuario, '') AS Apellidos,
    TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, '')) AS NombreCompleto,
    COALESCE(u.correo, '') AS CorreoActual,
    COALESCE(t.especialidad, '') AS Especialidad,
    COALESCE(t.activo, FALSE) AS Activo,
    EXISTS (
        SELECT 1
        FROM aocr_usuario_interno_rt rt
        WHERE rt.tecnico_id = t.codigotecnico
          AND rt.activo = TRUE
          AND (@excluirId IS NULL OR rt.id <> @excluirId)
    ) AS YaVinculado
FROM aocr_tbtecnico t
INNER JOIN usuario u ON u.idusuario = t.codigousuario
WHERE (@criterio = ''
       OR UPPER(COALESCE(u.codigousuario, '')) LIKE UPPER(@like)
       OR UPPER(COALESCE(u.nombreusuario, '')) LIKE UPPER(@like)
       OR UPPER(COALESCE(u.apellidousuario, '')) LIKE UPPER(@like)
       OR UPPER(TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, ''))) LIKE UPPER(@like)
       OR UPPER(COALESCE(u.correo, '')) LIKE UPPER(@like)
       OR UPPER(COALESCE(t.especialidad, '')) LIKE UPPER(@like))
ORDER BY TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, ''))
LIMIT 50;";

                try
                {
                    return cn.Query<TecnicoInternoDisponible>(
                        sql,
                        new
                        {
                            criterio,
                            like = "%" + criterio + "%",
                            excluirId = excluirUsuarioInternoId
                        }).AsList();
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    return new List<TecnicoInternoDisponible>();
                }
            }
        }

        public TecnicoInternoDisponible ObtenerTecnicoDisponiblePorId(int tecnicoId)
        {
            if (tecnicoId <= 0)
            {
                return null;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);

                if (!ExisteTabla(cn, null, "aocr_tbtecnico") || !ExisteTabla(cn, null, "usuario"))
                {
                    return null;
                }

                const string sql = @"
SELECT
    t.codigotecnico AS CodigoTecnico,
    u.idusuario AS UsuarioId,
    COALESCE(u.codigousuario, '') AS CodigoUsuario,
    COALESCE(NULLIF(TRIM(u.codigousuario), ''), '') AS Identificacion,
    COALESCE(u.nombreusuario, '') AS Nombres,
    COALESCE(u.apellidousuario, '') AS Apellidos,
    TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, '')) AS NombreCompleto,
    COALESCE(u.correo, '') AS CorreoActual,
    COALESCE(t.especialidad, '') AS Especialidad,
    COALESCE(t.activo, FALSE) AS Activo,
    EXISTS (
        SELECT 1
        FROM aocr_usuario_interno_rt rt
        WHERE rt.tecnico_id = t.codigotecnico
          AND rt.activo = TRUE
    ) AS YaVinculado
FROM aocr_tbtecnico t
INNER JOIN usuario u ON u.idusuario = t.codigousuario
WHERE t.codigotecnico = @tecnicoId
LIMIT 1;";

                try
                {
                    return cn.QueryFirstOrDefault<TecnicoInternoDisponible>(sql, new { tecnicoId });
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    return null;
                }
            }
        }

        public bool ActualizarRegistro(UsuarioInternoRTRegistro registro, string actor, out string mensaje)
        {
            mensaje = string.Empty;

            if (registro == null || registro.Id <= 0)
            {
                mensaje = "No se recibio informacion valida para actualizar el usuario interno RT.";
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        AsegurarEstructuraBasica(cn, tx);

                        var actual = ObtenerPorId(cn, tx, registro.Id);
                        if (actual == null)
                        {
                            tx.Rollback();
                            mensaje = "No se encontro el usuario interno RT.";
                            return false;
                        }

                        var codigo = NormalizarCodigo(string.IsNullOrWhiteSpace(registro.CodigoUsuario) ? actual.CodigoUsuario : registro.CodigoUsuario);
                        var identificacion = NormalizarTexto(
                            string.IsNullOrWhiteSpace(registro.Identificacion) ? actual.Identificacion : registro.Identificacion,
                            32);
                        var nombres = NormalizarTexto(
                            string.IsNullOrWhiteSpace(registro.Nombres) ? actual.Nombres : registro.Nombres,
                            120);
                        var apellidos = NormalizarTexto(
                            string.IsNullOrWhiteSpace(registro.Apellidos) ? actual.Apellidos : registro.Apellidos,
                            120);
                        var nombreCompleto = NormalizarTexto(
                            string.IsNullOrWhiteSpace(registro.NombreCompleto) ? actual.NombreCompleto : registro.NombreCompleto,
                            200);

                        const string sql = @"
UPDATE aocr_usuario_interno_rt
SET usuario_id = @UsuarioId,
    tecnico_id = @TecnicoId,
    codigo_usuario = @CodigoUsuario,
    identificacion = @Identificacion,
    nombres = @Nombres,
    apellidos = @Apellidos,
    nombre_completo = @NombreCompleto,
    tipo = @Tipo,
    estado_as400 = @EstadoAs400,
    ciudad_codigo = @CiudadCodigo,
    codigo_financiero = @CodigoFinanciero,
    opcar5 = @Opcar5,
    opcaer = @Opcaer,
    opcoi3 = @Opcoi3,
    correo_institucional = @CorreoInstitucional,
    rol_interno = @RolInterno,
    observaciones = @Observaciones,
    activo = @Activo,
    updated_at = NOW(),
    updated_by = @Actor
WHERE id = @Id;";

                        cn.Execute(sql, new
                        {
                            Id = registro.Id,
                            UsuarioId = registro.UsuarioId ?? actual.UsuarioId,
                            TecnicoId = registro.TecnicoId ?? actual.TecnicoId,
                            CodigoUsuario = codigo,
                            Identificacion = identificacion,
                            Nombres = nombres,
                            Apellidos = apellidos,
                            NombreCompleto = nombreCompleto,
                            Tipo = NormalizarTexto(string.IsNullOrWhiteSpace(registro.Tipo) ? actual.Tipo : registro.Tipo, 10),
                            EstadoAs400 = NormalizarTexto(string.IsNullOrWhiteSpace(registro.EstadoAs400) ? actual.EstadoAs400 : registro.EstadoAs400, 10),
                            CiudadCodigo = NormalizarTexto(string.IsNullOrWhiteSpace(registro.CiudadCodigo) ? actual.CiudadCodigo : registro.CiudadCodigo, 10),
                            CodigoFinanciero = registro.CodigoFinanciero > 0m ? registro.CodigoFinanciero : actual.CodigoFinanciero,
                            Opcar5 = NormalizarTexto(string.IsNullOrWhiteSpace(registro.Opcar5) ? actual.Opcar5 : registro.Opcar5, 10),
                            Opcaer = NormalizarTexto(string.IsNullOrWhiteSpace(registro.Opcaer) ? actual.Opcaer : registro.Opcaer, 10),
                            Opcoi3 = registro.Opcoi3 > 0m ? registro.Opcoi3 : actual.Opcoi3,
                            CorreoInstitucional = NormalizarTexto(string.IsNullOrWhiteSpace(registro.CorreoInstitucional) ? actual.CorreoInstitucional : registro.CorreoInstitucional, 200),
                            RolInterno = NormalizarTexto(string.IsNullOrWhiteSpace(registro.RolInterno) ? actual.RolInterno : registro.RolInterno, 100),
                            Observaciones = NormalizarTexto(registro.Observaciones ?? actual.Observaciones, 2000),
                            Activo = registro.Activo,
                            Actor = string.IsNullOrWhiteSpace(actor) ? "sistema" : actor.Trim()
                        }, tx);

                        tx.Commit();
                        mensaje = "Usuario interno RT actualizado correctamente.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "Error al actualizar usuario interno RT: " + ex.Message;
                        return false;
                    }
                }
            }
        }

        public bool CambiarEstado(int id, bool activo, string actor, out string mensaje)
        {
            mensaje = string.Empty;
            if (id <= 0)
            {
                mensaje = "Identificador invalido.";
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                var sql = @"
UPDATE aocr_usuario_interno_rt
SET activo = @activo,
    updated_at = NOW(),
    updated_by = @actor
WHERE id = @id;";

                var rows = cn.Execute(sql, new
                {
                    id,
                    activo,
                    actor = string.IsNullOrWhiteSpace(actor) ? "sistema" : actor.Trim()
                });

                if (rows <= 0)
                {
                    mensaje = "No se encontro el usuario interno RT.";
                    return false;
                }

                mensaje = activo
                    ? "Usuario interno RT activado correctamente."
                    : "Usuario interno RT inactivado correctamente.";
                return true;
            }
        }

        public UsuarioInternoRTRegistro ResolverDestinatarioAsignacionPorCodigoUsuario(string codigoUsuario)
        {
            return ObtenerActivoPorCodigoUsuario(codigoUsuario)
                ?? ObtenerInspectorFallbackPorCodigoUsuario(codigoUsuario, null);
        }

        public UsuarioInternoRTRegistro ObtenerInspectorAsignableActivo(string codigoUsuario, string tipoInspector = null)
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

                var tipoNormalizado = NormalizarTipoInspectorFiltro(tipoInspector);
                var sql = SelectUsuarioInterno + @"
WHERE activo = TRUE
  AND UPPER(TRIM(COALESCE(rol_interno, ''))) LIKE 'INSPECTOR%'
  AND (
        UPPER(TRIM(COALESCE(codigo_usuario, ''))) = UPPER(TRIM(@codigoUsuario))
        OR UPPER(TRIM(COALESCE(identificacion, ''))) = UPPER(TRIM(@codigoUsuario))
      )
  AND (@tipo = '' OR UPPER(TRIM(COALESCE(tipo, ''))) = @tipo)
ORDER BY id DESC
LIMIT 1;";

                return cn.QueryFirstOrDefault<UsuarioInternoRTRegistro>(sql, new
                {
                    codigoUsuario = codigo,
                    tipo = tipoNormalizado
                }) ?? ObtenerInspectorFallbackPorCodigoUsuario(codigo, tipoNormalizado);
            }
        }

        public UsuarioInternoRTRegistro ObtenerInspectorActivoPorTecnicoIdOUsuarioId(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);

                var sql = SelectUsuarioInterno + @"
WHERE activo = TRUE
  AND (tecnico_id = @id OR usuario_id = @id)
ORDER BY id DESC
LIMIT 1;";

                try
                {
                    return cn.QueryFirstOrDefault<UsuarioInternoRTRegistro>(sql, new { id });
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    return null;
                }
            }
        }

        public string ObtenerCorreoInstitucionalPorTecnicoId(int tecnicoId)
        {
            if (tecnicoId <= 0)
            {
                return string.Empty;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                const string sql = @"
SELECT COALESCE(correo_institucional, '')
FROM aocr_usuario_interno_rt
WHERE tecnico_id = @tecnicoId
  AND activo = TRUE
ORDER BY id DESC
LIMIT 1;";

                return cn.QueryFirstOrDefault<string>(sql, new { tecnicoId }) ?? string.Empty;
            }
        }

        public string ObtenerCorreoInstitucionalPorCodigoUsuario(string codigoUsuario)
        {
            var registro = ResolverDestinatarioAsignacionPorCodigoUsuario(codigoUsuario);
            return registro != null ? (registro.CorreoInstitucional ?? string.Empty).Trim() : string.Empty;
        }

        private static List<UsuarioInternoRTRegistro> ObtenerInspectoresDesdeUsuarios(string tipoInspector)
        {
            var usuarios = new List<Usuario>();

            foreach (var rol in RolesInspectorCompatibles)
            {
                try
                {
                    var usuariosRol = UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>();
                    usuarios.AddRange(usuariosRol.Where(u => u != null && u.Activo));
                }
                catch
                {
                    // Mantener el flujo principal basado en RT aunque el fallback de usuarios falle.
                }
            }

            return usuarios
                .GroupBy(ConstruirClaveUsuarioFallback, StringComparer.OrdinalIgnoreCase)
                .Select(g => MapearUsuarioAInspectorFallback(g.First(), tipoInspector))
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.UsuarioLogin))
                .OrderBy(x => x.NombreVisual ?? string.Empty)
                .ThenBy(x => x.UsuarioLogin ?? string.Empty)
                .ToList();
        }

        private static UsuarioInternoRTRegistro ObtenerInspectorFallbackPorCodigoUsuario(string codigoUsuario, string tipoInspector)
        {
            var codigoNormalizado = NormalizarCodigo(codigoUsuario);
            if (string.IsNullOrWhiteSpace(codigoNormalizado))
            {
                return null;
            }

            return ObtenerInspectoresDesdeUsuarios(tipoInspector)
                .FirstOrDefault(u =>
                    string.Equals(NormalizarCodigo(u.CodigoUsuario), codigoNormalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizarCodigo(u.Identificacion), codigoNormalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizarCodigo(u.CorreoInstitucional), codigoNormalizado, StringComparison.OrdinalIgnoreCase));
        }

        private static List<UsuarioInternoRTRegistro> CombinarCatalogosInspectores(
            IEnumerable<UsuarioInternoRTRegistro> inspectoresRt,
            IEnumerable<UsuarioInternoRTRegistro> inspectoresFallback)
        {
            var catalogo = new Dictionary<string, UsuarioInternoRTRegistro>(StringComparer.OrdinalIgnoreCase);

            foreach (var inspector in inspectoresRt ?? Enumerable.Empty<UsuarioInternoRTRegistro>())
            {
                RegistrarInspectorCatalogo(catalogo, inspector);
            }

            foreach (var inspector in inspectoresFallback ?? Enumerable.Empty<UsuarioInternoRTRegistro>())
            {
                RegistrarInspectorCatalogo(catalogo, inspector, false);
            }

            return catalogo.Values
                .OrderBy(x => x.NombreVisual ?? string.Empty)
                .ThenBy(x => x.UsuarioLogin ?? string.Empty)
                .ToList();
        }

        private static void RegistrarInspectorCatalogo(
            IDictionary<string, UsuarioInternoRTRegistro> catalogo,
            UsuarioInternoRTRegistro inspector,
            bool sobrescribir = true)
        {
            if (catalogo == null || inspector == null)
            {
                return;
            }

            var clave = ConstruirClaveInspector(inspector);
            if (string.IsNullOrWhiteSpace(clave))
            {
                return;
            }

            if (!catalogo.ContainsKey(clave) || sobrescribir)
            {
                catalogo[clave] = inspector;
            }
        }

        private static string ConstruirClaveInspector(UsuarioInternoRTRegistro inspector)
        {
            if (inspector == null)
            {
                return string.Empty;
            }

            var codigo = NormalizarCodigo(inspector.UsuarioLogin);
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return "COD:" + codigo;
            }

            if (inspector.UsuarioId.HasValue && inspector.UsuarioId.Value > 0)
            {
                return "USR:" + inspector.UsuarioId.Value;
            }

            if (inspector.TecnicoId.HasValue && inspector.TecnicoId.Value > 0)
            {
                return "TEC:" + inspector.TecnicoId.Value;
            }

            return string.Empty;
        }

        private static string ConstruirClaveUsuarioFallback(Usuario usuario)
        {
            if (usuario == null)
            {
                return string.Empty;
            }

            var codigo = NormalizarCodigo(usuario.CodigoUsuario);
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo;
            }

            return usuario.Id > 0 ? "USR:" + usuario.Id : string.Empty;
        }

        private static UsuarioInternoRTRegistro MapearUsuarioAInspectorFallback(Usuario usuario, string tipoInspector)
        {
            if (usuario == null || !usuario.Activo)
            {
                return null;
            }

            var tipoNormalizado = NormalizarTipoInspectorFiltro(tipoInspector);
            return new UsuarioInternoRTRegistro
            {
                UsuarioId = usuario.Id > 0 ? (int?)usuario.Id : null,
                CodigoUsuario = (usuario.CodigoUsuario ?? string.Empty).Trim(),
                Identificacion = (usuario.CodigoUsuario ?? string.Empty).Trim(),
                Nombres = (usuario.NombreUsuario ?? string.Empty).Trim(),
                Apellidos = (usuario.ApellidoUsuario ?? string.Empty).Trim(),
                NombreCompleto = ConstruirNombreUsuarioFallback(usuario),
                Tipo = tipoNormalizado,
                CorreoInstitucional = (usuario.Email ?? string.Empty).Trim(),
                RolInterno = RolesAOCR.INSPECTOR,
                EstadoAs400 = "AC",
                Activo = true,
                Observaciones = "Catálogo fallback desde usuario / rol"
            };
        }

        private static string ConstruirNombreUsuarioFallback(Usuario usuario)
        {
            if (usuario == null)
            {
                return string.Empty;
            }

            var nombre = string.Join(" ", new[]
            {
                usuario.NombreCompleto,
                usuario.NombreUsuario,
                usuario.ApellidoUsuario
            }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

            return string.IsNullOrWhiteSpace(nombre)
                ? (usuario.CodigoUsuario ?? string.Empty).Trim()
                : nombre;
        }

        public bool ExisteCorreoInstitucional(string correo, int? excluirId = null)
        {
            var correoNormalizado = NormalizarTexto(correo, 200);
            if (string.IsNullOrWhiteSpace(correoNormalizado))
            {
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                const string sql = @"
SELECT COUNT(1)
FROM aocr_usuario_interno_rt
WHERE activo = TRUE
  AND LOWER(TRIM(COALESCE(correo_institucional, ''))) = LOWER(TRIM(@correo))
  AND (@excluirId IS NULL OR id <> @excluirId);";

                return cn.ExecuteScalar<int>(sql, new
                {
                    correo = correoNormalizado,
                    excluirId
                }) > 0;
            }
        }

        public bool ExisteTecnicoActivo(int tecnicoId, int? excluirId = null)
        {
            if (tecnicoId <= 0)
            {
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                const string sql = @"
SELECT COUNT(1)
FROM aocr_usuario_interno_rt
WHERE tecnico_id = @tecnicoId
  AND activo = TRUE
  AND (@excluirId IS NULL OR id <> @excluirId);";

                return cn.ExecuteScalar<int>(sql, new
                {
                    tecnicoId,
                    excluirId
                }) > 0;
            }
        }

        public bool RegistrarAsignacion(int codigoSolicitud, string rtCedula, string rtNombre, string rtTipo, string usuarioAsigna, string observacion, out string mensaje)
        {
            mensaje = string.Empty;
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(rtCedula))
            {
                mensaje = "Datos de asignación incompletos.";
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        cn.Execute(
                            "UPDATE aocr_asignacion_rt SET activo = FALSE WHERE codigo_solicitud = @sol AND activo = TRUE;",
                            new { sol = codigoSolicitud }, tx);

                        cn.Execute(@"
INSERT INTO aocr_asignacion_rt (codigo_solicitud, rt_cedula, rt_nombre, rt_tipo, usuario_asigna, observacion, activo, created_at)
VALUES (@sol, @ced, @nom, @tip, @usr, @obs, TRUE, NOW());",
                            new
                            {
                                sol = codigoSolicitud,
                                ced = (rtCedula ?? "").Trim(),
                                nom = (rtNombre ?? "").Trim(),
                                tip = (rtTipo ?? "").Trim(),
                                usr = (usuarioAsigna ?? "sistema").Trim(),
                                obs = (observacion ?? "").Trim()
                            }, tx);

                        tx.Commit();
                        mensaje = "Asignación registrada.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "Error al registrar asignación: " + ex.Message;
                        return false;
                    }
                }
            }
        }

        public List<AsignacionRTRegistro> ObtenerHistorialAsignacion(int codigoSolicitud)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                const string sql = @"
SELECT
    id               AS Id,
    codigo_solicitud AS CodigoSolicitud,
    rt_cedula        AS RtCedula,
    rt_nombre        AS RtNombre,
    rt_tipo          AS RtTipo,
    fecha_asignacion AS FechaAsignacion,
    usuario_asigna   AS UsuarioAsigna,
    observacion      AS Observacion,
    activo           AS Activo,
    created_at       AS CreatedAt
FROM aocr_asignacion_rt
WHERE codigo_solicitud = @sol
ORDER BY created_at DESC;";
                return cn.Query<AsignacionRTRegistro>(sql, new { sol = codigoSolicitud }).AsList();
            }
        }

        public AsignacionRTRegistro ObtenerAsignacionActiva(int codigoSolicitud)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                AsegurarEstructuraBasica(cn, null);
                const string sql = @"
SELECT
    id               AS Id,
    codigo_solicitud AS CodigoSolicitud,
    rt_cedula        AS RtCedula,
    rt_nombre        AS RtNombre,
    rt_tipo          AS RtTipo,
    fecha_asignacion AS FechaAsignacion,
    usuario_asigna   AS UsuarioAsigna,
    observacion      AS Observacion,
    activo           AS Activo,
    created_at       AS CreatedAt
FROM aocr_asignacion_rt
WHERE codigo_solicitud = @sol AND activo = TRUE
ORDER BY created_at DESC
LIMIT 1;";
                return cn.QueryFirstOrDefault<AsignacionRTRegistro>(sql, new { sol = codigoSolicitud });
            }
        }

        private static UsuarioInternoRTRegistro ObtenerPorId(NpgsqlConnection cn, IDbTransaction tx, int id)
        {
            var sql = SelectUsuarioInterno + @"
WHERE id = @id
LIMIT 1;";

            return cn.QueryFirstOrDefault<UsuarioInternoRTRegistro>(sql, new { id }, tx);
        }

        private static bool ExisteTabla(IDbConnection cn, IDbTransaction tx, string tableName)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @tableName;";

            return cn.ExecuteScalar<int>(sql, new { tableName }, tx) > 0;
        }

        private static string NormalizarTexto(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var texto = value.Trim();
            if (texto.Length > maxLength)
            {
                texto = texto.Substring(0, maxLength);
            }

            return texto;
        }

    }
}
