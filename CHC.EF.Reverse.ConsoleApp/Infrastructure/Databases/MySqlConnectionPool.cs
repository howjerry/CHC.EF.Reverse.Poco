using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    /// <summary>
    /// 提供 MySQL 資料庫連線的執行緒安全池化管理。
    /// </summary>
    /// <remarks>
    /// 實作連線池模式以提供高效能的資料庫連線重用機制。核心功能包括：
    /// - 自動管理連線的建立與釋放
    /// - 執行緒安全的連線存取控制
    /// - 連線數量限制與資源使用監控
    /// - 連線狀態自動管理
    /// </remarks>
    public class MySqlConnectionPool
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Queue<MySqlConnection>> _pool;
        private readonly int _maxPoolSize;
        private int _totalConnections;

        /// <summary>
        /// 初始化 MySqlConnectionPool 的新執行個體。
        /// </summary>
        /// <param name="maxPoolSize">連線池的最大連線數，預設為 10</param>
        /// <exception cref="ArgumentException">當 maxPoolSize 小於或等於 0 時擲回</exception>
        public MySqlConnectionPool(int maxPoolSize = 10)
        {
            if (maxPoolSize <= 0)
            {
                throw new ArgumentException("連線池大小必須大於 0", nameof(maxPoolSize));
            }

            _maxPoolSize = maxPoolSize;
            _pool = new Dictionary<string, Queue<MySqlConnection>>();
            _totalConnections = 0;
        }

        /// <summary>
        /// 從連線池取得資料庫連線。
        /// </summary>
        /// <param name="connectionString">資料庫連線字串</param>
        /// <returns>可用的資料庫連線</returns>
        /// <exception cref="ArgumentNullException">當 connectionString 為 null 或空白時擲回</exception>
        /// <exception cref="InvalidOperationException">當連線池已達到最大容量時擲回</exception>
        public async Task<MySqlConnection> GetConnectionAsync(string connectionString)
        {
            ValidateConnectionString(connectionString);

            MySqlConnection connection = null;
            bool isNewConnection = false;

            lock (_lock)
            {
                connection = GetOrCreateConnection(connectionString, out isNewConnection);
            }

            try
            {
                await EnsureConnectionOpenAsync(connection);
                return connection;
            }
            catch (Exception ex)
            {
                await HandleConnectionErrorAsync(connection, isNewConnection);
                throw new InvalidOperationException("無法建立資料庫連線", ex);
            }
        }

        /// <summary>
        /// 將連線釋放回連線池。
        /// </summary>
        /// <param name="connection">要釋放的資料庫連線</param>
        /// <returns>表示非同步作業的工作</returns>
        /// <remarks>
        /// 此方法會：
        /// 1. 關閉連線
        /// 2. 將連線放回池中以供重用
        /// 3. 如果池已滿則釋放連線資源
        /// </remarks>
        public async Task ReleaseAsync(MySqlConnection connection)
        {
            if (connection == null) return;

            try
            {
                await CloseConnectionAsync(connection);

                lock (_lock)
                {
                    ReturnConnectionToPool(connection);
                }
            }
            catch (Exception ex)
            {
                await HandleReleaseErrorAsync(connection);
                throw new InvalidOperationException("釋放資料庫連線時發生錯誤", ex);
            }
        }

        /// <summary>
        /// 清除連線池中的所有連線。
        /// </summary>
        /// <returns>表示非同步作業的工作</returns>
        public async Task ClearAsync()
        {
            List<MySqlConnection> connectionsToDispose;

            lock (_lock)
            {
                connectionsToDispose = _pool.Values
                    .SelectMany(q => q)
                    .ToList();
                _pool.Clear();
                _totalConnections = 0;
            }

            foreach (var conn in connectionsToDispose)
            {
                await DisposeConnectionAsync(conn);
            }
        }

        #region Private Helper Methods

        private void ValidateConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }
        }

        private MySqlConnection GetOrCreateConnection(string connectionString, out bool isNewConnection)
        {
            isNewConnection = false;

            if (!_pool.TryGetValue(connectionString, out var queue))
            {
                queue = new Queue<MySqlConnection>();
                _pool[connectionString] = queue;
            }

            if (queue.Count > 0)
            {
                return queue.Dequeue();
            }

            if (_totalConnections >= _maxPoolSize)
            {
                throw new InvalidOperationException(
                    $"連線池已達到最大容量 ({_maxPoolSize})。請稍後再試或考慮增加最大連線數。");
            }

            isNewConnection = true;
            _totalConnections++;
            return new MySqlConnection(connectionString);
        }

        private async Task EnsureConnectionOpenAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
        }

        private async Task HandleConnectionErrorAsync(MySqlConnection connection, bool isNewConnection)
        {
            if (isNewConnection)
            {
                lock (_lock)
                {
                    _totalConnections--;
                }
            }

            if (connection != null)
            {
                await DisposeConnectionAsync(connection);
            }
        }

        private async Task CloseConnectionAsync(MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }

        private void ReturnConnectionToPool(MySqlConnection connection)
        {
            var queue = _pool.Values.FirstOrDefault(q => q.Any(c => c == connection));

            if (queue != null && queue.Count < _maxPoolSize)
            {
                queue.Enqueue(connection);
            }
            else
            {
                connection.Dispose();
                _totalConnections--;
            }
        }

        private async Task HandleReleaseErrorAsync(MySqlConnection connection)
        {
            try
            {
                await DisposeConnectionAsync(connection);
                lock (_lock)
                {
                    _totalConnections--;
                }
            }
            catch
            {
                // 忽略清理時的錯誤
            }
        }

        private async Task DisposeConnectionAsync(MySqlConnection connection)
        {
            try
            {
                await connection.CloseAsync();
                connection.Dispose();
            }
            catch
            {
                // 忽略釋放資源時的錯誤
            }
        }

        #endregion

        /// <summary>
        /// 取得連線池的當前統計資訊。
        /// </summary>
        /// <returns>包含連線池統計資料的物件</returns>
        public PoolStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new PoolStatistics
                {
                    TotalConnections = _totalConnections,
                    AvailableConnections = _pool.Values.Sum(q => q.Count),
                    MaxPoolSize = _maxPoolSize
                };
            }
        }
    }

    /// <summary>
    /// 表示連線池的統計資訊。
    /// </summary>
    public class PoolStatistics
    {
        /// <summary>
        /// 取得或設定目前的總連線數。
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// 取得或設定可用的連線數。
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// 取得或設定連線池的最大容量。
        /// </summary>
        public int MaxPoolSize { get; set; }
    }
}
