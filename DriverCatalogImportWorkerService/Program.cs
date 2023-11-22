using DriverCatalogImportWorkerService;
using DriverCatalogImporter;

IHostBuilder builder = Host.CreateDefaultBuilder(args)
   .UseWindowsService(options => {
       options.ServiceName = "Driver Catalog Import";
   })
   .ConfigureServices((context, services) =>
   {
       services.AddSingleton<AImporter>();
       services.AddHostedService<DriverCatalogImportWorker>();
   });
    

IHost host = builder.Build();
await host.RunAsync();
