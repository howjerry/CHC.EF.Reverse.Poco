using CHC.EF.Reverse.Poco.Core.Models;
using System;

namespace CHC.EF.Reverse.Poco.Tests.TestData
{
    /// <summary>
    /// 提供建立 <see cref="ColumnDefinition"/> 測試資料的流暢介面建構器。
    /// </summary>
    /// <remarks>
    /// 此建構器支援所有 ColumnDefinition 屬性的設定，並提供鏈式方法呼叫。
    /// 使用範例:
    /// <code>
    /// var column = new ColumnDefinitionBuilder()
    ///     .WithName("Id")
    ///     .AsType("int")
    ///     .AsPrimaryKey()
    ///     .AsIdentity()
    ///     .Build();
    /// </code>
    /// </remarks>
    public class ColumnDefinitionBuilder
    {
        private readonly ColumnDefinition _column;

        /// <summary>
        /// 初始化欄位定義建構器的新執行個體。
        /// </summary>
        public ColumnDefinitionBuilder()
        {
            _column = new ColumnDefinition
            {
                ParticipatingIndexes = new List<IndexDefinition>()
            };
        }

        /// <summary>
        /// 設定欄位名稱。
        /// </summary>
        /// <param name="name">欄位名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 name 為 null 或空白時擲回</exception>
        public ColumnDefinitionBuilder WithName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "欄位名稱不可為空");

            _column.ColumnName = name;
            return this;
        }

        /// <summary>
        /// 設定欄位資料型別。
        /// </summary>
        /// <param name="dataType">資料型別名稱</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentNullException">當 dataType 為 null 或空白時擲回</exception>
        public ColumnDefinitionBuilder AsType(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                throw new ArgumentNullException(nameof(dataType), "資料型別不可為空");

            _column.DataType = dataType;
            return this;
        }

        /// <summary>
        /// 將欄位設定為主鍵。
        /// </summary>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder AsPrimaryKey()
        {
            _column.IsPrimaryKey = true;
            return this;
        }

        /// <summary>
        /// 將欄位設定為自動遞增。
        /// </summary>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder AsIdentity()
        {
            _column.IsIdentity = true;
            return this;
        }

        /// <summary>
        /// 將欄位設定為必填。
        /// </summary>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder AsRequired()
        {
            _column.IsNullable = false;
            return this;
        }

        /// <summary>
        /// 設定欄位的最大長度。
        /// </summary>
        /// <param name="length">最大長度值</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentOutOfRangeException">當 length 小於或等於 0 時擲回</exception>
        public ColumnDefinitionBuilder WithMaxLength(long length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "長度必須大於 0");

            _column.MaxLength = length;
            return this;
        }

        /// <summary>
        /// 設定數值型別的精確度。
        /// </summary>
        /// <param name="precision">精確度</param>
        /// <param name="scale">小數位數</param>
        /// <returns>目前的建構器實例</returns>
        /// <exception cref="ArgumentOutOfRangeException">當 precision 或 scale 的值無效時擲回</exception>
        public ColumnDefinitionBuilder WithPrecision(int precision, int scale)
        {
            if (precision <= 0)
                throw new ArgumentOutOfRangeException(nameof(precision), "精確度必須大於 0");
            if (scale < 0 || scale > precision)
                throw new ArgumentOutOfRangeException(nameof(scale), "小數位數必須大於等於 0 且不大於精確度");

            _column.Precision = precision;
            _column.Scale = scale;
            return this;
        }

        /// <summary>
        /// 設定欄位的預設值。
        /// </summary>
        /// <param name="defaultValue">預設值表達式</param>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder WithDefaultValue(string defaultValue)
        {
            _column.DefaultValue = defaultValue;
            return this;
        }

        /// <summary>
        /// 設定欄位的描述註解。
        /// </summary>
        /// <param name="comment">描述文字</param>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder WithComment(string comment)
        {
            _column.Comment = comment;
            return this;
        }

        /// <summary>
        /// 設定欄位的定序規則。
        /// </summary>
        /// <param name="collationType">定序規則名稱</param>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder WithCollation(string collationType)
        {
            _column.CollationType = collationType;
            return this;
        }

        /// <summary>
        /// 將欄位設定為計算欄位。
        /// </summary>
        /// <param name="definition">計算欄位定義式</param>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder AsComputed(string definition)
        {
            _column.IsComputed = true;
            _column.ComputedColumnDefinition = definition;
            return this;
        }

        /// <summary>
        /// 將欄位設定為時間戳記欄位。
        /// </summary>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder AsRowVersion()
        {
            _column.IsRowVersion = true;
            return this;
        }

        /// <summary>
        /// 設定欄位的生成類型。
        /// </summary>
        /// <param name="generatedType">生成類型（ALWAYS/COMPUTED）</param>
        /// <returns>目前的建構器實例</returns>
        public ColumnDefinitionBuilder WithGeneratedType(string generatedType)
        {
            _column.GeneratedType = generatedType;
            return this;
        }

        /// <summary>
        /// 建立欄位定義實例。
        /// </summary>
        /// <returns>完整的欄位定義</returns>
        /// <exception cref="InvalidOperationException">當必要屬性未設定時擲回</exception>
        public ColumnDefinition Build()
        {
            ValidateColumn();
            return _column;
        }

        /// <summary>
        /// 驗證欄位定義的完整性。
        /// </summary>
        /// <exception cref="InvalidOperationException">當必要屬性未設定時擲回</exception>
        private void ValidateColumn()
        {
            if (string.IsNullOrWhiteSpace(_column.ColumnName))
                throw new InvalidOperationException("欄位名稱為必要屬性");

            if (string.IsNullOrWhiteSpace(_column.DataType))
                throw new InvalidOperationException("資料型別為必要屬性");

            // 數值型別的精確度驗證
            if (_column.DataType.ToLowerInvariant() is "decimal" or "numeric")
            {
                if (!_column.Precision.HasValue)
                    throw new InvalidOperationException("數值型別必須指定精確度");
            }

            // 字串型別的長度驗證
            if (_column.DataType.ToLowerInvariant() is "varchar" or "nvarchar" or "char" or "nchar")
            {
                if (!_column.MaxLength.HasValue)
                    throw new InvalidOperationException("字串型別必須指定最大長度");
            }
        }
    }
}