using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class RoutingConfig
{
    [YamlMember(Alias = "port")]
    public int Port { get; set; }

    [YamlMember(Alias = "routers")]
    public Dictionary<string, RouterConfig> Routers { get; set; } = new();

    [YamlMember(Alias = "middlewares")]
    public Dictionary<string, Dictionary<string, object>> Middlewares { get; set; } = new();
}