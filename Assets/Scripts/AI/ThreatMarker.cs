using UnityEngine;

/// <summary>
/// 威胁标记：挂在"会让敌方细胞害怕"的东西上。
///
/// 对应策划文档里的"危险（带攻击细胞的玩家、伤害蛋白）"。
/// 敌方 AI 只认带这个组件、并且 enabled == true 的对象。
///
/// 用法：
///   - 玩家：拿到攻击细胞时 threat.enabled = true，失去时 = false
///   - 伤害蛋白：预制体上直接挂一个，永久 enabled
/// </summary>
public class ThreatMarker : MonoBehaviour
{
    [Tooltip("威胁等级，以后可以用来区分\"很危险\"和\"有点危险\"，现在没用上")]
    public int level = 1;
}