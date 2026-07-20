using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.DAOs;
using CapaDatos.Models;
using System.Collections.Generic;

namespace AOCR.Tests.Unit
{
    public class FakeConfigService : CapaDatos.Services.ISecureConfigurationService
    {
        public string GetConnectionString(string name) => "";
        public string GetAppSetting(string key) => "";
        public CapaDatos.Services.AS400Credentials GetAS400Credentials() => new CapaDatos.Services.AS400Credentials { Server = "mock" };
        public CapaDatos.Services.EmailCredentials GetEmailCredentials() => null;
    }

    [TestClass]
    public class FacturacionAS400DAOTests
    {
        [TestMethod]
        public void ConstruirValoresCabecera_Debe_Normalizar_OPCNUM_Y_Correlacionar_OPCOBS()
        {
            // Arrange
            var mockConfig = new FakeConfigService();
            var dao = new FacturacionAS400DAO(mockConfig);
            var privateDao = new PrivateObject(dao);

            var record = new FacturaAs400Record
            {
                NumeroFactura = "001-002-000123456",
                Observaciones = "Vuelo de prueba",
                OrdenId = 999,
                Total = 100.50m
            };

            // Act
            var dict = (Dictionary<string, object>)privateDao.Invoke("ConstruirValoresCabecera", record, 5m);

            // Assert
            Assert.IsTrue(dict.ContainsKey("OPCNUM"));
            Assert.AreEqual(0m, dict["OPCNUM"], "Dado que 001-002-000123456 no es numÃ©rico puro, deberÃ­a resolverse como 0 o segÃºn la regla de TryParse.");

            // Probando con numero valido
            record.NumeroFactura = "123456";
            var dict2 = (Dictionary<string, object>)privateDao.Invoke("ConstruirValoresCabecera", record, 5m);
            Assert.AreEqual(123456m, dict2["OPCNUM"], "Si es numÃ©rico, debe convertirlo a decimal.");

            // Probando OPCOBS
            Assert.IsTrue(dict2.ContainsKey("OPCOBS"));
            var obs = dict2["OPCOBS"].ToString();
            Assert.IsTrue(obs.StartsWith("SOL:0|ORD:999"), "Debe correlacionar SOL y ORD.");
            Assert.IsTrue(obs.Contains("Vuelo de prueba"));
        }
    }
}

