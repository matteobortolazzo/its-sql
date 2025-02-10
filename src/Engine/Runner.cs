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
        // RunFrom(selectNode.From);
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

    private void RunFrom(FromNode fromNode)
    {
    }

    private JsonObject[] RunWhere(JsonObject[] results, WhereNode whereNode)
    {
        return RunWhereClause(results, whereNode.Node);
    }

    private JsonObject[] RunWhereClause(JsonObject[] results, Node whereClauseNode)
    {
        if (whereClauseNode is ComparisonNode comparisonNode)
        {
            return RunComparison(results, comparisonNode);
        }

        if (whereClauseNode is LogicalNode logicalNode)
        {
            return RunLogical(results, logicalNode);
        }
        
        throw new Exception("Unknown where clause node type");
    }

    private JsonObject[] RunLogical(JsonObject[] results, LogicalNode logicalNode)
    {
        // TODO: Fix logic
        results = RunWhereClause(results, logicalNode.Left);
        return RunWhereClause(results, logicalNode.Right);
    }

    private JsonObject[] RunComparison(JsonObject[] results, ComparisonNode comparisonNode)
    {
        var column = comparisonNode.Column.Identifier;
        var value = comparisonNode.Value.Value;

        if (comparisonNode.Operation == ComparisonOperation.Equal)
        {
            return results
                .Where(result => result[column].ToString() == value)
                .ToArray();
        }
        
        throw new Exception("Unknown comparison operation");
    }
}