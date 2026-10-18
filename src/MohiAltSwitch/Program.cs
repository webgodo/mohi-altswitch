namespace MohiAltSwitch;

internal static class Program
{
    private static int Main()
    {
        // One copy at a time: a second launch just exits and leaves the running one alone.
        using var instance = new Mutex(initiallyOwned: true, @"Local\MohiAltSwitch.Instance", out bool first);
        if (!first)
            return 0;

        return new App().Run();
    }
}
