using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    public class MySqlSchemaReader : IDatabaseSchemaReader
    {
        private readonly string _connectionString;
        private static readonly MySqlConnectionPool _connectionPool = new MySqlConnectionPool();
        private static readonly Dictionary<string, List<ForeignKeyDefinition>> _foreignKeyCache = new Dictionary<string, List<ForeignKeyDefinition>>();

        public MySqlSchemaReader(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 讀取資料庫中的所有資料表定義。
        /// </summary>
        /// <returns>資料表定義的集合</returns>
        public async Task<List<TableDefinition>> ReadTables()
        {
            var tables = new List<TableDefinition>();
            MySqlConnection conn = null;

            try
            {
                conn = await _connectionPool.GetConnectionAsync(_connectionString);

                // 先讀取所有資料表的基本資訊
                tables = await ReadTableBasicInfo(conn);

                // 依序處理每個資料表的詳細資訊
                foreach (var table in tables)
                {
                    using (var detailConn = await _connectionPool.GetConnectionAsync(_connectionString))
                    {
                        try
                        {
                            // 使用獨立的連線讀取詳細資訊
                            await ReadTableDetails(detailConn, table);
                        }
                        finally
                        {
                            await _connectionPool.ReleaseAsync(detailConn);
                        }
                    }
                }
            }
            finally
            {
                if (conn != null)
                {
                    await _connectionPool.ReleaseAsync(conn);
                }
            }

            return tables;
        }

        /// <summary>
        /// 讀取資料表的基本資訊。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <returns>資料表定義的基本資訊集合</returns>
        private async Task<List<TableDefinition>> ReadTableBasicInfo(MySqlConnection conn)
        {
            var tables = new List<TableDefinition>();
            using (var cmd = new MySqlCommand(@"
        SELECT 
            TABLE_NAME,
            TABLE_SCHEMA,
            TABLE_COMMENT
        FROM information_schema.TABLES 
        WHERE TABLE_SCHEMA = DATABASE() 
        AND TABLE_TYPE = 'BASE TABLE'", conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var table = new TableDefinition
                        {
                            TableName = reader["TABLE_NAME"].ToString(),
                            SchemaName = reader["TABLE_SCHEMA"].ToString(),
                            Comment = reader["TABLE_COMMENT"].ToString(),
                            Columns = new List<ColumnDefinition>(),
                            ForeignKeys = new List<ForeignKeyDefinition>(),
                            Indexes = new List<IndexDefinition>()
                        };
                        tables.Add(table);
                    }
                }
            }
            return tables;
        }

        /// <summary>
        /// 讀取單一資料表的詳細資訊。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">要讀取詳細資訊的資料表定義</param>
        private async Task ReadTableDetails(MySqlConnection conn, TableDefinition table)
        {
            // 依序讀取各種詳細資訊，確保不會同時開啟多個 DataReader
            await ReadColumns(conn, table);
            await ReadIndexes(conn, table);
            await ReadForeignKeys(conn, table);
            UpdateOneToOneRelationships(table);
        }

        /// <summary>
        /// 讀取資料表的欄位定義。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">資料表定義</param>
        private async Task ReadColumns(MySqlConnection conn, TableDefinition table)
        {
            using (var cmd = new MySqlCommand(@"
        SELECT 
            COLUMN_NAME,
            DATA_TYPE,
            IS_NULLABLE,
            CHARACTER_MAXIMUM_LENGTH,
            NUMERIC_PRECISION,
            NUMERIC_SCALE,
            COLUMN_DEFAULT,
            EXTRA,
            COLUMN_COMMENT,
            COLUMN_TYPE,
            COLLATION_NAME
        FROM information_schema.COLUMNS 
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = @tableName
        ORDER BY ORDINAL_POSITION", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", table.TableName);

                // 完整讀取資料後關閉 DataReader
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var column = CreateColumnDefinition(reader);
                        table.Columns.Add(column);
                    }
                }
            }

            // 在新的連線上下文中讀取主鍵資訊
            using (var cmd = new MySqlCommand(@"
        SELECT COLUMN_NAME
        FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = @tableName
        AND CONSTRAINT_NAME = 'PRIMARY'", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", table.TableName);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader["COLUMN_NAME"].ToString();
                        var column = table.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                        if (column != null)
                        {
                            column.IsPrimaryKey = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 讀取資料表的索引定義。
        /// </summary>
        /// <param name="conn">資料庫連線物件</param>
        /// <param name="table">要讀取索引的資料表定義</param>
        /// <returns>非同步操作的工作</returns>
        /// <remarks>
        /// 此方法負責讀取指定資料表的所有索引資訊，包括：
        /// - 索引名稱
        /// - 唯一性約束
        /// - 索引欄位及其排序方向
        /// - 主鍵索引識別
        /// 方法會保持連線開啟狀態，以供後續操作使用。
        /// </remarks>
        private async Task ReadIndexes(MySqlConnection conn, TableDefinition table)
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using (var cmd = new MySqlCommand(@"
        SELECT 
            INDEX_NAME,
            NON_UNIQUE,
            COLUMN_NAME,
            SEQ_IN_INDEX,
            COLLATION AS SORT_DIRECTION
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = @tableName
        ORDER BY INDEX_NAME, SEQ_IN_INDEX", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", table.TableName);

                var currentIndexName = string.Empty;
                IndexDefinition currentIndex = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var indexName = reader["INDEX_NAME"].ToString();

                        if (indexName != currentIndexName)
                        {
                            currentIndex = new IndexDefinition
                            {
                                IndexName = indexName,
                                IsUnique = !Convert.ToBoolean(reader["NON_UNIQUE"]),
                                IsPrimaryKey = indexName == "PRIMARY",
                                IsDisabled = false,
                                Columns = new List<IndexColumnDefinition>()
                            };

                            table.Indexes.Add(currentIndex);
                            currentIndexName = indexName;
                        }

                        currentIndex.Columns.Add(new IndexColumnDefinition
                        {
                            ColumnName = reader["COLUMN_NAME"].ToString(),
                            IsDescending = reader["SORT_DIRECTION"].ToString() == "D",
                            KeyOrdinal = Convert.ToInt32(reader["SEQ_IN_INDEX"]),
                            IsIncluded = false // MySQL 不支援包含的欄位
                        });
                    }
                }
            }
            // 移除了 await _connectionPool.ReleaseAsync(conn); 的呼叫
            // 保持連線開啟狀態，供後續的 ReadForeignKeys 使用
        }

        private async Task ReadForeignKeys(MySqlConnection conn, TableDefinition table)
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            if (_foreignKeyCache.TryGetValue(table.TableName, out var cachedForeignKeys))
            {
                table.ForeignKeys = cachedForeignKeys;
                await _connectionPool.ReleaseAsync(conn);
                return;
            }

            using (var cmd = new MySqlCommand(@"
            SELECT 
                CONSTRAINT_NAME,
                COLUMN_NAME,
                REFERENCED_TABLE_NAME,
                REFERENCED_COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
            AND TABLE_NAME = @tableName
            AND REFERENCED_TABLE_NAME IS NOT NULL
            ORDER BY CONSTRAINT_NAME, ORDINAL_POSITION", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", table.TableName);

                var currentFkName = string.Empty;
                ForeignKeyDefinition currentFk = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var constraintName = reader["CONSTRAINT_NAME"].ToString();

                        if (constraintName != currentFkName)
                        {
                            currentFk = new ForeignKeyDefinition
                            {
                                ConstraintName = constraintName,
                                PrimaryTable = reader["REFERENCED_TABLE_NAME"].ToString(),
                                ColumnPairs = new List<ForeignKeyColumnPair>(),
                                IsEnabled = true
                            };

                            // 讀取刪除和更新規則
                            await ReadForeignKeyRules(table.TableName, constraintName, currentFk);

                            table.ForeignKeys.Add(currentFk);
                            currentFkName = constraintName;
                        }

                        currentFk.ColumnPairs.Add(new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = reader["COLUMN_NAME"].ToString(),
                            PrimaryKeyColumn = reader["REFERENCED_COLUMN_NAME"].ToString()
                        });

                        if (currentFk.ColumnPairs.Count == 1)
                        {
                            currentFk.ForeignKeyColumn = reader["COLUMN_NAME"].ToString();
                            currentFk.PrimaryKeyColumn = reader["REFERENCED_COLUMN_NAME"].ToString();
                        }

                        currentFk.IsCompositeKey = currentFk.ColumnPairs.Count > 1;
                    }

                    await reader.CloseAsync();
                }
            }

            _foreignKeyCache[table.TableName] = table.ForeignKeys;
            await _connectionPool.ReleaseAsync(conn);
        }

        private async Task ReadForeignKeyRules(string tableName, string constraintName, ForeignKeyDefinition fk)
        {
            using (var conn = await _connectionPool.GetConnectionAsync(_connectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                using (var cmd = new MySqlCommand(@"
            SELECT 
                DELETE_RULE,
                UPDATE_RULE
            FROM information_schema.REFERENTIAL_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
            AND TABLE_NAME = @tableName
            AND CONSTRAINT_NAME = @constraintName", conn))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@constraintName", constraintName);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            fk.DeleteRule = reader["DELETE_RULE"].ToString();
                            fk.UpdateRule = reader["UPDATE_RULE"].ToString();
                        }

                        await reader.CloseAsync();
                    }
                }
                await _connectionPool.ReleaseAsync(conn);
            }
        }

        private void UpdateOneToOneRelationships(TableDefinition table)
        {
            foreach (var fk in table.ForeignKeys)
            {
                // 檢查是否存在唯一索引只包含這個外鍵列
                var hasUniqueConstraint = table.Indexes
                    .Where(idx => idx.IsUnique && !idx.IsPrimaryKey)
                    .Any(idx => idx.Columns.Count == 1 &&
                               idx.Columns[0].ColumnName == fk.ForeignKeyColumn);

                if (hasUniqueConstraint)
                {
                    fk.Comment = (fk.Comment ?? "") + " [One-to-One Relationship]";
                }
            }
        }

        /// <summary>
        /// 從資料庫讀取結果建立欄位定義物件。
        /// </summary>
        /// <param name="reader">包含欄位資訊的資料庫讀取器</param>
        /// <returns>欄位定義物件</returns>
        /// <remarks>
        /// 此方法負責解析資料庫欄位的完整定義，包括：
        /// - 基本屬性（名稱、資料類型、可為空性）
        /// - 進階屬性（長度、精確度、預設值）
        /// - 特殊屬性（自動遞增、計算欄位、產生類型）
        /// </remarks>
        /// <exception cref="ArgumentNullException">當 reader 為 null 時擲回</exception>
        /// <exception cref="InvalidOperationException">當無法正確讀取欄位資訊時擲回</exception>
        private ColumnDefinition CreateColumnDefinition(DbDataReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader),
                    "資料庫讀取器不可為 null");
            }

            try
            {
                return new ColumnDefinition
                {
                    // 基本欄位資訊
                    ColumnName = GetSafeString(reader, "COLUMN_NAME"),
                    DataType = GetSafeString(reader, "DATA_TYPE"),
                    IsNullable = GetSafeString(reader, "IS_NULLABLE")
                        .Equals("YES", StringComparison.OrdinalIgnoreCase),
                    Comment = GetSafeString(reader, "COLUMN_COMMENT"),

                    // 資料類型相關屬性
                    MaxLength = GetNullableLong(reader, "CHARACTER_MAXIMUM_LENGTH"),
                    Precision = GetNullableInt(reader, "NUMERIC_PRECISION"),
                    Scale = GetNullableInt(reader, "NUMERIC_SCALE"),

                    // 進階屬性
                    DefaultValue = GetSafeString(reader, "COLUMN_DEFAULT"),
                    CollationType = GetSafeString(reader, "COLLATION_NAME"),

                    // 特殊屬性
                    IsIdentity = IsAutoIncrement(reader),
                    IsComputed = IsComputedColumn(reader),
                    GeneratedType = DetermineGeneratedType(reader)
                };
            }
            catch (Exception ex)
            {
                var columnName = GetSafeString(reader, "COLUMN_NAME");
                throw new InvalidOperationException(
                    $"建立欄位定義時發生錯誤: {columnName}", ex);
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// 安全地從資料讀取器取得字串值。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>欄位值，若為 DBNull 則返回空字串</returns>
        private string GetSafeString(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        /// <summary>
        /// 安全地從資料讀取器取得可為空的長整數值。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>欄位值，若為 DBNull 則返回 null</returns>
        private long? GetNullableLong(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
        }

        /// <summary>
        /// 安全地從資料讀取器取得可為空的整數值。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>欄位值，若為 DBNull 則返回 null</returns>
        private int? GetNullableInt(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        /// <summary>
        /// 判斷欄位是否為自動遞增。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <returns>若為自動遞增欄位則返回 true，否則返回 false</returns>
        private bool IsAutoIncrement(DbDataReader reader)
        {
            var extra = GetSafeString(reader, "EXTRA");
            return extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判斷欄位是否為計算欄位。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <returns>若為計算欄位則返回 true，否則返回 false</returns>
        private bool IsComputedColumn(DbDataReader reader)
        {
            var extra = GetSafeString(reader, "EXTRA");
            return extra.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase) ||
                   extra.Contains("STORED", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判斷欄位的產生類型。
        /// </summary>
        /// <param name="reader">資料讀取器</param>
        /// <returns>欄位的產生類型定義</returns>
        private string DetermineGeneratedType(DbDataReader reader)
        {
            var extra = GetSafeString(reader, "EXTRA");
            if (extra.Contains("STORED", StringComparison.OrdinalIgnoreCase))
                return "STORED";
            if (extra.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase))
                return "VIRTUAL";
            return null;
        }

        #endregion
    }
}
