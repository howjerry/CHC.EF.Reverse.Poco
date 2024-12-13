using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    /// <summary>
    /// PostgreSQL 資料庫結構描述讀取器的實作。
    /// </summary>
    /// <remarks>
    /// 提供從 PostgreSQL 資料庫讀取表格結構、欄位定義、關聯等資訊的功能。
    /// 支援：
    /// - 表格與欄位的詳細資訊
    /// - 主鍵與外鍵約束
    /// - 索引資訊
    /// - 欄位註解
    /// </remarks>
    public class PostgreSqlSchemaReader : IDatabaseSchemaReader
    {
        private readonly string _connectionString;
        private readonly string _schema;
        private static readonly NpgsqlConnectionPool _connectionPool = new NpgsqlConnectionPool();
        private static readonly Dictionary<string, List<ForeignKeyDefinition>> _foreignKeyCache = new Dictionary<string, List<ForeignKeyDefinition>>();

        /// <summary>
        /// 初始化 PostgreSQL 結構描述讀取器的新執行個體。
        /// </summary>
        /// <param name="connectionString">資料庫連線字串</param>
        /// <param name="schema">要讀取的結構描述名稱，預設為 "public"</param>
        public PostgreSqlSchemaReader(string connectionString, string schema = "public")
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _schema = schema ?? "public";
        }

        /// <summary>
        /// 讀取資料庫中的所有表格定義。
        /// </summary>
        /// <returns>表格定義的集合</returns>
        public async Task<List<TableDefinition>> ReadTables()
        {
            var tables = new List<TableDefinition>();

            using (var conn = await _connectionPool.GetConnectionAsync(_connectionString))
            {
                await conn.OpenAsync();

                // 讀取資料表基本資訊
                using (var cmd = new NpgsqlCommand(@"
                    SELECT 
                        c.relname AS table_name,
                        n.nspname AS schema_name,
                        obj_description(c.oid) AS table_comment
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relkind = 'r'
                    AND n.nspname = @schema
                    ORDER BY c.relname", conn))
                {
                    cmd.Parameters.AddWithValue("schema", _schema);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tables.Add(new TableDefinition
                            {
                                TableName = reader["table_name"].ToString(),
                                SchemaName = reader["schema_name"].ToString(),
                                Comment = reader["table_comment"]?.ToString(),
                                Columns = new List<ColumnDefinition>(),
                                ForeignKeys = new List<ForeignKeyDefinition>(),
                                Indexes = new List<IndexDefinition>()
                            });
                        }
                    }
                }

                // Implement batch processing for reading tables and their details
                var batchSize = 10;
                var tableBatches = tables.Select((table, index) => new { table, index })
                                         .GroupBy(x => x.index / batchSize)
                                         .Select(g => g.Select(x => x.table).ToList())
                                         .ToList();

                var tasks = tableBatches.Select(batch => Task.Run(async () =>
                {
                    using (var batchConn = await _connectionPool.GetConnectionAsync(_connectionString))
                    {
                        await batchConn.OpenAsync();
                        foreach (var table in batch)
                        {
                            await ReadColumns(batchConn, table);
                            await ReadPrimaryKey(batchConn, table);
                            await ReadForeignKeys(batchConn, table);
                            await ReadIndexes(batchConn, table);
                            UpdateOneToOneRelationships(table);
                        }
                    }
                }));

                await Task.WhenAll(tasks);
            }

            return tables;
        }

        /// <summary>
        /// 讀取表格的欄位定義。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">要讀取的表格定義</param>
        private async Task ReadColumns(NpgsqlConnection conn, TableDefinition table)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT 
                    a.attname AS column_name,
                    pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                    NOT a.attnotnull AS is_nullable,
                    col_description(a.attrelid, a.attnum) AS column_comment,
                    pg_get_expr(d.adbin, d.adrelid) AS column_default,
                    a.attidentity != '' AS is_identity,
                    a.attgenerated != '' AS is_generated
                FROM pg_catalog.pg_attribute a
                LEFT JOIN pg_catalog.pg_attrdef d ON (a.attrelid, a.attnum) = (d.adrelid, d.adnum)
                WHERE a.attrelid = (
                    SELECT c.oid 
                    FROM pg_catalog.pg_class c
                    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = @tableName 
                    AND n.nspname = @schema
                )
                AND a.attnum > 0 
                AND NOT a.attisdropped
                ORDER BY a.attnum", conn))
            {
                cmd.Parameters.AddWithValue("tableName", table.TableName);
                cmd.Parameters.AddWithValue("schema", table.SchemaName);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var column = new ColumnDefinition
                        {
                            ColumnName = reader["column_name"].ToString(),
                            DataType = MapPostgreSqlType(reader["data_type"].ToString()),
                            IsNullable = Convert.ToBoolean(reader["is_nullable"]),
                            Comment = reader["column_comment"]?.ToString(),
                            DefaultValue = reader["column_default"]?.ToString(),
                            IsIdentity = Convert.ToBoolean(reader["is_identity"]),
                            IsComputed = Convert.ToBoolean(reader["is_generated"])
                        };

                        // 解析資料類型的長度、精確度等資訊
                        ParseDataTypeAttributes(column);

                        table.Columns.Add(column);
                    }
                }
            }
        }

        /// <summary>
        /// 讀取表格的主鍵資訊。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">要讀取的表格定義</param>
        private async Task ReadPrimaryKey(NpgsqlConnection conn, TableDefinition table)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT a.attname AS column_name
                FROM pg_index i
                JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
                WHERE i.indrelid = (
                    SELECT c.oid 
                    FROM pg_catalog.pg_class c
                    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = @tableName 
                    AND n.nspname = @schema
                )
                AND i.indisprimary", conn))
            {
                cmd.Parameters.AddWithValue("tableName", table.TableName);
                cmd.Parameters.AddWithValue("schema", table.SchemaName);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader["column_name"].ToString();
                        var column = table.Columns.First(c => c.ColumnName == columnName);
                        column.IsPrimaryKey = true;
                    }
                }
            }
        }

        /// <summary>
        /// 讀取表格的外鍵資訊。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">要讀取的表格定義</param>
        private async Task ReadForeignKeys(NpgsqlConnection conn, TableDefinition table)
        {
            if (_foreignKeyCache.TryGetValue(table.TableName, out var cachedForeignKeys))
            {
                table.ForeignKeys = cachedForeignKeys;
                return;
            }

            using (var cmd = new NpgsqlCommand(@"
                SELECT
                    c.conname AS constraint_name,
                    tf.relname AS foreign_table,
                    STRING_AGG(kcu.column_name, ',') AS fk_columns,
                    STRING_AGG(ccu.column_name, ',') AS pk_columns,
                    CASE c.confupdtype
                        WHEN 'a' THEN 'NO ACTION'
                        WHEN 'r' THEN 'RESTRICT'
                        WHEN 'c' THEN 'CASCADE'
                        WHEN 'n' THEN 'SET NULL'
                        WHEN 'd' THEN 'SET DEFAULT'
                    END AS update_rule,
                    CASE c.confdeltype
                        WHEN 'a' THEN 'NO ACTION'
                        WHEN 'r' THEN 'RESTRICT'
                        WHEN 'c' THEN 'CASCADE'
                        WHEN 'n' THEN 'SET NULL'
                        WHEN 'd' THEN 'SET DEFAULT'
                    END AS delete_rule
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_class tf ON tf.oid = c.confrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                JOIN information_schema.key_column_usage kcu 
                    ON kcu.constraint_name = c.conname 
                    AND kcu.table_schema = n.nspname
                JOIN information_schema.constraint_column_usage ccu 
                    ON ccu.constraint_name = c.conname 
                    AND ccu.table_schema = n.nspname
                WHERE c.contype = 'f'
                AND t.relname = @tableName
                AND n.nspname = @schema
                GROUP BY c.conname, tf.relname, c.confupdtype, c.confdeltype", conn))
            {
                cmd.Parameters.AddWithValue("tableName", table.TableName);
                cmd.Parameters.AddWithValue("schema", table.SchemaName);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var fkColumns = reader["fk_columns"].ToString().Split(',');
                        var pkColumns = reader["pk_columns"].ToString().Split(',');

                        var fk = new ForeignKeyDefinition
                        {
                            ConstraintName = reader["constraint_name"].ToString(),
                            PrimaryTable = reader["foreign_table"].ToString(),
                            UpdateRule = reader["update_rule"].ToString(),
                            DeleteRule = reader["delete_rule"].ToString(),
                            IsEnabled = true,
                            IsCompositeKey = fkColumns.Length > 1,
                            ColumnPairs = new List<ForeignKeyColumnPair>()
                        };

                        for (int i = 0; i < fkColumns.Length; i++)
                        {
                            fk.ColumnPairs.Add(new ForeignKeyColumnPair
                            {
                                ForeignKeyColumn = fkColumns[i],
                                PrimaryKeyColumn = pkColumns[i]
                            });
                        }

                        if (fk.ColumnPairs.Any())
                        {
                            fk.ForeignKeyColumn = fk.ColumnPairs[0].ForeignKeyColumn;
                            fk.PrimaryKeyColumn = fk.ColumnPairs[0].PrimaryKeyColumn;
                        }

                        table.ForeignKeys.Add(fk);
                    }
                }
            }

            _foreignKeyCache[table.TableName] = table.ForeignKeys;
        }

        /// <summary>
        /// 讀取表格的索引資訊。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="table">要讀取的表格定義</param>
        private async Task ReadIndexes(NpgsqlConnection conn, TableDefinition table)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT
                    i.relname AS index_name,
                    idx.indisunique AS is_unique,
                    idx.indisprimary AS is_primary,
                    idx.indisvalid AS is_valid,
                    am.amname AS index_type,
                    array_position(idx.indkey, a.attnum) AS key_ordinal,
                    a.attname AS column_name,
                    pg_index_column_has_property(i.oid, k.n, 'desc') AS is_descending,
                    pg_index_column_has_property(i.oid, k.n, 'nulls_first') AS nulls_first
                FROM pg_index idx
                JOIN pg_class i ON i.oid = idx.indexrelid
                JOIN pg_class t ON t.oid = idx.indrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                JOIN pg_am am ON am.oid = i.relam
                JOIN pg_attribute a ON a.attrelid = t.oid
                CROSS JOIN generate_series(1, (
                    SELECT COUNT(*) 
                    FROM pg_index_column_has_property(i.oid, k.n, 'orderable') p 
                    WHERE p
                )) k(n)
                WHERE t.relname = @tableName
                AND n.nspname = @schema
                AND a.attnum = ANY(idx.indkey)
                ORDER BY i.relname, key_ordinal", conn))
            {
                cmd.Parameters.AddWithValue("tableName", table.TableName);
                cmd.Parameters.AddWithValue("schema", table.SchemaName);

                IndexDefinition currentIndex = null;
                string currentIndexName = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var indexName = reader["index_name"].ToString();

                        if (indexName != currentIndexName)
                        {
                            currentIndex = new IndexDefinition
                            {
                                IndexName = indexName,
                                IsUnique = Convert.ToBoolean(reader["is_unique"]),
                                IsPrimaryKey = Convert.ToBoolean(reader["is_primary"]),
                                IsDisabled = !Convert.ToBoolean(reader["is_valid"]),
                                IndexType = reader["index_type"].ToString(),
                                Columns = new List<IndexColumnDefinition>()
                            };

                            table.Indexes.Add(currentIndex);
                            currentIndexName = indexName;
                        }

                        currentIndex.Columns.Add(new IndexColumnDefinition
                        {
                            ColumnName = reader["column_name"].ToString(),
                            IsDescending = Convert.ToBoolean(reader["is_descending"]),
                            KeyOrdinal = Convert.ToInt32(reader["key_ordinal"]),
                            IsIncluded = false // PostgreSQL 不支援包含的欄位概念
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 將 PostgreSQL 的資料類型轉換為標準資料類型。
        /// </summary>
        /// <param name="postgresType">PostgreSQL 資料類型</param>
        /// <returns>標準化的資料類型名稱</returns>
        private string MapPostgreSqlType(string postgresType)
        {
            return postgresType.ToLower() switch
            {
                var t when t.StartsWith("character varying") => "string",
                var t when t.StartsWith("character") => "string",
                var t when t.StartsWith("varchar") => "string",
                var t when t.StartsWith("text") => "string",
                "boolean" => "bool",
                "smallint" => "short",
                "integer" => "int",
                "bigint" => "long",
                "real" => "float",
                "double precision" => "double",
                var t when t.StartsWith("numeric") => "decimal",
                var t when t.StartsWith("decimal") => "decimal",
                "money" => "decimal",
                "timestamp without time zone" => "DateTime",
                "timestamp with time zone" => "DateTimeOffset",
                "date" => "DateTime",
                "time without time zone" => "TimeSpan",
                "time with time zone" => "DateTimeOffset",
                "interval" => "TimeSpan",
                "uuid" => "Guid",
                "bytea" => "byte[]",
                "json" => "string",
                "jsonb" => "string",
                "xml" => "string",
                _ => "string"  // 預設使用字串類型
            };
        }

        /// <summary>
        /// 解析 PostgreSQL 資料類型的相關屬性。
        /// </summary>
        /// <param name="column">要解析的欄位定義</param>
        /// <remarks>
        /// 處理以下資料類型的屬性：
        /// - 字串類型的長度限制
        /// - 數值類型的精確度與小數位數
        /// - 時間類型的精確度
        /// - 陣列類型的維度
        /// </remarks>
        private void ParseDataTypeAttributes(ColumnDefinition column)
        {
            try
            {
                var dataType = column.DataType.ToLower();

                // 解析字串類型的長度
                if (dataType.Contains("char") || dataType.Contains("varchar"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(dataType, @"\((\d+)\)");
                    if (match.Success)
                    {
                        column.MaxLength = Convert.ToInt64(match.Groups[1].Value);
                    }
                }
                // 解析數值類型的精確度與小數位數
                else if (dataType.StartsWith("numeric") || dataType.StartsWith("decimal"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(dataType, @"\((\d+),(\d+)\)");
                    if (match.Success)
                    {
                        column.Precision = Convert.ToInt32(match.Groups[1].Value);
                        column.Scale = Convert.ToInt32(match.Groups[2].Value);
                    }
                }
                // 解析時間戳類型的精確度
                else if (dataType.StartsWith("timestamp") || dataType.StartsWith("time"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(dataType, @"\((\d+)\)");
                    if (match.Success)
                    {
                        column.Precision = Convert.ToInt32(match.Groups[1].Value);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析資料類型屬性時發生錯誤: {column.DataType}", ex);
            }
        }

        /// <summary>
        /// 檢查欄位是否為計算欄位。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="tableName">表格名稱</param>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>如果是計算欄位則返回 true，否則返回 false</returns>
        private bool IsComputedColumn(NpgsqlConnection conn, string tableName, string columnName)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT pg_get_expr(d.adbin, d.adrelid) AS definition
                FROM pg_catalog.pg_attribute a
                JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                LEFT JOIN pg_catalog.pg_attrdef d ON (a.attrelid, a.attnum) = (d.adrelid, d.adnum)
                WHERE c.relname = @tableName
                AND n.nspname = @schema
                AND a.attname = @columnName
                AND a.attgenerated != ''", conn))
            {
                cmd.Parameters.AddWithValue("tableName", tableName);
                cmd.Parameters.AddWithValue("schema", _schema);
                cmd.Parameters.AddWithValue("columnName", columnName);

                var result = cmd.ExecuteScalar();
                return result != null;
            }
        }

        /// <summary>
        /// 檢查欄位是否為序列。
        /// </summary>
        /// <param name="conn">資料庫連線</param>
        /// <param name="tableName">表格名稱</param>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>如果是序列則返回 true，否則返回 false</returns>
        private bool IsSequenceColumn(NpgsqlConnection conn, string tableName, string columnName)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_attribute a
                    JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
                    JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = @tableName
                    AND n.nspname = @schema
                    AND a.attname = @columnName
                    AND a.attidentity != ''
                )", conn))
            {
                cmd.Parameters.AddWithValue("tableName", tableName);
                cmd.Parameters.AddWithValue("schema", _schema);
                cmd.Parameters.AddWithValue("columnName", columnName);

                return Convert.ToBoolean(cmd.ExecuteScalar());
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
    }

    public class NpgsqlConnectionPool
    {
        private readonly Dictionary<string, NpgsqlConnection> _pool = new Dictionary<string, NpgsqlConnection>();

        public async Task<NpgsqlConnection> GetConnectionAsync(string connectionString)
        {
            if (!_pool.ContainsKey(connectionString))
            {
                var connection = new NpgsqlConnection(connectionString);
                _pool[connectionString] = connection;
            }

            return _pool[connectionString];
        }
    }
}
