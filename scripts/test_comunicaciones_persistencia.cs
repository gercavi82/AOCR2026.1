// Ejecutar exclusivamente con la base vacía de test_comunicaciones_fixture.sql.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;
using CapaNegocio.Services;
using Npgsql;

class ComunicacionesPersistenciaTest
{
    static string cs;
    static int assertions;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static object Scalar(string sql) { using (var cn = new NpgsqlConnection(cs)) { cn.Open(); return new NpgsqlCommand(sql, cn).ExecuteScalar(); } }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "La operación inválida debía rechazarse."); }
    static int Main(string[] args)
    {
        try
        {
            cs = args[0];
            // Defensa contra ejecución accidental sobre otro servidor/base.
            var builder = new NpgsqlConnectionStringBuilder(cs);
            if (builder.Host != "127.0.0.1" || builder.Port != 55439 || builder.Database != "comunicaciones_test")
                throw new Exception("Este harness solo permite la base local aislada de pruebas.");
            Environment.SetEnvironmentVariable("AOCR_CONNSTR_AOCRCONNECTION", cs);
            var dao = new OrdenRecaudacionDAO(cs);
            Reject(() => dao.DevolverOrdenAFinanciero(1, 999, 10, "Faltan valores"));
            Check(dao.ListarCorreccionesFinancieras().Count == 0, "No debe auditar una orden de otra solicitud.");
            dao.DevolverOrdenAFinanciero(1, 101, 10, "Falta concepto de inspección");
            var correction = dao.ListarCorreccionesFinancieras(1).Single();
            Check((string)Scalar("SELECT estado FROM aocr_or_orden WHERE id=1") == "EN_REVISION_FINANCIERA", "La orden debe volver a Financiero.");
            Reject(() => dao.DevolverOrdenAFinanciero(1, 101, 10, "Duplicada"));
            Reject(() => dao.GuardarCorreccionFinanciera(1, correction.Id, new List<DetalleOrden>(), 20, "Omitir líneas"));
            Check(Convert.ToDecimal(Scalar("SELECT total FROM aocr_or_orden WHERE id=1")) == 110m, "Una corrección inválida no cambia el total.");
            var details = new List<DetalleOrden> {
                new DetalleOrden { Id=11, ConceptoId=7, Cantidad=2, ValorUnitario=100, PorcentajeAdmin=10 },
                new DetalleOrden { ConceptoId=8, Cantidad=1, ValorUnitario=50, PorcentajeAdmin=10 }
            };
            var catalog = new[] {
                new CapaDatos.Models.ConceptoModel { Id=7, Codigo="INSPECCION_EXT", Nombre="Inspección", Activo=true },
                new CapaDatos.Models.ConceptoModel { Id=8, Codigo="VIATICOS", Nombre="Viáticos", Activo=true }
            };
            Check(OrdenRecaudacionService.PrepararCorreccionFinanciera(details, catalog, "Añadir valores requeridos") == null, "Los conceptos válidos deben aceptarse.");
            Reject(() => dao.GuardarCorreccionFinanciera(1, correction.Id + 1, details, 20, "Formulario desactualizado"));
            dao.GuardarCorreccionFinanciera(1, correction.Id, details, 20, "Añadir valores requeridos");
            Check(Convert.ToDecimal(Scalar("SELECT total FROM aocr_or_orden WHERE id=1")) == 275m, "Total recalculado incorrecto.");
            Check(Convert.ToInt32(Scalar("SELECT COUNT(*) FROM aocr_or_orden_detalle WHERE id=11")) == 1, "Debe conservar el ID y relaciones del detalle original.");
            Reject(() => dao.ValidarCorreccionFinanciera(1, correction.Id, 275, 20, 274, "REF", "factura.pdf", "comprobante.pdf"));
            Reject(() => dao.ValidarCorreccionFinanciera(1, correction.Id, 110, 20, 275, "REF", "factura.pdf", "comprobante.pdf"));
            Check(dao.ListarCorreccionesFinancieras(1).Single().Estado == "CORREGIDA", "Rechazar validación no debe habilitar el expediente.");
            dao.ValidarCorreccionFinanciera(1, correction.Id, 275, 20, 275, "REF", "factura.pdf", "comprobante.pdf");
            Check(dao.ListarCorreccionesFinancieras(1).Count == 0, "La validación debe retirar el pendiente.");
            Check((string)Scalar("SELECT estado FROM aocr_or_orden WHERE id=1") == "PAGADA", "Debe restablecer el estado original.");
            Check(Convert.ToDecimal(Scalar("SELECT (orden_anterior->>'total')::numeric FROM aocr_or_correccion_financiera LIMIT 1")) == 110m, "La auditoría debe conservar el total original.");
            Reject(() => dao.ValidarCorreccionFinanciera(1, correction.Id, 275, 20, 275, "REF", "factura.pdf", "comprobante.pdf"));
            var stations = new SolicitudEstacionService(new SolicitudEstacionDAO());
            var location = new SolicitudEstacionInspeccion { EstacionCodigo="UIO", EstacionNombre="Quito", FechaInicio=new DateTime(2030,1,1), FechaFin=new DateTime(2030,1,2) };
            Check(stations.GuardarLugaresSolicitados(101, new[] { location }, 95).Exitoso, "El RT debe guardar lugares sin fechas.");
            Check(Scalar("SELECT fecha_inicio FROM aocr_tbsolicitud_estacion LIMIT 1") == DBNull.Value, "No debe inventar fecha ni aceptar la fecha enviada por RT.");
            var station = new SolicitudEstacionDAO().ListarPorSolicitud(101).Single();
            Check(station.FechaInicio == default(DateTime), "La lectura debe interpretar NULL como fecha pendiente.");
            station.FechaInicio = station.FechaFin = new DateTime(2026,10,20);
            using (var scope = new TransactionScope()) {
                Check(stations.GuardarEstaciones(101, new[] { station }, 10).Exitoso, "Guardar fechas dentro de transacción.");
                // No completar: simula el fallo del guardado de la inspección.
            }
            Check(Scalar("SELECT fecha_inicio FROM aocr_tbsolicitud_estacion LIMIT 1") == DBNull.Value, "Debe revertir fechas si falla el conjunto de planificación.");
            Check(stations.GuardarEstaciones(101, new[] { station }, 10).Exitoso, "El Inspector debe poder guardar las fechas acordadas.");
            location.Version = 2;
            Check(stations.GuardarLugaresSolicitados(101, new[] { location }, 95).Exitoso, "El RT puede mantener los lugares existentes.");
            Check(new SolicitudEstacionDAO().ListarPorSolicitud(101).Single().FechaInicio == new DateTime(2026,10,20), "El RT no debe reemplazar fechas acordadas por Inspector.");
            Console.WriteLine("OK: " + assertions + " verificaciones de persistencia, auditoría, concurrencia y rollback.");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
