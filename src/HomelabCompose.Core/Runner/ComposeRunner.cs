using System.Diagnostics;

namespace HomelabCompose.Core.Runner;

public class ComposeRunner
{
    public int Validate(string composeFilePath)
    {
        Console.WriteLine("Validating generated compose file...\n");
        return RunDockerCompose(composeFilePath, "config", "--quiet");
    }

    public int DryRun(string composeFilePath)
    {
        Console.WriteLine("Dry run — showing what would change...\n");
        return RunDockerCompose(composeFilePath, "up", "-d", "--dry-run");
    }

    public int Apply(string composeFilePath)
    {
        Console.WriteLine("Applying — running docker compose up...\n");
        return RunDockerCompose(composeFilePath, "up", "-d");
    }

    private int RunDockerCompose(string composeFilePath, params string[] args)
    {
        var arguments = $"compose -f \"{composeFilePath}\" {string.Join(" ", args)}";

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  > docker {arguments}");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) Console.WriteLine($"  {e.Data}");
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  {e.Data}");
                    Console.ResetColor();
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            Console.WriteLine();
            if (process.ExitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Docker Compose completed successfully.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Docker Compose failed with exit code {process.ExitCode}.");
            }
            Console.ResetColor();

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to run docker compose: {ex.Message}");
            Console.WriteLine("Make sure Docker is installed and running.");
            Console.ResetColor();
            return 1;
        }
    }
}
