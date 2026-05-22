using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class HomelabSchema
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "domain")]
    public string Domain { get; set; } = string.Empty;

    [YamlMember(Alias = "local_domain")]
    public string? LocalDomain { get; set; }

    [YamlMember(Alias = "defaults")]
    public DefaultsConfig Defaults { get; set; } = new();

    [YamlMember(Alias = "networks")]
    public Dictionary<string, NetworkDefinition> Networks { get; set; } = new();

    [YamlMember(Alias = "volumes")]
    public Dictionary<string, VolumeDefinition> Volumes { get; set; } = new();

    [YamlMember(Alias = "traefik")]
    public TraefikConfig Traefik { get; set; } = new();

    [YamlMember(Alias = "cloudflare_tunnel")]
    public CloudflareTunnelConfig? CloudflareTunnel { get; set; }

    [YamlMember(Alias = "services")]
    public Dictionary<string, ServiceDefinition> Services { get; set; } = new();

}
