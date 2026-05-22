using HomelabCompose.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabCompose.Core.Parsing;

public class SchemaParser
{
    private readonly IDeserializer _deserializer;

    public SchemaParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public HomelabSchema ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Schema file not found: {filePath}");

        var yaml = File.ReadAllText(filePath);
        return Parse(yaml);
    }

    public HomelabSchema Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("YAML content is empty.");

        var schema = _deserializer.Deserialize<HomelabSchema>(yaml)
            ?? throw new InvalidOperationException("Failed to deserialize YAML schema.");

        return schema;
    }

}
