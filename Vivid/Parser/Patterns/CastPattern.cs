﻿using System.Collections.Generic;

public class CastPattern : Pattern
{
	public const int PRIORITY = 18;

	private const int OBJECT = 0;
	private const int CAST = 1;
	private const int TYPE = 2;

	// ... as Type
	public CastPattern() : base
	(
		TokenType.OBJECT,
		TokenType.KEYWORD,
		TokenType.IDENTIFIER | TokenType.DYNAMIC
	)
	{ }

	public override int GetPriority(List<Token> tokens)
	{
		return PRIORITY;
	}

	public override bool Passes(Context context, PatternState state, List<Token> tokens)
	{
		return tokens[CAST].To<KeywordToken>().Keyword == Keywords.AS;
	}

	public override Node Build(Context context, List<Token> tokens)
	{
		var source = Singleton.Parse(context, tokens[OBJECT]);
		var type = Singleton.Parse(context, tokens[TYPE]);

		return new CastNode(source, type);
	}
}