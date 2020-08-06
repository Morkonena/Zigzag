public static class Builders
{
	public static Result Build(Unit unit, Node node)
	{
		switch (node.GetNodeType())
		{
			case NodeType.FUNCTION_NODE:
			{
				return Calls.Build(unit, (FunctionNode)node);
			}

			case NodeType.INCREMENT_NODE:
			{
				return ArithmeticOperators.Build(unit, (IncrementNode)node);
			}

			case NodeType.DECREMENT_NODE:
			{
				return ArithmeticOperators.Build(unit, (DecrementNode)node);
			}

			case NodeType.OPERATOR_NODE:
			{
				return ArithmeticOperators.Build(unit, (OperatorNode)node);
			}

			case NodeType.OFFSET_NODE:
			{
				return Arrays.BuildOffset(unit, (OffsetNode)node, AccessMode.READ);
			}

			case NodeType.LAMBDA_NODE:
			{
				return Lambdas.Build(unit, (LambdaNode)node);
			}

			case NodeType.LINK_NODE:
			{
				return Links.Build(unit, (LinkNode)node);
			}

			case NodeType.CONSTRUCTION_NODE:
			{
				return Construction.Build(unit, (ConstructionNode)node);
			}

			case NodeType.IF_NODE:
			{
				return Conditionals.Start(unit, (IfNode)node);
			}

			case NodeType.LOOP_NODE:
			{
				return Loops.Build(unit, (LoopNode)node);
			}

			case NodeType.RETURN_NODE:
			{
				return Returns.Build(unit, (ReturnNode)node);
			}

			case NodeType.CAST_NODE:
			{
				return Casts.Build(unit, (CastNode)node);
			}

			case NodeType.NOT_NODE:
			{
				return ArithmeticOperators.BuildNot(unit, (NotNode)node);
			}

			case NodeType.NEGATE_NODE:
			{
				return ArithmeticOperators.BuildNegate(unit, (NegateNode)node);
			}

			case NodeType.ARRAY_ALLOCATION:
			{
				return Arrays.BuildAllocation(unit, (ArrayAllocationNode)node);
			}

			case NodeType.LOOP_CONTROL:
			{
				return Loops.BuildControlInstruction(unit, (LoopControlNode)node);
			}

			case NodeType.ELSE_IF_NODE:
			case NodeType.ELSE_NODE:
			{
				// Skip else-statements since they are already built
				return new Result();
			}

			default:
			{
				var iterator = node.First;

				Result? reference = null;

				while (iterator != null)
				{
					reference = Build(unit, iterator);
					iterator = iterator.Next;
				}

				return reference ?? new Result();
			}
		}
	}
}