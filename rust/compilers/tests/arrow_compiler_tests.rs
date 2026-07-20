//! Ports of `ArrowGraphCompilerTests.cs` - cyclomatic complexity of the compiled
//! arrow graph.

use zametek_maths_graphs_compilers::ArrowGraphCompiler;
use zametek_maths_graphs_primitives::DependentActivity;

type Act = DependentActivity<i32, i32, i32>;

#[test]
fn cyclomatic_complexity_with_no_nodes_then_finds_zero() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.compile().unwrap();
    assert_eq!(compiler.cyclomatic_complexity(), 0);
}

#[test]
fn cyclomatic_complexity_in_one_network_then_as_expected() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [2]));
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 6);
}

#[test]
fn cyclomatic_complexity_in_three_networks_then_as_expected() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [1]));
    compiler.add_activity(Act::with_dependencies(5, 8, [2]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 3);
}

#[test]
fn cyclomatic_complexity_with_two_lone_nodes_then_as_expected() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [1]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 3);
}
