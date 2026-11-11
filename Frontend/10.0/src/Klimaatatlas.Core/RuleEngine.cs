using Microsoft.Data.Sqlite;

namespace Klimaatatlas.Core;

public readonly record struct ProgressReport(string? Message, long Current, long Total);

/// <summary>
/// Executes rules against a features table in a GeoPackage and writes the results back.
/// Port of clsRule.Execute and clsKlimaatatlas.ProcessRules.
///
/// For every scenario and record:
///   result         = Σ(verdict_i · weight_i) / Σ(weight_i)        → column "{scenario}_{rule}"
///   contribution_i = verdict_i · weight_i / Σ(weight_i)          → column "{scenario}_{resultsfield}"
/// Benchmarks without an input value for a record (no field/constant, or NULL without nullValue)
/// are left out of both sums for that record and their contribution is written as NULL.
/// </summary>
public sealed class RuleEngine(SqliteConnection connection, string tableName, DiagnosticsLog log, IProgress<ProgressReport>? progress = null)
{
    private const string FeatureIdColumn = "fid";

    /// <summary>Runs all rules for all scenarios. Returns false when at least one rule failed.</summary>
    public bool ProcessRules(IEnumerable<Rule> rules, IReadOnlyList<string> scenarios)
    {
        Report("Processing rules...", 0, 10);
        var allSucceeded = true;
        foreach (var rule in rules)
        {
            log.Trace($"Processing rule {rule.Name}");
            allSucceeded &= Execute(rule, scenarios);
            log.Trace($"Rule {rule.Name} processed.");
        }
        Report(allSucceeded ? "Rules successfully processed." : "Rules processed with errors.", 0, 10);
        return allSucceeded;
    }

    /// <summary>
    /// Executes one rule. Each scenario runs in its own transaction; on the first error that scenario is
    /// rolled back, the error is logged and the remaining scenarios of this rule are skipped (as before).
    /// </summary>
    public bool Execute(Rule rule, IReadOnlyList<string> scenarios)
    {
        try
        {
            foreach (var scenario in scenarios)
            {
                ExecuteScenario(rule, scenario);
                log.Trace($"Scenario {scenario} processed.");
            }
            return true;
        }
        catch (Exception ex)
        {
            log.AddError($"Error executing rule {rule.Name}: {ex.Message}");
            return false;
        }
    }

    private void ExecuteScenario(Rule rule, string scenario)
    {
        log.Trace($"Processing scenario {scenario}");
        Report($"Processing rule {rule.Name} for scenario {scenario}...", 0, 10);

        using var transaction = connection.BeginTransaction();

        var inputFields = rule.GetFieldNamesForScenario(scenario);
        var resultColumn = rule.ResultColumn(scenario);
        var componentColumns = rule.Components.Select(c => c.ResultColumn(scenario)).ToList();

        EnsureColumnExists(transaction, resultColumn, "REAL");
        foreach (var column in componentColumns) EnsureColumnExists(transaction, column, "REAL");

        var rows = ReadRows(transaction, rule.Filter, inputFields);
        log.Trace($"Number of rows to process for rule {rule.Name}: {rows.Count}");
        log.Trace($"Number of components to process for rule {rule.Name}: {rule.Components.Count}");

        // columns to update: rule result first, then one per component
        var updateColumns = new List<string> { resultColumn };
        updateColumns.AddRange(componentColumns);

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        var parameters = new List<SqliteParameter>();
        for (var i = 0; i < updateColumns.Count; i++)
            parameters.Add(update.Parameters.Add($"@p{i}", SqliteType.Real));
        var fidParameter = update.Parameters.Add("@fid", SqliteType.Integer);
        update.CommandText =
            $"UPDATE {Quote(tableName)} SET {string.Join(", ", updateColumns.Select((c, i) => $"{Quote(c)} = @p{i}"))} " +
            $"WHERE {Quote(FeatureIdColumn)} = @fid;";
        update.Prepare();

        var componentResults = new double[rule.Components.Count];
        var skippedValues = 0;

        for (var r = 0; r < rows.Count; r++)
        {
            Report(null, r + 1, rows.Count);
            var row = rows[r];

            double totalWeight = 0, resultSum = 0;
            for (var c = 0; c < rule.Components.Count; c++)
            {
                var component = rule.Components[c];
                var value = component.Benchmark.GetInputValue(scenario, row.Get);
                if (value is null)
                {
                    componentResults[c] = double.NaN;
                    skippedValues++;
                    continue;
                }
                componentResults[c] = component.Benchmark.GetResult(value) * component.Weight;
                totalWeight += component.Weight;
                resultSum += componentResults[c];
            }

            parameters[0].Value = DbValue(resultSum / totalWeight);
            for (var c = 0; c < componentResults.Length; c++)
                parameters[c + 1].Value = DbValue(componentResults[c] / totalWeight);
            fidParameter.Value = row.Fid;
            update.ExecuteNonQuery();
        }

        if (skippedValues > 0)
            log.AddWarning($"Rule {rule.Name}, scenario {scenario}: {skippedValues} benchmark value(s) were missing (NULL without nullValue, " +
                           "or no field/constant for this scenario) and were left out of the weighted average.");

        log.Trace("All rows processed, committing results.");
        transaction.Commit();
    }

    private List<FeatureRow> ReadRows(SqliteTransaction transaction, Filter filter, List<string> inputFields)
    {
        using var select = connection.CreateCommand();
        select.Transaction = transaction;

        var columns = new List<string> { Quote(FeatureIdColumn) };
        columns.AddRange(inputFields.Select(Quote));
        select.CommandText = $"SELECT {string.Join(", ", columns)} FROM {Quote(tableName)}";

        if (filter.IsActive)
        {
            // Operator comes from a fixed whitelist in Filter.Parse; the operand is a bound parameter.
            select.CommandText += $" WHERE {Quote(filter.FieldName!)} {filter.Operator} @filterValue";
            select.Parameters.AddWithValue("@filterValue", filter.Operand);
            log.Trace($"Executing filtered query {select.CommandText} with value {filter.Operand}");
        }
        else
        {
            log.Trace($"Executing query {select.CommandText}");
        }

        var fieldIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < inputFields.Count; i++) fieldIndex[inputFields[i]] = i + 1;

        var rows = new List<FeatureRow>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            var values = new object?[reader.FieldCount];
            reader.GetValues(values!);
            rows.Add(new FeatureRow(reader.GetInt64(0), values, fieldIndex));
        }
        return rows;
    }

    private void EnsureColumnExists(SqliteTransaction transaction, string columnName, string dataType)
    {
        using var info = connection.CreateCommand();
        info.Transaction = transaction;
        info.CommandText = $"PRAGMA table_info({Quote(tableName)});";
        using (var reader = info.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText = $"ALTER TABLE {Quote(tableName)} ADD COLUMN {Quote(columnName)} {dataType};";
        alter.ExecuteNonQuery();
        log.Trace($"Column {columnName} added to {tableName}.");
    }

    private void Report(string? message, long current, long total) => progress?.Report(new ProgressReport(message, current, total));

    // SQLite stores NaN as NULL anyway; be explicit about it.
    private static object DbValue(double value) => double.IsNaN(value) ? DBNull.Value : value;

    internal static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private sealed class FeatureRow(long fid, object?[] values, Dictionary<string, int> fieldIndex)
    {
        public long Fid { get; } = fid;

        public object? Get(string fieldName) =>
            fieldIndex.TryGetValue(fieldName, out var i)
                ? values[i]
                : throw new InvalidOperationException($"Field '{fieldName}' was not selected.");
    }
}
