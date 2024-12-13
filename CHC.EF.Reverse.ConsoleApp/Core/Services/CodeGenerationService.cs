using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Infrastructure.Generators;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Core.Services
{
    public class CodeGenerationService
    {
        private readonly Settings _settings;
        private readonly ILogger _logger;
        private readonly IDatabaseSchemaReaderFactory _schemaReaderFactory;

        public CodeGenerationService(
            IOptions<Settings> settings,
            ILogger logger,
            IDatabaseSchemaReaderFactory schemaReaderFactory)
        {
            _settings = settings.Value ?? throw new ArgumentNullException();
            _logger = logger;
            _schemaReaderFactory = schemaReaderFactory;
        }

        public async Task Run()
        {
            try
            {
                // 確保輸出目錄存在
                Directory.CreateDirectory(_settings.OutputDirectory);

                // 讀取資料庫結構
                var schemaReader = _schemaReaderFactory.Create();
                var tables = await schemaReader.ReadTables();
                _logger.Info($"讀取到 {tables.Count} 個資料表。");

                // 檢查並創建所需的子目錄
                var entityOutputDir = Path.Combine(_settings.OutputDirectory, "Entities");
                var configOutputDir = Path.Combine(_settings.OutputDirectory, "Configurations");
                Directory.CreateDirectory(entityOutputDir);
                Directory.CreateDirectory(configOutputDir);

                // 生成實體類
                var entityGenerator = new EntityGenerator(_settings, _logger, tables);
                await entityGenerator.GenerateAsync(tables);

                // 生成 DbContext
                if (_settings.ElementsToGenerate.Contains("DbContext"))
                {
                    var dbContextGenerator = new DbContextGenerator(_settings, _logger);
                    await dbContextGenerator.GenerateDbContextAsync(tables, _settings.OutputDirectory);
                }

                _logger.Info("所有程式碼產生完成。");
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("產生程式碼時發生錯誤", ex);
                throw;
            }
        }
    }
}
