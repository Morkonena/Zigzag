package fi.quanfoxes.Lexer;

import java.util.Objects;

public class OperatorToken extends Token {

    private OperatorType operator;

    public OperatorToken(Lexer.TokenArea area) {
        super(TokenType.OPERATOR);
        operator = OperatorType.get(area.text);
    }

    public OperatorToken(OperatorType type) {
        super(TokenType.OPERATOR);
        operator = type;
    }

    public OperatorType getOperator ()  {
        return operator;
    }

    @Override
    public String getText() {
        return operator.getText();
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (!(o instanceof OperatorToken)) return false;
        if (!super.equals(o)) return false;
        OperatorToken that = (OperatorToken) o;
        return operator == that.operator;
    }

    @Override
    public int hashCode() {
        return Objects.hash(super.hashCode(), operator);
    }
}
