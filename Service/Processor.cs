using System.Diagnostics;

namespace AWSS3Zip.Service;

public static class Processor
{
    public static void InvokeProcess(string command, string arguments)
    {
        ProcessStartInfo processStartInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(processStartInfo);
        using StreamReader reader = process.StandardOutput;

        Console.WriteLine(reader.ReadToEnd());
    }
}
