using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Infrastructure.Generators
{
    public class DbContextGenerator
    {
        private readonly Settings _settings;
        private readonly ILogger _logger;

        public DbContextGenerator(Settings settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public async Task GenerateDbContextAsync(List<TableDefinition> tables, string outputDir)
        {
            var sb = new StringBuilder();

            // 添加必要的 using 語句
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Data.Entity;");
            sb.AppendLine("using System.Data.Entity.ModelConfiguration.Conventions;");
            sb.AppendLine($"using {_settings.Namespace}.Entities;");
            sb.AppendLine($"using {_settings.Namespace}.Configurations;");
            sb.AppendLine();

            // 開始定義 DbContext 類
            sb.AppendLine($"namespace {_settings.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {_settings.DbContextName} : DbContext");
            sb.AppendLine("    {");

            // 構造函數
            GenerateConstructors(sb);

            // DbSet 屬性
            GenerateDbSets(sb, tables);

            // OnModelCreating 方法
            GenerateOnModelCreating(sb, tables);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            var filePath = Path.Combine(outputDir, $"{_settings.DbContextName}.cs");
            await File.WriteAllTextAsync(filePath, sb.ToString());
            _logger.Info($"Generated DbContext class: {filePath}");
        }

        private void GenerateConstructors(StringBuilder sb)
        {
            // 默認構造函數
            sb.AppendLine($"        public {_settings.DbContextName}()");
            sb.AppendLine("            : base(\"name=DefaultConnection\")");
            sb.AppendLine("        {");
            sb.AppendLine("            Configuration.LazyLoadingEnabled = true;");
            sb.AppendLine("            Configuration.ProxyCreationEnabled = true;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // 帶參數的構造函數
            sb.AppendLine($"        public {_settings.DbContextName}(string connectionString)");
            sb.AppendLine("            : base(connectionString)");
            sb.AppendLine("        {");
            sb.AppendLine("            Configuration.LazyLoadingEnabled = true;");
            sb.AppendLine("            Configuration.ProxyCreationEnabled = true;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateDbSets(StringBuilder sb, List<TableDefinition> tables)
        {
            sb.AppendLine("        #region DbSet Properties");
            foreach (var table in tables)
            {
                var entityTypeName = ToPascalCase(table.TableName);
                var dbSetName = _settings.IsPluralize ? Pluralize(entityTypeName) : entityTypeName;

                if (!string.IsNullOrEmpty(table.Comment))
                {
                    sb.AppendLine($"        /// <summary>");
                    sb.AppendLine($"        /// {table.Comment}");
                    sb.AppendLine($"        /// </summary>");
                }

                sb.AppendLine($"        public virtual DbSet<{entityTypeName}> {dbSetName} {{ get; set; }}");
                sb.AppendLine();
            }
            sb.AppendLine("        #endregion");
            sb.AppendLine();
        }

        private void GenerateOnModelCreating(StringBuilder sb, List<TableDefinition> tables)
        {
            sb.AppendLine("        protected override void OnModelCreating(DbModelBuilder modelBuilder)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Remove default conventions");
            sb.AppendLine("            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();");
            sb.AppendLine("            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();");
            sb.AppendLine();

            sb.AppendLine("            // Configure entity types");
            foreach (var table in tables)
            {
                var entityTypeName = ToPascalCase(table.TableName);
                sb.AppendLine($"            modelBuilder.Configurations.Add(new {entityTypeName}Configuration());");
            }

            sb.AppendLine();
            sb.AppendLine("            // Global configurations");
            sb.AppendLine("            modelBuilder.Properties<string>()");
            sb.AppendLine("                .Configure(c => c.HasColumnType(\"nvarchar\"));");
            sb.AppendLine();

            sb.AppendLine("            base.OnModelCreating(modelBuilder);");
            sb.AppendLine("        }");
        }

        private string ToPascalCase(string text)
        {
            return Regex.Replace(text, "(^|_)([a-z])", m => m.Groups[2].Value.ToUpper());
        }

        private string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // 這只是一個簡單的複數規則示例，實際應用中可能需要更複雜的規則
            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }
            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("z", StringComparison.OrdinalIgnoreCase))
            {
                return name + "es";
            }
            return name + "s";
        }

        private bool IsVowel(char c)
        {
            return "aeiouAEIOU".IndexOf(c) >= 0;
        }
    }
}