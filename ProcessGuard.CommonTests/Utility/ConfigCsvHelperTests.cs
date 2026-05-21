using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProcessGuard.Common.Models;
using ProcessGuard.Common.Utility;
using System.Collections.Generic;
using System.Linq;

namespace ProcessGuard.Common.Utility.Tests
{
    [TestClass]
    public class ConfigCsvHelperTests
    {
        [TestMethod]
        public void ExportAndParse_RoundTripsEscapedFieldsAndUtf8Text()
        {
            var items = new[]
            {
                new ConfigItem
                {
                    Id = "e7f38fb64354498c9d5fd3e0351b23ea",
                    ProcessName = "服务,\"一\"",
                    EXEFullPath = @"C:\Tools\App.EXE",
                    StartupParams = "first\r\nsecond",
                    OnlyOpenOnce = false,
                    Minimize = true,
                    NoWindow = false,
                    Started = true,
                    CronExpression = "*/5 * * * *",
                    StopBeforeCronExec = true
                }
            };

            var csv = ConfigCsvHelper.Export(items);
            var parsed = ConfigCsvHelper.Parse(csv);

            Assert.AreEqual(0, parsed.Errors.Count);
            Assert.AreEqual(1, parsed.Rows.Count);
            Assert.AreEqual("服务,\"一\"", parsed.Rows[0].ProcessName);
            Assert.AreEqual("first\r\nsecond", parsed.Rows[0].StartupParams);
            Assert.AreEqual("true", parsed.Rows[0].Minimize);
        }

        [TestMethod]
        public void Parse_ValidatesHeaderShape()
        {
            var missing = ConfigCsvHelper.Parse("Id,ProcessName\r\n");
            Assert.IsTrue(missing.Errors.Any(e => e.Message.Contains("Missing header column")));

            var unknown = ConfigCsvHelper.Parse(HeaderWith("Unknown") + "\r\n");
            Assert.IsTrue(unknown.Errors.Any(e => e.Message.Contains("Unknown header column")));

            var duplicate = ConfigCsvHelper.Parse("Id,Id,ProcessName,EXEFullPath,StartupParams,OnlyOpenOnce,Minimize,NoWindow,Started,CronExpression,StopBeforeCronExec\r\n");
            Assert.IsTrue(duplicate.Errors.Any(e => e.Message.Contains("Duplicate header column")));

            var reordered = ConfigCsvHelper.Parse("ProcessName,Id,EXEFullPath,StartupParams,OnlyOpenOnce,Minimize,NoWindow,Started,CronExpression,StopBeforeCronExec\r\napp,,C:\\app.exe,,false,false,false,false,,true\r\n");
            Assert.AreEqual(0, reordered.Errors.Count);
            Assert.AreEqual("app", reordered.Rows[0].ProcessName);
        }

        [TestMethod]
        public void ApplyChanges_AddsUpdatesKeepsOrderAndDoesNotDeleteMissingRows()
        {
            var current = new List<ConfigItem>
            {
                Item("11111111111111111111111111111111", "first", false),
                Item("22222222222222222222222222222222", "second", false)
            };

            var rows = new[]
            {
                Row("22222222222222222222222222222222", "second-updated", false),
                Row(string.Empty, "new-row", false)
            };

            var result = ConfigCsvHelper.ApplyChanges(current, rows);

            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.AddedCount);
            Assert.AreEqual(1, result.UpdatedCount);
            Assert.AreEqual(0, result.UnchangedCount);
            Assert.AreEqual("first", result.ConfigItems[0].ProcessName);
            Assert.AreEqual("second-updated", result.ConfigItems[1].ProcessName);
            Assert.AreEqual("new-row", result.ConfigItems[2].ProcessName);
            Assert.AreEqual(32, result.ConfigItems[2].Id.Length);
        }

        [TestMethod]
        public void ApplyChanges_TreatsUnknownValidIdAsRestoredNewRow()
        {
            var unknownId = "33333333-3333-3333-3333-333333333333";
            var result = ConfigCsvHelper.ApplyChanges(new ConfigItem[0], new[] { Row(unknownId, "restored", true) });

            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.AddedCount);
            Assert.AreEqual("33333333333333333333333333333333", result.ConfigItems[0].Id);
            Assert.AreEqual(1, result.ServiceNotifications.Count);
        }

        [TestMethod]
        public void ApplyChanges_NormalizesIdsBeforeMatchingAndDuplicateChecks()
        {
            var current = new[] { Item("e7f38fb64354498c9d5fd3e0351b23ea", "old", false) };
            var dashed = "E7F38FB6-4354-498C-9D5F-D3E0351B23EA";

            var result = ConfigCsvHelper.ApplyChanges(current, new[] { Row(dashed, "old", false) });

            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(0, result.AddedCount);
            Assert.AreEqual(0, result.UpdatedCount);
            Assert.AreEqual(1, result.UnchangedCount);
            Assert.AreEqual("e7f38fb64354498c9d5fd3e0351b23ea", result.ConfigItems[0].Id);

            var duplicate = ConfigCsvHelper.ApplyChanges(current, new[]
            {
                Row(dashed, "one", false),
                Row("{e7f38fb6-4354-498c-9d5f-d3e0351b23ea}", "two", false)
            });

            Assert.IsTrue(duplicate.Errors.Any(e => e.Message.Contains("Duplicate Id")));
        }

        [TestMethod]
        public void ApplyChanges_IsAtomicWhenAnyRowHasError()
        {
            var current = new[] { Item("11111111111111111111111111111111", "first", false) };
            var rows = new[]
            {
                Row("11111111111111111111111111111111", "changed", false),
                Row("bad-id", "bad", false)
            };

            var result = ConfigCsvHelper.ApplyChanges(current, rows);

            Assert.AreNotEqual(0, result.Errors.Count);
            Assert.AreEqual("first", result.ConfigItems[0].ProcessName);
            Assert.AreEqual(0, result.ServiceNotifications.Count);
        }

        [TestMethod]
        public void ApplyChanges_ComputesServiceNotifications()
        {
            AssertNotify(Item("11111111111111111111111111111111", "old", false), Row("11111111111111111111111111111111", "old", true), 1);
            AssertNotify(Item("11111111111111111111111111111111", "old", true), Row("11111111111111111111111111111111", "old", false), 1);
            AssertNotify(Item("11111111111111111111111111111111", "old", true), Row("11111111111111111111111111111111", "changed", true), 1);
            AssertNotify(Item("11111111111111111111111111111111", "old", false), Row("11111111111111111111111111111111", "changed", false), 0);
        }

        [TestMethod]
        public void ApplyChanges_ValidatesFieldRules()
        {
            AssertInvalid(Row(string.Empty, string.Empty, false), "ProcessName");
            AssertInvalid(Row(string.Empty, "bad-path", false, exeFullPath: @"C:\bad.txt"), ".exe");
            AssertInvalid(Row(string.Empty, "bad-bool", false, startedText: ""), "Started");
            AssertInvalid(Row(string.Empty, "bad-cron", false, cronExpression: "not a cron"), "CronExpression");
            AssertInvalid(Row(string.Empty, "once-with-cron", false, onlyOpenOnce: "true", cronExpression: "0 1 * * *"), "CronExpression");

            var upperExe = ConfigCsvHelper.ApplyChanges(new ConfigItem[0], new[] { Row(string.Empty, "upper", false, exeFullPath: @"C:\APP.EXE") });
            Assert.AreEqual(0, upperExe.Errors.Count);
        }

        private static void AssertNotify(ConfigItem current, ConfigCsvRow row, int expected)
        {
            var result = ConfigCsvHelper.ApplyChanges(new[] { current }, new[] { row });
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(expected, result.ServiceNotifications.Count);
        }

        private static void AssertInvalid(ConfigCsvRow row, string expectedMessagePart)
        {
            var result = ConfigCsvHelper.ApplyChanges(new ConfigItem[0], new[] { row });
            Assert.IsTrue(result.Errors.Any(e => e.Message.Contains(expectedMessagePart)));
        }

        private static ConfigItem Item(string id, string name, bool started)
        {
            return new ConfigItem
            {
                Id = id,
                ProcessName = name,
                EXEFullPath = @"C:\" + name + ".exe",
                StartupParams = string.Empty,
                OnlyOpenOnce = false,
                Minimize = false,
                NoWindow = false,
                Started = started,
                CronExpression = string.Empty,
                StopBeforeCronExec = true
            };
        }

        private static ConfigCsvRow Row(string id, string name, bool started, string exeFullPath = null, string startedText = null, string onlyOpenOnce = null, string cronExpression = "")
        {
            return new ConfigCsvRow
            {
                LineNumber = 2,
                Id = id,
                ProcessName = name,
                EXEFullPath = exeFullPath ?? (@"C:\" + name + ".exe"),
                StartupParams = string.Empty,
                OnlyOpenOnce = onlyOpenOnce ?? "false",
                Minimize = "false",
                NoWindow = "false",
                Started = startedText ?? started.ToString().ToLowerInvariant(),
                CronExpression = cronExpression,
                StopBeforeCronExec = "true"
            };
        }

        private static string HeaderWith(string replacement)
        {
            return string.Join(",", ConfigCsvHelper.Headers.Take(ConfigCsvHelper.Headers.Length - 1).Concat(new[] { replacement }));
        }
    }
}
