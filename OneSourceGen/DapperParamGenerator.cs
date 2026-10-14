using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Linq;

namespace OneSourceGen
{
    [Generator]
    public class DapperParameterSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(
    context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: static (s, _) => s is ClassDeclarationSyntax,
            transform: static (ctx, _) => (
                classDecl: (ClassDeclarationSyntax)ctx.Node,
                semanticModel: ctx.SemanticModel
            )
        )
        .Where(static t => t.classDecl is not null),
    (SourceProductionContext spc, (ClassDeclarationSyntax classDecl, SemanticModel semanticModel) t) =>
    {
        var (classDecl, semanticModel) = t;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (classSymbol == null) return;

        if (!IsBaseEntity(classSymbol))
            return;

        var className = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var props = classSymbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.SetMethod != null)
            .ToList();

        var sb = new StringBuilder($@"
using Dapper;
using System.Data;

namespace {ns}
{{
    public static partial class {className}DapperExtensions
    {{
        public static DynamicParameters ToDynamicParameters(this {className} entity)
        {{
            var p = new DynamicParameters();
");

        foreach (var prop in props)
        {
            var dbType = GetDbType(prop.Type);
            var size = GetDefaultSize(prop.Type, prop.Name);

            if (prop.Name == "Id") continue;

            if (dbType == "DbType.String" && size > 0)
                sb.AppendLine($@"            p.Add(""@{prop.Name}"", entity.{prop.Name}, {dbType}, size: {size});");
            else
                sb.AppendLine($@"            p.Add(""@{prop.Name}"", entity.{prop.Name}, {dbType});");
        }

        sb.AppendLine(@"
            p.Add(""@Id"", dbType: DbType.Guid, direction: ParameterDirection.Output);
            p.Add(""@Success"", dbType: DbType.Boolean, direction: ParameterDirection.Output);
            p.Add(""@Message"", dbType: DbType.String, size: 1000, direction: ParameterDirection.Output);
            return p;
        }
    }
}
");

        spc.AddSource($"{className}DapperExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
            );
        }

        private bool IsBaseEntity(INamedTypeSymbol symbol)
        {
            var b = symbol.BaseType;
            while (b != null)
            {
                if (b.Name.StartsWith("BaseEntity"))
                    return true;
                b = b.BaseType;
            }
            return false;
        }

        private string GetDbType(ITypeSymbol t)
        {
            var name = t.Name;
            return name switch
            {
                "String" => "DbType.String",
                "Int32" => "DbType.Int32",
                "Int64" => "DbType.Int64",
                "Guid" => "DbType.Guid",
                "Boolean" => "DbType.Boolean",
                "Decimal" => "DbType.Decimal",
                "Double" => "DbType.Double",
                "DateTime" => "DbType.DateTime",
                _ => "DbType.Object"
            };
        }

        private int GetDefaultSize(ITypeSymbol type, string propName)
        {
            // حجم افتراضي ذكي
            if (type.Name == "String")
            {
                if (propName.ToLower().Contains("name")) return 200;
                if (propName.ToLower().Contains("description")) return 500;
                if (propName.ToLower().Contains("barcode")) return 100;
                if (propName.ToLower().Contains("message")) return 1000;
                return 255;
            }
            return 0;
        }
    }
}