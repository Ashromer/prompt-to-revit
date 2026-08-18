using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using RevitBridge.Core.Compiler;
using Xunit;

namespace RevitBridge.Tests.Core;

public class CompilerTests
{
    [Fact]
    public void SyntaxGuard_RejectsCodeWithDocDelete()
    {
        // Arrange
        var sourceCode = @"
        public class MyMacro
        {
            public void Execute(object doc)
            {
                var id = 123;
                doc.Delete(id);
            }
        }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var guard = new SyntaxGuard();

        // Act
        guard.Visit(syntaxTree.GetRoot());

        // Assert
        Assert.NotEmpty(guard.Diagnostics);
        Assert.Contains(guard.Diagnostics, d => d.Id == "RB001");
    }

    [Fact]
    public void SyntaxGuard_ApprovesCleanCode()
    {
        // Arrange
        var sourceCode = @"
        public class MyMacro
        {
            public void Execute(object doc)
            {
                var id = 123;
                // do nothing
            }
        }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var guard = new SyntaxGuard();

        // Act
        guard.Visit(syntaxTree.GetRoot());

        // Assert
        Assert.Empty(guard.Diagnostics);
    }

    [Fact]
    public void RoslynCompiler_CompilesValidCSharpCode()
    {
        // Arrange
        var sourceCode = @"
        using System;
        public class MyMacro
        {
            public void Execute()
            {
                var x = 1 + 1;
            }
        }";
        var compiler = new RoslynCompiler();

        // Act
        var result = compiler.Compile(sourceCode);

        // Assert
        Assert.True(result.Success, "Compilation should succeed for valid code.");
        Assert.NotNull(result.Assembly);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    [Fact]
    public void RoslynCompiler_FailsGracefullyOnSyntaxErrors()
    {
        // Arrange
        var sourceCode = @"
        public class MyMacro
        {
            public void Execute()
            {
                var x = 1 + 1 // Missing semicolon
            }
        }";
        var compiler = new RoslynCompiler();

        // Act
        var result = compiler.Compile(sourceCode);

        // Assert
        Assert.False(result.Success, "Compilation should fail for syntax errors.");
        Assert.Null(result.Assembly);
        Assert.NotEmpty(result.Diagnostics);
    }
}
