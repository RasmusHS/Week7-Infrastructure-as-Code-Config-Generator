using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class DefaultsConfig
{
    [YamlMember(Alias = "restart")]
    public string Restart { get; set; } = "unless-stopped";

}
