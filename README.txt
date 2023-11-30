CS_project1: .NET6 console application, try out and test
DriverCatalogImportConsoleApp: .NET6 console application (or Windows Application), driver catalog import, run-to-complete
DriverCatalogImporter: .NET6 library
DriverCatalogImportNetFrameworkConsoleApp: .NET Framework4.7 console application (or Windows Application), driver catalog import, run-to-complete
DriverCatalogImportWinLib: .NET Framework4.7 library, up-to-date driver catalog import code
DriverCatalogImportWinService: .NET Framework4.7 background service, reference DriverCatalogImportWinLib
DriverCatalogImportWixPkg: package a background service application
DriverCatalogImportWorkerService: .NET6 backgroud service, reference DriverCatalogImporter
NetFrameworkConsoleApp1: .NET Framework4.7 console application (or Windows Application), try out and test


.NET Framework:                               .NET 6

NetFrameworkConsoleApp1                       CS_project1

DriverCatalogImportWinService                 DriverCatalogImportWorkerService

DriverCatalogImportNetFrameworkConsoleApp     DriverCatalogImportConsoleApp

DriverCatalogImportWinLib                     DriverCatalogImporter

                        DriverCatalogImportWixPkg
