using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator {
    [Generator]
    public class Generator : ISourceGenerator {
        
        public void Initialize(GeneratorInitializationContext context) {
            Debugger.Launch();
            Debug.WriteLine("Debugger launched");
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            foreach (ClassDeclarationSyntax nodeClass in GeneratorContext.NodeTypes) {
                var namespaceName = (nodeClass.Parent as NamespaceDeclarationSyntax).Name.ToString();
                var className = nodeClass.Identifier.ToString();
                var usingStatements = @"using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;";
                
                StringBuilder portDeclarations = new StringBuilder();
                StringBuilder portInitializers = new StringBuilder();
                StringBuilder serialization = new StringBuilder();
                StringBuilder deserialization = new StringBuilder();
                
                // Find all fields with [In] and [Out] attributes
                var fieldDeclarations = nodeClass.Members.OfType<FieldDeclarationSyntax>().ToList();
                var fields = new List<GeneratedField>();
                fields.AddRange(fieldDeclarations.Where(f => f.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "In"))).Select(field => new GeneratedField(field, GeneratedFieldKind.InputPort)));
                fields.AddRange(fieldDeclarations.Where(f => f.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "Out"))).Select(field => new GeneratedField(field, GeneratedFieldKind.OutputPort)));
                fields.AddRange(fieldDeclarations.Where(f => f.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "Setting"))).Select(field => new GeneratedField(field, GeneratedFieldKind.Setting)));

                /*foreach (GeneratedField field in fields) {
                    if (field.Kind != GeneratedFieldKind.Setting) {
                        portDeclarations.AppendLine(field.GetPortPropertyDeclaration());
                        portInitializers.AppendLine(field.GetPortCtorDeclaration());
                    }
                }

                string sourceCode = string.Format(Templates.ClassTemplate, usingStatements, namespaceName, className, portDeclarations, portInitializers);
                context.AddSource($"{className}.gen.cs", SourceText.From(sourceCode, Encoding.UTF8));*/
            }
        }
    }

    public class SyntaxReceiver : ISyntaxReceiver {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            if (syntaxNode is ClassDeclarationSyntax cds && cds.BaseList != null 
             && cds.BaseList.Types.Any(t => t.ToString() == "RuntimeNode")
             && cds.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "GenerateNodeImplementation"))
            ) {
                GeneratorContext.NodeTypes.Add(cds);
            }
            
            if (syntaxNode is EnumDeclarationSyntax eds) {
                GeneratorContext.EnumTypes.Add(eds);
            }
        }
    }
}