using System;
using System.Reflection;
using Sharp.Data;
using Sharp.Data.Log;

namespace Sharp.Migrations {
    public class SeedRunner : ISeedRunner {
        private IDataClient _dataClient;
        private Assembly _targetAssembly;

        public static ISharpLogger Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.Name);

        public SeedRunner(IDataClient dataClient, Assembly targetAssembly) {
            _dataClient = dataClient;
            _targetAssembly = targetAssembly;
        }

        public void Run(string seedName, string param = null) {
            Type seedType = MigrationFinder.FindSeed(_targetAssembly, seedName);
            Log.Info("Starting seed migration");
            //Log.Info("Migration group: " + VersionRepository.GetMigrationGroup(migrationGroup));
            Log.Info(String.Format("Applying Seed -> [{0}]", seedName));

            var migration = (SeedMigration) Activator.CreateInstance(seedType);
            migration.SetDataClient(_dataClient);
            migration.Up(param);
            _dataClient.Commit();
        }
    }
}