using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodebaseAnalysisLib
{
    public class CodebaseInfo
    {
        public CodebaseInfo()
        {
            DiagnosticsByProject = new Dictionary<string, List<Diagnostic>>();
            ProjectNames = new List<string>();
            ClassNames = new Dictionary<string, List<string>>();
            ClassMethods = new Dictionary<string, List<string>>();
            ClassProperties = new Dictionary<string, List<string>>();
            ClassFields = new Dictionary<string, List<string>>();
            ClassEvents = new Dictionary<string, List<string>>();
            ClassAttributes = new Dictionary<string, List<string>>();
            ClassRelationships = new Dictionary<string, List<string>>();
            FullCodeOfClass = new Dictionary<string, string>();
            FullCodeOfMethods = new Dictionary<string, string>();
        }

        public List<string> ProjectNames { get; set; }
        public Dictionary<string, List<Diagnostic>> DiagnosticsByProject { get; set; }
        public Dictionary<string, List<string>> ClassNames { get; set; }
        public Dictionary<string, List<string>> ClassMethods { get; set; }
        public Dictionary<string, List<string>> ClassProperties { get; set; }
        public Dictionary<string, List<string>> ClassFields { get; set; }
        public Dictionary<string, List<string>> ClassEvents { get; set; }
        public Dictionary<string, List<string>> ClassAttributes { get; set; }
        public Dictionary<string, List<string>> ClassRelationships { get; set; }
        public Dictionary<string, string> FullCodeOfClass { get; set; }
        public Dictionary<string, string> FullCodeOfMethods { get; set; }
        // Add ClassDependencies and MethodDependencies properties
        public Dictionary<string, List<string>> ClassDependencies { get; set; } = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> MethodDependencies { get; set; } = new Dictionary<string, List<string>>();

        // Continue with the ToString() method
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var projectName in ProjectNames.Distinct())
            {
                sb.AppendLine($"Project: {projectName}");

                if (DiagnosticsByProject.ContainsKey(projectName) && DiagnosticsByProject[projectName].Any())
                {
                    sb.AppendLine("  Diagnostics:");
                    foreach (var diagnostic in DiagnosticsByProject[projectName])
                    {
                        sb.AppendLine($"    - {diagnostic.Id}: {diagnostic.GetMessage()}");
                    }
                }

                foreach (var className in ClassNames[projectName])
                {
                    if (FullCodeOfClass.ContainsKey(className))
                    {
                        sb.AppendLine(FullCodeOfClass[className]);
                        continue;
                    }

                    sb.AppendLine($"  Class: {className}");

                    if (ClassRelationships.ContainsKey(className) && ClassRelationships[className].Count > 0)
                    {
                        sb.Append("    Inherits/Implements: ");
                        sb.AppendLine(string.Join(", ", ClassRelationships[className]));
                    }

                    if (ClassProperties.ContainsKey(className) && ClassProperties[className].Any())
                    {
                        sb.AppendLine("    Properties:");
                        foreach (var propertyName in ClassProperties[className])
                        {
                            sb.AppendLine($"      - {propertyName}");
                        }
                    }

                    if (ClassFields.ContainsKey(className) && ClassFields[className].Any())
                    {
                        sb.AppendLine("    Fields:");
                        foreach (var fieldName in ClassFields[className])
                        {
                            sb.AppendLine($"      - {fieldName}");
                        }
                    }

                    if (ClassEvents.ContainsKey(className) && ClassEvents[className].Any())
                    {
                        sb.AppendLine("    Events:");
                        foreach (var eventName in ClassEvents[className])
                        {
                            sb.AppendLine($"      - {eventName}");
                        }
                    }

                    if (ClassAttributes.ContainsKey(className) && ClassAttributes[className].Any())
                    {
                        sb.AppendLine("    Attributes:");
                        foreach (var attributeName in ClassAttributes[className])
                        {
                            sb.AppendLine($"      - {attributeName}");
                        }
                    }

                    if (ClassMethods[className].Any())
                    {
                        sb.AppendLine($"    Methods: ");
                        foreach (var methodName in ClassMethods[className])
                        {
                            if (FullCodeOfMethods.ContainsKey(methodName))
                            {
                                sb.AppendLine(FullCodeOfMethods[methodName]);
                                continue;
                            }
                            sb.AppendLine($"    - {methodName}");
                        }
                    }
                    if (ClassDependencies.ContainsKey(className) && ClassDependencies[className].Any())
                    {
                        sb.AppendLine("    Class Dependencies:");
                        foreach (var dependency in ClassDependencies[className])
                        {
                            sb.AppendLine($"      - {dependency}");
                        }
                    }

                    if (ClassMethods[className].Any())
                    {
                        sb.AppendLine($"    Methods: ");
                        foreach (var methodName in ClassMethods[className])
                        {
                            if (FullCodeOfMethods.ContainsKey(methodName))
                            {
                                sb.AppendLine(FullCodeOfMethods[methodName]);
                                continue;
                            }

                            sb.AppendLine($"    - {methodName}");

                            if (MethodDependencies.ContainsKey(methodName) && MethodDependencies[methodName].Any())
                            {
                                sb.AppendLine("      Method Dependencies:");
                                foreach (var dependency in MethodDependencies[methodName])
                                {
                                    sb.AppendLine($"        - {dependency}");
                                }
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
