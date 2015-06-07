using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Sharp.Data;
using Sharp.Data.Databases;
using Sharp.Data.Filters;
using Sharp.Data.Schema;
using Sharp.Migrations;
using Sharp.Migrations.Runners;

namespace Sharp.Tests.Databases.Migrations {
    public class VersionRepositoryTests {

        private VersionRepository _versionRepository;
        private IDataClient _client;
        

        [SetUp]
        public void SetUp() {
            _client = DBBuilder.GetDataClient(DataProviderNames.OracleManaged);
            _versionRepository = new VersionRepository(_client);
            if (_client.TableExists(VersionRepository.VERSION_TABLE_NAME)) {
                _client.RemoveTable(VersionRepository.VERSION_TABLE_NAME);
            }
            if (_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME)) {
                _client.RemoveTable(VersionRepository.OLD_VERSION_TABLE_NAME);
            }
             AllMigrations = new[] {M1, M2, M3, M4, M5}.ToList();
        }

        [TearDown]
        public void TearDown() {
            _client.Dispose();
        }

        [Test]
        public void Should_ensure_schema_version_table_existence() {
            _versionRepository.EnsureVersionTable(AllMigrations);
            Assert.IsTrue(_client.TableExists(VersionRepository.VERSION_TABLE_NAME));
        }

        [Test]
        public void Should_create_schema_version_table_on_desired_schema() {
            //VersionRepository.DEFAULT_SCHEMA = "sharp2";
        }

        [Test]
        public void Should_insert_versions_migrating_up() {
            _versionRepository.EnsureVersionTable(AllMigrations);
            _versionRepository.InsertVersion(M1);
            _versionRepository.InsertVersion(M2);
            _versionRepository.InsertVersion(M3);

            List<VersionInfo> all = QueryVersionTable();
            Assert.AreEqual(3, all.Count);
            for (int i = 0; i < 3; i++) {
                Assert.AreEqual((i+1), all[i].Version);
                Assert.AreEqual("Migration" + (i + 1), all[i].Info);
                Assert.AreEqual("default", all[i].MigrationGroup);
            }
        }

        [Test]
        public void Should_delete_versions_migrating_down() {
            _versionRepository.EnsureVersionTable(AllMigrations);
            _versionRepository.InsertVersion(M1);
            _versionRepository.InsertVersion(M2);
            _versionRepository.InsertVersion(M3);

            _versionRepository.RemoveVersion(M3);

            List<VersionInfo> all = QueryVersionTable();
            Assert.AreEqual(2, all.Count);
            for (int i = 0; i < 2; i++) {
                Assert.AreEqual((i + 1), all[i].Version);
                Assert.AreEqual("Migration" + (i+1), all[i].Info);
                Assert.AreEqual("default", all[i].MigrationGroup);
            }
        }

        [Test]
        public void Should_migrate_from_old_version_table_to_new_version_table() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            _versionRepository.EnsureVersionTable(AllMigrations);

            List<VersionInfo> all = QueryVersionTable();
            Assert.AreEqual(3, all.Count);
            for (int i = 0; i < 3; i++) {
                Assert.AreEqual((i + 1), all[i].Version);
                Assert.AreEqual("Migration" + (i + 1), all[i].Info);
                Assert.AreEqual("default", all[i].MigrationGroup);
            }
        }

        [Test]
        public void Old_version_table_is_not_destroyed_after_upgrade_if_there_is_other_group() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            InsertOldVersionTableVersionNumber(3, "othergroup");
            _versionRepository.EnsureVersionTable(AllMigrations);
            Assert.IsTrue(_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME));
        }

        [Test]
        public void Old_version_table_is_destroyed_if_empty() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            _versionRepository.EnsureVersionTable(AllMigrations);
            Assert.IsFalse(_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME));
        }

        [Test]
        public void Rollback_upgrade_on_fail() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            try {
                AllMigrations.Add(M1);
                _versionRepository.EnsureVersionTable(AllMigrations);
                Assert.Fail("Should throw UpgradeSharpMigrationException");
            }
            catch (UpgradeSharpMigrationException ex) {
                
            }
            Assert.IsFalse(_client.TableExists(VersionRepository.VERSION_TABLE_NAME));
        }

        [Test]
        public void Rollback_upgrade_on_fail__with_multiple_migration_groups() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            InsertOldVersionTableVersionNumber(3, "othergroup");
            try {
                _versionRepository.EnsureVersionTable(AllMigrations);
                _versionRepository.MigrationGroup = "othergroup";
                AllMigrations.Add(M1);
                _versionRepository.EnsureVersionTable(AllMigrations);
                Assert.Fail("Should throw UpgradeSharpMigrationException");
            }
            catch (UpgradeSharpMigrationException) {

            }
            Assert.IsTrue(_client.TableExists(VersionRepository.VERSION_TABLE_NAME));
            Assert.AreEqual(3, QueryVersionTable().Count);
            Assert.AreEqual("default", QueryVersionTable()[0].MigrationGroup);
        }

        [Test]
        public void Should_migrate_one_migration_group_at_a_time() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            InsertOldVersionTableVersionNumber(3, "othergroup");
            _versionRepository.EnsureVersionTable(AllMigrations);

            Assert.IsTrue(_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME));
            Assert.AreEqual(1, QueryOldVersionTable().Count);

            _versionRepository.MigrationGroup = "othergroup";
            _versionRepository.EnsureVersionTable(AllMigrations);

            Assert.IsFalse(_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME));
        }

        [Test]
        public void Should_deal_with_multiple_migration_groups__GetCurrentVersion() {
            _versionRepository.EnsureVersionTable(AllMigrations);
            _versionRepository.InsertVersion(M1);
            _versionRepository.InsertVersion(M2);
            _versionRepository.InsertVersion(M3);
            Assert.AreEqual(3, _versionRepository.GetCurrentVersion());
            
            _versionRepository.MigrationGroup = "group2";
            _versionRepository.InsertVersion(M1);
            Assert.AreEqual(1, _versionRepository.GetCurrentVersion());
        }

        [Test]
        public void Should_deal_with_multiple_migration_groups__GetAppliedMigrations() {
            _versionRepository.EnsureVersionTable(AllMigrations);
            _versionRepository.InsertVersion(M1);
            _versionRepository.InsertVersion(M2);
            _versionRepository.InsertVersion(M3);
            Assert.AreEqual(3, _versionRepository.GetAppliedMigrations().Count);

            _versionRepository.MigrationGroup = "group2";
            _versionRepository.InsertVersion(M1);
            Assert.AreEqual(1, _versionRepository.GetAppliedMigrations().Count);
        }

        [Test]
        public void GetCurrentVersion_for_old_schema_table() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            Assert.AreEqual(3, _versionRepository.GetCurrentVersion());
        }

        [Test]
        public void GetCurrentVersion_for_old_and_new_schema_table() {
            CreateOldVersionTable();
            InsertOldVersionTableVersionNumber(3);
            InsertOldVersionTableVersionNumber(4, "othergroup");
            _versionRepository.EnsureVersionTable(AllMigrations);
            Assert.AreEqual(3, _versionRepository.GetCurrentVersion());

            _versionRepository.MigrationGroup = "othergroup";
            Assert.AreEqual(4, _versionRepository.GetCurrentVersion());
        }

        private void CreateOldVersionTable() {
            if (_client.TableExists(VersionRepository.OLD_VERSION_TABLE_NAME)) {
                _client.RemoveTable(VersionRepository.OLD_VERSION_TABLE_NAME);
            }
            _client.Add
                .Table(VersionRepository.OLD_VERSION_TABLE_NAME)
                .WithColumns(
                    Column.String("name").Size(200).AsPrimaryKey(),
                    Column.Int64("version")
                );
        }

        private void InsertOldVersionTableVersionNumber(int number, string group = "default") {
            _client.Insert.Into(VersionRepository.OLD_VERSION_TABLE_NAME)
                          .Columns("name", "version")
                          .Values(group, number);
            _client.Commit();
        }

        private List<VersionInfo> QueryVersionTable() {
            return _client.Select.AllColumns().From(VersionRepository.VERSION_TABLE_NAME).AllRows().Map<VersionInfo>();
        }

        private List<OldVersionInfo> QueryOldVersionTable() {
            return _client.Select
                .AllColumns()
                .From(VersionRepository.OLD_VERSION_TABLE_NAME)
                .AllRows().Map<OldVersionInfo>();
        }

        public static MigrationInfo M1 = new MigrationInfo(typeof(Migration1));
        public static MigrationInfo M2 = new MigrationInfo(typeof(Migration2));
        public static MigrationInfo M3 = new MigrationInfo(typeof(Migration3));
        public static MigrationInfo M4 = new MigrationInfo(typeof(Migration4));
        public static MigrationInfo M5 = new MigrationInfo(typeof(Migration5));
        public static List<MigrationInfo> AllMigrations;
    }

    public class VersionInfo {
        public string MigrationGroup { get; set; }
        public long Version { get; set; }
        public string Info { get; set; }
        public DateTime Applied { get; set; }
    }

    public class OldVersionInfo {
        public string Name { get; set; }
        public long Version { get; set; }
    }

    public class Migration1 : Migration {
        public override void Up() {
        }

        public override void Down() {
        }
    }
    public class Migration2 : Migration1 { }
    public class Migration3 : Migration1 { }
    public class Migration4 : Migration1 { }
    public class Migration5 : Migration1 { }
}
