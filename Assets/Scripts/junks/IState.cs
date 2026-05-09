using UnityEngine;

public abstract class IState
{
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}
