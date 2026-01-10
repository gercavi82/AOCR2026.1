using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    public class InspeccionBL
    {
        // ======================================================
        // LISTAR POR SOLICITUD
        // ======================================================
        public static List<Inspeccion> ObtenerPorSolicitud(int idSolicitud)
        {
            if (idSolicitud <= 0)
                throw new Exception("El código de solicitud es inválido.");

            return InspeccionDAO.ObtenerPorSolicitud(idSolicitud);
        }

        // ======================================================
        // CREAR INSPECCIÓN
        // ======================================================
        public static bool Crear(Inspeccion i, int codigoUsuario)
        {
            if (i == null)
                throw new Exception("Datos de inspección inválidos.");

            if (codigoUsuario <= 0)
                throw new Exception("Código de usuario inválido.");

            i.CreatedAt = DateTime.Now;
            i.CreatedBy = codigoUsuario;

            return InspeccionDAO.Crear(i) > 0;
        }

        // ======================================================
        // GUARDAR INFORME DE INSPECCIÓN
        // ======================================================
        public static bool GuardarInforme(int idInspeccion, string informe, int codigoUsuario)
        {
            if (idInspeccion <= 0)
                throw new Exception("ID de inspección inválido.");

            if (codigoUsuario <= 0)
                throw new Exception("Código de usuario inválido.");

            if (string.IsNullOrWhiteSpace(informe))
                throw new Exception("El informe no puede estar vacío.");

            return InspeccionDAO.GuardarInforme(idInspeccion, informe, codigoUsuario) > 0;
        }

        // ======================================================
        // CERRAR INSPECCIÓN (APROBADO / RECHAZADO)
        // ======================================================
        public static bool CerrarInspeccion(int idInspeccion, string resultado, int codigoUsuario)
        {
            if (idInspeccion <= 0)
                throw new Exception("ID de inspección inválido.");

            if (codigoUsuario <= 0)
                throw new Exception("Código de usuario inválido.");

            if (string.IsNullOrWhiteSpace(resultado))
                throw new Exception("Resultado inválido.");

            resultado = resultado.Trim().ToUpperInvariant();

            bool aprobada;
            if (resultado == "APROBADO")
                aprobada = true;
            else if (resultado == "RECHAZADO")
                aprobada = false;
            else
                throw new Exception("Resultado inválido. Debe ser 'APROBADO' o 'RECHAZADO'.");

            return InspeccionDAO.CerrarInspeccion(idInspeccion, resultado, aprobada, codigoUsuario) > 0;
        }

        // ======================================================
        // ACTUALIZAR INSPECCIÓN (Planificación)
        // ======================================================
        public static bool Actualizar(Inspeccion i)
        {
            if (i == null || i.CodigoInspeccion <= 0)
                throw new Exception("Datos de inspección inválidos.");

            return InspeccionDAO.Actualizar(i); // ✅ Corregido: ya retorna bool
        }
    }
}
