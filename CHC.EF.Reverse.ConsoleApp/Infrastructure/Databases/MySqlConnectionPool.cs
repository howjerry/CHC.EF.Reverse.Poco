using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    /// <summary>
    /// ���� MySQL ��Ʈw�s�u��������w�����ƺ޲z�C
    /// </summary>
    /// <remarks>
    /// ��@�s�u���Ҧ��H���Ѱ��į઺��Ʈw�s�u���ξ���C�֤ߥ\��]�A�G
    /// - �۰ʺ޲z�s�u���إ߻P����
    /// - ������w�����s�u�s������
    /// - �s�u�ƶq����P�귽�ϥκʱ�
    /// - �s�u���A�۰ʺ޲z
    /// </remarks>
    public class MySqlConnectionPool
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Queue<MySqlConnection>> _pool;
        private readonly int _maxPoolSize;
        private int _totalConnections;

        /// <summary>
        /// ��l�� MySqlConnectionPool ���s�������C
        /// </summary>
        /// <param name="maxPoolSize">�s�u�����̤j�s�u�ơA�w�]�� 10</param>
        /// <exception cref="ArgumentException">�� maxPoolSize �p��ε��� 0 ���Y�^</exception>
        public MySqlConnectionPool(int maxPoolSize = 10)
        {
            if (maxPoolSize <= 0)
            {
                throw new ArgumentException("�s�u���j�p�����j�� 0", nameof(maxPoolSize));
            }

            _maxPoolSize = maxPoolSize;
            _pool = new Dictionary<string, Queue<MySqlConnection>>();
            _totalConnections = 0;
        }

        /// <summary>
        /// �q�s�u�����o��Ʈw�s�u�C
        /// </summary>
        /// <param name="connectionString">��Ʈw�s�u�r��</param>
        /// <returns>�i�Ϊ���Ʈw�s�u</returns>
        /// <exception cref="ArgumentNullException">�� connectionString �� null �Ϊťծ��Y�^</exception>
        /// <exception cref="InvalidOperationException">��s�u���w�F��̤j�e�q���Y�^</exception>
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
                throw new InvalidOperationException("�L�k�إ߸�Ʈw�s�u", ex);
            }
        }

        /// <summary>
        /// �N�s�u����^�s�u���C
        /// </summary>
        /// <param name="connection">�n���񪺸�Ʈw�s�u</param>
        /// <returns>��ܫD�P�B�@�~���u�@</returns>
        /// <remarks>
        /// ����k�|�G
        /// 1. �����s�u
        /// 2. �N�s�u��^�����H�ѭ���
        /// 3. �p�G���w���h����s�u�귽
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
                throw new InvalidOperationException("�����Ʈw�s�u�ɵo�Ϳ��~", ex);
            }
        }

        /// <summary>
        /// �M���s�u�������Ҧ��s�u�C
        /// </summary>
        /// <returns>��ܫD�P�B�@�~���u�@</returns>
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
                    $"�s�u���w�F��̤j�e�q ({_maxPoolSize})�C�еy��A�թΦҼ{�W�[�̤j�s�u�ơC");
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
                // �����M�z�ɪ����~
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
                // ��������귽�ɪ����~
            }
        }

        #endregion

        /// <summary>
        /// ���o�s�u������e�έp��T�C
        /// </summary>
        /// <returns>�]�t�s�u���έp��ƪ�����</returns>
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
    /// ��ܳs�u�����έp��T�C
    /// </summary>
    public class PoolStatistics
    {
        /// <summary>
        /// ���o�γ]�w�ثe���`�s�u�ơC
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// ���o�γ]�w�i�Ϊ��s�u�ơC
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// ���o�γ]�w�s�u�����̤j�e�q�C
        /// </summary>
        public int MaxPoolSize { get; set; }
    }
}
