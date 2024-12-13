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
    /// 提供 EntityGenerator 的單元測試。
    /// </summary>
    /// <remarks>
    /// 測試範圍包含：
    /// - 實體類別產生功能
    /// - 關聯處理邏輯
    /// - 錯誤處理機制
    /// - 設定驗證邏輯
    /// </remarks>
    public class EntityGeneratorTests : IDisposable
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Settings _settings;
        private readonly string _testOutputPath;
        private readonly List<TableDefinition> _tables;

        /// <summary>
        /// 初始化測試環境和必要的模擬物件。
        /// </summary>
        public EntityGeneratorTests()
        {
            // 初始化模擬物件
            _loggerMock = new Mock<ILogger>();

            // 設定測試輸出路徑
            _testOutputPath = Path.Combine(Path.GetTempPath(), "EntityGeneratorTests");
            Directory.CreateDirectory(_testOutputPath);

            // 初始化基本設定
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

            // 準備測試用資料表定義
            _tables = CreateTestTables();
        }

        /// <summary>
        /// 測試使用有效設定建立 EntityGenerator 實例。
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
        /// 測試使用空設定建立 EntityGenerator 實例時的異常處理。
        /// </summary>
        [Fact]
        public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EntityGenerator(null, _loggerMock.Object, _tables));
        }

        /// <summary>
        /// 測試產生實體類別的主要功能。
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
        /// 資源清理。
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
                _loggerMock.Object.Error("清理測試資源時發生錯誤", ex);
            }
        }

        #region Helper Methods

        /// <summary>
        /// 建立測試用的 EntityGenerator 實例。
        /// </summary>
        private EntityGenerator CreateEntityGenerator()
        {
            return new EntityGenerator(_settings, _loggerMock.Object, _tables);
        }

        /// <summary>
        /// 建立測試用的資料表定義集合。
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
        /// 建立測試用的使用者資料表定義。
        /// </summary>
        private TableDefinition CreateTestUserTable()
        {
            return new TableDefinition
            {
                TableName = "User",
                SchemaName = "dbo",
                Comment = "使用者資料表",
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition
                    {
                        ColumnName = "Id",
                        DataType = "int",
                        IsPrimaryKey = true,
                        IsIdentity = true,
                        Comment = "使用者編號"
                    },
                    new ColumnDefinition
                    {
                        ColumnName = "Name",
                        DataType = "nvarchar",
                        MaxLength = 50,
                        IsNullable = false,
                        Comment = "使用者名稱"
                    }
                }
            };
        }

        /// <summary>
        /// 建立測試用的訂單資料表定義。
        /// </summary>
        private TableDefinition CreateTestOrderTable()
        {
            return new TableDefinition
            {
                TableName = "Order",
                SchemaName = "dbo",
                Comment = "訂單資料表",
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition
                    {
                        ColumnName = "Id",
                        DataType = "int",
                        IsPrimaryKey = true,
                        IsIdentity = true,
                        Comment = "訂單編號"
                    },
                    new ColumnDefinition
                    {
                        ColumnName = "UserId",
                        DataType = "int",
                        IsNullable = false,
                        Comment = "使用者編號"
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
        /// 建立具有一對多關聯的測試資料表集合。
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