//! The README example: three activities compiled with infinite resources.

use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::DependentActivity;

fn main() {
    let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, 6));
    compiler.add_activity(DependentActivity::new(2, 7));
    compiler.add_activity(DependentActivity::with_dependencies(3, 8, [1, 2]));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());
    let a3 = compilation
        .dependent_activities
        .iter()
        .find(|a| a.id() == 3)
        .unwrap();
    assert_eq!(a3.earliest_start_time, Some(7));
    assert_eq!(a3.earliest_finish_time(), Some(15));

    println!(
        "Compiled {} activities across {} resource schedules; finish time {}.",
        compilation.dependent_activities.len(),
        compilation.resource_schedules.len(),
        compiler.finish_time()
    );
}
