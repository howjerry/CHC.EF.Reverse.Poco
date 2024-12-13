using System.Collections.Generic;

using System;
using System.Linq;
using CHC.EF.Reverse.Poco.Exceptions;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;

/// <summary>
/// 提供資料庫表格間關聯關係的分析功能。
/// </summary>
/// <remarks>
/// 支援分析以下類型的關聯：
/// - 一對一 (One-to-One)
/// - 一對多 (One-to-Many)
/// - 多對多 (Many-to-Many)
/// 
/// 分析過程會考慮：
/// - 主鍵配置
/// - 外鍵約束
/// - 唯一索引
/// - 可為空性
/// </remarks>
/// <summary>
/// 提供資料庫表格間關聯關係的分析功能。
/// </summary>
public class RelationshipAnalyzer
{
    private readonly ILogger _logger;
    private readonly RelationshipValidationService _validationService;
    private readonly RelationshipTypeResolver _typeResolver;

    public RelationshipAnalyzer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validationService = new RelationshipValidationService(logger);
        _typeResolver = new RelationshipTypeResolver(logger);
    }
    #region MyRegion
    /// <summary>
    /// 判定資料表間的關聯類型。
    /// </summary>
    /// <param name="sourceTable">來源資料表定義</param>
    /// <param name="targetTable">目標資料表定義</param>
    /// <param name="foreignKey">外鍵定義</param>
    /// <returns>資料表間的關聯類型</returns>
    /// <remarks>
    /// 此方法根據以下規則判定關聯類型：
    /// 1. 檢查是否為中介表格（多對多關聯）
    /// 2. 驗證唯一性約束（一對一關聯）
    /// 3. 預設為一對多關聯
    /// </remarks>
    private RelationType DetermineRelationType(
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ForeignKeyDefinition foreignKey)
    {
        try
        {
            _logger.Info($"開始分析關聯類型: {sourceTable.TableName} -> {targetTable.TableName}");

            // 檢查是否為多對多關聯的中介表格
            if (IsJunctionTable(sourceTable))
            {
                _logger.Info($"表格 {sourceTable.TableName} 被識別為中介表格");
                return RelationType.ManyToMany;
            }

            // 檢查是否為一對一關聯
            if (HasUniqueConstraint(sourceTable, foreignKey))
            {
                _logger.Info($"在表格 {sourceTable.TableName} 中發現唯一性約束，判定為一對一關聯");
                return RelationType.OneToOne;
            }

            // 預設為一對多關聯
            _logger.Info($"關聯類型判定為一對多: {sourceTable.TableName} -> {targetTable.TableName}");
            return RelationType.OneToMany;
        }
        catch (Exception ex)
        {
            _logger.Error($"判定關聯類型時發生錯誤: {ex.Message}", ex);
            return RelationType.Unknown;
        }
    }

    /// <summary>
    /// 建立中介表格的相關資訊。
    /// </summary>
    /// <param name="junctionTable">中介表格定義</param>
    /// <returns>中介表格資訊物件，如果不是中介表格則返回 null</returns>
    /// <remarks>
    /// 此方法處理多對多關聯中介表格的資訊，包括：
    /// 1. 收集關聯鍵欄位
    /// 2. 識別額外屬性欄位
    /// 3. 建立完整的中介表格描述
    /// </remarks>
    private JunctionTableInfo CreateJunctionTableInfo(TableDefinition junctionTable)
    {
        if (!IsJunctionTable(junctionTable))
        {
            _logger.Info($"表格 {junctionTable.TableName} 不是中介表格");
            return null;
        }

        try
        {
            _logger.Info($"開始建立中介表格資訊: {junctionTable.TableName}");

            // 收集所有外鍵欄位
            var foreignKeyColumns = junctionTable.ForeignKeys
                .SelectMany(fk => fk.ColumnPairs)
                .Select(cp => cp.ForeignKeyColumn)
                .ToList();

            // 識別額外的非關聯欄位
            var additionalColumns = junctionTable.Columns
                .Where(c => !foreignKeyColumns.Contains(c.ColumnName))
                .ToList();

            var junctionInfo = new JunctionTableInfo
            {
                TableName = junctionTable.TableName,
                SourceKeyColumns = foreignKeyColumns,
                AdditionalColumns = additionalColumns
            };

            _logger.Info($"已建立中介表格資訊，包含 {foreignKeyColumns.Count} 個關聯鍵和 {additionalColumns.Count} 個額外欄位");
            return junctionInfo;
        }
        catch (Exception ex)
        {
            _logger.Error($"建立中介表格資訊時發生錯誤: {ex.Message}", ex);
            throw new RelationshipAnalysisException(
                $"無法建立表格 {junctionTable.TableName} 的中介表格資訊", ex);
        }
    }

    /// <summary>
    /// 判斷指定的表格是否為中介表格。
    /// </summary>
    /// <param name="table">要判斷的表格定義</param>
    /// <returns>如果是中介表格則返回 true，否則返回 false</returns>
    /// <remarks>
    /// 判斷依據：
    /// 1. 具有至少兩個外鍵關係
    /// 2. 外鍵關係指向不同的表格
    /// 3. 主鍵由外鍵欄位組成
    /// 4. 沒有過多的額外欄位
    /// </remarks>
    private bool IsJunctionTable(TableDefinition table)
    {
        if (table == null || !table.ForeignKeys.Any())
            return false;

        try
        {
            // 檢查是否有足夠的外鍵關係
            var distinctForeignKeys = table.ForeignKeys
                .Select(fk => fk.PrimaryTable)
                .Distinct()
                .Count();

            if (distinctForeignKeys < 2)
                return false;

            // 檢查主鍵組成
            var primaryKeyColumns = table.Columns
                .Where(c => c.IsPrimaryKey)
                .Select(c => c.ColumnName)
                .ToList();

            if (primaryKeyColumns.Count < 2)
                return false;

            // 檢查主鍵是否由外鍵組成
            var foreignKeyColumns = table.ForeignKeys
                .SelectMany(fk => fk.ColumnPairs)
                .Select(cp => cp.ForeignKeyColumn)
                .ToList();

            var allPrimaryKeysAreForeignKeys = primaryKeyColumns
                .All(pk => foreignKeyColumns.Contains(pk));

            if (!allPrimaryKeysAreForeignKeys)
                return false;

            // 檢查額外欄位數量
            var nonKeyColumnCount = table.Columns.Count - primaryKeyColumns.Count;
            const int MaxAdditionalColumns = 3;

            return nonKeyColumnCount <= MaxAdditionalColumns;
        }
        catch (Exception ex)
        {
            _logger.Error($"判斷中介表格時發生錯誤: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// 檢查指定的外鍵是否具有唯一性約束。
    /// </summary>
    /// <param name="table">表格定義</param>
    /// <param name="foreignKey">外鍵定義</param>
    /// <returns>如果具有唯一性約束則返回 true，否則返回 false</returns>
    private bool HasUniqueConstraint(TableDefinition table, ForeignKeyDefinition foreignKey)
    {
        try
        {
            // 檢查是否存在只包含外鍵欄位的唯一索引
            var uniqueIndexExists = table.Indexes
                .Where(idx => idx.IsUnique && !idx.IsPrimaryKey)
                .Any(idx =>
                {
                    var indexColumns = idx.Columns
                        .Select(c => c.ColumnName)
                        .ToList();

                    var foreignKeyColumns = foreignKey.ColumnPairs
                        .Select(cp => cp.ForeignKeyColumn)
                        .ToList();

                    return indexColumns.Count == foreignKeyColumns.Count &&
                           foreignKeyColumns.All(fk => indexColumns.Contains(fk));
                });

            return uniqueIndexExists;
        }
        catch (Exception ex)
        {
            _logger.Error($"檢查唯一性約束時發生錯誤: {ex.Message}", ex);
            return false;
        }
    }
    #endregion
    /// <summary>
    /// 分析兩個資料表之間的關聯類型。
    /// </summary>
    /// <param name="sourceTable">來源資料表定義</param>
    /// <param name="targetTable">目標資料表定義</param>
    /// <returns>關聯類型定義</returns>
    /// <exception cref="ArgumentNullException">當任一表格參數為 null 時擲回</exception>
    /// <exception cref="RelationshipAnalysisException">當分析過程發生錯誤時擲回</exception>
    public RelationshipType AnalyzeRelationship(TableDefinition sourceTable, TableDefinition targetTable)
    {
        try
        {
            ValidateParameters(sourceTable, targetTable);

            var foreignKeys = GetValidForeignKeys(sourceTable, targetTable);
            if (!foreignKeys.Any())
            {
                _logger.Warning($"找不到從 {sourceTable.TableName} 到 {targetTable.TableName} 的有效外鍵");
                return CreateUnknownRelationship(sourceTable.TableName, targetTable.TableName);
            }

            var primaryForeignKey = foreignKeys.First();
            var relationType = DetermineRelationType(sourceTable, targetTable, primaryForeignKey);

            // 根據關聯類型決定來源和目標表格
            string sourceTableName, targetTableName;
            switch (relationType)
            {
                case RelationType.OneToOne:
                    // 一對一關聯中，持有外鍵的表格為來源表格
                    sourceTableName = sourceTable.TableName;
                    targetTableName = targetTable.TableName;
                    break;

                case RelationType.OneToMany:
                    // 一對多關聯中，父表格（被參考的表格）為來源表格
                    sourceTableName = targetTable.TableName;
                    targetTableName = sourceTable.TableName;
                    break;

                case RelationType.ManyToMany:
                    // 多對多關聯中，來源為第一個外鍵指向的表格
                    sourceTableName = primaryForeignKey.PrimaryTable;
                    targetTableName = sourceTable.TableName;
                    break;

                default:
                    sourceTableName = sourceTable.TableName;
                    targetTableName = targetTable.TableName;
                    break;
            }

            return new RelationshipType
            {
                Type = relationType,
                SourceTable = sourceTableName,
                TargetTable = targetTableName,
                ForeignKeyColumns = MapForeignKeyColumns(primaryForeignKey),
                JunctionTableInfo = relationType == RelationType.ManyToMany
                    ? CreateJunctionTableInfo(sourceTable)
                    : null
            };
        }
        catch (Exception ex) when (ex is not RelationshipAnalysisException)
        {
            var message = $"分析關聯時發生錯誤: {sourceTable?.TableName} -> {targetTable?.TableName}";
            _logger.Error(message, ex);
            throw new RelationshipAnalysisException(message, ex);
        }
    }

    /// <summary>
    /// 映射外鍵欄位資訊。
    /// </summary>
    /// <param name="foreignKey">外鍵定義</param>
    /// <returns>外鍵欄位資訊的集合</returns>
    /// <remarks>
    /// 處理以下情況：
    /// - 一般單一外鍵
    /// - 複合外鍵
    /// - 包含刪除和更新規則的外鍵
    /// </remarks>
    private List<ForeignKeyInfo> MapForeignKeyColumns(ForeignKeyDefinition foreignKey)
    {
        if (foreignKey == null) return new List<ForeignKeyInfo>();

        _logger.Info($"開始映射外鍵: {foreignKey.ConstraintName}, IsCompositeKey: {foreignKey.IsCompositeKey}");

        // 如果是複合外鍵，從 ColumnPairs 取得所有欄位對應
        if (foreignKey.IsCompositeKey && foreignKey.ColumnPairs?.Any() == true)
        {
            var mappedColumns = foreignKey.ColumnPairs.Select(pair => new ForeignKeyInfo
            {
                ForeignKeyColumn = pair.ForeignKeyColumn,
                PrimaryKeyColumn = pair.PrimaryKeyColumn,
                DeleteRule = foreignKey.DeleteRule,
                UpdateRule = foreignKey.UpdateRule
            }).ToList();

            _logger.Info($"映射了 {mappedColumns.Count} 個複合外鍵欄位");
            return mappedColumns;
        }

        // 一般單一外鍵
        var singleColumn = new ForeignKeyInfo
        {
            ForeignKeyColumn = foreignKey.ForeignKeyColumn,
            PrimaryKeyColumn = foreignKey.PrimaryKeyColumn,
            DeleteRule = foreignKey.DeleteRule,
            UpdateRule = foreignKey.UpdateRule
        };

        return new List<ForeignKeyInfo> { singleColumn };
    }

    /// <summary>
    /// 驗證分析所需的參數。
    /// </summary>
    private void ValidateParameters(TableDefinition sourceTable, TableDefinition targetTable)
    {
        if (sourceTable == null)
            throw new ArgumentNullException(nameof(sourceTable), "來源資料表不可為 null");

        if (targetTable == null)
            throw new ArgumentNullException(nameof(targetTable), "目標資料表不可為 null");
    }


    private IReadOnlyList<ForeignKeyDefinition> GetValidForeignKeys(TableDefinition sourceTable, TableDefinition targetTable)
    {
        return sourceTable.ForeignKeys
            .Where(fk =>
                fk.PrimaryTable == targetTable.TableName &&
                fk.IsEnabled &&
                IsValidForeignKey(fk))
            .ToList();
    }

    /// <summary>
    /// 檢查外鍵定義是否有效。
    /// </summary>
    private bool IsValidForeignKey(ForeignKeyDefinition foreignKey)
    {
        if (foreignKey.IsCompositeKey)
        {
            return foreignKey.ColumnPairs != null &&
                   foreignKey.ColumnPairs.Any() &&
                   foreignKey.ColumnPairs.All(cp =>
                       !string.IsNullOrEmpty(cp.ForeignKeyColumn) &&
                       !string.IsNullOrEmpty(cp.PrimaryKeyColumn));
        }

        return !string.IsNullOrEmpty(foreignKey.ForeignKeyColumn) &&
               !string.IsNullOrEmpty(foreignKey.PrimaryKeyColumn);
    }
    private RelationshipType CreateUnknownRelationship(string sourceTable, string targetTable)
    {
        _logger.Warning($"在資料表 {sourceTable} 和 {targetTable} 之間找不到有效的關聯");
        return new RelationshipType { Type = RelationType.Unknown };
    }
}

/// <summary>
/// 提供關聯分析的參數驗證服務。
/// </summary>
public class RelationshipValidationService
{
    private readonly ILogger _logger;

    public RelationshipValidationService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ValidateInputTables(TableDefinition sourceTable, TableDefinition targetTable)
    {
        if (sourceTable == null)
        {
            throw new ArgumentNullException(nameof(sourceTable), "來源資料表不可為 null");
        }

        if (targetTable == null)
        {
            throw new ArgumentNullException(nameof(targetTable), "目標資料表不可為 null");
        }

        ValidateTableDefinition(sourceTable, "來源");
        ValidateTableDefinition(targetTable, "目標");
    }

    private void ValidateTableDefinition(TableDefinition table, string tableType)
    {
        if (string.IsNullOrEmpty(table.TableName))
        {
            throw new ArgumentException($"{tableType}資料表名稱不可為空", nameof(table));
        }

        if (table.Columns == null || !table.Columns.Any())
        {
            _logger.Warning($"{tableType}資料表 {table.TableName} 沒有任何欄位定義");
        }
    }
}

/// <summary>
/// 負責解析資料表間的關聯類型。
/// </summary>
public class RelationshipTypeResolver
{
    private readonly ILogger _logger;

    public RelationshipTypeResolver(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RelationshipType ResolveRelationshipType(
        TableDefinition sourceTable,
        TableDefinition targetTable,
        IReadOnlyList<ForeignKeyDefinition> foreignKeys)
    {
        // 檢查是否為中介表
        if (IsJunctionTable(sourceTable, foreignKeys))
        {
            return CreateManyToManyRelationship(sourceTable, targetTable);
        }

        // 分析一般關聯類型
        return AnalyzeStandardRelationship(sourceTable, targetTable, foreignKeys);
    }

    private bool IsJunctionTable(TableDefinition table, IReadOnlyList<ForeignKeyDefinition> foreignKeys)
    {
        if (foreignKeys.Count != 2)
        {
            return false;
        }

        var primaryKeyColumns = table.Columns
            .Where(c => c.IsPrimaryKey)
            .Select(c => c.ColumnName)
            .ToList();

        if (primaryKeyColumns.Count != 2)
        {
            return false;
        }

        var foreignKeyColumns = foreignKeys
            .SelectMany(fk => fk.ColumnPairs)
            .Select(cp => cp.ForeignKeyColumn)
            .ToList();

        return primaryKeyColumns.All(pk => foreignKeyColumns.Contains(pk)) &&
               table.Columns.Count <= foreignKeys.Count + GetMaxAdditionalColumns();
    }

    private RelationshipType AnalyzeStandardRelationship(
        TableDefinition sourceTable,
        TableDefinition targetTable,
        IReadOnlyList<ForeignKeyDefinition> foreignKeys)
    {
        var firstForeignKey = foreignKeys.First();
        var sourceKeyColumns = GetSourceKeyColumns(sourceTable, foreignKeys);
        var targetKeyColumns = GetTargetKeyColumns(targetTable, foreignKeys);

        if (IsOneToOneRelationship(sourceKeyColumns, targetKeyColumns))
        {
            return CreateOneToOneRelationship(sourceTable, targetTable, firstForeignKey);
        }

        return CreateOneToManyRelationship(sourceTable, targetTable, firstForeignKey);
    }

    private IReadOnlyList<ColumnDefinition> GetSourceKeyColumns(
        TableDefinition table,
        IReadOnlyList<ForeignKeyDefinition> foreignKeys)
    {
        var foreignKeyColumns = foreignKeys
            .SelectMany(fk => fk.ColumnPairs)
            .Select(cp => cp.ForeignKeyColumn)
            .ToList();

        return table.Columns
            .Where(c => foreignKeyColumns.Contains(c.ColumnName))
            .ToList();
    }

    private IReadOnlyList<ColumnDefinition> GetTargetKeyColumns(
        TableDefinition table,
        IReadOnlyList<ForeignKeyDefinition> foreignKeys)
    {
        var primaryKeyColumns = foreignKeys
            .SelectMany(fk => fk.ColumnPairs)
            .Select(cp => cp.PrimaryKeyColumn)
            .ToList();

        return table.Columns
            .Where(c => primaryKeyColumns.Contains(c.ColumnName))
            .ToList();
    }

    private bool IsOneToOneRelationship(
        IReadOnlyList<ColumnDefinition> sourceColumns,
        IReadOnlyList<ColumnDefinition> targetColumns)
    {
        return sourceColumns.Count == targetColumns.Count &&
               sourceColumns.All(c => c.IsPrimaryKey) &&
               targetColumns.All(c => c.IsPrimaryKey);
    }

    private RelationshipType CreateManyToManyRelationship(
        TableDefinition junctionTable,
        TableDefinition targetTable)
    {
        var foreignKeys = junctionTable.ForeignKeys.ToList();

        return new RelationshipType
        {
            Type = RelationType.ManyToMany,
            SourceTable = foreignKeys[0].PrimaryTable,
            TargetTable = targetTable.TableName,
            JunctionTableInfo = new JunctionTableInfo
            {
                TableName = junctionTable.TableName,
                SourceKeyColumns = foreignKeys
                    .SelectMany(fk => fk.ColumnPairs)
                    .Select(cp => cp.ForeignKeyColumn)
                    .ToList()
            },
            ForeignKeyColumns = MapForeignKeyInfo(foreignKeys)
        };
    }

    private RelationshipType CreateOneToOneRelationship(
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ForeignKeyDefinition foreignKey)
    {
        return new RelationshipType
        {
            Type = RelationType.OneToOne,
            SourceTable = sourceTable.TableName,
            TargetTable = targetTable.TableName,
            ForeignKeyColumns = MapForeignKeyInfo(new[] { foreignKey })
        };
    }

    private RelationshipType CreateOneToManyRelationship(
        TableDefinition sourceTable,
        TableDefinition targetTable,
        ForeignKeyDefinition foreignKey)
    {
        return new RelationshipType
        {
            Type = RelationType.OneToMany,
            SourceTable = foreignKey.PrimaryTable,
            TargetTable = sourceTable.TableName,
            ForeignKeyColumns = MapForeignKeyInfo(new[] { foreignKey })
        };
    }

    private List<ForeignKeyInfo> MapForeignKeyInfo(IEnumerable<ForeignKeyDefinition> foreignKeys)
    {
        return foreignKeys.Select(fk => new ForeignKeyInfo
        {
            ForeignKeyColumn = fk.ForeignKeyColumn,
            PrimaryKeyColumn = fk.PrimaryKeyColumn,
            DeleteRule = fk.DeleteRule,
            UpdateRule = fk.UpdateRule
        }).ToList();
    }

    private int GetMaxAdditionalColumns() => 2;
}

/// <summary>
/// 定義資料庫表格之間可能的關聯類型。
/// </summary>
public enum RelationType
{
    /// <summary>
    /// 未知或無法確定的關聯類型
    /// </summary>
    Unknown,

    /// <summary>
    /// 一對一關聯
    /// </summary>
    OneToOne,

    /// <summary>
    /// 一對多關聯
    /// </summary>
    OneToMany,

    /// <summary>
    /// 多對多關聯
    /// </summary>
    ManyToMany
}
/// <summary>
/// 定義關聯分析的結果類型。
/// </summary>
public class RelationshipType
{
    public RelationType Type { get; set; }
    public string SourceTable { get; set; }
    public string TargetTable { get; set; }
    public List<ForeignKeyInfo> ForeignKeyColumns { get; set; }
    public JunctionTableInfo JunctionTableInfo { get; set; }
}

/// <summary>
/// 定義外鍵資訊。
/// </summary>
public class ForeignKeyInfo
{
    public string ForeignKeyColumn { get; set; }
    public string PrimaryKeyColumn { get; set; }
    public string DeleteRule { get; set; }
    public string UpdateRule { get; set; }
}

/// <summary>
/// 定義多對多關聯中間表的資訊。
/// </summary>
public class JunctionTableInfo
{
    /// <summary>
    /// 取得或設定中間表名稱。
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 取得或設定來源鍵欄位清單。
    /// </summary>
    public List<string> SourceKeyColumns { get; set; }

    /// <summary>
    /// 取得或設定額外欄位定義清單。
    /// </summary>
    public List<ColumnDefinition> AdditionalColumns { get; set; }
}
