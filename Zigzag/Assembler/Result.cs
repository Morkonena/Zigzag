using System.Collections.Generic;
using System.Linq;
using System;

public enum JoinSettings
{
    /// <summary>
    /// Normal send and receive
    /// </summary>
    DEFAULT,
    /// <summary>
    /// Filters all the changes coming from the other system
    /// </summary>
    DISABLE_RECEIVE,
    /// <summary>
    /// Filters all the changes leaving to the other system
    /// </summary>
    DISABLE_SEND
}

public struct Connection
{
    public Result Result;
    public bool IsSendingEnabled;
}

public class Result
{
    public Instruction? Instruction { get; set; }

    private Metadata _Metadata = new Metadata();
    public Metadata Metadata { 
        get => _Metadata; 
        set {
            _Metadata = value;
            Connections.ForEach(c => {
                if (c.IsSendingEnabled) {
                    c.Result._Metadata = value;
                }
            });
        }
    }

    private Handle _Value;
    public Handle Value {
        get => _Value;
        set {
            _Value = value;
            Connections.ForEach(c => {
                if (c.IsSendingEnabled) {
                    c.Result._Value = value;
                }
            });
        }
    }

    public Lifetime Lifetime { get; private set; } = new Lifetime();

    private List<Connection> Connections = new List<Connection>();
    private IEnumerable<Result> System => Connections.Select(c => c.Result).Concat(new List<Result>{ this });
    private IEnumerable<Result> Others => Connections.Select(c => c.Result);

    public bool Empty => _Value.Type == HandleType.NONE;

    public bool IsReleasable()
    {
        // Prevent releasing the this pointer
        if (Metadata.Primary is VariableAttribute attribute && attribute.Variable.IsThisPointer)
        {
            return false;
        }

        return Metadata.Variables.Count() > 0 && Metadata.Variables.All(v => v.Variable.IsPredictable);
    }

    public Result(Instruction instruction)
    {
        _Value = new Handle();
        Instruction = instruction;
    }

    public Result(Instruction instruction, Handle value)
    {
        _Value = value;
        Instruction = instruction;
    }

    public Result(Handle value)
    {
        _Value = value;
    }

    public Result()
    {
        _Value = new Handle();
    }

    /// <summary>
    /// Connects this result to the other system (doesn't make duplicates)
    /// </summary>
    private void Connect(IEnumerable<Result> system)
    {
        Connections.AddRange(system
            .Where(result => System.All(m => m != result))
            .Select(result => new Connection() {
                Result = result,
                IsSendingEnabled = true
            })
        );
    }

    private void Update()
    {
        // Update the value to the same because it sends an update wave which corrects all values across the system
        Metadata = Metadata;
        Value = Value;

        /// TODO: Turn into automatic update?
        foreach (var member in Others)
        {
            member.Instruction = Instruction;
            member.Lifetime = Lifetime;
        }
    }

    public void Join(Result parent)
    {
        foreach (var member in System)
        {
            member.Connect(parent.System);
        }

        foreach (var member in parent.System)
        {
            member.Connect(System);
        }

        parent.Update();
    }

    public bool IsExpiring(int position)
    {
        return position == -1 || !Lifetime.IsActive(position + 1);
    }

    public bool IsValid(int position)
    {
        return Lifetime.IsActive(position);
    }

    public void Use(int position)
    {
        if (position > Lifetime.End)
        {
            Lifetime.End = position;
        }

        if (Lifetime.Start == -1 || position < Lifetime.Start)
        {
            Lifetime.Start = position;
        }

        Value.Use(position);
        Connections.ForEach(c => {
            if (c.IsSendingEnabled) {
                c.Result.Lifetime = Lifetime.Clone();
            }
        });
    }

    public void Set(Handle value, bool force = false)
    {
        if (force)
        {
            // Do not care about the value permissions
            System.ToList().ForEach(r => r._Value = value);
        }
        else
        {
            Value = value;
        }
    }

    public override bool Equals(object? other)
    {
        return base.Equals(other) || other is Result result && result.Connections.Exists(c => c.Result == this);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public override string ToString() 
    {
        return Value?.ToString() ?? throw new InvalidOperationException("Missing value");
    }
}