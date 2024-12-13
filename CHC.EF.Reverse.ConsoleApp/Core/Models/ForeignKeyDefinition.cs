using System.Collections.Generic;

namespace CHC.EF.Reverse.Poco.Core.Models
{
    public class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; }
        public string ForeignKeyColumn { get; set; }
        public string PrimaryTable { get; set; }
        public string PrimaryKeyColumn { get; set; }
        public string DeleteRule { get; set; }
        public string UpdateRule { get; set; }
        public bool IsCompositeKey { get; set; }
        public string Comment { get; set; }
        public List<ForeignKeyColumnPair> ColumnPairs { get; set; } = new List<ForeignKeyColumnPair>();

        // 新增的擴展屬性
        public bool IsEnabled { get; set; } = true;
        public bool IsNotForReplication { get; set; }
    }
}
