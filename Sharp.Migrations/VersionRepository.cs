using System;
using System.Collections.Generic;
using System.Linq;
using Sharp.Data;
using Sharp.Data.Filters;
using Sharp.Data.Schema;
using Sharp.Migrations.Runners;

namespace Sharp.Migrations {
    public class VersionRepository : IVersionRepository {

        public const string VERSION_TABLE_NAME = "sharpmigrations";
        public const string OLD_VERSION_TABLE_NAME = "schema_info";
        public const string DEFAULT_MIGRATION_GROUP = "default";

        private IDataClient _dataClient;
        private Filter _migrationGroupFilter;
        private string _migrationGroup;

        public string MigrationTableSchema { get; set; }
        public string MigrationTableName { get; set; }
        public string MigrationGroup {
            get { return GetMigrationGroup(_migrationGroup); }
            set {
                _migrationGroup = value;
                _migrationGroupFilter = Filter.Eq("migrationgroup", MigrationGroup);
            }
        }

        public static string GetMigrationGroup(string migrationGroup) {
            return String.IsNullOrEmpty(migrationGroup) ? DEFAULT_MIGRATION_GROUP : migrationGroup;
        }

        public VersionRepository(IDataClient dataClient) {
            _dataClient = dataClient;
            MigrationGroup = DEFAULT_MIGRATION_GROUP;
            MigrationTableName = VERSION_TABLE_NAME;
        }

        public void EnsureVersionTable(List<MigrationInfo> allMigrationsFromAssembly) {
            CreateVersionTable();
            MigrateOldSchema(allMigrationsFromAssembly);
        }

        private void CreateVersionTable() {
            if (_dataClient.TableExists(VERSION_TABLE_NAME)) {
                return;
            }
            try {
                TryCreateVersionTable();
            }
            catch (Exception ex) {
                throw new MigrationException("Could not create schema version table (" + VERSION_TABLE_NAME + "). See inner exception for details.", ex);
            }
            finally {
                _dataClient.Close();
            }
        }

        private void TryCreateVersionTable() {
            _dataClient.Add
                .Table(VERSION_TABLE_NAME)
                .WithColumns(
                    Column.String("migrationgroup").Size(200).NotNull(),
                    Column.Int64("version").NotNull(),
                    Column.String("info").Size(1000),
                    Column.Date("applied")
                );
            _dataClient.AddPrimaryKey(VERSION_TABLE_NAME, "migrationgroup", "version");
            _dataClient.Commit();
        }

        private void MigrateOldSchema(List<MigrationInfo> allMigrationsFromAssembly) {
            if (!_dataClient.TableExists(OLD_VERSION_TABLE_NAME)) {
                return;
            }
            var res = _dataClient.Select.Columns("name", "version")
                .From(OLD_VERSION_TABLE_NAME)
                .Where(Filter.Eq("name", MigrationGroup))
                .AllRows();
            if (res.Count == 0) {
                DeleteOldSchemaVersionTable();
                return;
            }
            var version = Convert.ToInt32(res[0]["version"]);
            var migrationsToSave = allMigrationsFromAssembly.Where(m => m.Version <= version).OrderBy(m => m.Version);

            try {
                foreach (var migrationInfo in migrationsToSave) {
                    InsertVersion(migrationInfo);
                }    
            }
            catch (Exception ex) {
                RollbackFailedUpgrade();
                throw new UpgradeSharpMigrationException("Could not upgrade migration group {0}. Check Migration {1} (maybe already used version?)", ex);
            }
            DeleteOldSchemaVersionTable();
        }

        private void RollbackFailedUpgrade() {
            _dataClient.Delete.From(VERSION_TABLE_NAME).Where(_migrationGroupFilter);
            _dataClient.Commit();
            if (_dataClient.Count.Table(VERSION_TABLE_NAME).AllRows() == 0) {
                _dataClient.RemoveTable(VERSION_TABLE_NAME);
            }
        }

        private void DeleteOldSchemaVersionTable() {
            _dataClient.Delete.From(OLD_VERSION_TABLE_NAME).Where(Filter.Eq("name", MigrationGroup));
            _dataClient.Commit();
            if (_dataClient.Count.Table(OLD_VERSION_TABLE_NAME).AllRows() == 0) {
                _dataClient.RemoveTable(OLD_VERSION_TABLE_NAME);
            }
        }

        public List<long> GetAppliedMigrations() {
            try {
                return TryGetAppliedMigrations();
            }
            catch (Exception ex) {
                throw new MigrationException("Could not retrieve schema version table (" + VERSION_TABLE_NAME + "). See inner exception for details.", ex);
            }
            finally {
                _dataClient.Close();
            }
        }

        public virtual void InsertVersion(MigrationInfo migrationInfo) {
            EnsureMigrationGroup();
            _dataClient.Insert
                .Into(VERSION_TABLE_NAME)
                .Columns("migrationgroup", "version", "info", "applied")
                .Values(MigrationGroup, migrationInfo.Version, migrationInfo.Name, DateTime.Now);
            _dataClient.Commit();
        }

        private void EnsureMigrationGroup() {
            if (String.IsNullOrEmpty(MigrationGroup)) {
                MigrationGroup = DEFAULT_MIGRATION_GROUP;
            }
        }

        public virtual void RemoveVersion(MigrationInfo migrationInfo) {
            var filterGroup = Filter.Eq("migrationgroup", MigrationGroup);
            var filterVersion = Filter.Eq("version", migrationInfo.Version);
            _dataClient.Delete
                       .From(VERSION_TABLE_NAME)
                       .Where(Filter.And(filterGroup, filterVersion));
            _dataClient.Commit();
        }

        private List<long> TryGetAppliedMigrations() {
            return _dataClient.Select.Columns("version")
                .From(VERSION_TABLE_NAME)
                .Where(_migrationGroupFilter)
                .OrderBy(OrderBy.Ascending("version"))
                .AllRows()
                .Select(x => Convert.ToInt64(x["version"]))
                .ToList();
        }

        public virtual long GetCurrentVersion() {
            try {
                return TryGetCurrentVersion();
            }
            catch (Exception ex) {
                throw new MigrationException("Could not get schema version (table " + VERSION_TABLE_NAME + "). See inner exception for details.", ex);
            }
            finally {
                _dataClient.Close();
            }
        }

        private long TryGetCurrentVersion() {
            if (_dataClient.TableExists(OLD_VERSION_TABLE_NAME) && MigrationGroupExistsOnOldTable()) {
                return Convert.ToInt64(_dataClient.Select.Columns("version")
                        .From(OLD_VERSION_TABLE_NAME)
                        .Where(Filter.Eq("name", MigrationGroup))
                        .AllRows()[0][0]);    
            }
            if (_dataClient.TableExists(VERSION_TABLE_NAME) && MigrationGroupExists()) {
                var sql = "select max(version) from " + VERSION_TABLE_NAME + " where migrationgroup = '" + MigrationGroup + "'";
                return Convert.ToInt64(_dataClient.Database.QueryScalar(sql));
            }
            return 0;
        }

        private bool MigrationGroupExists() {
            return _dataClient.Count.Table(VERSION_TABLE_NAME).Where(_migrationGroupFilter) > 0;
        }

        private bool MigrationGroupExistsOnOldTable() {
            return _dataClient.Count.Table(OLD_VERSION_TABLE_NAME).Where(Filter.Eq("name", MigrationGroup)) > 0;
        }
    }
}