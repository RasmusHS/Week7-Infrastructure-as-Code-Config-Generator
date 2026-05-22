using HomelabCompose.Core.Models;

namespace HomelabCompose.Core.Validation;

public class SchemaValidator
{
    public ValidationResult Validate(HomelabSchema schema)
    {
        var result = new ValidationResult();

        ValidateRequiredFields(schema, result);
        ValidateNetworkReferences(schema, result);
        ValidateVolumeReferences(schema, result);
        ValidateDependsOnReferences(schema, result);
        ValidateEntrypointReferences(schema, result);
        ValidateRouterHosts(schema, result);
        ValidateTunnelConfig(schema, result);

        return result;
    }

    private void ValidateRequiredFields(HomelabSchema schema, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(schema.Name))
            result.AddError("Schema 'name' is required.");

        if (string.IsNullOrWhiteSpace(schema.Domain))
            result.AddError("Schema 'domain' is required.");

        if (schema.Services.Count == 0)
            result.AddError("Schema must define at least one service.");

        foreach (var (name, service) in schema.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Image))
                result.AddError($"Service '{name}' is missing required field 'image'.");

            if (service.Routing != null)
            {
                if (service.Routing.Port <= 0)
                    result.AddError($"Service '{name}' has routing but port is missing or invalid.");

                if (service.Routing.Routers.Count == 0)
                    result.AddError($"Service '{name}' has routing but no routers defined.");

                foreach (var (routerName, router) in service.Routing.Routers)
                {
                    if (string.IsNullOrWhiteSpace(router.Host))
                        result.AddError($"Service '{name}' router '{routerName}' is missing required field 'host'.");

                    if (router.Entrypoints.Count == 0)
                        result.AddError($"Service '{name}' router '{routerName}' has no entrypoints.");
                }
            }
        }
    }

    private void ValidateNetworkReferences(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            foreach (var networkName in service.Networks.Keys)
            {
                if (!schema.Networks.ContainsKey(networkName))
                    result.AddError($"Service '{name}' references undefined network '{networkName}'.");
            }
        }
    }

    private void ValidateVolumeReferences(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            foreach (var volume in service.Volumes)
            {
                // Named volumes are "name:/path", bind mounts start with "./" or "/"
                var source = volume.Split(':')[0];
                if (source.StartsWith("./") || source.StartsWith("/"))
                    continue;

                if (!schema.Volumes.ContainsKey(source))
                    result.AddError($"Service '{name}' references undefined volume '{source}'.");
            }
        }
    }

    private void ValidateDependsOnReferences(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            if (service.DependsOn == null) continue;

            foreach (var (dependency, condition) in service.DependsOn)
            {
                if (!schema.Services.ContainsKey(dependency))
                    result.AddError($"Service '{name}' depends on undefined service '{dependency}'.");

                var validConditions = new[] { "service_started", "service_healthy", "service_completed_successfully" };
                if (!validConditions.Contains(condition))
                    result.AddError($"Service '{name}' has invalid depends_on condition '{condition}' for '{dependency}'. Valid: {string.Join(", ", validConditions)}.");
            }
        }
    }

    private void ValidateEntrypointReferences(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            if (service.Routing == null) continue;

            foreach (var (routerName, router) in service.Routing.Routers)
            {
                foreach (var entrypoint in router.Entrypoints)
                {
                    if (!schema.Traefik.Entrypoints.ContainsKey(entrypoint))
                        result.AddError($"Service '{name}' router '{routerName}' references undefined entrypoint '{entrypoint}'.");
                }
            }
        }
    }

    private void ValidateRouterHosts(HomelabSchema schema, ValidationResult result)
    {
        var hostMap = new Dictionary<string, string>();

        foreach (var (name, service) in schema.Services)
        {
            if (service.Routing == null) continue;

            foreach (var (routerName, router) in service.Routing.Routers)
            {
                var key = $"{router.Host}";
                if (hostMap.TryGetValue(key, out var existingService))
                {
                    result.AddError($"Duplicate router host '{router.Host}' in service '{name}' — already used by service '{existingService}'.");
                }
                else
                {
                    hostMap[key] = name;
                }
            }
        }
    }

    private void ValidateTunnelConfig(HomelabSchema schema, ValidationResult result)
    {
        var tunnelRouters = schema.Services
            .Where(s => s.Value.Routing != null)
            .SelectMany(s => s.Value.Routing!.Routers.Values)
            .Any(r => r.Tunnel);

        if (tunnelRouters && schema.CloudflareTunnel == null)
            result.AddError("Services reference tunnel routing but 'cloudflare_tunnel' is not configured.");

        if (!tunnelRouters && schema.CloudflareTunnel != null)
            result.AddWarning("'cloudflare_tunnel' is configured but no services use tunnel routing.");

        foreach (var (name, service) in schema.Services)
        {
            if (service.Networks.Count == 0)
                result.AddWarning($"Service '{name}' has no networks defined.");
        }
    }

}
