using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using CommandLine;
using Sharp.Data;
using Sharp.Migrations;
using Sharp.Migrations.Runners;
using Sharp.Migrations.Runners.ScriptCreator;

namespace Sharp.Migrator {
    public class Migrator {
        private readonly string[] _args;
        public Options Options { get; set; }

        public Migrator(string[] args) {
            _args = args;
        }

        public void Start() {
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Sharp Migrator v" + Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("--------------------------------");
            PrintPlataform();
            try {
                ParseConfig();
            }
            catch (ArgumentException ex) {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine(Options.GetUsage());
                Exit();
            }
            PrintMigrationGroup();
            PrintDataSource(SharpFactory.Default.ConnectionString);
            Run();
        }

        public void ParseConfig() {
            Options = new Options();
            if (ParseArgs()) {
                throw new ArgumentException("Could not parse args");
            }
            SetSharpConfig();
        }

        private bool ParseArgs() {
            return _args.Length == 0 || !Parser.Default.ParseArguments(_args, Options);
        }

        private void SetSharpConfig() {
            if (TrySetConnectionStringFromArgs() || TrySetConnectionStringFromConfigFile()) {
                SetSharpConfigFromOptions();
                return;
            }
            throw new ArgumentException("Please, set a connectionstring");
        }

        private bool TrySetConnectionStringFromArgs() {
            return !String.IsNullOrEmpty(Options.ConnectionString) && !String.IsNullOrEmpty(Options.DatabaseProvider);
        }

        private bool TrySetConnectionStringFromConfigFile() {
            if (String.IsNullOrEmpty(Options.AssemblyWithMigrations)) {
                return false;
            }
            var appConfig = ConfigurationManager.OpenExeConfiguration(Options.AssemblyWithMigrations);
            return SetConnectionStringViaConnectionName(appConfig) || SetConnectionStringViaAppConfig(appConfig);
        }

        private bool SetConnectionStringViaConnectionName(Configuration appConfig) {
            if (String.IsNullOrEmpty(Options.ConnectionStringName)) {
                return false;
            }
            Options.ConnectionString =
                appConfig.ConnectionStrings.ConnectionStrings[Options.ConnectionStringName].ConnectionString;
            Options.DatabaseProvider =
                appConfig.ConnectionStrings.ConnectionStrings[Options.ConnectionStringName].ProviderName;
            return true;
        }

        private bool SetConnectionStringViaAppConfig(Configuration appConfig) {
            var cs = GetConnectionStringSettings(appConfig.ConnectionStrings.ConnectionStrings);
            Options.ConnectionString = cs.ConnectionString;
            Options.DatabaseProvider = cs.ProviderName;
            return true;
        }

        private void SetSharpConfigFromOptions() {
            if (String.IsNullOrEmpty(Options.ConnectionString)) {
                Exit("Please, set a connectionstring");
            }
            if (String.IsNullOrEmpty(Options.DatabaseProvider)) {
                Exit("Please, set a database provider");
            }
            SharpFactory.Default.ConnectionString = Options.ConnectionString;
            SharpFactory.Default.DataProviderName = Options.DatabaseProvider;
        }

        private void PrintMigrationGroup() {
            string migrationGroup = String.IsNullOrEmpty(Options.MigrationGroup) ? "not set" : Options.MigrationGroup;
            Console.WriteLine("MigrationGroup: " + migrationGroup);
            Console.WriteLine("--------------------------------");
        }

        private static void Exit(string message = null) {
            if (message != null) {
                Console.WriteLine(message);
            }
            Environment.Exit(0);
        }

        private static void PrintPlataform() {
            int bits = IntPtr.Size == 4 ? 32 : 64;
            Console.WriteLine("Running in    : " + bits + " bits");
        }

        private static void PrintDataSource(string connectionString) {
            var start = connectionString.IndexOf("Data Source=") + "Data Source=".Length;
            var end = connectionString.IndexOf(@";", start);
            var dataSource = string.Format("Data Source: {0}", connectionString.Substring(start, end - start));
            Console.WriteLine(dataSource);
        }

        private void Run() {
            int version = Options.TargetVersion ?? -1;
            string mode = (Options.Mode ?? "manual").ToLower();
            if (mode == "script") {
                RunScript(version);
                return;
            }
            if (mode == "auto") {
                var runner = new Runner(SharpFactory.Default.CreateDataClient(), GetAssemblyWithMigrations());
                runner.MigrationGroup = Options.MigrationGroup;
                runner.Run(version);
                return;
            }
            if (mode == "seed") {
                if (String.IsNullOrEmpty(Options.SeedName)) {
                    Exit("Please, set the seed name");
                    return;
                }
                var seedRunner = new SeedRunner(SharpFactory.Default.CreateDataClient(), GetAssemblyWithMigrations());
                seedRunner.Run(Options.SeedName, Options.SeedArgs);
                return;
            }
            var crunner = new ConsoleRunner(SharpFactory.Default.ConnectionString, SharpFactory.Default.DataProviderName);
            crunner.AssemblyWithMigrations = GetAssemblyWithMigrations();
            crunner.MigrationGroup = Options.MigrationGroup;
            crunner.Start();
        }

        private Assembly GetAssemblyWithMigrations() {
            if (String.IsNullOrEmpty(Options.AssemblyWithMigrations)) {
                return Assembly.GetEntryAssembly();
            }
            return Assembly.LoadFile(Path.GetFullPath(Options.AssemblyWithMigrations));
        }

        private void RunScript(int version) {
            if (String.IsNullOrEmpty(Options.Filename)) {
                Exit();
            }
            var runner = new ScriptCreatorRunner(SharpFactory.Default.CreateDataClient(), GetAssemblyWithMigrations());
            runner.Run(version, Options.MigrationGroup);
            File.WriteAllText(Options.Filename, runner.GetCreatedScript(), Encoding.UTF8);
            Console.WriteLine(" * Check {0} for the script dump. No migrations were performed on the database.",
                Options.Filename);
        }

        private static ConnectionStringSettings GetConnectionStringSettings(ConnectionStringSettingsCollection section) {
            Console.WriteLine(
                "You have to provide a connectionstring and a database provider. I couldn't detect them, so pick one from app.config:");
            Console.WriteLine("");
            for (var i = 0; i < section.Count; i++) {
                Console.WriteLine("{0} - {1}", i, section[i].Name);
                Console.WriteLine(section[i].ConnectionString);
                Console.WriteLine();
            }
            Console.Write("> ");
            int index;
            while (!Int32.TryParse(Console.ReadLine(), out index)) {
                Console.WriteLine(ConsoleRunner.INVALID_NUMBER);
            }
            Console.WriteLine("");
            Console.WriteLine("--------------------------------");
            return section[index];
        }
    }
}