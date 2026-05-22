using YamlDotNet.Serialization;

namespace HomelabCompose.Core.Models;

public class RouterConfig
{
    [YamlMember(Alias = "host")]
    public string Host { get; set; } = string.Empty;

    [YamlMember(Alias = "entrypoints")]
    public List<string> Entrypoints { get; set; } = new();

    [YamlMember(Alias = "tunnel")]
    public bool Tunnel { get; set; }
}