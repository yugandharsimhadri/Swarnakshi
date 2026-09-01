using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>
/// The approved Material Master taxonomy: 50 categories, their subcategories, and the
/// specification fields each subcategory declares.
///
/// Source: Swarnakshi_Material_Master_50_Categories.xlsx (business-design reference), with three
/// owner-approved revisions — Tiles, Granite and Marble are classified by body/form rather than by
/// application, because the same slab or tile SKU is used in several locations.
/// </summary>
public static class MaterialTaxonomy
{
    public record SpecField(string Key, string Label, SpecFieldKind Kind, string? Options = null,
        bool Required = false, bool Identity = true);

    // ---- reusable specification sets ------------------------------------

    private const string LengthUnits = "mm|cm|inch|ft|m";
    private const string SmallUnits = "mm|cm|inch";

    private static SpecField[] Dimensions2D(string thicknessUnits = SmallUnits) =>
    [
        new("length", "Length", SpecFieldKind.Number),
        new("width", "Width", SpecFieldKind.Number),
        new("dimension_unit", "Dimension Unit", SpecFieldKind.Select, LengthUnits),
        new("thickness", "Thickness", SpecFieldKind.Number),
        new("thickness_unit", "Thickness Unit", SpecFieldKind.Select, thicknessUnits),
    ];

    private static SpecField[] Dimensions3D() =>
    [
        new("length", "Length", SpecFieldKind.Number),
        new("width", "Width", SpecFieldKind.Number),
        new("height", "Height", SpecFieldKind.Number),
        new("dimension_unit", "Dimension Unit", SpecFieldKind.Select, LengthUnits),
    ];

    private static SpecField[] StoneSpecs() =>
    [
        .. Dimensions2D(),
        new("finish", "Finish", SpecFieldKind.Select, "Polished|Honed|Flamed|Leather|Lapotra|Rough"),
        new("variety", "Variety / Colour", SpecFieldKind.Text),
    ];

    private static SpecField[] TileSpecs() =>
    [
        .. Dimensions2D(),
        new("finish", "Finish", SpecFieldKind.Select, "Matt|Glossy|Satin|Rustic|Polished|Carving"),
        // Body type is the subcategory; application is an attribute, never part of identity.
        new("application", "Application", SpecFieldKind.Select,
            "Floor|Wall|Dado|Bathroom|Parking|Exterior|Elevation", Identity: false),
    ];

    private static SpecField[] Openings(string typeLabel, string typeOptions) =>
    [
        new("height", "Height", SpecFieldKind.Number),
        new("width", "Width", SpecFieldKind.Number),
        new("dimension_unit", "Dimension Unit", SpecFieldKind.Select, LengthUnits),
        new(typeLabel == "Material / Type" ? "material_type" : "type_movement", typeLabel,
            SpecFieldKind.Select, typeOptions),
    ];

    private static SpecField[] Diameter(string? typeOptions = null, string? typeLabel = null)
    {
        var list = new List<SpecField>
        {
            new("diameter", "Diameter", SpecFieldKind.Number),
            new("diameter_unit", "Diameter Unit", SpecFieldKind.Select, SmallUnits),
        };
        if (typeOptions is not null) list.Add(new("type", typeLabel ?? "Type", SpecFieldKind.Select, typeOptions));
        return [.. list];
    }

    /// <summary>Category -> subcategories, in the approved display order.</summary>
    public static readonly (string Category, string[] Subcategories)[] Tree =
    [
        ("Cement", ["OPC", "PPC", "PSC", "White Cement", "Masonry Cement"]),
        ("Sand", ["River Sand", "M-Sand", "P-Sand", "Robo Sand"]),
        ("Aggregates & Gravel", ["6mm Aggregate", "12mm Aggregate", "20mm Aggregate", "40mm Aggregate", "Gravel", "Crusher Dust"]),
        ("Iron & Steel", ["TMT Bars", "Binding Wire", "MS Rod", "MS Angle", "MS Channel", "GI Materials"]),
        ("Bricks", ["Red Brick", "Fly Ash Brick", "Engineering Brick"]),
        ("Blocks", ["AAC Block", "Solid Block", "Hollow Block", "Concrete Block"]),
        ("Concrete Products", ["Ready Mix Concrete", "Precast Items", "Kerbs", "Pavers"]),
        ("Construction Chemicals", ["Admixture", "Bonding Agent", "Curing Compound", "Epoxy"]),
        ("Waterproofing Materials", ["Cementitious Waterproofing", "Liquid Waterproofing", "Waterproofing Chemical", "Membrane", "Crystalline Waterproofing", "Additives", "Sealants", "Tape", "Accessories", "Repair Materials"]),
        ("Formwork & Shuttering", ["Shuttering Plywood", "Props", "Plates", "Formwork Accessories"]),
        ("CPVC Plumbing", ["CPVC Pipe", "Elbow", "Tee", "Coupler", "Reducer"]),
        ("UPVC Plumbing", ["UPVC Pipe", "Elbow", "Tee", "Coupler", "Reducer"]),
        ("PVC Plumbing", ["PVC Pipe", "Fittings", "Drainage Pipe"]),
        ("GI Plumbing", ["GI Pipe", "Fittings", "Nipples"]),
        ("Plumbing Valves", ["Ball Valve", "Gate Valve", "NRV", "Check Valve"]),
        ("Plumbing Fixtures", ["Taps", "Mixers", "Showers", "Health Faucets"]),
        ("Electrical Wire", ["Single Core", "Multi Core", "Flexible Wire"]),
        ("Electrical Cable", ["Power Cable", "Armoured Cable", "Flexible Cable"]),
        ("Electrical Conduit", ["PVC Conduit", "Flexible Conduit", "Accessories"]),
        ("Electrical Switches & Sockets", ["Switches", "Sockets", "Plates", "Modular Accessories"]),
        ("Electrical Protection", ["MCB", "RCCB", "RCBO", "Fuse", "Isolator"]),
        ("Distribution Boards", ["SPN DB", "TPN DB", "Enclosure", "DB Accessories"]),
        ("Electrical Accessories", ["Junction Box", "Connector", "Lug", "Cable Tie", "Tape"]),
        ("Lighting", ["LED Bulb", "Downlight", "Panel Light", "Outdoor Light"]),
        ("Earthing Materials", ["Earth Rod", "Earth Plate", "Earth Wire", "Compound"]),
        ("Sanitaryware", ["WC", "Wash Basin", "Cistern", "Urinal"]),
        ("Bathroom Accessories", ["Mirror", "Towel Rod", "Soap Dish", "Floor Drain"]),
        ("Bathroom Fittings", ["Floor Trap", "Waste Coupling", "Bottle Trap", "Drain"]),
        ("Water Storage", ["Water Tank", "Tank Accessories"]),
        ("Pumps & Water Equipment", ["Water Pump", "Pressure Pump", "Pump Accessories"]),
        // Revised: form-based, not application-based.
        ("Granite", ["Granite Slab", "Granite Tile", "Step & Riser", "Countertop Slab"]),
        // Revised: body-type-based. Porcelain is a vitrified body, distinguished by spec.
        ("Tiles", ["Vitrified Tile", "Ceramic Tile", "Mosaic Tile", "Cement / Terrazzo Tile", "Clay / Terracotta Tile"]),
        ("Tile Accessories", ["Tile Adhesive", "Grout", "Spacers", "Tile Trim"]),
        ("Marble", ["Marble Slab", "Marble Tile", "Countertop Slab"]),
        ("Natural Stone", ["Kota Stone", "Sandstone", "Slate", "Other Natural Stone"]),
        ("Door Frames", ["Wooden Frame", "Granite Frame", "Stone Frame", "Metal Frame"]),
        ("Doors & Shutters", ["Main Door", "Internal Door", "Flush Door", "PVC Door"]),
        ("Windows", ["UPVC Window", "Aluminium Window", "Sliding Window"]),
        ("Glass", ["Clear Glass", "Toughened Glass", "Frosted Glass", "Laminated Glass"]),
        ("Wood & Plywood", ["Teak", "Plywood", "MDF", "Blockboard", "Wood Sections"]),
        ("Paint", ["Interior", "Exterior", "Emulsion", "Enamel", "Texture"]),
        ("Primer & Putty", ["Wall Primer", "Metal Primer", "Wall Putty"]),
        ("Paint Accessories", ["Roller", "Brush", "Tray", "Scraper", "Sandpaper"]),
        ("Adhesives & Sealants", ["Silicone", "Construction Adhesive", "PVC Adhesive", "Sealant"]),
        ("Hardware", ["Hinges", "Handles", "Locks", "Tower Bolts", "Brackets"]),
        ("Fasteners", ["Screws", "Nails", "Bolts", "Nuts", "Washers", "Anchors"]),
        ("Roofing Materials", ["Roofing Sheet", "Ridge", "Flashing", "Roofing Accessories"]),
        ("Ceiling Materials", ["Gypsum Board", "Ceiling Sections", "Ceiling Accessories"]),
        ("Safety & Site Consumables", ["Helmet", "Gloves", "Safety Net", "Mask", "Site Consumables"]),
        ("Miscellaneous Construction Materials", ["General"]),
    ];

    /// <summary>
    /// Specification fields per subcategory, keyed "Category/Subcategory".
    /// Company/Brand is deliberately absent — it is a first-class Material column, not a spec.
    /// </summary>
    public static IReadOnlyDictionary<string, SpecField[]> Specs { get; } = Build();

    private static Dictionary<string, SpecField[]> Build()
    {
        var d = new Dictionary<string, SpecField[]>(StringComparer.OrdinalIgnoreCase);

        void Set(string category, string[] subs, SpecField[] fields)
        {
            foreach (var s in subs) d[$"{category}/{s}"] = fields;
        }

        void SetAll(string category, SpecField[] fields)
        {
            var subs = Tree.First(t => t.Category == category).Subcategories;
            Set(category, subs, fields);
        }

        SetAll("Cement", [new("grade", "Grade", SpecFieldKind.Text)]);
        SetAll("Sand", [new("type", "Type", SpecFieldKind.Text)]);
        SetAll("Aggregates & Gravel",
        [
            new("size", "Size", SpecFieldKind.Number),
            new("size_unit", "Size Unit", SpecFieldKind.Select, SmallUnits),
            new("type", "Type", SpecFieldKind.Text),
        ]);

        Set("Iron & Steel", ["TMT Bars"],
        [
            .. Diameter(),
            new("grade", "Grade", SpecFieldKind.Select, "Fe415|Fe500|Fe500D|Fe550|Fe600"),
        ]);
        Set("Iron & Steel", ["Binding Wire"],
        [
            new("thickness", "Thickness", SpecFieldKind.Number),
            new("thickness_unit", "Thickness Unit", SpecFieldKind.Select, SmallUnits),
        ]);

        SetAll("Bricks", Dimensions3D());
        SetAll("Blocks", [.. Dimensions3D(), new("type", "Type", SpecFieldKind.Text)]);

        Set("CPVC Plumbing", ["CPVC Pipe"], Diameter("Cold Water|Hot Water"));
        Set("UPVC Plumbing", ["UPVC Pipe"], Diameter());
        Set("PVC Plumbing", ["PVC Pipe", "Drainage Pipe"], Diameter());
        Set("GI Plumbing", ["GI Pipe"], Diameter());

        SetAll("Electrical Wire",
        [
            new("size", "Size", SpecFieldKind.Number, Required: true),
            new("size_unit", "Size Unit", SpecFieldKind.Select, "sq.mm|mm", Required: true),
        ]);
        SetAll("Electrical Cable",
        [
            new("size", "Size", SpecFieldKind.Number),
            new("size_unit", "Size Unit", SpecFieldKind.Select, "sq.mm|mm"),
            new("cores", "Cores", SpecFieldKind.Number),
        ]);

        SetAll("Granite", StoneSpecs());
        SetAll("Marble", StoneSpecs());
        SetAll("Tiles", TileSpecs());

        Set("Waterproofing Materials", ["Waterproofing Chemical"],
        [
            new("type", "Type", SpecFieldKind.Text),
            new("pack_size", "Pack Size", SpecFieldKind.Text),
        ]);
        Set("Waterproofing Materials", ["Membrane"],
        [
            new("type", "Type", SpecFieldKind.Text),
            new("thickness", "Thickness", SpecFieldKind.Number),
            new("thickness_unit", "Thickness Unit", SpecFieldKind.Select, SmallUnits),
        ]);

        SetAll("Doors & Shutters", Openings("Material / Type", "Teak|Sal Wood|Flush|PVC|WPC|Steel|Aluminium"));
        SetAll("Windows", Openings("Type / Movement", "Sliding|Casement|Fixed|Openable|Ventilator|Tilt & Turn"));

        SetAll("Paint",
        [
            new("type", "Type", SpecFieldKind.Text),
            new("finish", "Finish", SpecFieldKind.Select, "Matt|Satin|Sheen|Gloss|Semi Gloss|Texture"),
            new("pack_size", "Pack Size", SpecFieldKind.Text),
        ]);

        return d;
    }
}
