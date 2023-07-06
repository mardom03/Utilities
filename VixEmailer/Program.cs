using Serilog;
using Serilog.Core;
using System.Reflection.Metadata.Ecma335;

namespace VixEmailer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args).UseContentRoot(AppContext.BaseDirectory)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "Vix Emailer";
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<Logger>((svc) => { 
                        return new LoggerConfiguration().WriteTo.File("logs/VixEmailer.txt").CreateLogger(); 
                    });
                    services.AddSingleton<IConfigurationRoot>((svcs) =>
                    {
                        return new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile(@"appsettings.json")
                        .Build();
                    });
                    services.AddHostedService<Worker>();
                })
                .Build();

            host.Run();
        }
    }
}