using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    /// <summary>
    /// 提供 SQL Server 連線池管理的實現類別
    /// </summary>
    public class SqlConnectionPool : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Queue<SqlConnection>> _pool;
        private readonly int _maxPoolSize;
        private bool _disposed;

        /// <summary>
        /// 初始化 SQL 連線池
        /// </summary>
        /// <param name="maxPoolSize">連線池最大容量，預設為 10</param>
        public SqlConnectionPool(int maxPoolSize = 10)
        {
            _maxPoolSize = maxPoolSize;
            _pool = new Dictionary<string, Queue<SqlConnection>>();
        }

        /// <summary>
        /// 從連線池獲取連線
        /// </summary>
        /// <param name="connectionString">資料庫連線字串</param>
        /// <returns>資料庫連線包裝器</returns>
        public async Task<IDisposable> GetConnectionAsync(string connectionString)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SqlConnectionPool));
            }

            SqlConnection connection = null;
            lock (_lock)
            {
                if (!_pool.ContainsKey(connectionString))
                {
                    _pool[connectionString] = new Queue<SqlConnection>();
                }

                var connections = _pool[connectionString];
                if (connections.Count > 0)
                {
                    connection = connections.Dequeue();
                }
                else if (connections.Count < _maxPoolSize)
                {
                    connection = new SqlConnection(connectionString);
                }
            }

            if (connection == null)
            {
                throw new InvalidOperationException("連線池已達到最大容量限制");
            }

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            return new PooledConnection(connection, this, connectionString);
        }

        /// <summary>
        /// 歸還連線至連線池
        /// </summary>
        /// <param name="connection">要歸還的連線</param>
        /// <param name="connectionString">連線字串</param>
        private void ReturnConnection(SqlConnection connection, string connectionString)
        {
            if (connection == null) return;

            lock (_lock)
            {
                if (!_disposed)
                {
                    _pool[connectionString].Enqueue(connection);
                }
                else
                {
                    try
                    {
                        connection.Dispose();
                    }
                    catch { /* 忽略關閉時的錯誤 */ }
                }
            }
        }

        /// <summary>
        /// 釋放連線池資源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                if (!_disposed)
                {
                    foreach (var connections in _pool.Values)
                    {
                        while (connections.Count > 0)
                        {
                            var connection = connections.Dequeue();
                            try
                            {
                                connection.Dispose();
                            }
                            catch { /* 忽略關閉時的錯誤 */ }
                        }
                    }
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 連線包裝器類別，確保連線正確歸還至連線池
        /// </summary>
        public class PooledConnection : IDisposable
        {
            private readonly SqlConnection _connection;
            private readonly SqlConnectionPool _pool;
            private readonly string _connectionString;
            private bool _disposed;

            public PooledConnection(SqlConnection connection, SqlConnectionPool pool, string connectionString)
            {
                _connection = connection;
                _pool = pool;
                _connectionString = connectionString;
            }

            public SqlConnection Connection => _connection;

            public void Dispose()
            {
                if (_disposed) return;

                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
                _pool.ReturnConnection(_connection, _connectionString);
                _disposed = true;
            }
        }
    }
}
