using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RevitBridge.Core.Compiler;

public class CompilationResult
{
    public bool Success { get; set; }
    public IEnumerable<Diagnostic> Diagnostics { get; set; } = Enumerable.Empty<Diagnostic>();
    public Assembly? Assembly { get; set; }
}

/// <summary>
/// F1.2 y F1.4: RoslynCompiler en RevitBridge.Core. Compila a memoria usando un AssemblyLoadContext
/// que sea colectible.
/// </summary>
public class RoslynCompiler
{
    private static readonly ConcurrentDictionary<string, Assembly> _cache = new();

    public CompilationResult Compile(string sourceCode)
    {
        var hash = GetHash(sourceCode);
        if (_cache.TryGetValue(hash, out var cachedAssembly))
        {
            return new CompilationResult { Success = true, Assembly = cachedAssembly };
        }

        // F1.1 SyntaxGuard
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var guard = new SyntaxGuard();
        guard.Visit(syntaxTree.GetRoot());
        if (guard.Diagnostics.Any())
        {
            return new CompilationResult { Success = false, Diagnostics = guard.Diagnostics };
        }

        var references = new List<MetadataReference>();
        
        // Resolvemos referencias iterando sobre AppDomain.CurrentDomain.GetAssemblies() (F1.2)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // F1.4 Referencia explícita: asegurar que el compilador incluye explícitamente el ensamblado RevitBridge.Utils
        var utilsAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RevitBridge.Utils");
        if (utilsAssembly != null && !string.IsNullOrEmpty(utilsAssembly.Location))
        {
            if (!references.Any(r => r.Display == utilsAssembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(utilsAssembly.Location));
            }
        }

        var compilation = CSharpCompilation.Create(
            $"DynamicCode_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            return new CompilationResult { Success = false, Diagnostics = result.Diagnostics };
        }

        ms.Seek(0, SeekOrigin.Begin);
        
        // Compile to memory using a collectible AssemblyLoadContext (F1.2)
        var loadContext = new AssemblyLoadContext("RevitBridgeCollectible", isCollectible: true);
        var compiledAssembly = loadContext.LoadFromStream(ms);
        
        _cache.TryAdd(hash, compiledAssembly);

        return new CompilationResult { Success = true, Assembly = compiledAssembly };
    }

    private static string GetHash(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(bytes);
    }
}
