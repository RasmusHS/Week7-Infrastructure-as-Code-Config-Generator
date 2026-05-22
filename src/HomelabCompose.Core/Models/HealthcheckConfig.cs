using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class HealthcheckConfig
{
    [YamlMember(Alias = "test")]
    public List<string> Test { get; set; } = new();

    [YamlMember(Alias = "interval")]
    public string Interval { get; set; } = "30s";

    [YamlMember(Alias = "timeout")]
    public string Timeout { get; set; } = "10s";

    [YamlMember(Alias = "retries")]
    public int Retries { get; set; } = 3;

}
