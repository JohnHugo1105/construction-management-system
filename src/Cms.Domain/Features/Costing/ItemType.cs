namespace Cms.Domain.Features.Costing;

/// <summary>
/// What a line pays for. The desktop app offered only Material and Labor, then smuggled tools and equipment in as free text
/// from the warehouse screen.
/// </summary>
public enum ItemType
{
    Material = 0,
    Labor = 1,
    Equipment = 2,
    Subcontract = 3,
    Other = 4
}
