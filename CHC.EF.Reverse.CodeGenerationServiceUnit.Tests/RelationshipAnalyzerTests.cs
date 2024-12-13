using Xunit;
using Moq;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Core.Services;
using System;
using System.Collections.Generic;

/// <summary>
/// RelationshipAnalyzer 的單元測試類別。
/// </summary>
public class RelationshipAnalyzerTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly RelationshipAnalyzer _analyzer;

    public RelationshipAnalyzerTests()
    {
        _loggerMock = new Mock<ILogger>();
        _analyzer = new RelationshipAnalyzer(_loggerMock.Object);
    }

    /// <summary>
    /// 驗證中間表（多對多關聯）的判定邏輯。
    /// 依據 IsJunctionTable 方法的判定條件：
    /// 1. 必須有至少兩個不同的外鍵關係
    /// 2. 外鍵必須同時也是主鍵的一部分
    /// 3. 除了關聯欄位外只能有少量額外欄位
    /// </summary>
    [Fact]
    public void AnalyzeRelationship_WithJunctionTable_ReturnsManyToMany()
    {
        // Arrange
        var sourceTable = new TableDefinition
        {
            TableName = "StudentCourse",
            // 所有欄位都是主鍵和外鍵的組合
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition
                {
                    ColumnName = "StudentId",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                new ColumnDefinition
                {
                    ColumnName = "CourseId",
                    IsPrimaryKey = true,
                    IsNullable = false
                }
            },
            // 兩個外鍵都必須指向不同的表
            ForeignKeys = new List<ForeignKeyDefinition>
            {
                new ForeignKeyDefinition
                {
                    ConstraintName = "FK_StudentCourse_Student",
                    PrimaryTable = "Student",
                    ForeignKeyColumn = "StudentId",
                    PrimaryKeyColumn = "Id",
                    IsEnabled = true,
                    ColumnPairs = new List<ForeignKeyColumnPair>
                    {
                        new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = "StudentId",
                            PrimaryKeyColumn = "Id"
                        }
                    }
                },
                new ForeignKeyDefinition
                {
                    ConstraintName = "FK_StudentCourse_Course",
                    PrimaryTable = "Course",
                    ForeignKeyColumn = "CourseId",
                    PrimaryKeyColumn = "Id",
                    IsEnabled = true,
                    ColumnPairs = new List<ForeignKeyColumnPair>
                    {
                        new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = "CourseId",
                            PrimaryKeyColumn = "Id"
                        }
                    }
                }
            },
            // 確保有複合主鍵索引
            Indexes = new List<IndexDefinition>
            {
                new IndexDefinition
                {
                    IndexName = "PK_StudentCourse",
                    IsPrimaryKey = true,
                    IsUnique = true,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "StudentId",
                            KeyOrdinal = 1
                        },
                        new IndexColumnDefinition
                        {
                            ColumnName = "CourseId",
                            KeyOrdinal = 2
                        }
                    }
                }
            }
        };

        var targetTable = CreateBasicTable("Course");

        // Act
        var result = _analyzer.AnalyzeRelationship(sourceTable, targetTable);

        // Assert
        Assert.Equal(RelationType.ManyToMany, result.Type);
        Assert.NotNull(result.JunctionTableInfo);
        Assert.Contains("StudentId", result.JunctionTableInfo.SourceKeyColumns);
        Assert.Contains("CourseId", result.JunctionTableInfo.SourceKeyColumns);
    }

    /// <summary>
    /// 驗證一對一關聯的判定邏輯。
    /// </summary>
    /// <remarks>
    /// 測試要點：
    /// 1. 外鍵欄位必須具有唯一性約束
    /// 2. 外鍵不能是複合鍵的一部分
    /// 3. 關聯方向的正確判定
    /// 
    /// 關聯方向說明：
    /// - UserProfile 包含指向 User 的外鍵（UserId）
    /// - 由於外鍵在 UserProfile 表中，所以 UserProfile 是從屬端（Dependent）
    /// - User 是主體端（Principal）
    /// - 在一對一關聯中，從屬端應該是 SourceTable
    /// </remarks>
    [Fact]
    public void AnalyzeRelationship_WithUniqueConstraint_ReturnsOneToOne()
    {
        // Arrange
        // 1. 定義從屬端表格（包含外鍵）
        var sourceTable = new TableDefinition
        {
            TableName = "UserProfile",
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition
                {
                    ColumnName = "ProfileId",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    IsNullable = false
                },
                new ColumnDefinition
                {
                    ColumnName = "UserId",
                    IsNullable = false
                },
                new ColumnDefinition
                {
                    ColumnName = "Biography",
                    IsNullable = true
                }
            },
            ForeignKeys = new List<ForeignKeyDefinition>
            {
                new ForeignKeyDefinition
                {
                    ConstraintName = "FK_UserProfile_User",
                    ForeignKeyColumn = "UserId",
                    PrimaryTable = "User",
                    PrimaryKeyColumn = "Id",
                    IsEnabled = true,
                    IsCompositeKey = false,
                    ColumnPairs = new List<ForeignKeyColumnPair>
                    {
                        new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = "UserId",
                            PrimaryKeyColumn = "Id"
                        }
                    }
                }
            },
            Indexes = new List<IndexDefinition>
            {
                // 主鍵索引
                new IndexDefinition
                {
                    IndexName = "PK_UserProfile",
                    IsPrimaryKey = true,
                    IsUnique = true,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "ProfileId",
                            KeyOrdinal = 1
                        }
                    }
                },
                // 外鍵的唯一性約束
                new IndexDefinition
                {
                    IndexName = "UX_UserProfile_UserId",
                    IsUnique = true,
                    IsPrimaryKey = false,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "UserId",
                            KeyOrdinal = 1
                        }
                    }
                }
            }
        };

        // 2. 定義主體端表格
        var targetTable = new TableDefinition
        {
            TableName = "User",
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition
                {
                    ColumnName = "Id",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    IsNullable = false
                }
            },
            Indexes = new List<IndexDefinition>
            {
                new IndexDefinition
                {
                    IndexName = "PK_User",
                    IsPrimaryKey = true,
                    IsUnique = true,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "Id",
                            KeyOrdinal = 1
                        }
                    }
                }
            }
        };

        // Act
        var result = _analyzer.AnalyzeRelationship(sourceTable, targetTable);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RelationType.OneToOne, result.Type);
        // User 是主體端（Principal），應該是 SourceTable
        Assert.Equal("UserProfile", result.SourceTable);
        Assert.Equal("User", result.TargetTable);

        // 驗證外鍵設定
        Assert.NotNull(result.ForeignKeyColumns);
        Assert.Single(result.ForeignKeyColumns);
        var foreignKey = result.ForeignKeyColumns[0];
        Assert.Equal("UserId", foreignKey.ForeignKeyColumn);
        Assert.Equal("Id", foreignKey.PrimaryKeyColumn);
    }
    /// <summary>
    /// 驗證複合主鍵但非中間表的一對多關聯判定邏輯。
    /// 這種情況下：
    /// 1. 有複合主鍵但只有一個外鍵關係
    /// 2. 有其他非鍵欄位
    /// 3. 不符合中間表的條件
    /// </summary>
    [Fact]
    public void AnalyzeRelationship_WithCompositeKey_NotJunctionTable_ReturnsOneToMany()
    {
        // Arrange
        var sourceTable = new TableDefinition
        {
            TableName = "OrderDetail",
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition
                {
                    ColumnName = "OrderId",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                new ColumnDefinition
                {
                    ColumnName = "ProductId",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                new ColumnDefinition
                {
                    ColumnName = "Quantity",
                    IsNullable = false
                }
            },
            ForeignKeys = new List<ForeignKeyDefinition>
            {
                new ForeignKeyDefinition
                {
                    ConstraintName = "FK_OrderDetail_Order",
                    PrimaryTable = "Order",
                    ForeignKeyColumn = "OrderId",
                    PrimaryKeyColumn = "Id",
                    IsEnabled = true,
                    ColumnPairs = new List<ForeignKeyColumnPair>
                    {
                        new ForeignKeyColumnPair
                        {
                            ForeignKeyColumn = "OrderId",
                            PrimaryKeyColumn = "Id"
                        }
                    }
                }
            },
            Indexes = new List<IndexDefinition>
            {
                new IndexDefinition
                {
                    IndexName = "PK_OrderDetail",
                    IsPrimaryKey = true,
                    IsUnique = true,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "OrderId",
                            KeyOrdinal = 1
                        },
                        new IndexColumnDefinition
                        {
                            ColumnName = "ProductId",
                            KeyOrdinal = 2
                        }
                    }
                }
            }
        };

        var targetTable = CreateBasicTable("Order");

        // Act
        var result = _analyzer.AnalyzeRelationship(sourceTable, targetTable);

        // Assert
        Assert.Equal(RelationType.OneToMany, result.Type);
        Assert.Equal("Order", result.SourceTable);
        Assert.Equal("OrderDetail", result.TargetTable);
        Assert.Single(result.ForeignKeyColumns);
        Assert.Equal("OrderId", result.ForeignKeyColumns[0].ForeignKeyColumn);
    }

    /// <summary>
    /// 建立基本測試表格定義。
    /// </summary>
    private TableDefinition CreateBasicTable(string tableName)
    {
        return new TableDefinition
        {
            TableName = tableName,
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition
                {
                    ColumnName = "Id",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    IsNullable = false
                }
            },
            Indexes = new List<IndexDefinition>
            {
                new IndexDefinition
                {
                    IndexName = $"PK_{tableName}",
                    IsPrimaryKey = true,
                    IsUnique = true,
                    Columns = new List<IndexColumnDefinition>
                    {
                        new IndexColumnDefinition
                        {
                            ColumnName = "Id",
                            KeyOrdinal = 1
                        }
                    }
                }
            }
        };
    }
}