using ProcessGuard.Common.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ProcessGuard.Common.Utility
{
    public static class ConfigCsvHelper
    {
        public static readonly string[] Headers =
        {
            "Id",
            "ProcessName",
            "EXEFullPath",
            "StartupParams",
            "OnlyOpenOnce",
            "Minimize",
            "NoWindow",
            "Started",
            "CronExpression",
            "StopBeforeCronExec"
        };

        public static string Export(IEnumerable<ConfigItem> configItems)
        {
            var builder = new StringBuilder();
            WriteCsvLine(builder, Headers);

            foreach (var item in configItems ?? Enumerable.Empty<ConfigItem>())
            {
                WriteCsvLine(builder, new[]
                {
                    NormalizeGuidOrEmpty(item.Id),
                    item.ProcessName ?? string.Empty,
                    item.EXEFullPath ?? string.Empty,
                    item.StartupParams ?? string.Empty,
                    item.OnlyOpenOnce.ToString().ToLowerInvariant(),
                    item.Minimize.ToString().ToLowerInvariant(),
                    item.NoWindow.ToString().ToLowerInvariant(),
                    item.Started.ToString().ToLowerInvariant(),
                    item.CronExpression ?? string.Empty,
                    item.StopBeforeCronExec.ToString().ToLowerInvariant()
                });
            }

            return builder.ToString();
        }

        public static string ExportRows(IEnumerable<ConfigCsvRow> rows)
        {
            var builder = new StringBuilder();
            WriteCsvLine(builder, Headers);

            foreach (var row in rows ?? Enumerable.Empty<ConfigCsvRow>())
            {
                WriteCsvLine(builder, new[]
                {
                    row.Id ?? string.Empty,
                    row.ProcessName ?? string.Empty,
                    row.EXEFullPath ?? string.Empty,
                    row.StartupParams ?? string.Empty,
                    row.OnlyOpenOnce ?? string.Empty,
                    row.Minimize ?? string.Empty,
                    row.NoWindow ?? string.Empty,
                    row.Started ?? string.Empty,
                    row.CronExpression ?? string.Empty,
                    row.StopBeforeCronExec ?? string.Empty
                });
            }

            return builder.ToString();
        }

        public static CsvParseResult Parse(string csv)
        {
            var result = new CsvParseResult();
            var records = ParseRecords(csv ?? string.Empty, result.Errors);

            if (records.Count == 0)
            {
                result.Errors.Add(new CsvValidationError(1, "CSV header is missing."));
                return result;
            }

            var headerRecord = records[0];
            var headerMap = ValidateHeader(headerRecord.Fields, headerRecord.LineNumber, result.Errors);
            if (result.Errors.Count > 0)
            {
                return result;
            }

            for (var i = 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Fields.Count == 1 && string.IsNullOrEmpty(record.Fields[0]))
                {
                    continue;
                }

                if (record.Fields.Count != headerRecord.Fields.Count)
                {
                    result.Errors.Add(new CsvValidationError(record.LineNumber, "Column count does not match the header."));
                    continue;
                }

                result.Rows.Add(new ConfigCsvRow
                {
                    LineNumber = record.LineNumber,
                    Id = GetField(record, headerMap, "Id"),
                    ProcessName = GetField(record, headerMap, "ProcessName"),
                    EXEFullPath = GetField(record, headerMap, "EXEFullPath"),
                    StartupParams = GetField(record, headerMap, "StartupParams"),
                    OnlyOpenOnce = GetField(record, headerMap, "OnlyOpenOnce"),
                    Minimize = GetField(record, headerMap, "Minimize"),
                    NoWindow = GetField(record, headerMap, "NoWindow"),
                    Started = GetField(record, headerMap, "Started"),
                    CronExpression = GetField(record, headerMap, "CronExpression"),
                    StopBeforeCronExec = GetField(record, headerMap, "StopBeforeCronExec")
                });
            }

            return result;
        }

        public static ObservableCollection<ConfigCsvRow> ToRows(IEnumerable<ConfigItem> configItems)
        {
            var rows = new ObservableCollection<ConfigCsvRow>();
            var lineNumber = 2;

            foreach (var item in configItems ?? Enumerable.Empty<ConfigItem>())
            {
                rows.Add(new ConfigCsvRow
                {
                    LineNumber = lineNumber++,
                    Id = NormalizeGuidOrEmpty(item.Id),
                    ProcessName = item.ProcessName ?? string.Empty,
                    EXEFullPath = item.EXEFullPath ?? string.Empty,
                    StartupParams = item.StartupParams ?? string.Empty,
                    OnlyOpenOnce = item.OnlyOpenOnce.ToString().ToLowerInvariant(),
                    Minimize = item.Minimize.ToString().ToLowerInvariant(),
                    NoWindow = item.NoWindow.ToString().ToLowerInvariant(),
                    Started = item.Started.ToString().ToLowerInvariant(),
                    CronExpression = item.CronExpression ?? string.Empty,
                    StopBeforeCronExec = item.StopBeforeCronExec.ToString().ToLowerInvariant()
                });
            }

            return rows;
        }

        public static CsvApplyResult ApplyChanges(IEnumerable<ConfigItem> currentItems, IEnumerable<ConfigCsvRow> rows)
        {
            var result = new CsvApplyResult();
            var currentList = (currentItems ?? Enumerable.Empty<ConfigItem>()).Select(Clone).ToList();
            var inputRows = (rows ?? Enumerable.Empty<ConfigCsvRow>()).ToList();
            var normalizedRows = new List<NormalizedCsvRow>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in inputRows)
            {
                row.Error = string.Empty;
                var normalized = ValidateRow(row);
                if (normalized.Errors.Count > 0)
                {
                    var message = string.Join("; ", normalized.Errors);
                    row.Error = message;
                    result.Errors.Add(new CsvValidationError(row.LineNumber, message));
                    continue;
                }

                if (!string.IsNullOrEmpty(normalized.Item.Id) && !seenIds.Add(normalized.Item.Id))
                {
                    const string message = "Duplicate Id.";
                    row.Error = message;
                    result.Errors.Add(new CsvValidationError(row.LineNumber, message));
                    continue;
                }

                normalizedRows.Add(normalized);
            }

            var currentById = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in currentList)
            {
                var normalizedId = NormalizeGuidOrEmpty(item.Id);
                if (!string.IsNullOrEmpty(normalizedId))
                {
                    item.Id = normalizedId;
                    currentById[normalizedId] = item;
                }
            }

            if (result.Errors.Count > 0)
            {
                result.ConfigItems = new ObservableCollection<ConfigItem>(currentList);
                return result;
            }

            var updatesById = new Dictionary<string, NormalizedCsvRow>(StringComparer.OrdinalIgnoreCase);
            var newRows = new List<NormalizedCsvRow>();

            foreach (var normalizedRow in normalizedRows)
            {
                if (string.IsNullOrEmpty(normalizedRow.Item.Id))
                {
                    normalizedRow.Item.Id = Guid.NewGuid().ToString("N");
                    newRows.Add(normalizedRow);
                    continue;
                }

                if (currentById.ContainsKey(normalizedRow.Item.Id))
                {
                    updatesById[normalizedRow.Item.Id] = normalizedRow;
                }
                else
                {
                    newRows.Add(normalizedRow);
                }
            }

            var nextItems = new List<ConfigItem>();
            foreach (var current in currentList)
            {
                NormalizedCsvRow update;
                if (updatesById.TryGetValue(current.Id, out update))
                {
                    if (AreEqual(current, update.Item))
                    {
                        result.UnchangedCount++;
                        nextItems.Add(Clone(current));
                    }
                    else
                    {
                        result.UpdatedCount++;
                        var updatedItem = Clone(update.Item);
                        nextItems.Add(updatedItem);
                        if (ShouldNotifyService(current, updatedItem, false))
                        {
                            result.ServiceNotifications.Add(Clone(updatedItem));
                        }
                    }
                }
                else
                {
                    nextItems.Add(Clone(current));
                }
            }

            foreach (var newRow in newRows)
            {
                var newItem = Clone(newRow.Item);
                result.AddedCount++;
                nextItems.Add(newItem);
                if (ShouldNotifyService(null, newItem, true))
                {
                    result.ServiceNotifications.Add(Clone(newItem));
                }
            }

            result.ConfigItems = new ObservableCollection<ConfigItem>(nextItems);
            return result;
        }

        private static bool ShouldNotifyService(ConfigItem oldItem, ConfigItem newItem, bool isNew)
        {
            if (isNew)
            {
                return newItem.Started;
            }

            if (!oldItem.Started && newItem.Started)
            {
                return true;
            }

            if (oldItem.Started && !newItem.Started)
            {
                return true;
            }

            return oldItem.Started && newItem.Started && !AreEqual(oldItem, newItem);
        }

        private static NormalizedCsvRow ValidateRow(ConfigCsvRow row)
        {
            var result = new NormalizedCsvRow();
            var item = new ConfigItem();
            result.Item = item;

            var id = (row.Id ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(id))
            {
                Guid parsedId;
                if (!Guid.TryParse(id, out parsedId))
                {
                    result.Errors.Add("Invalid Id.");
                }
                else
                {
                    item.Id = parsedId.ToString("N");
                }
            }

            item.ProcessName = (row.ProcessName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(item.ProcessName))
            {
                result.Errors.Add("ProcessName is required.");
            }

            item.EXEFullPath = (row.EXEFullPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(item.EXEFullPath))
            {
                result.Errors.Add("EXEFullPath is required.");
            }
            else if (!item.EXEFullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("EXEFullPath must end with .exe.");
            }

            item.StartupParams = row.StartupParams ?? string.Empty;

            bool boolValue;
            if (!TryParseStrictBool(row.OnlyOpenOnce, out boolValue))
            {
                result.Errors.Add("OnlyOpenOnce must be true or false.");
            }
            item.OnlyOpenOnce = boolValue;

            if (!TryParseStrictBool(row.Minimize, out boolValue))
            {
                result.Errors.Add("Minimize must be true or false.");
            }
            item.Minimize = boolValue;

            if (!TryParseStrictBool(row.NoWindow, out boolValue))
            {
                result.Errors.Add("NoWindow must be true or false.");
            }
            item.NoWindow = boolValue;

            if (!TryParseStrictBool(row.Started, out boolValue))
            {
                result.Errors.Add("Started must be true or false.");
            }
            item.Started = boolValue;

            if (!TryParseStrictBool(row.StopBeforeCronExec, out boolValue))
            {
                result.Errors.Add("StopBeforeCronExec must be true or false.");
            }
            item.StopBeforeCronExec = boolValue;

            item.CronExpression = (row.CronExpression ?? string.Empty).Trim();
            if (item.OnlyOpenOnce && !string.IsNullOrEmpty(item.CronExpression))
            {
                result.Errors.Add("CronExpression must be empty when OnlyOpenOnce is true.");
            }
            else if (!string.IsNullOrEmpty(item.CronExpression))
            {
                CronParser parser;
                if (!CronParser.TryParse(item.CronExpression, out parser))
                {
                    result.Errors.Add("CronExpression is invalid.");
                }
            }

            return result;
        }

        private static bool TryParseStrictBool(string input, out bool value)
        {
            value = false;
            var normalized = (input ?? string.Empty).Trim();
            if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool AreEqual(ConfigItem left, ConfigItem right)
        {
            return string.Equals(NormalizeGuidOrEmpty(left.Id), NormalizeGuidOrEmpty(right.Id), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.ProcessName ?? string.Empty, right.ProcessName ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.EXEFullPath ?? string.Empty, right.EXEFullPath ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.StartupParams ?? string.Empty, right.StartupParams ?? string.Empty, StringComparison.Ordinal)
                && left.OnlyOpenOnce == right.OnlyOpenOnce
                && left.Minimize == right.Minimize
                && left.NoWindow == right.NoWindow
                && left.Started == right.Started
                && string.Equals(left.CronExpression ?? string.Empty, right.CronExpression ?? string.Empty, StringComparison.Ordinal)
                && left.StopBeforeCronExec == right.StopBeforeCronExec;
        }

        private static ConfigItem Clone(ConfigItem item)
        {
            return new ConfigItem
            {
                Id = item.Id,
                ProcessName = item.ProcessName,
                EXEFullPath = item.EXEFullPath,
                StartupParams = item.StartupParams,
                OnlyOpenOnce = item.OnlyOpenOnce,
                Minimize = item.Minimize,
                NoWindow = item.NoWindow,
                Started = item.Started,
                CronExpression = item.CronExpression,
                StopBeforeCronExec = item.StopBeforeCronExec
            };
        }

        private static string NormalizeGuidOrEmpty(string id)
        {
            var trimmed = (id ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            Guid parsed;
            return Guid.TryParse(trimmed, out parsed) ? parsed.ToString("N") : trimmed;
        }

        private static Dictionary<string, int> ValidateHeader(IList<string> headerFields, int lineNumber, IList<CsvValidationError> errors)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var expected = new HashSet<string>(Headers, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headerFields.Count; i++)
            {
                var header = (headerFields[i] ?? string.Empty).Trim().TrimStart('\uFEFF');
                if (string.IsNullOrEmpty(header))
                {
                    errors.Add(new CsvValidationError(lineNumber, "Empty header column."));
                    continue;
                }

                if (!expected.Contains(header))
                {
                    errors.Add(new CsvValidationError(lineNumber, "Unknown header column: " + header));
                    continue;
                }

                if (map.ContainsKey(header))
                {
                    errors.Add(new CsvValidationError(lineNumber, "Duplicate header column: " + header));
                    continue;
                }

                map[header] = i;
            }

            foreach (var header in Headers)
            {
                if (!map.ContainsKey(header))
                {
                    errors.Add(new CsvValidationError(lineNumber, "Missing header column: " + header));
                }
            }

            return map;
        }

        private static string GetField(CsvRecord record, IDictionary<string, int> headerMap, string name)
        {
            return record.Fields[headerMap[name]];
        }

        private static List<CsvRecord> ParseRecords(string csv, IList<CsvValidationError> errors)
        {
            var records = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var recordLine = 1;
            var line = 1;
            var i = 0;

            while (i < csv.Length)
            {
                var ch = csv[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        i++;
                        continue;
                    }

                    if (ch == '\n')
                    {
                        line++;
                    }
                    field.Append(ch);
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }

                if (ch == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new CsvRecord { Fields = fields, LineNumber = recordLine });
                    fields = new List<string>();

                    if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }

                    line++;
                    recordLine = line;
                    continue;
                }

                field.Append(ch);
                i++;
            }

            if (inQuotes)
            {
                errors.Add(new CsvValidationError(recordLine, "Quoted field is not closed."));
            }

            if (field.Length > 0 || fields.Count > 0 || csv.Length == 0 || (csv.Length > 0 && csv[csv.Length - 1] != '\n' && csv[csv.Length - 1] != '\r'))
            {
                fields.Add(field.ToString());
                records.Add(new CsvRecord { Fields = fields, LineNumber = recordLine });
            }

            return records;
        }

        private static void WriteCsvLine(StringBuilder builder, IEnumerable<string> fields)
        {
            var first = true;
            foreach (var field in fields)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                builder.Append(Escape(field ?? string.Empty));
                first = false;
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private class CsvRecord
        {
            public List<string> Fields { get; set; }
            public int LineNumber { get; set; }
        }

        private class NormalizedCsvRow
        {
            public ConfigItem Item { get; set; }
            public List<string> Errors { get; private set; }

            public NormalizedCsvRow()
            {
                Errors = new List<string>();
            }
        }
    }

    public class CsvParseResult
    {
        public List<ConfigCsvRow> Rows { get; private set; }
        public List<CsvValidationError> Errors { get; private set; }

        public CsvParseResult()
        {
            Rows = new List<ConfigCsvRow>();
            Errors = new List<CsvValidationError>();
        }
    }

    public class CsvApplyResult
    {
        public ObservableCollection<ConfigItem> ConfigItems { get; set; }
        public List<ConfigItem> ServiceNotifications { get; private set; }
        public List<CsvValidationError> Errors { get; private set; }
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int UnchangedCount { get; set; }

        public CsvApplyResult()
        {
            ConfigItems = new ObservableCollection<ConfigItem>();
            ServiceNotifications = new List<ConfigItem>();
            Errors = new List<CsvValidationError>();
        }
    }

    public class CsvValidationError
    {
        public int LineNumber { get; private set; }
        public string Message { get; private set; }

        public CsvValidationError(int lineNumber, string message)
        {
            LineNumber = lineNumber;
            Message = message;
        }

        public override string ToString()
        {
            return "Line " + LineNumber + ": " + Message;
        }
    }
}
