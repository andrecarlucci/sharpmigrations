using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using System.Linq;
using NUnit.Framework;
using Sharp.Data;
using Sharp.Data.Databases;
using Sharp.Migrations;
using System.Reflection;
using Sharp.Migrations.Runners;

namespace Sharp.Tests.Migrations {
	[TestFixture]
	public class RunnerTests {
		private Runner _runner;
		private Mock<IDataClient> _dataClient;
		private Mock<IVersionRepository> _versionRepository;
	    private List<long> _versionsFromDatabase;
            
        [SetUp]
		public void SetUp() {
			MigrationTestHelper.Clear();
            _versionsFromDatabase = new List<long> { 0 };

		    var provider = new Mock<IDataProvider>();
		    provider.Setup(x => x.DatabaseKind).Returns(DatabaseKind.Oracle);

		    var database = new Mock<IDatabase>();
		    database.Setup(x => x.Provider).Returns(provider.Object);

			_dataClient = new Mock<IDataClient>();
		    _dataClient.Setup(p => p.TableExists(VersionRepository.VERSION_TABLE_NAME)).Returns(true);
		    _dataClient.Setup(x => x.Database).Returns(database.Object);

		    _versionRepository = new Mock<IVersionRepository>();
		    _versionRepository.Setup(x => x.GetCurrentVersion()).Returns(() => _versionsFromDatabase.Last());
            _versionRepository.Setup(x => x.GetAppliedMigrations()).Returns(() => _versionsFromDatabase);
            _versionRepository.Setup(x => x.InsertVersion(It.IsAny<MigrationInfo>()))
                .Callback<MigrationInfo>(m => _versionsFromDatabase.Add(m.Version));
            _versionRepository.Setup(x => x.RemoveVersion(It.IsAny<MigrationInfo>()))
                .Callback<MigrationInfo>(m => _versionsFromDatabase.Remove(m.Version));

		    _runner = new Runner(_dataClient.Object, Assembly.GetExecutingAssembly());
		    _runner.VersionRepository = _versionRepository.Object;
		}

		[Test]
		[ExpectedException(typeof(MigrationException))]
		public void Should_throw_migration_exception_when_cant_run_migration() {
			MigrationTestHelper.VersionForException = 1;
			_runner.Run(5);
		}

		[Test]
		public void When_going_from_0_to_5__update_database_version_with_5() {
			_runner.Run(5);
			_versionRepository.Verify(p => p.InsertVersion(It.IsAny<MigrationInfo>()), Times.Exactly(5));
		}

		[Test]
		public void When_going_from_0_to_6_and_exception_on_6__update_database_with_5() {
			MigrationTestHelper.VersionForException = 6;
			try {
				_runner.Run(6);
			}
			catch {}
            _versionRepository.Verify(p => p.InsertVersion(It.IsAny<MigrationInfo>()), Times.Exactly(5));
		}

		[Test]
		public void When_going_from_6_to_0_and_exception_on_4__update_database_with_4() {
			MigrationTestHelper.VersionForException = 4;
			SetVersion(6);		
			try {
				_runner.Run(0);
			}
			catch { }
            _versionRepository.Verify(p => p.RemoveVersion(It.IsAny<MigrationInfo>()), Times.Exactly(2));
		}

		[Test]
		public void When_going_from_5_to_0__update_database_version_with_0() {
			SetVersion(5);		
			_runner.Run(0);
            _versionRepository.Verify(p => p.RemoveVersion(It.IsAny<MigrationInfo>()), Times.Exactly(5));
		}

		[Test]
		public void When_going_from_5_to_1__update_database_version_with_1() {
            SetVersion(5);
			_runner.Run(1);
            _versionRepository.Verify(p => p.RemoveVersion(It.IsAny<MigrationInfo>()), Times.Exactly(4));
		}

		[Test]
		public void When_going_down_from_3_to_1__dont_execute_the_1_down() {
		    SetVersion(3);
			_runner.Run(1);
			Assert.AreEqual(2, MigrationTestHelper.ExecutedMigrationsDown.Count);
		}

		[Test]
		public void When_going_up_from_1_to_3__dont_execute_the_1_up() {
            SetVersion(1);
			_runner.Run(3);

			Assert.AreEqual(2, MigrationTestHelper.ExecutedMigrationsUp.Count);

			List<Type> types = MigrationTestHelper.ExecutedMigrationsUp.Select(p => p.GetType()).ToList();
			
			CollectionAssert.DoesNotContain(types, typeof(Migration1));
		}

		[Test]
		public void When_going_up_from_0_to_3__execute_3_ups() {
			_runner.Run(3);
			Assert.AreEqual(3, MigrationTestHelper.ExecutedMigrationsUp.Count);
			Assert.AreEqual(typeof(Migration3), MigrationTestHelper.ExecutedMigrationsUp.Last().GetType());
		}

		[Test]
		public void When_going_up_from_0_to_6__execute_6_ups() {
			_runner.Run(6);
			Assert.AreEqual(6, MigrationTestHelper.ExecutedMigrationsUp.Count);
			Assert.AreEqual(typeof(Migration6), MigrationTestHelper.ExecutedMigrationsUp.Last().GetType());
		}

        [Test]
        public void Go_up_and_down_twice() {
            _runner.Run(6);
            Assert.AreEqual(6, MigrationTestHelper.ExecutedMigrationsUp.Count);
            Assert.AreEqual(typeof(Migration6), MigrationTestHelper.ExecutedMigrationsUp.Last().GetType());
            MigrationTestHelper.Clear();

            _runner.Run(0);
            Assert.AreEqual(6, MigrationTestHelper.ExecutedMigrationsDown.Count);
            Assert.AreEqual(typeof(Migration1), MigrationTestHelper.ExecutedMigrationsDown.Last().GetType());
            MigrationTestHelper.Clear();

            _runner.Run(3);
            Assert.AreEqual(3, MigrationTestHelper.ExecutedMigrationsUp.Count);
            Assert.AreEqual(typeof(Migration3), MigrationTestHelper.ExecutedMigrationsUp.Last().GetType());
        }

		[Test]
		public void Negative_target_version_means_last_version() {
			_runner.Run(-1);
			long maxVersion = VersionHelper.GetVersion(MigrationTestHelper.GetMigrations().Last());
			Assert.AreEqual(maxVersion, MigrationTestHelper.ExecutedMigrationsUp.Last().Version);
		}

		[Test]
		public void When_exception_should_call_rollback() {
			MigrationTestHelper.VersionForException = 4;
			try {
				_runner.Run(-1);
			}
			catch {}
			_dataClient.Verify(p => p.RollBack());
		}

		[Test]
		public void Should_not_throw_not_supported_by_dialect_when_config_is_set() {
		    Runner.IgnoreDialectNotSupportedActions = true;
			MigrationTestHelper.VersionForNotSupportedByDialectException = 4;
			_runner.Run(5);
		}

		[Test]
		[ExpectedException(typeof(NotSupportedByDialect))]
		public void Should_throw_not_supported_by_dialect_when_config_is_set() {
            Runner.IgnoreDialectNotSupportedActions = false;
			MigrationTestHelper.VersionForNotSupportedByDialectException = 4;
			_runner.Run(5);
		}

	    [Test]
	    public void Should_expose_the_last_version_number() {
            var expectedLastVersion = VersionHelper.GetVersion(MigrationTestHelper.GetMigrations().Last());
	        Assert.AreEqual(expectedLastVersion, _runner.LastVersionNumber);
	    }

	    [Test]
	    public void Should_expose_the_current_version_number() {
            _versionRepository.Setup(v => v.GetCurrentVersion()).Returns(10);
	        Assert.AreEqual(10, _runner.CurrentVersionNumber);
	    }

	    [Test]
	    public void Should_run_old_migrations_going_up() {
            SetVersion(6);
            _versionsFromDatabase.RemoveAt(2);
            _runner.Run(6);
            Assert.AreEqual(1, MigrationTestHelper.ExecutedMigrationsUp.Count);
            Assert.IsTrue(MigrationTestHelper.ExecutedMigrationsUp.Any(x => x.Version == 2));
            MigrationTestHelper.Clear();
	    }

        [Test]
        public void Should_run_old_migrations_going_down() {
            SetVersion(7);
            _versionsFromDatabase.RemoveAt(5);
            _versionRepository.Setup(x => x.EnsureVersionTable(It.IsAny<List<MigrationInfo>>()))
                              .Callback<List<MigrationInfo>>(list => list.RemoveRange(5,1));
            _runner.Run(5);
            Assert.AreEqual(1, MigrationTestHelper.ExecutedMigrationsUp.Count);
            Assert.IsTrue(MigrationTestHelper.ExecutedMigrationsUp.Any(x => x.Version == 5));
            MigrationTestHelper.Clear();
        }

	    private void SetVersion(int max) {
	        for (int i = 1; i <= max; i++) {
                _versionsFromDatabase.Add(i);	            
	        }
	    }
         
		[TearDown]
		public void TearDown() {
            Runner.IgnoreDialectNotSupportedActions = false;
		}
	}
}