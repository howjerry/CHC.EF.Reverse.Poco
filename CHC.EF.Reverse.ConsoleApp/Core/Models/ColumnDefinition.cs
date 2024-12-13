using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHC.EF.Reverse.Poco.Core.Models
{
    public class ColumnDefinition
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }
        public string DefaultValue { get; set; }
        public long? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public string Comment { get; set; }
        public bool IsUnique { get; set; }
        public List<IndexDefinition> ParticipatingIndexes { get; set; } = new List<IndexDefinition>();

        // 新增的擴展屬性
        public string CollationType { get; set; }
        public bool IsRowVersion { get; set; }
        public string GeneratedType { get; set; } // ALWAYS/BY DEFAULT for generated columns
        public string ComputedColumnDefinition { get; set; }
    }
}
