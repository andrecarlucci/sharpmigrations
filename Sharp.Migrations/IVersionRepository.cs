using System.Collections.Generic;
using Sharp.Migrations.Runners;

namespace Sharp.Migrations {
	public interface IVersionRepository {
        string MigrationTableSchema { get; set; }
	    string MigrationTableName { get; set; }
        string MigrationGroup { get; set; }
		long GetCurrentVersion();
        void EnsureVersionTable(List<MigrationInfo> allMigrationsFromAssembly);
	    List<long> GetAppliedMigrations();
        void InsertVersion(MigrationInfo migrationInfo);
        void RemoveVersion(MigrationInfo migrationInfo);
    }
}