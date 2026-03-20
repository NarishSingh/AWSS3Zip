using AWSS3Zip.Commands.Contracts;

namespace AWSS3Zip.Start;

public class CommandLineRunner(IProcessFactory<IProcessJob> _jobFactory)
{
    public void Run(string[] args)
    {
        if (args.Length == 0)
            Console.WriteLine(File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}Text\\Welcome.txt")); // no args -> display readme
        else
            _jobFactory.Build(args).Execute();
    }
}
