using CHC.EF.Reverse.Poco.Core.Models;

namespace CHC.EF.Reverse.Poco.Tests.TestData
{
    /// <summary>
    /// ���Ѵ��եθ�ƪ�w�q���y�Z�����غc���C
    /// </summary>
    public class TableDefinitionBuilder
    {
        private readonly TableDefinition _table;

        /// <summary>
        /// ��l�Ƹ�ƪ�w�q�غc���C
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
        /// �]�w��ƪ�W�١C
        /// </summary>
        /// <param name="name">��ƪ�W��</param>
        /// <returns>�غc�����</returns>
        public TableDefinitionBuilder WithName(string name)
        {
            _table.TableName = name;
            return this;
        }

        /// <summary>
        /// �s�W���w�q�C
        /// </summary>
        /// <param name="columnBuilder">���غc�ʧ@</param>
        /// <returns>�غc�����</returns>
        public TableDefinitionBuilder WithColumn(
            Action<ColumnDefinitionBuilder> columnBuilder)
        {
            var builder = new ColumnDefinitionBuilder();
            columnBuilder(builder);
            _table.Columns.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// �s�W�~��w�q�C
        /// </summary>
        /// <param name="foreignKeyBuilder">�~��غc�ʧ@</param>
        /// <returns>�غc�����</returns>
        public TableDefinitionBuilder WithForeignKey(
            Action<ForeignKeyDefinitionBuilder> foreignKeyBuilder)
        {
            var builder = new ForeignKeyDefinitionBuilder();
            foreignKeyBuilder(builder);
            _table.ForeignKeys.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// �إ߸�ƪ�w�q��ҡC
        /// </summary>
        /// <returns>���㪺��ƪ�w�q</returns>
        public TableDefinition Build()
        {
            // ���ҥ��n���
            if (string.IsNullOrEmpty(_table.TableName))
                throw new InvalidOperationException("��ƪ�W�٬����n���");

            return _table;
        }
    }
}