using System.Collections.Immutable;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace HotChocolate.Types.Analyzers.Generators;

public interface ISyntaxGenerator
{
    void Generate(
        SourceProductionContext context,
        string assemblyName,
        ImmutableArray<SyntaxInfo> syntaxInfos,
        Action<string, SourceText> addSource);
}
