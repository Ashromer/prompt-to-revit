using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RevitBridge.Core.Compiler;

/// <summary>
/// F1.1 Filtro sintáctico SyntaxGuard: analiza el código antes de compilar y rechaza
/// cualquier llamada a doc.Delete(...) o Document.Delete(...) de Revit.
/// </summary>
public class SyntaxGuard : CSharpSyntaxWalker
{
    public List<Diagnostic> Diagnostics { get; } = new();

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check for doc.Delete(...) or Document.Delete(...)
        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName == "Delete")
            {
                var target = memberAccess.Expression.ToString();
                if (target == "doc" || target == "Document")
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RB001",
                            "Forbidden API call",
                            "Llamar a doc.Delete() o Document.Delete() no está permitido.",
                            "Security",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        node.GetLocation());
                    Diagnostics.Add(diagnostic);
                }
            }
        }
        
        base.VisitInvocationExpression(node);
    }
}
