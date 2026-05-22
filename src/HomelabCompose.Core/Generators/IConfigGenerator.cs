using HomelabCompose.Core.Models;

namespace HomelabCompose.Core.Generators;

/// <summary>
/// Defines a contract for generating configuration files from a given schema.
/// </summary>
/// <remarks>Implementations of this interface are responsible for producing configuration content and specifying
/// the target file name. This interface is typically used to support multiple configuration formats or output
/// targets.</remarks>
public interface IConfigGenerator
{
    string FileName { get; }
    string Generate(HomelabSchema schema);
}
