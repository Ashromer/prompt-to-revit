using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitBridge.Core.Compiler;

/// <summary>
/// F1.1 Filtro sintáctico (DOCUMENTACION.md §5.A.3): rechaza namespaces y llamadas prohibidas
/// ANTES de compilar. Puramente sintáctico (sin semantic model): barato, corre antes del <c>Emit</c>.
/// Las llamadas de C.9 (Delete/SaveAs/Close) y la vía de reflexión se bloquean por NOMBRE DE MÉTODO,
/// no por el nombre de la variable receptora -- la versión anterior solo miraba si el texto del
/// receptor era literalmente "doc" o "Document", y renombrar la variable la esquivaba
/// (<c>var d = doc; d.Delete(id);</c> pasaba el filtro). Hallazgo de auditoría 2026-08-18.
/// Sigue sin ser sonido contra ofuscación arbitraria (p. ej. <c>global::System.IO...</c>), pero
/// cubre los vectores descritos en la documentación: los namespaces prohibidos y el patrón
/// GetMethod/Invoke de reflexión.
/// </summary>
public class SyntaxGuard : CSharpSyntaxWalker
{
    private static readonly string[] NamespacesProhibidos =
    {
        "System.IO", "System.Net", "System.Diagnostics", "System.Reflection"
    };

    private static readonly HashSet<string> MetodosProhibidos = new(StringComparer.Ordinal)
    {
        // §5.A.3 / C.9: borrado, guardado y cierre de documento.
        "Delete", "SaveAs", "Close",
        // Vía de reflexión para rodear lo anterior sin nombrarlo directamente.
        "GetMethod", "GetMethods", "GetMember", "GetMembers",
        "GetRuntimeMethod", "GetRuntimeMethods", "InvokeMember", "CreateDelegate"
    };

    public List<Diagnostic> Diagnostics { get; } = new();

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var nombre = node.Name?.ToString() ?? "";
        if (EsNamespaceProhibido(nombre))
        {
            Reportar(node.GetLocation(), $"El using '{nombre}' no está permitido en código generado.");
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var texto = node.ToString();
        if (EsNamespaceProhibido(texto))
        {
            Reportar(node.GetLocation(), $"La referencia '{texto}' a un namespace prohibido no está permitida.");
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;

            if (MetodosProhibidos.Contains(methodName))
            {
                Reportar(node.GetLocation(),
                    $"Llamar a '{methodName}(...)' no está permitido en código generado (DOCUMENTACION.md §5.A.3).");
            }

            if (methodName == "Exit")
            {
                var receptor = memberAccess.Expression.ToString();
                if (receptor == "Environment" || receptor.EndsWith(".Environment", StringComparison.Ordinal))
                {
                    Reportar(node.GetLocation(), "Llamar a Environment.Exit(...) no está permitido.");
                }
            }
        }

        base.VisitInvocationExpression(node);
    }

    private static bool EsNamespaceProhibido(string texto) =>
        NamespacesProhibidos.Any(ns => texto == ns || texto.StartsWith(ns + ".", StringComparison.Ordinal));

    private void Reportar(Location location, string mensaje)
    {
        Diagnostics.Add(Diagnostic.Create(
            new DiagnosticDescriptor(
                "RB001",
                "Forbidden API call",
                mensaje,
                "Security",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            location));
    }
}
