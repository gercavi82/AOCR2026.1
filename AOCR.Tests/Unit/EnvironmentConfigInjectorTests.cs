using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class EnvironmentConfigInjectorTests
    {
        [TestInitialize]
        public void Setup()
        {
            EnvironmentConfigInjector.ResetForTesting();
            // Limpiar variables de entorno para las pruebas
            Environment.SetEnvironmentVariable("AOCR_POSTGRES_CONNECTION", null);
            Environment.SetEnvironmentVariable("AOCR_POSTGRES_MIRROR_CONNECTION", null);
            Environment.SetEnvironmentVariable("AOCR_DB2_CONNECTION", null);
            Environment.SetEnvironmentVariable("AOCR_AS400_PASSWORD", null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            EnvironmentConfigInjector.ResetForTesting();
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Inject_ValoresPresentes_SobreescribeConfigurationManager()
        {
            // Arrange
            string testPgConn = "Host=testhost;Database=testdb;";
            string testAs400Pwd = "Secret123!";

            Environment.SetEnvironmentVariable("AOCR_POSTGRES_CONNECTION", testPgConn);
            Environment.SetEnvironmentVariable("AOCR_DB2_CONNECTION", "FakeDB2");
            Environment.SetEnvironmentVariable("AOCR_AS400_PASSWORD", testAs400Pwd);

            // Simular un appSetting dummy
            ConfigurationManager.AppSettings["AS400:Password"] = "dummy";

            // Act
            EnvironmentConfigInjector.Inject();

            // Assert
            Assert.AreEqual(testPgConn, ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString);
            Assert.AreEqual(testAs400Pwd, ConfigurationManager.AppSettings["AS400:Password"]);
        }

        [TestMethod]
        [TestCategory("Unit")]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void Inject_VariableObligatoriaFaltante_LanzaExcepcion()
        {
            // Arrange
            EnvironmentConfigInjector.ResetForTesting();
            Environment.SetEnvironmentVariable("AOCR_POSTGRES_CONNECTION", null);
            
            var existing = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (existing != null)
            {
                var elementReadOnlyField = typeof(ConfigurationElement).GetField("_bReadOnly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (elementReadOnlyField != null)
                {
                    elementReadOnlyField.SetValue(existing, false);
                    existing.ConnectionString = "${AOCR_POSTGRES_CONNECTION}";
                    elementReadOnlyField.SetValue(existing, true);
                }
            }
            
            // Act
            EnvironmentConfigInjector.Inject();
        }
    }
}
