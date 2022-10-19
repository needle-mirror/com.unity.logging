using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Logging;

public readonly struct CustomMirrorStruct : IEquatable<CustomMirrorStruct>
{
    public const string InterfaceName = "ILoggableMirrorStruct";
    public const string HeaderTypeName = "MirrorStructHeader";

    public enum ErrorStatus
    {
        Undefined = 0,
        NoError,
        MustBeInStruct,
        MustHaveOnlyOneInterfacePerStruct,
        MustBePartialStruct,
        TypeFieldShouldBeFirstField,
        MustHaveImplicitConversion
    }

    public readonly ErrorStatus Status;
    public readonly Location ErrorLocation;
    public readonly StructDeclarationSyntax WrapperStructExpression;
    public readonly INamedTypeSymbol WrapperStructTypeInfo;
    public readonly TypeSyntax OriginalStructType;
    public readonly ulong TypeId;

    public readonly string ContainingNamespace;
    public readonly TypeInfo OriginalStructTypeInfo;

    private static uint localId = uint.MaxValue / 2;

    public bool IsCreated => Status != ErrorStatus.Undefined;

    public CustomMirrorStruct(BaseListSyntax syntaxContextNode, SemanticModel semanticModel, ulong assemblyHash)
    {
        ContainingNamespace = "";
        TypeId = Common.CreateStructTypeId(assemblyHash, ++localId);

        // 0. implements ILoggableMirrorStruct<T>
        // 1. must be partial
        // 2. first field must be long TypeId
        // 3. must have implicit operator from target class/struct

        ErrorLocation = Location.None;
        OriginalStructType = null;
        OriginalStructTypeInfo = default;
        WrapperStructTypeInfo = default;
        Status = ErrorStatus.NoError;

        WrapperStructExpression = syntaxContextNode.Parent as StructDeclarationSyntax;
        if (WrapperStructExpression == null)
        {
            if (syntaxContextNode.Parent != null)
                ErrorLocation = syntaxContextNode.Parent.GetLocation();

            Status = ErrorStatus.MustBeInStruct;
            return;
        }

        if (IsPartialStruct(WrapperStructExpression) == false)
        {
            ErrorLocation = WrapperStructExpression.GetLocation();
            Status = ErrorStatus.MustBePartialStruct;
            return;
        }

        var count = 0;
        foreach (var type in syntaxContextNode.Types)
        {
            var gen = ExtractGenericSyntax(type);
            if (gen != null && gen.Identifier.Text == InterfaceName)
            {
                OriginalStructType = gen.TypeArgumentList.Arguments[0];
                ++count;
                if (count > 1)
                {
                    Status = ErrorStatus.MustHaveOnlyOneInterfacePerStruct;
                    ErrorLocation = gen.GetLocation();
                }
            }
        }

        if (Status != ErrorStatus.NoError)
            return;

        Status = ErrorStatus.TypeFieldShouldBeFirstField;
        ErrorLocation = WrapperStructExpression.GetLocation();

        foreach (var node in WrapperStructExpression.Members)
        {
            if (node is PropertyDeclarationSyntax)
            {
                Status = ErrorStatus.TypeFieldShouldBeFirstField;
                ErrorLocation = node.GetLocation();
                return;
            }

            if (node is FieldDeclarationSyntax fd)
            {
                if (fd.Declaration.Type.ToString() != "ulong" && fd.Declaration.Type.ToString() != HeaderTypeName)
                {
                    ErrorLocation = fd.Declaration.Type.GetLocation();
                }
                else
                {
                    Status = ErrorStatus.NoError;
                    ErrorLocation = Location.None;

                    break;
                }
            }
        }

        if (Status != ErrorStatus.NoError)
            return;

        if (HasImplicitOp(WrapperStructExpression, WrapperStructureName, OriginalStructureName) == false)
        {
            Status = ErrorStatus.MustHaveImplicitConversion;
            ErrorLocation = WrapperStructExpression.GetLocation();
        }

        if (Status == ErrorStatus.NoError && OriginalStructType != null)
        {
            OriginalStructTypeInfo = semanticModel.GetTypeInfo(OriginalStructType);
            WrapperStructTypeInfo = semanticModel.GetDeclaredSymbol(WrapperStructExpression);
            if (WrapperStructTypeInfo != null)
                ContainingNamespace = Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(WrapperStructTypeInfo.ContainingNamespace);
        }
    }

    public string WrapperStructureName => WrapperStructExpression.Identifier.Text;
    public string OriginalStructureName => OriginalStructType.ToString();

    private static bool HasImplicitOp(StructDeclarationSyntax structExpr, string wrapperType, string originalType)
    {
        if (wrapperType == originalType) return true; // no need - same type

        foreach (var node in structExpr.Members)
        {
            if (node is ConversionOperatorDeclarationSyntax implicitOp)
            {
                if (implicitOp.ImplicitOrExplicitKeyword.Kind() == SyntaxKind.ImplicitKeyword)
                {
                    var typeSyntax = implicitOp.ParameterList.Parameters[0].Type;

                    if (typeSyntax != null && implicitOp.ParameterList.Parameters.Count == 1 &&
                        typeSyntax.ToString() == originalType &&
                        implicitOp.Type.ToString() == wrapperType)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsPartialStruct(StructDeclarationSyntax structExpression)
    {
        foreach (var mod in structExpression.Modifiers)
            if (mod.Kind() == SyntaxKind.PartialKeyword)
                return true;
        return false;
    }

    public static GenericNameSyntax ExtractGenericSyntax(BaseTypeSyntax syntax)
    {
        return syntax.Type switch
        {
            QualifiedNameSyntax qn when qn.Right is GenericNameSyntax genInside => genInside,
            GenericNameSyntax gen => gen,
            _ => null
        };
    }


    (string, string) StatusToErrorString()
    {
        switch (Status)
        {
            case ErrorStatus.MustBeInStruct:
                return ("LMS0001", $"Mirror struct that implements {InterfaceName} must be a struct, not class or record");
            case ErrorStatus.MustHaveOnlyOneInterfacePerStruct:
                return ("LMS0002", $"Mirror struct must implement only one {InterfaceName}");
            case ErrorStatus.MustBePartialStruct:
                return ("LMS0003", $"Mirror struct must be marked as partial");
            case ErrorStatus.TypeFieldShouldBeFirstField:
                return ("LMS0004", $"The first field in the struct should be '{HeaderTypeName}'");
            case ErrorStatus.MustHaveImplicitConversion:
                return ("LMS0005", $"Mirror struct must have implicit constructor from its original structure: public static implicit operator {WrapperStructureName}(in {OriginalStructureName} arg)");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public Diagnostic CreateDiagnostics()
    {
        // Unity shows:
        //  severity, error code, message and location.

        var err = StatusToErrorString();
        var descriptor = new DiagnosticDescriptor(err.Item1, "", err.Item2, "Logging. Source Generator", DiagnosticSeverity.Error, true, "");
        return Diagnostic.Create(descriptor, ErrorLocation);
    }

    public bool Equals(CustomMirrorStruct other)
    {
        return Status == other.Status && Equals(WrapperStructExpression, other.WrapperStructExpression)
                                      && WrapperStructTypeInfo.Equals(other.WrapperStructTypeInfo, SymbolEqualityComparer.Default)
                                      && Equals(OriginalStructType, other.OriginalStructType)
                                      && OriginalStructTypeInfo.Equals(other.OriginalStructTypeInfo);
    }

    public override bool Equals(object obj)
    {
        return obj is CustomMirrorStruct other && Equals(other);
    }

    public override int GetHashCode()
    {
#pragma warning disable RS1024
        unchecked
        {
            var hashCode = (int)Status;
            hashCode = (hashCode * 397) ^ (WrapperStructExpression != null ? WrapperStructExpression.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ WrapperStructTypeInfo.GetHashCode();
            hashCode = (hashCode * 397) ^ (OriginalStructType != null ? OriginalStructType.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ OriginalStructTypeInfo.GetHashCode();

            return hashCode;
        }
#pragma warning restore RS1024
    }
}
