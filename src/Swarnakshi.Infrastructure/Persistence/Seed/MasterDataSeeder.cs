using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>Idempotent seed of Indian-construction master data + the initial Owner user. Safe to run every startup.</summary>
public static class MasterDataSeeder
{
    /// <summary>Seeds one company's master data. Requires a tenant scope — every row written is tenant-owned.</summary>
    public static async Task RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        await SeedUnitsAsync(db, ct);
        await SeedMaterialsAsync(db, ct);
        await SeedExpensesAsync(db, ct);
        await SeedSimpleMastersAsync(db, ct);
        await SeedSettingsAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    // (SettingKeys moved to Swarnakshi.Application.Common)

    private static async Task SeedUnitsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Units.AnyAsync(ct)) return;
        string[,] units =
        {
            {"NOS","Nos"},{"BAG","Bag"},{"KG","Kg"},{"TON","Ton"},{"QTL","Quintal"},
            {"MTR","Meter"},{"RMT","Running Meter"},{"SFT","Sq Ft"},{"SQM","Sq M"},
            {"CFT","Cubic Ft"},{"CUM","Cubic Meter"},{"LTR","Litre"},{"BDL","Bundle"},
            {"BOX","Box"},{"PCS","Piece"},{"SET","Set"},{"ROL","Roll"},{"PKT","Packet"},
            {"LOAD","Load"},{"TRIP","Trip"}
        };
        for (var i = 0; i < units.GetLength(0); i++)
            db.Units.Add(new Unit { Code = units[i, 0], Name = units[i, 1] });
    }

    private static async Task SeedMaterialsAsync(AppDbContext db, CancellationToken ct)
    {
        // Categories/subcategories/spec definitions live in MaterialMasterSeeder (the approved
        // 50-category taxonomy). It also remaps any legacy tree, so run it before seeding materials.
        await MaterialMasterSeeder.RunAsync(db, ct);

        if (await db.Materials.AnyAsync(ct)) return;

        var bag = await db.Units.FirstAsync(u => u.Code == "BAG", ct);
        var cft = await db.Units.FirstAsync(u => u.Code == "CFT", ct);
        var kg = await db.Units.FirstAsync(u => u.Code == "KG", ct);
        var nosU = await db.Units.FirstAsync(u => u.Code == "NOS", ct);
        var rmt = await db.Units.FirstAsync(u => u.Code == "RMT", ct);
        var ltr = await db.Units.FirstAsync(u => u.Code == "LTR", ct);
        var sft = await db.Units.FirstAsync(u => u.Code == "SFT", ct);

        Guid Sub(string cat, string sub) =>
            db.MaterialSubcategories.Include(x => x.Category)
              .First(x => x.Category.Name == cat && x.Name == sub).Id;

        // NOTE: codes below are stable identifiers referenced by tests and by any existing
        // transaction history — never renumber them.

        // Codes are stable identifiers referenced by tests and by any existing transaction
        // history — never renumber them. Categories/subcategories use the approved 50-category paths.
        // Specs are seeded where the material name already encodes them.
        var mats = new (string Code, string Name, string Cat, string Sub, Guid Unit, decimal Rate,
            (string Key, string Value)[] Specs)[]
        {
            ("MAT-CEM-OPC","OPC 53 Grade Cement","Cement","OPC",bag.Id,420,[("grade","53")]),
            ("MAT-CEM-PPC","PPC Cement","Cement","PPC",bag.Id,400,[]),
            ("MAT-SND-RIV","River Sand","Sand","River Sand",cft.Id,90,[]),
            ("MAT-SND-MSN","M-Sand","Sand","M-Sand",cft.Id,55,[]),
            ("MAT-AGG-20","20mm Aggregate","Aggregates & Gravel","20mm Aggregate",cft.Id,60,[("size","20"),("size_unit","mm")]),
            ("MAT-AGG-40","40mm Aggregate","Aggregates & Gravel","40mm Aggregate",cft.Id,58,[("size","40"),("size_unit","mm")]),
            ("MAT-STL-TMT","TMT Steel Bar Fe500","Iron & Steel","TMT Bars",kg.Id,68,[("grade","Fe500")]),
            ("MAT-STL-BND","Binding Wire","Iron & Steel","Binding Wire",kg.Id,85,[]),
            ("MAT-BRK-RED","Red Brick","Bricks","Red Brick",nosU.Id,8,[]),
            ("MAT-BRK-FLY","Fly Ash Brick","Bricks","Fly Ash Brick",nosU.Id,7,[]),
            ("MAT-BLK-AAC","AAC Block","Blocks","AAC Block",nosU.Id,48,[]),
            ("MAT-BLK-SLD","Solid Block","Blocks","Solid Block",nosU.Id,32,[]),
            ("MAT-PLB-CPVC","CPVC Pipe","CPVC Plumbing","CPVC Pipe",rmt.Id,120,[]),
            ("MAT-PLB-UPVC","UPVC Pipe","UPVC Plumbing","UPVC Pipe",rmt.Id,95,[]),
            ("MAT-PLB-PVC","PVC Pipe","PVC Plumbing","PVC Pipe",rmt.Id,70,[]),
            ("MAT-PLB-ELB","PVC Elbow","PVC Plumbing","Fittings",nosU.Id,12,[]),
            ("MAT-PLB-TEE","PVC Tee","PVC Plumbing","Fittings",nosU.Id,15,[]),
            ("MAT-PLB-VAL","Brass Ball Valve","Plumbing Valves","Ball Valve",nosU.Id,180,[]),
            ("MAT-ELC-WIRE","Electrical Wire 2.5sqmm","Electrical Wire","Single Core",rmt.Id,18,[("size","2.5"),("size_unit","sq.mm")]),
            ("MAT-ELC-CABLE","Electrical Cable","Electrical Cable","Power Cable",rmt.Id,55,[]),
            ("MAT-ELC-COND","PVC Conduit","Electrical Conduit","PVC Conduit",rmt.Id,22,[]),
            ("MAT-ELC-SW","Modular Switch","Electrical Switches & Sockets","Switches",nosU.Id,90,[]),
            ("MAT-ELC-SOC","Modular Socket","Electrical Switches & Sockets","Sockets",nosU.Id,120,[]),
            ("MAT-ELC-MCB","MCB Single Pole","Electrical Protection","MCB",nosU.Id,220,[]),
            ("MAT-ELC-DB","Distribution Board 8-Way","Distribution Boards","SPN DB",nosU.Id,1600,[]),
            ("MAT-ELC-JB","Junction Box","Electrical Accessories","Junction Box",nosU.Id,35,[]),
            ("MAT-PNT-PRM","Wall Primer","Primer & Putty","Wall Primer",ltr.Id,140,[]),
            ("MAT-PNT-PUT","Wall Putty","Primer & Putty","Wall Putty",kg.Id,28,[]),
            ("MAT-PNT-INT","Interior Emulsion Paint","Paint","Interior",ltr.Id,240,[("type","Emulsion")]),
            ("MAT-PNT-EXT","Exterior Emulsion Paint","Paint","Exterior",ltr.Id,320,[("type","Emulsion")]),
            ("MAT-WPF-COAT","Waterproofing Coating","Waterproofing Materials","Liquid Waterproofing",kg.Id,180,[]),
            ("MAT-FLR-VIT","Vitrified Tiles 600x600","Tiles","Vitrified Tile",sft.Id,55,[("length","600"),("width","600"),("dimension_unit","mm")]),
            ("MAT-FLR-ADH","Tile Adhesive","Tile Accessories","Tile Adhesive",bag.Id,380,[]),
            ("MAT-FLR-GRT","Tile Grout","Tile Accessories","Grout",kg.Id,60,[]),
            ("MAT-DR-FRM","Door Frame (Sal Wood)","Door Frames","Wooden Frame",nosU.Id,3500,[]),
            ("MAT-DR-SHT","Flush Door Shutter","Doors & Shutters","Flush Door",nosU.Id,2800,[("material_type","Flush")]),
            ("MAT-HW-HNG","SS Hinges","Hardware","Hinges",nosU.Id,45,[]),
            ("MAT-FST-NAIL","Nails","Fasteners","Nails",kg.Id,90,[]),
            ("MAT-FST-SCR","Screws","Fasteners","Screws",nosU.Id,2,[]),
            ("MAT-CHM-ADM","Concrete Admixture","Construction Chemicals","Admixture",ltr.Id,95,[]),
        };

        foreach (var m in mats)
        {
            var subId = Sub(m.Cat, m.Sub);
            var material = new Material
            {
                Code = m.Code, Name = m.Name, MaterialSubcategoryId = subId,
                UnitId = m.Unit, DefaultPurchaseRate = m.Rate, MinStockLevel = 0, ReorderLevel = 0,
                // Real signature/summary are rebuilt by MaterialMasterSeeder once specs are attached.
                SpecSignature = m.Code
            };

            foreach (var (key, value) in m.Specs)
            {
                var def = db.MaterialSpecDefinitions.FirstOrDefault(
                    d => d.MaterialSubcategoryId == subId && d.Key == key);
                if (def is not null)
                    material.Specifications.Add(new MaterialSpecValue
                    {
                        MaterialSpecDefinitionId = def.Id, Value = value
                    });
            }

            db.Materials.Add(material);
        }

        await db.SaveChangesAsync(ct);
        await MaterialMasterSeeder.RefreshMaterialIdentityAsync(db, ct);
    }

    private static async Task SeedExpensesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.ExpenseHeads.AnyAsync(ct)) return;

        var common = new[] { "Labour", "Material", "Machinery", "Transportation", "Other" };
        var heads = new (string Name, string[] Subs)[]
        {
            ("Site Preparation", common),
            ("Earthwork", common),
            ("Foundation", new[]{"Labour","Cement","Steel","Sand","Aggregate","Machinery","Transportation","Other"}),
            ("PCC", new[]{"Labour","Cement","Sand","Aggregate","Transportation","Other"}),
            ("RCC", new[]{"Labour","Cement","Steel","Sand","Aggregate","Shuttering","Machinery","Transportation","Other"}),
            ("Slab Work", new[]{"Labour","Cement","Steel","Sand","Aggregate","Shuttering","Scaffolding","Machinery","Transportation","Other"}),
            ("Column Work", new[]{"Labour","Cement","Steel","Sand","Aggregate","Shuttering","Other"}),
            ("Beam Work", new[]{"Labour","Cement","Steel","Sand","Aggregate","Shuttering","Other"}),
            ("Brick Work", new[]{"Labour","Bricks","Cement","Sand","Mortar Materials","Transportation","Other"}),
            ("Block Work", new[]{"Labour","Blocks","Cement","Sand","Transportation","Other"}),
            ("Internal Walls", common),
            ("Plastering", new[]{"Labour","Cement","Sand","Chemicals","Scaffolding","Other"}),
            ("Flooring", new[]{"Labour","Tiles","Adhesive","Grout","Cement","Sand","Other"}),
            ("Tiling", new[]{"Labour","Tiles","Adhesive","Grout","Other"}),
            ("Waterproofing", new[]{"Labour","Membrane","Chemicals","Other"}),
            ("Painting", new[]{"Labour","Primer","Putty","Interior Paint","Exterior Paint","Waterproofing","Thinner","Accessories","Other"}),
            ("Electrical", new[]{"Labour","Wire","Conduit","Switches","Sockets","MCB","Distribution Board","Accessories","Other"}),
            ("Plumbing", new[]{"Labour","Pipes","Fittings","Valves","Taps","Sanitary Materials","Accessories","Other"}),
            ("Sanitary", new[]{"Labour","WC","Wash Basin","CP Fittings","Accessories","Other"}),
            ("Doors", new[]{"Labour","Frame","Shutter","Hardware","Polish","Other"}),
            ("Windows", new[]{"Labour","Frame","Glass","Hardware","Other"}),
            ("Carpentry", common),
            ("Fabrication", new[]{"Labour","MS Material","SS Material","Welding","Paint","Other"}),
            ("Roofing", new[]{"Labour","Sheet","Truss","Fasteners","Other"}),
            ("External Development", common),
            ("Landscaping", common),
            ("Labour", new[]{"Mason","Helper","Skilled","Unskilled","Other"}),
            ("Machinery", new[]{"Excavator","JCB","Crane","Mixer","Vibrator","Other"}),
            ("Transportation", new[]{"Material Transport","Debris Removal","Other"}),
            ("Government / Approval Charges", new[]{"Plan Approval","Water Connection","Electricity Connection","Other"}),
            ("Miscellaneous", new[]{"General","Other"}),
        };

        var order = 1;
        foreach (var (name, subs) in heads)
        {
            var h = new ExpenseHead { Name = name, SortOrder = order++ };
            db.ExpenseHeads.Add(h);
            foreach (var s in subs.Distinct())
                h.Subheads.Add(new ExpenseSubhead { Name = s });
        }
    }

    private static async Task SeedSimpleMastersAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await db.LabourCategories.AnyAsync(ct))
            foreach (var n in new[]
            {
                "Mason","Helper","Carpenter","Shuttering Carpenter","Steel Fixer","Electrician",
                "Plumber","Painter","Tile Worker","Flooring Worker","Welder","Fabricator",
                "Excavator Operator","Machine Operator","General Labour","Supervisor","Other"
            })
                db.LabourCategories.Add(new LabourCategory { Name = n });

        if (!await db.PaymentMethods.AnyAsync(ct))
            foreach (var n in new[] { "Cash", "Bank Transfer", "UPI", "Cheque", "NEFT", "RTGS", "IMPS", "Other" })
                db.PaymentMethods.Add(new PaymentMethod { Name = n });

        if (!await db.ProjectTypes.AnyAsync(ct))
            foreach (var n in new[]
            {
                "Villa","Residential House","Apartment","Commercial Building","Renovation","Interior","Other"
            })
                db.ProjectTypes.Add(new ProjectType { Name = n });
    }

    private static async Task SeedSettingsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Settings.AnyAsync(s => s.SiteId == null, ct)) return;
        db.Settings.AddRange(
            new Setting { Key = SettingKeys.ValuationMethod, Value = nameof(InventoryValuationMethod.WeightedAverage) },
            new Setting { Key = SettingKeys.AllowNegativeStock, Value = "false" },
            // Money leaving the company is the owner's decision, so a purchase waits for them by
            // default. This used to seed "false", which meant a supervisor's purchase posted to
            // stock and to the supplier's ledger with nobody having agreed to it.
            new Setting { Key = SettingKeys.PurchaseNeedsApproval, Value = "true" },
            new Setting { Key = SettingKeys.InventoryAdjustmentNeedsApproval, Value = "true" });
    }

}
