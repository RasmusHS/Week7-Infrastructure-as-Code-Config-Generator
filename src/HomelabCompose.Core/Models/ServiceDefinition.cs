using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class ServiceDefinition
{
    [YamlMember(Alias = "image")]
    public string Image { get; set; } = string.Empty;

    [YamlMember(Alias = "command")]
    public string? Command { get; set; }

    [YamlMember(Alias = "ports")]
    public List<string>? Ports { get; set; }

    [YamlMember(Alias = "networks")]
    public Dictionary<string, ServiceNetworkConfig?> Networks { get; set; } = new();

    [YamlMember(Alias = "environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [YamlMember(Alias = "volumes")]
    public List<string> Volumes { get; set; } = new();

    [YamlMember(Alias = "depends_on")]
    public Dictionary<string, string>? DependsOn { get; set; }

    [YamlMember(Alias = "healthcheck")]
    public HealthcheckConfig? Healthcheck { get; set; }

    [YamlMember(Alias = "cap_add")]
    public List<string>? CapAdd { get; set; }

    [YamlMember(Alias = "routing")]
    public RoutingConfig? Routing { get; set; }

}
