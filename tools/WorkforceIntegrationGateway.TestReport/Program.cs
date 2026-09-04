using System.Text.Json;

if (args.Length == 0)
{
    Console.Error.WriteLine("Provide one or more CTRF JSON test-result files.");
    return 2;
}

var exitCode = 0;

foreach (var path in args)
{
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var summary = document.RootElement.GetProperty("results").GetProperty("summary");
        var discovered = summary.GetProperty("tests").GetInt32();
        var passed = summary.GetProperty("passed").GetInt32();
        var failed = summary.GetProperty("failed").GetInt32();
        var skipped = summary.GetProperty("skipped").GetInt32();
        var pending = summary.GetProperty("pending").GetInt32();
        var other = summary.GetProperty("other").GetInt32();
        var executed = passed + failed;
        var notRun = pending + other;
        var accountedFor = passed + failed + skipped + notRun;
        var name = Path.GetFileNameWithoutExtension(path);

        Console.WriteLine(
            "TEST_REPORT {0} discovered={1} executed={2} passed={3} failed={4} skipped={5} not_run={6}",
            name,
            discovered,
            executed,
            passed,
            failed,
            skipped,
            notRun);

        if (discovered == 0 || accountedFor != discovered || failed != 0 || skipped != 0 || notRun != 0)
        {
            exitCode = 1;
        }
    }
    catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException)
    {
        Console.Error.WriteLine($"Unable to verify test report '{Path.GetFileName(path)}'.");
        exitCode = 1;
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

return exitCode;
