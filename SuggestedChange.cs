using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace CodebaseAnalysisLib
{
    public class SuggestedChange
    {
        public string FileName { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string ChangeType { get; set; }
        public string OriginalCode { get; set; }
        public string NewCode { get; set; }

        public static async Task ApplySuggestedChanges(List<SuggestedChange> changes)
        {
            if (changes == null || !changes.Any())
            {
                throw new ArgumentException("Changes list is empty or null.");
            }

            foreach (var change in changes)
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), change.FileName);

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File '{change.FileName}' not found.");
                }

                string fileContent = File.ReadAllText(filePath);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                var workspace = new AdhocWorkspace();
                var projectId = ProjectId.CreateNewId();
                var documentId = DocumentId.CreateNewId(projectId);
                workspace.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp));
                workspace.AddDocument(DocumentInfo.Create(documentId, "MyDocument", loader: TextLoader.From(TextAndVersion.Create(SourceText.From(fileContent), VersionStamp.Default))));
                var document = workspace.CurrentSolution.GetDocument(documentId);

                try
                {
                    var editor = await DocumentEditor.CreateAsync(document);

                    if (string.IsNullOrWhiteSpace(change.ChangeType))
                    {
                        throw new ArgumentException("Change type is null, empty, or consists only of whitespace.");
                    }

                    if (change.ChangeType == "single" || change.ChangeType == "multi")
                    {
                        if (string.IsNullOrWhiteSpace(change.NewCode))
                        {
                            throw new ArgumentException("New code is null, empty, or consists only of whitespace.");
                        }

                        var linesToReplace = root.DescendantTokens().Where(t => t.GetLocation().GetLineSpan().StartLinePosition.Line >= change.StartLine - 1 && t.GetLocation().GetLineSpan().EndLinePosition.Line <= change.EndLine - 1);

                        if (!linesToReplace.Any())
                        {
                            throw new InvalidOperationException("No lines to replace found.");
                        }

                        foreach (var token in linesToReplace)
                        {
                            var newTriviaList = SyntaxFactory.ParseLeadingTrivia(change.NewCode);
                            editor.ReplaceNode(token.Parent, token.Parent.WithLeadingTrivia(newTriviaList));
                        }
                    }
                    else if (change.ChangeType == "method")
                    {
                        if (string.IsNullOrWhiteSpace(change.OriginalCode) || string.IsNullOrWhiteSpace(change.NewCode))
                        {
                            throw new ArgumentException("Original code or new code is null, empty, or consists only of whitespace.");
                        }

                        var methodDeclaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.GetText().ToString().Trim() == change.OriginalCode.Trim());

                        if (methodDeclaration == null)
                        {
                            throw new InvalidOperationException("Method not found.");
                        }

                        var newMethodDeclaration = SyntaxFactory.ParseMemberDeclaration(change.NewCode) as MethodDeclarationSyntax;
                        if (newMethodDeclaration == null)
                        {
                            throw new InvalidOperationException("New method code is not valid.");
                        }
                        editor.ReplaceNode(methodDeclaration, newMethodDeclaration);
                    }
                    else if (change.ChangeType == "class")
                    {
                        if (string.IsNullOrWhiteSpace(change.OriginalCode) || string.IsNullOrWhiteSpace(change.NewCode))
                        {
                            throw new ArgumentException("Original code or new code is null, empty, or consists only of whitespace.");
                        }
                        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.GetText().ToString().Trim() == change.OriginalCode.Trim());

                        if (classDeclaration == null)
                        {
                            throw new InvalidOperationException("Class not found.");
                        }

                        var newClassDeclaration = SyntaxFactory.ParseMemberDeclaration(change.NewCode) as ClassDeclarationSyntax;

                        if (newClassDeclaration == null)
                        {
                            throw new InvalidOperationException("New class code is not valid.");
                        }

                        editor.ReplaceNode(classDeclaration, newClassDeclaration);
                    }
                    else
                    {
                        throw new NotSupportedException($"Change type '{change.ChangeType}' is not supported.");
                    }

                    SyntaxNode newRoot = await editor.GetChangedDocument().GetSyntaxRootAsync();
                    File.WriteAllText(filePath, newRoot.ToFullString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error applying changes to '{change.FileName}': {ex.Message}");
                    continue;
                }

                

            }
        }

    }




}
