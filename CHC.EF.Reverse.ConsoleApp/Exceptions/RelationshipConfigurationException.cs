using System;

namespace CHC.EF.Reverse.Poco.Exceptions
{

    /// <summary>
    /// 關聯設定過程中的異常。
    /// </summary>
    public class RelationshipConfigurationException : Exception
    {
        public RelationshipConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
