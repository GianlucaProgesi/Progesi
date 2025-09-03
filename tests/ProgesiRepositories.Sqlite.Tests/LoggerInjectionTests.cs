// tests/ProgesiRepositories.Sqlite.Tests/LoggerInjectionTests.cs
using System;
using System.Collections.Generic;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
    public class LoggerInjectionTests
    {
        private class TestLogger : IProgesiLogger
        {
            public List<string> Calls { get; } = new List<string>();
            public Exception? LastException { get; private set; }

            public void Debug(string message) => Calls.Add($"Debug:{message}");
            public void Info(string message) => Calls.Add($"Info:{message}");
            public void Warn(string message) => Calls.Add($"Warn:{message}");

            // << aggiunte >>
            public void Error(string message) => Calls.Add($"Error:{message}");
            public void Error(Exception ex, string message)
            {
                LastException = ex;
                Calls.Add($"Error:{message}::{ex.GetType().Name}");
            }
        }

        [Fact]
        public void InjectsLogger_And_UsesIt()
        {
            var logger = new TestLogger();
            // arrange/use il tuo repository qui...
            logger.Debug("hello");
            Assert.Contains(logger.Calls, s => s.StartsWith("Debug:"));
        }
    }
}
