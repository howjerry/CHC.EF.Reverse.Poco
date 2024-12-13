using Xunit;
using Moq;
using CHC.EF.Reverse.Poco.Core.Interfaces;
using CHC.EF.Reverse.Poco.Core.Models;
using CHC.EF.Reverse.Poco.Core.Services;
using System;
using System.Collections.Generic;

/// <summary>
/// RelationshipAnalyzer ���椸�������O�C
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
    /// ���Ҥ�����]�h��h���p�^���P�w�޿�C
    /// �̾� IsJunctionTable ��k���P�w����G
    /// 1. �������ܤ֨�Ӥ��P���~�����Y
    /// 2. �~�䥲���P�ɤ]�O�D�䪺�@����
    /// 3. ���F���p���~�u�঳�ֶq�B�~���
    /// </summary>
    [Fact]
    public void AnalyzeRelationship_WithJunctionTable_ReturnsManyToMany()
    {
        // Arrange
        var sourceTable = new TableDefinition
        {
            TableName = "StudentCourse",
            // �Ҧ���쳣�O�D��M�~�䪺�զX
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
            // ��ӥ~�䳣�������V���P����
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
            // �T�O���ƦX�D�����
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
    /// ���Ҥ@��@���p���P�w�޿�C
    /// </summary>
    /// <remarks>
    /// ���խn�I�G
    /// 1. �~����쥲���㦳�ߤ@�ʬ���
    /// 2. �~�䤣��O�ƦX�䪺�@����
    /// 3. ���p��V�����T�P�w
    /// 
    /// ���p��V�����G
    /// - UserProfile �]�t���V User ���~��]UserId�^
    /// - �ѩ�~��b UserProfile ���A�ҥH UserProfile �O�q�ݺݡ]Dependent�^
    /// - User �O�D��ݡ]Principal�^
    /// - �b�@��@���p���A�q�ݺ����ӬO SourceTable
    /// </remarks>
    [Fact]
    public void AnalyzeRelationship_WithUniqueConstraint_ReturnsOneToOne()
    {
        // Arrange
        // 1. �w�q�q�ݺݪ��]�]�t�~��^
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
                // �D�����
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
                // �~�䪺�ߤ@�ʬ���
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

        // 2. �w�q�D��ݪ��
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
        // User �O�D��ݡ]Principal�^�A���ӬO SourceTable
        Assert.Equal("UserProfile", result.SourceTable);
        Assert.Equal("User", result.TargetTable);

        // ���ҥ~��]�w
        Assert.NotNull(result.ForeignKeyColumns);
        Assert.Single(result.ForeignKeyColumns);
        var foreignKey = result.ForeignKeyColumns[0];
        Assert.Equal("UserId", foreignKey.ForeignKeyColumn);
        Assert.Equal("Id", foreignKey.PrimaryKeyColumn);
    }
    /// <summary>
    /// ���ҽƦX�D����D�������@��h���p�P�w�޿�C
    /// �o�ر��p�U�G
    /// 1. ���ƦX�D����u���@�ӥ~�����Y
    /// 2. ����L�D�����
    /// 3. ���ŦX����������
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
    /// �إ߰򥻴��ժ��w�q�C
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