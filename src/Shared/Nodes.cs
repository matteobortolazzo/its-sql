using System.Text.Json.Serialization;

namespace Shared;

[JsonDerivedType(typeof(QueryNode), typeDiscriminator: "query")]
[JsonDerivedType(typeof(SelectNode), typeDiscriminator: "select")]
[JsonDerivedType(typeof(FromNode), typeDiscriminator: "from")]
[JsonDerivedType(typeof(ColumnNode), typeDiscriminator: "column")]
[JsonDerivedType(typeof(WhereNode), typeDiscriminator: "where")]
[JsonDerivedType(typeof(ValueNode), typeDiscriminator: "value")]
[JsonDerivedType(typeof(LogicalNode), typeDiscriminator: "logical")]
[JsonDerivedType(typeof(ComparisonNode), typeDiscriminator: "comparison")]
public abstract record Node();

public record QueryNode(SelectNode Select, WhereNode? Where) : Node;

public record SelectNode(ColumnNode[] Columns, FromNode From) : Node;

public record FromNode(string Table) : Node;

public record ColumnNode(string Identifier) : Node;

public record WhereNode(Node Node) : Node;

public record ValueNode(string Value) : Node;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogicalOperation
{
    And,
    Or
}

public record LogicalNode(LogicalOperation Operation, Node Left, Node Right) : Node;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComparisonOperation
{
    Equal
}

public record ComparisonNode(ComparisonOperation Operation, ColumnNode Column, ValueNode Value) : Node;
