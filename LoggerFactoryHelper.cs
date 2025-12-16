using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;

public static class LoggerFactoryHelper
{
    /// <summary>
    /// Initializes Serilog to log to both console and file, and returns an ILogger<T>.
    /// </summary>
    public static ILogger<T> CreateLogger<T>()
    {
        // Folder to store logs (relative to launch directory)
        var launchFolder = Environment.CurrentDirectory;
        var logFolder = Path.Combine(launchFolder, "logs");
        Directory.CreateDirectory(logFolder);

        var logPath = Path.Combine(logFolder, "log-.txt"); // rolling daily

        // Configure Serilog with multiple sinks: File + Console
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        // Setup DI logging to use Serilog
        var serviceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders(); // remove default console logger
                loggingBuilder.AddSerilog();
            })
            .BuildServiceProvider();

        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}
