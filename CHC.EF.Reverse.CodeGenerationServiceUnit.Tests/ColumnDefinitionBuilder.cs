using CHC.EF.Reverse.Poco.Core.Models;
using System;

namespace CHC.EF.Reverse.Poco.Tests.TestData
{
    /// <summary>
    /// ���ѫإ� <see cref="ColumnDefinition"/> ���ո�ƪ��y�Z�����غc���C
    /// </summary>
    /// <remarks>
    /// ���غc���䴩�Ҧ� ColumnDefinition �ݩʪ��]�w�A�ô����즡��k�I�s�C
    /// �ϥνd��:
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
        /// ��l�����w�q�غc�����s�������C
        /// </summary>
        public ColumnDefinitionBuilder()
        {
            _column = new ColumnDefinition
            {
                ParticipatingIndexes = new List<IndexDefinition>()
            };
        }

        /// <summary>
        /// �]�w���W�١C
        /// </summary>
        /// <param name="name">���W��</param>
        /// <returns>�ثe���غc�����</returns>
        /// <exception cref="ArgumentNullException">�� name �� null �Ϊťծ��Y�^</exception>
        public ColumnDefinitionBuilder WithName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "���W�٤��i����");

            _column.ColumnName = name;
            return this;
        }

        /// <summary>
        /// �]�w����ƫ��O�C
        /// </summary>
        /// <param name="dataType">��ƫ��O�W��</param>
        /// <returns>�ثe���غc�����</returns>
        /// <exception cref="ArgumentNullException">�� dataType �� null �Ϊťծ��Y�^</exception>
        public ColumnDefinitionBuilder AsType(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                throw new ArgumentNullException(nameof(dataType), "��ƫ��O���i����");

            _column.DataType = dataType;
            return this;
        }

        /// <summary>
        /// �N���]�w���D��C
        /// </summary>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder AsPrimaryKey()
        {
            _column.IsPrimaryKey = true;
            return this;
        }

        /// <summary>
        /// �N���]�w���۰ʻ��W�C
        /// </summary>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder AsIdentity()
        {
            _column.IsIdentity = true;
            return this;
        }

        /// <summary>
        /// �N���]�w������C
        /// </summary>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder AsRequired()
        {
            _column.IsNullable = false;
            return this;
        }

        /// <summary>
        /// �]�w��쪺�̤j���סC
        /// </summary>
        /// <param name="length">�̤j���׭�</param>
        /// <returns>�ثe���غc�����</returns>
        /// <exception cref="ArgumentOutOfRangeException">�� length �p��ε��� 0 ���Y�^</exception>
        public ColumnDefinitionBuilder WithMaxLength(long length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "���ץ����j�� 0");

            _column.MaxLength = length;
            return this;
        }

        /// <summary>
        /// �]�w�ƭȫ��O����T�סC
        /// </summary>
        /// <param name="precision">��T��</param>
        /// <param name="scale">�p�Ʀ��</param>
        /// <returns>�ثe���غc�����</returns>
        /// <exception cref="ArgumentOutOfRangeException">�� precision �� scale ���ȵL�Į��Y�^</exception>
        public ColumnDefinitionBuilder WithPrecision(int precision, int scale)
        {
            if (precision <= 0)
                throw new ArgumentOutOfRangeException(nameof(precision), "��T�ץ����j�� 0");
            if (scale < 0 || scale > precision)
                throw new ArgumentOutOfRangeException(nameof(scale), "�p�Ʀ�ƥ����j�󵥩� 0 �B���j���T��");

            _column.Precision = precision;
            _column.Scale = scale;
            return this;
        }

        /// <summary>
        /// �]�w��쪺�w�]�ȡC
        /// </summary>
        /// <param name="defaultValue">�w�]�Ȫ�F��</param>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder WithDefaultValue(string defaultValue)
        {
            _column.DefaultValue = defaultValue;
            return this;
        }

        /// <summary>
        /// �]�w��쪺�y�z���ѡC
        /// </summary>
        /// <param name="comment">�y�z��r</param>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder WithComment(string comment)
        {
            _column.Comment = comment;
            return this;
        }

        /// <summary>
        /// �]�w��쪺�w�ǳW�h�C
        /// </summary>
        /// <param name="collationType">�w�ǳW�h�W��</param>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder WithCollation(string collationType)
        {
            _column.CollationType = collationType;
            return this;
        }

        /// <summary>
        /// �N���]�w���p�����C
        /// </summary>
        /// <param name="definition">�p�����w�q��</param>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder AsComputed(string definition)
        {
            _column.IsComputed = true;
            _column.ComputedColumnDefinition = definition;
            return this;
        }

        /// <summary>
        /// �N���]�w���ɶ��W�O���C
        /// </summary>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder AsRowVersion()
        {
            _column.IsRowVersion = true;
            return this;
        }

        /// <summary>
        /// �]�w��쪺�ͦ������C
        /// </summary>
        /// <param name="generatedType">�ͦ������]ALWAYS/COMPUTED�^</param>
        /// <returns>�ثe���غc�����</returns>
        public ColumnDefinitionBuilder WithGeneratedType(string generatedType)
        {
            _column.GeneratedType = generatedType;
            return this;
        }

        /// <summary>
        /// �إ����w�q��ҡC
        /// </summary>
        /// <returns>���㪺���w�q</returns>
        /// <exception cref="InvalidOperationException">���n�ݩʥ��]�w���Y�^</exception>
        public ColumnDefinition Build()
        {
            ValidateColumn();
            return _column;
        }

        /// <summary>
        /// �������w�q������ʡC
        /// </summary>
        /// <exception cref="InvalidOperationException">���n�ݩʥ��]�w���Y�^</exception>
        private void ValidateColumn()
        {
            if (string.IsNullOrWhiteSpace(_column.ColumnName))
                throw new InvalidOperationException("���W�٬����n�ݩ�");

            if (string.IsNullOrWhiteSpace(_column.DataType))
                throw new InvalidOperationException("��ƫ��O�����n�ݩ�");

            // �ƭȫ��O����T������
            if (_column.DataType.ToLowerInvariant() is "decimal" or "numeric")
            {
                if (!_column.Precision.HasValue)
                    throw new InvalidOperationException("�ƭȫ��O�������w��T��");
            }

            // �r�ꫬ�O����������
            if (_column.DataType.ToLowerInvariant() is "varchar" or "nvarchar" or "char" or "nchar")
            {
                if (!_column.MaxLength.HasValue)
                    throw new InvalidOperationException("�r�ꫬ�O�������w�̤j����");
            }
        }
    }
}