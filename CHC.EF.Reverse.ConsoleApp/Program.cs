using CHC.EF.Reverse.Poco.Core.Configuration;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Core.Services;
using CHC.EF.Reverse.Poco.Infrastructure.Databases;
using CHC.EF.Reverse.Poco.Infrastructure.Logging;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;


namespace CHC.EF.Reverse.Poco
{
    class Program
    {
        static async Task Main(string[] args)
        {


            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options =>
                {
                    IServiceProvider provider = null;
                    try
                    {
                        // 建立服務集合
                        var services = new ServiceCollection();
                        // 讀取設定
                        var settings = await GetSettingsAsync(options);

                        // 註冊服務
                        services.AddSingleton<ILogger, Logger>();
                        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));
                        services.AddSingleton<IDatabaseSchemaReaderFactory, DatabaseSchemaReaderFactory>();
                        services.AddTransient<CodeGenerationService>();

                        // 建立服務提供者
                        provider = services.BuildServiceProvider();
                        // 執行程式碼生成
                        using (var scope = provider.CreateScope())
                        {
                            var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
                            var codeGenService = scope.ServiceProvider.GetRequiredService<CodeGenerationService>();
                            await codeGenService.Run();
                        }
                    }
                    catch (Exception ex)
                    {
                        provider?.GetService<ILogger>()?.Error(ex.Message);
                        Environment.Exit(1);
                    }
                });
        }

        private static Task<Settings> GetSettingsAsync(Options options)
        {
            Settings settings = new Settings();

            if (!string.IsNullOrEmpty(options.ConfigFile) && File.Exists(options.ConfigFile))
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(options.ConfigFile)
                    .Build();

                var section = configuration.GetSection("CodeGenerator");
                if (section.Exists())
                {
                    settings = section.Get<Settings>();
                }
            }

            // 命令列參數優先，覆蓋之前的設定
            settings = MergeSettings(settings, new Settings
            {
                ConnectionString = options.ConnectionString,
                ProviderName = options.Provider,
                Namespace = options.Namespace,
                OutputDirectory = options.OutputDirectory,
                IsPluralize = options.IsPluralize ?? false,
                UseDataAnnotations = options.UseDataAnnotations ?? false
            });

            ValidateSettings(settings);
            return Task.FromResult(settings);
        }

        private static Settings MergeSettings(Settings target, Settings source)
        {
            // 只覆蓋非空值
            if (!string.IsNullOrEmpty(source.ConnectionString))
                target.ConnectionString = source.ConnectionString;
            if (!string.IsNullOrEmpty(source.ProviderName))
                target.ProviderName = source.ProviderName;
            if (!string.IsNullOrEmpty(source.Namespace))
                target.Namespace = source.Namespace;
            if (!string.IsNullOrEmpty(source.OutputDirectory))
                target.OutputDirectory = source.OutputDirectory;
            if (!string.IsNullOrEmpty(source.DbContextName))
                target.DbContextName = source.DbContextName;

            return target;
        }

        private static void ValidateSettings(Settings settings)
        {
            if (string.IsNullOrEmpty(settings.ConnectionString))
                throw new ArgumentException("Connection string is required. Please specify it in configuration file or command line.");

            if (string.IsNullOrEmpty(settings.ProviderName))
                settings.ProviderName = "Microsoft.Data.SqlClient";

            if (string.IsNullOrEmpty(settings.Namespace))
                settings.Namespace = "GeneratedApp.Data";

            if (string.IsNullOrEmpty(settings.OutputDirectory))
                settings.OutputDirectory = "./Generated";
        }
    }
}