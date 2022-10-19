using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LoggingCommon
{
    public class ContextWrapper
    {
        private readonly bool UseGenContext;
        private readonly GeneratorExecutionContext GenContext;
        private readonly SourceProductionContext ProdContext;
        public readonly Compilation Compilation;
        private readonly Location DefLocation;
        private readonly INamedTypeSymbol StrTypeSymbol;
        public readonly object UserData;

        public ContextWrapper(GeneratorExecutionContext gencontext)
        {
            UseGenContext = true;
            ProdContext = default;
            GenContext = gencontext;
            Compilation = gencontext.Compilation;
            DefLocation = Compilation.SyntaxTrees.First().GetRoot().GetLocation();
            StrTypeSymbol = Compilation.GetSpecialType(SpecialType.System_String);
            UserData = gencontext.SyntaxReceiver;
        }

        public ContextWrapper(SourceProductionContext prodContext, Compilation compilation, object userData)
        {
            UseGenContext = false;
            ProdContext = prodContext;
            GenContext = default;
            Compilation = compilation;
            DefLocation = Compilation.SyntaxTrees.First().GetRoot().GetLocation();
            StrTypeSymbol = Compilation.GetSpecialType(SpecialType.System_String);
            UserData = userData;
        }

        public void ReportDiagnostic(Diagnostic create)
        {
            if (UseGenContext)
                GenContext.ReportDiagnostic(create);
            else
                ProdContext.ReportDiagnostic(create);
        }

        public Location DefaultLocation()
        {
            return DefLocation;
        }

        public ITypeSymbol GetStringTypeSymbol()
        {
            return StrTypeSymbol;
        }

        public void AddSource(string hint, SourceText src)
        {
            if (UseGenContext)
                GenContext.AddSource(hint, src);
            else
                ProdContext.AddSource(hint, src);
        }

        public void ThrowIfCancellationRequested()
        {
            if (UseGenContext)
                GenContext.CancellationToken.ThrowIfCancellationRequested();
            else
                ProdContext.CancellationToken.ThrowIfCancellationRequested();
        }
    }
}
