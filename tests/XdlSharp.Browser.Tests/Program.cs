using XdlSharp.Checks;

int failures = 0;
foreach ((string name, Action run) in Checks.All)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {exception}");
    }
}

Console.WriteLine($"{Checks.All.Count - failures} passed, {failures} failed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
return failures == 0 ? 0 : 1;
