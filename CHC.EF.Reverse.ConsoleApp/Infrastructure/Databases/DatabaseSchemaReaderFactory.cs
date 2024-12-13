using System;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Infrastructure.Databases;
using Microsoft.Extensions.Options;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    /// <summary>
    /// 提供資料庫結構描述讀取器的工廠類別。
    /// </summary>
    public class DatabaseSchemaReaderFactory : IDatabaseSchemaReaderFactory
    {
        private readonly Settings _settings;
        private readonly ILogger _logger;

        /// <summary>
        /// 初始化資料庫結構描述讀取器工廠的新執行個體。
        /// </summary>
        /// <param name="settings">應用程式設定選項</param>
        /// <param name="logger">日誌記錄器</param>
        /// <exception cref="ArgumentNullException">當 settings 或 logger 為 null 時擲回。</exception>
        public DatabaseSchemaReaderFactory(IOptions<Settings> settings, ILogger logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 建立適當的資料庫結構描述讀取器實例。
        /// </summary>
        /// <returns>資料庫結構描述讀取器的實例</returns>
        /// <exception cref="NotSupportedException">當指定的資料庫提供者不受支援時擲回。</exception>
        public IDatabaseSchemaReader Create()
        {
            _logger.Info($"正在建立資料庫結構描述讀取器: {_settings.ProviderName}");

            return _settings.ProviderName?.ToLowerInvariant() switch
            {
                "mysql.data.mysqlclient" => new MySqlSchemaReader(_settings.ConnectionString),
                "microsoft.data.sqlclient" => new SqlServerSchemaReader(_settings.ConnectionString, _logger),
                "npgsql" => new PostgreSqlSchemaReader(_settings.ConnectionString),
                _ => throw new NotSupportedException($"不支援的資料庫提供者: {_settings.ProviderName}")
            };
        }
    }
}