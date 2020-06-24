using System;
using System.Collections.Generic;
using System.Linq;

public class Resolver
{
	/// <summary>
	/// Tries to resolve the given node tree
	/// </summary>
	public static void Resolve(Context context, Node node)
	{
		var resolved = ResolveTree(context, node);

		if (resolved != null)
		{
			node.Replace(resolved);
		}
	}

	/// <summary>
	/// Tries to resolve the problems in the given context
	/// </summary>
	public static void ResolveContext(Context context)
	{
		ResolveVariables(context);
		
		foreach (var type in context.Types.Values)
		{
			ResolveVariables(type);
			ResolveContext(type);
		}

		var implementations = context.GetFunctionImplementations().ToList();

		foreach (var implementation in implementations)
		{
			ResolveVariables(implementation);

			// Check if the implementation has a return type and if it's unresolved
			if (implementation.ReturnType?.IsUnresolved ?? false)
			{
				var type = implementation.ReturnType!.To<UnresolvedType>().TryResolveType(implementation);

				if (!Equals(type, Types.UNKNOWN))
				{
					implementation.ReturnType = type;
				}
			}

			if (implementation.Node != null)
			{
				ResolveTree(implementation, implementation.Node!);
			}
		}
	}

	/// <summary>
	/// Tries to resolve the problems in the node tree
	/// </summary>
	private static Node? ResolveTree(Context context, Node node)
	{
		if (node is IResolvable resolvable)
		{
			try
			{
				return resolvable.Resolve(context) ?? node;
			}
			catch
			{
				Console.WriteLine("Warning: Resolvable threw an exception");
			}

			return null;
		}
		
		var iterator = node.First;

		while (iterator != null)
		{
			var resolved = ResolveTree(context, iterator);

			if (resolved != null)
			{
				iterator.Replace(resolved);
			}

			iterator = iterator.Next;
		}

		return node;
	}

	/// <summary>
	/// Returns the number type that should be the outcome when using the two given numbers together
	/// </summary>
	private static Type GetSharedNumber(Number a, Number b)
	{
		return a.Bits > b.Bits ? a : b;
	}

	/// <summary>
	/// Returns the shared type between the types
	/// </summary>
	/// <returns>Success: Shared type between the types, Failure: null</returns>
	public static Type? GetSharedType(Type? a, Type? b)
	{
		if (Equals(a, b))
		{
			return a;
		}
		
		if (a == Types.UNKNOWN || b == Types.UNKNOWN)
		{
			return Types.UNKNOWN;
		}

		if (!(a is Number x) || !(b is Number y))
			return a.Supertypes.FirstOrDefault(type => b.Supertypes.Contains(type));
		
		if (a is Decimal || b is Decimal)
		{
			return Types.DECIMAL;
		}

		return GetSharedNumber(x, y);

	}

	 /// <summary>
	 /// Returns the shared type between the types
	 /// </summary>
	 /// <param name="types">Type list to go through</param>
	 /// <returns>Success: Shared type between the types, Failure: null</returns>
	private static Type? GetSharedType(IReadOnlyList<Type> types)
	{
		switch (types.Count)
		{
			case 0:
				return Types.UNKNOWN;
			case 1:
				return types[0];
		}

		var current = types[0];

		for (var i = 1; i < types.Count; i++)
		{
			if (current == null)
			{
				break;
			}

			current = GetSharedType(current, types[i]);
		}

		return current;
	}

	/// <summary>
	/// Returns the types of the child nodes of the given node
	/// </summary>
	/// <returns>Success: Types of the child nodes, Failure: null</returns>
	public static List<Type>? GetTypes(Node node)
	{
		var types = new List<Type>();
		var iterator = node.First;

		while (iterator != null)
		{
			if (iterator is IType x)
			{
				var type = x.GetType();

				if (type == Types.UNKNOWN || type.IsUnresolved)
				{
					// This operation must be aborted since type list cannot contain unresolved types
					return null;
				}
				
				types.Add(type);
			}
			else
			{
				// This operation must be aborted since type list cannot contain unresolved types
				return null;
			}

			iterator = iterator.Next;
		}

		return types;
	}

	/// <summary>
	/// Tries to get the assign type from the given assign operation
	///</summary>
	private static Type? TryGetTypeFromAssignOperation(Node assign)
	{
		var operation = assign.To<OperatorNode>();

		// Try to resolve type via contextable right side of the assign operator
		if (operation.Operator == Operators.ASSIGN &&
			operation.Right is IType x)
		{
			return x.GetType();
		}

		return Types.UNKNOWN;
	}

	/// <summary>
	/// Tries to resolve the given variable by going through its references
	/// </summary>	
	private static void Resolve(Variable variable)
	{
		var types = new List<Type>();

		// Try resolving the type of the variable from its references
		foreach (var reference in variable.References)
		{
			var parent = reference.Parent;

			if (parent == null) continue;
			
			if (parent.GetNodeType() == NodeType.OPERATOR_NODE) // Locals
			{
				// Reference must be the destination in assign operation in order to resolve the type
				if (parent.First != reference)
				{
					continue;
				}

				var type = TryGetTypeFromAssignOperation(parent);

				if (type != Types.UNKNOWN)
				{
					types.Add(type);
				}
			}
			else if (parent.GetNodeType() == NodeType.LINK_NODE) // Members
			{
				// Reference must be the destination in assign operation in order to resolve the type
				if (parent.Last != reference)
				{
					continue;
				}

				parent = parent.Parent;
					
				if (parent == null || !parent.Is(NodeType.OPERATOR_NODE))
				{
					continue;
				}

				var type = TryGetTypeFromAssignOperation(parent!);

				if (type != Types.UNKNOWN)
				{
					types.Add(type);
				}
			}
		}

		// Get the shared type between the references
		var shared = GetSharedType(types);

		if (shared != Types.UNKNOWN)
		{
			// Now the type is resolved
			variable.Type = shared;
		}
	}

	/// <summary>
	/// Tries to resolve all the variables in the given context
	/// </summary>
	private static void ResolveVariables(Context context)
	{
		foreach (var variable in context.Variables.Values.Where(v => Equals(v.Type, Types.UNKNOWN) || v.Type.IsUnresolved))
		{
			Resolve(variable);
		}

		foreach (var subcontext in context.Subcontexts)
		{
			ResolveVariables(subcontext);
		}
	}
}