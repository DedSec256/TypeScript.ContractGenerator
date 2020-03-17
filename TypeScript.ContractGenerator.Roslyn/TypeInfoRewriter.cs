﻿using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SkbKontur.TypeScript.ContractGenerator.Abstractions;

using TypeInfo = SkbKontur.TypeScript.ContractGenerator.Internals.TypeInfo;

namespace SkbKontur.TypeScript.ContractGenerator.Roslyn
{
    public class TypeInfoRewriter : CSharpSyntaxRewriter
    {
        public TypeInfoRewriter(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var @namespace = node.Name.ToString();
            if (@namespace != "System" && !@namespace.StartsWith("System.") && !@namespace.StartsWith("SkbKontur.TypeScript.ContractGenerator"))
                return null;

            return base.VisitUsingDirective(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);

                if (typeInfo.Type.ToString() == typeInfoName)
                {
                    var foundType = GetSingleType(memberAccess, node.ArgumentList);

                    Types.Add(RoslynTypeInfo.From(semanticModel.GetTypeInfo(foundType).Type));
                    return ArrayElement(Types.Count - 1);
                }
            }

            return base.VisitInvocationExpression(node);
        }

        private static SyntaxNode ArrayElement(int index)
        {
            var identifier = SyntaxFactory.IdentifierName(thisName);
            var field = SyntaxFactory.IdentifierName(nameof(Types));

            var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, identifier, field);
            var argument = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(index)));
            return SyntaxFactory.ElementAccessExpression(memberAccess, SyntaxFactory.BracketedArgumentList(SyntaxFactory.SeparatedList(new[] {argument})));
        }

        private static TypeSyntax GetSingleType(MemberAccessExpressionSyntax memberAccessExpressionSyntax, BaseArgumentListSyntax argumentListSyntax)
        {
            if (memberAccessExpressionSyntax.Name is GenericNameSyntax genericNameSyntax)
                return genericNameSyntax.TypeArgumentList.Arguments.Single();

            var argument = argumentListSyntax.Arguments.Single().Expression;
            if (argument is TypeOfExpressionSyntax typeofExpression)
                return typeofExpression.Type;

            throw new InvalidOperationException($"Expected either TypeInfo.From<T>() or TypeInfo.From(typeof(T)), but found: {argumentListSyntax.Parent}");
        }

        private readonly SemanticModel semanticModel;

        private static readonly string typeInfoName = typeof(TypeInfo).FullName;
        private static readonly string thisName = typeof(TypeInfoRewriter).FullName;

        public static List<ITypeInfo> Types = new List<ITypeInfo>();
    }
}