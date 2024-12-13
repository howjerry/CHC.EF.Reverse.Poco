using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Exceptions
{
    public class CodeGenerationException : Exception
    {
        public CodeGenerationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
