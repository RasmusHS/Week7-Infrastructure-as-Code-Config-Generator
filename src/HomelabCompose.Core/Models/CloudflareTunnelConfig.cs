using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class CloudflareTunnelConfig
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; set; } = "token";

}
