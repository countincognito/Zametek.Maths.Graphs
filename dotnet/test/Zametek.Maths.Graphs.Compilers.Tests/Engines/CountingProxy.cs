using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Zametek.Maths.Graphs.Tests
{
    // Stands in for an engine (any interface), counts the calls that are made to it, and passes each call on to
    // the engine that it wraps. A test uses it to show that the engine that it injected is the engine that does
    // the work, without a spy class for every engine. It is public because DispatchProxy makes a type that
    // derives from it.
    public class CountingProxy
        : DispatchProxy
    {
        private object m_Inner;
        private int m_Calls;

        // The number of calls so far, from any thread.
        public int Calls => Volatile.Read(ref m_Calls);

        public static TEngine Wrap<TEngine>(TEngine inner, out CountingProxy counter)
            where TEngine : class
        {
            TEngine proxy = Create<TEngine, CountingProxy>();
            counter = (CountingProxy)(object)proxy;
            counter.m_Inner = inner;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Interlocked.Increment(ref m_Calls);
            try
            {
                return targetMethod.Invoke(m_Inner, args);
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
