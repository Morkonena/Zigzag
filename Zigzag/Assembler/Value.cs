using System.Collections.Generic;
using System.Linq;

public class Value : Reference
{
	public Reference Reference { get; private set; }
	public ValueType Type { get; set; }

	public bool IsCritical { get; set; }
	public bool IsDisposable { get; private set; }
	public bool IsFloating { get; private set; }

	public Value(Register? register, Size size, ValueType type, bool critical, bool disposable, bool floating) : base(size)
	{
		Type = type;
		IsCritical = critical;
		IsDisposable = disposable;
		IsFloating = floating;

		register?.Attach(this);
	}

	protected Value(Value value) : base(value.Size)
	{
		Type = value.Type;
		IsCritical = value.IsCritical;
		IsDisposable = value.IsDisposable;
		IsFloating = value.IsFloating;


	}

	public virtual Value Clone(Register register)
	{
		Value clone = new Value(this);
		register.Attach(clone);
		return clone;
	}

	public void SetReference(Register register)
	{
		Reference = new RegisterReference(register, Size);
	}

	public override string ToString()
	{
		return Reference.ToString();
	}

	public override bool IsRegister()
	{
		return Reference.IsRegister();
	}

	public override Register GetRegister()
	{
		return Reference.GetRegister();
	}

	public override string Peek(Size size)
	{
		return Reference.Use(size);
	}

	public override string Peek()
	{
		return Reference.Use();
	}

	public override void Lock()
	{
		Reference.Lock();
	}

	public override string Use(Size size)
	{
		if (IsDisposable && Reference.IsRegister())
		{
			var register = ((RegisterReference)Reference).GetRegister();
			register.Reset();
		}

		if (IsFloating)
		{
			IsCritical = false;
		}

		return Reference.Use(size);
	}

	public override string Use()
	{
		if (IsDisposable && Reference.IsRegister())
		{
			var register = ((RegisterReference)Reference).GetRegister();
			register.Reset();
		}

		if (IsFloating)
		{
			IsCritical = false;
		}

		return Reference.Use();
	}

	public override bool IsComplex()
	{
		return Reference.IsComplex();
	}

	public override LocationType GetType()
	{
		return LocationType.VALUE;
	}

	public static Value GetObjectPointer(Register register)
	{
		return new Value(register, Size.DWORD, ValueType.OBJECT_POINTER, false, false, false);
	}

	public static Value GetOperation(Register register, Size size)
	{
		return new Value(register, size, ValueType.OPERATION, true, true, false);
	}

	public static Value GetNumber(Register register, Size size)
	{
		return new Value(register, size, ValueType.NUMBER, true, false, true);
	}

	public static Value GetString(Register register)
	{
		return new Value(register, Size.DWORD, ValueType.STRING, true, false, true);
	}

	public static Value GetVariable(Register register, Variable variable)
	{
		var value = new Value(register, Size.DWORD, ValueType.VARIABLE, true, false, true);
		value.Metadata = variable;

		return value;
		/*if (reference.IsRegister())
		{
			return new VariableValue(reference.GetRegister(), variable);
		}

		return null;*/
	}

	public static Value GetVariable(Variable variable)
	{
		var value = new Value(null, Size.DWORD, ValueType.VARIABLE, true, false, true);
		value.Metadata = variable;

		return value;
		/*if (reference.IsRegister())
		{
			return new VariableValue(reference.GetRegister(), variable);
		}

		return null;*/
	}

	public override bool Equals(object? obj)
	{
		return (obj is Reference reference &&
				Reference.Equals(reference)) ||
			   (obj is Value value &&
			   EqualityComparer<Reference>.Default.Equals(Reference, value.Reference) &&
			   IsCritical == value.IsCritical &&
			   IsDisposable == value.IsDisposable &&
			   IsFloating == value.IsFloating);
	}
}