using System.Text;
using Shared;

namespace Engine;

public class Runner
{
    private readonly StringBuilder _output = new();
    
    public string Run(Node node)
    {
        _output.Clear();
        
        if (node is QueryNode queryNode)
        {
            RunQuery(queryNode);
            return _output.ToString();
        }
    
        return _output.Append("Unknown node type").ToString();
    }

    private void RunQuery(QueryNode queryNode)
    {
        RunSelect(queryNode.Select);

        if (queryNode.Where != null)
        {
            RunWhere(queryNode.Where);
        }
    }

    private void RunSelect(SelectNode selectNode)
    {
        RunFrom(selectNode.From);
        RunColumns(selectNode.Columns);
    }

    private void RunColumns(ColumnNode[] columnNodes)
    {
        foreach (var columnNode in columnNodes)
        {
            _output.AppendLine($"SELECT {columnNode.Identifier}");
        }
    }

    private void RunFrom(FromNode fromNode)
    {
        _output.AppendLine($"FROM {fromNode.Table}");
    }

    private void RunWhere(WhereNode whereNode)
    {
        RunWhereClause(whereNode.Node);
    }

    private void RunWhereClause(Node whereClauseNode)
    {
        if (whereClauseNode is ComparisonNode comparisonNode)
        {
            RunComparison(comparisonNode);
        }
        else if (whereClauseNode is LogicalNode logicalNode)
        {
            RunLogical(logicalNode);
        }
    }

    private void RunLogical(LogicalNode logicalNode)
    {
        RunWhereClause(logicalNode.Left);
        _output.AppendLine(logicalNode.Operation == LogicalOperation.And ? "AND" : "OR");
        RunWhereClause(logicalNode.Right);
    }

    private void RunComparison(ComparisonNode comparisonNode)
    {
        _output.AppendLine($"WHERE {comparisonNode.Column.Identifier} = {comparisonNode.Value.Value}");
    }
}