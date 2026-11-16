using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 11: sets the stored CoFID foods that seed 7 measures in millilitres (milk, juice, tea and other liquids) to <c>Millilitre</c>. It does not wait for the seed reload, which can leave a food a diary entry points at as it was.
/// The ids are frozen here, so the step gives the same result whatever seed the app bundles later; <c>SeedUnitsTests</c> checks the list equals the seed 7 millilitre foods. The step only sets millilitres: a later seed's unit changes come from the seed load.
/// Only <c>Unit</c> changes: a diary entry keeps its amount, and its calories (amount / 100 * the food's per-100 values) are the same in either unit. Safe on a database with no Food table, and when run again.
/// </summary>
internal sealed class FoodUnitUpgrade : ISchemaUpgrade
{
    /// <summary>The CoFID sourceIds of the millilitre foods in seed 7 (alcoholic drinks and liquids), sorted ordinally.</summary>
    internal static readonly IReadOnlyList<string> MillilitreSourceIds =
    [
        "12-038", "12-039", "12-307", "12-308", "12-309", "12-313", "12-314", "12-320",
        "12-321", "12-322", "12-323", "12-326", "12-327", "12-328", "12-329", "12-418",
        "12-419", "12-512", "12-513", "12-523", "12-524", "12-535", "12-536", "12-554",
        "12-563", "12-564", "12-565", "12-566", "12-567", "12-568", "12-569", "12-570",
        "12-589", "12-590", "12-591", "12-596", "12-597", "12-598", "13-382", "13-607",
        "14-275", "14-277", "14-279", "14-285", "14-286", "14-289", "14-329", "14-330",
        "14-331", "14-341", "14-369", "17-153", "17-154", "17-155", "17-156", "17-157",
        "17-159", "17-160", "17-161", "17-163", "17-165", "17-166", "17-167", "17-168",
        "17-169", "17-170", "17-171", "17-172", "17-175", "17-179", "17-201", "17-208",
        "17-212", "17-222", "17-223", "17-224", "17-233", "17-234", "17-236", "17-239",
        "17-244", "17-245", "17-247", "17-377", "17-505", "17-506", "17-594", "17-632",
        "17-672", "17-673", "17-676", "17-735", "17-736", "17-741", "17-742", "17-744",
        "17-745", "17-746", "17-747", "17-748", "17-749", "17-750", "17-751", "17-752",
        "17-754", "17-755", "17-756", "17-757", "17-758", "17-760", "17-761", "17-767",
        "17-768", "17-790", "17-791", "17-792", "17-793", "17-801", "17-802", "17-803",
        "17-807", "17-812", "17-818", "17-819", "17-820", "17-821", "17-822", "17-833",
        "17-844", "17-845", "17-846", "17-847", "17-848",
    ];

    public int Version => 11;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        // One statement: the list is 133 ids, well under SQLite's variable limit (999 on the oldest builds).
        var marks = string.Join(", ", MillilitreSourceIds.Select(_ => "?"));
        var args = new List<object> { (int)FoodUnit.Millilitre, (int)FoodSource.CoFID, (int)FoodUnit.Millilitre };
        args.AddRange(MillilitreSourceIds);
        db.Execute($"UPDATE Food SET Unit = ? WHERE Source = ? AND Unit <> ? AND SourceId IN ({marks})", args.ToArray());
    }
}
