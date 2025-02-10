using System.Text.Json;
using System.Text.Json.Nodes;
using Shared;

namespace Engine;

public class Runner(ILogger logger, string container)
{
    public async Task<JsonObject[]> RunAsync(Node node)
    {
        if (node is QueryNode queryNode)
        {
            return await RunQueryAsync(queryNode);
        }

        throw new Exception("Unknown node type");
    }

    private async Task<JsonObject[]> RunQueryAsync(QueryNode queryNode)
    {
        var tasks = Directory.GetFiles(container)
            .Select(async fileName =>
            {
                var content = await File.ReadAllTextAsync(fileName);
                return JsonSerializer.Deserialize<JsonObject>(content);
            });
        var results = await Task.WhenAll(tasks);
        if (results.Length == 0)
        {
            return [];
        }

        if (queryNode.Where != null)
        {
            results = RunWhere(results, queryNode.Where);
        }

        return RunSelect(results, queryNode.Select);
    }

    private JsonObject[] RunSelect(JsonObject[] results, SelectNode selectNode)
    {
        return RunColumns(results, selectNode.Columns);
    }

    private JsonObject[] RunColumns(JsonObject[] results, ColumnNode[] columnNodes)
    {
        var selectedColumns = columnNodes.Select(node => node.Identifier).ToArray();
        if (results.Length == 0)
        {
            return [];
        }

        var columnsToRemove = results[0]
            .Where(column =>
                !selectedColumns.Contains(column.Key))
            .Select(column => column.Key)
            .ToArray();
        foreach (var result in results)
        {
            foreach (var columnKey in columnsToRemove)
            {
                result.Remove(columnKey);
            }
        }

        return results;
    }

    private JsonObject[] RunWhere(JsonObject[] results, WhereNode whereNode)
    {
        List<JsonObject> newResults = [];
        foreach (var result in results)
        {
            var pass = RunWhereClause(result, whereNode.Node);
            if (pass)
            {
                newResults.Add(result);
            }
        }

        return newResults.ToArray();
    }

    private bool RunWhereClause(JsonObject result, Node whereClauseNode)
    {
        if (whereClauseNode is ComparisonNode comparisonNode)
        {
            return RunComparison(result, comparisonNode);
        }

        if (whereClauseNode is LogicalNode logicalNode)
        {
            return RunLogical(result, logicalNode);
        }

        throw new Exception("Unknown where clause node type");
    }

    private bool RunLogical(JsonObject result, LogicalNode logicalNode)
    {
        var leftPass = RunWhereClause(result, logicalNode.Left);
        if (logicalNode.Operation == LogicalOperation.And)
        {
            if (!leftPass)
            {
                return false;
            }
        }
        else // OR
        {
            if (leftPass)
            {
                return true;
            }
        }

        return RunWhereClause(result, logicalNode.Right);
    }

    private bool RunComparison(JsonObject result, ComparisonNode comparisonNode)
    {
        var column = comparisonNode.Column.Identifier;
        var value = comparisonNode.Value.Value;

        if (comparisonNode.Operation == ComparisonOperation.Equal)
        {
            return result[column].ToString() == value;
        }

        throw new Exception("Unknown comparison operation");
    }
}