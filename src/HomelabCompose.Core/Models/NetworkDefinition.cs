using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class NetworkDefinition
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

}
