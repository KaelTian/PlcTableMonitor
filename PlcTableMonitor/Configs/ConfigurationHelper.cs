using Microsoft.Extensions.Configuration;

namespace PlcTableMonitor.Configs
{
    public static class ConfigurationHelper
    {
        private static IConfiguration _configuration;

        static ConfigurationHelper()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        public static AppSettings GetAppSettings()
        {
            return _configuration.Get<AppSettings>()!;
        }
    }
}
