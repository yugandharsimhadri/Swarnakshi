using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Masters;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>
/// Brings the material taxonomy to the approved 50-category structure and keeps it there.
/// Idempotent — safe on every startup, on a fresh database or on one seeded by the old 19-category tree.
///
/// Preservation contract: Materials are never deleted and never lose their Id or Code. Only
/// <c>Material.MaterialSubcategoryId</c> is repointed, so every InventoryBalance, InventoryTransaction,
/// PurchaseItem and MaterialRequestItem keeps resolving. Retired categories/subcategories are
/// deactivated, never dropped, so historical rows still render.
/// </summary>
public static class MaterialMasterSeeder
{
    /// <summary>Legacy "Category/Subcategory" -> approved "Category/Subcategory".</summary>
    private static readonly Dictionary<string, string> LegacyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- Sand & Aggregates split ---
        ["Sand & Aggregates/River Sand"] = "Sand/River Sand",
        ["Sand & Aggregates/M-Sand"] = "Sand/M-Sand",
        ["Sand & Aggregates/P-Sand"] = "Sand/P-Sand",
        ["Sand & Aggregates/20mm Aggregate"] = "Aggregates & Gravel/20mm Aggregate",
        ["Sand & Aggregates/40mm Aggregate"] = "Aggregates & Gravel/40mm Aggregate",
        ["Sand & Aggregates/Crusher Dust"] = "Aggregates & Gravel/Crusher Dust",

        // --- Steel ---
        ["Steel / Iron/TMT Bars"] = "Iron & Steel/TMT Bars",
        ["Steel / Iron/Binding Wire"] = "Iron & Steel/Binding Wire",
        ["Steel / Iron/MS Angles"] = "Iron & Steel/MS Angle",
        ["Steel / Iron/MS Channels"] = "Iron & Steel/MS Channel",
        ["Steel / Iron/GI Materials"] = "Iron & Steel/GI Materials",

        // --- Bricks & Blocks split ---
        ["Bricks & Blocks/Red Brick"] = "Bricks/Red Brick",
        ["Bricks & Blocks/Fly Ash Brick"] = "Bricks/Fly Ash Brick",
        ["Bricks & Blocks/AAC Block"] = "Blocks/AAC Block",
        ["Bricks & Blocks/Solid Block"] = "Blocks/Solid Block",
        ["Bricks & Blocks/Hollow Block"] = "Blocks/Hollow Block",

        // --- Plumbing split by material family ---
        ["Plumbing Materials/CPVC Pipe"] = "CPVC Plumbing/CPVC Pipe",
        ["Plumbing Materials/UPVC Pipe"] = "UPVC Plumbing/UPVC Pipe",
        ["Plumbing Materials/PVC Pipe"] = "PVC Plumbing/PVC Pipe",
        ["Plumbing Materials/GI Pipe"] = "GI Plumbing/GI Pipe",
        ["Plumbing Materials/Elbow"] = "PVC Plumbing/Fittings",
        ["Plumbing Materials/Tee"] = "PVC Plumbing/Fittings",
        ["Plumbing Materials/Coupler"] = "PVC Plumbing/Fittings",
        ["Plumbing Materials/Valve"] = "Plumbing Valves/Ball Valve",
        ["Plumbing Materials/Tap"] = "Plumbing Fixtures/Taps",

        // --- Electrical split ---
        ["Electrical Materials/Wire"] = "Electrical Wire/Single Core",
        ["Electrical Materials/Cable"] = "Electrical Cable/Power Cable",
        ["Electrical Materials/Conduit"] = "Electrical Conduit/PVC Conduit",
        ["Electrical Materials/Switch"] = "Electrical Switches & Sockets/Switches",
        ["Electrical Materials/Socket"] = "Electrical Switches & Sockets/Sockets",
        ["Electrical Materials/MCB"] = "Electrical Protection/MCB",
        ["Electrical Materials/Distribution Board"] = "Distribution Boards/SPN DB",
        ["Electrical Materials/Junction Box"] = "Electrical Accessories/Junction Box",

        // --- Paint & finishing split ---
        ["Paint & Finishing/Interior Paint"] = "Paint/Interior",
        ["Paint & Finishing/Exterior Paint"] = "Paint/Exterior",
        ["Paint & Finishing/Primer"] = "Primer & Putty/Wall Primer",
        ["Paint & Finishing/Putty"] = "Primer & Putty/Wall Putty",
        ["Paint & Finishing/Waterproof Coating"] = "Waterproofing Materials/Liquid Waterproofing",

        // --- Flooring split into body/form categories ---
        ["Flooring/Vitrified Tiles"] = "Tiles/Vitrified Tile",
        ["Flooring/Ceramic Tiles"] = "Tiles/Ceramic Tile",
        ["Flooring/Granite"] = "Granite/Granite Slab",
        ["Flooring/Marble"] = "Marble/Marble Slab",
        ["Flooring/Adhesive"] = "Tile Accessories/Tile Adhesive",
        ["Flooring/Grout"] = "Tile Accessories/Grout",

        // --- Doors & windows split ---
        ["Doors & Windows/Door Frame"] = "Door Frames/Wooden Frame",
        ["Doors & Windows/Door Shutter"] = "Doors & Shutters/Flush Door",
        ["Doors & Windows/UPVC Window"] = "Windows/UPVC Window",
        ["Doors & Windows/Glass"] = "Glass/Clear Glass",
        ["Doors & Windows/Hardware"] = "Hardware/Hinges",

        // --- straightforward renames ---
        ["Waterproofing/Chemical Coating"] = "Waterproofing Materials/Liquid Waterproofing",
        ["Waterproofing/Membrane"] = "Waterproofing Materials/Membrane",
        ["Waterproofing/Crystalline"] = "Waterproofing Materials/Crystalline Waterproofing",
        ["Construction Chemicals/Tile Adhesive"] = "Tile Accessories/Tile Adhesive",
        ["Roofing/Roofing Sheet"] = "Roofing Materials/Roofing Sheet",
        ["Roofing/Ridge"] = "Roofing Materials/Ridge",
        ["Wood / Carpentry/Teak Wood"] = "Wood & Plywood/Teak",
        ["Wood / Carpentry/Plywood"] = "Wood & Plywood/Plywood",
        ["Wood / Carpentry/MDF"] = "Wood & Plywood/MDF",
        ["Wood / Carpentry/Beading"] = "Wood & Plywood/Wood Sections",
        ["Safety Materials/Helmet"] = "Safety & Site Consumables/Helmet",
        ["Safety Materials/Gloves"] = "Safety & Site Consumables/Gloves",
        ["Safety Materials/Safety Net"] = "Safety & Site Consumables/Safety Net",
        ["Hardware/Tower Bolt"] = "Hardware/Tower Bolts",
        ["Fasteners/Anchor Fasteners"] = "Fasteners/Anchors",
        ["Other Construction Materials/General"] = "Miscellaneous Construction Materials/General",
    };

    /// <summary>Owner-approved corrections to seeded material records.</summary>
    private static readonly Dictionary<string, string> MaterialRenames = new(StringComparer.OrdinalIgnoreCase)
    {
        // The name stated the body metal but not the mechanism; classification is Ball Valve.
        ["MAT-PLB-VAL"] = "Brass Ball Valve",
    };

    public static async Task RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Before anything is created: move the rows that already exist under the old fifty-category
        // shape. Doing this first is what keeps the seeder idempotent — otherwise the upsert below
        // would not find them under their new parent and would make a second copy.
        await FlattenTaxonomyAsync(db, ct);

        var subIndex = await UpsertTaxonomyAsync(db, ct);
        await RemapLegacyMaterialsAsync(db, subIndex, ct);
        await UpsertSpecDefinitionsAsync(db, subIndex, ct);
        await RetireUnknownAsync(db, ct);
        await SeedKnownSpecsAsync(db, ct);
        await RefreshMaterialIdentityAsync(db, ct);
    }

    /// <summary>
    /// Re-parents and renames subcategory rows from the old fifty-category shape into the nine the
    /// app now shows. The row keeps its Id, so every Material, InventoryBalance,
    /// InventoryTransaction, PurchaseItem and MaterialRequestItem pointing at it still resolves —
    /// only its parent and its label change. Categories left with nothing under them are
    /// deactivated, never deleted, so an old row that still names one renders.
    /// </summary>
    private static async Task FlattenTaxonomyAsync(AppDbContext db, CancellationToken ct)
    {
        var subs = await db.MaterialSubcategories.Include(s => s.Category).ToListAsync(ct);
        var categories = await db.MaterialCategories.ToListAsync(ct);

        // Nothing to move on a fresh database, or on one already flattened.
        var toMove = subs
            .Select(s => (Sub: s, Target: MaterialTaxonomy.Flatten.GetValueOrDefault($"{s.Category.Name}/{s.Name}")))
            .Where(x => x.Target is not null)
            .ToList();
        if (toMove.Count == 0) return;

        MaterialCategory CategoryNamed(string name)
        {
            var found = categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (found is not null) return found;
            found = new MaterialCategory { Name = name, IsActive = true };
            db.MaterialCategories.Add(found);
            categories.Add(found);
            return found;
        }

        // The nine have to exist (and have Ids) before anything is pointed at them.
        foreach (var (catName, _) in MaterialTaxonomy.Tree) CategoryNamed(catName);
        await db.SaveChangesAsync(ct);

        foreach (var (sub, target) in toMove)
        {
            var slash = target!.IndexOf('/');
            var newCat = CategoryNamed(target[..slash]);
            var newName = target[(slash + 1)..];

            // Two old subcategories can land on one name — "Fittings" under PVC and under GI. The
            // rename map splits those, so a survivor here means the row is already where it belongs.
            var clash = subs.FirstOrDefault(x => x.Id != sub.Id
                && x.MaterialCategoryId == newCat.Id
                && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
            if (clash is not null) continue;

            sub.MaterialCategoryId = newCat.Id;
            sub.Name = newName;
            sub.IsActive = true;
        }
        await db.SaveChangesAsync(ct);

        // Anything that is not one of the nine and now holds nothing is retired.
        var keep = MaterialTaxonomy.Tree.Select(t => t.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var live = await db.MaterialSubcategories.Select(x => x.MaterialCategoryId).Distinct().ToListAsync(ct);
        foreach (var c in categories.Where(c => !keep.Contains(c.Name) && !live.Contains(c.Id)))
            c.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Creates or reactivates every category/material type. Existing rows keep their Id.</summary>
    private static async Task<Dictionary<string, Guid>> UpsertTaxonomyAsync(AppDbContext db, CancellationToken ct)
    {
        var categories = await db.MaterialCategories.ToListAsync(ct);
        var subs = await db.MaterialSubcategories.ToListAsync(ct);

        var order = 1;
        foreach (var (catName, typeNames) in MaterialTaxonomy.Tree)
        {
            var cat = categories.FirstOrDefault(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase));
            if (cat is null)
            {
                cat = new MaterialCategory { Name = catName };
                db.MaterialCategories.Add(cat);
                categories.Add(cat);
            }
            cat.SortOrder = order++;
            cat.IsActive = true;

            foreach (var subName in typeNames)
            {
                var sub = subs.FirstOrDefault(s => s.MaterialCategoryId == cat.Id
                    && s.Name.Equals(subName, StringComparison.OrdinalIgnoreCase));
                if (sub is null)
                {
                    sub = new MaterialSubcategory { Name = subName, Category = cat, MaterialCategoryId = cat.Id };
                    db.MaterialSubcategories.Add(sub);
                    subs.Add(sub);
                }
                sub.IsActive = true;
            }
        }
        await db.SaveChangesAsync(ct);

        // "Category/Subcategory" -> subcategory id
        return await db.MaterialSubcategories.Include(s => s.Category)
            .ToDictionaryAsync(s => $"{s.Category.Name}/{s.Name}", s => s.Id, StringComparer.OrdinalIgnoreCase, ct);
    }

    /// <summary>Repoints materials sitting under a legacy subcategory. Ids and Codes are untouched.</summary>
    private static async Task RemapLegacyMaterialsAsync(AppDbContext db,
        Dictionary<string, Guid> subIndex, CancellationToken ct)
    {
        var materials = await db.Materials
            .Include(m => m.Subcategory).ThenInclude(s => s.Category)
            .ToListAsync(ct);

        var moved = 0;
        foreach (var m in materials)
        {
            var key = $"{m.Subcategory.Category.Name}/{m.Subcategory.Name}";
            if (!LegacyMap.TryGetValue(key, out var target)) continue;
            if (!subIndex.TryGetValue(target, out var targetId)) continue;
            if (m.MaterialSubcategoryId == targetId) continue;

            m.MaterialSubcategoryId = targetId;
            moved++;
        }

        foreach (var m in materials)
            if (MaterialRenames.TryGetValue(m.Code, out var newName) && m.Name != newName)
                m.Name = newName;

        if (moved > 0 || MaterialRenames.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertSpecDefinitionsAsync(AppDbContext db,
        Dictionary<string, Guid> subIndex, CancellationToken ct)
    {
        var existing = await db.MaterialSpecDefinitions.ToListAsync(ct);

        foreach (var (path, fields) in MaterialTaxonomy.Specs)
        {
            if (!subIndex.TryGetValue(path, out var subId)) continue;

            var order = 1;
            foreach (var f in fields)
            {
                var def = existing.FirstOrDefault(d => d.MaterialSubcategoryId == subId
                    && d.Key.Equals(f.Key, StringComparison.OrdinalIgnoreCase));
                if (def is null)
                {
                    def = new MaterialSpecDefinition { MaterialSubcategoryId = subId, Key = f.Key };
                    db.MaterialSpecDefinitions.Add(def);
                    existing.Add(def);
                }
                def.Label = f.Label;
                def.Kind = f.Kind;
                def.Options = f.Options;
                def.IsRequired = f.Required;
                def.PartOfIdentity = f.Identity;
                def.SortOrder = order++;
                def.IsActive = true;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deactivates anything outside the approved taxonomy. Never deletes — history must still render.</summary>
    private static async Task RetireUnknownAsync(AppDbContext db, CancellationToken ct)
    {
        var approvedCats = MaterialTaxonomy.Tree.Select(t => t.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var approvedPaths = MaterialTaxonomy.Tree
            .SelectMany(t => t.Types.Select(s => $"{t.Category}/{s}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var c in await db.MaterialCategories.Where(c => c.IsActive).ToListAsync(ct))
            if (!approvedCats.Contains(c.Name)) c.IsActive = false;

        foreach (var s in await db.MaterialSubcategories.Include(x => x.Category)
                     .Where(s => s.IsActive).ToListAsync(ct))
            if (!approvedPaths.Contains($"{s.Category.Name}/{s.Name}")) s.IsActive = false;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Specifications for the seeded catalogue, taken from what the material name already states
    /// ("Electrical Wire 2.5sqmm" -> size 2.5 sq.mm). Applied only to materials that carry no
    /// specification values yet, so it never overwrites anything a user has entered.
    /// </summary>
    private static readonly Dictionary<string, (string Key, string Value)[]> KnownSpecs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MAT-CEM-OPC"] = [("grade", "53")],
            ["MAT-AGG-20"] = [("size", "20"), ("size_unit", "mm")],
            ["MAT-AGG-40"] = [("size", "40"), ("size_unit", "mm")],
            ["MAT-STL-TMT"] = [("grade", "Fe500")],
            ["MAT-ELC-WIRE"] = [("size", "2.5"), ("size_unit", "sq.mm")],
            ["MAT-FLR-VIT"] = [("length", "600"), ("width", "600"), ("dimension_unit", "mm")],
            ["MAT-PNT-INT"] = [("type", "Emulsion")],
            ["MAT-PNT-EXT"] = [("type", "Emulsion")],
            ["MAT-DR-SHT"] = [("material_type", "Flush")],
        };

    private static async Task SeedKnownSpecsAsync(AppDbContext db, CancellationToken ct)
    {
        var codes = KnownSpecs.Keys.ToList();
        var materials = await db.Materials
            .Include(m => m.Specifications)
            .Where(m => codes.Contains(m.Code))
            .ToListAsync(ct);

        var added = false;
        foreach (var m in materials)
        {
            if (m.Specifications.Count > 0) continue;   // never clobber user-entered specs
            if (!KnownSpecs.TryGetValue(m.Code, out var specs)) continue;

            foreach (var (key, value) in specs)
            {
                var def = await db.MaterialSpecDefinitions.FirstOrDefaultAsync(
                    d => d.MaterialSubcategoryId == m.MaterialSubcategoryId && d.Key == key, ct);
                if (def is null) continue;
                // Add through the DbSet, not the navigation: BaseEntity pre-populates Id, so a
                // child reached through a tracked parent is classified Modified and EF emits an
                // UPDATE against a row that does not exist yet.
                db.MaterialSpecValues.Add(new MaterialSpecValue
                {
                    MaterialId = m.Id, MaterialSpecDefinitionId = def.Id, Value = value
                });
                added = true;
            }
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Rebuilds SpecSummary/SpecSignature from the attached specification values. The migration and
    /// the material seeder both stamp a placeholder signature (the row Id / Code) purely to satisfy
    /// the NOT NULL + unique index; this replaces it with the real identity key.
    /// </summary>
    public static async Task RefreshMaterialIdentityAsync(AppDbContext db, CancellationToken ct = default)
    {
        var materials = await db.Materials
            .Include(m => m.Specifications).ThenInclude(v => v.Definition)
            .ToListAsync(ct);

        var changed = false;
        foreach (var m in materials)
        {
            var parts = m.Specifications.Select(v => new SpecPart(
                v.Definition.Key, v.Definition.Label, v.Definition.SortOrder,
                v.Definition.PartOfIdentity, v.Value)).ToList();

            var signature = MaterialIdentity.Signature(m.Name, m.Brand, parts);
            var summary = MaterialIdentity.Summary(parts);

            if (m.SpecSignature != signature) { m.SpecSignature = signature; changed = true; }
            if (m.SpecSummary != summary) { m.SpecSummary = summary; changed = true; }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }
}
