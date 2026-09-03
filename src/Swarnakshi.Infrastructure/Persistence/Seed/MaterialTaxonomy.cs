using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>
/// The material taxonomy: nine categories, their material types, and the specification fields each
/// type declares.
///
/// <para>Nine, not fifty. An earlier draft mirrored a reference spreadsheet with fifty categories —
/// "CPVC Plumbing", "UPVC Plumbing", "Plumbing Valves", "Plumbing Fixtures" each their own heading.
/// Correct as a filing system, unusable as a menu: a storekeeper picking cement had fifty headings
/// to read before reaching the one obvious answer. The categories are now the trades a site is
/// actually organised into, and everything that used to be a category became a material type inside
/// one — which is the level people name things at anyway ("CPVC elbow", "20mm aggregate").</para>
///
/// <para>Type names are self-describing on purpose, because in a search box they appear on their own
/// with no parent to lean on: "CPVC Elbow", not "Elbow"; "OPC Cement", not "OPC".</para>
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
        // Body type is the material type; application is an attribute, never part of identity.
        new("application", "Application", SpecFieldKind.Select,
            "Floor|Wall|Dado|Bathroom|Parking|Exterior|Elevation", Identity: false),
    ];

    private static SpecField[] Openings(string typeLabel, string typeOptions) =>
    [
        new("height", "Height", SpecFieldKind.Number),
        new("width", "Width", SpecFieldKind.Number),
        new("dimension_unit", "Dimension Unit", SpecFieldKind.Select, LengthUnits),
        new("opening_type", typeLabel, SpecFieldKind.Select, typeOptions),
    ];

    private static SpecField[] Diameter(string? extraOptions = null) =>
        extraOptions is null
            ? [new("diameter", "Diameter", SpecFieldKind.Number, Required: true),
               new("diameter_unit", "Diameter Unit", SpecFieldKind.Select, SmallUnits, Required: true)]
            : [new("diameter", "Diameter", SpecFieldKind.Number, Required: true),
               new("diameter_unit", "Diameter Unit", SpecFieldKind.Select, SmallUnits, Required: true),
               new("line_type", "Line Type", SpecFieldKind.Select, extraOptions)];

    // ---- the tree --------------------------------------------------------

    /// <summary>Nine categories; the strings under each are material types.</summary>
    public static readonly (string Category, string[] Types)[] Tree =
    [
        ("Civil & Structure",
        [
            "OPC Cement", "PPC Cement", "PSC Cement", "White Cement", "Masonry Cement",
            "River Sand", "M-Sand", "P-Sand", "Robo Sand",
            "6mm Aggregate", "12mm Aggregate", "20mm Aggregate", "40mm Aggregate", "Gravel", "Crusher Dust",
            "TMT Bars", "Binding Wire", "MS Rod", "MS Angle", "MS Channel", "GI Sheet & Section",
            "Red Brick", "Fly Ash Brick", "Engineering Brick",
            "AAC Block", "Solid Block", "Hollow Block", "Concrete Block",
            "Ready Mix Concrete", "Precast Item", "Kerb", "Paver",
            "Concrete Admixture", "Bonding Agent", "Curing Compound", "Epoxy",
            "Cementitious Waterproofing", "Liquid Waterproofing", "Waterproofing Chemical",
            "Waterproofing Membrane", "Crystalline Waterproofing", "Waterproofing Additive",
            "Waterproofing Sealant", "Waterproofing Tape", "Waterproofing Accessory", "Repair Material",
            "Shuttering Plywood", "Shuttering Prop", "Shuttering Plate", "Formwork Accessory",
        ]),

        ("Plumbing",
        [
            "CPVC Pipe", "CPVC Elbow", "CPVC Tee", "CPVC Coupler", "CPVC Reducer",
            "UPVC Pipe", "UPVC Elbow", "UPVC Tee", "UPVC Coupler", "UPVC Reducer",
            "PVC Pipe", "PVC Fitting", "PVC Drainage Pipe",
            "GI Pipe", "GI Fitting", "GI Nipple",
            "Ball Valve", "Gate Valve", "NRV", "Check Valve",
            "Tap", "Mixer", "Shower", "Health Faucet",
            "Water Tank", "Water Tank Accessory",
            "Water Pump", "Pressure Pump", "Pump Accessory",
            "WC", "Wash Basin", "Cistern", "Urinal",
            "Bathroom Mirror", "Towel Rod", "Soap Dish", "Floor Drain",
            "Floor Trap", "Waste Coupling", "Bottle Trap", "Drain",
        ]),

        ("Electrical",
        [
            "Single Core Wire", "Multi Core Wire", "Flexible Wire",
            "Power Cable", "Armoured Cable", "Flexible Cable",
            "PVC Conduit", "Flexible Conduit", "Conduit Accessory",
            "Switch", "Socket", "Switch Plate", "Modular Accessory",
            "MCB", "RCCB", "RCBO", "Fuse", "Isolator",
            "SPN Distribution Board", "TPN Distribution Board", "DB Enclosure", "DB Accessory",
            "Junction Box", "Connector", "Lug", "Cable Tie", "Insulation Tape",
            "LED Bulb", "Downlight", "Panel Light", "Outdoor Light",
            "Earth Rod", "Earth Plate", "Earth Wire", "Earthing Compound",
        ]),

        ("Flooring & Stone",
        [
            "Vitrified Tile", "Ceramic Tile", "Mosaic Tile", "Cement / Terrazzo Tile", "Clay / Terracotta Tile",
            "Granite Slab", "Granite Tile", "Granite Step & Riser", "Granite Countertop",
            "Marble Slab", "Marble Tile", "Marble Countertop",
            "Kota Stone", "Sandstone", "Slate", "Other Natural Stone",
            "Tile Adhesive", "Tile Grout", "Tile Spacer", "Tile Trim",
        ]),

        ("Doors & Windows",
        [
            "Wooden Door Frame", "Granite Door Frame", "Stone Door Frame", "Metal Door Frame",
            "Main Door", "Internal Door", "Flush Door", "PVC Door",
            "UPVC Window", "Aluminium Window", "Sliding Window",
            "Clear Glass", "Toughened Glass", "Frosted Glass", "Laminated Glass",
            "Teak Wood", "Plywood", "MDF", "Blockboard", "Wood Section",
        ]),

        ("Painting",
        [
            "Interior Paint", "Exterior Paint", "Emulsion Paint", "Enamel Paint", "Texture Paint",
            "Wall Primer", "Metal Primer", "Wall Putty",
            "Paint Roller", "Paint Brush", "Paint Tray", "Scraper", "Sandpaper",
        ]),

        ("Hardware & Fasteners",
        [
            "Hinge", "Handle", "Lock", "Tower Bolt", "Bracket",
            "Screw", "Nail", "Bolt", "Nut", "Washer", "Anchor",
            "Silicone", "Construction Adhesive", "PVC Adhesive", "Sealant",
        ]),

        ("Roofing & Ceiling",
        [
            "Roofing Sheet", "Ridge", "Flashing", "Roofing Accessory",
            "Gypsum Board", "Ceiling Section", "Ceiling Accessory",
        ]),

        ("Site & Safety",
        [
            "Helmet", "Gloves", "Safety Net", "Mask", "Site Consumable", "General Material",
        ]),
    ];

    /// <summary>
    /// Where each type used to live, as "OldCategory/OldSubcategory" → "NewCategory/NewType".
    ///
    /// Subcategory rows are re-parented and renamed in place rather than recreated, so every
    /// Material, InventoryBalance, InventoryTransaction, PurchaseItem and MaterialRequestItem keeps
    /// resolving — nothing points at a category, everything points at the row this map moves.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Flatten = BuildFlatten();

    private static Dictionary<string, string> BuildFlatten()
    {
        // The fifty-category shape this replaced. Kept only to drive the move.
        (string Category, string[] Subs, string NewCategory, string? Prefix)[] old =
        [
            ("Cement", ["OPC", "PPC", "PSC", "White Cement", "Masonry Cement"], "Civil & Structure", null),
            ("Sand", ["River Sand", "M-Sand", "P-Sand", "Robo Sand"], "Civil & Structure", null),
            ("Aggregates & Gravel", ["6mm Aggregate", "12mm Aggregate", "20mm Aggregate", "40mm Aggregate", "Gravel", "Crusher Dust"], "Civil & Structure", null),
            ("Iron & Steel", ["TMT Bars", "Binding Wire", "MS Rod", "MS Angle", "MS Channel", "GI Materials"], "Civil & Structure", null),
            ("Bricks", ["Red Brick", "Fly Ash Brick", "Engineering Brick"], "Civil & Structure", null),
            ("Blocks", ["AAC Block", "Solid Block", "Hollow Block", "Concrete Block"], "Civil & Structure", null),
            ("Concrete Products", ["Ready Mix Concrete", "Precast Items", "Kerbs", "Pavers"], "Civil & Structure", null),
            ("Construction Chemicals", ["Admixture", "Bonding Agent", "Curing Compound", "Epoxy"], "Civil & Structure", null),
            ("Waterproofing Materials", ["Cementitious Waterproofing", "Liquid Waterproofing", "Waterproofing Chemical", "Membrane", "Crystalline Waterproofing", "Additives", "Sealants", "Tape", "Accessories", "Repair Materials"], "Civil & Structure", null),
            ("Formwork & Shuttering", ["Shuttering Plywood", "Props", "Plates", "Formwork Accessories"], "Civil & Structure", null),

            ("CPVC Plumbing", ["CPVC Pipe", "Elbow", "Tee", "Coupler", "Reducer"], "Plumbing", "CPVC "),
            ("UPVC Plumbing", ["UPVC Pipe", "Elbow", "Tee", "Coupler", "Reducer"], "Plumbing", "UPVC "),
            ("PVC Plumbing", ["PVC Pipe", "Fittings", "Drainage Pipe"], "Plumbing", null),
            ("GI Plumbing", ["GI Pipe", "Fittings", "Nipples"], "Plumbing", null),
            ("Plumbing Valves", ["Ball Valve", "Gate Valve", "NRV", "Check Valve"], "Plumbing", null),
            ("Plumbing Fixtures", ["Taps", "Mixers", "Showers", "Health Faucets"], "Plumbing", null),
            ("Water Storage", ["Water Tank", "Tank Accessories"], "Plumbing", null),
            ("Pumps & Water Equipment", ["Water Pump", "Pressure Pump", "Pump Accessories"], "Plumbing", null),
            ("Sanitaryware", ["WC", "Wash Basin", "Cistern", "Urinal"], "Plumbing", null),
            ("Bathroom Accessories", ["Mirror", "Towel Rod", "Soap Dish", "Floor Drain"], "Plumbing", null),
            ("Bathroom Fittings", ["Floor Trap", "Waste Coupling", "Bottle Trap", "Drain"], "Plumbing", null),

            ("Electrical Wire", ["Single Core", "Multi Core", "Flexible Wire"], "Electrical", null),
            ("Electrical Cable", ["Power Cable", "Armoured Cable", "Flexible Cable"], "Electrical", null),
            ("Electrical Conduit", ["PVC Conduit", "Flexible Conduit", "Accessories"], "Electrical", null),
            ("Electrical Switches & Sockets", ["Switches", "Sockets", "Plates", "Modular Accessories"], "Electrical", null),
            ("Electrical Protection", ["MCB", "RCCB", "RCBO", "Fuse", "Isolator"], "Electrical", null),
            ("Distribution Boards", ["SPN DB", "TPN DB", "Enclosure", "DB Accessories"], "Electrical", null),
            ("Electrical Accessories", ["Junction Box", "Connector", "Lug", "Cable Tie", "Tape"], "Electrical", null),
            ("Lighting", ["LED Bulb", "Downlight", "Panel Light", "Outdoor Light"], "Electrical", null),
            ("Earthing Materials", ["Earth Rod", "Earth Plate", "Earth Wire", "Compound"], "Electrical", null),

            ("Tiles", ["Vitrified Tile", "Ceramic Tile", "Mosaic Tile", "Cement / Terrazzo Tile", "Clay / Terracotta Tile"], "Flooring & Stone", null),
            ("Granite", ["Granite Slab", "Granite Tile", "Step & Riser", "Countertop Slab"], "Flooring & Stone", null),
            ("Marble", ["Marble Slab", "Marble Tile", "Countertop Slab"], "Flooring & Stone", null),
            ("Natural Stone", ["Kota Stone", "Sandstone", "Slate", "Other Natural Stone"], "Flooring & Stone", null),
            ("Tile Accessories", ["Tile Adhesive", "Grout", "Spacers", "Tile Trim"], "Flooring & Stone", null),

            ("Door Frames", ["Wooden Frame", "Granite Frame", "Stone Frame", "Metal Frame"], "Doors & Windows", null),
            ("Doors & Shutters", ["Main Door", "Internal Door", "Flush Door", "PVC Door"], "Doors & Windows", null),
            ("Windows", ["UPVC Window", "Aluminium Window", "Sliding Window"], "Doors & Windows", null),
            ("Glass", ["Clear Glass", "Toughened Glass", "Frosted Glass", "Laminated Glass"], "Doors & Windows", null),
            ("Wood & Plywood", ["Teak", "Plywood", "MDF", "Blockboard", "Wood Sections"], "Doors & Windows", null),

            ("Paint", ["Interior", "Exterior", "Emulsion", "Enamel", "Texture"], "Painting", null),
            ("Primer & Putty", ["Wall Primer", "Metal Primer", "Wall Putty"], "Painting", null),
            ("Paint Accessories", ["Roller", "Brush", "Tray", "Scraper", "Sandpaper"], "Painting", null),

            ("Hardware", ["Hinges", "Handles", "Locks", "Tower Bolts", "Brackets"], "Hardware & Fasteners", null),
            ("Fasteners", ["Screws", "Nails", "Bolts", "Nuts", "Washers", "Anchors"], "Hardware & Fasteners", null),
            ("Adhesives & Sealants", ["Silicone", "Construction Adhesive", "PVC Adhesive", "Sealant"], "Hardware & Fasteners", null),

            ("Roofing Materials", ["Roofing Sheet", "Ridge", "Flashing", "Roofing Accessories"], "Roofing & Ceiling", null),
            ("Ceiling Materials", ["Gypsum Board", "Ceiling Sections", "Ceiling Accessories"], "Roofing & Ceiling", null),

            ("Safety & Site Consumables", ["Helmet", "Gloves", "Safety Net", "Mask", "Site Consumables"], "Site & Safety", null),
            ("Miscellaneous Construction Materials", ["General"], "Site & Safety", null),
        ];

        // Old subcategory names that do not survive a prefix rule — spelled out.
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cement/OPC"] = "OPC Cement",
            ["Cement/PPC"] = "PPC Cement",
            ["Cement/PSC"] = "PSC Cement",
            ["Iron & Steel/GI Materials"] = "GI Sheet & Section",
            ["Concrete Products/Precast Items"] = "Precast Item",
            ["Concrete Products/Kerbs"] = "Kerb",
            ["Concrete Products/Pavers"] = "Paver",
            ["Construction Chemicals/Admixture"] = "Concrete Admixture",
            ["Waterproofing Materials/Membrane"] = "Waterproofing Membrane",
            ["Waterproofing Materials/Additives"] = "Waterproofing Additive",
            ["Waterproofing Materials/Sealants"] = "Waterproofing Sealant",
            ["Waterproofing Materials/Tape"] = "Waterproofing Tape",
            ["Waterproofing Materials/Accessories"] = "Waterproofing Accessory",
            ["Waterproofing Materials/Repair Materials"] = "Repair Material",
            ["Formwork & Shuttering/Props"] = "Shuttering Prop",
            ["Formwork & Shuttering/Plates"] = "Shuttering Plate",
            ["Formwork & Shuttering/Formwork Accessories"] = "Formwork Accessory",

            ["PVC Plumbing/Fittings"] = "PVC Fitting",
            ["PVC Plumbing/Drainage Pipe"] = "PVC Drainage Pipe",
            ["GI Plumbing/Fittings"] = "GI Fitting",
            ["GI Plumbing/Nipples"] = "GI Nipple",
            ["Plumbing Fixtures/Taps"] = "Tap",
            ["Plumbing Fixtures/Mixers"] = "Mixer",
            ["Plumbing Fixtures/Showers"] = "Shower",
            ["Plumbing Fixtures/Health Faucets"] = "Health Faucet",
            ["Water Storage/Tank Accessories"] = "Water Tank Accessory",
            ["Pumps & Water Equipment/Pump Accessories"] = "Pump Accessory",
            ["Bathroom Accessories/Mirror"] = "Bathroom Mirror",

            ["Electrical Wire/Single Core"] = "Single Core Wire",
            ["Electrical Wire/Multi Core"] = "Multi Core Wire",
            ["Electrical Conduit/Accessories"] = "Conduit Accessory",
            ["Electrical Switches & Sockets/Switches"] = "Switch",
            ["Electrical Switches & Sockets/Sockets"] = "Socket",
            ["Electrical Switches & Sockets/Plates"] = "Switch Plate",
            ["Electrical Switches & Sockets/Modular Accessories"] = "Modular Accessory",
            ["Distribution Boards/SPN DB"] = "SPN Distribution Board",
            ["Distribution Boards/TPN DB"] = "TPN Distribution Board",
            ["Distribution Boards/Enclosure"] = "DB Enclosure",
            ["Distribution Boards/DB Accessories"] = "DB Accessory",
            ["Electrical Accessories/Tape"] = "Insulation Tape",
            ["Earthing Materials/Compound"] = "Earthing Compound",

            ["Granite/Step & Riser"] = "Granite Step & Riser",
            ["Granite/Countertop Slab"] = "Granite Countertop",
            ["Marble/Countertop Slab"] = "Marble Countertop",
            ["Tile Accessories/Grout"] = "Tile Grout",
            ["Tile Accessories/Spacers"] = "Tile Spacer",

            ["Door Frames/Wooden Frame"] = "Wooden Door Frame",
            ["Door Frames/Granite Frame"] = "Granite Door Frame",
            ["Door Frames/Stone Frame"] = "Stone Door Frame",
            ["Door Frames/Metal Frame"] = "Metal Door Frame",
            ["Wood & Plywood/Teak"] = "Teak Wood",
            ["Wood & Plywood/Wood Sections"] = "Wood Section",

            ["Paint/Interior"] = "Interior Paint",
            ["Paint/Exterior"] = "Exterior Paint",
            ["Paint/Emulsion"] = "Emulsion Paint",
            ["Paint/Enamel"] = "Enamel Paint",
            ["Paint/Texture"] = "Texture Paint",
            ["Paint Accessories/Roller"] = "Paint Roller",
            ["Paint Accessories/Brush"] = "Paint Brush",
            ["Paint Accessories/Tray"] = "Paint Tray",

            ["Hardware/Hinges"] = "Hinge",
            ["Hardware/Handles"] = "Handle",
            ["Hardware/Locks"] = "Lock",
            ["Hardware/Tower Bolts"] = "Tower Bolt",
            ["Hardware/Brackets"] = "Bracket",
            ["Fasteners/Screws"] = "Screw",
            ["Fasteners/Nails"] = "Nail",
            ["Fasteners/Bolts"] = "Bolt",
            ["Fasteners/Nuts"] = "Nut",
            ["Fasteners/Washers"] = "Washer",
            ["Fasteners/Anchors"] = "Anchor",

            ["Roofing Materials/Roofing Accessories"] = "Roofing Accessory",
            ["Ceiling Materials/Ceiling Sections"] = "Ceiling Section",
            ["Ceiling Materials/Ceiling Accessories"] = "Ceiling Accessory",

            ["Safety & Site Consumables/Site Consumables"] = "Site Consumable",
            ["Miscellaneous Construction Materials/General"] = "General Material",
        };

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cat, subs, newCat, prefix) in old)
            foreach (var sub in subs)
            {
                var key = $"{cat}/{sub}";
                var name = renames.TryGetValue(key, out var renamed)
                    ? renamed
                    : prefix is not null && !sub.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase)
                        ? prefix + sub
                        : sub;
                map[key] = $"{newCat}/{name}";
            }
        return map;
    }

    /// <summary>
    /// Specification fields per material type, keyed "Category/Type".
    /// Company/Brand is deliberately absent — it is a first-class Material column, not a spec.
    /// </summary>
    public static IReadOnlyDictionary<string, SpecField[]> Specs { get; } = Build();

    private static Dictionary<string, SpecField[]> Build()
    {
        var d = new Dictionary<string, SpecField[]>(StringComparer.OrdinalIgnoreCase);

        void Set(string category, string[] types, SpecField[] fields)
        {
            foreach (var t in types) d[$"{category}/{t}"] = fields;
        }

        const string Civil = "Civil & Structure";
        const string Plumb = "Plumbing";
        const string Elec = "Electrical";
        const string Floor = "Flooring & Stone";

        Set(Civil, ["OPC Cement", "PPC Cement", "PSC Cement", "White Cement", "Masonry Cement"],
            [new("grade", "Grade", SpecFieldKind.Text)]);
        Set(Civil, ["River Sand", "M-Sand", "P-Sand", "Robo Sand"],
            [new("type", "Type", SpecFieldKind.Text)]);
        Set(Civil, ["6mm Aggregate", "12mm Aggregate", "20mm Aggregate", "40mm Aggregate", "Gravel", "Crusher Dust"],
        [
            new("size", "Size", SpecFieldKind.Number),
            new("size_unit", "Size Unit", SpecFieldKind.Select, SmallUnits),
            new("type", "Type", SpecFieldKind.Text),
        ]);
        Set(Civil, ["TMT Bars"],
        [
            .. Diameter(),
            new("grade", "Grade", SpecFieldKind.Select, "Fe415|Fe500|Fe500D|Fe550|Fe600"),
        ]);
        Set(Civil, ["Binding Wire"],
        [
            new("thickness", "Thickness", SpecFieldKind.Number),
            new("thickness_unit", "Thickness Unit", SpecFieldKind.Select, SmallUnits),
        ]);
        Set(Civil, ["Red Brick", "Fly Ash Brick", "Engineering Brick"], Dimensions3D());
        Set(Civil, ["AAC Block", "Solid Block", "Hollow Block", "Concrete Block"],
            [.. Dimensions3D(), new("type", "Type", SpecFieldKind.Text)]);
        Set(Civil, ["Waterproofing Chemical"],
        [
            new("type", "Type", SpecFieldKind.Text),
            new("pack_size", "Pack Size", SpecFieldKind.Text),
        ]);
        Set(Civil, ["Waterproofing Membrane"],
        [
            new("type", "Type", SpecFieldKind.Text),
            new("thickness", "Thickness", SpecFieldKind.Number),
            new("thickness_unit", "Thickness Unit", SpecFieldKind.Select, SmallUnits),
        ]);

        Set(Plumb, ["CPVC Pipe"], Diameter("Cold Water|Hot Water"));
        Set(Plumb, ["UPVC Pipe"], Diameter());
        Set(Plumb, ["PVC Pipe", "PVC Drainage Pipe"], Diameter());
        Set(Plumb, ["GI Pipe"], Diameter());

        Set(Elec, ["Single Core Wire", "Multi Core Wire", "Flexible Wire"],
        [
            new("size", "Size", SpecFieldKind.Number, Required: true),
            new("size_unit", "Size Unit", SpecFieldKind.Select, "sq.mm|mm", Required: true),
        ]);
        Set(Elec, ["Power Cable", "Armoured Cable", "Flexible Cable"],
        [
            new("size", "Size", SpecFieldKind.Number),
            new("size_unit", "Size Unit", SpecFieldKind.Select, "sq.mm|mm"),
            new("cores", "Cores", SpecFieldKind.Number),
        ]);

        Set(Floor, ["Granite Slab", "Granite Tile", "Granite Step & Riser", "Granite Countertop",
                    "Marble Slab", "Marble Tile", "Marble Countertop"], StoneSpecs());
        Set(Floor, ["Vitrified Tile", "Ceramic Tile", "Mosaic Tile",
                    "Cement / Terrazzo Tile", "Clay / Terracotta Tile"], TileSpecs());

        Set("Doors & Windows", ["Main Door", "Internal Door", "Flush Door", "PVC Door"],
            Openings("Material / Type", "Teak|Sal Wood|Flush|PVC|WPC|Steel|Aluminium"));
        Set("Doors & Windows", ["UPVC Window", "Aluminium Window", "Sliding Window"],
            Openings("Type / Movement", "Sliding|Casement|Fixed|Openable|Ventilator|Tilt & Turn"));

        Set("Painting", ["Interior Paint", "Exterior Paint", "Emulsion Paint", "Enamel Paint", "Texture Paint"],
        [
            new("type", "Type", SpecFieldKind.Text),
            new("finish", "Finish", SpecFieldKind.Select, "Matt|Satin|Sheen|Gloss|Semi Gloss|Texture"),
            new("pack_size", "Pack Size", SpecFieldKind.Text),
        ]);

        return d;
    }
}
