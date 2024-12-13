using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Core.Services;
using CHC.EF.Reverse.Poco.Infrastructure.Generators;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CHC.EF.Reverse.Poco.Tests.Infrastructure.Generators
{
    /// <summary>
    /// ���� EntityGenerator ���椸���աC
    /// </summary>
    /// <remarks>
    /// ���սd��]�t�G
    /// - �������O���ͥ\��
    /// - ���p�B�z�޿�
    /// - ���~�B�z����
    /// - �]�w�����޿�
    /// </remarks>
    public class EntityGeneratorTests : IDisposable
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Settings _settings;
        private readonly string _testOutputPath;
        private readonly List<TableDefinition> _tables;

        /// <summary>
        /// ��l�ƴ������ҩM���n����������C
        /// </summary>
        public EntityGeneratorTests()
        {
            // ��l�Ƽ�������
            _loggerMock = new Mock<ILogger>();

            // �]�w���տ�X���|
            _testOutputPath = Path.Combine(Path.GetTempPath(), "EntityGeneratorTests");
            Directory.CreateDirectory(_testOutputPath);

            // ��l�ư򥻳]�w
            _settings = new Settings
            {
                ConnectionString = "Server=test;Database=testdb;",
                Namespace = "Test.Namespace",
                OutputDirectory = _testOutputPath,
                UseDataAnnotations = true,
                IsPluralize = true,
                IncludeComments = true,
                ElementsToGenerate = new List<string> { "POCO", "Configuration", "DbContext" }
            };

            // �ǳƴ��եθ�ƪ�w�q
            _tables = CreateTestTables();
        }

        /// <summary>
        /// ���ըϥΦ��ĳ]�w�إ� EntityGenerator ��ҡC
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var generator = CreateEntityGenerator();

            // Assert
            Assert.NotNull(generator);
        }

        /// <summary>
        /// ���ըϥΪų]�w�إ� EntityGenerator ��Үɪ����`�B�z�C
        /// </summary>
        [Fact]
        public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EntityGenerator(null, _loggerMock.Object, _tables));
        }

        /// <summary>
        /// ���ղ��͹������O���D�n�\��C
        /// </summary>
        [Fact]
        public async Task GenerateAsync_WithValidTable_ShouldGenerateEntityClass()
        {
            // Arrange
            var generator = CreateEntityGenerator();
            var tables = new List<TableDefinition> { CreateTestUserTable() };

            // Act
            await generator.GenerateAsync(tables);

            // Assert
            var entityFilePath = Path.Combine(_testOutputPath, "Entities", "User.cs");
            Assert.True(File.Exists(entityFilePath));
            var content = await File.ReadAllTextAsync(entityFilePath);
            Assert.Contains("public class User", content);
            Assert.Contains("public int Id { get; set; }", content);
        }

        /// <summary>
        /// �귽�M�z�C
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testOutputPath))
                {
                    Directory.Delete(_testOutputPath, true);
                }
            }
            catch (Exception ex)
            {
                _loggerMock.Object.Error("�M�z���ո귽�ɵo�Ϳ��~", ex);
            }
        }

        #region Helper Methods

        /// <summary>
        /// �إߴ��եΪ� EntityGenerator ��ҡC
        /// </summary>
        private EntityGenerator CreateEntityGenerator()
        {
            return new EntityGenerator(_settings, _loggerMock.Object, _tables);
        }

        /// <summary>
        /// �إߴ��եΪ���ƪ�w�q���X�C
        /// </summary>
        private List<TableDefinition> CreateTestTables()
        {
            return new List<TableDefinition>
            {
                CreateTestUserTable(),
                CreateTestOrderTable()
            };
        }

        /// <summary>
        /// �إߴ��եΪ��ϥΪ̸�ƪ�w�q�C
        /// </summary>
        private TableDefinition CreateTestUserTable()
        {
            return new TableDefinition
            {
                TableName = "User",
                SchemaName = "dbo",
                Comment = "�ϥΪ̸�ƪ�",
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition
                    {
                        ColumnName = "Id",
                        DataType = "int",
                        IsPrimaryKey = true,
                        IsIdentity = true,
                        Comment = "�ϥΪ̽s��"
                    },
                    new ColumnDefinition
                    {
                        ColumnName = "Name",
                        DataType = "nvarchar",
                        MaxLength = 50,
                        IsNullable = false,
                        Comment = "�ϥΪ̦W��"
                    }
                }
            };
        }

        /// <summary>
        /// �إߴ��եΪ��q���ƪ�w�q�C
        /// </summary>
        private TableDefinition CreateTestOrderTable()
        {
            return new TableDefinition
            {
                TableName = "Order",
                SchemaName = "dbo",
                Comment = "�q���ƪ�",
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition
                    {
                        ColumnName = "Id",
                        DataType = "int",
                        IsPrimaryKey = true,
                        IsIdentity = true,
                        Comment = "�q��s��"
                    },
                    new ColumnDefinition
                    {
                        ColumnName = "UserId",
                        DataType = "int",
                        IsNullable = false,
                        Comment = "�ϥΪ̽s��"
                    }
                },
                ForeignKeys = new List<ForeignKeyDefinition>
                {
                    new ForeignKeyDefinition
                    {
                        ConstraintName = "FK_Order_User",
                        ForeignKeyColumn = "UserId",
                        PrimaryTable = "User",
                        PrimaryKeyColumn = "Id"
                    }
                }
            };
        }

        /// <summary>
        /// �إߨ㦳�@��h���p�����ո�ƪ��X�C
        /// </summary>
        private List<TableDefinition> CreateTestTablesWithOneToManyRelationship()
        {
            var user = CreateTestUserTable();
            var order = CreateTestOrderTable();
            return new List<TableDefinition> { user, order };
        }

        #endregion
    }
}