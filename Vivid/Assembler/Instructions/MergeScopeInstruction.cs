using System.Collections.Generic;

/// <summary>
/// Relocates variables so that their locations match the state of the outer scope
/// This instruction works on all architectures
/// </summary>
public class MergeScopeInstruction : Instruction
{
	public MergeScopeInstruction(Unit unit) : base(unit, InstructionType.MERGE_SCOPE) { }

	private Result GetVariableStackHandle(Variable variable)
	{
		return new Result(References.CreateVariableHandle(Unit, variable), variable.Type!.Format);
	}

	private Result GetDestinationHandle(Variable variable)
	{
		return Unit.Scope!.Outer?.GetCurrentVariableHandle(variable) ?? GetVariableStackHandle(variable);
	}

	private bool IsUsedLater(Variable variable)
	{
		return Unit.Scope!.Outer?.IsUsedLater(variable) ?? false;
	}

	public override void OnBuild()
	{
		var moves = new List<MoveInstruction>();

		foreach (var variable in Scope!.Actives)
		{
			var source = Unit.GetCurrentVariableHandle(variable) ?? GetVariableStackHandle(variable);

			// Copy the destination value to prevent any relocation leaks
			var destination = new Result(GetDestinationHandle(variable).Value, variable.GetRegisterFormat());

			// When the destination is a memory handle, it most likely means it won't be used later
			if (destination.IsMemoryAddress && !IsUsedLater(variable))
			{
				continue;
			}

			if (destination.IsConstant)
			{
				continue;
			}

			moves.Add(new MoveInstruction(Unit, destination, source));
		}

		Unit.Append(Memory.Align(Unit, moves), true);
	}
}