using System;
using System.Data;
using System.Reflection;
using System.Transactions;
using CapaDatos.DAOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SolicitudEstacionTransaccionTests
    {
        private static bool Ejecutar(Conexion cn, Func<IDbTransaction, bool> guardar)
        {
            var metodo = typeof(SolicitudEstacionDAO).GetMethod("EjecutarGuardadoTransaccional", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)metodo.Invoke(null, new object[] { cn, guardar });
        }

        [TestMethod]
        public void DentroDelFormulario_NoAbreNiConfirmaOtraTransaccion()
        {
            var cn = new Conexion();
            using (var scope = new TransactionScope())
            {
                Assert.IsTrue(Ejecutar(cn, tx => { Assert.IsNull(tx); return true; }));
                Assert.AreEqual(0, cn.Inicios);
                Assert.AreEqual(TransactionStatus.Active, Transaction.Current.TransactionInformation.Status);
                Assert.IsFalse(cn.Tx.Confirmada);
            }
        }

        [TestMethod]
        public void GuardadoIndependiente_ConfirmaSuTransaccion()
        {
            var cn = new Conexion();
            Assert.IsTrue(Ejecutar(cn, tx => { Assert.AreSame(cn.Tx, tx); return true; }));
            Assert.AreEqual(1, cn.Inicios);
            Assert.IsTrue(cn.Tx.Confirmada);
            Assert.IsTrue(cn.Tx.Liberada);
        }

        [TestMethod]
        public void GuardadoIndependienteFallido_Revierte()
        {
            var cn = new Conexion();
            Assert.IsFalse(Ejecutar(cn, tx => false));
            Assert.IsTrue(cn.Tx.Revertida);
            Assert.IsFalse(cn.Tx.Confirmada);
        }

        [TestMethod]
        public void ExcepcionIndependiente_RevierteYPropaga()
        {
            var cn = new Conexion();
            var ex = Assert.ThrowsException<TargetInvocationException>(() => Ejecutar(cn, tx => { throw new InvalidOperationException("fallo simulado"); }));
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
            Assert.IsTrue(cn.Tx.Revertida);
        }

        [TestMethod]
        public void FalloDentroDelFormulario_SeDevuelveAlResponsableDelRollback()
        {
            var cn = new Conexion();
            using (var scope = new TransactionScope())
            {
                Assert.IsFalse(Ejecutar(cn, tx => false));
                Assert.AreEqual(0, cn.Inicios);
                Assert.IsFalse(cn.Tx.Confirmada);
            }
        }

        private sealed class Conexion : IDbConnection
        {
            public readonly Transaccion Tx = new Transaccion();
            public int Inicios;
            public IDbTransaction BeginTransaction() { Inicios++; return Tx; }
            public IDbTransaction BeginTransaction(System.Data.IsolationLevel level) { return BeginTransaction(); }
            public string ConnectionString { get; set; }
            public int ConnectionTimeout => 0;
            public string Database => "prueba";
            public ConnectionState State => ConnectionState.Open;
            public void ChangeDatabase(string name) { throw new NotSupportedException(); }
            public IDbCommand CreateCommand() { throw new NotSupportedException(); }
            public void Open() { }
            public void Close() { }
            public void Dispose() { }
        }

        private sealed class Transaccion : IDbTransaction
        {
            public bool Confirmada, Revertida, Liberada;
            public IDbConnection Connection => null;
            public System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
            public void Commit() { Confirmada = true; }
            public void Rollback() { Revertida = true; }
            public void Dispose() { Liberada = true; }
        }
    }
}
