using CHC.EF.Reverse.Poco.Core.Models;

namespace CHC.EF.Reverse.Poco.Tests.TestData
{
    /// <summary>
    /// 提供測試用資料表定義的流暢介面建構器。
    /// </summary>
    public class TableDefinitionBuilder
    {
        private readonly TableDefinition _table;

        /// <summary>
        /// 初始化資料表定義建構器。
        /// </summary>
        public TableDefinitionBuilder()
        {
            _table = new TableDefinition
            {
                Columns = new List<ColumnDefinition>(),
                ForeignKeys = new List<ForeignKeyDefinition>(),
                Indexes = new List<IndexDefinition>()
            };
        }

        /// <summary>
        /// 設定資料表名稱。
        /// </summary>
        /// <param name="name">資料表名稱</param>
        /// <returns>建構器實例</returns>
        public TableDefinitionBuilder WithName(string name)
        {
            _table.TableName = name;
            return this;
        }

        /// <summary>
        /// 新增欄位定義。
        /// </summary>
        /// <param name="columnBuilder">欄位建構動作</param>
        /// <returns>建構器實例</returns>
        public TableDefinitionBuilder WithColumn(
            Action<ColumnDefinitionBuilder> columnBuilder)
        {
            var builder = new ColumnDefinitionBuilder();
            columnBuilder(builder);
            _table.Columns.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// 新增外鍵定義。
        /// </summary>
        /// <param name="foreignKeyBuilder">外鍵建構動作</param>
        /// <returns>建構器實例</returns>
        public TableDefinitionBuilder WithForeignKey(
            Action<ForeignKeyDefinitionBuilder> foreignKeyBuilder)
        {
            var builder = new ForeignKeyDefinitionBuilder();
            foreignKeyBuilder(builder);
            _table.ForeignKeys.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// 建立資料表定義實例。
        /// </summary>
        /// <returns>完整的資料表定義</returns>
        public TableDefinition Build()
        {
            // 驗證必要欄位
            if (string.IsNullOrEmpty(_table.TableName))
                throw new InvalidOperationException("資料表名稱為必要欄位");

            return _table;
        }
    }
}