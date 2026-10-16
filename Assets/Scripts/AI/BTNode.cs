using System;
using UnityEngine;
/// <summary>
/// 行为树节点的执行结果。和 Unity 的协程不同，行为树每帧都会被 Tick 一次。
/// </summary>
public enum NodeState
{
    Success,   // 本帧做完了，且成功
    Failure,   // 本帧做完了，但失败（让上层去试别的分支）
    Running    // 还没做完，下一帧继续
}
/// <summary>
/// 所有行为树节点的基类。
/// </summary>
public abstract class BTNode
{
    protected NodeState state;
    public NodeState State => state;
    /// <summary>每帧调用一次，返回本节点的执行结果。</summary>
    public abstract NodeState Tick();
    /// <summary>重置内部进度（比如"跑到第几个子节点了"）。</summary>
    public virtual void Reset() { state = NodeState.Failure; }
}
/// <summary>组合节点基类：管理一组子节点和"当前跑到第几个"。</summary>
public abstract class BTComposite : BTNode
{
    protected readonly BTNode[] children;
    protected int current;
    protected BTComposite(params BTNode[] children)
    {
        this.children = children ?? new BTNode[0];
    }
    public override void Reset()
    {
        base.Reset();
        current = 0;
        foreach (var c in children) c.Reset();
    }
}
/// <summary>
/// 顺序节点（带记忆）：挨个执行子节点，全部成功才成功。
/// 某个子节点返回 Running 时会【记住位置】，下一帧从它继续，不再重跑前面的。
/// </summary>
public class Sequence : BTComposite
{
    public Sequence(params BTNode[] children) : base(children) { }
    public override NodeState Tick()
    {
        while (current < children.Length)
        {
            state = children[current].Tick();
            if (state == NodeState.Running) return state;          // 记住 current
            if (state == NodeState.Failure) { Reset(); return NodeState.Failure; }
            current++;                                              // Success → 下一个
        }
        Reset();
        return NodeState.Success;
    }
}
/// <summary>
/// 反应式顺序节点（无记忆）：每帧都【从第一个子节点重新开始】执行。
/// 这是"条件 + 动作"组合的正确写法 —— 条件一变化就能立刻切换分支。
/// 例如：危险一消失，"远离危险"这个分支就应该马上失效。
/// </summary>
public class ReactiveSequence : BTNode
{
    private readonly BTNode[] children;
    public ReactiveSequence(params BTNode[] children)
    {
        this.children = children ?? new BTNode[0];
    }
    public override void Reset()
    {
        base.Reset();
        foreach (var c in children) c.Reset();
    }
    public override NodeState Tick()
    {
        for (int i = 0; i < children.Length; i++)
        {
            state = children[i].Tick();
            if (state == NodeState.Running) return state;
            if (state == NodeState.Failure) return NodeState.Failure;
        }
        return NodeState.Success;
    }
}
/// <summary>
/// 选择节点：挨个尝试子节点，谁先成功就用谁。
/// 全部失败才返回 Failure。用来表达"优先级"——排在前面的分支优先。
/// </summary>
public class Selector : BTComposite
{
    public Selector(params BTNode[] children) : base(children) { }
    public override NodeState Tick()
    {
        while (current < children.Length)
        {
            state = children[current].Tick();
            if (state == NodeState.Running) return state;           // 记住 current
            if (state == NodeState.Success) { Reset(); return NodeState.Success; }
            current++;                                              // Failure → 试下一个
        }
        Reset();
        return NodeState.Failure;
    }
}
/// <summary>条件节点：包一个返回 bool 的委托，true → Success，false → Failure。</summary>
public class Condition : BTNode
{
    private readonly Func<bool> check;
    public Condition(Func<bool> check) { this.check = check; }
    public override NodeState Tick()
    {
        state = check() ? NodeState.Success : NodeState.Failure;
        return state;
    }
}
/// <summary>动作节点：包一个返回 NodeState 的委托（通常返回 Running）。</summary>
public class Action : BTNode
{
    private readonly Func<NodeState> act;
    public Action(Func<NodeState> act) { this.act = act; }
    public override NodeState Tick()
    {
        state = act();
        return state;
    }
}
/// <summary>取反装饰器：子节点成功就失败，失败就成功。Running 原样透传。</summary>
public class Inverter : BTNode
{
    private readonly BTNode child;
    public Inverter(BTNode child) { this.child = child; }
    public override void Reset() { base.Reset(); child.Reset(); }
    public override NodeState Tick()
    {
        var s = child.Tick();
        if (s == NodeState.Running) return s;
        state = (s == NodeState.Success) ? NodeState.Failure : NodeState.Success;
        return state;
    }
}
/// <summary>
/// 反应式选择节点：每帧都从第一个子节点重新尝试。
///
/// AI 的"优先级决策"必须用这个，不能用带记忆的 Selector。
/// 否则一旦落到最后一个分支（比如"闲逛"），就再也不会回头检查
/// 前面更高优先级的分支了 —— 玩家走近了它也不会逃、也不会攻击。
/// </summary>
public class ReactiveSelector : BTNode
{
    private readonly BTNode[] children;

    public ReactiveSelector(params BTNode[] children)
    {
        this.children = children ?? new BTNode[0];
    }

    public override void Reset()
    {
        base.Reset();
        foreach (var c in children) c.Reset();
    }

    public override NodeState Tick()
    {
        for (int i = 0; i < children.Length; i++)
        {
            state = children[i].Tick();

            if (state == NodeState.Running) return state;
            if (state == NodeState.Success) return state;
        }
        return NodeState.Failure;
    }
}
