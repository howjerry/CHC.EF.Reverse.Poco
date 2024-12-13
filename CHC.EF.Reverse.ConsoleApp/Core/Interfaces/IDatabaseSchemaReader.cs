using CHC.EF.Reverse.Poco.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Core.Interfaces
{
    public interface IDatabaseSchemaReader
    {
        Task<List<TableDefinition>> ReadTables();
    }
}
