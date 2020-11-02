using System;
using System.Collections.Generic;
using System.Linq;

public static class Conditionals
{
	/// <summary>
	/// Builds the body of an if-statement or an else-if-statement
	/// </summary>
	private static Result BuildBody(Unit unit, Context local_context, Node body)
	{
		var active_variables = Scope.GetAllActiveVariablesForScope(unit, body, local_context.Parent!, local_context);

		var state = unit.GetState(unit.Position);
		Result? result;

		// Since this is a body of some statement is also has a scope
		using (var scope = new Scope(unit, active_variables))
		{
			scope.AppendFinalizers = false;

			// Merges all changes that happen in the scope with the outer scope
			var merge = new MergeScopeInstruction(unit);

			// Build the body
			result = Builders.Build(unit, body);

			// Restore the state after the body
			unit.Append(merge);

			// Keep all scope variables which are needed later active
			var required_variables = active_variables.Where(i => Analysis.IsUsedLater(i, body)).ToList();

			required_variables.ForEach(i => unit.Append(new GetVariableInstruction(unit, i)));
		}

		unit.Set(state);

		return result;
	}

	/// <summary>
	/// Builds an if-statement or an else-if-statement
	/// </summary>
	private static Result Build(Unit unit, IfNode node, Node condition, LabelInstruction end)
	{
		// Set the next label to be the end label if there's no successor since then there wont be any other comparisons
		var interphase = node.Successor == null ? end.Label : unit.GetNextLabel();

		// Initialize the condition
		node.GetConditionInitialization().ForEach(i => Builders.Build(unit, i));

		// Jump to the next label based on the comparison
		BuildCondition(unit, node.Context.Parent!, condition, interphase);

		// Get the current state of the unit for later recovery
		var recovery = new SaveStateInstruction(unit);
		unit.Append(recovery);

		// Build the body of this if-statement
		var result = BuildBody(unit, node.Context, node.Body);

		// Recover the previous state
		unit.Append(new RestoreStateInstruction(unit, recovery));

		// If the body of the if-statement is executed it must skip the potential successors
		if (node.Successor == null)
		{
			return result;
		}

		// Skip the next successor from this if-statement's body and add the interphase label
		unit.Append(new JumpInstruction(unit, end.Label));
		unit.Append(new LabelInstruction(unit, interphase));

		// Build the successor
		return Build(unit, node.Successor, end);
	}

	private static Result Build(Unit unit, Node node, LabelInstruction end)
	{
		switch (node)
		{
			case IfNode if_node:
			{
				return Build(unit, if_node, if_node.Condition, end);
			}
				
			case ElseNode else_node:
			{
				// Get the current state of the unit for later recovery
				var recovery = new SaveStateInstruction(unit);
				unit.Append(recovery);

				var result = BuildBody(unit, else_node.Context, else_node.Body);

				// Recover the previous state
				unit.Append(new RestoreStateInstruction(unit, recovery));

				return result;
			}

			default: throw new ApplicationException("Successor of an if-statement wasn't an else-if-statement or an else-statement");
		}
	}

	public static Result Start(Unit unit, IfNode node)
	{
		var roots = node.GetBranches().ToArray();
		var contexts = roots.Select(i => i is IfNode x ? x.Context : i.To<ElseNode>().Context).ToArray();
		Scope.Cache(unit, roots, contexts);

		Scope.PrepareConditionallyChangingConstants(unit, node);

		unit.Append(new BranchInstruction(unit, node.GetBranches().ToArray()));

		var end = new LabelInstruction(unit, unit.GetNextLabel());
		var result = Build(unit, node, end);
		unit.Append(end);

		return result;
	}

	public static void BuildCondition(Unit unit, Context current_context, Node condition, Label failure)
	{
		// Conditions sometimes edit variable so entering must be prepared
		Scope.PrepareConditionallyChangingConstants(unit, condition);
		unit.Append(new BranchInstruction(unit, new[] { condition }));

		var success = unit.GetNextLabel();

		var instructions = BuildCondition(unit, condition, success, failure);
		instructions.Add(new LabelInstruction(unit, success));

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
				// Since the label isn't used, it can be removed
				instructions.Remove(label);
			}
		}

		// Append all the instructions to the unit
		foreach (var instruction in instructions)
		{
			if (instruction.Is(InstructionType.TEMPORARY_COMPARE))
			{
				instruction.To<TemporaryCompareInstruction>().Append(current_context);
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

		public TemporaryCompareInstruction(Unit unit, Node comparison) : base(unit)
		{
			Comparison = comparison;
		}

		public void Append(Context current_context)
		{
			// Get the current state of the unit for later recovery
			var recovery = new SaveStateInstruction(Unit);
			Unit.Append(recovery);

			var active_variables = Scope.GetAllActiveVariablesForScope(Unit, Comparison, current_context);

			var state = Unit.GetState(Unit.Position);

			// Since this is a body of some statement is also has a scope
			using (new Scope(Unit, active_variables))
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

		public override InstructionType GetInstructionType()
		{
			return InstructionType.TEMPORARY_COMPARE;
		}
	}

	private static List<Instruction> BuildCondition(Unit unit, Node condition, Label success, Label failure)
	{
		if (condition.Is(NodeType.OPERATOR))
		{
			var operation = condition.To<OperatorNode>();
			var type = operation.Operator.Type;

			if (type == OperatorType.LOGIC)
			{
				return BuildLogicalCondition(unit, operation, success, failure);
			}
			else if (type == OperatorType.COMPARISON)
			{
				return BuildComparison(unit, operation, success, failure);
			}
		}

		if (condition.Is(NodeType.CONTENT))
		{
			return BuildCondition(unit, condition.First ?? throw new ApplicationException("Encountered an empty parenthesis while building a condition"), success, failure);
		}

		var replacement = new OperatorNode(Operators.NOT_EQUALS);
		condition.Replace(replacement);

		replacement.SetOperands(condition, new NumberNode(Assembler.Format, 0L));

		return BuildCondition(unit, replacement, success, failure);
	}

	private static List<Instruction> BuildComparison(Unit unit, OperatorNode condition, Label success, Label failure)
	{
		var x = condition.Left.GetType() ?? throw new ApplicationException("Could not get the type of left operand");
		var y = condition.Right.GetType() ?? throw new ApplicationException("Could not get the type of right operand");
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