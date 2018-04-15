using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SharpVk.Multivendor;
using Tectonic;

using static SharpVk.Multivendor.DebugReportFlags;

namespace FieldWarning
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                                .Enrich.FromLogContext()
                                .WriteTo.Trace()
                                .WriteTo.RollingFile(".\\logs\\FieldWarning-{Date}.log", buffered: true)
                                .MinimumLevel.Debug()
                                .CreateLogger();

            Game.Run(services =>
                {
                    services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
                            .AddVulkan("Project Field Warning", (1, 0, 0), Error | Warning)
                            .AddVulkanLayer("VK_LAYER_LUNARG_standard_validation", isOptional: true)
                            .AddVulkanExtension(ExtExtensions.DebugReport, true)
                            .AddGlfwService()
                            .AddGameService<LifecycleService>();
                },
                provider =>
                {
                });

            Log.CloseAndFlush();
        }
    }
}
