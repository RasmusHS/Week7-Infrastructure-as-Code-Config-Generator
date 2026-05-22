using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class ServiceNetworkConfig
{
    [YamlMember(Alias = "aliases")]
    public List<string>? Aliases { get; set; }
}