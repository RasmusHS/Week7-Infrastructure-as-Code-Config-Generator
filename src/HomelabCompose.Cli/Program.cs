using System.CommandLine;

// Shared options
var inputOption = new Option<FileInfo>("--input", "-i")
{
    Description = "Path to the homelab YAML schema file",
    Required = true
};

var outputOption = new Option<DirectoryInfo>("--output", "-o")
{
    Description = "Output directory for generated configs",
    DefaultValueFactory = _ => new DirectoryInfo("./output")
};

var diffOption = new Option<bool>("--diff")
{
    Description = "Show what changed instead of writing files"
};

var applyOption = new Option<bool>("--apply")
{
    Description = "Run docker compose up after generating"
};

// Generate command
var generateCommand = new Command("generate", "Generate configs from a homelab schema");
generateCommand.Options.Add(inputOption);
generateCommand.Options.Add(outputOption);
generateCommand.Options.Add(diffOption);
generateCommand.Options.Add(applyOption);

generateCommand.SetAction(parseResult =>
{
    var input = parseResult.GetValue(inputOption)!;
    var output = parseResult.GetValue(outputOption)!;
    var diff = parseResult.GetValue(diffOption);
    var apply = parseResult.GetValue(applyOption);

    // TODO: Parse YAML → Validate → Generate → (Diff / Write / Apply)
    Console.WriteLine($"Generating from: {input.FullName}");
    Console.WriteLine($"Output to: {output.FullName}");
    if (diff) Console.WriteLine("Diff mode enabled");
    if (apply) Console.WriteLine("Will run docker compose up");
});

// Validate command
var validateCommand = new Command("validate", "Validate a homelab schema without generating");
validateCommand.Options.Add(inputOption);

validateCommand.SetAction(parseResult =>
{
    var input = parseResult.GetValue(inputOption)!;

    // TODO: Parse YAML → Validate → Report
    Console.WriteLine($"Validating: {input.FullName}");
});

// Root
var rootCommand = new RootCommand("HomelabCompose — generate Docker Compose, Traefik, and Cloudflare Tunnel configs from a YAML schema");
rootCommand.Subcommands.Add(generateCommand);
rootCommand.Subcommands.Add(validateCommand);

return rootCommand.Parse(args).Invoke();