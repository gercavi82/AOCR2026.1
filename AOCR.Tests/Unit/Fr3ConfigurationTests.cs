using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo.Common;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Fr3ConfigurationTests
    {
        [TestMethod]
        public void Fr3ConfigurationProvider_Should_Return_Legacy_By_Default()
        {
            // Arrange
            var provider = new Fr3ConfigurationProvider();

            // Act
            var config = provider.GetConfiguration();

            // Assert
            Assert.AreEqual(Fr3ProcessingMode.Legacy, config.Mode, "El modo predeterminado debe ser Legacy.");
            Assert.IsTrue(config.TransactionRequired);
            Assert.IsTrue(config.AutomaticRetryEnabled);
            Assert.AreEqual(5, config.MaxIntentos);
            Assert.AreEqual(300, config.BaseBackoffSeconds);
            Assert.AreEqual(60, config.LeaseDurationSeconds);
        }
    }
}
