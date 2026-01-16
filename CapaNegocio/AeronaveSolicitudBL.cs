using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    public class AeronaveSolicitudBL
    {
        private readonly AeronaveSolicitudDAO _dao;

        public AeronaveSolicitudBL()
        {
            _dao = new AeronaveSolicitudDAO();
        }

        public List<AeronaveSolicitud> ObtenerPorSolicitud(int codigoSolicitud)
        {
            return _dao.ObtenerPorSolicitud(codigoSolicitud);
        }

        public bool Crear(AeronaveSolicitud a, int codigoUsuario)
        {
            if (a == null) return false;

            a.UsuarioRegistro = codigoUsuario.ToString();

            // Si no te llega FechaRegistro, la seteamos
            if (!a.FechaRegistro.HasValue)
                a.FechaRegistro = DateTime.Now;

            // ✅ Usamos el usuario como created_by
            int id = _dao.Crear(a, a.UsuarioRegistro);

            return id > 0;
        }

        public bool Eliminar(int codigoAeronaveSolicitud)
        {
            return _dao.Eliminar(codigoAeronaveSolicitud);
        }

        public bool ReemplazarLista(int codigoSolicitud, List<AeronaveSolicitud> lista, int codigoUsuario)
        {
            // Borra todas y vuelve a insertar
            _dao.EliminarPorSolicitud(codigoSolicitud);

            if (lista == null || lista.Count == 0)
                return true; // no hay nada que insertar

            // Limpieza mínima: sin matrícula no insertamos
            var aeronavesValidas = lista
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                .ToList();

            foreach (var a in aeronavesValidas)
            {
                a.CodigoSolicitud = codigoSolicitud;
                a.UsuarioRegistro = codigoUsuario.ToString();
                a.FechaRegistro = a.FechaRegistro ?? DateTime.Now;

                _dao.Crear(a, a.UsuarioRegistro);
            }

            return true;
        }
    }
}
