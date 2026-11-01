namespace ConstructOPX.Domain.Features.Costing;

public sealed record WbsDivision(int Number, string MainScope);

public sealed record WbsEntry(int Division, string MainScope, string Code, string SubScope);

/// <summary>
/// The standard work breakdown the retired desktop app generated in code (CSI MasterFormat 1995, 16 divisions): a division fixes
/// the main scope, and a work-breakdown code fixes the sub-scope. Custom codes are still allowed (see <see cref="CostClassification"/>).
/// It is reference data: it never changes at run time and is the same for every organization.
/// </summary>
public static class WbsCatalog
{
    public const int FirstDivision = 1;
    public const int LastDivision = 16;

    private static readonly string[] DivisionNames =
    [
        "General Requirements", "Site Construction", "Concrete", "Masonry", "Metals", "Wood and Plastics",
        "Thermal and Moisture Protection", "Doors and Windows", "Finishes", "Specialties", "Equipment", "Furnishings",
        "Special Construction", "Conveying Systems", "Mechanical", "Electrical"
    ];

    // division|code|sub-scope
    private const string Table = """
        1|1100|Summary
        1|1200|Price and Payment Procedures
        1|1300|Administrative Requirements
        1|1400|Quality Requirements
        1|1500|Temporary Facilities and Controls
        1|1600|Product Requirements
        1|1700|Execution Requirements
        1|1800|Facility Operation
        1|1900|Facility Decommissioning
        2|2050|Basic Site Materials and Methods
        2|2100|Site Remediation
        2|2200|Site Preparation
        2|2300|Earthwork
        2|2400|Tunneling, Boring and Jacking
        2|2450|Foundation and Load-Bearing Elements
        2|2500|Utility Services
        2|2600|Drainage and Containment
        2|2700|Bases, Ballasts, Pavements and Appurtenances
        2|2800|Site Improvements and Amenities
        2|2900|Planting
        2|2950|Site Restoration and Rehabilitation
        3|3050|Basic Concrete Materials and Methods
        3|3100|Concrete Forms and Accessories
        3|3200|Concrete Reinforcement
        3|3300|Cast-In-Place Concrete
        3|3400|Precast Concrete
        3|3500|Cementitious Decks and Underlayment
        3|3600|Grouts
        3|3700|Mass Concrete
        3|3900|Concrete Restoration and Cleaning
        4|4050|Basic Masonry Materials and Methods
        4|4200|Masonry Units
        4|4400|Stone
        4|4500|Refractories
        4|4600|Corrosion-Resistant Masonry
        4|4700|Simulated Masonry
        4|4800|Masonry Assemblies
        4|4900|Masonry Restoration and Cleaning
        5|5050|Basic Metal Materials and Methods
        5|5100|Structural Metal Framing
        5|5200|Metal Joists
        5|5300|Metal Deck
        5|5400|Cold-Formed Metal Framing
        5|5500|Metal Fabrications
        5|5600|Hydraulic Fabrications
        5|5700|Ornamental Metal
        5|5800|Expansion Control
        5|5900|Metal Restoration and Cleaning
        6|6050|Basic Wood and Plastic Materials and Methods
        6|6100|Rough Carpentry
        6|6200|Finish Carpentry
        6|6400|Architectural Woodwork
        6|6500|Structural Plastics
        6|6600|Plastic Fabrications
        6|6900|Wood and Plastic Restoration and Cleaning
        7|7050|Basic Thermal and Moisture Protection Materials and Methods
        7|7100|Damproofing and Waterproofing
        7|7200|Thermal Protection
        7|7300|Shingles, Roof Tiles, and Roof Coverings
        7|7400|Roofing and Siding Panels
        7|7500|Membrane Roofing
        7|7600|Flashing and Sheet Metal
        7|7700|Roof Specialties and Accessories
        7|7800|Fire and Smoke Protection
        7|7900|Joint Sealers
        8|8050|Basic Door and Window Materials and Methods
        8|8100|Metal Doors and Frames
        8|8200|Wood and Plastic Doors
        8|8300|Specialty Doors
        8|8400|Entrances and Storefronts
        8|8500|Windows
        8|8600|Skylights
        8|8700|Hardware
        8|8800|Glazing
        8|8900|Glazed Curtain Wall
        9|9050|Basic Finish Materials and Methods
        9|9100|Metal Support Assemblies
        9|9200|Plaster and Gypsum Board
        9|9300|Tile
        9|9400|Terrazzo
        9|9500|Ceilings
        9|9600|Flooring
        9|9700|Wall Finishes
        9|9800|Acoustical Treatment
        9|9900|Paints and Coatings
        10|10100|Visual Display Boards
        10|10150|Compartments and Cubicles
        10|10200|Louvers and Vents
        10|10240|Grilles and Screens
        10|10250|Service Walls
        10|10260|Wall and Corner Guards
        10|10270|Access Flooring
        10|10290|Pest Control
        10|10300|Fireplaces and Stoves
        10|10340|Manufactured Exterior Specialties
        10|10350|Flagpoles
        10|10400|Identification Devices
        10|10450|Pedestrian Control Devices
        10|10500|Lockers
        10|10520|Fire Protection Specialties
        10|10530|Protective Covers
        10|10550|Postal Specialties
        10|10600|Partitions
        10|10670|Storage Shelving
        10|10700|Exterior Protection
        10|10750|Telephone Specialties
        10|10800|Toilet, Bath, and Laundry Specialties
        10|10880|Scales
        10|10900|Wardrobe and Closet Specialties
        11|11010|Maintenance Equipment
        11|11020|Security and Vault Equipment
        11|11030|Teller and Service Equipment
        11|11040|Ecclesiastical Equipment
        11|11050|Library Equipment
        11|11060|Theater and Stage Equipment
        11|11070|Instrumental Equipment
        11|11080|Registration Equipment
        11|11090|Checkroom Equipment
        11|11100|Mercantile Equipment
        11|11110|Commercial Laundry and Dry Cleaning Equipment
        11|11120|Vending Equipment
        11|11130|Audio-Visual Equipment
        11|11140|Vehicle Service Equipment
        11|11150|Parking Control Equipment
        11|11160|Loading Dock Equipment
        11|11170|Solid Waste Handling Equipment
        11|11190|Detention Equipment
        11|11200|Water Supply and Treatment Equipment
        11|11280|Hydraulic Gates and Valves
        11|11300|Fluid Waste Treatment and Disposal Equipment
        11|11400|Food Service Equipment
        11|11450|Residential Equipment
        11|11460|Unit Kitchens
        11|11470|Darkroom Equipment
        11|11480|Athletic, Recreational, and Therapeutic Equipment
        11|11500|Industrial and Process Equipment
        11|11600|Laboratory Equipment
        11|11650|Planetarium Equipment
        11|11660|Observatory Equipment
        11|11680|Office Equipment
        11|11700|Medical Equipment
        11|11780|Mortuary Equipment
        11|11850|Navigation Equipment
        11|11870|Agricultural Equipment
        11|11900|Exhibit Equipment
        12|12050|Fabrics
        12|12100|Art
        12|12300|Manufactured Casework
        12|12400|Furnishings and Accessories
        12|12500|Furniture
        12|12600|Multiple Seating
        12|12700|Systems Furniture
        12|12800|Interior Plants and Planters
        12|12900|Furnishings Restoration and Repair
        13|13010|Air-Supported Structures
        13|13020|Building Modules
        13|13030|Special Purpose Rooms
        13|13080|Sound, Vibration, and Seismic Control
        13|13090|Radiation Protection
        13|13100|Lightning Protection
        13|13110|Cathodic Protection
        13|13120|Pre-Engineered Structures
        13|13150|Swimming Pools
        13|13160|Aquariums
        13|13165|Aquatic Park Facilities
        13|13170|Tubs and Pools
        13|13175|Ice Rinks
        13|13185|Kennels and Animal Shelters
        13|13190|Site-Constructed Incinerators
        13|13200|Storage Tanks
        13|13220|Filter Underdrains and Media
        13|13230|Digester Covers and Appurtenances
        13|13240|Oxygenation Systems
        13|13260|Sludge Conditioning Systems
        13|13280|Hazardous Material Remediation
        13|13400|Measurement and Control Instrumentation
        13|13500|Recording Instrumentation
        13|13550|Transportation Control Instrumentation
        13|13600|Solar and Wind Energy Equipment
        13|13700|Security Access and Surveillance
        13|13800|Building Automation and Control
        13|13850|Detection and Alarm
        13|13900|Fire Suppression
        14|14100|Dumbwaiters
        14|14200|Elevators
        14|14300|Escalators and Moving Walks
        14|14400|Lifts
        14|14500|Material Handling
        14|14600|Hoists and Cables
        14|14700|Turntables
        14|14800|Scaffolding
        14|14900|Transportation
        15|15050|Basic Mechanical Materials and Methods
        15|15100|Building Service Piping
        15|15200|Process Piping
        15|15300|Fire Protection Piping
        15|15400|Plumbing Fixtures and Equipment
        15|15500|Heat-Generation Equipment
        15|15600|Refrigeration Equipment
        15|15700|Heating, Ventilating, and Air Conditioning Equipment
        15|15800|Air Distribution
        15|15900|HVAC Instrumentation and Controls
        15|15950|Testing, Adjusting, and Balancing
        16|16050|Basic Electrical Materials and Methods
        16|16100|Wiring Methods
        16|16200|Electrical Power
        16|16300|Transmission and Distribution
        16|16400|Low-Voltage Distribution
        16|16500|Lighting
        16|16700|Communications
        16|16800|Sound and Video
        """;

    private static readonly Lazy<IReadOnlyList<WbsEntry>> LazyEntries = new(Parse);
    private static readonly Lazy<Dictionary<string, WbsEntry>> LazyByCode = new(() => Entries.ToDictionary(entry => entry.Code, StringComparer.Ordinal));

    public static IReadOnlyList<WbsDivision> Divisions { get; } =
        DivisionNames.Select((name, index) => new WbsDivision(index + 1, name)).ToArray();

    public static IReadOnlyList<WbsEntry> Entries => LazyEntries.Value;

    public static WbsEntry? FindByCode(string? code) =>
        code is not null && LazyByCode.Value.TryGetValue(code.Trim(), out var entry) ? entry : null;

    public static string? MainScopeOf(int division) =>
        division is >= FirstDivision and <= LastDivision ? DivisionNames[division - 1] : null;

    public static IReadOnlyList<WbsEntry> ForDivision(int division) => Entries.Where(entry => entry.Division == division).ToArray();

    private static IReadOnlyList<WbsEntry> Parse() =>
        Table.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|'))
            .Select(parts => new WbsEntry(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                DivisionNames[int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) - 1], parts[1], parts[2]))
            .ToArray();
}
