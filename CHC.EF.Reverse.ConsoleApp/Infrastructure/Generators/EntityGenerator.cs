using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace CHC.EF.Reverse.Poco.Infrastructure.Generators
{
    /// <summary>
    /// 負責產生實體類別與相關設定的程式碼生成器。
    /// </summary>
    /// <remarks>
    /// 提供以下功能：
    /// 1. 從資料庫結構產生實體類別
    /// 2. 產生實體類別的 Entity Framework 設定
    /// 3. 處理實體間的關聯關係
    /// </remarks>
    public class EntityGenerator
    {
        private readonly Settings _settings;
        private readonly ILogger _logger;
        private readonly List<TableDefinition> _tables;
        private readonly RelationshipAnalyzer _relationshipAnalyzer;

        /// <summary>
        /// 初始化實體產生器的新實例。
        /// </summary>
        /// <param name="settings">程式碼產生設定</param>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="tables">資料表定義集合</param>
        public EntityGenerator(Settings settings, ILogger logger, List<TableDefinition> tables)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _relationshipAnalyzer = new RelationshipAnalyzer(logger);
        }

        /// <summary>
        /// 產生實體類別與設定的程式碼。
        /// </summary>
        /// <returns>非同步操作任務</returns>
        public async Task GenerateAsync(List<TableDefinition> tables)
        {
            try
            {
                _logger.Info("開始產生實體類別與設定");

                // 建立輸出目錄
                var entityOutputDir = Path.Combine(_settings.OutputDirectory, "Entities");
                var configOutputDir = Path.Combine(_settings.OutputDirectory, "Configurations");
                Directory.CreateDirectory(entityOutputDir);
                Directory.CreateDirectory(configOutputDir);

                // 分析所有表格之間的關聯
                var relationships = AnalyzeAllRelationships(tables);
                _logger.Info($"找到 {relationships.Count} 個表格關聯");

                // 產生實體類別與設定
                var tasks = tables.Select(table => Task.WhenAll(
                    GenerateEntityClassAsync(table, relationships, entityOutputDir),
                    GenerateConfigurationClassAsync(table, relationships, configOutputDir)
                ));

                await Task.WhenAll(tasks);

                _logger.Info("實體類別與設定產生完成");
            }
            catch (Exception ex)
            {
                _logger.Error("產生程式碼時發生錯誤", ex);
                throw new CodeGenerationException("產生程式碼時發生錯誤", ex);
            }
        }
        /// <summary>
        /// 分析所有表格之間的關聯關係。
        /// </summary>
        /// <param name="tables">資料表定義集合</param>
        /// <returns>表格關聯資訊集合</returns>
        private List<RelationshipType> AnalyzeAllRelationships(List<TableDefinition> tables)
        {
            var relationships = new List<RelationshipType>();

            foreach (var sourceTable in tables)
            {
                foreach (var targetTable in tables.Where(t => t != sourceTable))
                {
                    var relationship = _relationshipAnalyzer.AnalyzeRelationship(sourceTable, targetTable);
                    if (relationship.Type != RelationType.Unknown)
                    {
                        relationships.Add(relationship);
                    }
                }
            }

            return relationships;
        }
        /// <summary>
        /// 根據關聯類型產生適當的導航屬性。
        /// </summary>
        /// <param name="table">目前的資料表定義</param>
        /// <param name="relationships">所有的關聯資訊</param>
        /// <param name="sb">字串建構器</param>
        private void GenerateNavigationProperties(
            TableDefinition table,
            List<RelationshipType> relationships,
            StringBuilder sb)
        {
            foreach (var relationship in relationships.Where(r =>
                r.SourceTable == table.TableName || r.TargetTable == table.TableName))
            {
                switch (relationship.Type)
                {
                    case RelationType.OneToOne:
                        GenerateOneToOneNavigationProperty(table, relationship, sb);
                        break;

                    case RelationType.OneToMany:
                        GenerateOneToManyNavigationProperty(table, relationship, sb);
                        break;

                    case RelationType.ManyToMany:
                        GenerateManyToManyNavigationProperty(table, relationship, sb);
                        break;
                }
            }
        }
        /// <summary>
        /// 產生一對一關聯的導航屬性。
        /// </summary>
        private void GenerateOneToOneNavigationProperty(
            TableDefinition table,
            RelationshipType relationship,
            StringBuilder sb)
        {
            var isSource = table.TableName == relationship.SourceTable;
            var relatedTable = isSource ? relationship.TargetTable : relationship.SourceTable;
            var propertyName = ToPascalCase(relatedTable);

            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 關聯的 {relatedTable} 實體");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public virtual {propertyName} {propertyName} {{ get; set; }}");
            sb.AppendLine();
        }

        /// <summary>
        /// 產生一對多關聯的導航屬性。
        /// </summary>
        private void GenerateOneToManyNavigationProperty(
            TableDefinition table,
            RelationshipType relationship,
            StringBuilder sb)
        {
            var isSource = table.TableName == relationship.SourceTable;
            var relatedTable = isSource ? relationship.TargetTable : relationship.SourceTable;
            var propertyName = isSource ?
                Pluralize(ToPascalCase(relatedTable)) :
                ToPascalCase(relatedTable);

            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 關聯的 {relatedTable} {(isSource ? "集合" : "實體")}");
            sb.AppendLine($"        /// </summary>");

            if (isSource)
            {
                sb.AppendLine($"        public virtual ICollection<{ToPascalCase(relatedTable)}> {propertyName} {{ get; set; }}");
            }
            else
            {
                sb.AppendLine($"        public virtual {ToPascalCase(relatedTable)} {propertyName} {{ get; set; }}");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// 產生多對多關聯的導航屬性。
        /// </summary>
        private void GenerateManyToManyNavigationProperty(
            TableDefinition table,
            RelationshipType relationship,
            StringBuilder sb)
        {
            if (relationship.JunctionTableInfo == null)
            {
                _logger.Warning($"多對多關聯缺少中間表資訊: {table.TableName}");
                return;
            }

            // 處理中間表實體
            if (table.TableName == relationship.JunctionTableInfo.TableName)
            {
                // 中間表應該有兩個單一導航屬性指向兩端實體
                var sourceClassName = ToPascalCase(relationship.SourceTable);
                var targetClassName = ToPascalCase(relationship.TargetTable);

                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// 關聯的 {sourceClassName} 實體");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public virtual {sourceClassName} {sourceClassName} {{ get; set; }}");
                sb.AppendLine();

                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// 關聯的 {targetClassName} 實體");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public virtual {targetClassName} {targetClassName} {{ get; set; }}");
                sb.AppendLine();
            }
            else
            {
                // 兩端實體應該有集合導航屬性
                var otherEndClassName = ToPascalCase(
                    table.TableName == relationship.SourceTable ?
                    relationship.TargetTable :
                    relationship.SourceTable);

                var intermediateClassName = ToPascalCase(relationship.JunctionTableInfo.TableName);

                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// 關聯的 {intermediateClassName} 集合");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public virtual ICollection<{intermediateClassName}> {Pluralize(intermediateClassName)} {{ get; set; }}");
                sb.AppendLine();
            }
        }
        /// <summary>
        /// 產生實體設定類別的程式碼。
        /// </summary>
        /// <param name="table">資料表定義</param>
        /// <param name="relationships">表格間的關聯定義集合</param>
        /// <param name="outputDir">輸出目錄路徑</param>
        /// <returns>非同步操作任務</returns>
        /// <remarks>
        /// 此方法負責：
        /// 1. 產生 Entity Framework 實體設定類別
        /// 2. 設定資料表對應
        /// 3. 設定主鍵和索引
        /// 4. 設定關聯對應
        /// 5. 設定資料表和欄位的其他特性
        /// </remarks>
        private async Task GenerateConfigurationClassAsync(
            TableDefinition table,
            List<RelationshipType> relationships,
            string outputDir)
        {
            try
            {
                var sb = new StringBuilder();
                var className = ToPascalCase(table.TableName);

                // 加入必要的 using 陳述式
                GenerateConfigurationUsings(sb);
                sb.AppendLine();

                // 產生命名空間
                sb.AppendLine($"namespace {_settings.Namespace}.Configurations");
                sb.AppendLine("{");

                // 產生類別定義
                sb.AppendLine($"    public class {className}Configuration : EntityTypeConfiguration<{_settings.Namespace}.Entities.{className}>");
                sb.AppendLine("    {");

                // 產生建構函式
                sb.AppendLine($"        public {className}Configuration()");
                sb.AppendLine("        {");

                // 設定資料表對應
                GenerateTableMapping(sb, table);

                // 設定主鍵
                GeneratePrimaryKeyConfiguration(sb, table);

                // 設定欄位對應
                GeneratePropertyConfigurations(sb, table);

                // 設定關聯對應
                GenerateRelationshipConfigurations(sb, table, relationships);

                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                // 寫入檔案
                var filePath = Path.Combine(outputDir, $"{className}Configuration.cs");
                await File.WriteAllTextAsync(filePath, sb.ToString());
                _logger.Info($"已產生設定類別: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"產生設定類別 {table.TableName} 時發生錯誤", ex);
                throw;
            }
        }
        /// <summary>
        /// 產生實體設定類別所需的 using 陳述式。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <remarks>
        /// 自動引入實體設定所需的命名空間，包括：
        /// 1. Entity Framework 的設定類別
        /// 2. 資料模型相關命名空間
        /// 3. 自訂實體的命名空間
        /// </remarks>
        /// <exception cref="ArgumentNullException">當 sb 為 null 時擲回例外</exception>
        private void GenerateConfigurationUsings(StringBuilder sb)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));

            try
            {
                // Entity Framework 核心命名空間
                sb.AppendLine("using System.Data.Entity;");
                sb.AppendLine("using System.Data.Entity.ModelConfiguration;");
                sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");

                // 加入實體命名空間
                sb.AppendLine($"using {_settings.Namespace}.Entities;");

                // 根據需求加入額外的命名空間
                if (_settings.UseDataAnnotations)
                {
                    sb.AppendLine("using System.ComponentModel.DataAnnotations;");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("產生設定類別的 using 陳述式時發生錯誤", ex);
                throw new CodeGenerationException("無法產生 using 陳述式", ex);
            }
        }

        /// <summary>
        /// 產生資料表對應的設定程式碼。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="table">資料表定義資訊</param>
        /// <remarks>
        /// 設定 Entity Framework 的資料表對應，包括：
        /// 1. 資料表名稱
        /// 2. 結構描述名稱
        /// 3. 資料表層級的設定
        /// </remarks>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回例外</exception>
        private void GenerateTableMapping(StringBuilder sb, TableDefinition table)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (table == null) throw new ArgumentNullException(nameof(table));

            try
            {
                // 基本資料表對應
                sb.AppendLine($"            // 設定資料表對應");
                sb.AppendLine($"            ToTable(\"{table.TableName}\", \"{table.SchemaName}\");");

                // 加入資料表層級的註解
                if (!string.IsNullOrEmpty(table.Comment))
                {
                    sb.AppendLine();
                    sb.AppendLine("            // 資料表描述");
                    sb.AppendLine($"            // {table.Comment}");
                }

                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"產生資料表 {table.TableName} 的對應設定時發生錯誤", ex);
                throw new CodeGenerationException($"無法產生資料表對應設定: {table.TableName}", ex);
            }
        }
        /// <summary>
        /// 產生主鍵設定的程式碼。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="table">資料表定義資訊</param>
        /// <remarks>
        /// 設定實體的主鍵，支援：
        /// 1. 單一主鍵
        /// 2. 複合主鍵
        /// 3. 自動遞增設定
        /// </remarks>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回例外</exception>
        private void GeneratePrimaryKeyConfiguration(StringBuilder sb, TableDefinition table)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (table == null) throw new ArgumentNullException(nameof(table));

            try
            {
                var primaryKeys = table.Columns.Where(c => c.IsPrimaryKey).ToList();

                if (!primaryKeys.Any())
                {
                    _logger.Warning($"資料表 {table.TableName} 未定義主鍵");
                    return;
                }

                sb.AppendLine($"            // 設定主鍵");
                if (primaryKeys.Count == 1)
                {
                    // 單一主鍵設定
                    var pkColumn = primaryKeys[0];
                    sb.AppendLine($"            HasKey(t => t.{ToPascalCase(pkColumn.ColumnName)});");

                    // 設定自動遞增
                    if (pkColumn.IsIdentity)
                    {
                        sb.AppendLine($"            Property(t => t.{ToPascalCase(pkColumn.ColumnName)})");
                        sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);");
                    }
                }
                else
                {
                    // 複合主鍵設定
                    var pkProperties = string.Join(", ", primaryKeys.Select(pk =>
                        $"t.{ToPascalCase(pk.ColumnName)}"));
                    sb.AppendLine($"            HasKey(t => new {{ {pkProperties} }});");
                }

                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"產生資料表 {table.TableName} 的主鍵設定時發生錯誤", ex);
                throw new CodeGenerationException($"無法產生主鍵設定: {table.TableName}", ex);
            }
        }

        /// <summary>
        /// 產生欄位屬性設定的程式碼。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="table">資料表定義資訊</param>
        /// <remarks>
        /// 設定每個欄位的特性，包括：
        /// 1. 欄位名稱對應
        /// 2. 資料類型設定
        /// 3. 長度限制
        /// 4. 必要性設定
        /// 5. 預設值
        /// 6. 計算欄位
        /// </remarks>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回例外</exception>
        private void GeneratePropertyConfigurations(StringBuilder sb, TableDefinition table)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (table == null) throw new ArgumentNullException(nameof(table));

            try
            {
                sb.AppendLine($"            // 設定欄位屬性");

                foreach (var column in table.Columns.Where(c => !c.IsPrimaryKey))
                {
                    var propertyName = ToPascalCase(column.ColumnName);

                    sb.AppendLine($"            Property(t => t.{propertyName})");
                    sb.AppendLine($"                .HasColumnName(\"{column.ColumnName}\")");

                    // 設定資料類型
                    if (!string.IsNullOrEmpty(column.DataType))
                    {
                        ConfigureColumnDataType(sb, column);
                    }

                    // 設定是否必要
                    if (!column.IsNullable)
                    {
                        sb.AppendLine("                .IsRequired()");
                    }

                    // 設定最大長度
                    if (column.MaxLength.HasValue && column.DataType.ToLower() == "string")
                    {
                        sb.AppendLine($"                .HasMaxLength({column.MaxLength.Value})");
                    }

                    // 設定計算欄位
                    if (column.IsComputed)
                    {
                        sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed)");
                    }

                    // 設定預設值
                    if (!string.IsNullOrEmpty(column.DefaultValue))
                    {
                        ConfigureDefaultValue(sb, column);
                    }

                    sb.AppendLine("                ;");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"產生資料表 {table.TableName} 的欄位設定時發生錯誤", ex);
                throw new CodeGenerationException($"無法產生欄位設定: {table.TableName}", ex);
            }
        }

        /// <summary>
        /// 設定欄位的資料類型。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="column">欄位定義</param>
        private void ConfigureColumnDataType(StringBuilder sb, ColumnDefinition column)
        {
            var dataType = column.DataType.ToLower();

            if (dataType == "decimal" || dataType == "numeric")
            {
                sb.AppendLine($"                .HasPrecision({column.Precision},{column.Scale})");
            }
            else if (dataType == "varchar")
            {
                var maxLength = column.MaxLength ?? -1;
                sb.AppendLine($"                .HasColumnType(\"{dataType}\")");
                sb.AppendLine($"                .HasMaxLength({(maxLength == -1 ? "max" : maxLength.ToString())})");
            }
            else if (dataType == "nvarchar")
            {
                var maxLength = column.MaxLength ?? -1;
                maxLength = maxLength / 2;
                sb.AppendLine($"                .HasColumnType(\"{dataType}\")");
                sb.AppendLine($"                .HasMaxLength({(maxLength == -1 ? "max" : maxLength.ToString())})");
            }
        }

        /// <summary>
        /// 設定欄位的預設值。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="column">欄位定義</param>
        private void ConfigureDefaultValue(StringBuilder sb, ColumnDefinition column)
        {
            // 根據資料類型處理預設值
            var defaultValue = column.DefaultValue.Trim();
            var dataType = column.DataType.ToLower();

            // 處理不同類型的預設值
            if (IsComputedDefaultValue(defaultValue))
            {
                // 對於計算型預設值（如 GETDATE()）
                sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed)");
            }
            else if (IsIdentityColumn(column))
            {
                // 對於自動遞增欄位
                sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)");
            }
            else
            {
                // 對於一般預設值
                sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)");
            }
        }

        /// <summary>
        /// 檢查是否為計算型預設值。
        /// </summary>
        /// <param name="defaultValue">預設值定義</param>
        /// <returns>若為計算型預設值則返回 true，否則返回 false</returns>
        private bool IsComputedDefaultValue(string defaultValue)
        {
            if (string.IsNullOrEmpty(defaultValue))
                return false;

            // 檢查常見的計算型函數
            var computedFunctions = new[]
            {
                "GETDATE",
                "CURRENT_TIMESTAMP",
                "NEWID",
                "NEWSEQUENTIALID",
                "CURRENT_USER",
                "SYSTEM_USER"
            };

            return computedFunctions.Any(func =>
                defaultValue.ToUpperInvariant().Contains(func.ToUpperInvariant()));
        }

        /// <summary>
        /// 檢查是否為自動遞增欄位。
        /// </summary>
        /// <param name="column">欄位定義</param>
        /// <returns>若為自動遞增欄位則返回 true，否則返回 false</returns>
        private bool IsIdentityColumn(ColumnDefinition column)
        {
            return column.IsIdentity;
        }
        /// <summary>
        /// 產生關聯設定的程式碼。
        /// </summary>
        /// <remarks>
        /// 根據不同的關聯類型產生對應的設定程式碼。
        /// </remarks>
        private void GenerateRelationshipConfigurations(
            StringBuilder sb,
            TableDefinition table,
            List<RelationshipType> relationships)
        {
            foreach (var relationship in relationships.Where(r =>
                r.SourceTable == table.TableName || r.TargetTable == table.TableName))
            {
                switch (relationship.Type)
                {
                    case RelationType.OneToOne:
                        ConfigureOneToOneRelationship(sb, table, relationship);
                        break;

                    case RelationType.OneToMany:
                        ConfigureOneToManyRelationship(sb, table, relationship);
                        break;

                    case RelationType.ManyToMany:
                        ConfigureManyToManyRelationship(sb, table, relationship);
                        break;
                }
            }
        }

        /// <summary>
        /// 設定一對一關聯的實體對應。
        /// </summary>
        /// <param name="sb">字串建構器實例，用於生成設定程式碼</param>
        /// <param name="table">當前處理的資料表定義</param>
        /// <param name="relationship">關聯類型資訊</param>
        /// <remarks>
        /// 此方法負責處理以下設定：
        /// 1. 建立必要或選擇性的一對一關聯
        /// 2. 設定外鍵對應和約束條件
        /// 3. 配置參考完整性和關聯刪除行為
        /// 4. 處理雙向導航屬性
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一參數為 null 時擲回</exception>
        private void ConfigureOneToOneRelationship(
            StringBuilder sb,
            TableDefinition table,
            RelationshipType relationship)
        {
            ValidateParameters(sb, table, relationship);

            try
            {
                var isSource = table.TableName == relationship.SourceTable;
                var foreignKey = GetRelationshipForeignKey(table, relationship);

                if (foreignKey == null)
                {
                    _logger.Warning($"在資料表 {table.TableName} 中找不到關聯 {relationship.SourceTable}-{relationship.TargetTable} 的外鍵定義");
                    return;
                }

                // 確定目標類別名稱和導航屬性名稱
                var principalClassName = ToPascalCase(relationship.TargetTable);
                var dependentClassName = ToPascalCase(relationship.SourceTable);

                sb.AppendLine($"            // 設定與 {(isSource ? principalClassName : dependentClassName)} 的一對一關聯");

                if (isSource)
                {
                    // 外鍵擁有者(Dependent)端的配置
                    ConfigureSourceEndOneToOne(
                        sb,
                        foreignKey,
                        principalClassName,  // 主體方的類別名稱
                        dependentClassName   // 相依方的類別名稱
                    );
                }
                else
                {
                    // 主體(Principal)端的配置
                    ConfigureTargetEndOneToOne(
                        sb,
                        table.TableName,
                        principalClassName
                    );
                }

                sb.AppendLine("                ;");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"設定一對一關聯時發生錯誤: {table.TableName}", ex);
                throw new RelationshipConfigurationException($"無法設定一對一關聯: {table.TableName}", ex);
            }
        }

        /// <summary>
        /// 設定一對一關聯中來源端的實體對應邏輯。
        /// </summary>
        /// <param name="sb">用於生成設定程式碼的字串建構器實例</param>
        /// <param name="foreignKey">定義關聯的外鍵資訊</param>
        /// <param name="targetClassName">目標實體類別名稱</param>
        /// <param name="navigationProperty">導航屬性名稱</param>
        /// <remarks>
        /// 此方法負責設定一對一關聯中擁有外鍵的實體方配置，包括：
        /// 1. 建立對目標實體的必要或選擇性參考
        /// 2. 設定外鍵對應和約束關係
        /// 3. 配置參考完整性規則
        /// 4. 處理關聯的刪除和更新行為
        /// 
        /// 使用範例：
        /// ```csharp
        /// // 在 User 類別中配置與 UserProfile 的一對一關聯
        /// HasRequired(u => u.UserProfile)
        ///     .WithRequiredPrincipal()
        ///     .HasForeignKey(u => u.ProfileId);
        /// ```
        /// </remarks>
        /// <exception cref="ArgumentNullException">當任一必要參數為 null 時擲回</exception>
        /// <exception cref="RelationshipConfigurationException">設定關聯時發生錯誤時擲回</exception>
        private void ConfigureSourceEndOneToOne(
            StringBuilder sb,
            ForeignKeyDefinition foreignKey,
            string targetClassName,
            string navigationProperty)
        {
            try
            {
                ValidateSourceEndParameters(sb, foreignKey, targetClassName, navigationProperty);

                var foreignKeyProperty = ToPascalCase(foreignKey.ForeignKeyColumn);

                sb.AppendLine($"            // 配置來源端實體 ({targetClassName} 的外鍵擁有者)");

                // 根據外鍵的可為空性決定關聯類型
                if (IsForeignKeyNullable(foreignKey))
                {
                    sb.AppendLine($"            HasOptional(t => t.{targetClassName})");
                    sb.AppendLine($"                .WithRequired(t => t.{navigationProperty})");
                }
                else
                {
                    sb.AppendLine($"            HasRequired(t => t.{targetClassName})");
                    sb.AppendLine($"                .WithOptional(t => t.{navigationProperty})");
                }

                // 設定外鍵約束選項
                ConfigureForeignKeyOptions(sb, foreignKey);

                _logger.Info($"已設定一對一關聯的來源端：{navigationProperty} -> {targetClassName}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"設定一對一關聯的來源端時發生錯誤：{targetClassName}.{navigationProperty}";
                _logger.Error(errorMessage, ex);
                throw new RelationshipConfigurationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// 設定一對一關聯中目標端的實體對應邏輯。
        /// </summary>
        /// <param name="sb">用於生成設定程式碼的字串建構器實例</param>
        /// <param name="sourceTableName">來源表格名稱</param>
        /// <param name="navigationProperty">導航屬性名稱</param>
        /// <remarks>
        /// 此方法負責設定一對一關聯中不持有外鍵的實體方配置，包括：
        /// 1. 建立對來源實體的反向參考
        /// 2. 設定導航屬性的對應關係
        /// 3. 配置參考完整性規則
        /// 
        /// 使用範例：
        /// ```csharp
        /// // 在 UserProfile 類別中配置與 User 的一對一關聯
        /// HasRequired(p => p.User)
        ///     .WithRequiredDependent();
        /// ```
        /// </remarks>
        /// <exception cref="ArgumentNullException">當任一必要參數為 null 時擲回</exception>
        /// <exception cref="RelationshipConfigurationException">設定關聯時發生錯誤時擲回</exception>
        private void ConfigureTargetEndOneToOne(
            StringBuilder sb,
            string sourceTableName,
            string navigationProperty)
        {
            try
            {
                ValidateTargetEndParameters(sb, sourceTableName, navigationProperty);

                sb.AppendLine($"            // 配置目標端實體 (被參考方)");
                sb.AppendLine($"            HasRequired(t => t.{navigationProperty})");
                sb.AppendLine("                .WithRequiredDependent()");

                _logger.Info($"已設定一對一關聯的目標端：{sourceTableName} <- {navigationProperty}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"設定一對一關聯的目標端時發生錯誤：{sourceTableName}.{navigationProperty}";
                _logger.Error(errorMessage, ex);
                throw new RelationshipConfigurationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// 驗證來源端設定所需的參數有效性。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="foreignKey">外鍵定義</param>
        /// <param name="targetClassName">目標類別名稱</param>
        /// <param name="navigationProperty">導航屬性名稱</param>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回</exception>
        private void ValidateSourceEndParameters(
            StringBuilder sb,
            ForeignKeyDefinition foreignKey,
            string targetClassName,
            string navigationProperty)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (foreignKey == null) throw new ArgumentNullException(nameof(foreignKey));
            if (string.IsNullOrEmpty(targetClassName))
                throw new ArgumentNullException(nameof(targetClassName));
            if (string.IsNullOrEmpty(navigationProperty))
                throw new ArgumentNullException(nameof(navigationProperty));
        }

        /// <summary>
        /// 驗證目標端設定所需的參數有效性。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="sourceTableName">來源表格名稱</param>
        /// <param name="navigationProperty">導航屬性名稱</param>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回</exception>
        private void ValidateTargetEndParameters(
            StringBuilder sb,
            string sourceTableName,
            string navigationProperty)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (string.IsNullOrEmpty(sourceTableName))
                throw new ArgumentNullException(nameof(sourceTableName));
            if (string.IsNullOrEmpty(navigationProperty))
                throw new ArgumentNullException(nameof(navigationProperty));
        }

        /// <summary>
        /// 檢查外鍵是否允許 NULL 值。
        /// </summary>
        /// <param name="foreignKey">外鍵定義</param>
        /// <returns>如果外鍵允許 NULL 值則返回 true，否則返回 false</returns>
        private bool IsForeignKeyNullable(ForeignKeyDefinition foreignKey)
        {
            return _tables
                .SelectMany(t => t.Columns)
                .Where(c => c.ColumnName == foreignKey.ForeignKeyColumn)
                .Any(c => c.IsNullable);
        }

        /// <summary>
        /// 設定外鍵的進階選項。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="foreignKey">外鍵定義</param>
        private void ConfigureForeignKeyOptions(StringBuilder sb, ForeignKeyDefinition foreignKey)
        {
            // 設定外鍵索引
            if (HasUniqueConstraint(foreignKey))
            {
                sb.AppendLine("                .HasIndex(c => c.IsUnique = true)");
            }


            // 設定更新行為
            if (!string.IsNullOrEmpty(foreignKey.UpdateRule))
            {
                ConfigureUpdateBehavior(sb, foreignKey.UpdateRule);
            }
        }


        /// <summary>
        /// 設定一對多關聯的實體對應。
        /// </summary>
        /// <param name="sb">字串建構器實例，用於生成設定程式碼</param>
        /// <param name="table">當前處理的資料表定義</param>
        /// <param name="relationship">關聯類型資訊</param>
        /// <remarks>
        /// 此方法負責處理以下設定：
        /// 1. 設定一對多關聯的主從關係
        /// 2. 配置集合導航屬性
        /// 3. 設定外鍵對應和約束條件
        /// 4. 處理參考完整性和級聯刪除
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一參數為 null 時擲回</exception>
        private void ConfigureOneToManyRelationship(
            StringBuilder sb,
            TableDefinition table,
            RelationshipType relationship)
        {
            ValidateParameters(sb, table, relationship);

            try
            {
                var isSource = table.TableName == relationship.SourceTable;
                var foreignKey = GetRelationshipForeignKey(table, relationship);

                if (foreignKey == null)
                {
                    _logger.Warning($"在資料表 {table.TableName} 中找不到關聯 {relationship.SourceTable}-{relationship.TargetTable} 的外鍵定義");
                    return;
                }

                var targetClassName = ToPascalCase(isSource ? relationship.TargetTable : relationship.SourceTable);

                sb.AppendLine($"            // 設定與 {targetClassName} 的一對多關聯");

                if (isSource)
                {
                    // 設定集合導航屬性（一方）
                    var collectionProperty = Pluralize(targetClassName);
                    ConfigureOneToManyPrincipal(sb, targetClassName, collectionProperty, foreignKey);
                }
                else
                {
                    // 設定參考導航屬性（多方）
                    ConfigureOneToManyDependent(sb, targetClassName, foreignKey);
                }

                sb.AppendLine("                ;");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"設定一對多關聯時發生錯誤: {table.TableName}", ex);
                throw new RelationshipConfigurationException($"無法設定一對多關聯: {table.TableName}", ex);
            }
        }

        /// <summary>
        /// 設定一對多關聯中主體端(Principal)的實體對應。
        /// </summary>
        /// <param name="sb">字串建構器實例，用於生成設定程式碼</param>
        /// <param name="targetClassName">關聯目標類別名稱</param>
        /// <param name="collectionProperty">集合導航屬性名稱</param>
        /// <param name="foreignKey">外鍵定義資訊</param>
        /// <remarks>
        /// 此方法負責設定一對多關聯中「一」的一方（主體端）的實體對應，包括：
        /// 1. 建立對多方的集合導航屬性
        /// 2. 設定與從屬端的關聯映射
        /// 3. 配置外鍵約束和參考完整性
        /// 4. 處理可為空值的關聯
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一參數為 null 時擲回此例外</exception>
        /// <exception cref="RelationshipConfigurationException">設定關聯時發生錯誤時擲回此例外</exception>
        private void ConfigureOneToManyPrincipal(
            StringBuilder sb,
            string targetClassName,
            string collectionProperty,
            ForeignKeyDefinition foreignKey)
        {
            ValidateOneToManyPrincipalParameters(sb, targetClassName, collectionProperty, foreignKey);

            try
            {
                // 取得外鍵參考的主鍵欄位
                var principalKeyProperty = ToPascalCase(foreignKey.PrimaryKeyColumn);
                var dependentKeyProperty = ToPascalCase(foreignKey.ForeignKeyColumn);

                sb.AppendLine($"            // 設定一對多關聯的主體端 (Principal)");
                sb.AppendLine($"            HasMany(t => t.{collectionProperty})");

                // 設定從屬端的導航屬性
                if (foreignKey.IsEnabled)
                {
                    // 啟用外鍵約束的情況
                    sb.AppendLine($"                .WithRequired(t => t.{targetClassName})");
                }
                else
                {
                    // 未啟用外鍵約束的情況
                    sb.AppendLine($"                .WithOptional(t => t.{targetClassName})");
                }

                // 設定外鍵對應
                sb.AppendLine($"                .HasForeignKey(t => t.{dependentKeyProperty})");

                // 如果有指定對應的主鍵欄位
                if (!string.IsNullOrEmpty(principalKeyProperty))
                {
                    sb.AppendLine($"                .Map(m => m.MapKey(\"{foreignKey.PrimaryKeyColumn}\"))");
                }

                // 設定額外的索引
                if (HasUniqueConstraint(foreignKey))
                {
                    sb.AppendLine("                .HasRequired()");
                }

                // 處理復原設定
                if (!string.IsNullOrEmpty(foreignKey.UpdateRule))
                {
                    ConfigureUpdateBehavior(sb, foreignKey.UpdateRule);
                }

                _logger.Info($"已設定一對多關聯的主體端: {targetClassName}.{collectionProperty}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"設定一對多關聯的主體端時發生錯誤: {targetClassName}.{collectionProperty}";
                _logger.Error(errorMessage, ex);
                throw new RelationshipConfigurationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// 設定一對多關聯中從屬端(Dependent)的實體對應。
        /// </summary>
        /// <param name="sb">字串建構器實例，用於生成設定程式碼</param>
        /// <param name="targetClassName">關聯目標類別名稱</param>
        /// <param name="foreignKey">外鍵定義資訊</param>
        /// <remarks>
        /// 此方法負責設定一對多關聯中「多」的一方（從屬端）的實體對應，包括：
        /// 1. 建立對一方的參考導航屬性
        /// 2. 設定與主體端的關聯映射
        /// 3. 配置外鍵欄位的資料庫對應
        /// 4. 處理參考完整性約束
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一參數為 null 時擲回此例外</exception>
        /// <exception cref="RelationshipConfigurationException">設定關聯時發生錯誤時擲回此例外</exception>
        private void ConfigureOneToManyDependent(
            StringBuilder sb,
            string targetClassName,
            ForeignKeyDefinition foreignKey)
        {
            ValidateOneToManyDependentParameters(sb, targetClassName, foreignKey);

            try
            {
                var dependentKeyProperty = ToPascalCase(foreignKey.ForeignKeyColumn);

                sb.AppendLine($"            // 設定一對多關聯的從屬端 (Dependent)");
                sb.AppendLine($"            HasRequired(t => t.{targetClassName})");

                // 設定外鍵對應
                sb.AppendLine($"                .WithMany()");
                sb.AppendLine($"                .HasForeignKey(t => t.{dependentKeyProperty})");

                // 設定外鍵約束
                if (foreignKey.IsEnabled)
                {
                    ConfigureForeignKeyConstraints(sb, foreignKey);
                }

                // 設定索引
                if (HasUniqueConstraint(foreignKey))
                {
                    sb.AppendLine("                .HasIndex(c => c.IsUnique = true)");
                }

                _logger.Info($"已設定一對多關聯的從屬端: {targetClassName}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"設定一對多關聯的從屬端時發生錯誤: {targetClassName}";
                _logger.Error(errorMessage, ex);
                throw new RelationshipConfigurationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// 驗證一對多關聯主體端設定的參數。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="targetClassName">目標類別名稱</param>
        /// <param name="collectionProperty">集合屬性名稱</param>
        /// <param name="foreignKey">外鍵定義</param>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回</exception>
        private void ValidateOneToManyPrincipalParameters(
            StringBuilder sb,
            string targetClassName,
            string collectionProperty,
            ForeignKeyDefinition foreignKey)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (string.IsNullOrEmpty(targetClassName))
                throw new ArgumentNullException(nameof(targetClassName));
            if (string.IsNullOrEmpty(collectionProperty))
                throw new ArgumentNullException(nameof(collectionProperty));
            if (foreignKey == null) throw new ArgumentNullException(nameof(foreignKey));
        }

        /// <summary>
        /// 驗證一對多關聯從屬端設定的參數。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="targetClassName">目標類別名稱</param>
        /// <param name="foreignKey">外鍵定義</param>
        /// <exception cref="ArgumentNullException">當任一參數為 null 時擲回</exception>
        private void ValidateOneToManyDependentParameters(
            StringBuilder sb,
            string targetClassName,
            ForeignKeyDefinition foreignKey)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (string.IsNullOrEmpty(targetClassName))
                throw new ArgumentNullException(nameof(targetClassName));
            if (foreignKey == null) throw new ArgumentNullException(nameof(foreignKey));
        }

        /// <summary>
        /// 檢查外鍵是否具有唯一性約束。
        /// </summary>
        /// <param name="foreignKey">外鍵定義</param>
        /// <returns>若具有唯一性約束則返回 true，否則返回 false</returns>
        private bool HasUniqueConstraint(ForeignKeyDefinition foreignKey)
        {
            // 實作唯一性約束檢查邏輯
            return false; // 待實作
        }

        /// <summary>
        /// 設定更新行為的規則。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="updateRule">更新規則</param>
        private void ConfigureUpdateBehavior(StringBuilder sb, string updateRule)
        {
            switch (updateRule.ToUpper())
            {
                case "CASCADE":
                    sb.AppendLine("                .WillCascadeOnDelete(true)");
                    break;
                case "SET NULL":
                    sb.AppendLine("                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)");
                    break;
                default:
                    sb.AppendLine("                .WillCascadeOnDelete(false)");
                    break;
            }
        }

        /// <summary>
        /// 設定外鍵約束。
        /// </summary>
        /// <param name="sb">字串建構器實例</param>
        /// <param name="foreignKey">外鍵定義</param>
        private void ConfigureForeignKeyConstraints(StringBuilder sb, ForeignKeyDefinition foreignKey)
        {
            if (!string.IsNullOrEmpty(foreignKey.UpdateRule))
            {
                ConfigureUpdateBehavior(sb, foreignKey.UpdateRule);
            }
        }


        /// <summary>
        /// 設定多對多關聯的實體對應。
        /// </summary>
        /// <param name="sb">字串建構器實例，用於生成設定程式碼</param>
        /// <param name="table">當前處理的資料表定義</param>
        /// <param name="relationship">關聯類型資訊</param>
        /// <remarks>
        /// 此方法負責處理以下設定：
        /// 1. 設定多對多關聯的中間表對應
        /// 2. 配置雙向集合導航屬性
        /// 3. 設定連接表的主鍵和外鍵
        /// 4. 處理額外的中間表欄位
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一參數為 null 時擲回</exception>
        private void ConfigureManyToManyRelationship(
            StringBuilder sb,
            TableDefinition table,
            RelationshipType relationship)
        {
            ValidateParameters(sb, table, relationship);

            try
            {
                var junctionInfo = relationship.JunctionTableInfo;
                if (junctionInfo == null)
                {
                    _logger.Warning($"多對多關聯 {relationship.SourceTable}-{relationship.TargetTable} 缺少中間表資訊");
                    return;
                }

                var targetClassName = ToPascalCase(
                    table.TableName == relationship.SourceTable ?
                    relationship.TargetTable :
                    relationship.SourceTable
                );

                sb.AppendLine($"            // 設定與 {targetClassName} 的多對多關聯");
                sb.AppendLine($"            HasMany(t => t.{Pluralize(targetClassName)})");
                sb.AppendLine($"                .WithMany(t => t.{Pluralize(table.TableName)})");
                sb.AppendLine("                .Map(m =>");
                sb.AppendLine("                {");

                // 設定中間表
                sb.AppendLine($"                    m.ToTable(\"{junctionInfo.TableName}\");");

                // 設定外鍵對應
                if (table.TableName == relationship.SourceTable)
                {
                    foreach (var column in junctionInfo.SourceKeyColumns)
                    {
                        sb.AppendLine($"                    m.MapLeftKey(\"{column}\");");
                    }
                    foreach (var column in relationship.ForeignKeyColumns.Select(fk => fk.ForeignKeyColumn))
                    {
                        sb.AppendLine($"                    m.MapRightKey(\"{column}\");");
                    }
                }
                else
                {
                    foreach (var column in relationship.ForeignKeyColumns.Select(fk => fk.ForeignKeyColumn))
                    {
                        sb.AppendLine($"                    m.MapLeftKey(\"{column}\");");
                    }
                    foreach (var column in junctionInfo.SourceKeyColumns)
                    {
                        sb.AppendLine($"                    m.MapRightKey(\"{column}\");");
                    }
                }

                sb.AppendLine("                });");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"設定多對多關聯時發生錯誤: {table.TableName}", ex);
                throw new RelationshipConfigurationException($"無法設定多對多關聯: {table.TableName}", ex);
            }
        }

        /// <summary>
        /// 驗證關聯設定所需的參數。
        /// </summary>
        private void ValidateParameters(StringBuilder sb, TableDefinition table, RelationshipType relationship)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (relationship == null) throw new ArgumentNullException(nameof(relationship));
        }

        /// <summary>
        /// 取得指定關聯的外鍵定義。
        /// </summary>
        private ForeignKeyDefinition GetRelationshipForeignKey(TableDefinition table, RelationshipType relationship)
        {
            return table.ForeignKeys.FirstOrDefault(fk =>
                (relationship.ForeignKeyColumns?.Any(rc =>
                    rc.ForeignKeyColumn == fk.ForeignKeyColumn) ?? false) &&
                (fk.PrimaryTable == relationship.TargetTable ||
                 fk.PrimaryTable == relationship.SourceTable));
        }

        /// <summary>
        /// 產生實體類別的程式碼。
        /// </summary>
        /// <param name="table">資料表定義</param>
        /// <param name="relationships">表格間的關聯定義集合</param>
        /// <param name="outputDir">輸出目錄路徑</param>
        /// <returns>非同步操作任務</returns>
        /// <remarks>
        /// 此方法負責：
        /// 1. 產生實體類別的基本結構
        /// 2. 產生資料表欄位對應的屬性
        /// 3. 根據關聯定義產生導航屬性
        /// 4. 加入適當的 XML 文件註解
        /// </remarks>
        private async Task GenerateEntityClassAsync(
            TableDefinition table,
            List<RelationshipType> relationships,
            string outputDir)
        {
            try
            {
                var sb = new StringBuilder();
                var className = ToPascalCase(table.TableName);

                // 加入必要的 using 陳述式
                GenerateEntityUsings(sb);
                sb.AppendLine();
                //sb.AppendLine("using System;");
                //sb.AppendLine("using System.Collections.Generic;");

                //if (_settings.UseDataAnnotations)
                //{
                //    sb.AppendLine("using System.ComponentModel.DataAnnotations;");
                //    sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
                //}

                // 產生命名空間
                sb.AppendLine();
                sb.AppendLine($"namespace {_settings.Namespace}.Entities");
                sb.AppendLine("{");

                // 加入類別註解
                if (!string.IsNullOrEmpty(table.Comment) && _settings.IncludeComments)
                {
                    sb.AppendLine("    /// <summary>");
                    AppendXmlComment(sb, table.Comment, "    ");
                    sb.AppendLine("    /// </summary>");
                }

                sb.AppendLine($"    public class {className}");
                sb.AppendLine("    {");

                // 產生建構函式
                GenerateEntityConstructor(sb, className, relationships);

                // 產生資料表欄位對應的屬性
                foreach (var column in table.Columns)
                {
                    GenerateColumnProperty(sb, column);
                }

                // 產生導航屬性
                GenerateNavigationProperties(table, relationships, sb);

                sb.AppendLine("    }");
                sb.AppendLine("}");

                // 寫入檔案
                var filePath = Path.Combine(outputDir, $"{className}.cs");
                await File.WriteAllTextAsync(filePath, sb.ToString());
                _logger.Info($"已產生實體類別: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"產生實體類別 {table.TableName} 時發生錯誤", ex);
                throw;
            }
            //// Properties
            //foreach (var column in table.Columns)
            //{
            //    if (_settings.IncludeComments && !string.IsNullOrEmpty(column.Comment))
            //    {
            //        sb.AppendLine("        /// <summary>");
            //        AppendXmlComment(sb, column.Comment, "        ");
            //        sb.AppendLine("        /// </summary>");
            //    }

            //if (_settings.UseDataAnnotations)
            //{
            //    if (column.IsPrimaryKey)
            //        sb.AppendLine("        [Key]");

            //    if (!column.IsNullable)
            //        sb.AppendLine("        [Required]");

            //    if (column.MaxLength.HasValue && column.DataType.ToLower() == "string")
            //        sb.AppendLine($"        [StringLength({column.MaxLength.Value})]");

            //    if (column.DataType.ToLower() == "decimal" || column.DataType.ToLower() == "numeric")
            //        sb.AppendLine($"        [Column(TypeName = \"decimal({column.Precision},{column.Scale})\")]");

            //    if (column.ColumnName.ToLower().Contains("email"))
            //        sb.AppendLine("        [EmailAddress]");
            //}

            //    var propertyType = GetPropertyType(column);
            //    if (column.IsNullable && IsValueType(propertyType))
            //    {
            //        propertyType += "?";
            //    }

            //    sb.AppendLine($"        public {propertyType} {ToPascalCase(column.ColumnName)} {{ get; set; }}");
            //    sb.AppendLine();
            //}

            // Navigation Properties
            //if (_settings.IncludeForeignKeys)
            //{
            //    // One-to-One and One-to-Many navigation properties
            //    foreach (var fk in table.ForeignKeys)
            //    {
            //        var refTableName = ToPascalCase(fk.PrimaryTable);
            //        sb.AppendLine($"        public virtual {refTableName} {refTableName} {{ get; set; }}");
            //        sb.AppendLine();
            //    }

            //    // Inverse navigation properties for tables referencing this one
            //    foreach (var referencingTable in _tables.Where(t => !t.IsManyToMany &&
            //        t.ForeignKeys.Any(fk => fk.PrimaryTable == table.TableName)))
            //    {
            //        var referencingClassName = ToPascalCase(referencingTable.TableName);
            //        var collectionName = Pluralize(referencingClassName);

            //        sb.AppendLine($"        public virtual ICollection<{referencingClassName}> {collectionName} {{ get; set; }}");
            //        sb.AppendLine();
            //    }

            //    // Many-to-Many collection properties
            //    if (!table.IsManyToMany)
            //    {
            //        var manyToManyRelationships = GetManyToManyRelationships(table);
            //        foreach (var rel in manyToManyRelationships)
            //        {
            //            var relatedEntity = ToPascalCase(rel.RelatedTable);
            //            sb.AppendLine($"        public virtual ICollection<{relatedEntity}> {Pluralize(relatedEntity)} {{ get; set; }}");
            //            sb.AppendLine();
            //        }
            //    }
            //}

            //sb.AppendLine("    }");
            //sb.AppendLine("}");

            //var filePath = Path.Combine(outputDir, $"{className}.cs");
            //await File.WriteAllTextAsync(filePath, sb.ToString());
            //_logger.Info($"Generated entity class: {filePath}");
        }
        /// <summary>
        /// 產生實體類別所需的 using 陳述式。
        /// </summary>
        /// <param name="sb">字串建構器</param>
        /// <remarks>
        /// 根據實體類別的需求自動引入必要的命名空間，包括：
        /// 1. 基本的系統類別
        /// 2. 資料註解 (若啟用)
        /// 3. Entity Framework 相關命名空間
        /// </remarks>
        private void GenerateEntityUsings(StringBuilder sb)
        {
            try
            {
                // 基本系統命名空間
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Generic;");

                // 根據設定決定是否引入資料註解
                if (_settings.UseDataAnnotations)
                {
                    sb.AppendLine("using System.ComponentModel.DataAnnotations;");
                    sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
                }

                // 若有其他特定需求的命名空間，可在此擴充
                if (_settings.IncludeForeignKeys)
                {
                    sb.AppendLine("using System.Data.Entity;");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("產生 using 陳述式時發生錯誤", ex);
                throw;
            }
        }

        /// <summary>
        /// 產生實體類別的建構函式。
        /// </summary>
        /// <param name="sb">字串建構器</param>
        /// <param name="className">類別名稱</param>
        /// <param name="relationships">關聯定義集合</param>
        /// <remarks>
        /// 建構函式負責：
        /// 1. 初始化集合導航屬性
        /// 2. 設定預設值
        /// 3. 根據關聯類型初始化必要的屬性
        /// </remarks>
        private void GenerateEntityConstructor(
            StringBuilder sb,
            string className,
            List<RelationshipType> relationships)
        {
            try
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// 初始化 {className} 類別的新執行個體。");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public {className}()");
                sb.AppendLine("        {");

                // 初始化集合導航屬性
                var collectionProperties = relationships
                    .Where(r => r.SourceTable == className &&
                               (r.Type == RelationType.OneToMany || r.Type == RelationType.ManyToMany) ||
                               r.TargetTable == className && r.Type == RelationType.ManyToMany)
                    .ToList();

                foreach (var rel in collectionProperties)
                {
                    var propertyType = rel.Type == RelationType.ManyToMany ?
                        rel.TargetTable :
                        rel.SourceTable == className ? rel.TargetTable : rel.SourceTable;

                    var propertyName = Pluralize(ToPascalCase(propertyType));
                    sb.AppendLine($"            {propertyName} = new HashSet<{ToPascalCase(propertyType)}>();");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"產生 {className} 的建構函式時發生錯誤", ex);
                throw;
            }
        }

        /// <summary>
        /// 產生資料表欄位對應的屬性。
        /// </summary>
        /// <param name="sb">字串建構器</param>
        /// <param name="column">欄位定義</param>
        /// <remarks>
        /// 此方法負責：
        /// 1. 產生屬性的 XML 文件註解
        /// 2. 根據設定加入適當的資料註解
        /// 3. 處理欄位的資料類型對應
        /// 4. 處理可為空值的情況
        /// </remarks>
        private void GenerateColumnProperty(StringBuilder sb, ColumnDefinition column)
        {
            try
            {
                // 加入屬性註解
                if (_settings.IncludeComments && !string.IsNullOrEmpty(column.Comment))
                {
                    sb.AppendLine("        /// <summary>");
                    AppendXmlComment(sb, column.Comment, "        ");
                    sb.AppendLine("        /// </summary>");
                }

                // 加入資料註解 (若啟用)
                if (_settings.UseDataAnnotations)
                {
                    GenerateDataAnnotations(sb, column);
                }

                // 決定屬性型別
                var propertyType = GetPropertyType(column);
                if (column.IsNullable && IsValueType(propertyType))
                {
                    propertyType += "?";
                }

                // 產生屬性定義
                var propertyName = ToPascalCase(column.ColumnName);
                sb.AppendLine($"        public {propertyType} {propertyName} {{ get; set; }}");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"產生欄位 {column.ColumnName} 的屬性時發生錯誤", ex);
                throw;
            }
        }

        /// <summary>
        /// 產生欄位的資料註解。
        /// </summary>
        /// <param name="sb">字串建構器</param>
        /// <param name="column">欄位定義</param>
        /// <remarks>
        /// 根據欄位特性加入適當的資料註解，包括：
        /// - 主鍵標記
        /// - 必要欄位標記
        /// - 最大長度限制
        /// - 資料類型特定設定
        /// - 驗證規則
        /// </remarks>
        private void GenerateDataAnnotations(StringBuilder sb, ColumnDefinition column)
        {
            // 主鍵標記
            if (column.IsPrimaryKey)
            {
                sb.AppendLine("        [Key]");
            }

            // 必要欄位標記
            if (!column.IsNullable)
            {
                sb.AppendLine("        [Required]");
            }

            // 字串長度限制
            if (column.MaxLength.HasValue && column.DataType.ToLower() == "string")
            {
                sb.AppendLine($"        [StringLength({column.MaxLength.Value})]");
            }

            // 資料類型特定設定
            if (column.DataType.ToLower() == "decimal" || column.DataType.ToLower() == "numeric")
            {
                sb.AppendLine($"        [Column(TypeName = \"decimal({column.Precision},{column.Scale})\")]");
            }

            // 特定欄位的驗證規則
            if (column.ColumnName.ToLower().Contains("email"))
            {
                sb.AppendLine("        [EmailAddress]");
            }
        }

        private List<ManyToManyRelationship> GetManyToManyRelationships(TableDefinition table)
        {
            var relationships = new List<ManyToManyRelationship>();

            foreach (var junctionTable in _tables.Where(t => t.IsManyToMany))
            {
                var fks = junctionTable.ForeignKeys;
                if (fks.Count != 2) continue;

                if (fks[0].PrimaryTable == table.TableName)
                {
                    relationships.Add(new ManyToManyRelationship
                    {
                        JunctionTable = junctionTable.TableName,
                        RelatedTable = fks[1].PrimaryTable
                    });
                }
                else if (fks[1].PrimaryTable == table.TableName)
                {
                    relationships.Add(new ManyToManyRelationship
                    {
                        JunctionTable = junctionTable.TableName,
                        RelatedTable = fks[0].PrimaryTable
                    });
                }
            }
            return relationships;
        }

        private string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, "(^|_)([a-z])", m => m.Groups[2].Value.ToUpper());
        }

        private string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) && !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }
            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("z", StringComparison.OrdinalIgnoreCase))
            {
                return name + "es";
            }
            return name + "s";
        }

        private bool IsVowel(char c)
        {
            return "aeiouAEIOU".IndexOf(c) >= 0;
        }

        private string GetPropertyType(ColumnDefinition column)
        {
            return column.DataType.ToLower() switch
            {
                "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "datetimeoffset" => "DateTimeOffset",
                "date" => "DateTime",
                "time" => "TimeSpan",
                "int" => "int",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" or "money" or "smallmoney" => "decimal",
                "float" => "double",
                "real" => "float",
                "uniqueidentifier" => "Guid",
                _ => "string"
            };
        }

        private bool IsValueType(string typeName)
        {
            return typeName switch
            {
                "int" or "long" or "short" or "byte" or "bool" or "decimal"
                or "float" or "double" or "DateTime" or "DateTimeOffset" or "Guid" => true,
                _ => false
            };
        }

        private void AppendXmlComment(StringBuilder sb, string comment, string indent)
        {
            var lines = comment.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                sb.AppendLine($"{indent}/// {SecurityElement.Escape(line.Trim())}");
            }
        }
    }
}
