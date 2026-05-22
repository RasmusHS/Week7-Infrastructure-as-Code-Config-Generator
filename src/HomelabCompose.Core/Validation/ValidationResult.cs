namespace HomelabCompose.Core.Validation;

public class ValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool IsValid => _errors.Count == 0;

    public void AddError(string message) => _errors.Add(message);
    public void AddWarning(string message) => _warnings.Add(message);

    public void PrintToConsole()
    {
        foreach (var warning in _warnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  WARNING: {warning}");
        }

        foreach (var error in _errors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ERROR: {error}");
        }

        Console.ResetColor();

        if (IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(_warnings.Count == 0
                ? "\nSchema is valid."
                : $"\nSchema is valid with {_warnings.Count} warning(s).");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nSchema has {_errors.Count} error(s) and {_warnings.Count} warning(s).");
            Console.ResetColor();
        }
    }

}
