using System;
using System.Reflection;

// Reuse established functional assertions without their bundled benchmarks.
internal static class MixerRegressionRunner
{
    [STAThread]
    private static int Main()
    {
        PortableBootstrap.Initialize();
        try
        {
            foreach(string method in new[]{"BoardChecks","CoreChecks","MaxChecks"})
                typeof(SoundCardsProbe).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Console.WriteLine("PASS SoundCards functional-only assertions={0}; benchmarks excluded",
                typeof(SoundCardsProbe).GetField("checks",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null));
            return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
