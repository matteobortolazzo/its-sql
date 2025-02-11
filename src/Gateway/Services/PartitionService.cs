namespace Gateway.Services;

public class PartitionService
{
    private readonly Dictionary<string, string?> _containers = new()
    {
        { "users", "city" }
    };

    public bool TryGetPartitionKeyPath(string container, out string? partitionKeyPath)
    {
        return _containers.TryGetValue(container, out partitionKeyPath);
    }
}