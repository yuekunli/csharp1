using DriverCatalogImporter;

namespace DriverCatalogImportWorkerService
{
    public class DriverCatalogImportWorker : BackgroundService
    {
        private readonly ILogger<DriverCatalogImportWorker> _logger;
        private readonly AImporter? _importer;

        public DriverCatalogImportWorker(ILogger<DriverCatalogImportWorker> logger)
        {
            _logger = logger;
            try
            {
                _importer = new AImporter(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to initialize third party driver catalog importer");
                _importer= null;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_importer == null)
            {
                Environment.Exit(1);
            }
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    _importer.Start();
                    await Task.Delay(-1, stoppingToken);
                    // Task.Dealy users Timers.Time internally, I can use Task.Delay in this file as a timer
                    // and make importer expose only a run-once API.
                    // But I want to make the interval configurable, and I want to keep the config file parsing
                    // inside importer. So only importer knows the user desired interval.
                }
            }
            catch (OperationCanceledException ex) 
            {
                _logger.LogInformation(ex, "User request worker service to stop");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Worker service stopped unexpectedly");
                Environment.Exit(1);
            }
        }
    }
}