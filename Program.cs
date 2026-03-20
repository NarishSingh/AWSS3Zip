using AWSS3Zip.Commands;
using AWSS3Zip.Commands.Contracts;
using AWSS3Zip.Start;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AWSS3Zip;

internal class Program
{
    private static void Main(string[] args)
    {
        IHost host = CreateHostBuilder(args).Build();
        CommandLineRunner service = host.Services.GetRequiredService<CommandLineRunner>();
        service.Run(args);
    }

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, svc) =>
        {
            svc.AddSingleton<ExtractJob, ExtractJob>();
            svc.AddSingleton<IProcessFactory<IProcessJob>, JobFactory>();

            svc.AddTransient<CommandLineRunner, CommandLineRunner>();
        });
}
