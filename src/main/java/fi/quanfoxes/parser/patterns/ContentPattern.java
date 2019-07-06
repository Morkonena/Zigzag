package fi.quanfoxes.parser.patterns;

import fi.quanfoxes.lexer.ContentToken;
import fi.quanfoxes.lexer.Token;
import fi.quanfoxes.lexer.TokenType;
import fi.quanfoxes.parser.Context;
import fi.quanfoxes.parser.Node;
import fi.quanfoxes.parser.Parser;
import fi.quanfoxes.parser.Pattern;
import fi.quanfoxes.parser.nodes.ContentNode;

import java.util.ArrayList;
import java.util.List;

public class ContentPattern extends Pattern {
    public static final int PRIORITY = 16;

    private static final int CONTENT = 0;

    public ContentPattern() {
        super(TokenType.CONTENT);
    }

    @Override
    public int priority(List<Token> tokens) {
        return PRIORITY;
    }

    @Override
    public boolean passes(List<Token> tokens) {
        return true;
    }

    @Override
    public Node build(Context context, List<Token> tokens) throws Exception {
        ContentToken token = (ContentToken)tokens.get(CONTENT);
        ContentNode content = new ContentNode(); 

        for (int i = 0; i < token.getSectionCount(); i++) {
            ArrayList<Token> section = token.getTokens(i);
            content.add(Parser.parse(context, section));
        }

        return content;
    }
}
