using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodebaseAnalysisLib
{
    public class CodebaseAnalyzer
    {
        public static async Task<CodebaseInfo> GetCodebaseInfo(string solutionPath, string userInput)
        {
            userInput ??= string.Empty;
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var codebaseInfo = new CodebaseInfo();

            var projects = solution.Projects.ToList();

            var tasks = projects.Select(async project =>
            {
                var updatedProject = project.WithAllSourceFiles();
                var compilation = await updatedProject.GetCompilationAsync();
                var excludes = new List<string> { @"\obj\", @"\platforms\" };
                var excludedRegex = new Regex(string.Join("|", excludes.Select(Regex.Escape)), RegexOptions.IgnoreCase);

                foreach (var syntaxTree in compilation.SyntaxTrees.Where(tree => !excludedRegex.IsMatch(tree.FilePath)))
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    //var diagnostics = await GetDiagnosticsAsync(compilation);
                    //codebaseInfo.DiagnosticsByProject[project.AssemblyName] = diagnostics.ToList();

                    var root = await syntaxTree.GetRootAsync();
                    var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var classDeclaration in classDeclarations)
                    {
                        var className = classDeclaration.Identifier.ValueText;
                        ExtractClassMembers(classDeclaration, semanticModel, codebaseInfo, className);
                        codebaseInfo.ProjectNames.Add(project.AssemblyName);
                        if (!codebaseInfo.ClassNames.ContainsKey(project.AssemblyName))
                        {
                            codebaseInfo.ClassNames.Add(project.AssemblyName, new List<string>());
                        }
                        codebaseInfo.ClassNames[project.AssemblyName].Add(className);
                        // Find base types and implemented interfaces
                        var baseTypesAndInterfaces = new List<string>();
                        foreach (var baseType in classDeclaration.BaseList?.Types ?? Enumerable.Empty<BaseTypeSyntax>())
                        {
                            var typeSymbol = semanticModel.GetTypeInfo(baseType.Type).Type;
                            if (typeSymbol != null)
                            {
                                baseTypesAndInterfaces.Add(typeSymbol.Name);
                            }
                        }
                        if (baseTypesAndInterfaces.Count > 0)
                        {
                            codebaseInfo.ClassRelationships[className] = baseTypesAndInterfaces;
                        }

                        if (userInput.Contains(className))
                        {
                            if (userInput.Contains(className))
                            {
                                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                                var classSyntax = classSymbol.DeclaringSyntaxReferences.First().GetSyntax();
                                codebaseInfo.FullCodeOfClass[className] = classSyntax.GetText().ToString();
                            }
                        }

                        var classMethods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
                        var methodNames = new List<string>();
                        foreach (var methodDeclaration in classMethods)
                        {
                            var methodName = methodDeclaration.Identifier.ValueText;
                            methodNames.Add(methodName);
                            if (userInput.Contains(methodName))
                            {
                                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                                var methodSyntax = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax();
                                codebaseInfo.FullCodeOfMethods[methodName] = methodSyntax.GetText().ToString();
                            }
                        }
                        if (!codebaseInfo.ClassMethods.ContainsKey(className))
                        {
                            codebaseInfo.ClassMethods.Add(className, methodNames);
                        }
                    }
                }
            }).ToArray();
            await Task.WhenAll(tasks);

            return codebaseInfo;
        }

        private static void ExtractClassMembers(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, CodebaseInfo codebaseInfo, string className)
        {
            var properties = classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
            var fields = classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            var events = classDeclaration.DescendantNodes().OfType<EventDeclarationSyntax>().ToList();
            var attributes = classDeclaration.AttributeLists.SelectMany(al => al.Attributes).ToList();

            var propertyNames = properties.Select(p => p.Identifier.ValueText).ToList();
            var fieldNames = fields.SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.ValueText)).ToList();
            var eventNames = events.Select(e => e.Identifier.ValueText).ToList();
            var attributeNames = attributes.Select(a => semanticModel.GetTypeInfo(a).Type.Name).ToList();

            lock (codebaseInfo)
            {
                codebaseInfo.ClassProperties[className] = propertyNames;
                codebaseInfo.ClassFields[className] = fieldNames;
                codebaseInfo.ClassEvents[className] = eventNames;
                codebaseInfo.ClassAttributes[className] = attributeNames;
            }
        }

        private static async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(Compilation compilation)
        {
            var analyzers = GetAllAnalyzers();
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
            var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            return diagnostics;
        }

        private static ImmutableArray<DiagnosticAnalyzer> GetAllAnalyzers()
        {
            var assemblyLocation = typeof(CodebaseAnalyzer).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            var analyzerAssemblyPath = Path.Combine(assemblyDirectory, "Analyzers", "Microsoft.CodeAnalysis.NetAnalyzers.dll");

            var analyzerAssembly = Assembly.LoadFrom(analyzerAssemblyPath);
            var analyzerTypes = analyzerAssembly.GetTypes().Where(type => type.IsSubclassOf(typeof(DiagnosticAnalyzer)) && !type.IsAbstract);
            var analyzers = analyzerTypes.Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)).ToImmutableArray();
            return analyzers;
        }

    }



    static class ProjectExtensions
    {
        public static Project AddDocuments(this Project project, IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                project = project.AddDocument(file, File.ReadAllText(file)).Project;
            }
            return project;
        }

        private static IEnumerable<string> GetAllSourceFiles(string directoryPath) => Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);


        public static Project WithAllSourceFiles(this Project project)
        {
            string projectDirectory = Directory.GetParent(project.FilePath).FullName;
            var files = GetAllSourceFiles(projectDirectory);
            var newProject = project.AddDocuments(files);
            return newProject;
        }
    }
}

