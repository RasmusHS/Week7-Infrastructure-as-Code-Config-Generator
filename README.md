# HomelabCompose

A CLI tool that generates Docker Compose files, Traefik routing labels, and Cloudflare Tunnel service definitions from a simplified YAML schema describing your homelab infrastructure.

Instead of hand-editing sprawling compose files with dozens of Traefik labels, you define your services once in a concise schema and let HomelabCompose generate the rest.

## Features

- **Schema-driven generation** — define services, routing, and middlewares in a clean YAML format; get a production-ready `docker-compose.yml` with all Traefik labels, the Traefik service itself, and a Cloudflared service generated automatically.
- **Validation** — catches misconfigurations before they hit your stack: undefined network/volume references, port conflicts, circular dependencies, unreachable dependencies (no shared network), invalid middleware properties, duplicate hostnames, and more.
- **Diff mode** — compare generated output against existing files on disk without writing anything. Uses an LCS-based diff with colored terminal output.
- **Apply mode** — optionally validate the generated compose file with Docker and run `docker compose up -d`, with a `--dry-run` safety net.

## Installation

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

```bash
git clone https://github.com/RasmusHS/HomelabCompose.git
cd HomelabCompose
dotnet build
```

## Usage

### Generate configs

```bash
dotnet run --project src/HomelabCompose.Cli -- generate -i homelab.yml -o ./output
```

### Validate a schema without generating

```bash
dotnet run --project src/HomelabCompose.Cli -- validate -i homelab.yml
```

### Show diff against existing output

```bash
dotnet run --project src/HomelabCompose.Cli -- generate -i homelab.yml --diff -o ./output
```

### Dry run with Docker

Writes files, validates the compose file, and shows what `docker compose up` would do without starting anything.

```bash
dotnet run --project src/HomelabCompose.Cli -- generate -i homelab.yml -o ./output --dry-run
```

### Apply

Writes files, validates, and runs `docker compose up -d`.

```bash
dotnet run --project src/HomelabCompose.Cli -- generate -i homelab.yml -o ./output --apply
```

## Schema Format

```yaml
name: my-homelab
domain: example.com
local_domain: home.lab

defaults:
  restart: unless-stopped

networks:
  proxy:
    name: proxy
  backend:
    name: backend

volumes:
  app_data: {}

traefik:
  dashboard: true
  dashboard_insecure: true
  entrypoints:
    web: ":80"
  log_level: WARN
  expose_by_default: false
  docker_socket: /var/run/docker.sock

cloudflare_tunnel:
  mode: token

services:
  myapp:
    image: myapp:latest
    networks:
      proxy:
        aliases: [myapp.home.lab]
      backend: {}
    environment:
      DB_HOST: postgres
    volumes:
      - app_data:/data
    depends_on:
      postgres: service_healthy
    routing:
      port: 8080
      routers:
        local:
          host: myapp.home.lab
          entrypoints: [web]
        public:
          host: myapp.example.com
          entrypoints: [web]
          tunnel: true
      middlewares:
        headers:
          stsSeconds: 15552000
          stsIncludeSubdomains: true
```

### Key concepts

**`routing` block** — the main abstraction. Defines how a service is exposed through Traefik. Each router gets its own Traefik `Host()` rule, and routers with `tunnel: true` signal that the hostname should be accessible via the Cloudflare Tunnel. Middlewares are defined per-service with typed properties that map directly to Traefik middleware labels.

**`traefik` and `cloudflare_tunnel` blocks** — configure the infrastructure services. HomelabCompose generates the Traefik and Cloudflared container definitions automatically from these blocks, so you don't define them as services.

**`defaults` block** — shared settings applied to all services (currently `restart` policy).

## Validation Rules

The validator checks for:

- Missing required fields (name, domain, image, routing host/port/entrypoints)
- Invalid image format
- Undefined network, volume, or service references
- Circular dependencies in `depends_on` chains
- Services that depend on each other but share no networks
- Port conflicts across services
- Duplicate router hostnames
- Entrypoints referencing undefined Traefik entrypoints
- Unrecognized middleware types or properties
- Missing required middleware properties (e.g. `redirectregex` without `regex`)
- `service_healthy` condition on services without a healthcheck
- Tunnel routing without `cloudflare_tunnel` configured (and vice versa)

## Project Structure

```
HomelabCompose/
├── src/
│   ├── HomelabCompose.Core/        # Library — models, parsing, validation, generation
│   │   ├── Models/                 # YAML schema models (YamlDotNet attributes)
│   │   ├── Parsing/                # SchemaParser (YAML deserialization)
│   │   ├── Validation/             # SchemaValidator + ValidationResult
│   │   ├── Generators/             # IConfigGenerator, DockerComposeGenerator
│   │   ├── Diff/                   # LCS-based diff service
│   │   └── Runner/                 # Docker Compose process runner
│   └── HomelabCompose.Cli/         # CLI shell — argument parsing, wiring
│       └── Program.cs
└── HomelabCompose.sln
```

## Dependencies

- [YamlDotNet](https://github.com/aaubry/YamlDotNet) — YAML deserialization and serialization
- [System.CommandLine](https://github.com/dotnet/command-line-api) — CLI argument parsing

## License

Apache-2.0