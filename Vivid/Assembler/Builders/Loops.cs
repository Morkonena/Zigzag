using System.Collections.Generic;
using System.Linq;
using System;

public static class Loops
{
	public static Result BuildControlInstruction(Unit unit, LoopControlNode node)
	{
		if (node.Loop == null)
		{
			throw new ApplicationException("Loop control instruction was not inside a loop");
		}

		if (node.Instruction == Keywords.STOP)
		{
			var exit = node.Loop.Exit ?? throw new ApplicationException("Missing loop exit label");

			var symmetry_start = unit.Loops[node.Loop.Identifier!] ?? throw new ApplicationException("Loop was not registered to unit");
			var symmetry_end = new SymmetryEndInstruction(unit, symmetry_start);

			// Restore the state after the body
			symmetry_end.Append();

			// Restore the state after the body
			unit.Append(symmetry_end);

			return new JumpInstruction(unit, exit).Execute();
		}
		else if (node.Instruction == Keywords.CONTINUE)
		{
			var start = node.Loop.Start ?? throw new ApplicationException("Missing loop exit label");

			var symmetry_start = unit.Loops[node.Loop.Identifier!] ?? throw new ApplicationException("Loop was not registered to unit");
			var symmetry_end = new SymmetryEndInstruction(unit, symmetry_start);

			// Restore the state after the body
			symmetry_end.Append();

			// Restore the state after the body
			unit.Append(symmetry_end);

			return new JumpInstruction(unit, start).Execute();
		}
		else
		{
			throw new NotImplementedException("Unknown loop control instruction");
		}
	}

	private static Result BuildForeverLoopBody(Unit unit, LoopNode loop, LabelInstruction start)
	{
		var active_variables = Scope.GetAllActiveVariablesForScope(unit, new Node[] { loop }, loop.Body.Context.Parent!, loop.Body.Context);

		var state = unit.GetState(unit.Position);
		var result = (Result?)null;

		using (var scope = new Scope(unit, active_variables))
		{
			// Append the label where the loop will start
			unit.Append(start);

			var symmetry_start = new SymmetryStartInstruction(unit, active_variables);
			unit.Append(symmetry_start);

			// Register loop to the unit
			loop.Identifier = unit.GetNextIdentity();
			unit.Loops.Add(loop.Identifier, symmetry_start);

			// Build the loop body
			result = Builders.Build(unit, loop.Body);

			var symmetry_end = new SymmetryEndInstruction(unit, symmetry_start);

			// Restore the state after the body
			symmetry_end.Append();

			// Restore the state after the body
			unit.Append(symmetry_end);
		}

		unit.Set(state);

		return result;
	}

	private static Result BuildLoopBody(Unit unit, LoopNode loop, LabelInstruction start)
	{
		var active_variables = Scope.GetAllActiveVariablesForScope(unit, new Node[] { loop }, loop.Body.Context.Parent!, loop.Body.Context);

		var state = unit.GetState(unit.Position);
		var result = (Result?)null;

		using (var scope = new Scope(unit, active_variables))
		{
			// Append the label where the loop will start
			unit.Append(start);

			var symmetry_start = new SymmetryStartInstruction(unit, active_variables);
			unit.Append(symmetry_start);

			// Register loop to the unit
			loop.Identifier = unit.GetNextIdentity();
			unit.Loops.Add(loop.Identifier, symmetry_start);

			// Build the loop body
			result = Builders.Build(unit, loop.Body);

			if (!loop.IsForeverLoop)
			{
				// Build the loop action
				Builders.Build(unit, loop.Action);
			}

			scope.AppendFinalizers = false;

			var symmetry_end = new SymmetryEndInstruction(unit, symmetry_start);

			// Restore the state after the body
			symmetry_end.Append();

			// Restore the state after the body
			unit.Append(symmetry_end);

			// Initialize the condition
			loop.GetConditionInitialization().ForEach(i => Builders.Build(unit, i));

			BuildEndCondition(unit, loop.Condition, start.Label);

			// Keep all scope variables which are needed later active
			active_variables.ForEach(i => unit.Append(new GetVariableInstruction(unit, i, AccessMode.READ)));
		}

		unit.Set(state);

		return result;
	}

	private static Result BuildForeverLoop(Unit unit, LoopNode node)
	{
		var start = unit.GetNextLabel();

		// Get the current state of the unit for later recovery
		var recovery = new SaveStateInstruction(unit);
		unit.Append(recovery);

		if (!Assembler.IsDebuggingEnabled)
		{
			// Try to cache loop variables
			Scope.Cache(unit, node);
		}

		Scope.PrepareConditionallyChangingConstants(unit, node, node.Context, node.Body.Context);

		// Register the start and exit label to the loop for control keywords
		node.Start = unit.GetNextLabel();
		node.Exit = unit.GetNextLabel();

		// Append the start label
		unit.Append(new LabelInstruction(unit, node.Start));

		// Build the loop body
		var result = BuildForeverLoopBody(unit, node, new LabelInstruction(unit, start));

		// Jump to the start of the loop
		unit.Append(new JumpInstruction(unit, start));

		// Recover the previous state
		unit.Append(new RestoreStateInstruction(unit, recovery));

		// Append the exit label
		unit.Append(new LabelInstruction(unit, node.Exit));

		return result;
	}

	public static Result Build(Unit unit, LoopNode node)
	{
		unit.TryAppendPosition(node);
		
		if (node.IsForeverLoop)
		{
			return BuildForeverLoop(unit, node);
		}

		// Create the start and end label of the loop
		var start = unit.GetNextLabel();
		var end = unit.GetNextLabel();

		// Register the start and exit label to the loop for control keywords
		node.Start = start;
		node.Exit = end;

		// Initialize the loop
		Builders.Build(unit, node.Initialization);

		if (!Assembler.IsDebuggingEnabled)
		{
			// Try to cache loop variables
			Scope.Cache(unit, node);
		}

		Scope.PrepareConditionallyChangingConstants(unit, node, node.Body.Context);

		// Initialize the condition
		node.GetConditionInitialization().ForEach(i => Builders.Build(unit, i));

		// Jump to the end based on the comparison
		Conditionals.BuildCondition(unit, node.Context.Parent!, node.Condition, end);

		// Get the current state of the unit for later recovery
		var recovery = new SaveStateInstruction(unit);
		unit.Append(recovery);

		// Build the loop body
		var result = BuildLoopBody(unit, node, new LabelInstruction(unit, start));

		// Append the label where the loop ends
		unit.Append(new LabelInstruction(unit, end));

		// Recover the previous state
		unit.Append(new RestoreStateInstruction(unit, recovery));

		return result;
	}

	private static void BuildEndCondition(Unit unit, Node condition, Label success)
	{
		var failure = unit.GetNextLabel();

		var instructions = BuildCondition(unit, condition, success, failure);
		instructions.Add(new LabelInstruction(unit, failure));

		// Remove all occurrences of the following pattern from the instructions:
		// jmp [Label]
		// [Label]:
		for (var i = instructions.Count - 2; i >= 0; i--)
		{
			if (instructions[i].Is(InstructionType.JUMP) && instructions[i + 1].Is(InstructionType.LABEL))
			{
				var jump = instructions[i].To<JumpInstruction>();
				var label = instructions[i + 1].To<LabelInstruction>();

				if (!jump.IsConditional && Equals(jump.Label, label.Label))
				{
					instructions.RemoveAt(i);
				}
			}
		}

		// Replace all occurances of the following pattern in the instructions:
		// [Conditional jump] [Label 1]
		// jmp [Label 2]
		// [Label 1]:
		// =====================================
		// [Inverted conditional jump] [Label 2]
		// [Label 1]:
		for (var i = instructions.Count - 3; i >= 0; i--)
		{
			if (instructions[i].Is(InstructionType.JUMP) &&
			   instructions[i + 1].Is(InstructionType.JUMP) &&
			   instructions[i + 2].Is(InstructionType.LABEL))
			{
				var conditional_jump = instructions[i].To<JumpInstruction>();
				var jump = instructions[i + 1].To<JumpInstruction>();
				var label = instructions[i + 2].To<LabelInstruction>();

				if (conditional_jump.IsConditional && !jump.IsConditional && Equals(conditional_jump.Label, label.Label) && !Equals(jump.Label, label.Label))
				{
					conditional_jump.Invert();
					conditional_jump.Label = jump.Label;

					instructions.RemoveAt(i + 1);
				}
			}
		}

		// Remove unused labels
		var labels = instructions.Where(i => i.Is(InstructionType.LABEL)).Select(i => i.To<LabelInstruction>()).ToList();
		var jumps = instructions.Where(i => i.Is(InstructionType.JUMP)).Select(j => j.To<JumpInstruction>());

		foreach (var label in labels)
		{
			// Check if any jump instruction uses the current label
			if (!jumps.Any(j => j.Label == label.Label))
			{
				// Since the label is not used, it can be removed
				instructions.Remove(label);
			}
		}

		// Append all the instructions to the unit
		foreach (var instruction in instructions)
		{
			if (instruction.Is(InstructionType.TEMPORARY_COMPARE))
			{
				instruction.To<TemporaryCompareInstruction>().Append();
			}
			else
			{
				unit.Append(instruction);
			}
		}
	}

	private class TemporaryCompareInstruction : TemporaryInstruction
	{
		private Node Comparison { get; }
		private Node Left => Comparison.First!;
		private Node Right => Comparison.Last!;

		public TemporaryCompareInstruction(Unit unit, Node comparison) : base(unit, InstructionType.TEMPORARY_COMPARE)
		{
			Comparison = comparison;
		}

		public void Append()
		{
			// Get the current state of the unit for later recovery
			var recovery = new SaveStateInstruction(Unit);
			Unit.Append(recovery);

			var state = Unit.GetState(Unit.Position);

			// Since this is a body of some statement is also has a scope
			using (new Scope(Unit, Unit.Scope!.Actives))
			{
				// Merges all changes that happen in the scope with the outer scope
				var merge = new MergeScopeInstruction(Unit);

				// Build the body
				var left = References.Get(Unit, Left);
				var right = References.Get(Unit, Right);

				// Compare the two operands
				Unit.Append(new CompareInstruction(Unit, left, right));

				// Restore the state after the body
				Unit.Append(merge);
			}

			Unit.Set(state);

			// Recover the previous state
			Unit.Append(new RestoreStateInstruction(Unit, recovery));
		}
	}

	private static List<Instruction> BuildCondition(Unit unit, Node condition, Label success, Label failure)
	{
		if (condition.Is(NodeType.OPERATOR))
		{
			var operation = condition.To<OperatorNode>();

			return operation.Operator.Type switch
			{
				OperatorType.LOGIC => BuildLogicalCondition(unit, operation, success, failure),
				OperatorType.COMPARISON => BuildComparison(unit, operation, success, failure),
				_ => throw new ApplicationException(
				   "Unsupported operator encountered while building a conditional statement")
			};
		}

		if (condition.Is(NodeType.CONTENT))
		{
			return BuildCondition(unit, condition.First ?? throw new ApplicationException("Encountered an empty parenthesis while building a condition"), success, failure);
		}

		var replacement = new OperatorNode(Operators.NOT_EQUALS, condition.Position);
		condition.Replace(replacement);

		replacement.SetOperands(condition, new NumberNode(Assembler.Format, 0L, condition.Position));

		return BuildCondition(unit, replacement, success, failure);
	}

	private static List<Instruction> BuildComparison(Unit unit, OperatorNode condition, Label success, Label failure)
	{
		var x = condition.Left.GetType();
		var y = condition.Right.GetType();
		var unsigned = (x.Format.IsDecimal() || y.Format.IsDecimal()) || (x.Format.IsUnsigned() && y.Format.IsUnsigned());

		return new List<Instruction>
		{
			new TemporaryCompareInstruction(unit, condition),
			new JumpInstruction(unit, (ComparisonOperator)condition.Operator, false, !unsigned, success),
			new JumpInstruction(unit, failure)
		};
	}

	private static List<Instruction> BuildLogicalCondition(Unit unit, OperatorNode condition, Label success, Label failure)
	{
		var instructions = new List<Instruction>();
		var interphase = unit.GetNextLabel();

		if (Equals(condition.Operator, Operators.AND))
		{
			instructions.AddRange(BuildCondition(unit, condition.Left, interphase, failure));
			instructions.Add(new LabelInstruction(unit, interphase));
			instructions.AddRange(BuildCondition(unit, condition.Right, success, failure));
		}
		else if (Equals(condition.Operator, Operators.OR))
		{
			instructions.AddRange(BuildCondition(unit, condition.Left, success, interphase));
			instructions.Add(new LabelInstruction(unit, interphase));
			instructions.AddRange(BuildCondition(unit, condition.Right, success, failure));
		}
		else
		{
			throw new ApplicationException("Unsupported logical operator encountered while building a conditional statement");
		}

		return instructions;
	}
}