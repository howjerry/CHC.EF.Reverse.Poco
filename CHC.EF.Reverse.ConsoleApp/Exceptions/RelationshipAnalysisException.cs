using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Exceptions
{

    /// <summary>
    /// 關聯分析過程中可能發生的異常。
    /// </summary>
    public class RelationshipAnalysisException : Exception
    {
        public RelationshipAnalysisException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
