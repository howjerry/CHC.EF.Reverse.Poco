using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace CHC.EF.Reverse.Poco.Infrastructure.Databases
{
    public class SqlServerSchemaReader : IDatabaseSchemaReader
    {
        private readonly string _connectionString;
        private readonly SqlConnectionPool _connectionPool;
        private readonly ILogger _logger;

        public SqlServerSchemaReader(string connectionString, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 驗證連線字串設定
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "連線字串不可為空");
            }

            // 檢查連線字串是否包含 MultipleActiveResultSets 設定
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!builder.MultipleActiveResultSets)
            {
                _logger.Warning("連線字串缺少 MultipleActiveResultSets=True 設定，這可能會導致執行錯誤");
                _logger.Info("正在自動加入 MultipleActiveResultSets=True 設定");

                builder.MultipleActiveResultSets = true;
                connectionString = builder.ConnectionString;
            }

            _connectionString = connectionString;
            _connectionPool = new SqlConnectionPool();
        }

        public async Task<List<TableDefinition>> ReadTables()
        {
            var tables = new List<TableDefinition>();

            using (var pooledConnection = await _connectionPool.GetConnectionAsync(_connectionString))
            {

                var conn = ((SqlConnectionPool.PooledConnection)pooledConnection).Connection;

                // 讀取資料表及其描述
                using (var cmd = new SqlCommand(@"
                    SELECT 
                        t.name AS TableName,
                        s.name AS SchemaName,
                        ep.value AS TableComment
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    LEFT JOIN sys.extended_properties ep ON 
                        ep.major_id = t.object_id AND 
                        ep.minor_id = 0 AND 
                        ep.name = 'MS_Description'", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tables.Add(new TableDefinition
                            {
                                TableName = reader["TableName"].ToString(),
                                SchemaName = reader["SchemaName"].ToString(),
                                Comment = reader["TableComment"]?.ToString()
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
                        var conn = ((SqlConnectionPool.PooledConnection)pooledConnection).Connection;

                        foreach (var table in batch)
                        {
                            await ReadColumns(conn, table);
                            await ReadIndexes(conn, table);
                            await ReadForeignKeys(conn, table);
                            UpdateOneToOneRelationships(table);
                        }
                    }
                }));

                await Task.WhenAll(tasks);
            }

            return tables;
        }

        private async Task ReadColumns(SqlConnection conn, TableDefinition table)
        {
            using (var cmd = new SqlCommand(@"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.is_nullable AS IsNullable,
                    c.max_length AS MaxLength,
                    c.precision AS Precision,
                    c.scale AS Scale,
                    CAST(CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsPrimaryKey,
                    c.is_identity AS IsIdentity,
                    c.is_computed AS IsComputed,
                    c.collation_name AS CollationType,
                    cc.definition AS ComputedDefinition,
                    COLUMNPROPERTY(c.object_id, c.name, 'IsRowVersion') AS IsRowVersion,
                    ep.value AS Comment,
                    dc.definition AS DefaultDefinition,
                    CASE WHEN c.generated_always_type > 0 THEN 'ALWAYS' 
                         WHEN c.is_computed = 1 THEN 'COMPUTED'
                         ELSE NULL END AS GeneratedType
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN sys.extended_properties ep ON 
                    ep.major_id = c.object_id AND 
                    ep.minor_id = c.column_id AND
                    ep.name = 'MS_Description'
                LEFT JOIN sys.computed_columns cc ON 
                    cc.object_id = c.object_id AND 
                    cc.column_id = c.column_id
                LEFT JOIN sys.default_constraints dc ON 
                    dc.parent_object_id = c.object_id AND 
                    dc.parent_column_id = c.column_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id
                    FROM sys.index_columns ic
                    JOIN sys.indexes i ON 
                        i.object_id = ic.object_id AND 
                        i.index_id = ic.index_id
                    WHERE i.is_primary_key = 1
                ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
                WHERE c.object_id = OBJECT_ID(@tableName)
                ORDER BY c.column_id", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", $"{table.SchemaName}.{table.TableName}");

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        table.Columns.Add(new ColumnDefinition
                        {
                            ColumnName = reader["ColumnName"].ToString(),
                            DataType = reader["DataType"].ToString(),
                            IsNullable = Convert.ToBoolean(reader["IsNullable"]),
                            MaxLength = reader["MaxLength"] != DBNull.Value ? Convert.ToInt32(reader["MaxLength"]) : null,
                            Precision = reader["Precision"] != DBNull.Value ? Convert.ToInt32(reader["Precision"]) : null,
                            Scale = reader["Scale"] != DBNull.Value ? Convert.ToInt32(reader["Scale"]) : null,
                            IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
                            IsIdentity = Convert.ToBoolean(reader["IsIdentity"]),
                            IsComputed = Convert.ToBoolean(reader["IsComputed"]),
                            CollationType = reader["CollationType"].ToString(),
                            IsRowVersion = reader["IsRowVersion"] != DBNull.Value
                            ?
                                Convert.ToBoolean(reader["IsRowVersion"])
                            :
                                false,
                            GeneratedType = reader["GeneratedType"].ToString(),
                            ComputedColumnDefinition = reader["ComputedDefinition"].ToString(),
                            Comment = reader["Comment"].ToString(),
                            DefaultValue = reader["DefaultDefinition"].ToString()
                        });
                    }
                }
            }
        }

        private async Task ReadIndexes(SqlConnection conn, TableDefinition table)
        {
            using (var cmd = new SqlCommand(@"
    SELECT 
        i.name AS IndexName,
        i.is_unique AS IsUnique,
        i.is_primary_key AS IsPrimaryKey,
        i.is_disabled AS IsDisabled,
        i.type_desc AS IndexType,
        i.is_padded AS IsPadded,
        ic.key_ordinal AS KeyOrdinal,
        ic.is_descending_key AS IsDescending,
        ic.is_included_column AS IsIncluded,
        c.name AS ColumnName
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON 
        i.object_id = ic.object_id AND 
        i.index_id = ic.index_id
    INNER JOIN sys.columns c ON 
        ic.object_id = c.object_id AND 
        ic.column_id = c.column_id
    WHERE i.object_id = OBJECT_ID(@tableName)", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", $"{table.SchemaName}.{table.TableName}");

                var currentIndexName = string.Empty;
                IndexDefinition currentIndex = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var indexName = reader["IndexName"].ToString();

                        if (indexName != currentIndexName)
                        {
                            currentIndex = new IndexDefinition
                            {
                                IndexName = indexName,
                                IsUnique = Convert.ToBoolean(reader["IsUnique"]),
                                IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
                                IsDisabled = Convert.ToBoolean(reader["IsDisabled"]),
                                IndexType = reader["IndexType"].ToString(),
                                Columns = new List<IndexColumnDefinition>()
                            };

                            table.Indexes.Add(currentIndex);
                            currentIndexName = indexName;
                        }

                        currentIndex.Columns.Add(new IndexColumnDefinition
                        {
                            ColumnName = reader["ColumnName"].ToString(),
                            IsDescending = Convert.ToBoolean(reader["IsDescending"]),
                            KeyOrdinal = Convert.ToInt32(reader["KeyOrdinal"]),
                            IsIncluded = Convert.ToBoolean(reader["IsIncluded"])
                        });
                    }
                }
            }
        }

        private async Task ReadForeignKeys(SqlConnection conn, TableDefinition table)
        {
            using (var cmd = new SqlCommand(@"
            SELECT 
                fk.name AS ConstraintName,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS PrimaryTableSchema,
                OBJECT_NAME(fk.referenced_object_id) AS PrimaryTableName,
                fk.is_disabled AS IsDisabled,
                fk.is_not_for_replication AS IsNotForReplication,
                fk.delete_referential_action_desc AS DeleteRule,
                fk.update_referential_action_desc AS UpdateRule,
                c1.name AS ForeignKeyColumn,
                c2.name AS PrimaryKeyColumn,
                fkc.constraint_column_id AS ColumnOrder
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON 
                fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.columns c1 ON 
                fkc.parent_object_id = c1.object_id AND 
                fkc.parent_column_id = c1.column_id
            INNER JOIN sys.columns c2 ON 
                fkc.referenced_object_id = c2.object_id AND 
                fkc.referenced_column_id = c2.column_id
            WHERE fk.parent_object_id = OBJECT_ID(@tableName)
            ORDER BY fk.name, fkc.constraint_column_id", conn))
            {
                cmd.Parameters.AddWithValue("@tableName", $"{table.SchemaName}.{table.TableName}");

                var currentFkName = string.Empty;
                ForeignKeyDefinition currentFk = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var constraintName = reader["ConstraintName"].ToString();

                        if (constraintName != currentFkName)
                        {
                            currentFk = new ForeignKeyDefinition
                            {
                                ConstraintName = constraintName,
                                PrimaryTable = reader["PrimaryTableName"].ToString(),
                                DeleteRule = reader["DeleteRule"].ToString(),
                                UpdateRule = reader["UpdateRule"].ToString(),
                                IsEnabled = !Convert.ToBoolean(reader["IsDisabled"]),
                                IsNotForReplication = Convert.ToBoolean(reader["IsNotForReplication"]),
                                ColumnPairs = new List<ForeignKeyColumnPair>()
                            };

                            table.ForeignKeys.Add(currentFk);
                            currentFkName = constraintName;
                        }

                        currentFk.ColumnPairs.Add(new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = reader["ForeignKeyColumn"].ToString(),
                            PrimaryKeyColumn = reader["PrimaryKeyColumn"].ToString()
                        });

                        // 設置第一個外鍵列為主要外鍵列
                        if (currentFk.ColumnPairs.Count == 1)
                        {
                            currentFk.ForeignKeyColumn = reader["ForeignKeyColumn"].ToString();
                            currentFk.PrimaryKeyColumn = reader["PrimaryKeyColumn"].ToString();
                        }

                        currentFk.IsCompositeKey = currentFk.ColumnPairs.Count > 1;
                    }
                }
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

                // 更新外鍵關係的註解以標記一對一關係
                if (hasUniqueConstraint)
                {
                    fk.Comment = (fk.Comment ?? "") + " [One-to-One Relationship]";
                }
            }
        }
    }
}
