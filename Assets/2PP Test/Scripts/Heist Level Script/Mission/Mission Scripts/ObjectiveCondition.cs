using System;
using UnityEngine;

public abstract class ObjectiveCondition : ScriptableObject
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public event Action<ObjectiveCondition> Completed;

    public bool IsCompleted { get; private set; }

    public virtual void Activate() { }
    public virtual void Deactivate() { }
    public virtual void ResetProgress() { IsCompleted = false; }

    protected void Complete()
    {
        if (IsCompleted) return;
        IsCompleted = true;
        Completed?.Invoke(this);
    }
}
