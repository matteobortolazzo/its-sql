namespace Gateway.Services;

public class PartitionService
{
    private readonly Dictionary<string, string> _containers = new()
    {
        { "users", "city" }
    };

    public string GetPartitionKeyPath(string container)
    {
        return _containers[container];
    }
}