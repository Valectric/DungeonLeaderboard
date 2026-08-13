using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Dungeon.Editor
{
    /// <summary>
    /// Removes test-only assemblies from player builds.
    /// </summary>
    /// <remarks>
    /// MooseRunner's <c>MooseRunner.helper</c> assembly is a Runtime assembly with no define
    /// constraints that lists <c>nunit.framework.dll</c> as a precompiled reference. It is therefore
    /// compiled into player builds, where nunit does not exist, and the IL2CPP linker aborts with
    /// "Failed to resolve assembly: 'nunit.framework'". Nothing in a shipped game references it —
    /// only tests do — so dropping it from the build is safe and keeps the player smaller.
    /// <para>
    /// Matched <b>case-insensitively</b> on purpose: the package ships
    /// <c>MooseRunner.Helpers.Runtime.dll</c>, whose casing differs from its asmdef name, and an
    /// exact-case filter silently misses it. The whole family is excluded because
    /// <c>MooseRunner.Internal</c> references the helper, so dropping only the helper leaves an
    /// unresolvable reference behind.
    /// </para>
    /// This cost a day in the sister project. Remove it only once the package constrains that
    /// assembly to test builds.
    /// </remarks>
    public sealed class BuildAssemblyFilter : IFilterBuildAssemblies
    {
        /// <summary>Runs early; the order relative to other filters does not matter here.</summary>
        public int callbackOrder => 0;

        /// <summary>
        /// Assembly name fragments that must never reach a player build.
        /// </summary>
        /// <remarks>
        /// <c>MCPForUnity</c> is here for the same reason as MooseRunner, and was added the moment
        /// the package was installed rather than after a build failed. It ships a
        /// <c>MCPForUnity.Runtime</c> assembly with <c>includePlatforms: []</c>, so Unity compiles
        /// editor-automation code into the shipped game -- code full of reflection-based version
        /// compat shims, which is exactly what managed stripping and IL2CPP handle worst. Nothing in
        /// this game references it.
        /// </remarks>
        private static readonly string[] Excluded =
        {
            "MooseRunner",
            "MCPForUnity",
            "nunit.framework",
            ".Tests"
        };

        /// <summary>
        /// Filters the assembly list Unity is about to build into the player.
        /// </summary>
        /// <param name="buildOptions">Options for the build in progress.</param>
        /// <param name="assemblies">Assembly paths Unity intends to include.</param>
        /// <returns>The assemblies to actually include.</returns>
        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            string[] kept = assemblies
                .Where(path => !Excluded.Any(
                    fragment => path.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();

            int dropped = assemblies.Length - kept.Length;
            if (dropped > 0)
            {
                Debug.Log($"[Dungeon] Excluded {dropped} test assembly/assemblies from the build");
            }

            return kept;
        }
    }
}
