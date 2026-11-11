using Klimaatatlas.Core;
using Microsoft.Data.Sqlite;

namespace Klimaatatlas.Core.Tests;

public sealed class RuleEngineTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("klimaatatlas-tests-").FullName;
    private string GpkgPath => Path.Combine(_dir, "test.gpkg");

    private const string Config = """
        {
          "scenarios": ["K2023", "K2050"],
          "features_dataset": {
            "_comment": "test", "id": "watervlakken", "data_type": "polygons", "storage_type": "geopackage",
            "path": "test.gpkg", "tablename": "watervlakken",
            "fields": []
          },
          "rules": [
            {
              "name": "kroos",
              "benchmarks": [
                { "name": "diepte", "classification": "continuous",
                  "fieldname": [ {"scenario":"K2023","field":"waterdiepte"}, {"scenario":"K2050","field":"waterdiepte"} ],
                  "valuesRange": [ {"value":0,"verdict":1}, {"value":2,"verdict":0} ] },
                { "name": "dood", "classification": "discrete",
                  "fieldname": [ {"scenario":"K2023","field":"doodlopend"}, {"scenario":"K2050","field":"doodlopend"} ],
                  "classes": [ {"name":"ja","value":1}, {"name":"nee","value":0} ] },
                { "name": "temp", "classification": "continuous",
                  "constants": [ {"scenario":"K2023","value":"25"}, {"scenario":"K2050","value":30} ],
                  "valuesRange": [ {"value":15,"verdict":0}, {"value":30,"verdict":1} ] }
              ],
              "components": [
                { "benchmark":"diepte", "weight":2, "resultsfield":"kroos_diepte" },
                { "benchmark":"dood",   "weight":1, "resultsfield":"kroos_dood" },
                { "benchmark":"temp",   "weight":1, "resultsfield":"kroos_temp" }
              ]
            },
            {
              "name": "diep",
              "benchmarks": [
                { "name": "diepte", "classification": "continuous", "filter": "[waterdiepte]>=1.5", "nullValue": 0,
                  "fieldname": [ {"scenario":"K2023","field":"waterdiepte"}, {"scenario":"K2050","field":"waterdiepte", "transformation":"x^2+0x+0"} ],
                  "valuesRange": [ {"value":0,"verdict":0}, {"value":10,"verdict":1} ] }
              ],
              "components": [ { "benchmark":"diepte", "weight":1, "resultsfield":"diep_diepte" } ]
            }
          ]
        }
        """;

    public RuleEngineTests()
    {
        File.WriteAllText(Path.Combine(_dir, "config.json"), Config);
        using var con = new SqliteConnection($"Data Source={GpkgPath};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        // A minimal table plus GDAL-style rtree triggers that call SpatiaLite functions,
        // to prove that adding columns and updating attributes works without SpatiaLite.
        cmd.CommandText = """
            CREATE TABLE watervlakken (fid INTEGER PRIMARY KEY AUTOINCREMENT, geom BLOB, waterdiepte REAL, doodlopend TEXT);
            CREATE TABLE rtree_dummy (id INTEGER PRIMARY KEY);
            INSERT INTO watervlakken (fid, waterdiepte, doodlopend) VALUES (1, 1.0, 'ja'), (2, NULL, 'nee'), (3, 2.0, 'Nee');
            CREATE TRIGGER "rtree_watervlakken_geom_update1" AFTER UPDATE OF geom ON watervlakken
              WHEN OLD.fid = NEW.fid AND (NEW.geom NOTNULL AND NOT ST_IsEmpty(NEW.geom))
              BEGIN INSERT OR REPLACE INTO rtree_dummy VALUES (NEW.fid); END;
            CREATE TRIGGER "rtree_watervlakken_geom_insert" AFTER INSERT ON watervlakken
              WHEN (NEW.geom NOT NULL AND NOT ST_IsEmpty(NEW.geom))
              BEGIN INSERT OR REPLACE INTO rtree_dummy VALUES (NEW.fid); END;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private (bool ok, DiagnosticsLog log) Run()
    {
        var config = AtlasConfiguration.Load(Path.Combine(_dir, "config.json"));
        Assert.Equal(GpkgPath, config.FeaturesDataset.Path); // relative path resolved next to the config
        var log = new DiagnosticsLog();
        using var con = GeoPackage.Open(config.FeaturesDataset.Path, log, spatialiteExtension: null);
        var ok = new RuleEngine(con, config.FeaturesDataset.TableName, log).ProcessRules(config.Rules, config.Scenarios);
        return (ok, log);
    }

    private object?[] Row(long fid, params string[] columns)
    {
        using var con = new SqliteConnection($"Data Source={GpkgPath};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT {string.Join(", ", columns.Select(c => $"\"{c}\""))} FROM watervlakken WHERE fid = {fid}";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        var values = new object?[columns.Length];
        reader.GetValues(values!);
        return values.Select(v => v is DBNull ? null : v).ToArray();
    }

    [Fact]
    public void ComputesWeightedAverageAndContributions()
    {
        var (ok, log) = Run();
        Assert.True(ok, string.Join("; ", log.Errors));

        // fid 1: diepte 1.0 → 0.5 (w2), dood ja → 1 (w1), temp 25 → 2/3 (w1); Σw = 4
        var r1 = Row(1, "K2023_kroos", "K2023_kroos_diepte", "K2023_kroos_dood", "K2023_kroos_temp");
        Assert.Equal((2 * 0.5 + 1 + 2.0 / 3) / 4, (double)r1[0]!, 9);
        Assert.Equal(1.0 / 4, (double)r1[1]!, 9);
        Assert.Equal(1.0 / 4, (double)r1[2]!, 9);
        Assert.Equal(2.0 / 3 / 4, (double)r1[3]!, 9);

        // fid 2: diepte NULL without nullValue → left out; dood nee → 0; temp 2/3; Σw = 2
        var r2 = Row(2, "K2023_kroos", "K2023_kroos_diepte", "K2023_kroos_dood", "K2023_kroos_temp");
        Assert.Equal(2.0 / 3 / 2, (double)r2[0]!, 9);
        Assert.Null(r2[1]);
        Assert.Equal(0.0, (double)r2[2]!, 9);

        // K2050: temp constant 30 → 1
        var r1b = Row(1, "K2050_kroos");
        Assert.Equal((2 * 0.5 + 1 + 1) / 4, (double)r1b[0]!, 9);

        Assert.Contains(log.Warnings, w => w.Contains("kroos") && w.Contains("K2023"));
    }

    [Fact]
    public void AppliesFilterAndTransformation()
    {
        var (ok, log) = Run();
        Assert.True(ok, string.Join("; ", log.Errors));

        // filter [waterdiepte]>=1.5 → only fid 3 (2.0) gets a value
        Assert.Null(Row(1, "K2023_diep")[0]);
        Assert.Null(Row(2, "K2023_diep")[0]);
        Assert.Equal(0.2, (double)Row(3, "K2023_diep")[0]!, 9);

        // K2050 transformation x^2: 2.0 → 4.0 → verdict 0.4
        Assert.Equal(0.4, (double)Row(3, "K2050_diep")[0]!, 9);
    }

    [Fact]
    public void RunningTwiceGivesSameResult()
    {
        Assert.True(Run().ok);
        var first = Row(1, "K2023_kroos")[0];
        Assert.True(Run().ok);
        Assert.Equal(first, Row(1, "K2023_kroos")[0]);
    }

    [Fact]
    public void FailedRuleIsRolledBackAndReported()
    {
        File.WriteAllText(Path.Combine(_dir, "config.json"), Config.Replace("\"field\":\"doodlopend\"}, {\"scenario\":\"K2050\"", "\"field\":\"bestaatniet\"}, {\"scenario\":\"K2050\""));
        var (ok, log) = Run();
        Assert.False(ok);
        Assert.Contains(log.Errors, e => e.Contains("kroos"));
        // the scenario transaction was rolled back, so the result column was not added
        using var con = new SqliteConnection($"Data Source={GpkgPath};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('watervlakken') WHERE name = 'K2023_kroos'";
        Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
        // the other rule still ran
        Assert.Equal(0.2, (double)Row(3, "K2023_diep")[0]!, 9);
    }

    [Fact]
    public void ReadsTheRealRekenregelsFile()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../../rekenregels/20241122_rekenregels.json"));
        if (!File.Exists(path)) return; // repository layout not available
        var config = AtlasConfiguration.Load(path);
        Assert.Equal(["K2023", "K2050", "K2100"], config.Scenarios);
        Assert.NotEmpty(config.Rules);
        Assert.All(config.Rules, r => Assert.NotEmpty(r.Components));
    }
}
