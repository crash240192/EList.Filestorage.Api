using EList.Common.Configuration;
using LinqToDB.Configuration;

namespace EList.DbDataProvider
{
    public class ElistLinq2dbSettings : ILinqToDBSettings
    {
        private readonly string[] connectionNames;

        public ElistLinq2dbSettings()
        { 
            this.connectionNames = new string[] { DefaultConfiguration };
        }

        public ElistLinq2dbSettings(string[] connectionNames)
        {
            this.connectionNames = connectionNames;
        }

        public string DefaultConfiguration => "elist_main_db";

        public string DefaultDataProvider
        {
            get
            {
                var key = "connectionStrings:" + DefaultConfiguration + ":providerName";
                var providerName = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrWhiteSpace(providerName))
                    throw new ApplicationException($"Missing database providerName value. Please check '{key}' value in appsettings.json file");
                return providerName;
            }
        }

        public IEnumerable<IDataProviderSettings> DataProviders
        {
            get { yield break; }
        }

        public IEnumerable<IConnectionStringSettings> ConnectionStrings
        {
            get
            {
                var settings = new List<IConnectionStringSettings>();

                foreach (var connectionName in connectionNames)
                {
                    var connectionStringKey = "connectionStrings:" + connectionName + ":connectionString";
                    var providerNameKey = "connectionStrings:" + connectionName + ":providerName";

                    var connectionString = ConfigurationManager.AppSettings[connectionStringKey];
                    var providerName = ConfigurationManager.AppSettings[providerNameKey];

                    var connection = new ConnectionStringSettings(connectionName, connectionString, providerName);

                    if (string.IsNullOrWhiteSpace(connection.ConnectionString))
                        throw new ApplicationException($"Missing database connectionString value. Please check '{connectionString}' value in appsettings.json file");

                    if (string.IsNullOrWhiteSpace(connection.ProviderName))
                        throw new ApplicationException($"Missing database providerName value. Please check '{providerName}' value in appsettings.json file");

                    settings.Add(connection);
                }

                return settings;
            }
        }
    }
}