# CHC.EF.Reverse.Poco - Effortless Entity Framework Core Model Generation

This .NET Global Tool streamlines the creation of Entity Framework Core models from your existing database schema. By intelligently analyzing your database structure, it automatically generates clean, efficient C# code for your data access layer.

## Why Choose CHC.EF.Reverse.Poco?

* **Simplicity:** A single command generates all the necessary code: POCO classes, DbContext, and configurations.
* **Efficiency:**  Eliminate tedious manual coding and focus on building your application logic.
* **Flexibility:** Supports SQL Server, MySQL, and PostgreSQL with customizable settings.
* **Maintainability:** Keep your models in sync with your database schema with ease.

## Installation

Ensure you have the .NET 8 SDK installed. Then, install the tool globally using:

```bash
dotnet tool install --global CHC.EF.Reverse.Poco
```

## Usage

Generate your models with a single command:

```bash
efrev --connection "your_connection_string" --provider "your_provider_name" --output "your_output_directory"
```

**Options:**

* `--connection`: Your database connection string.
* `--provider`: The database provider (SqlServer/MySql/PostgreSql).
* `--output`: The directory for generated code.
* `--pluralize`: Pluralize table names (default: true).
* `--data-annotations`: Use data annotations (default: true).

**Example:**

```bash
efrev --connection "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;" --provider SqlServer --output "C:\MyProject\Models" 
```

## Advanced Configuration

Fine-tune the code generation process using an `appsettings.json` file:

```json
{
  "CodeGenerator": {
    "ConnectionString": "your_connection_string",
    "ProviderName": "your_provider_name",
    "Namespace": "your_namespace",
    "OutputDirectory": "your_output_directory",
    "IsPluralize": true,
    "UseDataAnnotations": true
  }
}
```

## Key Features

* **Relationship Mapping:** Accurately handles one-to-one, one-to-many, and many-to-many relationships.
* **Data Annotations:** Optionally decorate your models with data annotations for validation and metadata.
* **Connection Pooling:** Efficiently manages database connections for optimal performance.
* **Clear Logging:** Provides detailed logs for debugging and troubleshooting.

## Build and Test

```bash
dotnet build
dotnet test
```

## License

MIT License

## Contributing

Contributions are welcome! Feel free to submit pull requests or report issues on GitHub.

## Acknowledgements

This tool is built on the shoulders of giants, leveraging the power of Entity Framework Core and the .NET ecosystem.

