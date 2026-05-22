using HomelabCompose.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabCompose.Core.Generators;

/// <summary>
/// Generates Docker Compose YAML configurations from a homelab schema definition.
/// </summary>
/// <remarks>The generated configuration includes all services, volumes, and networks specified in the schema, and
/// automatically incorporates a Cloudflare tunnel service if present. The output uses Docker Compose naming conventions
/// and omits null values for clarity. This class is intended for scenarios where automated, schema-driven generation of
/// Docker Compose files is required.</remarks>
public class DockerComposeGenerator : IConfigGenerator
{
    public string FileName => "docker-compose.yml";

    /// <summary>
    /// Generates a Docker Compose YAML configuration based on the specified homelab schema.
    /// </summary>
    /// <remarks>The generated YAML includes all services, volumes, and networks defined in the schema. If a
    /// Cloudflare tunnel is specified, it is included as a service. The output omits null values for brevity and uses
    /// underscored naming conventions to match Docker Compose standards.</remarks>
    /// <param name="schema">The schema describing the services, volumes, networks, and optional Cloudflare tunnel to include in the
    /// generated configuration. Cannot be null.</param>
    /// <returns>A string containing the generated Docker Compose YAML configuration.</returns>
    public string Generate(HomelabSchema schema)
    {
        var compose = new Dictionary<string, object?>();
        var services = new Dictionary<string, object>();

        services["traefik"] = BuildTraefikService(schema);

        if (schema.CloudflareTunnel != null)
            services["cloudflared"] = BuildCloudflaredService(schema);

        foreach (var (name, service) in schema.Services)
            services[name] = BuildService(name, service, schema);

        compose["services"] = services;

        if (schema.Volumes.Count > 0)
        {
            var volumes = new Dictionary<string, object?>();
            foreach (var key in schema.Volumes.Keys)
                volumes[key] = null;
            compose["volumes"] = volumes;
        }

        if (schema.Networks.Count > 0)
        {
            var networks = new Dictionary<string, object?>();
            foreach (var (key, net) in schema.Networks)
            {
                if (!string.IsNullOrWhiteSpace(net.Name))
                    networks[key] = new Dictionary<string, object> { ["name"] = net.Name };
                else
                    networks[key] = null;
            }
            compose["networks"] = networks;
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithEventEmitter(next => new QuotedSequenceEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(compose);
    }

    /// <summary>
    /// Builds a dictionary representing the configuration for a Traefik service based on the specified schema.
    /// </summary>
    /// <remarks>The returned dictionary is intended for use in generating service definitions, such as those
    /// used in Docker Compose files. The configuration reflects the settings provided in the input schema, including
    /// entrypoints, dashboard options, and logging level.</remarks>
    /// <param name="schema">The schema containing Traefik and environment configuration settings used to construct the service definition.
    /// Cannot be null.</param>
    /// <returns>A dictionary containing key-value pairs that define the Traefik service configuration, including image,
    /// container name, restart policy, command-line arguments, ports, volumes, and networks.</returns>
    private Dictionary<string, object> BuildTraefikService(HomelabSchema schema)
    {
        var traefik = schema.Traefik;
        var service = new Dictionary<string, object>
        {
            ["image"] = "traefik:latest",
            ["container_name"] = "traefik",
            ["restart"] = schema.Defaults.Restart,
        };

        var command = new List<string>();
        if (traefik.Dashboard)
            command.Add("--api.dashboard=true");
        if (traefik.DashboardInsecure)
            command.Add("--api.insecure=true");
        foreach (var (name, address) in traefik.Entrypoints)
            command.Add($"--entrypoints.{name}.address={address}");
        command.Add("--providers.docker=true");
        command.Add($"--providers.docker.exposedbydefault={traefik.ExposeByDefault.ToString().ToLower()}");
        command.Add($"--log.level={traefik.LogLevel}");
        service["command"] = command;

        var ports = new List<string>();
        foreach (var (_, address) in traefik.Entrypoints)
        {
            var port = address.TrimStart(':');
            ports.Add($"{port}:{port}");
        }
        if (traefik.DashboardInsecure)
            ports.Add("8080:8080");
        service["ports"] = ports;

        service["volumes"] = new List<string>
        {
            $"{traefik.DockerSocket}:/var/run/docker.sock:ro"
        };

        service["networks"] = BuildNetworkEntry("proxy");

        return service;
    }

    /// <summary>
    /// Builds a dictionary representing the configuration for a Cloudflared service using the specified schema.
    /// </summary>
    /// <remarks>The returned dictionary is intended for use in service orchestration scenarios, such as
    /// generating Docker Compose configurations. The method uses values from the provided schema to ensure consistency
    /// with other services.</remarks>
    /// <param name="schema">The schema containing default values and settings used to configure the Cloudflared service. Cannot be null.</param>
    /// <returns>A dictionary containing key-value pairs that define the Cloudflared service configuration, including image,
    /// container name, restart policy, command, environment variables, and network settings.</returns>
    private Dictionary<string, object> BuildCloudflaredService(HomelabSchema schema)
    {
        return new Dictionary<string, object>
        {
            ["image"] = "cloudflare/cloudflared:latest",
            ["container_name"] = "cloudflared",
            ["restart"] = schema.Defaults.Restart,
            ["command"] = "tunnel run",
            ["environment"] = new Dictionary<string, string>
            {
                ["TUNNEL_TOKEN"] = "${TUNNEL_TOKEN}"
            },
            ["networks"] = BuildNetworkEntry("proxy"),
        };
    }

    /// <summary>
    /// Builds a dictionary representing the configuration for a service based on the provided service definition and
    /// schema defaults.
    /// </summary>
    /// <remarks>The resulting dictionary includes only the configuration entries relevant to the provided
    /// service definition. Optional fields are included only if specified in the input. This method does not validate
    /// the completeness or correctness of the resulting configuration.</remarks>
    /// <param name="name">The name to assign to the service container. This value is used as the container name in the resulting
    /// configuration.</param>
    /// <param name="service">The service definition containing image, environment, dependencies, and other configuration details for the
    /// service.</param>
    /// <param name="schema">The schema providing default values and settings to apply to the service configuration.</param>
    /// <returns>A dictionary containing key-value pairs that represent the service configuration, suitable for use in container
    /// orchestration or deployment scenarios.</returns>
    private Dictionary<string, object?> BuildService(
        string name, ServiceDefinition service, HomelabSchema schema)
    {
        var svc = new Dictionary<string, object?>
        {
            ["image"] = service.Image,
            ["container_name"] = name,
            ["restart"] = schema.Defaults.Restart,
        };

        if (!string.IsNullOrWhiteSpace(service.Command))
            svc["command"] = service.Command;

        if (service.DependsOn is { Count: > 0 })
        {
            var dependsOn = new Dictionary<string, object>();
            foreach (var (dep, condition) in service.DependsOn)
                dependsOn[dep] = new Dictionary<string, object> { ["condition"] = condition };
            svc["depends_on"] = dependsOn;
        }

        if (service.Environment.Count > 0)
            svc["environment"] = service.Environment;

        if (service.Volumes.Count > 0)
            svc["volumes"] = service.Volumes;

        if (service.Ports is { Count: > 0 })
            svc["ports"] = service.Ports;

        if (service.CapAdd is { Count: > 0 })
            svc["cap_add"] = service.CapAdd;

        if (service.Networks.Count > 0)
        {
            var networks = new Dictionary<string, object?>();
            foreach (var (netName, netConfig) in service.Networks)
            {
                if (netConfig?.Aliases is { Count: > 0 })
                    networks[netName] = new Dictionary<string, object> { ["aliases"] = netConfig.Aliases };
                else
                    networks[netName] = null;
            }
            svc["networks"] = networks;
        }

        if (service.Healthcheck != null)
        {
            svc["healthcheck"] = new Dictionary<string, object>
            {
                ["test"] = service.Healthcheck.Test,
                ["interval"] = service.Healthcheck.Interval,
                ["timeout"] = service.Healthcheck.Timeout,
                ["retries"] = service.Healthcheck.Retries,
            };
        }

        if (service.Routing != null)
            svc["labels"] = BuildTraefikLabels(name, service.Routing, service.Networks.Count > 1);

        return svc;
    }

    /// <summary>
    /// Builds a list of Traefik label strings for configuring service routing, middlewares, and routers based on the
    /// specified service name and routing configuration.
    /// </summary>
    /// <remarks>The generated labels can be applied to Docker containers to configure Traefik's dynamic
    /// routing, middleware chains, and entrypoints for the service. Middleware and router names are automatically
    /// prefixed with the service name to ensure uniqueness.</remarks>
    /// <param name="serviceName">The name of the service for which Traefik labels are generated. Used as a prefix in label keys to uniquely
    /// identify the service.</param>
    /// <param name="routing">The routing configuration that defines ports, middlewares, and routers for the service. Must not be null.</param>
    /// <param name="multipleNetworks">true to include a label for the 'proxy' Docker network; otherwise, false.</param>
    /// <returns>A list of strings containing Traefik label definitions for the specified service and routing configuration.</returns>
    private List<string> BuildTraefikLabels(
        string serviceName, RoutingConfig routing, bool multipleNetworks)
    {
        var labels = new List<string>
        {
            "traefik.enable=true",
            $"traefik.http.services.{serviceName}.loadbalancer.server.port={routing.Port}",
        };

        // Middleware definitions
        var middlewareNames = new List<string>();
        foreach (var (mwName, mwProps) in routing.Middlewares)
        {
            var fullName = $"{serviceName}-{mwName}";
            middlewareNames.Add(fullName);

            foreach (var (prop, value) in mwProps)
                labels.Add($"traefik.http.middlewares.{fullName}.{mwName}.{prop}={value}");
        }

        var middlewareChain = string.Join(",", middlewareNames);

        // Routers
        foreach (var (routerName, router) in routing.Routers)
        {
            var fullRouterName = routerName == "local"
                ? serviceName
                : $"{serviceName}-{routerName}";

            labels.Add(
                $"traefik.http.routers.{fullRouterName}.rule=Host(`{router.Host}`)");
            labels.Add(
                $"traefik.http.routers.{fullRouterName}.entrypoints={string.Join(",", router.Entrypoints)}");

            if (routerName != "local")
                labels.Add($"traefik.http.routers.{fullRouterName}.service={serviceName}");

            if (middlewareNames.Count > 0)
                labels.Add($"traefik.http.routers.{fullRouterName}.middlewares={middlewareChain}");
        }

        if (multipleNetworks)
            labels.Add("traefik.docker.network=proxy");

        return labels;
    }

    /// <summary>
    /// Creates a dictionary entry with the specified network name as the key and a null value.
    /// </summary>
    /// <param name="networkName">The name of the network to use as the key in the returned dictionary. Cannot be null.</param>
    /// <returns>A dictionary containing a single entry where the key is the specified network name and the value is null.</returns>
    private static Dictionary<string, object?> BuildNetworkEntry(string networkName)
    {
        return new Dictionary<string, object?> { [networkName] = null };
    }

}
