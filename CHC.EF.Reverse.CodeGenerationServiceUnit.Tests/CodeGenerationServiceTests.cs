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
    /// ���� CodeGenerationService ���椸���աC
    /// </summary>
    /// <remarks>
    /// ���սd��]�t�G
    /// 1. �򥻥\�����
    /// 2. ���~�B�z����
    /// 3. ��ɱ������
    /// 4. �̩ۨʪ`�J����
    /// </remarks>
    public class CodeGenerationServiceTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IDatabaseSchemaReaderFactory> _schemaReaderFactoryMock;
        private readonly Mock<IDatabaseSchemaReader> _schemaReaderMock;
        private readonly Settings _settings;

        /// <summary>
        /// ��l�ƴ������ҩM��������C
        /// </summary>
        public CodeGenerationServiceTests()
        {
            // ��l�Ƽ�������
            _loggerMock = new Mock<ILogger>();
            _schemaReaderFactoryMock = new Mock<IDatabaseSchemaReaderFactory>();
            _schemaReaderMock = new Mock<IDatabaseSchemaReader>();

            // �]�w�򥻴��ճ]�w
            _settings = new Settings
            {
                ConnectionString = "Server=test;Database=testdb;",
                Namespace = "Test.Namespace",
                OutputDirectory = "./TestOutput",
                UseDataAnnotations = true,
                IsPluralize = true
            };

            // �t�m��������欰
            _schemaReaderFactoryMock
                .Setup(f => f.Create())
                .Returns(_schemaReaderMock.Object);
        }

        /// <summary>
        /// ���ըϥΦ��ĳ]�w�إߪA�ȹ�ҡC
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
        /// ���ըϥεL�ĳ]�w�إߪA�ȹ�ҡC
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
        /// ���ե��`����{���X�ͦ��C
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
        /// ���ո�ƮwŪ�����Ѫ����p�C
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
        /// ���տ�X�ؿ��Ыإ��Ѫ����p�C
        /// </summary>
        [Fact]
        public async Task Run_WhenOutputDirectoryCreationFails_ShouldThrowException()
        {
            // Arrange
            _settings.OutputDirectory = "Z:\\InvalidPath";  // �ϥεL�ĸ��|
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.Run());
        }


        /// <summary>
        /// �Ыش��եΪ� CodeGenerationService ��ҡC
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