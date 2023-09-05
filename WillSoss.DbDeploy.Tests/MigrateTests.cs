﻿using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections;
using WillSoss.DbDeploy.Sql;

namespace WillSoss.DbDeploy.Tests
{
    [Collection(nameof(DatabaseCollection))]
    [Trait("Category", "Migrations")]
    public class MigrateTests : DatabaseTest
    {

        public MigrateTests(DatabaseFixture fixture)
            : base(fixture, "test") { }
        
        [Fact]
        public void ShouldLoadMigrations()
        {
            // Arrange
            var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");

            // Act
            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(migrationsPath)
                .Build();

            // Assert
            db.Migrations.Count().Should().Be(10);
        }

        [Fact]
        public async void ShouldApplyMigrations()
        {
            // Arrange
            var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(migrationsPath)
                .Build();

            await db.Create();

            // Act
            var count = await db.MigrateToLatest();

            // Assert
            count.Should().Be(10);

            var migrations = await db.GetAppliedMigrations(db.GetConnection());

            // 4 scripts + database create
            migrations.Count().Should().Be(11);
        }

        [Fact]
        public async void ShouldApplyMigrationToVersion()
        {
            // Arrange
            var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(migrationsPath)
                .Build();

            await db.Create();

            await db.MigrateTo(new Version(0, 1));

            // Act
            var count = await db.MigrateToLatest();

            // Assert
            count.Should().Be(6);

            var migrations = await db.GetAppliedMigrations(db.GetConnection());

            // 10 scripts + database create
            migrations.Count().Should().Be(11);
        }

        [Fact]
        public async void ShouldNotApplySkippedMigrations()
        {
            // Arrange

            // Apply migrations, but skip "1.1 one-one.sql"
            var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts - One Missing");

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(migrationsPath)
                .Build();

            await db.Create();

            await db.MigrateToLatest();

            // Now set up for migrating again, but with the missing script
            migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");

            db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(migrationsPath)
                .Build();

            // Act
            var ex = await Assert.ThrowsAsync<MigrationsNotAppliedInOrderException>(db.MigrateToLatest);

            // Assert
            ex.Should().NotBeNull();
        }

        [Fact]
        public async void WithMultipleVersionsPreThenPost_ShouldApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(MultipleVersionsPreThenPost)
                .Build();

            await db.Create();

            // Act
            var count = await db.MigrateToLatest();

            // Assert
            count.Should().Be(4);
        }

        [Fact]
        public async void WithPreFlagAndPreThenPost_ShouldApplyPreMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(PreThenPost)
                .Build();

            await db.Create();

            // Act
            var count = await db.MigrateTo(null, MigrationPhase.Pre);

            // Assert
            count.Should().Be(1);
        }

        [Fact]
        public async void WithPreFlagAndOnlyPost_ShouldApplyZeroMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(OnlyPost)
                .Build();

            await db.Create();

            // Act
            var count = await db.MigrateTo(null, MigrationPhase.Pre);

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public async void WithPreFlagAndPreAfterPost_ShouldNotApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(PrePostPre)
                .Build();

            await db.Create();

            // ACT
            var ex = await db.Invoking(db => db.MigrateTo(null, MigrationPhase.Pre))
                .Should().ThrowAsync<UnableToMigrateException>();

            // ASSERT
        }

        [Fact]
        public async void WithPreFlagAndPostThenPre_ShouldNotApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(PostThenPre)
                .Build();

            await db.Create();

            // ACT
            var ex = await db.Invoking(db => db.MigrateTo(null, MigrationPhase.Pre))
                .Should().ThrowAsync<UnableToMigrateException>();

            // ASSERT
        }

        [Fact]
        public async void WithPostFlagAndOnlyPost_ShouldApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(OnlyPost)
                .Build();

            await db.Create();

            // Act
            var count = await db.MigrateTo(null, MigrationPhase.Post);

            // Assert
            count.Should().Be(2);
        }

        [Fact]
        public async void WithPostFlagAndOnlyPre_ShouldNotApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(OnlyPre)
                .Build();

            await db.Create();

            // ACT
            var ex = await db.Invoking(db => db.MigrateTo(null, MigrationPhase.Post))
                .Should().ThrowAsync<UnableToMigrateException>();

            // ASSERT
        }

        [Fact]
        public async void WithPostFlagAndPostThenPre_ShouldNotApplyMigrations()
        {
            // Arrange

            var db = SqlDatabase.CreateBuilder()
                .WithConnectionString(ConnectionString)
                .AddMigrations(PostThenPre)
                .Build();

            await db.Create();

            // ACT
            var ex = await db.Invoking(db => db.MigrateTo(null, MigrationPhase.Post))
                .Should().ThrowAsync<UnableToMigrateException>();

            // ASSERT
        }

        private static IEnumerable<MigrationScript> MultipleVersionsPreThenPost => new[]
        {
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("2.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("3.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("4.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql"))
        };

        private static IEnumerable<MigrationScript> MultipleVersionsMixedPreAndPost => new[]
        {
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("2.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("2.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql"))
        };

        private static IEnumerable<MigrationScript> OnlyPre => new[]
{
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.1"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
        };

        private static IEnumerable<MigrationScript> OnlyPost => new[]
        {
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.1"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
        };

        private static IEnumerable<MigrationScript> PreThenPost => new[]
        {
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
        };

        private static IEnumerable<MigrationScript> PostThenPre => new[]
{
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.1"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
        };

        private static IEnumerable<MigrationScript> PrePostPre => new[]
{
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.0"), MigrationPhase.Post, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql")),
            new MigrationScript(Version.Parse("1.1"), MigrationPhase.Pre, Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "0.1", "_pre", "01 one.sql"))
        };
    }
}
