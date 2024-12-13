using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Core.Interfaces
{
    public interface IDatabaseSchemaReaderFactory
    {
        IDatabaseSchemaReader Create();
    }
}
