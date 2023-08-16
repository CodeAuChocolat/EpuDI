using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace EpuDI.Generators
{  
    [Generator(LanguageNames.CSharp)]
    public class EpuDiGenerator : IIncrementalGenerator
    {
        const string Namespace = "EpuDI";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            AddAttributesSource(context);

            //INamedTypeSymbol attributeSymbol = context.CompilationProvider.Compilation.GetTypeByMetadataName("AutoNotify.AutoNotifyAttribute");

            var compositionClassDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(IsClassWithAttributes, TransformCompositionClass)
                .Where(classDeclaration => classDeclaration is not null);

            var factoriesClassDeclaration = context.SyntaxProvider
                .CreateSyntaxProvider(IsClassWithAttributes, TransformFactoriesClass)
                .Where(it => it is not null);

            var combined = compositionClassDeclarations.Combine(factoriesClassDeclaration.Collect());

            context.RegisterSourceOutput(combined,(spc, s) => CreateCompositionOutput(spc, s.Left, s.Right));
        }

        static bool IsClassWithAttributes(SyntaxNode node, CancellationToken ct)
            => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0;

        static ClassInfo TransformCompositionClass(GeneratorSyntaxContext context, CancellationToken ct)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            
            foreach(var attributeList in classDeclaration.AttributeLists)
            {
                foreach(var attribute in attributeList.Attributes)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
                    if(symbol is null) continue;

                    var attributeName = symbol.ContainingType.ToDisplayString();
                    if(attributeName == "EpuDI.CompositionAttribute")
                    {
                        var containingNamespace = context.SemanticModel
                            .GetDeclaredSymbol(classDeclaration)
                            .ContainingNamespace
                            .ToDisplayString();
                        
                        var classInfo = new ClassInfo 
                        { 
                            Namespace = containingNamespace,
                            Declaration = classDeclaration 
                        };

                        foreach(var member in classDeclaration.Members)
                        {
                            if(member is FieldDeclarationSyntax fieldDeclaration && IsFactoriesField(fieldDeclaration))
                            {
                                classInfo.Fields.Add(fieldDeclaration);
                            }
                        }
                       return classInfo;
                    }
                }
            }

            return null;
        }

        static FactoriesInfo TransformFactoriesClass(GeneratorSyntaxContext context, CancellationToken ct)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            foreach(var attributeList in classDeclaration.AttributeLists)
            {
                foreach(var attribute in attributeList.Attributes)
                {
                    var name = attribute.Name as IdentifierNameSyntax;
                    if(name is null || name.Identifier.Text != "Factories") return null;
                }
            }

            var result = new FactoriesInfo();

            foreach(var member in classDeclaration.Members)
            {
                if(member is MethodDeclarationSyntax methodDeclaration)
                {
                    foreach(var attributeList in methodDeclaration.AttributeLists)
                    {
                        foreach(var attribute in attributeList.Attributes)
                        {
                            var name = attribute.Name as IdentifierNameSyntax;
                            if(name is not null && name.Identifier.Text == "Transient")
                            {
                                var typeInfo = context.SemanticModel.GetTypeInfo(methodDeclaration.ReturnType, ct);
                                result.Methods.Add(new TransientFactoryMethodInfo
                                {
                                    Declaration = methodDeclaration,
                                    TypeInfo = typeInfo
                                });
                            }
                            else if(name is not null && name.Identifier.Text == "Scoped")
                            {
                                var typeInfo = context.SemanticModel.GetTypeInfo(methodDeclaration.ReturnType, ct);
                                result.Methods.Add(new ScopedFactoryMethodInfo
                                {
                                    Declaration = methodDeclaration,
                                    TypeInfo = typeInfo
                                });
                            }
                            else if(name is not null &&  name.Identifier.Text == "Singleton")
                            {
                                var typeInfo = context.SemanticModel.GetTypeInfo(methodDeclaration.ReturnType, ct);
                                result.Methods.Add(new SingletonFactoryMethodInfo
                                {
                                    Declaration = methodDeclaration,
                                    TypeInfo = typeInfo
                                });
                            }
                        }
                    }
                    
                }
            }

            return result;
        }

        private static bool IsFactoriesField(FieldDeclarationSyntax fieldDeclaration)
        {
            foreach(var attributeList in fieldDeclaration.AttributeLists)
            {
                foreach(var attribute in attributeList.Attributes)
                {
                    var name = attribute.Name as IdentifierNameSyntax;
                    if(name is not null && name.Identifier.Text == "Factories") return true;
                }
            }

            return false;
        }

        private void CreateCompositionOutput(SourceProductionContext context, ClassInfo classInfo, ImmutableArray<FactoriesInfo> factoryClasses)
        {
            var classDeclaration = classInfo.Declaration;
            var typeName = classDeclaration.Identifier.ValueText;
            var typeNamespace = classInfo.Namespace;
            context.AddSource($"{typeName}.g.cs", 
                $$"""
                // <auto-generated/>
                #nullable enable
                namespace {{typeNamespace}};

                partial class {{typeName}}
                {
                {{string.Join("\r\n\r\n", factoryClasses.FirstOrDefault()?.Methods?.Select(m => m.GeneratCode()))}}
                }
                """
            );
        }

        static void AddAttributesSource(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(c =>
            {
                c.AddSource("EpuDi.g.cs", 
                    $$"""
                    // <auto-generated/>
                    #nullable enable
                    namespace {{Namespace}};
                    using System;
                    using System.Collections.Generic;

                    public class CompositionAttribute : global::System.Attribute {}
                    public class FactoriesAttribute : global::System.Attribute {}
                    public class TransientAttribute : global::System.Attribute {}
                    public class ScopedAttribute : global::System.Attribute {}
                    public class SingletonAttribute : global::System.Attribute {}

                    public abstract class ServiceContainerBase : global::System.IDisposable
                    {
                        private object _initLock = new ();
                        private readonly Stack<IDisposable> _disposables = new Stack<IDisposable>();
                        private bool _disposedValue;

                        protected T RegisterDisposable<T>(T value)
                        {
                            if(value is null) return value;
                            if(value is IDisposable d) {
                                _disposables.Push(d);
                            }
                            return value;
                        }

                        protected virtual void Dispose(bool disposing)
                        {
                            if (!_disposedValue) {
                                if (disposing) {
                                    while(_disposables.Count > 0) {
                                        _disposables.Pop().Dispose();
                                    }
                                }
                                _disposedValue = true;
                            }
                        }

                        public void Dispose()
                        {
                            Dispose(disposing: true);
                            GC.SuppressFinalize(this);
                        }

                        protected T EnsureInitialized<T>(ref T? target, Func<T> factory) where T : class
                        {
                            T? value = Volatile.Read(ref target);
                            if(value is not null) return value;

                            lock(_initLock)
                            {
                                if(target is not null) return target;

                                value = factory();
                                RegisterDisposable<T>(value);
                                return value;
                            }
                        }

                        protected T EnsureInitialized<T>(ref T? target, ref bool initialized, Func<T> valueFactory)
                            => LazyInitializer.EnsureInitialized(ref target, ref initialized, ref _initLock, valueFactory)!;
                    }
                    """);
            });
        }

        class GenerationInfo
        {
            public List<ClassInfo> Classes { get; set; } = new List<ClassInfo>();
        }

        class ClassInfo
        {
            public string Namespace { get; set; }
            public ClassDeclarationSyntax Declaration { get; set; }
            public List<FieldDeclarationSyntax> Fields { get; set; } = new List<FieldDeclarationSyntax>();
        }

        class FactoriesInfo
        {
            public List<IFactoryMethodInfo> Methods { get; set; } = new List<IFactoryMethodInfo>();
        }

        public interface IFactoryMethodInfo
        {
            string GeneratCode();
        }

        class TransientFactoryMethodInfo : IFactoryMethodInfo
        {
            public MethodDeclarationSyntax Declaration { get; set; }
            public TypeInfo TypeInfo { get; set; }

            public string GeneratCode()
            {
                var name = Declaration.Identifier.Text;
                var returnType = TypeInfo.Type.ToDisplayString();

                return $"    public {returnType} {name} => RegisterDisposable(_factories.{name}(this));";
            }
        }

        class ScopedFactoryMethodInfo : IFactoryMethodInfo
        {
            public MethodDeclarationSyntax Declaration { get; set; }
            public TypeInfo TypeInfo { get; set; }

            public string GeneratCode()
            {
                var name = Declaration.Identifier.Text;
                var returnType = TypeInfo.Type.ToDisplayString();
                
                return $"""
                    private {returnType}? _{name};
                    private bool _{name}Initialized;
                    public {returnType} {name} => _{name} ??= EnsureInitialized(ref _{name}, ref _{name}Initialized, () => _factories.{name}(this));
                """;
            }
        }

        class SingletonFactoryMethodInfo : IFactoryMethodInfo
        {
            public MethodDeclarationSyntax Declaration { get; set; }
            public TypeInfo TypeInfo { get; set; }

            public string GeneratCode()
            {
                var name = Declaration.Identifier.Text;
                var returnType = TypeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return $$"""
                    private {{returnType}}? _{{name}};
                    private bool _{{name}}Initialized;
                    public {{returnType}} {{name}}
                    {
                        get
                        {
                            if(_parent is null) return _{{name}} ??= EnsureInitialized(ref _{{name}}, ref _{{name}}Initialized, () => _factories.{{name}}(this));
                            return _parent.{{name}};
                        }
                    }
                """;
            }
        }
    }
}
