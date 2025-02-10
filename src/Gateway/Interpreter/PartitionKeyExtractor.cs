using Shared;

namespace Gateway.Interpreter;

public class PartitionKeyExtractor(string partitionKeyPath)
{
    public string? Get(Node node)
    {
        if (node is QueryNode { Where: not null } queryNode)
        {
            return RunWhereClause(queryNode.Where.Node);
        }

        throw new Exception("Unknown node type");
    }
    
    private string? RunWhereClause(Node whereClauseNode)
    {
        if (whereClauseNode is ComparisonNode comparisonNode)
        {
            return RunComparison(comparisonNode);
        }

        if (whereClauseNode is LogicalNode logicalNode)
        {
            return RunLogical(logicalNode);
        }

        throw new Exception("Unknown where clause node type");
    }

    private string? RunLogical(LogicalNode logicalNode)
    {
        var leftResult = RunWhereClause(logicalNode.Left);
        if (leftResult != null)
        {
            return leftResult;
        }
        return RunWhereClause(logicalNode.Right);
    }

    private string? RunComparison(ComparisonNode comparisonNode)
    {
        var column = comparisonNode.Column.Identifier;
        var value = comparisonNode.Value;

        if (value is StringValueNode stringValueNode)
        {
            return RunStringComparison(comparisonNode, column, stringValueNode.Value);
        }

        if (value is NumberValueNode)
        {
            return null;
        }

        throw new Exception("Unknown comparison operation");
    }

    private string? RunStringComparison(ComparisonNode comparisonNode, string column, string value)
    {
        if (column == partitionKeyPath && comparisonNode.Operation == ComparisonOperation.Equal)
        {
            return value;
        }

        return null;
    }
}