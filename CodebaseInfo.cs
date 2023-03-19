using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodebaseAnalysisLib
{
    public class CodebaseInfo
    {
        public List<string> ClassNames { get; set; } = new List<string>();
        public Dictionary<string, List<string>> ClassMethods { get; set; } = new Dictionary<string, List<string>>();
        public Dictionary<string, string> FullCodeOfClass { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> FullCodeOfMethods { get; set; } = new Dictionary<string, string>();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var className in ClassNames)
            {
                sb.AppendLine($"Class: {className}");
                sb.AppendLine("Methods:");

                foreach (var methodName in ClassMethods[className])
                {
                    sb.AppendLine($"- {methodName}");
                }

                if (FullCodeOfClass.ContainsKey(className))
                {
                    sb.AppendLine($"Full code of class '{className}':");
                    sb.AppendLine(FullCodeOfClass[className]);
                }

                foreach (var methodName in ClassMethods[className])
                {
                    if (FullCodeOfMethods.ContainsKey(methodName))
                    {
                        sb.AppendLine($"Full code of method '{methodName}':");
                        sb.AppendLine(FullCodeOfMethods[methodName]);
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public class CodebaseAnalyzer
    {
        public async Task<CodebaseInfo> GetCodebaseInfo(string solutionPath, string userInput)
        {
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            var codebaseInfo = new CodebaseInfo();

            foreach (var project in solution.Projects)
            {
                var updatedProject = project.WithAllSourceFiles();
                var compilation = await updatedProject.GetCompilationAsync();
                var excludes = new List<string> { @"\obj\", @"\platforms\" };
                var excludedRegex = new Regex(string.Join("|", excludes.Select(Regex.Escape)), RegexOptions.IgnoreCase);


                foreach (var syntaxTree in compilation.SyntaxTrees.Where(tree => !excludedRegex.IsMatch(tree.FilePath)))
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync();

                    var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var classDeclaration in classDeclarations)
                    {
                        var className = classDeclaration.Identifier.ValueText;
                        codebaseInfo.ClassNames.Add(className);
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
                            codebaseInfo.ClassMethods.Add(className, methodNames);
                    }
                }
            }

            return codebaseInfo;
        }

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

    private static IEnumerable<string> GetAllSourceFiles(string directoryPath)
    {
        var res = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

        return res;
    }


    public static Project WithAllSourceFiles(this Project project)
    {
        string projectDirectory = Directory.GetParent(project.FilePath).FullName;
        var files = GetAllSourceFiles(projectDirectory);
        var newProject = project.AddDocuments(files);
        return newProject;
    }
}

