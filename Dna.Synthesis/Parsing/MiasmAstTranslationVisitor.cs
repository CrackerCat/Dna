﻿using Antlr4.Runtime.Misc;
using Dna.Synthesis.Miasm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dna.Synthesis.Parsing
{
    class MiasmAstTranslationVisitor : MiasmBaseVisitor<Expr>
    {
        public override Expr VisitBinaryComplementExpression([NotNull] MiasmParser.BinaryComplementExpressionContext context)
        {
            var expr = Visit(context.expression());
            return new ExprOp(expr.Size, "^", expr, new ExprInt(Convert.ToUInt64(-1), expr.Size));
        }

        public override Expr VisitNegateExpression([NotNull] MiasmParser.NegateExpressionContext context)
        {
            var expr = Visit(context.expression());
            return new ExprOp(expr.Size, "-", expr);
        }

        public override Expr VisitMulExpression([NotNull] MiasmParser.MulExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "*", expr1, expr2);
        }

        public override Expr VisitAddExpression([NotNull] MiasmParser.AddExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "+", expr1, expr2);
        }

        public override Expr VisitAndExpression([NotNull] MiasmParser.AndExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "&", expr1, expr2);
        }

        public override Expr VisitOrExpression([NotNull] MiasmParser.OrExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "|", expr1, expr2);
        }

        public override Expr VisitXorExpression([NotNull] MiasmParser.XorExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "^", expr1, expr2);
        }

        public override Expr VisitLeftShiftExpression([NotNull] MiasmParser.LeftShiftExpressionContext context)
        {
            var expressions = context.expression();
            var expr1 = Visit(expressions[0]);
            var expr2 = Visit(expressions[1]);
            if (expr1.Size != expr2.Size)
                throw new InvalidOperationException();

            return new ExprOp(expr1.Size, "<<", expr1, expr2);
        }

        public override Expr VisitSliceExpression([NotNull] MiasmParser.SliceExpressionContext context)
        {
            var expr = Visit(context.expression());
            var numbers = context.NUMBER();

            var start = Convert.ToUInt32(numbers[0].ToString());
            var stop = Convert.ToUInt32(numbers[1].ToString());
            return new ExprSlice(expr, start, stop);
        }

        public override Expr VisitIdExpression([NotNull] MiasmParser.IdExpressionContext context)
        {
            // If the name contains quotations(e.g. "p0"), remove them.
            var name = context.STRING().GetText();
            name.Replace(@"""", "");

            var sizeText = context.NUMBER().GetText();
            var size = Convert.ToUInt32(sizeText);

            return new ExprId(name, size);
        }

        public override Expr VisitIntExpression([NotNull] MiasmParser.IntExpressionContext context)
        {
            var numbers = context.NUMBER();
            var value = Convert.ToUInt32(numbers[0].GetText());
            var size = Convert.ToUInt32(numbers[1].GetText());
            return new ExprInt(value, size);
        }
    }
}
