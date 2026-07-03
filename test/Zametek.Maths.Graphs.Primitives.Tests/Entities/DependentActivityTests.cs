using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DependentActivityTests
    {
        [Fact]
        public void DependentActivity_GivenCtor_ThenDependencySetsEmpty()
        {
            var activity = new DependentActivity<int, int, int>(1, 5);

            activity.Dependencies.ShouldBeEmpty();
            activity.PlanningDependencies.ShouldBeEmpty();
            activity.ResourceDependencies.ShouldBeEmpty();
            activity.Successors.ShouldBeEmpty();
        }

        [Fact]
        public void DependentActivity_GivenCtor_WithDependencies_ThenDependenciesCopied()
        {
            var activity = new DependentActivity<int, int, int>(3, 8, [1, 2]);

            activity.Dependencies.ShouldBe([1, 2], ignoreOrder: true);
        }

        [Fact]
        public void DependentActivity_GivenCtor_WithNullDependencies_ThenThrowsArgumentNullException()
        {
            Action act = () => new DependentActivity<int, int, int>(1, 5, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DependentActivity_GivenCloneObject_ThenDependencySetsPreserved()
        {
            var activity = new DependentActivity<int, int, int>(3, 8, [1, 2]);
            activity.PlanningDependencies.Add(4);
            activity.ResourceDependencies.Add(5);
            activity.Successors.Add(6);

            var clone = (DependentActivity<int, int, int>)activity.CloneObject();

            clone.Dependencies.ShouldBe([1, 2], ignoreOrder: true);
            clone.PlanningDependencies.ShouldBe([4]);
            clone.ResourceDependencies.ShouldBe([5]);
            clone.Successors.ShouldBe([6]);
        }

        [Fact]
        public void DependentActivity_GivenCloneObject_ThenDependencySetsAreIndependentCopies()
        {
            var activity = new DependentActivity<int, int, int>(3, 8, [1]);

            var clone = (DependentActivity<int, int, int>)activity.CloneObject();
            clone.Dependencies.Add(2);

            activity.Dependencies.Count.ShouldBe(1);
            clone.Dependencies.Count.ShouldBe(2);
        }

        [Fact]
        public void DependentActivity_GivenCloneObject_ThenBaseActivityPropertiesPreserved()
        {
            var activity = new DependentActivity<int, int, int>(3, 8)
            {
                Name = "name",
                EarliestStartTime = 1,
                LatestFinishTime = 12,
                FreeSlack = 2,
            };
            activity.SetAsRemovable();

            var clone = (DependentActivity<int, int, int>)activity.CloneObject();

            clone.Name.ShouldBe("name");
            clone.EarliestStartTime.ShouldBe(1);
            clone.LatestFinishTime.ShouldBe(12);
            clone.FreeSlack.ShouldBe(2);
            clone.CanBeRemoved.ShouldBeTrue();
        }
    }
}
