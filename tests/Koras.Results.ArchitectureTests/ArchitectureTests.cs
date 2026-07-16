using System.Reflection;
using Koras.Results.AspNetCore;
using Koras.Results.FluentValidation;
using Koras.Results.MediatR;
using Koras.Results.OpenTelemetry;
using NetArchTest.Rules;

namespace Koras.Results.ArchitectureTests;

/// <summary>
/// Enforces the architecture rules from docs/architecture/package-boundaries.md and
/// docs/architecture/dependency-rules.md. A failure here means a boundary was violated, not that
/// this test needs adjusting.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(Result).Assembly;
    private static readonly Assembly AspNetCoreAssembly = typeof(KorasResultsOptions).Assembly;
    private static readonly Assembly FluentValidationAssembly = typeof(ValidatorExtensions).Assembly;
    private static readonly Assembly MediatRAssembly = typeof(KorasResultsMediatRServiceCollectionExtensions).Assembly;
    private static readonly Assembly OpenTelemetryAssembly = typeof(KorasResultsActivityTags).Assembly;

    [Fact]
    public void Core_references_no_packages_beyond_the_base_class_library()
    {
        var references = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        // The zero-dependency promise (ADR-0001): only System.* / core runtime assemblies.
        Assert.All(references, name =>
            Assert.True(
                name.StartsWith("System", StringComparison.Ordinal)
                || name is "netstandard" or "mscorlib" or "Microsoft.CSharp",
                $"Koras.Results (core) must not reference '{name}'"));
    }

    [Theory]
    [InlineData("FluentValidation")]
    [InlineData("MediatR")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions")]
    public void Core_does_not_reference_integration_frameworks(string forbiddenPrefix)
    {
        var references = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(references, name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Satellites_do_not_reference_each_other_except_MediatR_to_FluentValidation()
    {
        AssertNoReference(AspNetCoreAssembly, "Koras.Results.FluentValidation");
        AssertNoReference(AspNetCoreAssembly, "Koras.Results.MediatR");
        AssertNoReference(AspNetCoreAssembly, "Koras.Results.OpenTelemetry");
        AssertNoReference(FluentValidationAssembly, "Koras.Results.AspNetCore");
        AssertNoReference(FluentValidationAssembly, "Koras.Results.MediatR");
        AssertNoReference(FluentValidationAssembly, "Koras.Results.OpenTelemetry");
        AssertNoReference(OpenTelemetryAssembly, "Koras.Results.AspNetCore");
        AssertNoReference(OpenTelemetryAssembly, "Koras.Results.FluentValidation");
        AssertNoReference(OpenTelemetryAssembly, "Koras.Results.MediatR");
        AssertNoReference(MediatRAssembly, "Koras.Results.AspNetCore");
        AssertNoReference(MediatRAssembly, "Koras.Results.OpenTelemetry");

        // The single audited exception:
        Assert.Contains(
            MediatRAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Koras.Results.FluentValidation");
    }

    [Fact]
    public void Every_satellite_references_the_core()
    {
        foreach (var assembly in new[] { AspNetCoreAssembly, FluentValidationAssembly, MediatRAssembly, OpenTelemetryAssembly })
        {
            Assert.Contains(assembly.GetReferencedAssemblies(), reference => reference.Name == "Koras.Results");
        }
    }

    [Fact]
    public void Public_namespaces_match_package_identity()
    {
        foreach (var (assembly, expectedRoot) in new[]
        {
            (CoreAssembly, "Koras.Results"),
            (AspNetCoreAssembly, "Koras.Results.AspNetCore"),
            (FluentValidationAssembly, "Koras.Results.FluentValidation"),
            (MediatRAssembly, "Koras.Results.MediatR"),
            (OpenTelemetryAssembly, "Koras.Results.OpenTelemetry"),
        })
        {
            var publicNamespaces = assembly.GetExportedTypes().Select(t => t.Namespace).Distinct().ToArray();
            Assert.All(publicNamespaces, ns =>
                Assert.True(
                    ns == expectedRoot || ns?.StartsWith(expectedRoot + ".", StringComparison.Ordinal) == true,
                    $"{assembly.GetName().Name}: public namespace '{ns}' must start with '{expectedRoot}'"));
        }
    }

    [Fact]
    public void Core_public_types_are_immutable()
    {
        // Precise reflection check: no public settable properties (init-only is immutable) and no
        // public mutable fields anywhere in the core's public surface.
        foreach (var type in CoreAssembly.GetExportedTypes().Where(t => !t.IsEnum))
        {
            var mutableProperties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Where(p => !IsInitOnly(p))
                .Select(p => $"{type.Name}.{p.Name}");

            var mutableFields = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsInitOnly)
                .Select(f => $"{type.Name}.{f.Name}");

            var offenders = mutableProperties.Concat(mutableFields).ToArray();
            Assert.True(offenders.Length == 0, "Mutable public core members: " + string.Join(", ", offenders));
        }

        static bool IsInitOnly(PropertyInfo property) =>
            property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit") == true;
    }

    [Fact]
    public void No_public_core_class_is_left_unsealed_except_Error()
    {
        var result = Types.InAssembly(CoreAssembly)
            .That().ArePublic().And().AreClasses().And().DoNotHaveName(nameof(Error))
            .Should().BeSealed().Or().BeStatic()
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Unsealed public core classes: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Error_subclasses_are_sealed()
    {
        var subclasses = CoreAssembly.GetExportedTypes()
            .Where(t => t.BaseType == typeof(Error))
            .ToArray();

        Assert.NotEmpty(subclasses);
        Assert.All(subclasses, t => Assert.True(t.IsSealed, $"{t.Name} must be sealed (ADR-0005)"));
    }

    [Fact]
    public void Extension_classes_follow_the_naming_convention()
    {
        foreach (var assembly in new[] { CoreAssembly, AspNetCoreAssembly, FluentValidationAssembly, OpenTelemetryAssembly })
        {
            var extensionClasses = assembly.GetExportedTypes()
                .Where(t => t.IsAbstract && t.IsSealed && t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false)))
                .ToArray();

            Assert.All(extensionClasses, t =>
                Assert.True(
                    t.Name.EndsWith("Extensions", StringComparison.Ordinal),
                    $"{t.FullName} contains extension methods but is not named *Extensions"));
        }
    }

    [Fact]
    public void Async_public_methods_have_the_Async_suffix()
    {
        foreach (var assembly in new[] { CoreAssembly, AspNetCoreAssembly, FluentValidationAssembly, MediatRAssembly, OpenTelemetryAssembly })
        {
            var offenders = assembly.GetExportedTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
                .Where(m => !m.Name.EndsWith("Async", StringComparison.Ordinal))
                .Where(m => m.Name != "Handle") // IPipelineBehavior.Handle is MediatR's contract
                .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
                .ToArray();

            Assert.True(offenders.Length == 0, "Task-returning members missing Async suffix: " + string.Join(", ", offenders));
        }
    }

    private static void AssertNoReference(Assembly assembly, string forbidden) =>
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == forbidden);
}
