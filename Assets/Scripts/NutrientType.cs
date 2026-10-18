using UnityEngine;

/// <summary>
/// 营养球类型。原先嵌在 em 里（em.NutrientType），
/// 现在 em 被废弃，独立成单独的类型，供 Nutrient / NutrientSpawner 使用。
/// </summary>
public enum NutrientType
{
    Small,    // 小营养体：4 能量
    Medium,   // 中营养体：16 能量
    Large     // 大营养体：64 能量
}
