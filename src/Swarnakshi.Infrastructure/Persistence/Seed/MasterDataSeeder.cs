using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>Idempotent seed of Indian-construction master data + the initial Owner user. Safe to run every startup.</summary>
public static class MasterDataSeeder
{
    public static async Task RunAsync(AppDbContext db, IPasswordHasher hasher, string ownerEmail, string ownerPassword, CancellationToken ct = default)
    {
        await SeedUnitsAsync(db, ct);
        await SeedMaterialsAsync(db, ct);
        await SeedExpensesAsync(db, ct);
        await SeedSimpleMastersAsync(db, ct);
        await SeedSettingsAsync(db, ct);
        await SeedOwnerAsync(db, hasher, ownerEmail, ownerPassword, ct);
        await db.SaveChangesAsync(ct);
    }

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
        if (await db.MaterialCategories.AnyAsync(ct)) return;

        // category -> subcategories
        var tree = new (string Cat, string[] Subs)[]
        {
            ("Cement", new[]{"OPC","PPC","PSC","White Cement","Masonry Cement"}),
            ("Sand & Aggregates", new[]{"River Sand","M-Sand","P-Sand","20mm Aggregate","40mm Aggregate","Crusher Dust"}),
            ("Steel / Iron", new[]{"TMT Bars","Binding Wire","MS Angles","MS Channels","GI Materials"}),
            ("Bricks & Blocks", new[]{"Red Brick","Fly Ash Brick","AAC Block","Solid Block","Hollow Block"}),
            ("Plumbing Materials", new[]{"CPVC Pipe","UPVC Pipe","PVC Pipe","GI Pipe","Elbow","Tee","Coupler","Valve","Tap","Jointing Material"}),
            ("Electrical Materials", new[]{"Wire","Cable","Switch","Socket","MCB","Distribution Board","Conduit","Junction Box","Accessories"}),
            ("Paint & Finishing", new[]{"Interior Paint","Exterior Paint","Primer","Putty","Waterproof Coating","Thinner"}),
            ("Flooring", new[]{"Vitrified Tiles","Ceramic Tiles","Granite","Marble","Adhesive","Grout"}),
            ("Doors & Windows", new[]{"Door Frame","Door Shutter","Window Frame","UPVC Window","Glass","Hardware"}),
            ("Hardware", new[]{"Hinges","Locks","Handles","Tower Bolt","Miscellaneous"}),
            ("Wood / Carpentry", new[]{"Teak Wood","Plywood","MDF","Laminate","Beading"}),
            ("Roofing", new[]{"Roofing Sheet","Truss Material","Fasteners","Ridge"}),
            ("Waterproofing", new[]{"Membrane","Chemical Coating","Crystalline"}),
            ("Construction Chemicals", new[]{"Admixture","Curing Compound","Tile Adhesive","Epoxy"}),
            ("Sanitaryware", new[]{"WC","Wash Basin","Cistern","CP Fittings"}),
            ("Fasteners", new[]{"Nails","Screws","Bolts","Anchor Fasteners"}),
            ("Safety Materials", new[]{"Helmet","Safety Shoes","Safety Net","Gloves"}),
            ("Tools", new[]{"Hand Tools","Power Tools","Consumables"}),
            ("Other Construction Materials", new[]{"General"}),
        };

        var nos = 1;
        foreach (var (catName, subs) in tree)
        {
            var cat = new MaterialCategory { Name = catName, SortOrder = nos++ };
            db.MaterialCategories.Add(cat);
            foreach (var s in subs)
                cat.Subcategories.Add(new MaterialSubcategory { Name = s });
        }
        await db.SaveChangesAsync(ct);

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

        var mats = new (string Code, string Name, string Cat, string Sub, Guid Unit, decimal Rate)[]
        {
            ("MAT-CEM-OPC","OPC 53 Grade Cement","Cement","OPC",bag.Id,420),
            ("MAT-CEM-PPC","PPC Cement","Cement","PPC",bag.Id,400),
            ("MAT-SND-RIV","River Sand","Sand & Aggregates","River Sand",cft.Id,90),
            ("MAT-SND-MSN","M-Sand","Sand & Aggregates","M-Sand",cft.Id,55),
            ("MAT-AGG-20","20mm Aggregate","Sand & Aggregates","20mm Aggregate",cft.Id,60),
            ("MAT-AGG-40","40mm Aggregate","Sand & Aggregates","40mm Aggregate",cft.Id,58),
            ("MAT-STL-TMT","TMT Steel Bar Fe500","Steel / Iron","TMT Bars",kg.Id,68),
            ("MAT-STL-BND","Binding Wire","Steel / Iron","Binding Wire",kg.Id,85),
            ("MAT-BRK-RED","Red Brick","Bricks & Blocks","Red Brick",nosU.Id,8),
            ("MAT-BRK-FLY","Fly Ash Brick","Bricks & Blocks","Fly Ash Brick",nosU.Id,7),
            ("MAT-BLK-AAC","AAC Block","Bricks & Blocks","AAC Block",nosU.Id,48),
            ("MAT-BLK-SLD","Solid Block","Bricks & Blocks","Solid Block",nosU.Id,32),
            ("MAT-PLB-CPVC","CPVC Pipe","Plumbing Materials","CPVC Pipe",rmt.Id,120),
            ("MAT-PLB-UPVC","UPVC Pipe","Plumbing Materials","UPVC Pipe",rmt.Id,95),
            ("MAT-PLB-PVC","PVC Pipe","Plumbing Materials","PVC Pipe",rmt.Id,70),
            ("MAT-PLB-ELB","PVC Elbow","Plumbing Materials","Elbow",nosU.Id,12),
            ("MAT-PLB-TEE","PVC Tee","Plumbing Materials","Tee",nosU.Id,15),
            ("MAT-PLB-VAL","Brass Valve","Plumbing Materials","Valve",nosU.Id,180),
            ("MAT-ELC-WIRE","Electrical Wire 2.5sqmm","Electrical Materials","Wire",rmt.Id,18),
            ("MAT-ELC-CABLE","Electrical Cable","Electrical Materials","Cable",rmt.Id,55),
            ("MAT-ELC-COND","PVC Conduit","Electrical Materials","Conduit",rmt.Id,22),
            ("MAT-ELC-SW","Modular Switch","Electrical Materials","Switch",nosU.Id,90),
            ("MAT-ELC-SOC","Modular Socket","Electrical Materials","Socket",nosU.Id,120),
            ("MAT-ELC-MCB","MCB Single Pole","Electrical Materials","MCB",nosU.Id,220),
            ("MAT-ELC-DB","Distribution Board 8-Way","Electrical Materials","Distribution Board",nosU.Id,1600),
            ("MAT-ELC-JB","Junction Box","Electrical Materials","Junction Box",nosU.Id,35),
            ("MAT-PNT-PRM","Wall Primer","Paint & Finishing","Primer",ltr.Id,140),
            ("MAT-PNT-PUT","Wall Putty","Paint & Finishing","Putty",kg.Id,28),
            ("MAT-PNT-INT","Interior Emulsion Paint","Paint & Finishing","Interior Paint",ltr.Id,240),
            ("MAT-PNT-EXT","Exterior Emulsion Paint","Paint & Finishing","Exterior Paint",ltr.Id,320),
            ("MAT-WPF-COAT","Waterproofing Coating","Waterproofing","Chemical Coating",kg.Id,180),
            ("MAT-FLR-VIT","Vitrified Tiles 600x600","Flooring","Vitrified Tiles",sft.Id,55),
            ("MAT-FLR-ADH","Tile Adhesive","Flooring","Adhesive",bag.Id,380),
            ("MAT-FLR-GRT","Tile Grout","Flooring","Grout",kg.Id,60),
            ("MAT-DR-FRM","Door Frame (Sal Wood)","Doors & Windows","Door Frame",nosU.Id,3500),
            ("MAT-DR-SHT","Flush Door Shutter","Doors & Windows","Door Shutter",nosU.Id,2800),
            ("MAT-HW-HNG","SS Hinges","Doors & Windows","Hardware",nosU.Id,45),
            ("MAT-FST-NAIL","Nails","Fasteners","Nails",kg.Id,90),
            ("MAT-FST-SCR","Screws","Fasteners","Screws",nosU.Id,2),
            ("MAT-CHM-ADM","Concrete Admixture","Construction Chemicals","Admixture",ltr.Id,95),
        };

        foreach (var m in mats)
            db.Materials.Add(new Material
            {
                Code = m.Code, Name = m.Name, MaterialSubcategoryId = Sub(m.Cat, m.Sub),
                UnitId = m.Unit, DefaultPurchaseRate = m.Rate, MinStockLevel = 0, ReorderLevel = 0
            });
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
            new Setting { Key = SettingKeys.PurchaseNeedsApproval, Value = "false" },
            new Setting { Key = SettingKeys.InventoryAdjustmentNeedsApproval, Value = "true" });
    }

    private static async Task SeedOwnerAsync(AppDbContext db, IPasswordHasher hasher, string email, string password, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return;
        db.Users.Add(new User
        {
            Name = "Owner",
            Email = email,
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Owner,
            IsActive = true
        });
    }
}

public static class SettingKeys
{
    public const string ValuationMethod = "inventory.valuation_method";
    public const string AllowNegativeStock = "inventory.allow_negative_stock";
    public const string PurchaseNeedsApproval = "purchase.needs_approval";
    public const string InventoryAdjustmentNeedsApproval = "inventory.adjustment_needs_approval";
}
