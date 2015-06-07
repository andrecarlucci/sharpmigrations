using System;
using System.IO;
using System.Reflection;
using log4net.Config;
using Sharp.Data;

namespace Sharp.Migrator {
    public class Program {
        public static Migrator Migrator;

        public static void Main(string[] args) {
            //string[] files = Directory.GetFiles(Path.GetFullPath("."), "*.dll");
            //foreach(string file in files) { File.Delete(file); }
            //args = @"-a|..\..\..\Sharp.Tests.Chinook\bin\Debug\Sharp.Tests.Chinook.exe".Split('|');
            //args = @"-a|..\..\..\Sharp.Tests.Chinook\bin\Debug\Sharp.Tests.Chinook.exe|-m|manual|-f|sql.txt|-v|-1|-c|Data Source=//localhost:1521/XE;User Id=sharp2;Password=sharp2;|-p|Oracle.ManagedDataAccess.Client".Split('|');
            //args = @"-a|..\..\..\Sharp.Tests.Chinook\bin\Debug\Sharp.Tests.Chinook.exe|-c|Data Source=//localhost:1521/XE;User Id=sharp;Password=sharp;|-p|Oracle.ManagedDataAccess.Client|-g|plugin|-m|script|-f|script.sql".Split('|');
            //args = @"-a|c:\dev\opensource\sharpmigrations\Sharp.Tests.Northwind\bin\Debug\Sharp.Tests.Northwind.exe".Split('|');
            //args = @"-a|C:\dev\test\TestApp\TestApp\bin\Debug\TestApp.exe|-p|Oracle.DataAccess.Client|-c|Data Source=//localhost:1521/XE; User Id=sharp; Password=sharp;|-m|auto|-v|0".Split('|');
            //args = @"-a|C:\dev\test\TestApp\TestApp\bin\Debug\TestApp.exe|-c|Data Source=//localhost:1521/XE; User Id=sharp; Password=sharp;|-m|auto".Split('|');
            //args = @"-a|C:\dev\test\TestApp\TestApp\bin\Debug\TestApp.exe|-c|Data Source=//localhost:1521/XE; User Id=sharp; Password=sharp;|-m|seed|-s|asdf".Split('|');
            //args = @"-a|C:\dev\test\TestApp\TestApp\bin\Debug\TestApp.exe|-p|Oracle.ManagedDataAccess.Client|-c|Data Source=//localhost:1521/XE; User Id=sharp; Password=sharp;".Split('|');

            //args = @"-a|C:\dev\way2\pim\Way2Pim.Migrations\bin\Debug\Way2Pim.Migrations.exe|-n|Oracle|-g|Way2Pim.Server".Split('|');
            //args = @"-a|C:\dev\opensource\Hangfire\src\HangFire.Oracle\bin\Debug\Hangfire.Oracle.dll|-p|Oracle.ManagedDataAccess.Client|-c|Data Source=//localhost:1521/XE;User Id=hangfire;Password=hangfire".Split('|');
            args = @"-a|Data Source=//localhost:1521/XE;User Id=ccee;Password=ccee;|-p|Oracle.ManagedDataAccess.Client|-m|script|-f|script.txt|-a|c:\dev\opensource\Hangfire\src\HangFire.Oracle\bin\Debug\Hangfire.Oracle.dll".Split('|');

            AppDomain.CurrentDomain.AssemblyResolve += ResolveNotFoundAssembly;

            XmlConfigurator.Configure();
            Migrator = new Migrator(args);
            try {
                Migrator.Start();
            }
            catch (Exception ex) {
                Console.WriteLine();
                Console.WriteLine("Error running migrator: ");
                Console.WriteLine(ExceptionHelper.GetAllErrors(ex));
            }
        }

        public static Assembly ResolveNotFoundAssembly(object sender, ResolveEventArgs args) {
            var migrationsAssemblyPath = Migrator.Options.AssemblyWithMigrations;
            string applicationDirectory = Path.GetDirectoryName(migrationsAssemblyPath);

            string[] fields = args.Name.Split(',');
            string assemblyName = fields[0];
            string assemblyCulture = fields[2].Substring(fields[2].IndexOf('=') + 1);

            string assemblyFileName = assemblyName + ".dll";
            string assemblyPath;
            if (assemblyName.EndsWith(".resources")) {
                string resourceDirectory = Path.Combine(applicationDirectory, assemblyCulture);
                assemblyPath = Path.Combine(resourceDirectory, assemblyFileName);
            }
            else {
                assemblyPath = Path.Combine(applicationDirectory, assemblyFileName);
            }
            if (File.Exists(assemblyPath)) {
                Assembly loadingAssembly = Assembly.LoadFrom(assemblyPath);
                return loadingAssembly;
            }
            return null;
        }
    }
}