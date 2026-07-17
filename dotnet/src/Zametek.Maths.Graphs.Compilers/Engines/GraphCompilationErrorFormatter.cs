using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zametek.Maths.Graphs
{
    // Stateless helper that builds human-readable error messages for graph compilation
    // errors. Extracted from VertexGraphCompiler so the compiler stays a thin coordinator.
    internal static class GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        internal static string BuildInvalidDependenciesErrorMessage(
            List<T> invalidDependencies,
            List<TDependentActivity> activities)
        {
            if (invalidDependencies == null || invalidDependencies.Count == 0
                || activities == null || activities.Count == 0)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_InvalidDependencies}");
            foreach (T invalidDependency in invalidDependencies)
            {
                IList<T> actsWithInvalidDeps = activities
                    .Where(x => x.Dependencies.Union(x.PlanningDependencies).Contains(invalidDependency))
                    .Select(x => x.Id)
                    .OrderBy(x => x)
                    .ToList();
                output.AppendLine($@"{invalidDependency} {Properties.Resources.Message_IsInvalidButReferencedBy} {string.Join(@", ", actsWithInvalidDeps)}");
            }
            return output.ToString();
        }

        internal static string BuildCircularDependenciesErrorMessage(List<ICircularDependency<T>> circularDependencies)
        {
            if (circularDependencies == null || circularDependencies.Count == 0)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_CircularDependencies}");
            foreach (ICircularDependency<T> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        internal static string BuildInvalidConstraintsErrorMessage(List<IInvalidConstraint<T>> invalidConstraints)
        {
            if (invalidConstraints == null || invalidConstraints.Count == 0)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_InvalidConstraints}");
            foreach (IInvalidConstraint<T> invalidConstraint in invalidConstraints)
            {
                output.AppendLine($@"{invalidConstraint.Id} -> {invalidConstraint.Message}");
            }
            return output.ToString();
        }

        internal static string BuildUnavailableResourcesErrorMessage(List<IUnavailableResources<T, TResourceId>> unavailableResourceSet)
        {
            if (unavailableResourceSet == null || unavailableResourceSet.Count == 0)
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_UnavailableResources}");
            foreach (IUnavailableResources<T, TResourceId> unavailableResources in unavailableResourceSet)
            {
                output.AppendLine($@"{unavailableResources.Id} -> {string.Join(@", ", unavailableResources.ResourceIds.OrderBy(x => x))}");
            }
            return output.ToString();
        }
    }
}
