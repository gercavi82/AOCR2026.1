using System;
using System.Configuration;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class DiagnosticoRolesIntegrationTests
    {
        [TestMethod]
        [TestCategory("Integration")]
        public void DiagnosticarRolesYUsuariosEnBD()
        {
            var item = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (item == null || string.IsNullOrWhiteSpace(item.ConnectionString))
            {
                Assert.Inconclusive("AOCRConnection no está configurada.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNÓSTICO EN VIVO DE BASE DE DATOS AOCR ===");

            using (var cn = new NpgsqlConnection(item.ConnectionString))
            {
                cn.Open();
                // 0. Ejecutar migración SQL de segregación
                var sqlMigracion = File.ReadAllText(@"c:\proyectos\AOCR\scripts\sql\20260903_roles_segregacion_dircav_dirdac.sql");
                using (var cmd = new NpgsqlCommand(sqlMigracion, cn))
                {
                    cmd.ExecuteNonQuery();
                    sb.AppendLine("MIGRACIÓN SQL 20260903 EJECUTADA CORRECTAMENTE.");
                }

                // 1. Tabla ROL
                sb.AppendLine("\n--- 1. COLUMNAS Y CONTENIDO DE TABLA ROL ---");
                using (var cmd = new NpgsqlCommand("SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='rol';", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"col: {r[0]} ({r[1]}) null={r[2]}");
                    }
                }
                using (var cmd = new NpgsqlCommand("SELECT * FROM rol ORDER BY codigorol;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    for (int i = 0; i < r.FieldCount; i++) sb.Append(r.GetName(i) + " | ");
                    sb.AppendLine();
                    while (r.Read())
                    {
                        for (int i = 0; i < r.FieldCount; i++) sb.Append(r[i] + " | ");
                        sb.AppendLine();
                    }
                }

                // 2. Usuarios con roles (usuario y usuario_rol)
                sb.AppendLine("\n--- 2. USUARIOS CON ROLES ASIGNADOS ---");
                using (var cmd = new NpgsqlCommand(@"
SELECT u.idusuario, u.codigousuario, u.nombreusuario, u.apellidousuario, u.correo, u.rol AS rol_columna,
       r.codigorol, r.descripcion AS rol_desc, ur.activo AS ur_activo
FROM usuario u
LEFT JOIN usuario_rol ur ON ur.codigousuario = u.codigousuario
LEFT JOIN rol r ON r.codigorol = ur.codigorol
ORDER BY r.codigorol, u.idusuario;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"id: {r[0]} | user: {r[1]} | nombre: {r[2]} {r[3]} | email: {r[4]} | u.rol: {r[5]} | codigorol: {r[6]} | desc: {r[7]} | ur_activo: {r[8]}");
                    }
                }

                // 3. Columnas de seguridad_permiso y seguridad_rol_permiso
                sb.AppendLine("\n--- 3. COLUMNAS DE SEGURIDAD_PERMISO ---");
                using (var cmd = new NpgsqlCommand("SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='public' AND table_name='seguridad_permiso';", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"col: {r[0]} ({r[1]})");
                    }
                }

                sb.AppendLine("\n--- 4. COLUMNAS DE SEGURIDAD_ROL_PERMISO ---");
                using (var cmd = new NpgsqlCommand("SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='public' AND table_name='seguridad_rol_permiso';", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"col: {r[0]} ({r[1]})");
                    }
                }

                // 5. Todos los permisos y asignaciones a roles
                sb.AppendLine("\n--- 5. ASIGNACIONES EN SEGURIDAD_ROL_PERMISO ---");
                using (var cmd = new NpgsqlCommand(@"
SELECT rp.codigorol, r.descripcion, p.codigo, p.nombre, rp.activo
FROM seguridad_rol_permiso rp
INNER JOIN rol r ON r.codigorol = rp.codigorol
INNER JOIN seguridad_permiso p ON p.id_permiso = rp.id_permiso
ORDER BY rp.codigorol, p.codigo;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"codigorol: {r[0]} ({r[1]}) | permiso: {r[2]} ({r[3]}) | activo: {r[4]}");
                    }
                }
                // 6. Extracción completa de tablas AOCR y su estructura
                sb.AppendLine("\n--- 6. TABLAS AOCR Y DE SISTEMA ---");
                using (var cmd = new NpgsqlCommand(@"
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    ORDER BY table_name;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"TABLA: {r[0]}");
                    }
                }

                // 7. Columnas de tablas clave
                sb.AppendLine("\n--- 7. COLUMNAS DE TABLAS OPERATIVAS ---");
                var tablasOperativas = new[] { 
                    "aocr_tbsolicitud", "aocr_tbsolicitud_estacion", "aocr_tbinspeccion", "aocr_tbinforme_inspeccion", 
                    "aocr_tbfirma_documento", "aocr_or_orden", "aocr_tb_factura_pago", "email_queue", "auditoria",
                    "historial_estado_solicitud", "usuario", "rol", "usuario_rol", "seguridad_permiso", "seguridad_rol_permiso"
                };

                foreach (var tbl in tablasOperativas)
                {
                    sb.AppendLine($"\n>>> TABLA: {tbl}");
                    using (var cmd = new NpgsqlCommand($@"
                        SELECT column_name, data_type, is_nullable, column_default
                        FROM information_schema.columns 
                        WHERE table_schema = 'public' AND table_name = '{tbl}'
                        ORDER BY ordinal_position;", cn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            sb.AppendLine($"  {r[0]} | {r[1]} | null={r[2]} | def={r[3]}");
                        }
                    }
                }

                // 8. Constraints (PK, FK, Unique)
                sb.AppendLine("\n--- 8. RESTRICCIONES (PK, FK, UNIQUE) ---");
                using (var cmd = new NpgsqlCommand(@"
                    SELECT tc.table_name, tc.constraint_name, tc.constraint_type, kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                      ON tc.constraint_name = kcu.constraint_name
                     AND tc.table_schema = kcu.table_schema
                    WHERE tc.table_schema = 'public'
                      AND tc.table_name IN ('aocr_tbsolicitud', 'aocr_tbsolicitud_estacion', 'aocr_tbinspeccion', 'aocr_tbinforme_inspeccion', 'aocr_tbfirma_documento', 'aocr_or_orden', 'usuario')
                    ORDER BY tc.table_name, tc.constraint_type, tc.constraint_name;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"  [{r[0]}] {r[2]}: {r[1]} ({r[3]})");
                    }
                }

                // 9. Estados existentes en aocr_tbsolicitud
                sb.AppendLine("\n--- 9. ESTADOS DISTINTOS EN AOCR_TBSOLICITUD ---");
                using (var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT estado, count(*) 
                    FROM aocr_tbsolicitud 
                    GROUP BY estado 
                    ORDER BY count(*) DESC;", cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        sb.AppendLine($"  Estado: '{r[0]}' -> {r[1]} registros");
                    }
                }
            }

            var salida = sb.ToString();
            File.WriteAllText(@"c:\proyectos\AOCR\scripts\diagnostico_esquema_completo_bd.txt", salida, Encoding.UTF8);
        }
    }
}
