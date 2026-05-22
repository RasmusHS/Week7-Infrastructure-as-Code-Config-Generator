using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class TraefikConfig
{
    [YamlMember(Alias = "dashboard")]
    public bool Dashboard { get; set; }

    [YamlMember(Alias = "dashboard_insecure")]
    public bool DashboardInsecure { get; set; }

    [YamlMember(Alias = "entrypoints")]
    public Dictionary<string, string> Entrypoints { get; set; } = new();

    [YamlMember(Alias = "log_level")]
    public string LogLevel { get; set; } = "WARN";

    [YamlMember(Alias = "expose_by_default")]
    public bool ExposeByDefault { get; set; }

    [YamlMember(Alias = "docker_socket")]
    public string DockerSocket { get; set; } = "/var/run/docker.sock";

}
