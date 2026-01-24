using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo.Common;
using CapaNegocio.Helpers;

namespace CapaNegocio
{
    public class OrdenRecaudacionBL
    {
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly ConceptoDAO _conceptoDao = new ConceptoDAO();

        public List<OrdenRecaudacionModel> Listar(int codigoUsuario, string estado = null)
            => _ordenDao.ListarPorUsuario(codigoUsuario, estado);

        public OrdenRecaudacionModel Obtener(int id)
            => _ordenDao.ObtenerPorId(id);

        public ResultadoOperacion CrearBorrador(OrdenRecaudacionModel orden)
        {
            try
            {
                if (orden == null) return ResultadoOperacion.Fail("Orden inválida.");

                orden.Estado = ValidacionEstadosOR.BORRADOR;
                orden.NumeroOrden = string.IsNullOrWhiteSpace(orden.NumeroOrden) ? GenerarNumeroOrden() : orden.NumeroOrden;
                orden.FechaCreacion = (orden.FechaCreacion == default(DateTime)) ? DateTime.Now : orden.FechaCreacion;

                RecalcularTotales(orden);

                var newId = _ordenDao.Insertar(orden);
                return ResultadoOperacion.Success($"Orden creada (BORRADOR). ID={newId}");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        public ResultadoOperacion ActualizarBorrador(OrdenRecaudacionModel orden)
        {
            try
            {
                if (orden == null) return ResultadoOperacion.Fail("Orden inválida.");

                var actual = _ordenDao.ObtenerPorId(orden.Id);
                if (actual == null) return ResultadoOperacion.Fail("Orden no existe.");

                if (!string.Equals(actual.Estado, ValidacionEstadosOR.BORRADOR, StringComparison.OrdinalIgnoreCase))
                    return ResultadoOperacion.Fail("Solo se puede editar una orden en BORRADOR.");

                // Mantener estado y número
                orden.Estado = actual.Estado;
                orden.NumeroOrden = actual.NumeroOrden;
                orden.CodigoUsuario = actual.CodigoUsuario;

                RecalcularTotales(orden);

                var ok = _ordenDao.Actualizar(orden);
                return ok
                    ? ResultadoOperacion.Success("Orden actualizada (BORRADOR).")
                    : ResultadoOperacion.Fail("No se pudo actualizar la orden.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        public ResultadoOperacion Generar(int id)
        {
            try
            {
                var orden = _ordenDao.ObtenerPorId(id);
                if (orden == null) return ResultadoOperacion.Fail("Orden no existe.");

                ValidacionEstadosOR.ValidarTransicion(orden.Estado, ValidacionEstadosOR.GENERADA);

                RecalcularTotales(orden);
                _ordenDao.Actualizar(orden);

                var ok = _ordenDao.CambiarEstado(id, ValidacionEstadosOR.GENERADA);
                return ok
                    ? ResultadoOperacion.Success("Orden generada (GENERADA).")
                    : ResultadoOperacion.Fail("No se pudo cambiar el estado.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        public ResultadoOperacion MarcarEnviada(int id)
        {
            try
            {
                var orden = _ordenDao.ObtenerPorId(id);
                if (orden == null) return ResultadoOperacion.Fail("Orden no existe.");

                ValidacionEstadosOR.ValidarTransicion(orden.Estado, ValidacionEstadosOR.ENVIADA);

                var ok = _ordenDao.CambiarEstado(id, ValidacionEstadosOR.ENVIADA);
                return ok
                    ? ResultadoOperacion.Success("Orden marcada como ENVIADA.")
                    : ResultadoOperacion.Fail("No se pudo cambiar el estado.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        public ResultadoOperacion RegistrarPago(int id)
        {
            try
            {
                var orden = _ordenDao.ObtenerPorId(id);
                if (orden == null) return ResultadoOperacion.Fail("Orden no existe.");

                ValidacionEstadosOR.ValidarTransicion(orden.Estado, ValidacionEstadosOR.PAGADA);

                var ok = _ordenDao.CambiarEstado(id, ValidacionEstadosOR.PAGADA);
                return ok
                    ? ResultadoOperacion.Success("Orden marcada como PAGADA.")
                    : ResultadoOperacion.Fail("No se pudo cambiar el estado.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        public ResultadoOperacion Anular(int id, string motivo)
        {
            try
            {
                var orden = _ordenDao.ObtenerPorId(id);
                if (orden == null) return ResultadoOperacion.Fail("Orden no existe.");

                ValidacionEstadosOR.ValidarTransicion(orden.Estado, ValidacionEstadosOR.ANULADA);

                orden.Observacion = (orden.Observacion ?? "") +
                                   $"\n\nANULADA: {motivo} ({DateTime.Now:dd/MM/yyyy HH:mm})";

                _ordenDao.Actualizar(orden);

                var ok = _ordenDao.CambiarEstado(id, ValidacionEstadosOR.ANULADA);
                return ok
                    ? ResultadoOperacion.Success("Orden anulada.")
                    : ResultadoOperacion.Fail("No se pudo cambiar el estado.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Fail(ex.Message);
            }
        }

        // ================== REGLAS DE CÁLCULO ==================
        public void RecalcularTotales(OrdenRecaudacionModel orden)
        {
            if (orden.Detalles == null) orden.Detalles = new List<OrdenDetalleModel>();

            decimal subtotal = 0m, admin = 0m, total = 0m;

            foreach (var d in orden.Detalles)
            {
                // Normalizar: si ValorUnitario viene 0, intenta poblar desde concepto
                if (d.ValorUnitario <= 0 && d.ConceptoId > 0)
                {
                    var c = _conceptoDao.ObtenerPorId(d.ConceptoId);
                    if (c != null)
                    {
                        d.ConceptoCodigo = c.Codigo;
                        d.ConceptoNombre = c.Nombre;
                        d.ValorUnitario = c.ValorBase;

                        // Normaliza % admin: si viene "8" => 8% => 0.08
                        var p = c.PorcentajeAdmin;
                        d.PorcentajeAdmin = (p > 1m) ? (p / 100m) : p;
                    }
                }
                else
                {
                    if (d.PorcentajeAdmin > 1m) d.PorcentajeAdmin = d.PorcentajeAdmin / 100m;
                }

                d.Subtotal = d.Cantidad * d.ValorUnitario;
                d.Admin = Math.Round(d.Subtotal * d.PorcentajeAdmin, 2);
                d.TotalLinea = Math.Round(d.Subtotal + d.Admin, 2);

                subtotal += d.Subtotal;
                admin += d.Admin;
                total += d.TotalLinea;
            }

            orden.Subtotal = Math.Round(subtotal, 2);
            orden.Admin = Math.Round(admin, 2);
            orden.Total = Math.Round(total, 2);
        }

        private string GenerarNumeroOrden()
        {
            return $"OR-{DateTime.Now:yyyyMMdd-HHmmss}";
        }
    }
}
