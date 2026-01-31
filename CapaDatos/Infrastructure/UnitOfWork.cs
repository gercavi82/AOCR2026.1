using System;
using System.Data;
using Npgsql;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Interface para Unit of Work
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        NpgsqlConnection Connection { get; }
        NpgsqlTransaction Transaction { get; }
        void Begin(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
        void Commit();
        void Rollback();
        bool IsActive { get; }
    }

    /// <summary>
    /// Implementación de Unit of Work para transacciones multi-tabla
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly string _connectionString;
        private NpgsqlConnection _connection;
        private NpgsqlTransaction _transaction;
        private bool _disposed;

        public NpgsqlConnection Connection { get { return _connection; } }
        public NpgsqlTransaction Transaction { get { return _transaction; } }
        public bool IsActive { get { return _transaction != null; } }

        public UnitOfWork(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }
            _connectionString = connectionString;
        }

        public void Begin(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("Ya existe una transacción activa.");
            }

            _connection = new NpgsqlConnection(_connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction(isolationLevel);
        }

        public void Commit()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No hay transacción activa para confirmar.");
            }

            try
            {
                _transaction.Commit();
            }
            finally
            {
                DisposeTransaction();
            }
        }

        public void Rollback()
        {
            if (_transaction == null)
            {
                return; // No hay transacción, nada que hacer
            }

            try
            {
                _transaction.Rollback();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en rollback: " + ex.Message);
            }
            finally
            {
                DisposeTransaction();
            }
        }

        private void DisposeTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }

            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Closed)
                {
                    _connection.Close();
                }
                _connection.Dispose();
                _connection = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Rollback(); // Rollback si no se hizo commit
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Factory para crear UnitOfWork
    /// </summary>
    public interface IUnitOfWorkFactory
    {
        IUnitOfWork Create();
    }

    /// <summary>
    /// Implementación del factory
    /// </summary>
    public class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly string _connectionString;

        public UnitOfWorkFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IUnitOfWork Create()
        {
            return new UnitOfWork(_connectionString);
        }
    }
}
