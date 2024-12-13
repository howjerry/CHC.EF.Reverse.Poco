using System.Collections.Generic;

namespace CHC.EF.Reverse.Poco.Core.Models
{
    /// <summary>
    /// Represents the configuration settings for the code generator.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// The database connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The database provider name, e.g., "Microsoft.Data.SqlClient" or "MySql.Data.MySqlClient".
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// The namespace for the generated code.
        /// </summary>
        public string Namespace { get; set; } = "GeneratedApp.Data";

        /// <summary>
        /// The name of the DbContext class to be generated.
        /// </summary>
        public string DbContextName { get; set; } = "AppDbContext";

        /// <summary>
        /// Whether to use DataAnnotations for entity classes.
        /// </summary>
        public bool UseDataAnnotations { get; set; } = true;

        /// <summary>
        /// Whether to include comments from database schema as XML comments in the code.
        /// </summary>
        public bool IncludeComments { get; set; } = true;

        /// <summary>
        /// Whether to pluralize entity names for DbSet and collection navigation properties.
        /// </summary>
        public bool IsPluralize { get; set; } = true;

        /// <summary>
        /// Whether to use PascalCase for class and property names.
        /// </summary>
        public bool UsePascalCase { get; set; } = true;

        /// <summary>
        /// Whether to generate separate files for each entity and configuration class.
        /// </summary>
        public bool GenerateSeparateFiles { get; set; } = true;

        /// <summary>
        /// The output directory for the generated code.
        /// </summary>
        public string OutputDirectory { get; set; } = "C:\\GeneratedCode";

        /// <summary>
        /// Whether to include views in the generated code.
        /// </summary>
        public bool IncludeViews { get; set; } = false;

        /// <summary>
        /// Whether to include stored procedures in the generated code.
        /// </summary>
        public bool IncludeStoredProcedures { get; set; } = false;

        /// <summary>
        /// A list of elements to generate (e.g., "POCO", "Configuration", "DbContext").
        /// </summary>
        public List<string> ElementsToGenerate { get; set; } = new List<string>
        {
            "POCO",
            "Configuration",
            "DbContext"
        };

        /// <summary>
        /// Whether to include foreign keys as navigation properties in entities.
        /// </summary>
        public bool IncludeForeignKeys { get; set; } = true;

        /// <summary>
        /// Whether to treat many-to-many tables as junction tables and generate appropriate relationships.
        /// </summary>
        public bool IncludeManyToMany { get; set; } = true;
    }
}
