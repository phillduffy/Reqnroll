﻿using Reqnroll.FeatureSourceGenerator.CSharp;

namespace Reqnroll.FeatureSourceGenerator.XUnit;

/// <summary>
/// The handler for xUnit.
/// </summary>
public class XUnitHandler : ITestFrameworkHandler
{
    public string FrameworkName => "xUnit";

    public bool CanGenerateForCompilation(CompilationInformation compilationInformation) => 
        compilationInformation is CSharpCompilationInformation;

    public ITestFixtureGenerator GetTestFixtureGenerator(CompilationInformation compilation)
    {
        return compilation switch
        {
            CSharpCompilationInformation => new XUnitCSharpTestFixtureGenerator(),
            _ => throw new NotSupportedException(),
        };
    }

    public bool IsTestFrameworkReferenced(CompilationInformation compilationInformation)
    {
        return compilationInformation.ReferencedAssemblies
            .Any(assembly => assembly.Name == "xunit.core");
    }
}
