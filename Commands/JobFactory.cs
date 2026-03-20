using AWSS3Zip.Commands.Contracts;

namespace AWSS3Zip.Commands;

public class JobFactory(ExtractJob _extractJob) : IProcessFactory<IProcessJob>
{
    private readonly List<IProcessJob> Jobs = [];

    IProcessFactory<IProcessJob> IProcessFactory<IProcessJob>.Build(string[] parameters)
    {
        if (parameters.Contains("-e") || parameters.Contains("--extract"))
            Jobs.Add(_extractJob.BuildParameters(parameters));
        else
            Console.WriteLine("Command parameter missing. Check Options!");

        return this;
    }

    public void Execute() => Jobs.ForEach(j => j.Execute());
}
