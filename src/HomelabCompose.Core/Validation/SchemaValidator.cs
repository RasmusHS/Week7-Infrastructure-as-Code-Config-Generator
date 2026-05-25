using HomelabCompose.Core.Models;
using System.Text.RegularExpressions;

namespace HomelabCompose.Core.Validation;

public class SchemaValidator
{
    private static readonly Dictionary<string, HashSet<string>> KnownMiddlewareProperties = new()
    {
        ["headers"] = [
        "stsSeconds", "stsIncludeSubdomains", "stsPreload",
            "forceSTSHeader", "frameDeny", "contentTypeNosniff",
            "browserXssFilter", "customFrameOptionsValue",
            "contentSecurityPolicy", "referrerPolicy",
            "permissionsPolicy", "isDevelopment",
            "accessControlAllowMethods", "accessControlAllowOriginList",
            "accessControlAllowHeaders", "accessControlMaxAge",
            "accessControlExposeHeaders", "addVaryHeader",
            "customRequestHeaders", "customResponseHeaders",
        ],
        ["redirectregex"] = ["permanent", "regex", "replacement"],
        ["redirectscheme"] = ["scheme", "permanent", "port"],
        ["stripprefix"] = ["prefixes", "forceSlash"],
        ["stripprefixregex"] = ["regex"],
        ["addprefix"] = ["prefix"],
        ["ratelimit"] = ["average", "burst", "period"],
        ["basicauth"] = ["users", "usersFile", "realm", "removeHeader", "headerField"],
        ["digestauth"] = ["users", "usersFile", "realm", "removeHeader", "headerField"],
        ["forwardauth"] = [
        "address", "tls", "trustForwardHeader",
            "authResponseHeaders", "authResponseHeadersRegex",
            "authRequestHeaders",
        ],
        ["ipallowlist"] = ["sourceRange", "ipStrategy"],
        ["compress"] = ["excludedContentTypes", "minResponseBodyBytes"],
        ["chain"] = ["middlewares"],
        ["retry"] = ["attempts", "initialInterval"],
        ["circuitbreaker"] = ["expression"],
        ["buffering"] = ["maxRequestBodyBytes", "memRequestBodyBytes", "maxResponseBodyBytes", "memResponseBodyBytes", "retryExpression"],
    };

    private static readonly HashSet<string> ValidMiddlewareTypes =
        new(KnownMiddlewareProperties.Keys);

    public ValidationResult Validate(HomelabSchema schema)
    {
        var result = new ValidationResult();

        ValidateRequiredFields(schema, result);
        ValidateImageFormats(schema, result);
        ValidateNetworkReferences(schema, result);
        ValidateVolumeReferences(schema, result);
        ValidateDependsOnReferences(schema, result);
        ValidateCircularDependencies(schema, result);
        ValidateDependencyNetworkConnectivity(schema, result);
        ValidateEntrypointReferences(schema, result);
        ValidateRouterHosts(schema, result);
        ValidatePortConflicts(schema, result);
        ValidateMiddlewares(schema, result);
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

    private void ValidateImageFormats(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Image))
                continue; // Already caught by required fields

            if (!ImageFormatRegexPattern.IsMatch(service.Image))
                result.AddError($"Service '{name}' has invalid image format '{service.Image}'. Expected format: [registry/]name[:tag].");
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

                // Validate that service_healthy has a healthcheck defined
                if (condition == "service_healthy"
                    && schema.Services.TryGetValue(dependency, out var depService)
                    && depService.Healthcheck == null)
                {
                    result.AddWarning($"Service '{name}' depends on '{dependency}' with condition 'service_healthy', but '{dependency}' has no healthcheck defined.");
                }
            }
        }
    }

    private void ValidateCircularDependencies(HomelabSchema schema, ValidationResult result)
    {
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var name in schema.Services.Keys)
        {
            if (!visited.Contains(name))
            {
                var cycle = DetectCycle(name, schema, visited, inStack, []);
                if (cycle != null)
                {
                    result.AddError($"Circular dependency detected: {string.Join(" → ", cycle)}.");
                    return; // One cycle error is enough
                }
            }
        }
    }

    private List<string>? DetectCycle(
        string current,
        HomelabSchema schema,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path)
    {
        visited.Add(current);
        inStack.Add(current);
        path.Add(current);

        var service = schema.Services[current];
        if (service.DependsOn != null)
        {
            foreach (var dependency in service.DependsOn.Keys)
            {
                if (!schema.Services.ContainsKey(dependency))
                    continue; // Already caught by reference validation

                if (inStack.Contains(dependency))
                {
                    // Build the cycle path from where the loop starts
                    var cycleStart = path.IndexOf(dependency);
                    var cycle = path.Skip(cycleStart).ToList();
                    cycle.Add(dependency);
                    return cycle;
                }

                if (!visited.Contains(dependency))
                {
                    var cycle = DetectCycle(dependency, schema, visited, inStack, path);
                    if (cycle != null)
                        return cycle;
                }
            }
        }

        inStack.Remove(current);
        path.RemoveAt(path.Count - 1);
        return null;
    }

    private void ValidateDependencyNetworkConnectivity(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            if (service.DependsOn == null) continue;

            var serviceNetworks = service.Networks.Keys.ToHashSet();

            foreach (var dependency in service.DependsOn.Keys)
            {
                if (!schema.Services.TryGetValue(dependency, out var depService))
                    continue; // Already caught by reference validation

                var depNetworks = depService.Networks.Keys.ToHashSet();
                var sharedNetworks = serviceNetworks.Intersect(depNetworks).ToList();

                if (sharedNetworks.Count == 0)
                    result.AddError($"Service '{name}' depends on '{dependency}' but they share no networks. They won't be able to communicate.");
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
                var key = router.Host;
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

    private void ValidatePortConflicts(HomelabSchema schema, ValidationResult result)
    {
        var portMap = new Dictionary<string, string>();

        foreach (var (name, service) in schema.Services)
        {
            if (service.Ports == null) continue;

            foreach (var portMapping in service.Ports)
            {
                // Extract host port from "hostPort:containerPort" or "hostPort:containerPort/protocol"
                var hostPort = portMapping.Split(':')[0];

                if (portMap.TryGetValue(hostPort, out var existingService))
                {
                    result.AddError($"Port conflict: host port {hostPort} is used by both '{existingService}' and '{name}'.");
                }
                else
                {
                    portMap[hostPort] = name;
                }
            }
        }
    }

    private void ValidateMiddlewares(HomelabSchema schema, ValidationResult result)
    {
        foreach (var (name, service) in schema.Services)
        {
            if (service.Routing?.Middlewares == null) continue;

            foreach (var (mwName, mwProps) in service.Routing.Middlewares)
            {
                if (!ValidMiddlewareTypes.Contains(mwName))
                {
                    result.AddWarning($"Service '{name}' middleware '{mwName}' is not a recognized Traefik middleware type. Known types: {string.Join(", ", ValidMiddlewareTypes.Order())}.");
                    continue;
                }

                var validProps = KnownMiddlewareProperties[mwName];
                foreach (var prop in mwProps.Keys)
                {
                    if (!validProps.Contains(prop))
                        result.AddWarning($"Service '{name}' middleware '{mwName}' has unknown property '{prop}'. Valid properties: {string.Join(", ", validProps.Order())}.");
                }

                // Validate required properties for known types
                ValidateRequiredMiddlewareProperties(name, mwName, mwProps, result);
            }
        }
    }

    private void ValidateRequiredMiddlewareProperties(
        string serviceName,
        string mwType,
        Dictionary<string, object> props,
        ValidationResult result)
    {
        var prefix = $"Service '{serviceName}' middleware '{mwType}'";

        switch (mwType)
        {
            case "redirectregex":
                if (!props.ContainsKey("regex"))
                    result.AddError($"{prefix} is missing required property 'regex'.");
                if (!props.ContainsKey("replacement"))
                    result.AddError($"{prefix} is missing required property 'replacement'.");
                break;
            case "redirectscheme":
                if (!props.ContainsKey("scheme"))
                    result.AddError($"{prefix} is missing required property 'scheme'.");
                break;
            case "addprefix":
                if (!props.ContainsKey("prefix"))
                    result.AddError($"{prefix} is missing required property 'prefix'.");
                break;
            case "ratelimit":
                if (!props.ContainsKey("average"))
                    result.AddError($"{prefix} is missing required property 'average'.");
                break;
            case "circuitbreaker":
                if (!props.ContainsKey("expression"))
                    result.AddError($"{prefix} is missing required property 'expression'.");
                break;
            case "retry":
                if (!props.ContainsKey("attempts"))
                    result.AddError($"{prefix} is missing required property 'attempts'.");
                break;
            case "forwardauth":
                if (!props.ContainsKey("address"))
                    result.AddError($"{prefix} is missing required property 'address'.");
                break;
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

    private static readonly Regex ImageFormatRegexPattern =
    new(@"^[a-zA-Z0-9]([a-zA-Z0-9._\-/]*[a-zA-Z0-9])?(:[a-zA-Z0-9][a-zA-Z0-9._\-]*)?$", RegexOptions.Compiled);
}
