using System.Collections.Generic;
using System.Linq;

public class ExpressionVariablePattern : Pattern
{
	public const int PRIORITY = 21;

	public const int IMPLICATION = 1;

	// Pattern: $name => ...
	public ExpressionVariablePattern() : base
	(
		TokenType.IDENTIFIER,
		TokenType.OPERATOR
	) { }

	public override bool Passes(Context context, PatternState state, List<Token> tokens)
	{
		return context.IsInsideType && tokens[IMPLICATION].Is(Operators.IMPLICATION);
	}

	public override Node? Build(Context type, PatternState state, List<Token> tokens)
	{
		var name = tokens.First().To<IdentifierToken>();

		// Create function which has the name of the property but has no parameters
		var function = new Function(type, Modifier.DEFAULT, name.Value) { Position = name.Position };
		function.DeclareSelfPointer();

		// Collect the tokens of the body
		// Add the implication operator token to the start of the blueprint to represent a return statement
		var blueprint = new List<Token> { tokens[IMPLICATION] };

		if (!Common.ConsumeBlock(function, state, blueprint))
		{
			throw Errors.Get(name.Position, $"Could not resolve the body of the property '{name.Value}'");
		}

		// Save the blueprint
		function.Blueprint.AddRange(blueprint);

		// Finally, declare the function
		type.Declare(function);

		return new FunctionDefinitionNode(function, name.Position);
	}

	public override int GetPriority(List<Token> tokens)
	{
		return PRIORITY;
	}
}