using Xunit;
using Microsoft.Extensions.Options;
using System;
using Moq;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Infrastructure.Databases;
using CHC.EF.Reverse.Poco.Core.Interfaces;

namespace CHC.EF.Reverse.Poco.Tests
{
    public class DatabaseSchemaReaderFactoryTests
    {
        private readonly Mock<IOptions<Settings>> _mockSettings;
        private readonly Mock<ILogger> _loggerMock;

        public DatabaseSchemaReaderFactoryTests()
        {
            _mockSettings = new Mock<IOptions<Settings>>();
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public void Create_WithMySqlProvider_ReturnsMySqlReader()
        {
            // Arrange
            _mockSettings.Setup(x => x.Value).Returns(new Settings
            {
                ProviderName = "MySql.Data.MySqlClient",
                ConnectionString = "Server=localhost;Database=test;Uid=root;Pwd=password;"
            });

            var factory = new DatabaseSchemaReaderFactory(_mockSettings.Object, _loggerMock.Object);

            // Act
            var reader = factory.Create();

            // Assert
            Assert.IsType<MySqlSchemaReader>(reader);
        }

        [Fact]
        public void Create_WithSqlServerProvider_ReturnsSqlServerReader()
        {
            // Arrange
            _mockSettings.Setup(x => x.Value).Returns(new Settings
            {
                ProviderName = "Microsoft.Data.SqlClient",
                ConnectionString = "Server=localhost;Database=test;Trusted_Connection=True;"
            });

            var factory = new DatabaseSchemaReaderFactory(_mockSettings.Object,_loggerMock.Object);

            // Act
            var reader = factory.Create();

            // Assert
            Assert.IsType<SqlServerSchemaReader>(reader);
        }

        [Fact]
        public void Create_WithUnsupportedProvider_ThrowsNotSupportedException()
        {
            // Arrange
            _mockSettings.Setup(x => x.Value).Returns(new Settings
            {
                ProviderName = "UnsupportedProvider",
                ConnectionString = "connection string"
            });

            var factory = new DatabaseSchemaReaderFactory(_mockSettings.Object, _loggerMock.Object);

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => factory.Create());
            Assert.Contains("不支援的資料庫提供者", exception.Message);
        }
    }
}