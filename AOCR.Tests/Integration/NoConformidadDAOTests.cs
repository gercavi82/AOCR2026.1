using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaDatos.DAOs;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class NoConformidadDAOTests
    {
        private NoConformidadDAO _dao;

        [TestInitialize]
        public void Setup()
        {
            _dao = new NoConformidadDAO();
        }

        [TestMethod]
        public void Insertar_DebeGenerarIdYGuardarCorrectamente()
        {
            // Arrange
            var nc = new NoConformidad
            {
                CodigoInspeccion = 9999,
                CodigoInforme = 8888,
                CodigoSolicitud = 7777,
                TipoRuta = "CON_INSPECCION",
                Estado = "BORRADOR",
                Version = 1,
                RequiereNuevaInspeccion = true,
                Detalle = "Test detail"
            };

            // Act
            var insertado = _dao.Insertar(nc);

            // Assert
            Assert.IsNotNull(insertado);
            Assert.IsTrue(insertado.CodigoNoConformidad > 0);
            Assert.AreEqual("CON_INSPECCION", insertado.TipoRuta);
        }
        
        [TestMethod]
        public void ObtenerPorInspeccion_DebeRetornarLista()
        {
            // Arrange
            var nc = new NoConformidad
            {
                CodigoInspeccion = 10001,
                CodigoInforme = 10001,
                CodigoSolicitud = 10001,
                TipoRuta = "SIN_INSPECCION",
                Estado = "GENERADA",
                Version = 1,
                RequiereNuevaInspeccion = false
            };
            _dao.Insertar(nc);

            // Act
            var list = _dao.ObtenerPorInspeccion(10001);

            // Assert
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0);
            Assert.AreEqual(10001, list[0].CodigoInspeccion);
            Assert.AreEqual("SIN_INSPECCION", list[0].TipoRuta);
        }
    }
}
