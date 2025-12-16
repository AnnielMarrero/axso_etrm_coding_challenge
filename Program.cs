using Axpo;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int TOTAL_PERIODS = 24;
    static async Task<int> Main(string[] args)
    {
        var logger = LoggerFactoryHelper.CreateLogger<Program>();

        // Load settings from args / appsettings.json / defaults
        var (outputFolderFromArgsOrConfig, intervalMinutesFromArgsOrConfig) = LoadSettings(args);
        logger.LogInformation($"Using OutputFolder=\"{outputFolderFromArgsOrConfig}\", IntervalMinutes={intervalMinutesFromArgsOrConfig}");
        
        logger.LogInformation("Service started. Press Ctrl+C to stop.");
        PowerService powerService = new();
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true; // prevent abrupt termination
            cts.Cancel();
            logger.LogInformation("Stopping...");
        };

        await RunPeriodicTaskAsync(cts.Token, outputFolderFromArgsOrConfig, intervalMinutesFromArgsOrConfig, logger, powerService);

        logger.LogInformation("Service stopped.");
        return 0;

    }


    static async Task RunPeriodicTaskAsync(CancellationToken token, string outputFolder, int intervalMinutes, ILogger<Program> logger, PowerService powerService)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Run work immediately
                await DoWorkAsync(token, outputFolder, logger, powerService);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Error: {ex}");
            }

            // Wait for next tick, but exit early if cancelled
            if (!await timer.WaitForNextTickAsync(token))
                break;
        }
    }

    /// <summary>
    /// Main Logic
    /// </summary>
    /// <param name="token"></param>
    /// <param name="outputFolder"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    static async Task DoWorkAsync(CancellationToken token, string outputFolder, ILogger<Program> logger, PowerService powerService)
    {
        logger.LogInformation("Next execution...");
        logger.LogInformation($"Task started at {DateTime.Now}");
        // do async work
        var data = await GetDataAsync(logger, powerService);
        await ExtractCsvAsync(data, outputFolder, logger);
        logger.LogInformation($"Task finished at {DateTime.Now}");
    }

    /// <summary>
    /// Get data from the service and return aggregated volumes per period
    /// </summary>
    /// <param name="logger"></param>
    /// <returns></returns>
    static async Task<(string Period, double Volume)[]> GetDataAsync(ILogger<Program> logger, PowerService powerService)
    {
        var data = new (string Period, double Volume)[TOTAL_PERIODS];
        for (int i = 0; i < TOTAL_PERIODS; i++)
        {
            string periodLabel = ((i + TOTAL_PERIODS - 1) % TOTAL_PERIODS).ToString().PadLeft(2, '0') + ":00";
            data[i] = (periodLabel, 0);
        }
        
        var currentDate = DateTime.Now;
        // ensure retries on failure
        while (true) {
            try
            {
                var trades = await powerService.GetTradesAsync(currentDate);
                if (trades != null && trades.Any())
                {
                    foreach (var trade in trades)
                    {
                        foreach (PowerPeriod period in trade.Periods)
                        {
                            data[period.Period - 1].Volume += period.Volume;
                        }
                    }

                }
                break;
            }
            catch (PowerServiceException ex)
            {
                logger.LogError($"Error fetching data from PowerService: {ex.Message}. Retrying...");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error while fetching data: {ex.Message}. Retrying...");
            }
        }
            
        
        return data;
    }

    /// <summary>
    /// Build CSV
    /// </summary>
    /// <param name="data"></param>
    /// <param name="outputFolder"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    static async Task ExtractCsvAsync( (string Period, double Volume) [] data, string outputFolder, ILogger<Program> logger) {
        var rows = new List<string>
        {
            "Local Time,Volume" // header
        };
        foreach (var (Period, Volume) in data)
        {
            rows.Add($"{Period},{Math.Round(Volume)}");
        }
        DateTime extractTimeLocal = DateTime.Now;
        // Filename: PowerPosition_YYYYMMDD_HHMM.csv using extract local time
        var fileName = $"PowerPosition_{extractTimeLocal:yyyyMMdd}_{extractTimeLocal:HHmm}.csv";
        var filePath = Path.Combine(outputFolder, fileName);
        await File.WriteAllLinesAsync(filePath, rows, Encoding.UTF8);
        
        logger.LogInformation($"[{extractTimeLocal:yyyy-MM-dd HH:mm:ss}] Wrote CSV: {filePath}");

    }


    /// <summary>
    /// Load the settings from command-line arguments or configuration files.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>Tuple containing the output folder and the interval in minutes.</returns>
    static (string outputFolder, int intervalMinutes) LoadSettings(string[] args)
    {
        // 3) defaults
        string defaultFolder = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        int defaultInterval = 15;

        // 1) Try command-line args
        if (TryGetFromArgs(args, out var argFolder, out var argInterval))
        {
            return (argFolder, argInterval);
        }

        // 2) Try appsettings.json
        if (TryGetFromConfig(out var cfgFolder, out var cfgInterval))
        {
            return (cfgFolder, cfgInterval);
        }

        // 3) Fallback to defaults
        return (defaultFolder, defaultInterval);
    }
    static bool TryGetFromArgs(string[] args, out string outputFolder, out int intervalMinutes)
    {
        outputFolder = null;
        intervalMinutes = 0;

        if (args == null || args.Length < 2)
            return false;

        string folder = args[0];
        bool validInterval = int.TryParse(args[1], out int interval) && interval > 0;

        if (!string.IsNullOrWhiteSpace(folder) &&
            Directory.Exists(folder) &&
            validInterval)
        {
            outputFolder = folder;
            intervalMinutes = interval;
            return true;
        }

        return false;
    }
    static bool TryGetFromConfig(out string outputFolder, out int intervalMinutes)
    {
        outputFolder = null;
        intervalMinutes = 0;

        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (!File.Exists(configPath))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = doc.RootElement;

            string folder = root.GetProperty("OutputFolder").GetString();
            string intervalRaw = root.GetProperty("IntervalMinutes").ToString();

            if (string.IsNullOrWhiteSpace(folder))
                return false;

            if (!Directory.Exists(folder))
                return false;

            if (!int.TryParse(intervalRaw, out int interval) || interval <= 0)
                return false;

            outputFolder = folder;
            intervalMinutes = interval;
            return true;
        }
        catch
        {
            // Any parsing/IO error → treat config as invalid
            return false;
        }
    }

}