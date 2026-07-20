namespace CapaModelo.Common
{
    public class Fr3Configuration
    {
        public Fr3ProcessingMode Mode { get; set; }
        public bool TransactionRequired { get; set; }
        public bool AutomaticRetryEnabled { get; set; }
        public int MaxIntentos { get; set; }
        public int BaseBackoffSeconds { get; set; }
        public int LeaseDurationSeconds { get; set; }
    }
}
