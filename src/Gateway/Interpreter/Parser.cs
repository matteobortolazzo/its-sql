using Shared;

namespace Gateway.Interpreter;

public class Parser
{
    public Node Parse(Token[] tokens)
    {
        var current = 0;
        if (tokens[current].Type != TokenType.Keyword)
        {
            throw new Exception("Expected Keyword");
        }

        if (tokens[current].Value == "SELECT")
        {
            var select = ParseSelect(tokens, ref current);
            
            if (tokens[current].Type != TokenType.Keyword || tokens[current].Value != "WHERE")
            {
                throw new Exception("Expected WHERE");
            }
            
            var where = ParseWhere(tokens, ref current);
            return new QueryNode(select, where);
        }
        
        throw new Exception("Unsupported query");
    }

    private static SelectNode ParseSelect(Token[] tokens, ref int current)
    {
        current++;
        var columns = new List<ColumnNode>();
        while (tokens[current].Type != TokenType.Keyword)
        {
            columns.Add(ParseColumn(tokens, ref current));
            var nextToken = tokens[current];
            if (nextToken is { Type: TokenType.Operator, Value: "," })
            {
                current++;
            }
        }

        var fromNode = ParseFrom(tokens, ref current);
        return new SelectNode(columns.ToArray(), fromNode);
    }

    private static FromNode ParseFrom(Token[] tokens, ref int current)
    {
        if (tokens[current].Type != TokenType.Keyword || tokens[current].Value != "FROM")
        {
            throw new Exception("Expected FROM");
        }

        current++;

        if (tokens[current].Type != TokenType.Identifier)
        {
            throw new Exception("Expected table name");
        }

        var table = tokens[current].Value;
        current++;
        return new FromNode(table);
    }

    private static WhereNode ParseWhere(Token[] tokens, ref int current)
    {
        current++;
        var condition = ParseWhereClause(tokens, current, tokens.Length);
        return new WhereNode(condition);
    }
    
    private static Node ParseWhereClause(Token[] tokens, int start, int end)
    {
        var lastOperatorIndex = -1;
        var current = start;
        while (current < end)
        {
            if (tokens[current].Type == TokenType.Keyword &&
                (tokens[current].Value == "AND" || 
                 tokens[current].Value == "OR"))
            {
                lastOperatorIndex = current;
            }
            current++;
        }

        if (lastOperatorIndex < 0)
        {
            return ParseComparison(tokens, ref start);
        }
        
        var left = ParseWhereClause(tokens, start, lastOperatorIndex);
        var operation = tokens[lastOperatorIndex].Value == "AND" ? LogicalOperation.And : LogicalOperation.Or;
        var right = ParseWhereClause(tokens, lastOperatorIndex + 1, end);
        return new LogicalNode(operation, left, right);
    }

    private static ComparisonNode ParseComparison(Token[] tokens, ref int current)
    {
        if (tokens[current].Type != TokenType.Identifier)
        {
            throw new Exception("Expected Identifier");
        }
        
        var column = new ColumnNode(tokens[current].Value);
        current++;

        if (tokens[current].Type != TokenType.Operator || tokens[current].Value != "=")
        {
            throw new Exception("Expected =");
        }
        current++;
        
        if (tokens[current].Type != TokenType.String)
        {
            throw new Exception("Expected String");
        }
        
        var value = new ValueNode(tokens[current].Value);
        current++;

        return new ComparisonNode(ComparisonOperation.Equal, column, value);
    }

    private static ColumnNode ParseColumn(Token[] tokens, ref int current)
    {
        var token = tokens[current];
        if (token.Type == TokenType.Identifier || token is { Type: TokenType.Operator, Value: "*" })
        {
            current++;
            return new ColumnNode(token.Value);
        }

        throw new Exception("Expected Identifier");
    }
}