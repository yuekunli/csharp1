namespace DriverCatalogImporter
{
    internal class ImportStats
    {
        public int Total {  get; set; }
        public int Success {  get; set; }
        public int Failure { get; set; }

        public ImportStats() { }

        public ImportStats(int total, int success, int failure)
        {
            Total = total;
            Success = success;
            Failure = failure;
        }
    }
}
