using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaDatos.Entidades;

namespace AOCR.TestUtils
{
    class TestDbConnection
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== TEST DE CONEXIÓN A POSTGRESQL 18 ===");
            Console.WriteLine();

            try
            {
                var dao = new OrdenRecaudacionDAO();
                
                // Probar conexión
                Console.WriteLine("1. Probando conexión básica...");
                bool conexionOk = dao.ProbarConexion();
                Console.WriteLine($"   Resultado: {(conexionOk ? "✓ CONECTADO" : "✗ ERROR DE CONEXIÓN")}");
                Console.WriteLine();

                if (!conexionOk)
                {
                    Console.WriteLine("No se pudo conectar a la base de datos. Verificar configuración.");
                    return;
                }

                // Probar obtener estadísticas
                Console.WriteLine("2. Obteniendo estadísticas básicas...");
                var stats = dao.ObtenerEstadisticas();
                Console.WriteLine($"   Total órdenes: {stats.GetValueOrDefault("total", "N/A")}");
                Console.WriteLine($"   Órdenes BORRADOR: {stats.GetValueOrDefault("borrador", "N/A")}");
                Console.WriteLine($"   Órdenes COMPLETADA: {stats.GetValueOrDefault("completada", "N/A")}");
                Console.WriteLine();

                // Probar los nuevos métodos agregados
                Console.WriteLine("3. Probando nuevos métodos para búsqueda por código de solicitud...");
                
                // Buscar órdenes con código de solicitud 1 (si existe)
                var ordenesPorSolicitud = dao.ObtenerPorCodigoSolicitud(1);
                Console.WriteLine($"   Órdenes encontradas para solicitud 1: {ordenesPorSolicitud.Count}");
                
                if (ordenesPorSolicitud.Count > 0)
                {
                    var primera = dao.ObtenerPrimaPorCodigoSolicitud(1);
                    Console.WriteLine($"   Primera orden para solicitud 1 - ID: {primera?.Id}, Estado: {primera?.Estado}");
                }
                else
                {
                    Console.WriteLine("   No se encontraron órdenes para la solicitud 1");
                }
                Console.WriteLine();

                // Verificar que existe la tabla aocr_or_orden
                Console.WriteLine("4. Verificando estructura de datos...");
                var todasOrdenes = dao.ObtenerTodas();
                Console.WriteLine($"   Total de órdenes en la tabla aocr_or_orden: {todasOrdenes.Count}");

                if (todasOrdenes.Count > 0)
                {
                    var muestra = todasOrdenes[0];
                    Console.WriteLine($"   Muestra - ID: {muestra.Id}, Código Solicitud: {muestra.CodigoSolicitud}, Estado: {muestra.Estado}");
                }
                Console.WriteLine();

                // Verificar existencia de solicitudes
                Console.WriteLine("5. Verificando relación con solicitudes...");
                bool existeSolicitud1 = dao.ExisteSolicitud(1);
                Console.WriteLine($"   Existe solicitud con código 1: {(existeSolicitud1 ? "Sí" : "No")}");
                
                bool existeSolicitud999 = dao.ExisteSolicitud(999);
                Console.WriteLine($"   Existe solicitud con código 999: {(existeSolicitud999 ? "Sí" : "No")}");
                Console.WriteLine();

                Console.WriteLine("=== TODAS LAS PRUEBAS COMPLETADAS EXITOSAMENTE ===");
                Console.WriteLine($"PostgreSQL 18 está funcionando correctamente.");
                Console.WriteLine($"Los nuevos métodos de búsqueda por código de solicitud están operativos.");

            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ ERROR DURANTE LAS PRUEBAS:");
                Console.WriteLine($"   Mensaje: {ex.Message}");
                Console.WriteLine($"   Tipo: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Error interno: {ex.InnerException.Message}");
                }
                Console.WriteLine();
                Console.WriteLine("Posibles causas:");
                Console.WriteLine("- PostgreSQL no está ejecutándose");
                Console.WriteLine("- Configuración de conexión incorrecta");
                Console.WriteLine("- Problemas con las tablas de la base de datos");
            }

            Console.WriteLine();
            Console.WriteLine("Presione cualquier tecla para salir...");
            Console.ReadKey();
        }
    }
}