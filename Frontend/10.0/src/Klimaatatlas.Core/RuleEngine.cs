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
/// A NULL input without nullValue behaves as in the original application: a discrete benchmark gives
/// verdict 0 (still counted in Σ weight); a continuous benchmark gives NaN, so the rule result and that
/// component's contribution are written as NULL for the record.
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

        var inputFields = rule.GetFieldNamesForScenario(scenario);
        CheckInputs(rule, scenario, inputFields);

        using var transaction = connection.BeginTransaction();

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
        var nullCounts = new int[rule.Components.Count];

        for (var r = 0; r < rows.Count; r++)
        {
            Report(null, r + 1, rows.Count);
            var row = rows[r];

            double totalWeight = 0, resultSum = 0;
            for (var c = 0; c < rule.Components.Count; c++)
            {
                var component = rule.Components[c];
                // Never null here: CheckInputs guarantees a field or constant for this scenario.
                var value = component.Benchmark.GetInputValue(scenario, row.Get, out var wasNull)!;
                if (wasNull) nullCounts[c]++;
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

        ReportNullValues(rule, scenario, nullCounts);

        log.Trace("All rows processed, committing results.");
        transaction.Commit();
    }

    /// <summary>
    /// Fails early, with a message that names the benchmark, when a benchmark has no input for this
    /// scenario or reads a column that does not exist in the table.
    /// </summary>
    private void CheckInputs(Rule rule, string scenario, List<string> inputFields)
    {
        var withoutInput = rule.Components
            .Select(c => c.Benchmark)
            .Where(b => !b.FieldNamesPerScenario.ContainsKey(scenario) && !b.ConstantsPerScenario.ContainsKey(scenario))
            .Select(b => b.Name)
            .Distinct()
            .ToList();
        if (withoutInput.Count > 0)
            throw new InvalidOperationException(
                $"scenario {scenario}: benchmark(s) {string.Join(", ", withoutInput)} have neither a field nor a constant for this scenario.");

        var columns = GetColumnNames();
        var wanted = inputFields.Select(f => (field: f, benchmarks: rule.Benchmarks.Values
            .Where(b => b.FieldNamesPerScenario.TryGetValue(scenario, out var bf) && string.Equals(bf, f, StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Name)));
        if (rule.Filter.IsActive) wanted = wanted.Append((rule.Filter.FieldName!, ["filter"]));

        var missing = wanted.Where(w => !columns.Contains(w.field)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"scenario {scenario}: column(s) not found in table {tableName}: " +
                string.Join("; ", missing.Select(m => $"{m.field} (used by {string.Join(", ", m.benchmarks)})")) +
                ". Add the column to the geopackage or correct the field name in the configuration.");
    }

    private void ReportNullValues(Rule rule, string scenario, int[] nullCounts)
    {
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var c = 0; c < nullCounts.Length; c++)
        {
            var benchmark = rule.Components[c].Benchmark;
            if (nullCounts[c] == 0 || !reported.Add(benchmark.Name)) continue;

            var field = benchmark.FieldNamesPerScenario[scenario];
            var effect = !double.IsNaN(benchmark.NullValue)
                ? $"replaced by nullValue {benchmark.NullValue}"
                : benchmark.ClassificationType == ClassificationType.Discrete
                    ? "no nullValue, so verdict 0 (counted in the weights)"
                    : $"no nullValue, so the result {rule.ResultColumn(scenario)} is NULL for these records";
            log.AddWarning($"Rule {rule.Name}, scenario {scenario}: benchmark {benchmark.Name} has {nullCounts[c]} empty value(s) in {field}; {effect}.");
        }
    }

    private HashSet<string> GetColumnNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({Quote(tableName)});";
        using var reader = info.ExecuteReader();
        while (reader.Read()) names.Add(reader.GetString(reader.GetOrdinal("name")));
        if (names.Count == 0) throw new InvalidOperationException($"table {tableName} not found in the geopackage.");
        return names;
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
