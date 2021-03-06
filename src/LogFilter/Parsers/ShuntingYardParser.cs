﻿namespace LogFlow.Viewer.LogFilter.Parsers
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    using LogFlow.Viewer.LogFilter.Expressions;
    using LogFlow.Viewer.LogFilter.Tokens;

    /// <summary>
    /// Parse LL(1) grammar
    ///     E -> P{BP|P}
    ///     P -> v|(E)|UP
    ///     B -> && | ||
    ///     V -> !
    /// </summary>
    internal class ShuntingYardParser
    {
        private TokenInput input;

        private Stack<OperatorToken> operators;

        private Stack<Expression> operands;

        internal ShuntingYardParser(List<Token> tokens)
        {
            this.input = new TokenInput(tokens);
        }

        [Pure]
        private Token Next()
        {
            var peek = this.input.Peek();
            if (peek == null)
            {
                return new EndOfFileToken();
            }
            else
            {
                return peek;
            }
        }

        private void Consume()
        {
            this.input.Read();
        }

        private void Expect<T>() where T : Token
        {
            if (this.Next() is T)
            {
                this.Consume();
            }
            else
            {
                throw new ParsingException($"Expected {typeof(T)} but get {this.Next().GetType()}", this.Next().Index);
            }
        }

        internal Expression Parse()
        {
            this.operators = new Stack<OperatorToken>();
            this.operands = new Stack<Expression>();
            this.operators.Push(new OpenParenthesisToken());
            this.E();
            this.Expect<EndOfFileToken>();
            return this.operands.Peek();
        }

        internal void E()
        {
            this.P();
            while (true)
            {
                if (this.Next() is BinaryOperaterToken)
                {
                    this.PushOperator((BinaryOperaterToken)this.Next());
                    this.Consume();
                    this.P();
                }
                else if (this.Next() is ContentToken || this.Next() is OpenParenthesisToken || this.Next() is UnaryOperatorToken)
                {
                    // Check all P's possible starts here to get the syntax sugar "P P = P && P".
                    this.PushOperator(new LogicalAndToken());
                    this.P();
                }
                else
                {
                    break;
                }
            }

            while (!(this.operators.Peek() is OpenParenthesisToken))
            {
                this.PopOperator();
            }
        }

        internal void P()
        {
            if (this.Next() is ContentToken)
            {
                this.operands.Push(MkLeaf(this.Next()));
                this.Consume();
            }
            else if (this.Next() is OpenParenthesisToken)
            {
                this.Consume();
                this.operators.Push(new OpenParenthesisToken());
                this.E();
                this.Expect<CloseParenthesisToken>();
                this.operands.Push(new ParenthesisedExpression { Oprand = this.operands.Pop() });
                this.operators.Pop();
            }
            else if (this.Next() is UnaryOperatorToken)
            {
                this.PushOperator((UnaryOperatorToken)this.Next());
                this.Consume();
                this.P();
            }
            else
            {
                throw new ParsingException($"{this.Next().ToString()} is not a valid start of parameter. Expected filter content, open parenthesis or logical not operator.", this.Next().Index);
            }
        }

        private void PushOperator(OperatorToken op)
        {
            // Here we use openParenthesisToken as sentinel token
            while (this.operators.Peek().Precedence > op.NewPushPrecedence)
            {
                this.PopOperator();
            }

            this.operators.Push(op);
        }

        private void PopOperator()
        {
            if (this.operators.Peek() is BinaryOperaterToken)
            {
                Expression oprand2 = this.operands.Pop();
                Expression oprand1 = this.operands.Pop();
                this.operands.Push(MkNode(this.operators.Pop(), oprand1, oprand2));
            }
            else
            {
                this.operands.Push(MkNode(this.operators.Pop(), this.operands.Pop()));
            }
        }

        private static Expression MkNode(Token op, Expression oprand1, Expression oprand2)
        {
            if (op is LogicalAndToken)
            {
                return new LogicalAndExpression() { Lhs = oprand1, Rhs = oprand2 };
            }
            else if (op is LogicalOrToken)
            {
                return new LogicalOrExpression() { Lhs = oprand1, Rhs = oprand2 };
            }

            throw new ParsingException($"{op.ToString()} is not {nameof(BinaryOperaterToken)}", op.Index);
        }

        private static Expression MkNode(Token op, Expression operand)
        {
            if (op is LogicalNotToken)
            {
                return new LogicalNotExpression { Oprand = operand };
            }

            throw new ParsingException($"{op.ToString()} is not {nameof(UnaryOperatorToken)}", op.Index);
        }

        private static Expression MkLeaf(Token token)
        {
            if (token is ContentToken)
            {
                return ContentMatchExpression.CreateContentMatchExpression((ContentToken)token);
            }

            throw new ParsingException($"{token.ToString()} is not {nameof(ContentToken)}", token.Index);
        }
    }
}
