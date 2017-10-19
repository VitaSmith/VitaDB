/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

/* DB Schema:

  CREATE TABLE IF NOT EXISTS `Apps` (
	`TITLE_ID`	TEXT NOT NULL,
	`NAME`	TEXT,
	`ALT_NAME`	TEXT,
	`CONTENT_ID`	TEXT NOT NULL UNIQUE,
	`PARENT_ID`	TEXT,
	`CATEGORY`	INTEGER,
	`PKG_ID`	INTEGER UNIQUE,
	`ZRIF`	TEXT,
	`COMMENTS`	TEXT,
	`FLAGS`	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY(`CONTENT_ID`),
	FOREIGN KEY(`CATEGORY`) REFERENCES `Categories`(`VALUE`)
	FOREIGN KEY(`PKG_ID`) REFERENCES `Pkgs`(`ID`),
  );

  CREATE TABLE IF NOT EXISTS `Pkgs` (
	`ID`	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
	`URL`	TEXT UNIQUE,
	`SIZE`	INTEGER,
	`SHA1`	TEXT UNIQUE,
	`CATEGORY`	TEXT,
	`APP_VER`	INTEGER,
	`SYS_VER`	INTEGER,
	`C_DATE`	INTEGER,
	`V_DATE`	INTEGER,
	`COMMENTS`	TEXT
  );

  CREATE TABLE IF NOT EXISTS `Updates` (
	`CONTENT_ID`	TEXT NOT NULL,
	`VERSION`	INTEGER NOT NULL,
	`TYPE`	INTEGER NOT NULL,
	`PKG_ID`	INTEGER NOT NULL UNIQUE,
	PRIMARY KEY(`PKG_ID`),
	FOREIGN KEY(`CONTENT_ID`) REFERENCES `Apps`(`CONTENT_ID`),
	FOREIGN KEY(`TYPE`) REFERENCES `Types`(`VALUE`)
	FOREIGN KEY(`PKG_ID`) REFERENCES `Pkgs`(`ID`),
  );

  CREATE TABLE IF NOT EXISTS `Categories` (
	`NAME`	TEXT NOT NULL UNIQUE,
	`VALUE`	INTEGER NOT NULL UNIQUE,
	PRIMARY KEY(`VALUE`)
  );

  CREATE TABLE IF NOT EXISTS `Flags` (
	`NAME`	TEXT NOT NULL UNIQUE,
	`VALUE`	INTEGER NOT NULL UNIQUE,
	PRIMARY KEY(`VALUE`)
  );

  CREATE TABLE IF NOT EXISTS `Types` (
	`NAME`	TEXT NOT NULL UNIQUE,
	`VALUE`	INTEGER NOT NULL UNIQUE,
	PRIMARY KEY(`VALUE`)
  );

 */

namespace VitaDB
{
    // Optional logging to file
    public class SQLLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string cat_name)
        {
            File.Delete(Assembly.GetEntryAssembly().GetName().Name + ".log");
            return new SQLLogger();
        }

        public void Dispose()
        { }

        private class SQLLogger : ILogger
        {
            public bool IsEnabled(LogLevel log_level)
            {
                return true;
            }

            public void Log<TState>(LogLevel log_level, EventId event_id, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                File.AppendAllText(Assembly.GetEntryAssembly().GetName().Name + ".log", formatter(state, exception));
                //Debug.WriteLine(formatter(state, exception));
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }

    public class Database : DbContext
    {
        public DbSet<App> Apps { get; set; }
        private DbSet<Category> Categories { get; set; }
        private DbSet<Flag> Flags { get; set; }
        public DbSet<Pkg> Pkgs { get; set; }
        public DbSet<Update> Updates { get; set; }
        private DbSet<Type> Types { get; set; }
        // Shorthands for converting Categories and Flags into a value
        public readonly Dictionary<string, int> Category;
        public readonly Dictionary<string, UInt16> Flag;
        public readonly Dictionary<string, int> Type;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={Settings.Instance.database_name}");
            optionsBuilder.EnableSensitiveDataLogging();
        }

        /// <summary>
        /// Create a new database context.
        /// </summary>
        /// <param name="logging">(Optional) Log Entity Framework data.</param>
        public Database(bool initial_setup = false, bool logging = false) : base()
        {
            if (!initial_setup)
            {
                Category = new Dictionary<string, int>();
                foreach (var category in Categories)
                    Category[category.NAME] = category.VALUE;
                Flag = new Dictionary<string, UInt16>();
                foreach (var flag in Flags)
                    Flag[flag.NAME] = (UInt16)flag.VALUE;
                Type = new Dictionary<string, int>();
                foreach (var type in Types)
                    Type[type.NAME] = type.VALUE;
            }
            if (logging)
            {
                var sp = this.GetInfrastructure<IServiceProvider>();
                var lf = sp.GetService<ILoggerFactory>();
                lf.AddProvider(new SQLLoggerProvider());
            }
        }

        /// <summary>
        /// (Re)create the SQLite Database from an SQL dump.
        /// </summary>
        /// <param name="recreate">(Optional) If true, this will recreate the database even if it exists.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool CreateDB(string sql_file, bool recreate = false)
        {
            if (recreate)
            {
                try
                {
                    File.Delete(Settings.Instance.database_name);
                }
                catch (System.IO.IOException e)
                {
                    Console.Error.WriteLine("[ERROR] " + e.Message);
                    return false;
                }
            }
            Console.WriteLine($"Creating database '{Settings.Instance.database_name}' from '{sql_file}'...");
            var lines = File.ReadLines(sql_file);
            using (var db = new Database(true))
            using (var connection = db.Database.GetDbConnection())
            {
                connection.Open();
                {
                    using (var command = connection.CreateCommand())
                    {
                        foreach (var line in lines)
                        {
                            command.CommandText += line;
                            if (line.EndsWith(';'))
                            {
                                command.ExecuteNonQuery();
                                command.CommandText = "";
                            }
                        }
                        // Turn FOREIGN KEYS on, just in case...
                        command.CommandText = "PRAGMA foreign_keys = ON;";
                        command.ExecuteNonQuery();
                    }
                }
                connection.Close();
            }
            return true;
        }
    }
}
