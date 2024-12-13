using System.Collections.Generic;

namespace CHC.EF.Reverse.Poco.Core.Models
{
    public class IndexDefinition
    {
        public string IndexName { get; set; }
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsDisabled { get; set; }
        public string IndexType { get; set; } // CLUSTERED/NONCLUSTERED
        public List<IndexColumnDefinition> Columns { get; set; } = new List<IndexColumnDefinition>();
        public bool IsClustered { get; set; }
        public string FilterDefinition { get; set; }
        public int FillFactor { get; set; } = 0;
        public bool IsPadded { get; set; }
    }
}
