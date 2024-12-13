using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Core.Services;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CHC.EF.Reverse.CodeGenerationServiceUnit.Tests
{
    /// <summary>
    /// 提供 CodeGenerationService 的單元測試。
    /// </summary>
    /// <remarks>
    /// 測試範圍包含：
    /// 1. 基本功能測試
    /// 2. 錯誤處理測試
    /// 3. 邊界條件測試
    /// 4. 相依性注入測試
    /// </remarks>
    public class CodeGenerationServiceTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IDatabaseSchemaReaderFactory> _schemaReaderFactoryMock;
        private readonly Mock<IDatabaseSchemaReader> _schemaReaderMock;
        private readonly Settings _settings;

        /// <summary>
        /// 初始化測試環境和模擬物件。
        /// </summary>
        public CodeGenerationServiceTests()
        {
            // 初始化模擬物件
            _loggerMock = new Mock<ILogger>();
            _schemaReaderFactoryMock = new Mock<IDatabaseSchemaReaderFactory>();
            _schemaReaderMock = new Mock<IDatabaseSchemaReader>();

            // 設定基本測試設定
            _settings = new Settings
            {
                ConnectionString = "Server=test;Database=testdb;",
                Namespace = "Test.Namespace",
                OutputDirectory = "./TestOutput",
                UseDataAnnotations = true,
                IsPluralize = true
            };

            // 配置模擬物件行為
            _schemaReaderFactoryMock
                .Setup(f => f.Create())
                .Returns(_schemaReaderMock.Object);
        }

        /// <summary>
        /// 測試使用有效設定建立服務實例。
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var service = CreateService();

            // Assert
            Assert.NotNull(service);
        }

        /// <summary>
        /// 測試使用無效設定建立服務實例。
        /// </summary>
        [Fact]
        public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            Settings nullSettings = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CodeGenerationService(
                Options.Create(nullSettings),
                _loggerMock.Object,
                _schemaReaderFactoryMock.Object));
        }

        /// <summary>
        /// 測試正常執行程式碼生成。
        /// </summary>
        [Fact]
        public async Task Run_WithValidConfiguration_ShouldGenerateCode()
        {
            // Arrange
            var tables = new List<TableDefinition>
            {
                new TableDefinition
                {
                    TableName = "TestTable",
                    SchemaName = "dbo",
                    Columns = new List<ColumnDefinition>
                    {
                        new ColumnDefinition
                        {
                            ColumnName = "Id",
                            DataType = "int",
                            IsPrimaryKey = true,
                            IsIdentity = true
                        }
                    }
                }
            };

            _schemaReaderMock
                .Setup(r => r.ReadTables())
                .ReturnsAsync(tables);

            var service = CreateService();

            // Act
            await service.Run();

            // Assert
            _loggerMock.Verify(l => l.Info(It.IsAny<string>()), Times.AtLeast(1));
            _schemaReaderMock.Verify(r => r.ReadTables(), Times.Once);
        }

        /// <summary>
        /// 測試資料庫讀取失敗的情況。
        /// </summary>
        [Fact]
        public async Task Run_WhenDatabaseReadFails_ShouldLogErrorAndThrowException()
        {
            // Arrange
            var expectedException = new Exception("Database connection failed");
            _schemaReaderMock
                .Setup(r => r.ReadTables())
                .ThrowsAsync(expectedException);

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.Run());
            _loggerMock.Verify(
                l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()),
                Times.Once);
        }


        /// <summary>
        /// 測試輸出目錄創建失敗的情況。
        /// </summary>
        [Fact]
        public async Task Run_WhenOutputDirectoryCreationFails_ShouldThrowException()
        {
            // Arrange
            _settings.OutputDirectory = "Z:\\InvalidPath";  // 使用無效路徑
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.Run());
        }


        /// <summary>
        /// 創建測試用的 CodeGenerationService 實例。
        /// </summary>
        private CodeGenerationService CreateService()
        {
            return new CodeGenerationService(
                Options.Create(_settings),
                _loggerMock.Object,
                _schemaReaderFactoryMock.Object);
        }
    }
}