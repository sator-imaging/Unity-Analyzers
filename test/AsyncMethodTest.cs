#define __expected_errors

using System;
using Object = UnityEngine.Object;

#if __expected_errors
using System.Threading.Tasks;
using UnityEngine;
#endif

//#pragma warning disable SIUA001  // Suppress SIUA001
//#pragma warning disable SIUA002  // Suppress SIUA002

#pragma warning disable IDE0002  // Simplify member access
#pragma warning disable IDE0052  // Remove unread private member
#pragma warning disable IDE0300  // Use collection expression for array
#pragma warning disable IDE0011  // Add braces
#pragma warning disable IDE0059  // Remove unnecessary value assignment
#pragma warning disable IDE0200  // Remove unnecessary lambda expression
#pragma warning disable IDE0078  // Use pattern matching
#pragma warning disable IDE0130  // Namespace does not match folder structure
#pragma warning disable CA1822  // Mark members as static
#pragma warning disable CA1050  // Declare types in namespaces
#pragma warning disable CA1816  // Call GC.SuppressFinalize correctly

namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component
    {
        public bool IsEnabled { get; set; }
        public string Name = string.Empty;
        public void Foo() { }
        public async Task<bool> FooAsync() => false;
        public MonoBehaviour GetSelf() => this;
        public async Task<MonoBehaviour> GetSelfAsync() => this;
        public event Action? OnChanged;
        public Object this[int index] { get => this; set { } }
        public Object this[string name] { get => this; set { } }
        public static MonoBehaviour Create() => new();
        public static async Task<MonoBehaviour> CreateAsync() => new();
    }
}


#if __expected_errors

public class Behaviour : MonoBehaviour
{
    private bool m_boolValue;
    private MonoBehaviour? m_unityObj;

    public async void Receiverless() => await FooAsync();   // OK: first await
    public async void ThisCall() => await this.FooAsync();  // OK: first await

    public async void ReceiverlessMultiple()
    {
        await FooAsync();
        await FooAsync();  // Error: 2nd await call
    }

    public async void ThisCallMultiple()
    {
        await this.FooAsync();
        await this.FooAsync();  // Error: 2nd await call
    }

    public async void ThisAccessOnBothSide()
    {
        m_unityObj = await this.GetSelfAsync();
        m_unityObj = await this.GetSelfAsync();  // Error: expect both lhs and rhs got error
                                                 // TODO: suppress lhs error IF both side get error
                                                 //       note that lhs has access to 'this' so it must get error
                                                 //       don't try to 'always suppress lhs in any case'
    }

    public async void ReceiverlessAssign() => m_boolValue = await FooAsync();   // TODO: should not emit error
    public async void ThisCallAssign() => m_boolValue = await this.FooAsync();  // TODO: should not emit error
}

public class BehaviourOther : MonoBehaviour, IDisposable
{
    public async Task This()
    {
        this.Foo();

        await Task.Delay(1);
        this.Foo();  // Error
    }

    public void Dispose() { }
}

namespace SampleConsumer
{
    internal class PureCSharpDirectMethodCallOnNullable
    {
        public async Task Test()
        {
            ((Behaviour?)null)?.Foo();

            await Task.Delay(1);
            ((Behaviour?)null)?.Foo();  // Error
        }
    }

    internal class PureCSharpNullableMethod
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            nullable?.Foo();

            await Task.Delay(1);
            nullable?.Foo();  // Error
        }
    }

    internal class PureCSharpNullablePropertyGet
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            _ = nullable?.Name;

            await Task.Delay(1);
            _ = nullable?.Name;  // Error
        }
    }

    internal class PureCSharpNullablePropertySet
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            nullable?.Name = "Test";

            await Task.Delay(1);
            nullable?.Name = "Test";  // Error
        }
    }

    internal class PureCSharpDirectMethodCallOnNew
    {
        public async Task Test()
        {
            (new Behaviour()).Foo();

            await Task.Delay(1);
            (new Behaviour()).Foo();  // Error
        }
    }

    internal class PureCSharpEvent
    {
        public async Task Test()
        {
            using var other = new BehaviourOther();
            other.OnChanged += () => { };
            other.OnChanged += other.Foo;
            other.OnChanged += () => other.Foo();

            await Task.Delay(1);
            other.OnChanged += () => { };  // Error
            other.OnChanged += other.Foo;  // Error: both lhs and rhs
            other.OnChanged += () => other.Foo();  // Error: both lhs and rhs
        }
    }

    internal class PureCSharpPropertySet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            behaviour.Name = "Test";

            await Task.Delay(1);
            behaviour.Name = "Test";  // Error
        }
    }

    internal class PureCSharpPropertyGet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            _ = behaviour.Name;

            await Task.Delay(1);
            _ = behaviour.Name;  // Error
        }
    }

    internal class PureCSharpIndexerSet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            _ = behaviour[0] = null!;

            await Task.Delay(1);
            _ = behaviour[0] = null!;  // Error
        }
    }

    internal class PureCSharpIndexerGet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            _ = behaviour[0];

            await Task.Delay(1);
            _ = behaviour[0];  // Error
        }
    }

    internal class PureCSharpIndexerStringSet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            _ = behaviour["Test"] = null!;

            await Task.Delay(1);
            _ = behaviour["Test"] = null!;  // Error
        }
    }

    internal class PureCSharpIndexerStringGet
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            _ = behaviour["Test"];

            await Task.Delay(1);
            _ = behaviour["Test"];  // Error
        }
    }

    internal class PureCSharpPropertyBoolean
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            behaviour.IsEnabled = true;

            await Task.Delay(1);
            behaviour.IsEnabled = true;  // Error
        }
    }

    internal class PureCSharpMethodCall
    {
        readonly Behaviour behaviour = new();
        public async Task Test()
        {
            behaviour.Foo();

            await Task.Delay(1);
            behaviour.Foo();  // Error
        }
    }

    internal class PureCSharp_NoError
    {
        public string Test()  // OK: not async method
        {
            var b = new Behaviour();
            b.Name = "Test";
            b.IsEnabled = true;
            b.Foo();

            return "Ok";
        }
    }

    internal class PureCSharp_Errors
    {

        readonly static Behaviour _instance = new();
        readonly static Behaviour[] _instances = new Behaviour[] { new() };

        public async Task AwaitWithoutBlock()
        {
            foreach (var x in _instances)
                await x.FooAsync();   // Error: first await but in the block

            _instance.Name = "Test";  // Error
        }

        public async Task AwaitInBlock()
        {
            foreach (var x in _instances)
            {
                x.Name = "Test";      // Error: before first await but in the loop block
                x.IsEnabled = true;   // Error
                x.Foo();              // Error
                await Task.Delay(1);
            }
        }

        public async Task AwaitInUnnecessaryBlock()
        {

            {
                _instance.Name = "Test";      // Error: before first await but in the block
                _instance.IsEnabled = true;   // Error
                _instance.Foo();              // Error
                await Task.Delay(1);
            }
        }

        public async Task AwaitInWhile()
        {
            while (await _instance.FooAsync())  // Error: in the statement
            {
            }

            _instance.Name = "Test";  // Error
        }

        public async Task AwaitInIf()
        {
            if (await _instance.FooAsync())  // Error: in the statement
            {
            }

            _instance.Name = "Test";  // Error
        }

        public async Task AwaitInSwitch()
        {
            switch (await _instance.FooAsync())  // Error: in the statement
            {
                default:
                    break;
            }

            _instance.Name = "Test";  // Error
        }

        public async ValueTask AwaitSwitchCase()
        {
            switch (true)
            {
                default:
                    _instance.Name = "Test";     // Error: before first await but in the case block
                    await _instance.FooAsync();  // Error
                    break;
            }

        }


        public async Task AwaitMethodArguments()
        {
            await WithArgOmittable(_instance);  // OK: first await
            await WithArgOmittable(_instance);  // Error

            if (_instance != null)
            {
                await StaticAsync();  // Warning
            }
        }

        public async Task AwaitMethodArguments2()
        {
            await StaticAsync();
            await WithArgOmittable(_instance);  // Error

            MonoBehaviour.Create();
            await MonoBehaviour.CreateAsync();

            await this.WithArgOmittable();      // OK: omitted
            await this.WithArgOmittable(null);  // OK: null
            await this.WithArgOmittable(new Behaviour());  // Error

            await WithArgStatic(_instance);  // Error

            if (_instance != null)
            {
                await WithArgStatic(_instance);  // Warning
                await WithArgOmittable();  // Warning
            }
        }

        static async Task StaticAsync() { }
        static async Task<Object> WithArgStatic(Behaviour behaviour) => behaviour;
        private async Task<Object?> WithArgOmittable(Behaviour? behaviour = null) => behaviour;

        private bool m_boolValue;

        public async Task FirstAwaitDetectionAlpha()
        {
            MonoBehaviour? foo = new();
            foo = await MonoBehaviour.CreateAsync();  // OK: static factory
            foo = await MonoBehaviour.CreateAsync();  // OK
            var n = await foo.FooAsync();  // Error
        }

        public async Task FirstAwaitDetectionBravo()
        {
            MonoBehaviour? foo = new();
            var x = await foo.FooAsync();  // OK: first assignment
            var y = await foo.FooAsync();  // Error
        }

        public async Task FirstAwaitDetectionCharlie()
        {
            MonoBehaviour? foo = new();
            bool value;
            value = await foo.FooAsync();  // OK: first assignment without declaration
            value = await foo.FooAsync();  // Error
        }

        public async Task FirstAwaitDetectionDelta()
        {
            MonoBehaviour? foo = new();
            m_boolValue = await foo.FooAsync();  // OK: first assignment to field
            m_boolValue = await foo.FooAsync();  // Error
        }

        public async Task<bool> FirstAwaitDetectionReturn()
        {
            MonoBehaviour? foo = new();
            if (m_boolValue)
            {
                return await foo.FooAsync();  // OK: first 'return await'
            }
            else
            {
                return await foo.FooAsync();  // Error
            }
        }

        public async Task<bool> FirstAwaitDetectionReturnDeep()
        {
            MonoBehaviour? foo = new();
            try
            {
                try
                {
                    try
                    {
                        return await foo.FooAsync();  // OK: first 'return await'
                    }
                    catch
                    {
                        return await foo.FooAsync();  // Error
                    }
                }
                catch
                {
                    return await foo.FooAsync();  // Error
                }
            }
            catch
            {
                return await foo.FooAsync();  // Error
            }
        }

        public async Task FirstAwaitInRootTry()
        {
            MonoBehaviour? foo = new();
            try
            {
                await foo.FooAsync();
                await foo.FooAsync();  // Error
            }
            finally
            {
            }
        }

        public async Task FirstVarAwaitInRootTry()
        {
            MonoBehaviour? foo = new();
            try
            {
                var x = await foo.FooAsync();
                var y = await foo.FooAsync();  // Error
            }
            finally
            {
            }
        }

        public async Task FirstEqualAwaitInRootTry()
        {
            MonoBehaviour? foo = new();
            try
            {
                m_boolValue = await foo.FooAsync();
                m_boolValue = await foo.FooAsync();  // Error
            }
            finally
            {
            }
        }

        public async Task TestAsync()
        {
            var behaviour = new Behaviour();
            behaviour.Name = "Test";     // OK: before first await
            behaviour.IsEnabled = true;  // OK
            behaviour.Foo();             // OK

            await Task.CompletedTask;
            behaviour.Name = "Test";     // Error: after first await (actually, awaiting Task.CompletedTask is safe but no way to detect it)
            behaviour.IsEnabled = true;  // Error
            behaviour.Foo();             // Error

            if (behaviour != null)  // OK
            {
                behaviour.Name = "Test";
                behaviour.IsEnabled = true;
                behaviour.Foo();

                await TestAsync();  // Warning: don't allow await in safe block at all
                                    //          even if it is called at the end of block
            }
            else
            {
                behaviour.Name = "Test";  // Error

                await TestAsync();  // TODO: should emit error?
            }

            var arr = new Behaviour[] { new() };

            if (arr[0] != null)  // OK
            {
                arr[0].Name = "Test";
                arr[0].IsEnabled = true;
            }

            await TestAsync();  // OK: can call itself

            if (behaviour != null && ((behaviour != null)))  // OK: parentheses are handled
            {
                behaviour.Name = "Test";
                behaviour.IsEnabled = true;
                behaviour.Foo();
            }

            if (behaviour != null && behaviour is not null)  // Error: only allow '!= null' and '&&'
            {
                behaviour.Name = "Test";     // Error
                behaviour.IsEnabled = true;  // Error
            }

            if (behaviour != null && arr != null)  // Error: don't allow combining non-Unity object checks
            {
                behaviour.Name = "Test";     // Error
                behaviour.IsEnabled = true;  // Error
            }

            if (PureCSharp_Errors._instance != null)
            {
                _instance.Name = "Test";
                _instance.IsEnabled = true;

                behaviour.Name = "Test";  // TODO: should emit error
            }

            behaviour.Name = "Test";     // Error
            behaviour.IsEnabled = true;  // Error
            behaviour.Foo();             // Error

            if (behaviour.GetSelf() != null)  // Error: don't allow member acccess in if statement
                                              //        even through it's valid
            {
                _ = behaviour.GetSelf();  // Error: due to if statement contains violation (not perfect)
            }
        }
    }
}

#endif
