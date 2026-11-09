using XdlSharp.Checks;

namespace XdlSharp.Tests;

public class CheckTests
{
    private static IEnumerable<string> Names => Checks.Checks.All.Select(check => check.Name);

    [TestCaseSource(nameof(Names))]
    public void Run(string name)
    {
        Checks.Checks.All.Single(check => check.Name == name).Run();
    }
}
