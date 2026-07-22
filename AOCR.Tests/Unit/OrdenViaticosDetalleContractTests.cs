using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenViaticosDetalleContractTests
    {
        [TestMethod]
        public void Nueva_SeparaCantidadDeNumeroDiasYAgregaViaticosAutomaticamente()
        {
            var view = Read("CapaPresentacion/Views/OrdenRecaudacion/Nueva.cshtml");

            StringAssert.Contains(view, "id=\"encabezadoCantidad\">Cantidad</th>");
            StringAssert.Contains(view, "id=\"encabezadoNumeroDias\">NÚMERO DE DÍAS</th>");
            StringAssert.Contains(view, "agregarDetalleDesdeOpcion($opcionViaticos, 1, 1)");
            StringAssert.Contains(view, "NumeroDiasInspeccion: d.NumeroDiasInspeccion");
            StringAssert.Contains(view, "detalles[idx].Cantidad = 1;");
        }

        [TestMethod]
        public void Servidor_FuerzaCantidadUnoYCalculaConNumeroDias()
        {
            var controller = Read("CapaPresentacion/Controllers/OrdenRecaudacionController.cs");

            StringAssert.Contains(controller, "cantidad = 1;");
            StringAssert.Contains(controller, "CalcularSubtotalViatico(numeroDiasRecibido, precioUnitario)");
            StringAssert.Contains(controller, "NumeroDiasInspeccion = numeroDiasInspeccion");
            StringAssert.Contains(controller, "TieneLugarConViaticos(lugarNormalizado)");
        }

        [TestMethod]
        public void Migracion_PersisteDiasSinReutilizarCantidad()
        {
            var sql = Read("scripts/20260722_aocr_viaticos_dias_menos_uno.sql");

            StringAssert.Contains(sql, "ADD COLUMN IF NOT EXISTS numero_dias_inspeccion INTEGER");
            StringAssert.Contains(sql, "ADD COLUMN IF NOT EXISTS dias_pagados_viatico INTEGER");
            StringAssert.Contains(sql, "NEW.dias_pagados_viatico := GREATEST(v_numero_dias_inspeccion - 1, 0)");
            StringAssert.Contains(sql, "COALESCE(NEW.porcentaje_admin, 0) / 100.0");
        }

        private static string Read(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var absolutePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(File.Exists(absolutePath), "No se encontro el archivo: " + absolutePath);
            return File.ReadAllText(absolutePath);
        }
    }
}
