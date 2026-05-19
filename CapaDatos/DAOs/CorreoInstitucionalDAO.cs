using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Npgsql;
using CapaDatos.Models;

namespace CapaDatos.DAOs
{
    public class CorreoInstitucionalDAO
    {
        private const string CodigoAreaInspectorAocr = "INSPECTOR_AOCR";
        private readonly string _connectionString;

        public CorreoInstitucionalDAO()
            : this(ConexionDAO.ObtenerCadenaConexion())
        {
        }

        public CorreoInstitucionalDAO(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EnsureSchema()
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                cn.Execute(@"
CREATE TABLE IF NOT EXISTS public.aocr_tbcorreo_institucional (
    codigo_correo SERIAL PRIMARY KEY,
    codigo_area VARCHAR(80) NOT NULL UNIQUE,
    nombre_area VARCHAR(150) NOT NULL,
    correo_principal VARCHAR(250) NOT NULL,
    correos_cc TEXT NULL,
    correos_bcc TEXT NULL,
    descripcion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NULL,
    created_by VARCHAR(100) NULL,
    updated_by VARCHAR(100) NULL
);

CREATE TABLE IF NOT EXISTS public.aocr_tbcorreo_institucional_historial (
    codigo_historial SERIAL PRIMARY KEY,
    codigo_correo INTEGER NOT NULL,
    codigo_area VARCHAR(80) NOT NULL,
    correo_anterior VARCHAR(250) NULL,
    correo_nuevo VARCHAR(250) NULL,
    cc_anterior TEXT NULL,
    cc_nuevo TEXT NULL,
    bcc_anterior TEXT NULL,
    bcc_nuevo TEXT NULL,
    usuario_modificacion VARCHAR(100) NULL,
    fecha_modificacion TIMESTAMP NOT NULL DEFAULT NOW(),
    accion VARCHAR(50) NOT NULL
);

INSERT INTO public.aocr_tbcorreo_institucional
(codigo_area, nombre_area, correo_principal, descripcion, activo, created_at, created_by)
VALUES
('COORDINADOR_AOCR', 'Coordinador AOCR', 'coordinador.aocr@aviacioncivil.gob.ec', 'Correo institucional para notificaciones de asignación de inspector.', TRUE, NOW(), 'SYSTEM'),
('FINANCIERO_AOCR', 'Financiero AOCR', 'financiero.aocr@aviacioncivil.gob.ec', 'Correo institucional para notificaciones financieras.', TRUE, NOW(), 'SYSTEM'),
('DIRDAC', 'DIRDAC', 'dirdac@aviacioncivil.gob.ec', 'Correo institucional para decisiones DIRDAC.', TRUE, NOW(), 'SYSTEM'),
('DIRECCION_JEFATURA', 'Dirección / Jefatura', 'direccion.jefatura@aviacioncivil.gob.ec', 'Correo institucional para aprobación institucional.', TRUE, NOW(), 'SYSTEM'),
('SOPORTE_AOCR', 'Soporte AOCR', 'soporte.aocr@aviacioncivil.gob.ec', 'Correo institucional de soporte del sistema AOCR.', TRUE, NOW(), 'SYSTEM'),
('NOTIFICACIONES_AOCR', 'Notificaciones AOCR', 'notificaciones.aocr@aviacioncivil.gob.ec', 'Correo general de notificaciones del sistema AOCR.', TRUE, NOW(), 'SYSTEM')
ON CONFLICT (codigo_area) DO NOTHING;");
            }
        }

        public List<CorreoInstitucionalModel> ListarCorreosInstitucionales()
        {
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.Query<CorreoInstitucionalModel>(
                    SelectBase() + " WHERE UPPER(codigo_area) <> UPPER(@codigoAreaInspectorAocr) ORDER BY codigo_area;",
                    new { codigoAreaInspectorAocr = CodigoAreaInspectorAocr }).ToList();
            }
        }

        public CorreoInstitucionalModel ObtenerPorId(int codigoCorreo)
        {
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.QueryFirstOrDefault<CorreoInstitucionalModel>(
                    SelectBase() + " WHERE codigo_correo = @codigoCorreo;",
                    new { codigoCorreo });
            }
        }

        public CorreoInstitucionalModel ObtenerPorCodigoArea(string codigoArea)
        {
            if (string.IsNullOrWhiteSpace(codigoArea)) return null;
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.QueryFirstOrDefault<CorreoInstitucionalModel>(
                    SelectBase() + " WHERE UPPER(codigo_area) = UPPER(@codigoArea);",
                    new { codigoArea });
            }
        }

        public string ObtenerCorreoPrincipal(string codigoArea)
        {
            var model = ObtenerPorCodigoArea(codigoArea);
            return model != null && model.Activo ? model.CorreoPrincipal : null;
        }

        public CorreoInstitucionalModel ObtenerDestinatarios(string codigoArea)
        {
            var model = ObtenerPorCodigoArea(codigoArea);
            return model != null && model.Activo ? model : null;
        }

        public bool ExisteCodigoArea(string codigoArea, int? excluirCodigoCorreo = null)
        {
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.ExecuteScalar<int>(@"
SELECT COUNT(1)
FROM public.aocr_tbcorreo_institucional
WHERE UPPER(codigo_area) = UPPER(@codigoArea)
  AND (@excluirCodigoCorreo IS NULL OR codigo_correo <> @excluirCodigoCorreo);",
                    new { codigoArea, excluirCodigoCorreo }) > 0;
            }
        }

        public int Crear(CorreoInstitucionalModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var id = cn.ExecuteScalar<int>(@"
INSERT INTO public.aocr_tbcorreo_institucional
(codigo_area, nombre_area, correo_principal, correos_cc, correos_bcc, descripcion, activo, created_at, created_by)
VALUES
(@CodigoArea, @NombreArea, @CorreoPrincipal, @CorreosCc, @CorreosBcc, @Descripcion, @Activo, NOW(), @CreatedBy)
RETURNING codigo_correo;", model, tx);

                    RegistrarHistorial(cn, tx, id, model.CodigoArea, null, model.CorreoPrincipal, null, model.CorreosCc, null, model.CorreosBcc, model.CreatedBy, "CREAR");
                    tx.Commit();
                    return id;
                }
            }
        }

        public bool Actualizar(CorreoInstitucionalModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var anterior = ObtenerPorIdInterno(cn, tx, model.CodigoCorreo);
                    if (anterior == null) return false;

                    var rows = cn.Execute(@"
UPDATE public.aocr_tbcorreo_institucional
SET nombre_area = @NombreArea,
    correo_principal = @CorreoPrincipal,
    correos_cc = @CorreosCc,
    correos_bcc = @CorreosBcc,
    descripcion = @Descripcion,
    activo = @Activo,
    updated_at = NOW(),
    updated_by = @UpdatedBy
WHERE codigo_correo = @CodigoCorreo;", model, tx);

                    if (rows > 0)
                    {
                        RegistrarHistorial(cn, tx, model.CodigoCorreo, anterior.CodigoArea, anterior.CorreoPrincipal, model.CorreoPrincipal, anterior.CorreosCc, model.CorreosCc, anterior.CorreosBcc, model.CorreosBcc, model.UpdatedBy, "ACTUALIZAR");
                    }

                    tx.Commit();
                    return rows > 0;
                }
            }
        }

        public bool CambiarEstado(int codigoCorreo, bool activo, string usuario)
        {
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var anterior = ObtenerPorIdInterno(cn, tx, codigoCorreo);
                    if (anterior == null) return false;

                    var rows = cn.Execute(@"
UPDATE public.aocr_tbcorreo_institucional
SET activo = @activo,
    updated_at = NOW(),
    updated_by = @usuario
WHERE codigo_correo = @codigoCorreo;", new { codigoCorreo, activo, usuario }, tx);

                    if (rows > 0)
                    {
                        RegistrarHistorial(cn, tx, codigoCorreo, anterior.CodigoArea, anterior.CorreoPrincipal, anterior.CorreoPrincipal, anterior.CorreosCc, anterior.CorreosCc, anterior.CorreosBcc, anterior.CorreosBcc, usuario, activo ? "ACTIVAR" : "INACTIVAR");
                    }

                    tx.Commit();
                    return rows > 0;
                }
            }
        }

        public List<CorreoInstitucionalHistorialModel> ListarHistorial(int codigoCorreo)
        {
            EnsureSchema();
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.Query<CorreoInstitucionalHistorialModel>(@"
SELECT codigo_historial AS CodigoHistorial,
       codigo_correo AS CodigoCorreo,
       codigo_area AS CodigoArea,
       correo_anterior AS CorreoAnterior,
       correo_nuevo AS CorreoNuevo,
       cc_anterior AS CcAnterior,
       cc_nuevo AS CcNuevo,
       bcc_anterior AS BccAnterior,
       bcc_nuevo AS BccNuevo,
       usuario_modificacion AS UsuarioModificacion,
       fecha_modificacion AS FechaModificacion,
       accion AS Accion
FROM public.aocr_tbcorreo_institucional_historial
WHERE codigo_correo = @codigoCorreo
ORDER BY fecha_modificacion DESC;", new { codigoCorreo }).ToList();
            }
        }

        private static string SelectBase()
        {
            return @"
SELECT codigo_correo AS CodigoCorreo,
       codigo_area AS CodigoArea,
       nombre_area AS NombreArea,
       correo_principal AS CorreoPrincipal,
       correos_cc AS CorreosCc,
       correos_bcc AS CorreosBcc,
       descripcion AS Descripcion,
       activo AS Activo,
       created_at AS CreatedAt,
       updated_at AS UpdatedAt,
       created_by AS CreatedBy,
       updated_by AS UpdatedBy
FROM public.aocr_tbcorreo_institucional";
        }

        private static CorreoInstitucionalModel ObtenerPorIdInterno(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoCorreo)
        {
            return cn.QueryFirstOrDefault<CorreoInstitucionalModel>(
                SelectBase() + " WHERE codigo_correo = @codigoCorreo;",
                new { codigoCorreo }, tx);
        }

        private static void RegistrarHistorial(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int codigoCorreo,
            string codigoArea,
            string correoAnterior,
            string correoNuevo,
            string ccAnterior,
            string ccNuevo,
            string bccAnterior,
            string bccNuevo,
            string usuario,
            string accion)
        {
            cn.Execute(@"
INSERT INTO public.aocr_tbcorreo_institucional_historial
(codigo_correo, codigo_area, correo_anterior, correo_nuevo, cc_anterior, cc_nuevo, bcc_anterior, bcc_nuevo, usuario_modificacion, fecha_modificacion, accion)
VALUES
(@codigoCorreo, @codigoArea, @correoAnterior, @correoNuevo, @ccAnterior, @ccNuevo, @bccAnterior, @bccNuevo, @usuario, NOW(), @accion);",
                new { codigoCorreo, codigoArea, correoAnterior, correoNuevo, ccAnterior, ccNuevo, bccAnterior, bccNuevo, usuario, accion }, tx);
        }
    }
}
