using CHC.EF.Reverse.Poco.Core.Models;
using System;
using System.Collections.Generic;

namespace CHC.EF.Reverse.Poco.Tests.TestData
{
    /// <summary>
    /// 提供建立 <see cref="ForeignKeyDefinition"/> 測試資料的流暢介面建構器。
    /// </summary>
    /// <remarks>
    /// 此建構器支援建立完整的外鍵定義，包含：
    /// - 外鍵名稱與欄位對應
    /// - 參考的主表與主鍵欄位
    /// - 外鍵約束規則（刪除、更新）
    /// - 複合外鍵的欄位配對
    /// 
    /// 使用範例：
    /// <code>
    /// var foreignKey = new ForeignKeyDefinitionBuilder()
    ///     .WithName("FK_Orders_Customers")
    ///     .WithColumn("CustomerId")
    ///     .ReferencingTable("Customers")
    ///     .ReferencingColumn("Id")
    ///     .WithDeleteRule("CASCADE")
    ///     .Build();
    /// </code>
    /// </remarks>
    public class ForeignKeyDefinitionBuilder
    {
        private readonly ForeignKeyDefinition _foreignKey;

        /// <summary>
        /// 初始化外鍵定義建構器的新執行個體。
        /// </summary>
        public ForeignKeyDefinitionBuilder()
        {
            _foreignKey = new ForeignKeyDefinition
            {
                ColumnPairs = new List<ForeignKeyColumnPair>(),
                IsEnabled = true
            };
        }

        /// <summary>
        /// 設定外鍵約束名稱。
        /// </summary>
        /// <param name="name">約束名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 name 為 null 或空白時擲回</exception>
        /// <remarks>
        /// 約束名稱應遵循資料庫命名規範，建議使用有意義的前綴，如 "FK_子表_主表"。
        /// </remarks>
        public ForeignKeyDefinitionBuilder WithName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "約束名稱不可為空");

            _foreignKey.ConstraintName = name;
            return this;
        }

        /// <summary>
        /// 設定外鍵欄位。
        /// </summary>
        /// <param name="columnName">外鍵欄位名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 columnName 為 null 或空白時擲回</exception>
        /// <remarks>
        /// 此方法設定參考表中的外鍵欄位。對於複合外鍵，應多次呼叫此方法。
        /// </remarks>
        public ForeignKeyDefinitionBuilder WithColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName), "欄位名稱不可為空");

            _foreignKey.ForeignKeyColumn = columnName;
            return this;
        }

        /// <summary>
        /// 設定參考的主表名稱。
        /// </summary>
        /// <param name="tableName">主表名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 tableName 為 null 或空白時擲回</exception>
        public ForeignKeyDefinitionBuilder ReferencingTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName), "主表名稱不可為空");

            _foreignKey.PrimaryTable = tableName;
            return this;
        }

        /// <summary>
        /// 設定參考的主鍵欄位。
        /// </summary>
        /// <param name="columnName">主鍵欄位名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 columnName 為 null 或空白時擲回</exception>
        public ForeignKeyDefinitionBuilder ReferencingColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName), "主鍵欄位名稱不可為空");

            _foreignKey.PrimaryKeyColumn = columnName;

            // 同時新增到欄位配對集合
            _foreignKey.ColumnPairs.Add(new ForeignKeyColumnPair
            {
                ForeignKeyColumn = _foreignKey.ForeignKeyColumn,
                PrimaryKeyColumn = columnName
            });

            return this;
        }

        /// <summary>
        /// 設定刪除規則。
        /// </summary>
        /// <param name="rule">刪除規則（NO ACTION/CASCADE/SET NULL/SET DEFAULT）</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentException">當規則值無效時擲回</exception>
        public ForeignKeyDefinitionBuilder WithDeleteRule(string rule)
        {
            ValidateRule(rule, "刪除規則");
            _foreignKey.DeleteRule = rule;
            return this;
        }

        /// <summary>
        /// 設定更新規則。
        /// </summary>
        /// <param name="rule">更新規則（NO ACTION/CASCADE/SET NULL/SET DEFAULT）</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentException">當規則值無效時擲回</exception>
        public ForeignKeyDefinitionBuilder WithUpdateRule(string rule)
        {
            ValidateRule(rule, "更新規則");
            _foreignKey.UpdateRule = rule;
            return this;
        }

        /// <summary>
        /// 設定外鍵是否啟用。
        /// </summary>
        /// <param name="isEnabled">是否啟用外鍵約束</param>
        /// <returns>目前的建構器實例</returns>
        public ForeignKeyDefinitionBuilder IsEnabled(bool isEnabled)
        {
            _foreignKey.IsEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// 新增複合外鍵的欄位配對。
        /// </summary>
        /// <param name="foreignKeyColumn">外鍵欄位名稱</param>
        /// <param name="primaryKeyColumn">主鍵欄位名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當任一參數為 null 或空白時擲回</exception>
        /// <remarks>
        /// 用於建立複合外鍵時，定義多個欄位之間的對應關係。
        /// </remarks>
        public ForeignKeyDefinitionBuilder WithColumnPair(string foreignKeyColumn, string primaryKeyColumn)
        {
            if (string.IsNullOrWhiteSpace(foreignKeyColumn))
                throw new ArgumentNullException(nameof(foreignKeyColumn), "外鍵欄位名稱不可為空");
            if (string.IsNullOrWhiteSpace(primaryKeyColumn))
                throw new ArgumentNullException(nameof(primaryKeyColumn), "主鍵欄位名稱不可為空");

            _foreignKey.ColumnPairs.Add(new ForeignKeyColumnPair
            {
                ForeignKeyColumn = foreignKeyColumn,
                PrimaryKeyColumn = primaryKeyColumn
            });

            _foreignKey.IsCompositeKey = _foreignKey.ColumnPairs.Count > 1;
            return this;
        }

        /// <summary>
        /// 設定註解說明。
        /// </summary>
        /// <param name="comment">註解文字</param>
        /// <returns>目前的建構器實例</returns>
        public ForeignKeyDefinitionBuilder WithComment(string comment)
        {
            _foreignKey.Comment = comment;
            return this;
        }

        /// <summary>
        /// 建立外鍵定義實例。
        /// </summary>
        /// <returns>完整的外鍵定義</returns>
        /// <exception cref="InvalidOperationException">當必要屬性未設定時擲回</exception>
        public ForeignKeyDefinition Build()
        {
            ValidateForeignKey();
            return _foreignKey;
        }

        /// <summary>
        /// 驗證規則值的有效性。
        /// </summary>
        /// <param name="rule">規則值</param>
        /// <param name="ruleType">規則類型描述</param>
        /// <exception cref="ArgumentException">當規則值無效時擲回</exception>
        private void ValidateRule(string rule, string ruleType)
        {
            if (string.IsNullOrWhiteSpace(rule))
                throw new ArgumentNullException(nameof(rule), $"{ruleType}不可為空");

            var validRules = new[] { "NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT" };
            if (!validRules.Contains(rule, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"無效的{ruleType}：{rule}", nameof(rule));
        }

        /// <summary>
        /// 驗證外鍵定義的完整性。
        /// </summary>
        /// <exception cref="InvalidOperationException">當必要屬性未設定時擲回</exception>
        private void ValidateForeignKey()
        {
            if (string.IsNullOrWhiteSpace(_foreignKey.ConstraintName))
                throw new InvalidOperationException("約束名稱為必要屬性");

            if (string.IsNullOrWhiteSpace(_foreignKey.PrimaryTable))
                throw new InvalidOperationException("參考的主表名稱為必要屬性");

            if (!_foreignKey.ColumnPairs.Any())
                throw new InvalidOperationException("至少需要一個欄位配對");

            // 檢查複合外鍵的完整性
            if (_foreignKey.IsCompositeKey)
            {
                var foreignKeyColumns = _foreignKey.ColumnPairs.Select(cp => cp.ForeignKeyColumn).Distinct();
                var primaryKeyColumns = _foreignKey.ColumnPairs.Select(cp => cp.PrimaryKeyColumn).Distinct();

                if (foreignKeyColumns.Count() != _foreignKey.ColumnPairs.Count ||
                    primaryKeyColumns.Count() != _foreignKey.ColumnPairs.Count)
                {
                    throw new InvalidOperationException("複合外鍵中的欄位配對必須是唯一的");
                }
            }
        }
    }
}